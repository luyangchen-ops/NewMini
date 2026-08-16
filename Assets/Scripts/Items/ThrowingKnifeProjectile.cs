using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class ThrowingKnifeProjectile : MonoBehaviour
{
    private Rigidbody2D body;
    private Vector2 direction;
    private float speed;
    private float destroyTime;
    private GameObject owner;
    private bool hasHit;

    public void Launch(Vector2 launchDirection, float launchSpeed, float lifetime, GameObject projectileOwner)
    {
        body = GetComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        direction = launchDirection.sqrMagnitude > .0001f ? launchDirection.normalized : Vector2.right;
        speed = Mathf.Max(0f, launchSpeed);
        destroyTime = Time.time + Mathf.Max(.05f, lifetime);
        owner = projectileOwner;

        CircleCollider2D projectileCollider = GetComponent<CircleCollider2D>();
        if (projectileCollider == null) projectileCollider = gameObject.AddComponent<CircleCollider2D>();
        projectileCollider.isTrigger = true;
        projectileCollider.radius = .12f;

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer == null) renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
        renderer.color = Color.white;
        renderer.sortingOrder = 20;
        transform.localScale = new Vector3(.55f, .16f, 1f);
    }

    private void FixedUpdate()
    {
        if (body != null) body.linearVelocity = direction * speed;
    }

    private void Update()
    {
        if (Time.time >= destroyTime) Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other) => HandleHit(other);

    private void OnCollisionEnter2D(Collision2D collision) => HandleHit(collision.collider);

    private void HandleHit(Collider2D other)
    {
        if (hasHit || other == null || (owner != null && other.transform.IsChildOf(owner.transform))) return;
        EnemyAgent enemy = other.GetComponentInParent<EnemyAgent>();
        if (enemy == null) return;

        hasHit = true;
        PlayerCharacterController player = owner != null ? owner.GetComponent<PlayerCharacterController>() : null;
        if (!enemy.CanBeKilledBy(player != null ? player.transform.position : transform.position, false))
        {
            enemy.BlockIncomingAttack();
        }
        else
        {
            SpecialItemDropSpawner.TryDropFromEnemy(enemy.transform.position);
            Destroy(enemy.gameObject);
        }
        Destroy(gameObject);
    }
}
