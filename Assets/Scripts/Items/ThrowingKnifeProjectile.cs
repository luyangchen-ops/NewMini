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
        float directionAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, directionAngle + 135f);
        speed = Mathf.Max(0f, launchSpeed);
        destroyTime = Time.time + Mathf.Max(.05f, lifetime);
        owner = projectileOwner;

        CircleCollider2D projectileCollider = GetComponent<CircleCollider2D>();
        if (projectileCollider == null) projectileCollider = gameObject.AddComponent<CircleCollider2D>();
        projectileCollider.isTrigger = true;
        projectileCollider.radius = .12f;

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer == null) renderer = gameObject.AddComponent<SpriteRenderer>();
        Sprite knifeSprite = SpecialItemVisualCatalog.GetSprite(SpecialItemType.ThrowingKnife);
        renderer.sprite = knifeSprite != null
            ? knifeSprite
            : Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
        renderer.color = Color.white;
        renderer.sortingOrder = 20;
        float visualScale = knifeSprite != null
            ? .75f / Mathf.Max(.01f, Mathf.Max(knifeSprite.bounds.size.x, knifeSprite.bounds.size.y))
            : .35f;
        transform.localScale = Vector3.one * visualScale;
        projectileCollider.radius = .12f / visualScale;
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

        BreakableMapProp breakable = other.GetComponentInParent<BreakableMapProp>();
        if (breakable != null && !breakable.IsBroken)
        {
            hasHit = true;
            breakable.Break();
            Destroy(gameObject);
            return;
        }

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
            // Go through the shared death gateway so Boss-specific mechanics
            // (borrowed lives and the five-hit counter) can intercept the hit.
            enemy.Die();
            if (enemy.IsDead)
                SpecialItemDropSpawner.TryDropFromEnemy(enemy.transform.position);
        }
        Destroy(gameObject);
    }
}
