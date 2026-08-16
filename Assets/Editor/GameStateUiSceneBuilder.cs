using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Authors the pause, help, and death UI directly into Extra.unity.</summary>
[InitializeOnLoad]
public static class GameStateUiSceneBuilder
{
    private const string ExtraScenePath = "Assets/Scenes/Extra.unity";
    private const string RootName = "Root_游戏状态UI";

    static GameStateUiSceneBuilder()
    {
        EditorApplication.delayCall += EnsureExtraScene;
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    [MenuItem("NewMini/UI/Rebuild Pause, Help And Death UI In Extra")]
    public static void RebuildExtraScene()
    {
        Scene scene = EditorSceneManager.OpenScene(ExtraScenePath, OpenSceneMode.Single);
        GameObject existing = GameObject.Find(RootName);
        if (existing != null) Object.DestroyImmediate(existing);
        Build(scene);
    }

    private static void EnsureExtraScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        EnsureExtraScene(SceneManager.GetActiveScene());
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (scene.path != ExtraScenePath) return;
        EditorApplication.delayCall += () => EnsureExtraScene(scene);
    }

    private static void EnsureExtraScene(Scene scene)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.IsValid() || !scene.isLoaded) return;
        if (scene.path != ExtraScenePath) return;
        GameObject existing = GameObject.Find(RootName);
        if (existing != null)
        {
            if (existing.transform.Find("Panel_Help") != null) return;
            Object.DestroyImmediate(existing);
        }
        Build(scene);
    }

    private static void Build(Scene scene)
    {
        EnsureEventSystem(scene);

        GameObject root = NewUi(RootName, null, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(GameStateUIController));
        Canvas canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 200;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = .5f;

        GameObject pause = ScreenPanel("Panel_Pause", root.transform, new Color(.015f, .02f, .03f, .88f));
        TextNode("Txt_PauseTitle", pause.transform, new Vector2(0f, 235f), new Vector2(700f, 110f), "游戏暂停", 58, Color.white);
        Button pauseContinue = ButtonNode("Btn_PauseContinue", pause.transform, new Vector2(0f, 75f), "从上一个存档点继续");
        Button pauseHelp = ButtonNode("Btn_PauseHelp", pause.transform, new Vector2(0f, -45f), "帮助 / 操作说明");
        Button pauseMenu = ButtonNode("Btn_PauseMainMenu", pause.transform, new Vector2(0f, -165f), "返回主菜单");

        GameObject help = ScreenPanel("Panel_Help", root.transform, new Color(.01f, .015f, .025f, 1f));
        TextNode("Txt_HelpTitle", help.transform, new Vector2(0f, 450f), new Vector2(1000f, 80f), "操作说明", 52, Color.white);
        GameObject pagesRoot = NewUi("Group_HelpPages", help.transform);
        RectTransform pagesRect = pagesRoot.GetComponent<RectTransform>(); pagesRect.anchorMin = pagesRect.anchorMax = new Vector2(.5f, .5f); pagesRect.anchoredPosition = new Vector2(0f, 25f); pagesRect.sizeDelta = new Vector2(1320f, 760f);
        GameObject[] pages =
        {
            HelpPage(
                pagesRoot.transform,
                "Page_01_MovementAim",
                "Assets/Resources/UI/Tutorial/Artwork/Tutorial_Page01_MovementAim.png",
                "移动与瞄准",
                "WASD：控制移动\n移动鼠标：调整朝向与攻击方向"),
            HelpPage(
                pagesRoot.transform,
                "Page_02_NormalAttack",
                "Assets/Resources/UI/Tutorial/Artwork/Tutorial_Page02_NormalAttack.png",
                "挥剑攻击",
                "鼠标左键：向光标方向挥剑攻击\n靠近敌人后发起斩击"),
            HelpPage(
                pagesRoot.transform,
                "Page_03_DodgePerfectDodge",
                "Assets/Resources/UI/Tutorial/Artwork/Tutorial_Page03_DodgePerfectDodge.png",
                "闪避，抓住反击时机",
                "Space：向光标方向闪避\n贴近敌人攻击瞬间闪避，可触发完美闪避"),
            HelpPage(
                pagesRoot.transform,
                "Page_04_KillChain",
                "Assets/Resources/UI/Tutorial/Artwork/Tutorial_Page04_KillChain.png",
                "完美闪避后：连续处决",
                "将鼠标移向范围内敌人，左键发动连斩\n连续选择下一个目标；右键可取消"),
            HelpPage(
                pagesRoot.transform,
                "Page_05_Ultimate",
                "Assets/Resources/UI/Tutorial/Artwork/Tutorial_Page05_Ultimate.png",
                "终极：划出必杀之路",
                "R：进入终极标记\n按住鼠标左键拖拽划过敌人，松开后执行连斩；右键取消")
        };
        Button previous = ButtonNode("Btn_HelpPrevious", help.transform, new Vector2(-520f, -445f), "〈 上一页", new Vector2(230f, 70f), 24);
        Button next = ButtonNode("Btn_HelpNext", help.transform, new Vector2(520f, -445f), "下一页 〉", new Vector2(230f, 70f), 24);
        Text pageIndicator = TextNode("Txt_HelpPageIndicator", help.transform, new Vector2(535f, -282f), new Vector2(150f, 50f), "1 / 5", 24, new Color(1f, .84f, .48f, 1f));
        Button helpBack = ButtonNode("Btn_HelpBack", help.transform, new Vector2(0f, -520f), "返回暂停菜单", new Vector2(300f, 62f), 22);

        GameObject death = ScreenPanel("Panel_Death", root.transform, new Color(.035f, .005f, .008f, .93f));
        Text deathMark = TextNode("Txt_DeathMark", death.transform, new Vector2(0f, 250f), new Vector2(520f, 300f), "卒", 220, new Color(.78f, .015f, .02f, 1f)); deathMark.fontStyle = FontStyle.Bold;
        TextNode("Txt_DeathTitle", death.transform, new Vector2(0f, 75f), new Vector2(700f, 80f), "此身已殒", 42, new Color(.92f, .82f, .78f, 1f));
        Button deathContinue = ButtonNode("Btn_DeathContinue", death.transform, new Vector2(0f, -75f), "从上一个存档点继续");
        Button deathMenu = ButtonNode("Btn_DeathMainMenu", death.transform, new Vector2(0f, -205f), "返回主菜单");

        GameStateUIController controller = root.GetComponent<GameStateUIController>();
        UnityEventTools.AddPersistentListener(pauseContinue.onClick, controller.ContinueFromCheckpoint); UnityEventTools.AddPersistentListener(pauseHelp.onClick, controller.ShowHelp); UnityEventTools.AddPersistentListener(pauseMenu.onClick, controller.ReturnToMainMenu); UnityEventTools.AddPersistentListener(previous.onClick, controller.PreviousHelpPage); UnityEventTools.AddPersistentListener(next.onClick, controller.NextHelpPage); UnityEventTools.AddPersistentListener(helpBack.onClick, controller.CloseHelp); UnityEventTools.AddPersistentListener(deathContinue.onClick, controller.ContinueFromCheckpoint); UnityEventTools.AddPersistentListener(deathMenu.onClick, controller.ReturnToMainMenu);
        SerializedObject serialized = new SerializedObject(controller);
        serialized.FindProperty("player").objectReferenceValue = Object.FindAnyObjectByType<PlayerCharacterController>(); serialized.FindProperty("gameplayHudRoot").objectReferenceValue = GameObject.Find("Root_角色战斗HUD"); serialized.FindProperty("panelPause").objectReferenceValue = pause; serialized.FindProperty("panelHelp").objectReferenceValue = help; serialized.FindProperty("panelDeath").objectReferenceValue = death; serialized.FindProperty("pauseContinueButton").objectReferenceValue = pauseContinue; serialized.FindProperty("helpPreviousButton").objectReferenceValue = previous; serialized.FindProperty("helpNextButton").objectReferenceValue = next; serialized.FindProperty("helpPageIndicator").objectReferenceValue = pageIndicator; serialized.FindProperty("helpPages").arraySize = pages.Length;
        for (int i = 0; i < pages.Length; i++) serialized.FindProperty("helpPages").GetArrayElementAtIndex(i).objectReferenceValue = pages[i];
        serialized.FindProperty("deathContinueButton").objectReferenceValue = deathContinue; serialized.ApplyModifiedPropertiesWithoutUndo();
        pause.SetActive(false); help.SetActive(false); death.SetActive(false); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
    }

    private static void EnsureEventSystem(Scene scene)
    {
        if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;

        GameObject eventSystem = new GameObject(
            "Root_EventSystem",
            typeof(UnityEngine.EventSystems.EventSystem),
            typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        SceneManager.MoveGameObjectToScene(eventSystem, scene);
    }

    private static GameObject HelpPage(Transform parent, string name, string artworkPath, string title, string description)
    {
        GameObject page = NewUi(name, parent); Stretch(page.GetComponent<RectTransform>());
        GameObject artwork = NewUi("Img_TutorialArtwork", page.transform, typeof(CanvasRenderer), typeof(Image));
        RectTransform artworkRect = artwork.GetComponent<RectTransform>();
        artworkRect.anchorMin = artworkRect.anchorMax = new Vector2(.5f, .5f);
        artworkRect.anchoredPosition = Vector2.zero;
        artworkRect.sizeDelta = new Vector2(1280f, 720f);
        Image artworkImage = artwork.GetComponent<Image>();
        artworkImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(artworkPath);
        artworkImage.color = Color.white;
        artworkImage.preserveAspect = true;
        artworkImage.raycastTarget = false;
        if (artworkImage.sprite == null) Debug.LogError($"Missing tutorial artwork: {artworkPath}");

        Text pageTitle = TextNode("Txt_PageTitle", page.transform, new Vector2(0f, 315f), new Vector2(1060f, 64f), title, 38, new Color(1f, .88f, .6f, 1f));
        pageTitle.fontStyle = FontStyle.Bold;
        TextNode("Txt_PageDescription", page.transform, new Vector2(0f, -302f), new Vector2(1080f, 86f), description, 25, new Color(.95f, .96f, 1f, 1f));
        return page;
    }

    private static GameObject ScreenPanel(string name, Transform parent, Color color) { GameObject panel = NewUi(name, parent, typeof(CanvasRenderer), typeof(Image)); Stretch(panel.GetComponent<RectTransform>()); Image image = panel.GetComponent<Image>(); image.color = color; image.raycastTarget = true; return panel; }
    private static Button ButtonNode(string name, Transform parent, Vector2 position, string label, Vector2? size = null, int fontSize = 28) { GameObject go = NewUi(name, parent, typeof(CanvasRenderer), typeof(Image), typeof(Button)); RectTransform rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = size ?? new Vector2(470f, 92f); Image image = go.GetComponent<Image>(); image.color = new Color(.14f, .12f, .12f, .98f); Button button = go.GetComponent<Button>(); button.targetGraphic = image; ColorBlock colors = button.colors; colors.highlightedColor = new Color(.48f, .08f, .07f, 1f); colors.pressedColor = new Color(.68f, .035f, .025f, 1f); colors.selectedColor = colors.highlightedColor; button.colors = colors; TextNode("Txt_ButtonLabel", go.transform, Vector2.zero, Vector2.zero, label, fontSize, Color.white, true); return button; }
    private static Text TextNode(string name, Transform parent, Vector2 position, Vector2 size, string value, int fontSize, Color color, bool stretch = false) { GameObject go = NewUi(name, parent, typeof(CanvasRenderer), typeof(Text)); RectTransform rect = go.GetComponent<RectTransform>(); if (stretch) Stretch(rect, new Vector2(-24f, -12f)); else { rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = size; } Text text = go.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.text = value; text.fontSize = fontSize; text.alignment = TextAnchor.MiddleCenter; text.color = color; text.raycastTarget = false; return text; }
    private static void Stretch(RectTransform rect, Vector2? sizeDelta = null) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.anchoredPosition = Vector2.zero; rect.sizeDelta = sizeDelta ?? Vector2.zero; }
    private static GameObject NewUi(string name, Transform parent, params System.Type[] components) { GameObject go = new GameObject(name, typeof(RectTransform)); go.layer = LayerMask.NameToLayer("UI"); if (parent != null) go.transform.SetParent(parent, false); foreach (System.Type component in components) go.AddComponent(component); return go; }
}
