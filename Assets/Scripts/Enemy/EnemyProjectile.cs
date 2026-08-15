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

        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }
    }

    private void OnEnable()
    {
        hasHit = false;
        destroyTime = Time.time + lifetime;
    }

    private void Update()
    {
        if (worldCamera != null
            && ExtraCameraBounds.IsOutside(worldCamera, body.position, transform.position.z))
        {
            Destroy(gameObject);
        }
        else if (worldCamera == null && Time.time >= destroyTime)
        {
            Destroy(gameObject);
        }
    }

    private void FixedUpdate()
    {
        body.linearVelocity = travelDirection * (travelSpeed * TestControl.EnemyTimeScale);
    }

    public void Launch(Vector2 direction, float speed, GameObject projectileOwner)
    {
        owner = projectileOwner;
        Vector2 normalizedDirection = direction.sqrMagnitude > 0f
            ? direction.normalized
            : Vector2.right;

        travelDirection = normalizedDirection;
        travelSpeed = Mathf.Max(0f, speed);
        body.linearVelocity = travelDirection * (travelSpeed * TestControl.EnemyTimeScale);
        transform.right = normalizedDirection;
        IgnoreOwnerCollisions();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleHit(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleHit(collision.collider);
    }

    private void HandleHit(Collider2D other)
    {
        if (hasHit || other == null || BelongsToOwner(other.transform))
        {
            return;
        }
        
        TestControl extraPlayer = other.GetComponentInParent<TestControl>();
        if (extraPlayer == null)
        {
            return;
        }

        // A space dodge phases through projectiles. Distance, rather than the
        // trigger contact itself, decides whether this becomes a perfect dodge.
        if (extraPlayer != null && extraPlayer.IsDodging)
        {
            IgnorePlayerCollisions(extraPlayer);
            extraPlayer.TryTriggerPerfectDodge(transform.position);
            return;
        }
        

        hasHit = true;
        Destroy(gameObject);
    }

    public void IgnorePlayerCollisions(TestControl player)
    {
        if (player == null)
        {
            return;
        }

        Collider2D[] projectileColliders = GetComponentsInChildren<Collider2D>();
        Collider2D[] playerColliders = player.GetComponentsInChildren<Collider2D>();

        foreach (Collider2D projectileCollider in projectileColliders)
        {
            foreach (Collider2D playerCollider in playerColliders)
            {
                Physics2D.IgnoreCollision(projectileCollider, playerCollider);
            }
        }
    }

    private bool BelongsToOwner(Transform candidate)
    {
        return owner != null && candidate.IsChildOf(owner.transform);
    }

    private void IgnoreOwnerCollisions()
    {
        if (owner == null)
        {
            return;
        }

        Collider2D[] projectileColliders = GetComponentsInChildren<Collider2D>();
        Collider2D[] ownerColliders = owner.GetComponentsInChildren<Collider2D>();

        foreach (Collider2D projectileCollider in projectileColliders)
        {
            foreach (Collider2D ownerCollider in ownerColliders)
            {
                Physics2D.IgnoreCollision(projectileCollider, ownerCollider);
            }
        }
    }
}
