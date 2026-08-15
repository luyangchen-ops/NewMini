using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Creates scene-authored arena objects. Nothing is generated at runtime.</summary>
public sealed class MiniArenaLevelEditorWindow : EditorWindow
{
    private string arenaName = "Arena_01";
    private float gateThickness = .35f;
    private bool isDrawingArea;
    private bool hasArea;
    private Vector2 dragStart;
    private Vector2 dragEnd;

    [MenuItem("Tools/NewMini/箱庭关卡编辑器")]
    public static void Open() => GetWindow<MiniArenaLevelEditorWindow>("箱庭关卡编辑器");

    private void OnEnable() => SceneView.duringSceneGui += DrawSceneAreaSelector;

    private void OnDisable() => SceneView.duringSceneGui -= DrawSceneAreaSelector;

    private void OnGUI()
    {
        EditorGUILayout.LabelField("箱庭关卡编辑器", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("点击“开始框选”后，在 Scene 视图左键拖拽出战区。区域会直接贴合地图坐标，所有节点仍保存在当前场景中。按 Esc 可取消。", MessageType.Info);
        arenaName = EditorGUILayout.TextField("区域名称", arenaName);
        gateThickness = EditorGUILayout.Slider("边界厚度", gateThickness, .1f, 1f);

        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(arenaName)))
        {
            if (GUILayout.Button(isDrawingArea ? "框选中：回到 Scene 视图拖拽" : "开始框选战区", GUILayout.Height(32)))
            {
                isDrawingArea = !isDrawingArea;
                hasArea = false;
                SceneView.RepaintAll();
            }

            using (new EditorGUI.DisabledScope(!hasArea || isDrawingArea))
            {
                if (GUILayout.Button("按框选范围创建箱庭战区", GUILayout.Height(28)))
                {
                    CreateArena(GetAreaCenter(), GetAreaSize());
                    hasArea = false;
                    SceneView.RepaintAll();
                }
            }
        }

