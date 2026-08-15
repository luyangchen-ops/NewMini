using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class EnemyAgent : MonoBehaviour
{
    [SerializeField] private EnemyData data;
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

    public EnemyData Data => data;
    public Rigidbody2D Body => body;
    public Transform Target => target;
    public bool HasTarget => target != null;
    public bool CanFire => data != null && data.ProjectilePrefab != null && fireCooldown <= 0f;
    public bool CanMeleeAttack => data != null && data.Archetype == EnemyArchetype.Melee && fireCooldown <= 0f;
    public bool IsMeleeAttackPerfectDodgeable => data != null
        && data.Archetype == EnemyArchetype.Melee
        && Time.time >= meleePerfectDodgeStartTime
        && Time.time <= meleePerfectDodgeEndTime;
    public EnemyIdleState IdleState { get; private set; }
    public EnemyState DefaultActiveState => data.Archetype == EnemyArchetype.Melee ? chaseState : roamState;
    public EnemyRoamState RoamState => roamState;
    public EnemyAttackState AttackState => attackState;

    private EnemyChaseState chaseState;
    private EnemyRoamState roamState;
    private EnemyAttackState attackState;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        worldCamera ??= Camera.main;
        visualAnimator ??= GetComponentInChildren<Animator>();
        visualRenderer ??= GetComponentInChildren<SpriteRenderer>();

        stateMachine = new EnemyStateMachine();
        IdleState = new EnemyIdleState(this, stateMachine);
        chaseState = new EnemyChaseState(this, stateMachine);
        roamState = new EnemyRoamState(this, stateMachine);
        attackState = new EnemyAttackState(this, stateMachine);
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
        visualAnimator?.SetTrigger(Attack);
        target?.GetComponentInParent<PlayerCharacterController>()?.TakeDamage(data.Damage);
        fireCooldown = data.FireInterval;
    }

    private void Face(float horizontalDirection)
    {
        if (visualRenderer != null && Mathf.Abs(horizontalDirection) > .01f)
        {
            visualRenderer.flipX = horizontalDirection < 0f;
        }
    }

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
