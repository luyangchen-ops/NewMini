using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class EnemyAgent : MonoBehaviour
{
    public enum EnemyVisualStyle
    {
        Auto,
        Swordsman,
        Archer,
        ShieldBearer,
        Spearman
    }

    /// <summary>Animation integration point; spear animation assets are intentionally not assigned yet.</summary>
    public enum EnemyAnimationState { Idle, Move, Attack, Death }

    [SerializeField] private EnemyData data;
    [Header("Visuals")]
    [SerializeField] private EnemyVisualStyle visualStyle = EnemyVisualStyle.Auto;
    [SerializeField] private Transform target;
    [SerializeField] private string fallbackTargetName = "Player";
    [SerializeField] private Camera worldCamera;
    [SerializeField] private Animator visualAnimator;
    [SerializeField] private SpriteRenderer visualRenderer;
    [SerializeField, Min(0f)] private float boundaryPadding = 0.5f;

    private Rigidbody2D body;
    private Vector2 desiredVelocity;
    private float nextTargetSearchTime;
    private float fireCooldown;
    private float meleePerfectDodgeStartTime;
    private float meleePerfectDodgeEndTime;
    private EnemyStateMachine stateMachine;

    private static readonly int Attack = Animator.StringToHash("Attack");
    private static readonly int Shoot = Animator.StringToHash("Shoot");
    private static readonly int IsMoving = Animator.StringToHash("IsMoving");
    private static readonly int IsRunning = Animator.StringToHash("IsRunning");

    private bool supportsAttack;
    private bool supportsShoot;
    private bool supportsIsMoving;
    private bool supportsIsRunning;

    public EnemyData Data => data;
    public Rigidbody2D Body => body;
    public Transform Target => target;
    public bool HasTarget => target != null;
    public bool CanFire => data != null && data.ProjectilePrefab != null && fireCooldown <= 0f;
    public bool CanMeleeAttack => data != null && data.Archetype == EnemyArchetype.Melee && fireCooldown <= 0f;
    public bool CanSpearAttack => data != null && data.Archetype == EnemyArchetype.Spearman && fireCooldown <= 0f;
    public bool IsMeleeCombatant => data != null && (data.Archetype == EnemyArchetype.Melee || data.Archetype == EnemyArchetype.Spearman);
    public bool IsShieldBearer => GetVisualStyle() == EnemyVisualStyle.ShieldBearer;
    public bool IsShieldAttackExposed { get; private set; }
    public bool IsMeleeAttackPerfectDodgeable => data != null
        && data.Archetype == EnemyArchetype.Melee
        && Time.time >= meleePerfectDodgeStartTime
        && Time.time <= meleePerfectDodgeEndTime;
    public EnemyIdleState IdleState { get; private set; }
    public EnemyState DefaultActiveState => IsMeleeCombatant
        ? IsShieldBearer ? shieldGuardState : chaseState
        : roamState;
    public EnemyRoamState RoamState => roamState;
    public EnemyAttackState AttackState => attackState;
    public EnemyShieldAttackState ShieldAttackState => shieldAttackState;
    public event System.Action<EnemyAnimationState> AnimationStateChanged;

    private EnemyChaseState chaseState;
    private EnemyRoamState roamState;
    private EnemyAttackState attackState;
    private EnemyShieldGuardState shieldGuardState;
    private EnemyShieldBlockState shieldBlockState;
    private EnemyShieldAttackState shieldAttackState;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        worldCamera ??= Camera.main;
        visualAnimator ??= GetComponentInChildren<Animator>();
        visualRenderer ??= GetComponentInChildren<SpriteRenderer>();
        EnsureVisualAnimator();
        CacheAnimatorParameters();

        stateMachine = new EnemyStateMachine();
        IdleState = new EnemyIdleState(this, stateMachine);
        chaseState = new EnemyChaseState(this, stateMachine);
        roamState = new EnemyRoamState(this, stateMachine);
        attackState = new EnemyAttackState(this, stateMachine);
        shieldGuardState = new EnemyShieldGuardState(this, stateMachine);
        shieldBlockState = new EnemyShieldBlockState(this, stateMachine);
        shieldAttackState = new EnemyShieldAttackState(this, stateMachine);
        TryFindTarget();
        target?.GetComponentInParent<PlayerCharacterController>()?.IgnoreEnemyCollisions(this);
        fireCooldown = data != null ? Random.Range(0.1f, data.FireInterval) : 0f;
        stateMachine.ChangeState(IdleState);
    }

    private void Update()
    {
        if (data == null)
        {
            return;
        }

        if (visualAnimator != null)
        {
            visualAnimator.speed = PlayerCharacterController.EnemyTimeScale;
        }

        if (!HasTarget && Time.time >= nextTargetSearchTime)
        {
            TryFindTarget();
        }

        fireCooldown = Mathf.Max(0f, fireCooldown - Time.deltaTime * PlayerCharacterController.EnemyTimeScale);
        if (PlayerCharacterController.EnemyTimeScale > 0f)
        {
            stateMachine.Tick();
        }
    }

    private void FixedUpdate()
    {
        if (data == null)
        {
            return;
        }

        stateMachine.FixedTick();
        Vector2 currentPosition = body.position;
        Vector2 clampedPosition = CameraBounds.Clamp(worldCamera, currentPosition, boundaryPadding, transform.position.z);
        if ((clampedPosition - currentPosition).sqrMagnitude > 0.000001f)
        {
            body.position = clampedPosition;
        }

        Vector2 clampedNext = CameraBounds.Clamp(
            worldCamera,
            clampedPosition + desiredVelocity * (PlayerCharacterController.EnemyTimeScale * Time.fixedDeltaTime),
            boundaryPadding,
            transform.position.z);
        body.linearVelocity = (clampedNext - clampedPosition) / Time.fixedDeltaTime;
    }

    public void SetDesiredVelocity(Vector2 velocity)
    {
        desiredVelocity = velocity;
        Face(velocity.x);
        SetMovementAnimation(velocity.sqrMagnitude > .0001f);
    }

    public Vector2 GetMeleeFormationMoveDirection(out bool isAtFormation)
    {
        isAtFormation = false;
        if (!HasTarget || data == null || !IsMeleeCombatant) return Vector2.zero;

        int formationCount = 0;
        int formationIndex = 0;
        EntityId ownEntityId = GetEntityId();
        foreach (EnemyAgent other in FindObjectsByType<EnemyAgent>(FindObjectsInactive.Exclude))
        {
            if (other == null || other.data == null || !other.IsMeleeCombatant
                || other.target != target) continue;

            formationCount++;
            if (other.GetEntityId() < ownEntityId) formationIndex++;
        }

        if (formationCount == 0) return Vector2.zero;

        float angle = formationIndex * Mathf.PI * 2f / formationCount;
        Vector2 formationPosition = (Vector2)target.position
            + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * data.MeleeFormationRadius;
        Vector2 toFormation = formationPosition - body.position;
        isAtFormation = toFormation.sqrMagnitude
            <= data.MeleeFormationArrivalDistance * data.MeleeFormationArrivalDistance;

        Vector2 separation = CalculateMeleeSeparation();
        Vector2 formationDirection = isAtFormation ? Vector2.zero : toFormation.normalized;
        Vector2 steering = formationDirection + separation * data.MeleeSeparationStrength;
        return steering.sqrMagnitude > .0001f ? steering.normalized : Vector2.zero;
    }

    private Vector2 CalculateMeleeSeparation()
    {
        float radius = data.MeleeSeparationRadius;
        Vector2 separation = Vector2.zero;
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(body.position, radius))
        {
            EnemyAgent other = hit.GetComponentInParent<EnemyAgent>();
            if (other == null || other == this || other.data == null
                || !other.IsMeleeCombatant || other.target != target) continue;

            Vector2 offset = body.position - other.body.position;
            float distance = offset.magnitude;
            if (distance <= .0001f)
            {
                float angle = unchecked((uint)GetEntityId().GetHashCode()) / (float)uint.MaxValue * Mathf.PI * 2f;
                separation += new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                continue;
            }

            separation += offset / distance * Mathf.Clamp01(1f - distance / radius);
        }

        return separation;
    }

    public void FireProjectile()
    {
        if (!HasTarget || !CanFire)
        {
            return;
        }

        Vector2 direction = (Vector2)target.position - body.position;
        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        direction.Normalize();
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Face(direction.x);
        EnemyProjectile projectile = Instantiate(
            data.ProjectilePrefab,
            body.position + direction * data.ProjectileSpawnOffset,
            Quaternion.Euler(0f, 0f, angle));
        projectile.Launch(direction, data.ProjectileSpeed, gameObject, data.Damage);
        fireCooldown = data.FireInterval;
    }

    public void PerformMeleeAttack()
    {
        if (!CanMeleeAttack)
        {
            return;
        }

        if (HasTarget)
        {
            Face(target.position.x - transform.position.x);
        }

        meleePerfectDodgeStartTime = Time.time + data.MeleePerfectDodgeDelay;
        meleePerfectDodgeEndTime = meleePerfectDodgeStartTime + data.MeleePerfectDodgeDuration;
        if (supportsAttack)
        {
            visualAnimator.SetTrigger(Attack);
        }
        target?.GetComponentInParent<PlayerCharacterController>()?.TakeDamage(data.Damage);
        fireCooldown = data.FireInterval;
    }

    public void BeginSpearAttack()
    {
        SetDesiredVelocity(Vector2.zero);
        FaceTarget();
        SetAnimationState(EnemyAnimationState.Attack);
        if (supportsAttack)
        {
            visualAnimator.ResetTrigger(Attack);
            visualAnimator.SetTrigger(Attack);
        }
    }

    public void BeginSpearThrust(Vector2 direction)
    {
        Face(direction.x);
        meleePerfectDodgeStartTime = Time.time;
        meleePerfectDodgeEndTime = Time.time + data.MeleePerfectDodgeDuration;
    }

    public void TryHitWithSpear(Vector2 direction)
    {
        if (data == null || !HasTarget || direction.sqrMagnitude <= .0001f) return;

        Vector2 toTarget = (Vector2)target.position - body.position;
        if (toTarget.sqrMagnitude <= .0001f
            || Vector2.Angle(direction, toTarget) > data.SpearHitAngle * .5f) return;
        float forwardDistance = Vector2.Dot(direction.normalized, toTarget);
        if (forwardDistance < 0f || forwardDistance > data.SpearHitRange) return;
        float lateralDistance = Mathf.Abs(Vector2.Perpendicular(direction.normalized).x * toTarget.x
            + Vector2.Perpendicular(direction.normalized).y * toTarget.y);
        if (lateralDistance > data.SpearHitRadius) return;

        target.GetComponentInParent<PlayerCharacterController>()?.TakeDamage(data.Damage);
    }

    public void CompleteSpearAttack()
    {
        fireCooldown = data.FireInterval;
        SetDesiredVelocity(Vector2.zero);
    }

    public void BeginRangedAttack()
    {
        SetDesiredVelocity(Vector2.zero);
        FaceTarget();
        if (supportsShoot)
        {
            visualAnimator.ResetTrigger(Shoot);
            visualAnimator.SetTrigger(Shoot);
        }
    }

    public void PerformShieldAttack()
    {
        if (data == null || !IsShieldBearer || !HasTarget)
        {
            return;
        }

        FaceTarget();
        meleePerfectDodgeStartTime = Time.time + data.MeleePerfectDodgeDelay;
        meleePerfectDodgeEndTime = meleePerfectDodgeStartTime + data.MeleePerfectDodgeDuration;
        target.GetComponentInParent<PlayerCharacterController>()?.TakeDamage(data.Damage);
        fireCooldown = data.ShieldAttackInterval;
    }

    public bool CanBeKilledBy(Vector2 attackerPosition, bool bypassShield)
    {
        if (!IsShieldBearer || bypassShield || IsShieldAttackExposed)
        {
            return true;
        }

        Vector2 toAttacker = attackerPosition - body.position;
        if (toAttacker.sqrMagnitude <= .0001f)
        {
            return false;
        }

        Vector2 facing = visualRenderer != null && visualRenderer.flipX ? Vector2.left : Vector2.right;
        float rearDotThreshold = Mathf.Cos(data.ShieldRearKillHalfAngle * Mathf.Deg2Rad);
        return Vector2.Dot(-facing, toAttacker.normalized) >= rearDotThreshold;
    }

    public void BlockIncomingAttack()
    {
        if (IsShieldBearer && !IsShieldAttackExposed)
        {
            stateMachine.ChangeState(shieldBlockState);
        }
    }

    public void BeginShieldAttack()
    {
        IsShieldAttackExposed = true;
        SetDesiredVelocity(Vector2.zero);
        FaceTarget();
    }

    public void EndShieldAttack() => IsShieldAttackExposed = false;

    public void FaceTarget()
    {
        if (HasTarget)
        {
            Face(target.position.x - transform.position.x);
        }
    }

    public float EnemyDeltaTime => Time.deltaTime * PlayerCharacterController.EnemyTimeScale;

    private void Face(float horizontalDirection)
    {
        if (visualRenderer != null && Mathf.Abs(horizontalDirection) > .01f)
        {
            visualRenderer.flipX = horizontalDirection < 0f;
        }
    }

    private void EnsureVisualAnimator()
    {
        if (visualAnimator == null && visualRenderer != null)
        {
            visualAnimator = visualRenderer.gameObject.AddComponent<Animator>();
        }

        if (visualAnimator == null)
        {
            return;
        }

        string controllerPath = GetVisualStyle() switch
        {
            EnemyVisualStyle.Archer => "Animation/弓兵/弓兵",
            EnemyVisualStyle.ShieldBearer => "Animation/盾兵/WarriorWalk/盾兵_行走",
            // The spear soldier controller will be assigned once its animation clips are authored.
            EnemyVisualStyle.Spearman => string.Empty,
            _ => "Animation/刀兵/刀兵_跑步/刀兵_跑步"
        };
        if (string.IsNullOrEmpty(controllerPath))
        {
            return;
        }
        RuntimeAnimatorController controller = Resources.Load<RuntimeAnimatorController>(controllerPath);
        if (controller != null)
        {
            // Enemies may have been created before their visual style was assigned.
            // Always use the controller that matches the selected style so its parameters are valid.
            visualAnimator.runtimeAnimatorController = controller;
        }
    }

    private EnemyVisualStyle GetVisualStyle()
    {
        if (visualStyle != EnemyVisualStyle.Auto)
        {
            return visualStyle;
        }

        if (data == null) return EnemyVisualStyle.Swordsman;
        return data.Archetype switch
        {
            EnemyArchetype.Ranged => EnemyVisualStyle.Archer,
            EnemyArchetype.Spearman => EnemyVisualStyle.Spearman,
            _ => EnemyVisualStyle.Swordsman
        };
    }

    private void CacheAnimatorParameters()
    {
        if (visualAnimator == null)
        {
            return;
        }

        foreach (AnimatorControllerParameter parameter in visualAnimator.parameters)
        {
            if (parameter.nameHash == Attack) supportsAttack = parameter.type == AnimatorControllerParameterType.Trigger;
            else if (parameter.nameHash == Shoot) supportsShoot = parameter.type == AnimatorControllerParameterType.Trigger;
            else if (parameter.nameHash == IsMoving) supportsIsMoving = parameter.type == AnimatorControllerParameterType.Bool;
            else if (parameter.nameHash == IsRunning) supportsIsRunning = parameter.type == AnimatorControllerParameterType.Bool;
        }
    }

    private void SetMovementAnimation(bool isMoving)
    {
        if (visualAnimator == null)
        {
            return;
        }

        if (supportsIsMoving) visualAnimator.SetBool(IsMoving, isMoving);
        if (supportsIsRunning) visualAnimator.SetBool(IsRunning, isMoving);
        SetAnimationState(isMoving ? EnemyAnimationState.Move : EnemyAnimationState.Idle);
    }

    /// <summary>Reserved for future death animations before the enemy object is removed.</summary>
    public void NotifyDeathAnimation() => SetAnimationState(EnemyAnimationState.Death);

    private void SetAnimationState(EnemyAnimationState state) => AnimationStateChanged?.Invoke(state);

    private void TryFindTarget()
    {
        nextTargetSearchTime = Time.time + 1f;
        if (string.IsNullOrWhiteSpace(fallbackTargetName))
        {
            return;
        }

        GameObject targetObject = GameObject.Find(fallbackTargetName);
        if (targetObject != null)
        {
            target = targetObject.transform;
            target.GetComponentInParent<PlayerCharacterController>()?.IgnoreEnemyCollisions(this);
        }
    }

    private void OnDisable()
    {
        desiredVelocity = Vector2.zero;
        if (visualAnimator != null)
        {
            visualAnimator.speed = 1f;
        }
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
        }
    }
}
