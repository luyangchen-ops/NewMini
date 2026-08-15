using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class EnemyProjectile : MonoBehaviour
{
    [SerializeField, Min(0f)] private float damage = 10f;
    [Tooltip("Safety fallback used only when no Main Camera is available.")]
    [SerializeField, Min(0.05f)] private float lifetime = 4f;
    [SerializeField] private Camera worldCamera;

    private Rigidbody2D body;
    private GameObject owner;
    private float destroyTime;
    private bool hasHit;
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
        destroyTime = Time.time + lifetime;
    }

    private void Update()
    {
        if (worldCamera != null && CameraBounds.IsOutside(worldCamera, body.position, transform.position.z))
        {
            Destroy(gameObject);
        }
        else if (worldCamera == null && Time.time >= destroyTime)
        {
            Destroy(gameObject);
        }
    }

    private void FixedUpdate() => body.linearVelocity = travelDirection * (travelSpeed * TestControl.EnemyTimeScale);

    public void Launch(Vector2 direction, float speed, GameObject projectileOwner)
    {
        owner = projectileOwner;
        travelDirection = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.right;
        travelSpeed = Mathf.Max(0f, speed);
        body.linearVelocity = travelDirection * (travelSpeed * TestControl.EnemyTimeScale);
        transform.right = travelDirection;
        IgnoreOwnerCollisions();
    }

    private void OnTriggerEnter2D(Collider2D other) => HandleHit(other);
    private void OnCollisionEnter2D(Collision2D collision) => HandleHit(collision.collider);

    private void HandleHit(Collider2D other)
    {
        if (hasHit || other == null || BelongsToOwner(other.transform)) return;
        TestControl player = other.GetComponentInParent<TestControl>();
        if (player == null) return;
        if (player.IsDodging)
        {
            IgnorePlayerCollisions(player);
            player.TryTriggerPerfectDodge(transform.position);
            return;
        }

        hasHit = true;
        Destroy(gameObject);
    }

    public void IgnorePlayerCollisions(TestControl player)
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