        EditorGUILayout.Space(12f);
        EditorGUILayout.LabelField("敌人波次", EditorStyles.boldLabel);
        ArenaCombatZone selectedZone = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponent<ArenaCombatZone>()
            : null;
        EditorGUILayout.HelpBox(selectedZone != null
            ? "已选中战区：可为它创建波次生成器。之后在 Inspector 的 Waves 中指定每波敌人预制体、数量和生成点。"
            : "在 Hierarchy 中选中 Group_CombatZone，再创建波次生成器。"
            , selectedZone != null ? MessageType.Info : MessageType.None);
        using (new EditorGUI.DisabledScope(selectedZone == null))
        {
            if (GUILayout.Button("为选中战区添加波次生成器", GUILayout.Height(28)))
                CreateWaveSpawner(selectedZone);
        }
    }

    private void DrawSceneAreaSelector(SceneView sceneView)
    {
        if (!isDrawingArea && !hasArea) return;

        Event current = Event.current;
        if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
        {
            isDrawingArea = false;
            hasArea = false;
            current.Use();
            Repaint();
            return;
        }

        if (isDrawingArea && current.button == 0 && !current.alt)
        {
            if (current.type == EventType.MouseDown)
            {
                dragStart = dragEnd = GetMouseWorldPosition(current.mousePosition);
                current.Use();
            }
            else if (current.type == EventType.MouseDrag)
            {
                dragEnd = GetMouseWorldPosition(current.mousePosition);
                current.Use();
                sceneView.Repaint();
            }
            else if (current.type == EventType.MouseUp)
            {
                dragEnd = GetMouseWorldPosition(current.mousePosition);
                isDrawingArea = false;
                hasArea = GetAreaSize().x >= .1f && GetAreaSize().y >= .1f;
                current.Use();
                Repaint();
            }
        }

        if (isDrawingArea || hasArea) DrawAreaPreview();
    }

    private static Vector2 GetMouseWorldPosition(Vector2 guiPosition)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(guiPosition);
        Plane mapPlane = new(Vector3.forward, Vector3.zero);
        return mapPlane.Raycast(ray, out float distance) ? ray.GetPoint(distance) : Vector3.zero;
    }

    private void DrawAreaPreview()
    {
        Vector2 min = Vector2.Min(dragStart, dragEnd);
        Vector2 max = Vector2.Max(dragStart, dragEnd);
        Vector3[] corners =
        {
            new(min.x, min.y, 0f), new(min.x, max.y, 0f), new(max.x, max.y, 0f), new(max.x, min.y, 0f)
        };
        Handles.DrawSolidRectangleWithOutline(corners, new Color(.15f, .75f, 1f, .15f), new Color(.15f, .85f, 1f, 1f));
        Handles.Label(new Vector3(min.x, max.y, 0f), $"战区 {GetAreaSize().x:F1} × {GetAreaSize().y:F1}");
    }

    private Vector2 GetAreaCenter() => (dragStart + dragEnd) * .5f;
    private Vector2 GetAreaSize() => Vector2.Max(Vector2.one * .1f, Vector2.Max(dragStart, dragEnd) - Vector2.Min(dragStart, dragEnd));

    private void CreateArena(Vector2 center, Vector2 selectedSize)
    {
        Vector2 size = new(Mathf.Max(1f, selectedSize.x), Mathf.Max(1f, selectedSize.y));
        GameObject root = new($"Root_{arenaName}");
        Undo.RegisterCreatedObjectUndo(root, "Create Mini Arena");
        root.transform.position = new Vector3(center.x, center.y, 0f);

        GameObject zoneObject = new("Group_CombatZone");
        zoneObject.transform.SetParent(root.transform);
        BoxCollider2D zoneCollider = zoneObject.AddComponent<BoxCollider2D>();
        zoneCollider.size = size;
        zoneCollider.isTrigger = true;
        ArenaCombatZone zone = zoneObject.AddComponent<ArenaCombatZone>();

        GameObject gatesGroup = new("Group_BoundaryGates");
        gatesGroup.transform.SetParent(root.transform);
        ArenaBoundaryGate[] gates =
        {
            CreateGate("Gate_North", gatesGroup.transform, new Vector2(0f, size.y * .5f), new Vector2(size.x + gateThickness * 2f, gateThickness)),
            CreateGate("Gate_South", gatesGroup.transform, new Vector2(0f, -size.y * .5f), new Vector2(size.x + gateThickness * 2f, gateThickness)),
            CreateGate("Gate_East", gatesGroup.transform, new Vector2(size.x * .5f, 0f), new Vector2(gateThickness, size.y)),
            CreateGate("Gate_West", gatesGroup.transform, new Vector2(-size.x * .5f, 0f), new Vector2(gateThickness, size.y))
        };

        GameObject respawnObject = new("Group_RespawnPoint");
        respawnObject.transform.SetParent(root.transform);
        respawnObject.transform.localPosition = new Vector3(0f, -size.y * .35f, 0f);
        CircleCollider2D respawnCollider = respawnObject.AddComponent<CircleCollider2D>();
        respawnCollider.isTrigger = true;
        respawnCollider.radius = .5f;
        RespawnPoint respawnPoint = respawnObject.AddComponent<RespawnPoint>();

        SerializedObject serializedZone = new(zone);
        SerializedProperty gateProperty = serializedZone.FindProperty("boundaryGates");
        gateProperty.arraySize = gates.Length;
        for (int i = 0; i < gates.Length; i++) gateProperty.GetArrayElementAtIndex(i).objectReferenceValue = gates[i];
        serializedZone.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject serializedRespawn = new(respawnPoint);
        serializedRespawn.FindProperty("pointId").stringValue = $"{arenaName}_checkpoint";
        serializedRespawn.FindProperty("activeOnLevelStart").boolValue = true;
        serializedRespawn.ApplyModifiedPropertiesWithoutUndo();

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);
        SceneView.lastActiveSceneView?.FrameSelected();
    }

    private static void CreateWaveSpawner(ArenaCombatZone zone)
    {
        GameObject spawnerObject = new("Group_WaveSpawner");
        Undo.RegisterCreatedObjectUndo(spawnerObject, "Create Arena Wave Spawner");
        spawnerObject.transform.SetParent(zone.transform.parent != null ? zone.transform.parent : zone.transform);
        spawnerObject.transform.position = zone.transform.position;
        ArenaWaveSpawner spawner = spawnerObject.AddComponent<ArenaWaveSpawner>();

        GameObject pointsGroup = new("Group_SpawnPoints");
        pointsGroup.transform.SetParent(spawnerObject.transform);
        GameObject pointObject = new("Group_SpawnPoint_01");
        pointObject.transform.SetParent(pointsGroup.transform);
        pointObject.transform.position = zone.transform.position;

        GameObject enemiesGroup = new("Group_SpawnedEnemies");
        enemiesGroup.transform.SetParent(spawnerObject.transform);

        SerializedObject serializedSpawner = new(spawner);
        serializedSpawner.FindProperty("spawnedEnemyParent").objectReferenceValue = enemiesGroup.transform;
        SerializedProperty waves = serializedSpawner.FindProperty("waves");
        waves.arraySize = 1;
        SerializedProperty firstWave = waves.GetArrayElementAtIndex(0);
        firstWave.FindPropertyRelative("waveName").stringValue = "Wave 1";
        SerializedProperty spawns = firstWave.FindPropertyRelative("spawns");
        spawns.arraySize = 1;
        SerializedProperty firstSpawn = spawns.GetArrayElementAtIndex(0);
        firstSpawn.FindPropertyRelative("count").intValue = 1;
        SerializedProperty spawnPoints = firstSpawn.FindPropertyRelative("spawnPoints");
        spawnPoints.arraySize = 1;
        spawnPoints.GetArrayElementAtIndex(0).objectReferenceValue = pointObject.transform;
        serializedSpawner.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject serializedZone = new(zone);
        SerializedProperty spawners = serializedZone.FindProperty("waveSpawners");
        int index = spawners.arraySize;
        spawners.arraySize++;
        spawners.GetArrayElementAtIndex(index).objectReferenceValue = spawner;
        serializedZone.ApplyModifiedPropertiesWithoutUndo();

        Selection.activeGameObject = spawnerObject;
        EditorSceneManager.MarkSceneDirty(zone.gameObject.scene);
    }

    private static ArenaBoundaryGate CreateGate(string name, Transform parent, Vector2 position, Vector2 size)
    {
        GameObject gateObject = new(name);
        gateObject.transform.SetParent(parent);
        gateObject.transform.localPosition = position;
        BoxCollider2D collider = gateObject.AddComponent<BoxCollider2D>();
        collider.size = size;
        collider.isTrigger = false;
        return gateObject.AddComponent<ArenaBoundaryGate>();
    }
}
