using UnityEditor;
using UnityEngine;

/// <summary>Applies the default swordsman visual to the existing boss gameplay root.</summary>
[InitializeOnLoad]
public static class BossPrefabAuthoring
{
    private const string BossPrefabPath = "Assets/Prefabs/Story/Boss_借命阎罗_裘九.prefab";
    private const string SwordsmanVisualPrefabPath = "Assets/Prefabs/Enemy/刀兵.prefab";
    private const string MeleeDataPath = "Assets/Data/Enemies/MeleeEnemyData.asset";
    private const string BossDataPath = "Assets/Data/Enemies/BossEnemyData.asset";

    static BossPrefabAuthoring() => EditorApplication.delayCall += UpgradeExistingBossPrefab;

    [MenuItem("NewMini/Story/Upgrade Boss Prefab")]
    public static void UpgradeExistingBossPrefab()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath) != null) BuildOrUpgrade();
    }

    public static GameObject BuildOrUpgrade()
    {
        EnsureBossData();
        GameObject visualSource = AssetDatabase.LoadAssetAtPath<GameObject>(SwordsmanVisualPrefabPath);
        if (visualSource == null) throw new System.InvalidOperationException("Swordsman visual prefab is missing.");

        string sourcePath = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath) != null
            ? BossPrefabPath
            : "Assets/Prefabs/Enemy/刀兵.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(sourcePath);
        root.name = "Boss_借命阎罗_裘九";

        Transform existingVisual = root.transform.Find("Visual_Boss");
        if (existingVisual != null) Object.DestroyImmediate(existingVisual.gameObject);

        SpriteRenderer swordsmanRenderer = visualSource.GetComponent<SpriteRenderer>();
        Animator swordsmanAnimator = visualSource.GetComponent<Animator>();
        if (swordsmanRenderer == null || swordsmanAnimator == null)
            throw new System.InvalidOperationException("Swordsman prefab needs a SpriteRenderer and Animator.");

        GameObject visual = new("Visual_Boss");
        visual.transform.SetParent(root.transform, false);
        // The standard swordsman uses a .6 scale. Apply the requested 1.1x
        // visual size without changing the Boss root's combat body or logic.
        visual.transform.localScale = Vector3.one * .66f;

        SpriteRenderer newRenderer = visual.AddComponent<SpriteRenderer>();
        newRenderer.sprite = swordsmanRenderer.sprite;
        newRenderer.sharedMaterial = swordsmanRenderer.sharedMaterial;
        newRenderer.sortingLayerID = swordsmanRenderer.sortingLayerID;
        newRenderer.sortingOrder = swordsmanRenderer.sortingOrder;
        newRenderer.color = new Color(1f, .22f, .22f, 1f);

        Animator newAnimator = visual.AddComponent<Animator>();
        newAnimator.runtimeAnimatorController = swordsmanAnimator.runtimeAnimatorController;

        EnemyAgent agent = root.GetComponent<EnemyAgent>();
        if (agent == null) agent = root.AddComponent<EnemyAgent>();
        if (root.GetComponent<BorrowedLifeBossController>() == null) root.AddComponent<BorrowedLifeBossController>();
        if (root.GetComponent<BossCombatController>() == null) root.AddComponent<BossCombatController>();

        SerializedObject serializedAgent = new SerializedObject(agent);
        serializedAgent.FindProperty("data").objectReferenceValue = AssetDatabase.LoadAssetAtPath<EnemyData>(BossDataPath);
        serializedAgent.FindProperty("visualStyle").enumValueIndex = (int)EnemyAgent.EnemyVisualStyle.Swordsman;
        serializedAgent.FindProperty("visualAnimator").objectReferenceValue = newAnimator;
        serializedAgent.FindProperty("visualRenderer").objectReferenceValue = newRenderer;
        serializedAgent.ApplyModifiedPropertiesWithoutUndo();

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, BossPrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
        AssetDatabase.SaveAssets();
        return saved;
    }

    private static void EnsureBossData()
    {
        EnemyData data = AssetDatabase.LoadAssetAtPath<EnemyData>(BossDataPath);
        if (data == null)
        {
            if (!AssetDatabase.CopyAsset(MeleeDataPath, BossDataPath))
                throw new System.InvalidOperationException("Unable to create BossEnemyData from MeleeEnemyData.");
            data = AssetDatabase.LoadAssetAtPath<EnemyData>(BossDataPath);
        }

        SerializedObject serialized = new SerializedObject(data);
        SetFloat(serialized, "MoveSpeed", 3.7f);
        SetFloat(serialized, "StoppingDistance", 1.55f);
        SetInt(serialized, "MeleePressureLimit", 1);
        SetVector2(serialized, "MeleeEngagementDelayRange", Vector2.zero);
        SetVector2(serialized, "MeleeAttackPreparationDelayRange", new Vector2(.04f, .08f));
        SetVector2(serialized, "MeleeAttackRecoveryDelayRange", new Vector2(.08f, .14f));
        SetFloat(serialized, "MeleeAttackHitDelay", .42f);
        SetFloat(serialized, "MeleeAttackDuration", .78f);
        SetFloat(serialized, "Damage", 22f);
        SetFloat(serialized, "FireInterval", .28f);
        SetFloat(serialized, "MeleePerfectDodgeDelay", .34f);
        SetFloat(serialized, "MeleePerfectDodgeDuration", .26f);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);
    }

    private static void SetFloat(SerializedObject serialized, string name, float value) =>
        serialized.FindProperty($"<{name}>k__BackingField").floatValue = value;
    private static void SetInt(SerializedObject serialized, string name, int value) =>
        serialized.FindProperty($"<{name}>k__BackingField").intValue = value;
    private static void SetVector2(SerializedObject serialized, string name, Vector2 value) =>
        serialized.FindProperty($"<{name}>k__BackingField").vector2Value = value;
}
