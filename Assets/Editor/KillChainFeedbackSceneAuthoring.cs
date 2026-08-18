using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class KillChainFeedbackSceneAuthoring
{
    private const string RequestPath = "Temp/AuthorKillChainFeedback.request";
    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/Level_LD.unity",
        "Assets/Scenes/Extra.unity"
    };
    private const string ActiveSlashInkPath = "Assets/Resources/UI/KillChainFeedback/UI_KillChain_ActiveSlashInk.png";
    private const string ActiveBloodInkPath = "Assets/Resources/UI/KillChainFeedback/UI_KillChain_ActiveBloodInk.png";
    private const string SettlementBannerPath = "Assets/Resources/UI/KillChainFeedback/UI_KillChain_SettlementBanner.png";
    private const string SlashVfxPath = "Assets/Resources/UI/KillChainFeedback/VFX_KillChain_Slash.png";

    [MenuItem("NewMini/UI/Author Kill Chain Settlement Feedback")]
    public static void AuthorAllScenes()
    {
        AssetDatabase.Refresh();
        ConfigureUiSprite(ActiveSlashInkPath);
        ConfigureUiSprite(ActiveBloodInkPath);
        ConfigureUiSprite(SettlementBannerPath);
        ConfigureUiSprite(SlashVfxPath);
        AssetDatabase.Refresh();

        Scene originalActiveScene = SceneManager.GetActiveScene();
        foreach (string scenePath in ScenePaths)
        {
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool openedForAuthoring = !scene.IsValid() || !scene.isLoaded;
            if (openedForAuthoring)
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            AuthorScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            if (openedForAuthoring)
                EditorSceneManager.CloseScene(scene, true);
        }

        if (originalActiveScene.IsValid() && originalActiveScene.isLoaded)
            SceneManager.SetActiveScene(originalActiveScene);
        AssetDatabase.SaveAssets();
        Debug.Log("Authored kill-chain settlement feedback in Level_LD and Extra.");
    }

    [MenuItem("NewMini/UI/Upgrade Kill Chain Active Prompt Only")]
    public static void UpgradeActivePromptOnly()
    {
        AssetDatabase.Refresh();
        ConfigureUiSprite(ActiveSlashInkPath);
        ConfigureUiSprite(ActiveBloodInkPath);
        AssetDatabase.Refresh();

        Scene originalActiveScene = SceneManager.GetActiveScene();
        foreach (string scenePath in ScenePaths)
        {
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool openedForAuthoring = !scene.IsValid() || !scene.isLoaded;
            if (openedForAuthoring)
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            UpgradeActivePrompt(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            if (openedForAuthoring)
                EditorSceneManager.CloseScene(scene, true);
        }

        if (originalActiveScene.IsValid() && originalActiveScene.isLoaded)
            SceneManager.SetActiveScene(originalActiveScene);
        AssetDatabase.SaveAssets();
        Debug.Log("Upgraded only the active kill-chain prompt in Level_LD and Extra; settlement UI was preserved.");
    }

    [DidReloadScripts]
    private static void AuthorWhenRequested()
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrEmpty(projectRoot)) return;
        string absoluteRequestPath = Path.Combine(projectRoot, RequestPath);
        if (!File.Exists(absoluteRequestPath)) return;
        File.Delete(absoluteRequestPath);
        EditorApplication.delayCall += AuthorAllScenes;
    }

    private static void AuthorScene(Scene scene)
    {
        GameObject hudRoot = FindRoot(scene, "Root_角色战斗HUD");
        if (hudRoot == null)
            throw new System.InvalidOperationException($"{scene.path} has no authored Root_角色战斗HUD.");

        Transform oldLayer = hudRoot.transform.Find("Layer_连斩结算反馈");
        if (oldLayer != null) Object.DestroyImmediate(oldLayer.gameObject);

        GameObject layer = NewUi("Layer_连斩结算反馈", hudRoot.transform);
        Stretch(layer.GetComponent<RectTransform>());

        BuildActivePrompt(layer.transform, out GameObject active, out Text activeCount, out Text activeTitle,
            out Text activeReward, out Image activeAccent, out RectTransform activeInkRevealMask,
            out CanvasGroup activeTextGroup, out CanvasGroup activeBloodInkGroup, out RectTransform activeBloodInk);

        GameObject settlement = NewUi("Panel_连斩结算", layer.transform, typeof(CanvasGroup));
        Rect(settlement.GetComponent<RectTransform>(), new Vector2(.82f, .79f), new Vector2(.82f, .79f), Vector2.zero, new Vector2(520f, 175f));
        settlement.GetComponent<CanvasGroup>().alpha = 0f;
        Image settlementInk = ImageNode("Img_结算墨底", settlement.transform, Vector2.zero, new Vector2(500f, 212f), Color.white);
        settlementInk.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SettlementBannerPath);
        settlementInk.preserveAspect = true;
        Image settlementSlash = ImageNode("Img_结算斩痕", settlement.transform, new Vector2(-28f, -4f), new Vector2(500f, 500f), Color.white);
        settlementSlash.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SlashVfxPath);
        settlementSlash.preserveAspect = true;
        CanvasGroup settlementSlashGroup = settlementSlash.gameObject.AddComponent<CanvasGroup>();
        settlementSlashGroup.alpha = 0f;
        Image settlementAccent = ImageNode("Img_结算朱砂线", settlement.transform, new Vector2(0f, -51f), new Vector2(405f, 4f), new Color(.74f, .08f, .055f, 1f));
        Text rank = TextNode("Txt_结算品级", settlement.transform, new Vector2(0f, 43f), new Vector2(405f, 34f), "行云流水", 20, new Color(.74f, .08f, .055f, 1f), FontStyle.Bold);
        Text count = TextNode("Txt_结算斩数", settlement.transform, new Vector2(0f, 5f), new Vector2(405f, 54f), "三 斩", 40, new Color(.12f, .105f, .09f, 1f), FontStyle.Bold);
        Text rewards = TextNode("Txt_结算收益", settlement.transform, new Vector2(0f, -34f), new Vector2(420f, 30f), "气血 +45    气势 +4", 17, new Color(1f, .72f, .28f, 1f), FontStyle.Bold);

        KillChainSettlementFeedback feedback = layer.AddComponent<KillChainSettlementFeedback>();
        PlayerCharacterController player = FindInScene<PlayerCharacterController>(scene);
        SerializedObject serialized = new(feedback);
        serialized.FindProperty("player").objectReferenceValue = player;
        serialized.FindProperty("activeChainGroup").objectReferenceValue = active.GetComponent<CanvasGroup>();
        serialized.FindProperty("activeChainContent").objectReferenceValue = active.GetComponent<RectTransform>();
        serialized.FindProperty("activeCountText").objectReferenceValue = activeCount;
        serialized.FindProperty("activeTitleText").objectReferenceValue = activeTitle;
        serialized.FindProperty("activeRewardHintText").objectReferenceValue = activeReward;
        serialized.FindProperty("activeAccent").objectReferenceValue = activeAccent;
        serialized.FindProperty("activeInkRevealMask").objectReferenceValue = activeInkRevealMask;
        serialized.FindProperty("activeTextGroup").objectReferenceValue = activeTextGroup;
        serialized.FindProperty("activeBloodInkGroup").objectReferenceValue = activeBloodInkGroup;
        serialized.FindProperty("activeBloodInk").objectReferenceValue = activeBloodInk;
        serialized.FindProperty("settlementGroup").objectReferenceValue = settlement.GetComponent<CanvasGroup>();
        serialized.FindProperty("settlementContent").objectReferenceValue = settlement.GetComponent<RectTransform>();
        serialized.FindProperty("settlementRankText").objectReferenceValue = rank;
        serialized.FindProperty("settlementCountText").objectReferenceValue = count;
        serialized.FindProperty("settlementRewardText").objectReferenceValue = rewards;
        serialized.FindProperty("settlementAccent").objectReferenceValue = settlementAccent;
        serialized.FindProperty("settlementSlashGroup").objectReferenceValue = settlementSlashGroup;
        serialized.FindProperty("settlementSlash").objectReferenceValue = settlementSlash.rectTransform;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        if (player != null) BindPlayerEvents(player, feedback);
        else Debug.LogWarning($"{scene.path} has no PlayerCharacterController to bind.");
    }

    private static void UpgradeActivePrompt(Scene scene)
    {
        GameObject hudRoot = FindRoot(scene, "Root_角色战斗HUD");
        Transform layer = hudRoot != null ? hudRoot.transform.Find("Layer_连斩结算反馈") : null;
        if (layer == null)
            throw new System.InvalidOperationException($"{scene.path} has no authored Layer_连斩结算反馈.");

        KillChainSettlementFeedback feedback = layer.GetComponent<KillChainSettlementFeedback>();
        if (feedback == null)
            throw new System.InvalidOperationException($"{scene.path} has no KillChainSettlementFeedback component.");

        Transform oldActive = layer.Find("Panel_连斩进行中");
        int siblingIndex = oldActive != null ? oldActive.GetSiblingIndex() : 0;
        if (oldActive != null) Object.DestroyImmediate(oldActive.gameObject);

        BuildActivePrompt(layer, out GameObject active, out Text activeCount, out Text activeTitle,
            out Text activeReward, out Image activeAccent, out RectTransform activeInkRevealMask,
            out CanvasGroup activeTextGroup, out CanvasGroup activeBloodInkGroup, out RectTransform activeBloodInk);
        active.transform.SetSiblingIndex(siblingIndex);

        SerializedObject serialized = new(feedback);
        serialized.FindProperty("activeChainGroup").objectReferenceValue = active.GetComponent<CanvasGroup>();
        serialized.FindProperty("activeChainContent").objectReferenceValue = active.GetComponent<RectTransform>();
        serialized.FindProperty("activeCountText").objectReferenceValue = activeCount;
        serialized.FindProperty("activeTitleText").objectReferenceValue = activeTitle;
        serialized.FindProperty("activeRewardHintText").objectReferenceValue = activeReward;
        serialized.FindProperty("activeAccent").objectReferenceValue = activeAccent;
        serialized.FindProperty("activeInkRevealMask").objectReferenceValue = activeInkRevealMask;
        serialized.FindProperty("activeTextGroup").objectReferenceValue = activeTextGroup;
        serialized.FindProperty("activeBloodInkGroup").objectReferenceValue = activeBloodInkGroup;
        serialized.FindProperty("activeBloodInk").objectReferenceValue = activeBloodInk;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildActivePrompt(Transform parent, out GameObject active, out Text activeCount,
        out Text activeTitle, out Text activeReward, out Image activeAccent,
        out RectTransform activeInkRevealMask, out CanvasGroup activeTextGroup,
        out CanvasGroup activeBloodInkGroup, out RectTransform activeBloodInk)
    {
        active = NewUi("Panel_连斩进行中", parent, typeof(CanvasGroup));
        Rect(active.GetComponent<RectTransform>(), new Vector2(.90f, .61f), new Vector2(.90f, .61f),
            Vector2.zero, new Vector2(300f, 160f));
        active.GetComponent<CanvasGroup>().alpha = 0f;

        GameObject inkMask = NewUi("Mask_连斩墨迹揭示", active.transform, typeof(RectMask2D));
        activeInkRevealMask = inkMask.GetComponent<RectTransform>();
        Rect(activeInkRevealMask, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
            new Vector2(-145f, -12f), new Vector2(290f, 112f));
        activeInkRevealMask.pivot = new Vector2(0f, .5f);
        Image activeInk = ImageNode("Img_连斩斩痕墨迹", inkMask.transform, new Vector2(145f, 0f),
            new Vector2(290f, 145f), Color.white);
        activeInk.sprite = LoadUiSprite(ActiveSlashInkPath);
        activeInk.preserveAspect = true;
        activeInk.rectTransform.localEulerAngles = new Vector3(0f, 0f, -2f);

        Image bloodInk = ImageNode("Img_连斩血墨", inkMask.transform, new Vector2(145f, 0f),
            new Vector2(290f, 145f), Color.white);
        bloodInk.sprite = LoadUiSprite(ActiveBloodInkPath);
        bloodInk.preserveAspect = true;
        bloodInk.rectTransform.localEulerAngles = new Vector3(0f, 0f, -2f);
        activeAccent = bloodInk;
        activeBloodInk = bloodInk.rectTransform;
        activeBloodInkGroup = bloodInk.gameObject.AddComponent<CanvasGroup>();
        activeBloodInkGroup.alpha = 0f;

        GameObject textGroup = NewUi("Group_连斩文字", active.transform, typeof(CanvasGroup));
        Rect(textGroup.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
            Vector2.zero, new Vector2(300f, 160f));
        activeTextGroup = textGroup.GetComponent<CanvasGroup>();
        activeTextGroup.alpha = 0f;
        Color warmInkText = new(.94f, .90f, .78f, 1f);
        activeCount = TextNode("Txt_连斩数字", textGroup.transform, new Vector2(-16f, 45f),
            new Vector2(110f, 72f), "0", 58, warmInkText, FontStyle.Bold);
        AddInkOutline(activeCount, new Color(.06f, .05f, .045f, .88f), new Vector2(1.5f, -1.5f));
        activeTitle = TextNode("Txt_连斩标题", textGroup.transform, new Vector2(58f, 40f),
            new Vector2(62f, 32f), "伺 机", 18, warmInkText, FontStyle.Bold);
        AddInkOutline(activeTitle, new Color(.06f, .05f, .045f, .82f), new Vector2(1f, -1f));
        activeReward = TextNode("Txt_连斩奖励提示", textGroup.transform, new Vector2(20f, -51f),
            new Vector2(205f, 24f), "再斩 3 人 · 气势加倍", 13,
            new Color(warmInkText.r, warmInkText.g, warmInkText.b, .72f));
    }

    private static void BindPlayerEvents(PlayerCharacterController player, KillChainSettlementFeedback feedback)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        UnityEvent started = typeof(PlayerCharacterController).GetField("onKillChainStarted", flags)?.GetValue(player) as UnityEvent;
        UnityEvent<int> confirmed = typeof(PlayerCharacterController).GetField("onKillChainKillConfirmed", flags)?.GetValue(player) as UnityEvent<int>;
        UnityEvent<int> ended = typeof(PlayerCharacterController).GetField("onKillChainEnded", flags)?.GetValue(player) as UnityEvent<int>;
        if (started == null || confirmed == null || ended == null)
            throw new System.InvalidOperationException("Player kill-chain UnityEvents could not be resolved.");

        RemoveFeedbackListeners(started, nameof(KillChainSettlementFeedback.BeginKillChain));
        RemoveFeedbackListeners(confirmed, nameof(KillChainSettlementFeedback.ConfirmKill));
        RemoveFeedbackListeners(ended, nameof(KillChainSettlementFeedback.EndKillChain));
        UnityEventTools.AddPersistentListener(started, feedback.BeginKillChain);
        UnityEventTools.AddPersistentListener(confirmed, feedback.ConfirmKill);
        UnityEventTools.AddPersistentListener(ended, feedback.EndKillChain);
        EditorUtility.SetDirty(player);
    }

    private static void RemoveFeedbackListeners(UnityEventBase unityEvent, string methodName)
    {
        for (int i = unityEvent.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            Object target = unityEvent.GetPersistentTarget(i);
            if (unityEvent.GetPersistentMethodName(i) == methodName
                && (target == null || target is KillChainSettlementFeedback))
                UnityEventTools.RemovePersistentListener(unityEvent, i);
        }
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == name) return root;
        return null;
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T found = root.GetComponentInChildren<T>(true);
            if (found != null) return found;
        }
        return null;
    }

    private static GameObject NewUi(string name, Transform parent, params System.Type[] components)
    {
        GameObject go = new(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        if (parent != null) go.transform.SetParent(parent, false);
        foreach (System.Type component in components) go.AddComponent(component);
        return go;
    }

    private static void ConfigureUiSprite(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.textureShape = TextureImporterShape.Texture2D;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }

    private static Sprite LoadUiSprite(string assetPath)
    {
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite != null) return sprite;

        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            if (asset is Sprite importedSprite) return importedSprite;

        throw new System.InvalidOperationException($"{assetPath} was not imported as a Sprite.");
    }

    private static Image ImageNode(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        GameObject go = NewUi(name, parent, typeof(CanvasRenderer), typeof(Image));
        Rect(go.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f), position, size);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Text TextNode(string name, Transform parent, Vector2 position, Vector2 size, string value, int fontSize, Color color, FontStyle style = FontStyle.Normal)
    {
        GameObject go = NewUi(name, parent, typeof(CanvasRenderer), typeof(Text));
        Rect(go.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f), position, size);
        Text text = go.GetComponent<Text>();
        text.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/NotoSerifCJKsc-Regular.otf")
            ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private static void AddInkOutline(Text text, Color color, Vector2 distance)
    {
        Outline outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
        outline.useGraphicAlpha = true;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static void Rect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
