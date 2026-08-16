using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class EnemyProjectile : MonoBehaviour
{
    [SerializeField, Min(0f)] private float damage = 0f;
    [Tooltip("Safety fallback in case the projectile cannot cover its configured distance.")]
    [SerializeField, Min(0.05f)] private float lifetime = 4f;

    private Rigidbody2D body;
    private GameObject owner;
    private Vector2 launchPosition;
    private float maximumTravelDistance;
    private float destroyTime;
    private bool hasHit;
    private bool isLaunched;
    private Vector2 velocity;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    private void OnEnable()
    {
        hasHit = false;
        isLaunched = false;
        launchPosition = Vector2.zero;
        maximumTravelDistance = 0f;
        destroyTime = float.PositiveInfinity;
    }

    private void Update()
    {
        // Instantiate invokes Awake/OnEnable before EnemyAgent can call Launch.
        // Do not allow an uninitialised clone to destroy itself during that window.
        if (!isLaunched)
        {
            return;
        }

        float travelledDistanceSqr = (body.position - launchPosition).sqrMagnitude;
        if (travelledDistanceSqr >= maximumTravelDistance * maximumTravelDistance
            || Time.time >= destroyTime)
        {
            Destroy(gameObject);
        }
    }

    private void FixedUpdate()
    {
        if (!isLaunched || body == null)
        {
            return;
        }

        float timeScale = PlayerCharacterController.EnemyTimeScale;
        body.linearVelocity = velocity * timeScale;
    }

    public void Launch(Vector2 targetPosition, float speed, float maxTravelDistance, GameObject projectileOwner, float attackDamage = 0f)
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        owner = projectileOwner;
        float launchSpeed = Mathf.Max(0.01f, speed);
        Vector2 toTarget = targetPosition - (Vector2)transform.position;
        Vector2 direction = toTarget.sqrMagnitude > .0001f ? toTarget.normalized : (Vector2)transform.right;
        velocity = direction * launchSpeed;
        launchPosition = body.position;
        maximumTravelDistance = Mathf.Max(.1f, maxTravelDistance);
        damage = Mathf.Max(0f, attackDamage);
        destroyTime = Time.time + lifetime;
        isLaunched = true;
        body.linearVelocity = velocity * PlayerCharacterController.EnemyTimeScale;
        if (velocity.sqrMagnitude > .0001f)
        {
            transform.right = velocity;
        }
        IgnoreOwnerCollisions();
    }

    private void OnTriggerEnter2D(Collider2D other) => HandleHit(other);
    private void OnCollisionEnter2D(Collision2D collision) => HandleHit(collision.collider);

    private void HandleHit(Collider2D other)
    {
        if (hasHit || other == null || BelongsToOwner(other.transform)) return;
        PlayerCharacterController player = other.GetComponentInParent<PlayerCharacterController>();
        if (player == null) return;
        if (player.IsDodging)
        {
            IgnorePlayerCollisions(player);
            player.TryTriggerPerfectDodge(transform.position);
            return;
        }

        if (player.IsInvulnerable) return;

        hasHit = true;
        player.TakeDamage(damage);
        Destroy(gameObject);
    }

    public void IgnorePlayerCollisions(PlayerCharacterController player)
    {
        if (player == null) return;
        IgnoreCollisions(GetComponentsInChildren<Collider2D>(), player.GetComponentsInChildren<Collider2D>());
    }

    private bool BelongsToOwner(Transform candidate) => owner != null && candidate.IsChildOf(owner.transform);

    private void IgnoreOwnerCollisions()
    {
        if (owner != null) IgnoreCollisions(GetComponentsInChildren<Collider2D>(), owner.GetComponentsInChildren<Collider2D>());
    }

    private static void IgnoreCollisions(Collider2D[] first, Collider2D[] second)
    {
        foreach (Collider2D firstCollider in first)
            foreach (Collider2D secondCollider in second)
                Physics2D.IgnoreCollision(firstCollider, secondCollider);
    }
}
