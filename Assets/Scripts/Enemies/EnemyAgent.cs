using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class EnemyAgent : MonoBehaviour
{
    [SerializeField] private EnemyData data;
    [SerializeField] private Transform target;
    [SerializeField] private string fallbackTargetName = "Player";
    [SerializeField] private Camera worldCamera;
    [SerializeField, Min(0f)] private float boundaryPadding = 0.5f;

    private Rigidbody2D body;
    private Vector2 desiredVelocity;
    private float nextTargetSearchTime;
    private float fireCooldown;
    private EnemyStateMachine stateMachine;

    public EnemyData Data => data;
    public Rigidbody2D Body => body;
    public Transform Target => target;
    public bool HasTarget => target != null;
    public bool CanFire => data != null && data.ProjectilePrefab != null && fireCooldown <= 0f;
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

        stateMachine = new EnemyStateMachine();
        IdleState = new EnemyIdleState(this, stateMachine);
        chaseState = new EnemyChaseState(this, stateMachine);
        roamState = new EnemyRoamState(this, stateMachine);
        attackState = new EnemyAttackState(this, stateMachine);
        TryFindTarget();
        fireCooldown = data != null ? Random.Range(0.1f, data.FireInterval) : 0f;
        stateMachine.ChangeState(IdleState);
    }

    private void Update()
    {
        if (data == null)
        {
            return;
        }

        if (!HasTarget && Time.time >= nextTargetSearchTime)
        {
            TryFindTarget();
        }

        fireCooldown = Mathf.Max(0f, fireCooldown - Time.deltaTime * TestControl.EnemyTimeScale);
        stateMachine.Tick();
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
            worldCamera, clampedPosition + desiredVelocity * Time.fixedDeltaTime, boundaryPadding, transform.position.z);
        body.linearVelocity = (clampedNext - clampedPosition) / Time.fixedDeltaTime;
    }

    public void SetDesiredVelocity(Vector2 velocity) => desiredVelocity = velocity * TestControl.EnemyTimeScale;

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
        projectile.Launch(direction, data.ProjectileSpeed, gameObject);
        fireCooldown = data.FireInterval;
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
        }
    }

    private void OnDisable()
    {
        desiredVelocity = Vector2.zero;
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
        }
    }
}
