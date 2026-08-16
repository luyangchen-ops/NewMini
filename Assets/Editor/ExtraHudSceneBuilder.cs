using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ExtraHudSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Extra.unity";

    [MenuItem("NewMini/UI/Rebuild Extra HUD")]
    public static void Build()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == "Root_角色战斗HUD" || root.name == "Root_ExtraHUD")
                Object.DestroyImmediate(root);
        }

        GameObject rootHud = NewUi("Root_角色战斗HUD", null, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(ExtraHudController));
        Canvas canvas = rootHud.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        CanvasScaler scaler = rootHud.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;

        RectTransform layer = Rect(NewUi("Layer_角色状态HUD", rootHud.transform), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        Transform health = Group("Group_角色血量", layer);
        Image healthBg = ImageNode("Panel_角色血量背景", health, new Vector2(200, -65), new Vector2(370, 55), new Color(.03f, .1f, .14f, .92f), new Vector2(0, 1));
        Image healthFill = ImageNode("Img_角色血量填充", health, new Vector2(200, -65), new Vector2(350, 39), new Color(.86f, .1f, .14f, 1f), new Vector2(0, 1));
        healthFill.type = Image.Type.Filled;
        healthFill.fillMethod = Image.FillMethod.Horizontal;
        TextNode("Txt_角色血量", health, new Vector2(200, -65), new Vector2(350, 39), "角色血量  100 / 100", new Vector2(0, 1));

        Transform dodge = Group("Group_翻滚冷却", layer);
        ImageNode("Panel_翻滚冷却背景", dodge, new Vector2(94, 94), new Vector2(105, 105), new Color(.03f, .1f, .14f, .92f), Vector2.zero);
        Image dodgeFill = ImageNode("Img_翻滚冷却扇形", dodge, new Vector2(94, 94), new Vector2(88, 88), new Color(.35f, .37f, .42f, .92f), Vector2.zero);
        dodgeFill.type = Image.Type.Filled;
        dodgeFill.fillMethod = Image.FillMethod.Radial360;
        dodgeFill.fillClockwise = false;
        TextNode("Txt_翻滚冷却", dodge, new Vector2(94, 94), new Vector2(140, 36), "翻滚  就绪", Vector2.zero);

        Transform knife = Group("Group_飞刀栏", layer);
        ImageNode("Panel_飞刀栏背景", knife, new Vector2(-180, 78), new Vector2(315, 58), new Color(.03f, .1f, .14f, .92f), Vector2.one);
        TextNode("Txt_飞刀栏", knife, new Vector2(-180, 78), new Vector2(300, 45), "飞刀  Q   x0", Vector2.one);

        Transform momentum = Group("Group_势条", layer);
        ImageNode("Panel_势条背景", momentum, new Vector2(0, 52), new Vector2(570, 36), new Color(.03f, .1f, .14f, .92f), new Vector2(.5f, 0));
        Image momentumFill = ImageNode("Img_势条填充", momentum, new Vector2(0, 52), new Vector2(550, 21), new Color(.33f, .68f, .85f, 1f), new Vector2(.5f, 0));
        momentumFill.type = Image.Type.Filled;
        momentumFill.fillMethod = Image.FillMethod.Horizontal;
        TextNode("Txt_势条", momentum, new Vector2(0, 52), new Vector2(550, 30), "势条  0 / 20", new Vector2(.5f, 0));

        PlayerCharacterController player = Object.FindAnyObjectByType<PlayerCharacterController>();
        SerializedObject hud = new SerializedObject(rootHud.GetComponent<ExtraHudController>());
        hud.FindProperty("player").objectReferenceValue = player;
        hud.FindProperty("healthFill").objectReferenceValue = healthFill;
        hud.FindProperty("healthText").objectReferenceValue = Find<Text>(health, "Txt_角色血量");
        hud.FindProperty("dodgeCooldownFill").objectReferenceValue = dodgeFill;
        hud.FindProperty("dodgeText").objectReferenceValue = Find<Text>(dodge, "Txt_翻滚冷却");
        hud.FindProperty("knifeText").objectReferenceValue = Find<Text>(knife, "Txt_飞刀栏");
        hud.FindProperty("momentumFill").objectReferenceValue = momentumFill;
        hud.FindProperty("momentumText").objectReferenceValue = Find<Text>(momentum, "Txt_势条");
        hud.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Extra HUD rebuilt and saved with named hierarchy objects.");
    }

    private static Transform Group(string name, Transform parent) => Rect(NewUi(name, parent), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

    private static GameObject NewUi(string name, Transform parent, params System.Type[] extraTypes)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        if (parent != null) go.transform.SetParent(parent, false);
        foreach (System.Type type in extraTypes) go.AddComponent(type);
        return go;
    }

    private static RectTransform Rect(GameObject go, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
    {
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static Image ImageNode(string name, Transform parent, Vector2 position, Vector2 size, Color color, Vector2 anchor)
    {
        GameObject go = NewUi(name, parent, typeof(CanvasRenderer), typeof(Image));
        Rect(go, anchor, anchor, position, size);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Text TextNode(string name, Transform parent, Vector2 position, Vector2 size, string value, Vector2 anchor)
    {
        GameObject go = NewUi(name, parent, typeof(CanvasRenderer), typeof(Text));
        Rect(go, anchor, anchor, position, size);
        Text text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = 18;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private static T Find<T>(Transform parent, string name) where T : Component
    {
        foreach (T component in parent.GetComponentsInChildren<T>(true))
            if (component.name == name) return component;
        return null;
    }
}
