using UnityEngine;

/// <summary>Editor-authored sprite references used by runtime-created special items.</summary>
public sealed class SpecialItemVisualCatalog : ScriptableObject
{
    private const string ResourcePath = "SpecialItemVisualCatalog";

    [SerializeField] private Sprite oneHitShield;
    [SerializeField] private Sprite healingPotion;
    [SerializeField] private Sprite throwingKnife;

    private static SpecialItemVisualCatalog instance;

    public static Sprite GetSprite(SpecialItemType itemType)
    {
        instance ??= Resources.Load<SpecialItemVisualCatalog>(ResourcePath);
        if (instance == null)
        {
            Debug.LogWarning($"Missing Resources/{ResourcePath}.asset; special items will use fallback visuals.");
            return null;
        }

        return itemType switch
        {
            SpecialItemType.OneHitShield => instance.oneHitShield,
            SpecialItemType.HealingPotion => instance.healingPotion,
            SpecialItemType.ThrowingKnife => instance.throwingKnife,
            _ => null
        };
    }
}
