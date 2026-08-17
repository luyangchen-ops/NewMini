using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Authors the final victory panel directly into Level_LD's existing UI canvas.</summary>
[InitializeOnLoad]
public static class VictoryUiSceneBuilder
{
    private const string LevelScenePath = "Assets/Scenes/Level_LD.unity";

    static VictoryUiSceneBuilder()
    {
        EditorApplication.delayCall += EnsureActiveLevelScene;
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    [MenuItem("NewMini/UI/Add Victory Panel To Level_LD")]
    public static void AddVictoryPanelToLevel()
    {
        Scene scene = EditorSceneManager.OpenScene(LevelScenePath, OpenSceneMode.Single);
        EnsureVictoryPanel(scene);
    }

    private static void EnsureActiveLevelScene()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode) EnsureVictoryPanel(SceneManager.GetActiveScene());
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (scene.path == LevelScenePath)
            EditorApplication.delayCall += () => EnsureVictoryPanel(scene);
    }

    private static void EnsureVictoryPanel(Scene scene)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.IsValid() || !scene.isLoaded || scene.path != LevelScenePath) return;

        GameStateUIController controller = Object.FindAnyObjectByType<GameStateUIController>();
        if (controller == null || controller.transform.Find("Panel_Victory") != null) return;

        GameObject panel = ScreenPanel("Panel_Victory", controller.transform, new Color(.015f, .012f, .025f, .95f));
        Text title = TextNode("Txt_VictoryTitle", panel.transform, new Vector2(0f, 180f), new Vector2(1000f, 120f), "\u80DC\u5229", 80, new Color(1f, .82f, .35f, 1f));
        title.fontStyle = FontStyle.Bold;
        TextNode("Txt_ToBeContinued", panel.transform, new Vector2(0f, 55f), new Vector2(1000f, 76f), "\u672A\u5B8C\u5F85\u7EED", 42, Color.white);
        TextNode("Txt_VictoryMessage", panel.transform, new Vector2(0f, -35f), new Vector2(1000f, 56f), "\u8C22\u8C22\u6E38\u73A9", 24, new Color(.8f, .82f, .9f, 1f));
        Button restart = ButtonNode("Btn_VictoryRestart", panel.transform, new Vector2(0f, -175f), "\u91CD\u65B0\u5F00\u59CB");
        Button exit = ButtonNode("Btn_VictoryExit", panel.transform, new Vector2(0f, -290f), "\u9000\u51FA\u5230\u4E3B\u83DC\u5355");
        UnityEventTools.AddPersistentListener(restart.onClick, controller.RestartCurrentLevel);
        UnityEventTools.AddPersistentListener(exit.onClick, controller.ReturnToMainMenu);

        SerializedObject serialized = new(controller);
        serialized.FindProperty("panelVictory").objectReferenceValue = panel;
        serialized.FindProperty("victoryRestartButton").objectReferenceValue = restart;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        panel.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    private static GameObject ScreenPanel(string name, Transform parent, Color color)
    {
        GameObject panel = NewUi(name, parent, typeof(CanvasRenderer), typeof(Image));
        Stretch(panel.GetComponent<RectTransform>());
        Image image = panel.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        return panel;
    }

    private static Button ButtonNode(string name, Transform parent, Vector2 position, string label)
    {
        GameObject buttonObject = NewUi(name, parent, typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(430f, 82f);
        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(.22f, .05f, .05f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(.52f, .1f, .08f, 1f);
        colors.pressedColor = new Color(.72f, .04f, .02f, 1f);
        button.colors = colors;
        TextNode("Txt_ButtonLabel", buttonObject.transform, Vector2.zero, Vector2.zero, label, 28, Color.white, true);
        return button;
    }

    private static Text TextNode(string name, Transform parent, Vector2 position, Vector2 size, string value, int fontSize, Color color, bool stretch = false)
    {
        GameObject textObject = NewUi(name, parent, typeof(CanvasRenderer), typeof(Text));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        if (stretch) Stretch(rect, new Vector2(-24f, -12f));
        else
        {
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect, Vector2? sizeDelta = null)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = sizeDelta ?? Vector2.zero;
    }

    private static GameObject NewUi(string name, Transform parent, params System.Type[] components)
    {
        GameObject gameObject = new(name, typeof(RectTransform));
        gameObject.layer = LayerMask.NameToLayer("UI");
        gameObject.transform.SetParent(parent, false);
        foreach (System.Type component in components) gameObject.AddComponent(component);
        return gameObject;
    }
}
