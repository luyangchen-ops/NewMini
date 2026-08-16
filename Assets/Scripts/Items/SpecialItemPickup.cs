using UnityEngine;

[DisallowMultipleComponent]
public sealed class SpecialItemPickup : MonoBehaviour
{
    [SerializeField] private SpecialItemType itemType;

    private bool collected;

    public void Initialize(SpecialItemType type)
    {
        itemType = type;
        name = $"Pickup_{type}";

        Sprite itemSprite = SpecialItemVisualCatalog.GetSprite(itemType);
        float visualScale = itemSprite != null
            ? .8f / Mathf.Max(.01f, itemSprite.bounds.size.y)
            : .35f;
        transform.localScale = Vector3.one * visualScale;

        CircleCollider2D pickupCollider = GetComponent<CircleCollider2D>();
        if (pickupCollider == null) pickupCollider = gameObject.AddComponent<CircleCollider2D>();
        pickupCollider.isTrigger = true;
        pickupCollider.radius = .28f / visualScale;

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer == null) renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = itemSprite != null
            ? itemSprite
            : Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
        renderer.color = itemSprite != null ? Color.white : itemType switch
        {
            SpecialItemType.OneHitShield => new Color(.25f, .75f, 1f),
            SpecialItemType.HealingPotion => new Color(.95f, .2f, .25f),
            _ => new Color(.9f, .9f, .9f)
        };
        renderer.sortingOrder = 20;
    }

    private void OnTriggerEnter2D(Collider2D other) => TryCollect(other);

    private void OnCollisionEnter2D(Collision2D collision) => TryCollect(collision.collider);

    private void TryCollect(Collider2D other)
    {
        if (collected || other == null) return;
        PlayerSpecialItemInventory inventory = other.GetComponentInParent<PlayerSpecialItemInventory>();
        if (inventory == null) return;

        collected = true;
        inventory.Collect(itemType);
        Destroy(gameObject);
    }
}
