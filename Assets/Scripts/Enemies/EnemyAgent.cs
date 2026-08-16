using System.Collections.Generic;
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
    [SerializeField] private AudioSource attackSfxSource;
    [SerializeField, Min(0f)] private float boundaryPadding = 0.5f;

    private Rigidbody2D body;
    private Collider2D[] bodyColliders;
    private Vector2 desiredVelocity;
    private float nextTargetSearchTime;
    private float fireCooldown;
    private float meleeEngagementStartTime;
    private float meleeEngagementSpeedMultiplier = 1f;
    private float lastMeleeAttackTime;
    private Vector2 meleeWaitingRoamDirection;
    private float nextMeleeWaitingRoamDirectionTime;
    private float meleeAttackPreparationEndTime = -1f;
    private float meleeAttackRecoveryEndTime = -1f;
    private float meleePerfectDodgeStartTime;
    private float meleePerfectDodgeEndTime;
    private EnemyStateMachine stateMachine;

    private static readonly int Attack = Animator.StringToHash("Attack");
    private static readonly int Death = Animator.StringToHash("Death");
    private static readonly int Shoot = Animator.StringToHash("Shoot");
    private static readonly int IsMoving = Animator.StringToHash("IsMoving");
    private static readonly int IsRunning = Animator.StringToHash("IsRunning");

    private bool supportsAttack;
    private bool supportsDeath;
    private bool supportsShoot;
    private bool supportsIsMoving;
    private bool supportsIsRunning;

    public EnemyData Data => data;
    public Rigidbody2D Body => body;
    public Transform Target => target;
    public bool HasTarget => target != null;
    public bool CanFire => !IsDead && data != null && data.ProjectilePrefab != null && fireCooldown <= 0f;
    public bool CanMeleeAttack => !IsDead && data != null && data.Archetype == EnemyArchetype.Melee && fireCooldown <= 0f;
    public bool CanSpearAttack => !IsDead && data != null && data.Archetype == EnemyArchetype.Spearman && fireCooldown <= 0f;
    public bool IsMeleeCombatant => data != null && (data.Archetype == EnemyArchetype.Melee || data.Archetype == EnemyArchetype.Spearman);
    public bool IsDead { get; private set; }
    public bool IsShieldBearer => GetVisualStyle() == EnemyVisualStyle.ShieldBearer;
    public bool IsShieldAttackExposed { get; private set; }
    public bool IsMeleeAttackRecovering => Time.time < meleeAttackRecoveryEndTime;
    public bool IsWaitingToEngageInMelee => Time.time < meleeEngagementStartTime;
    public float MeleeEngagementMoveSpeed => data.MoveSpeed * meleeEngagementSpeedMultiplier;
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
        bodyColliders = GetComponentsInChildren<Collider2D>(true);
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        worldCamera ??= Camera.main;
        visualAnimator ??= GetComponentInChildren<Animator>();
        visualRenderer ??= GetComponentInChildren<SpriteRenderer>();
        EnsureVisualAnimator();
        CacheAnimatorParameters();
        IgnoreExistingEnemyCollisions();

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
        if (data != null && IsMeleeCombatant)
        {
            meleeEngagementStartTime = Time.time + data.GetMeleeEngagementDelay();
            float variation = data.MeleeEngagementSpeedVariance;
            meleeEngagementSpeedMultiplier = Random.Range(1f - variation, 1f + variation);
            // Establishes a stable but varied initial order before the first attack rotation.
            lastMeleeAttackTime = Time.time - Random.Range(0f, 3f);
        }
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
            visualAnimator.speed = IsDead ? 1f : PlayerCharacterController.EnemyTimeScale;
        }

        if (IsDead) return;

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
        if (data == null || IsDead)
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

        Vector2 movementVelocity = desiredVelocity;
        if (IsMeleeCombatant)
        {
            // Keep melee bodies apart even while they are waiting, attacking, or recovering.
            // This is applied here instead of through SetDesiredVelocity so attack facing stays locked.
            Vector2 separation = CalculateMeleeSeparation() * data.MeleeSeparationStrength;
            movementVelocity += separation * data.MoveSpeed;
            movementVelocity = Vector2.ClampMagnitude(
                movementVelocity,
                Mathf.Max(data.MoveSpeed, desiredVelocity.magnitude));
        }

        Vector2 clampedNext = CameraBounds.Clamp(
            worldCamera,
            clampedPosition + movementVelocity * (PlayerCharacterController.EnemyTimeScale * Time.fixedDeltaTime),
            boundaryPadding,
            transform.position.z);
        body.linearVelocity = (clampedNext - clampedPosition) / Time.fixedDeltaTime;
    }

    public void SetDesiredVelocity(Vector2 velocity)
    {
        if (IsDead) return;

        desiredVelocity = velocity;
        Face(velocity.x);
        SetMovementAnimation(velocity.sqrMagnitude > .0001f);
    }

    public Vector2 GetMeleeFormationMoveDirection(out bool isAtFormation)
    {
        return GetMeleeRingMoveDirection(data.MeleeFormationRadius, out isAtFormation);
    }

    public bool IsWithinMeleeAttackDistance()
    {
        if (!HasTarget || data == null) return false;

        Vector2 ownCenter = body.position + GetOwnBodyCenterOffset();
        Vector2 targetOffset = GetTargetBodyCenter() - ownCenter;
        return targetOffset.sqrMagnitude <= data.StoppingDistance * data.StoppingDistance;
    }

    public Vector2 GetMeleeWaitingRoamDirection()
    {
        if (Time.time >= nextMeleeWaitingRoamDirectionTime)
        {
            Vector2 durationRange = data.RoamStateDurationRange;
            nextMeleeWaitingRoamDirectionTime = Time.time + Random.Range(durationRange.x, durationRange.y);
            meleeWaitingRoamDirection = Random.value < data.IdleChance ? Vector2.zero : Random.Range(0, 4) switch
            {
                0 => Vector2.up,
                1 => Vector2.down,
                2 => Vector2.left,
                _ => Vector2.right
            };
        }

        return meleeWaitingRoamDirection;
    }

    public bool CanPressureTarget()
    {
        if (!HasTarget || data == null || !IsMeleeCombatant) return false;

        int priority = 0;
        EntityId ownEntityId = GetEntityId();
        foreach (EnemyAgent other in FindObjectsByType<EnemyAgent>(FindObjectsInactive.Exclude))
        {
            if (other == null || other.data == null || !other.IsMeleeCombatant || other.target != target) continue;

            bool attackedEarlier = other.lastMeleeAttackTime < lastMeleeAttackTime;
            bool sameAttackTimeWithLowerId = Mathf.Approximately(other.lastMeleeAttackTime, lastMeleeAttackTime)
                && other.GetEntityId() < ownEntityId;
            if (attackedEarlier || sameAttackTimeWithLowerId) priority++;
        }

        return priority < data.MeleePressureLimit;
    }

    private Vector2 GetMeleeRingMoveDirection(float ringRadius, out bool isAtRing)
    {
        isAtRing = false;
        if (!HasTarget || data == null || !IsMeleeCombatant) return Vector2.zero;

        List<EnemyAgent> combatants = new();
        foreach (EnemyAgent other in FindObjectsByType<EnemyAgent>(FindObjectsInactive.Exclude))
        {
            if (other == null || other.data == null || !other.IsMeleeCombatant
                || other.target != target) continue;

            combatants.Add(other);
        }

        if (combatants.Count == 0) return Vector2.zero;

        combatants.Sort((left, right) =>
        {
            if (left.GetEntityId() < right.GetEntityId()) return -1;
            return right.GetEntityId() < left.GetEntityId() ? 1 : 0;
        });
        List<int> availableSlots = new(combatants.Count);
        for (int slot = 0; slot < combatants.Count; slot++) availableSlots.Add(slot);

        Vector2 targetCenter = GetTargetBodyCenter();
        int ownSlot = 0;
        foreach (EnemyAgent combatant in combatants)
        {
            int nearestAvailableIndex = 0;
            float nearestDistanceSquared = float.PositiveInfinity;
            Vector2 combatantCenter = combatant.Body.position + combatant.GetOwnBodyCenterOffset();
            for (int availableIndex = 0; availableIndex < availableSlots.Count; availableIndex++)
            {
                int slot = availableSlots[availableIndex];
                float slotAngle = slot * Mathf.PI * 2f / combatants.Count;
                Vector2 slotPosition = targetCenter
                    + new Vector2(Mathf.Cos(slotAngle), Mathf.Sin(slotAngle)) * ringRadius;
                float distanceSquared = (combatantCenter - slotPosition).sqrMagnitude;
                if (distanceSquared < nearestDistanceSquared)
                {
                    nearestDistanceSquared = distanceSquared;
                    nearestAvailableIndex = availableIndex;
                }
            }

            int assignedSlot = availableSlots[nearestAvailableIndex];
            if (combatant == this) ownSlot = assignedSlot;
            availableSlots.RemoveAt(nearestAvailableIndex);
        }

        float angle = ownSlot * Mathf.PI * 2f / combatants.Count;
        Vector2 formationPosition = targetCenter
            + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ringRadius;
        formationPosition -= GetOwnBodyCenterOffset();
        Vector2 toFormation = formationPosition - body.position;
        isAtRing = toFormation.sqrMagnitude
            <= data.MeleeFormationArrivalDistance * data.MeleeFormationArrivalDistance;

        Vector2 separation = CalculateMeleeSeparation();
        Vector2 formationDirection = isAtRing ? Vector2.zero : toFormation.normalized;
        Vector2 steering = formationDirection + separation * data.MeleeSeparationStrength;
        return steering.sqrMagnitude > .0001f ? steering.normalized : Vector2.zero;
    }

    private Vector2 GetTargetBodyCenter()
    {
        Collider2D targetCollider = target != null ? target.GetComponent<Collider2D>() : null;
        return targetCollider != null ? targetCollider.bounds.center : (Vector2)target.position;
    }

    private Vector2 GetOwnBodyCenterOffset()
    {
        Collider2D ownCollider = GetComponentInChildren<Collider2D>();
        return ownCollider != null ? (Vector2)ownCollider.bounds.center - body.position : Vector2.zero;
    }

    private Vector2 CalculateMeleeSeparation()
    {
        float radius = data.MeleeSeparationRadius;
        Vector2 separation = Vector2.zero;
        Vector2 ownCenter = body.position + GetOwnBodyCenterOffset();
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(ownCenter, radius))
        {
            EnemyAgent other = hit.GetComponentInParent<EnemyAgent>();
            if (other == null || other == this || other.data == null
                || !other.IsMeleeCombatant || other.target != target) continue;

            Vector2 otherCenter = other.body.position + other.GetOwnBodyCenterOffset();
            Vector2 offset = ownCenter - otherCenter;
            float distance = offset.magnitude;
            if (distance <= .0001f)
            {
                EntityId ownId = GetEntityId();
                EntityId otherId = other.GetEntityId();
                uint pairHash = unchecked((uint)(ownId.GetHashCode() ^ otherId.GetHashCode()));
                float angle = pairHash / (float)uint.MaxValue * Mathf.PI * 2f;
                Vector2 splitDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                separation += ownId < otherId ? splitDirection : -splitDirection;
                continue;
            }

            separation += offset / distance * Mathf.Clamp01(1f - distance / radius);
        }

        return separation;
    }

    private void IgnoreExistingEnemyCollisions()
    {
        if (bodyColliders == null || bodyColliders.Length == 0) return;

        foreach (EnemyAgent other in FindObjectsByType<EnemyAgent>(FindObjectsInactive.Include))
        {
            if (other == null || other == this) continue;
            Collider2D[] otherColliders = other.GetComponentsInChildren<Collider2D>(true);
            foreach (Collider2D ownCollider in bodyColliders)
            {
                if (ownCollider == null) continue;
                foreach (Collider2D otherCollider in otherColliders)
                {
                    if (otherCollider != null)
                        Physics2D.IgnoreCollision(ownCollider, otherCollider, true);
                }
            }
        }
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
        Vector2 projectileOrigin = visualRenderer != null
            ? visualRenderer.bounds.center
            : body.position;
        EnemyProjectile projectile = Instantiate(
            data.ProjectilePrefab,
            projectileOrigin + direction * data.ProjectileSpawnOffset,
            Quaternion.Euler(0f, 0f, angle));
        projectile.Launch(target.position, data.ProjectileSpeed, data.ProjectileGravity, gameObject, data.Damage);
        PlayAttackSfx();
        fireCooldown = data.FireInterval;
    }

    public void BeginMeleeAttack()
    {
        if (!CanMeleeAttack)
        {
            return;
        }

        if (HasTarget)
        {
            Face(target.position.x - transform.position.x);
        }

        SetDesiredVelocity(Vector2.zero);
        SetAnimationState(EnemyAnimationState.Attack);
        meleePerfectDodgeStartTime = Time.time + data.MeleePerfectDodgeDelay;
        meleePerfectDodgeEndTime = meleePerfectDodgeStartTime + data.MeleePerfectDodgeDuration;
        if (supportsAttack)
        {
            visualAnimator.ResetTrigger(Attack);
            visualAnimator.SetTrigger(Attack);
        }
        PlayAttackSfx();
    }

    public void PerformMeleeAttack()
    {
        if (!CanMeleeAttack)
        {
            return;
        }

        TryDamageTarget();
        fireCooldown = data.GetMeleeAttackCooldown(data.FireInterval);
        lastMeleeAttackTime = Time.time;
    }

    public void CompleteMeleeAttack() => StartMeleeAttackRecovery();

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
        PlayAttackSfx();
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

        TryDamageTarget();
    }

    public void CompleteSpearAttack()
    {
        fireCooldown = data.GetMeleeAttackCooldown(data.FireInterval);
        lastMeleeAttackTime = Time.time;
        StartMeleeAttackRecovery();
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

        meleePerfectDodgeStartTime = Time.time + data.MeleePerfectDodgeDelay;
        meleePerfectDodgeEndTime = meleePerfectDodgeStartTime + data.MeleePerfectDodgeDuration;
        TryDamageTarget();
        fireCooldown = data.GetMeleeAttackCooldown(data.ShieldAttackInterval);
    }

    /// <summary>
    /// Starts a per-unit reaction delay only after the unit reaches its melee position.
    /// This keeps an approaching pack from becoming ready and swinging on the same frame.
    /// </summary>
    public bool TryBeginMeleeAttack()
    {
        if (data == null || !IsMeleeCombatant || fireCooldown > 0f)
        {
            return false;
        }

        if (meleeAttackPreparationEndTime < 0f)
        {
            meleeAttackPreparationEndTime = Time.time + data.GetMeleeAttackPreparationDelay();
            return false;
        }

        if (Time.time < meleeAttackPreparationEndTime)
        {
            return false;
        }

        meleeAttackPreparationEndTime = -1f;
        return true;
    }

    public void CancelMeleeAttackPreparation() => meleeAttackPreparationEndTime = -1f;

    private void StartMeleeAttackRecovery()
    {
        meleeAttackRecoveryEndTime = Time.time + data.GetMeleeAttackRecoveryDelay();
    }

    public bool CanBeKilledBy(Vector2 attackerPosition, bool bypassShield)
    {
        if (IsDead) return false;

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
        PlayAttackSfx();
    }

    public void EndShieldAttack()
    {
        IsShieldAttackExposed = false;
        lastMeleeAttackTime = Time.time;
        StartMeleeAttackRecovery();
    }

    public void FaceTarget()
    {
        if (HasTarget)
        {
            Face(target.position.x - transform.position.x);
        }
    }

    public float EnemyDeltaTime => Time.deltaTime * PlayerCharacterController.EnemyTimeScale;

    public void Die()
    {
        if (IsDead) return;

        IsDead = true;
        desiredVelocity = Vector2.zero;
        meleePerfectDodgeStartTime = 0f;
        meleePerfectDodgeEndTime = 0f;
        SetMovementAnimation(false);

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
        }

        foreach (Collider2D hitbox in GetComponentsInChildren<Collider2D>())
        {
            hitbox.enabled = false;
        }

        if (visualAnimator != null && supportsDeath)
        {
            visualAnimator.ResetTrigger(Attack);
            visualAnimator.SetTrigger(Death);
            StartCoroutine(DestroyAfterDeathAnimation(GetDeathAnimationDuration()));
            return;
        }

        Destroy(gameObject);
    }

    private System.Collections.IEnumerator DestroyAfterDeathAnimation(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += EnemyDeltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }

    private float GetDeathAnimationDuration()
    {
        if (visualAnimator?.runtimeAnimatorController == null) return .9f;

        float duration = 0f;
        foreach (AnimationClip clip in visualAnimator.runtimeAnimatorController.animationClips)
        {
            if (clip != null && clip.name.IndexOf("death", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                duration = Mathf.Max(duration, clip.length);
            }
        }

        // Covers the controller transition before the death clip begins.
        return Mathf.Max(.05f, duration > 0f ? duration + .05f : .9f);
    }

    private void Face(float horizontalDirection)
    {
        if (visualRenderer != null && Mathf.Abs(horizontalDirection) > .01f)
        {
            visualRenderer.flipX = horizontalDirection < 0f;
        }
    }

    private void PlayAttackSfx()
    {
        AudioClip clip = data != null ? data.AttackSfx : null;
        if (clip == null) return;

        attackSfxSource ??= GetComponent<AudioSource>();
        if (attackSfxSource == null)
        {
            attackSfxSource = gameObject.AddComponent<AudioSource>();
            attackSfxSource.playOnAwake = false;
            attackSfxSource.loop = false;
            attackSfxSource.spatialBlend = 0f;
        }

        attackSfxSource.volume = GameAudioSettings.GetChannelVolume(GameAudioChannel.SoundEffects);
        attackSfxSource.PlayOneShot(clip, data.AttackSfxVolume);
    }

    private void TryDamageTarget()
    {
        PlayerCharacterController player = target != null
            ? target.GetComponentInParent<PlayerCharacterController>()
            : null;
        if (player == null) return;

        // Resolve the perfect-dodge window before any invulnerability or damage checks.
        // Dodging itself is invulnerable, so checking IsInvulnerable first would skip
        // this branch and prevent a valid perfect dodge at the hit boundary.
        if (player.IsDodging && IsMeleeAttackPerfectDodgeable
            && player.TryTriggerPerfectDodge(transform.position))
        {
            return;
        }

        if (player.IsInvulnerable) return;

        player.TakeDamage(data.Damage);
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
            _ => "Animation/SwordBandit/SwordBandit"
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
            else if (parameter.nameHash == Death) supportsDeath = parameter.type == AnimatorControllerParameterType.Trigger;
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
