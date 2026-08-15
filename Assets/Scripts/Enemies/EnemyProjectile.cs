using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class EnemyProjectile : MonoBehaviour
{
    [SerializeField, Min(0f)] private float damage = 0f;
    [Tooltip("Safety fallback used only when no Main Camera is available.")]
    [SerializeField, Min(0.05f)] private float lifetime = 4f;
    [SerializeField] private Camera worldCamera;

    private Rigidbody2D body;
    private GameObject owner;
    private float destroyTime;
    private bool hasHit;
    private bool isLaunched;
    private Vector2 travelDirection;
    private float travelSpeed;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        worldCamera ??= Camera.main;
    }

    private void OnEnable()
    {
        hasHit = false;
        isLaunched = false;
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

        if (worldCamera != null && CameraBounds.IsOutside(worldCamera, body.position, transform.position.z))
        {
            Destroy(gameObject);
        }
        else if (worldCamera == null && Time.time >= destroyTime)
        {
            Destroy(gameObject);
        }
    }

    private void FixedUpdate() => body.linearVelocity = travelDirection * (travelSpeed * PlayerCharacterController.EnemyTimeScale);

    public void Launch(Vector2 direction, float speed, GameObject projectileOwner, float attackDamage = 0f)
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        owner = projectileOwner;
        travelDirection = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.right;
        travelSpeed = Mathf.Max(0f, speed);
        damage = Mathf.Max(0f, attackDamage);
        destroyTime = Time.time + lifetime;
        isLaunched = true;
        body.linearVelocity = travelDirection * (travelSpeed * PlayerCharacterController.EnemyTimeScale);
        transform.right = travelDirection;
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
