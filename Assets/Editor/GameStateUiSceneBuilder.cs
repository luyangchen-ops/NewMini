using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class GameStateUiSceneBuilder
{
    private const string ExtraScenePath = "Assets/Scenes/Extra.unity";
    private const string RootName = "Root_游戏状态UI";

    static GameStateUiSceneBuilder()
    {
        EditorApplication.delayCall += EnsureExtraScene;
    }

    [MenuItem("NewMini/UI/Rebuild Pause And Death UI In Extra")]
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
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ExtraScenePath || GameObject.Find(RootName) != null) return;
        Build(scene);
    }

    private static void Build(Scene scene)
    {
        GameObject root = NewUi(RootName, null, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(GameStateUIController));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;

        GameObject pause = ScreenPanel("Panel_暂停界面", root.transform, new Color(.015f, .02f, .03f, .88f));
        TextNode("Txt_暂停标题", pause.transform, new Vector2(0f, 190f), new Vector2(700f, 110f), "游戏暂停", 58, Color.white);
        Button pauseContinue = ButtonNode("Btn_暂停_从存档点继续", pause.transform, new Vector2(0f, 25f), "从上个存档点继续");
        Button pauseMenu = ButtonNode("Btn_暂停_返回主菜单", pause.transform, new Vector2(0f, -105f), "返回主菜单");

        GameObject death = ScreenPanel("Panel_死亡界面", root.transform, new Color(.035f, .005f, .008f, .93f));
        Text deathMark = TextNode("Txt_死亡卒字", death.transform, new Vector2(0f, 250f), new Vector2(520f, 300f), "卒", 220, new Color(.78f, .015f, .02f, 1f));
        deathMark.fontStyle = FontStyle.Bold;
        TextNode("Txt_死亡标题", death.transform, new Vector2(0f, 75f), new Vector2(700f, 80f), "此身已殁", 42, new Color(.92f, .82f, .78f, 1f));
        Button deathContinue = ButtonNode("Btn_死亡_从存档点继续", death.transform, new Vector2(0f, -75f), "从上个存档点继续");
        Button deathMenu = ButtonNode("Btn_死亡_返回主菜单", death.transform, new Vector2(0f, -205f), "返回主菜单");

        GameStateUIController controller = root.GetComponent<GameStateUIController>();
        UnityEventTools.AddPersistentListener(pauseContinue.onClick, controller.ContinueFromCheckpoint);
        UnityEventTools.AddPersistentListener(pauseMenu.onClick, controller.ReturnToMainMenu);
        UnityEventTools.AddPersistentListener(deathContinue.onClick, controller.ContinueFromCheckpoint);
        UnityEventTools.AddPersistentListener(deathMenu.onClick, controller.ReturnToMainMenu);

        PlayerCharacterController player = Object.FindAnyObjectByType<PlayerCharacterController>();
        GameObject hud = GameObject.Find("Root_角色战斗HUD");
        SerializedObject serialized = new SerializedObject(controller);
        serialized.FindProperty("player").objectReferenceValue = player;
        serialized.FindProperty("gameplayHudRoot").objectReferenceValue = hud;
        serialized.FindProperty("panelPause").objectReferenceValue = pause;
        serialized.FindProperty("panelDeath").objectReferenceValue = death;
        serialized.FindProperty("pauseContinueButton").objectReferenceValue = pauseContinue;
        serialized.FindProperty("deathContinueButton").objectReferenceValue = deathContinue;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        pause.SetActive(false);
        death.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Pause and death UI authored in Extra scene hierarchy.");
    }

    private static GameObject ScreenPanel(string name, Transform parent, Color color)
    {
        GameObject panel = NewUi(name, parent, typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        Image image = panel.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        return panel;
    }

    private static Button ButtonNode(string name, Transform parent, Vector2 position, string label)
    {
        GameObject go = NewUi(name, parent, typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(470f, 92f);
        Image image = go.GetComponent<Image>();
        image.color = new Color(.14f, .12f, .12f, .98f);
        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(.48f, .08f, .07f, 1f);
        colors.pressedColor = new Color(.68f, .035f, .025f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
        TextNode("Txt_按钮文字", go.transform, Vector2.zero, Vector2.zero, label, 28, Color.white, true);
        return button;
    }

    private static Text TextNode(string name, Transform parent, Vector2 position, Vector2 size, string value, int fontSize, Color color, bool stretch = false)
    {
        GameObject go = NewUi(name, parent, typeof(CanvasRenderer), typeof(Text));
        RectTransform rect = go.GetComponent<RectTransform>();
        if (stretch)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(-24f, -12f);
        }
        else
        {
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
        Text text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private static GameObject NewUi(string name, Transform parent, params System.Type[] components)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        if (parent != null) go.transform.SetParent(parent, false);
        foreach (System.Type component in components) go.AddComponent(component);
        return go;
    }
}
