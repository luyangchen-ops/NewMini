using UnityEngine;

/// <summary>Creates collectible special-item drops. Call the source-specific helpers when a source is destroyed.</summary>
public static class SpecialItemDropSpawner
{
    public const float EnemyDropChance = .10f;
    public const float BreakableDropChance = .60f;

    public static void TryDropFromEnemy(Vector3 position) => TryDrop(position, EnemyDropChance);

    public static void TryDropFromBreakable(Vector3 position) => TryDrop(position, BreakableDropChance);

    public static void TryDrop(Vector3 position, float chance)
    {
        if (Random.value > Mathf.Clamp01(chance)) return;

        GameObject pickupObject = new GameObject("Pickup_SpecialItem");
        pickupObject.transform.position = position;
        pickupObject.AddComponent<SpecialItemPickup>().Initialize((SpecialItemType)Random.Range(0, 3));
    }
}
