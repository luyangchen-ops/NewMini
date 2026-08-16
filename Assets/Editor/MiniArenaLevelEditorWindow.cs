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
    [SerializeField] private ArenaWaveSpawner targetWaveSpawner;
    [SerializeField] private int selectedWaveIndex;
    [SerializeField] private EnemySpawner.EnemyType placementEnemyType;
    [SerializeField] private GameObject swordsmanPrefab;
    [SerializeField] private GameObject archerPrefab;
    [SerializeField] private GameObject shieldBearerPrefab;
    [SerializeField] private GameObject spearmanPrefab;
    [SerializeField] private bool showAllCombatZones = true;
    [SerializeField] private bool showCombatZoneLabels = true;
    [SerializeField] private bool showAllEnemySpawnPoints = true;
    [SerializeField] private bool showSpawnPointLabels = true;
    private bool isPlacingEnemy;

    private static readonly string[] EnemyTypeLabels = { "刀兵", "弓兵", "盾兵", "枪兵" };

    [MenuItem("Tools/NewMini/箱庭关卡编辑器")]
    public static void Open() => GetWindow<MiniArenaLevelEditorWindow>("箱庭关卡编辑器");

    private void OnEnable()
    {
        SceneView.duringSceneGui += DrawSceneAreaSelector;
        Undo.undoRedoPerformed += RepaintSceneVisualization;
        EditorApplication.hierarchyChanged += RepaintSceneVisualization;
        showAllCombatZones = true;
        showAllEnemySpawnPoints = true;
        TryAssignKnownEnemyPrefabs();
        TryUseSpawnerFromSelection();
        SceneView.RepaintAll();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DrawSceneAreaSelector;
        Undo.undoRedoPerformed -= RepaintSceneVisualization;
        EditorApplication.hierarchyChanged -= RepaintSceneVisualization;
        isPlacingEnemy = false;
        SceneView.RepaintAll();
    }

    private void OnSelectionChange()
    {
        TryUseSpawnerFromSelection();
        Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("箱庭关卡编辑器", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("点击“开始框选”后，在 Scene 视图左键拖拽出战区。区域会直接贴合地图坐标，所有节点仍保存在当前场景中。按 Esc 可取消。", MessageType.Info);
        DrawCombatZoneDisplayControls();
        arenaName = EditorGUILayout.TextField("区域名称", arenaName);
        gateThickness = EditorGUILayout.Slider("边界厚度", gateThickness, .1f, 1f);

        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(arenaName)))
        {
            if (GUILayout.Button(isDrawingArea ? "框选中：回到 Scene 视图拖拽" : "开始框选战区", GUILayout.Height(32)))
            {
                isPlacingEnemy = false;
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
            {
                targetWaveSpawner = CreateWaveSpawner(selectedZone);
                selectedWaveIndex = 0;
            }
        }

        DrawWavePlacementTools();
    }

    private void DrawWavePlacementTools()
    {
        EditorGUILayout.Space(12f);
        EditorGUILayout.LabelField("Scene 点选布怪", EditorStyles.boldLabel);
        DrawSpawnPointDisplayControls();

        EditorGUI.BeginChangeCheck();
        ArenaWaveSpawner assignedSpawner = EditorGUILayout.ObjectField(
            "Wave Spawner",
            targetWaveSpawner,
            typeof(ArenaWaveSpawner),
            true) as ArenaWaveSpawner;
        if (EditorGUI.EndChangeCheck())
        {
            isPlacingEnemy = false;
            targetWaveSpawner = assignedSpawner;
            selectedWaveIndex = 0;
            SceneView.RepaintAll();
        }

        if (targetWaveSpawner == null)
        {
            EditorGUILayout.HelpBox("在 Hierarchy 中选择 Group_WaveSpawner，或将它拖到上方字段。", MessageType.Info);
            return;
        }

        SerializedObject serializedSpawner = new(targetWaveSpawner);
        serializedSpawner.Update();
        SerializedProperty waves = serializedSpawner.FindProperty("waves");
        if (waves == null)
        {
            EditorGUILayout.HelpBox("当前 ArenaWaveSpawner 找不到 Waves 数据。", MessageType.Error);
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (waves.arraySize > 0)
            {
                selectedWaveIndex = Mathf.Clamp(selectedWaveIndex, 0, waves.arraySize - 1);
                selectedWaveIndex = EditorGUILayout.Popup("目标 Wave", selectedWaveIndex, GetWaveNames(waves));
            }
            else
            {
                EditorGUILayout.LabelField("目标 Wave", "尚未创建");
            }

            if (GUILayout.Button("+ 新建 Wave", GUILayout.Width(100f)))
            {
                selectedWaveIndex = AddWave(targetWaveSpawner);
                serializedSpawner.Update();
                waves = serializedSpawner.FindProperty("waves");
            }
        }

        placementEnemyType = (EnemySpawner.EnemyType)GUILayout.Toolbar(
            (int)placementEnemyType,
            EnemyTypeLabels,
            GUILayout.Height(25f));

        GameObject selectedPrefab = GetPrefab(placementEnemyType);
        EditorGUI.BeginChangeCheck();
        selectedPrefab = EditorGUILayout.ObjectField(
            $"{GetEnemyTypeName(placementEnemyType)} Prefab",
            selectedPrefab,
            typeof(GameObject),
            false) as GameObject;
        if (EditorGUI.EndChangeCheck())
        {
            SetPrefab(placementEnemyType, selectedPrefab);
            ApplyPrefabToAllWaveEntries(placementEnemyType, selectedPrefab);
        }

        if (selectedPrefab == null)
        {
            EditorGUILayout.HelpBox(
                $"{GetEnemyTypeName(placementEnemyType)}尚未指定 Prefab。仍可先布置并写入类型与点位，Prefab 到位后再补。",
                MessageType.Warning);
        }
        else if (selectedPrefab.GetComponentInChildren<EnemyAgent>(true) == null)
        {
            EditorGUILayout.HelpBox("当前 Prefab 没有 EnemyAgent，运行时不会被战斗系统正确识别。", MessageType.Warning);
        }

        if (waves.arraySize > 0) DrawSelectedWaveSummary(waves.GetArrayElementAtIndex(selectedWaveIndex));

        using (new EditorGUI.DisabledScope(waves.arraySize == 0))
        {
            string buttonLabel = isPlacingEnemy
                ? "结束点选布怪（Esc）"
                : $"开始放置：{GetEnemyTypeName(placementEnemyType)}";
            if (GUILayout.Button(buttonLabel, GUILayout.Height(32f)))
            {
                isDrawingArea = false;
                hasArea = false;
                isPlacingEnemy = !isPlacingEnemy;
                SceneView.lastActiveSceneView?.Focus();
                SceneView.RepaintAll();
            }
        }

        EditorGUILayout.HelpBox(
            "开始后在 Scene 中左键连续添加出生点；切换兵种后可继续放置。按 Esc 结束。每个点会自动写入当前 Wave。",
            MessageType.Info);
    }

    private void DrawEnemyPlacement(SceneView sceneView)
    {
        if (targetWaveSpawner == null || !TryGetSelectedWave(out SerializedProperty selectedWave))
        {
            isPlacingEnemy = false;
            Repaint();
            return;
        }

        Event current = Event.current;
        if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
        {
            isPlacingEnemy = false;
            current.Use();
            Repaint();
            sceneView.Repaint();
            return;
        }

        int controlId = GUIUtility.GetControlID("NewMiniEnemyPlacement".GetHashCode(), FocusType.Passive);
        if (current.type == EventType.Layout) HandleUtility.AddDefaultControl(controlId);

        if (!showAllEnemySpawnPoints)
            DrawWaveSpawnPoints(selectedWave, selectedWaveIndex);
        Vector3 worldPosition = GetMouseWorldPosition(current.mousePosition, targetWaveSpawner.transform.position.z);
        Color typeColor = GetEnemyTypeColor(placementEnemyType);
        float markerSize = HandleUtility.GetHandleSize(worldPosition) * .12f;
        Handles.color = new Color(typeColor.r, typeColor.g, typeColor.b, .35f);
        Handles.DrawSolidDisc(worldPosition, Vector3.forward, markerSize);
        Handles.color = typeColor;
        Handles.DrawWireDisc(worldPosition, Vector3.forward, markerSize);
        Handles.Label(
            worldPosition + Vector3.up * markerSize * 1.3f,
            $"{GetSelectedWaveName(selectedWave)} · {GetEnemyTypeName(placementEnemyType)}\n左键放置 / Esc 结束");

        if (current.type == EventType.MouseMove) sceneView.Repaint();
        if (current.type != EventType.MouseDown || current.button != 0 || current.alt) return;

        AddEnemySpawnPoint(worldPosition);
        current.Use();
        Repaint();
        sceneView.Repaint();
    }

    private bool TryGetSelectedWave(out SerializedProperty selectedWave)
    {
        selectedWave = null;
        if (targetWaveSpawner == null) return false;

        SerializedObject serializedSpawner = new(targetWaveSpawner);
        serializedSpawner.Update();
        SerializedProperty waves = serializedSpawner.FindProperty("waves");
        if (waves == null || selectedWaveIndex < 0 || selectedWaveIndex >= waves.arraySize) return false;
        selectedWave = waves.GetArrayElementAtIndex(selectedWaveIndex);
        return true;
    }

    private void AddEnemySpawnPoint(Vector3 worldPosition)
    {
        if (targetWaveSpawner == null) return;

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Add Wave Enemy Spawn Point");
        Undo.RecordObject(targetWaveSpawner, "Add Wave Enemy Spawn Point");

        SerializedObject serializedSpawner = new(targetWaveSpawner);
        serializedSpawner.Update();
        SerializedProperty waves = serializedSpawner.FindProperty("waves");
        if (waves == null || selectedWaveIndex < 0 || selectedWaveIndex >= waves.arraySize) return;

        SerializedProperty wave = waves.GetArrayElementAtIndex(selectedWaveIndex);
        SerializedProperty spawns = wave.FindPropertyRelative("spawns");
        int entryIndex = FindSpawnEntryIndex(spawns, placementEnemyType);
        if (entryIndex < 0)
        {
            entryIndex = spawns.arraySize;
            spawns.arraySize++;
            SerializedProperty newEntry = spawns.GetArrayElementAtIndex(entryIndex);
            newEntry.FindPropertyRelative("enemyType").intValue = (int)placementEnemyType;
            newEntry.FindPropertyRelative("enemyPrefab").objectReferenceValue = null;
            newEntry.FindPropertyRelative("spawnPoints").arraySize = 0;
            newEntry.FindPropertyRelative("count").intValue = 1;
        }

        SerializedProperty entry = spawns.GetArrayElementAtIndex(entryIndex);
        entry.FindPropertyRelative("enemyType").intValue = (int)placementEnemyType;
        GameObject configuredPrefab = GetPrefab(placementEnemyType);
        if (configuredPrefab != null)
            entry.FindPropertyRelative("enemyPrefab").objectReferenceValue = configuredPrefab;

        SerializedProperty spawnPoints = entry.FindPropertyRelative("spawnPoints");
        Transform pointsRoot = GetOrCreateChild(targetWaveSpawner.transform, "Group_SpawnPoints");
        Transform waveGroup = GetOrCreateChild(pointsRoot, $"Group_Wave_{selectedWaveIndex + 1:00}");
        Transform typeGroup = GetOrCreateChild(waveGroup, $"Group_{GetEnemyTypeName(placementEnemyType)}");
        int pointNumber = spawnPoints.arraySize + 1;
        GameObject pointObject = new($"Spawn_{GetEnemyTypeName(placementEnemyType)}_{pointNumber:00}");
        Undo.RegisterCreatedObjectUndo(pointObject, "Create Enemy Spawn Point");
        pointObject.transform.SetParent(typeGroup, false);
        pointObject.transform.position = worldPosition;

        spawnPoints.arraySize++;
        spawnPoints.GetArrayElementAtIndex(spawnPoints.arraySize - 1).objectReferenceValue = pointObject.transform;
        entry.FindPropertyRelative("count").intValue = spawnPoints.arraySize;
        serializedSpawner.ApplyModifiedProperties();

        EditorUtility.SetDirty(targetWaveSpawner);
        EditorSceneManager.MarkSceneDirty(targetWaveSpawner.gameObject.scene);
        Undo.CollapseUndoOperations(undoGroup);
    }

    private void DrawCombatZoneDisplayControls()
    {
        EditorGUI.BeginChangeCheck();
        showAllCombatZones = EditorGUILayout.ToggleLeft(
            "打开工具时在 Scene 显示全部交战区域",
            showAllCombatZones);
        using (new EditorGUI.DisabledScope(!showAllCombatZones))
        {
            showCombatZoneLabels = EditorGUILayout.ToggleLeft("显示战区名称与尺寸", showCombatZoneLabels);
        }

        if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();
    }

    private void DrawAllCombatZones()
    {
        if (!showAllCombatZones || Event.current.type != EventType.Repaint) return;

        ArenaCombatZone[] zones = Object.FindObjectsByType<ArenaCombatZone>(FindObjectsInactive.Include);
        UnityEngine.Rendering.CompareFunction previousZTest = Handles.zTest;
        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

        try
        {
            foreach (ArenaCombatZone zone in zones)
            {
                if (zone == null
                    || EditorUtility.IsPersistent(zone)
                    || !zone.gameObject.scene.IsValid()
                    || !zone.gameObject.scene.isLoaded)
                    continue;

                Collider2D area = zone.ZoneCollider != null ? zone.ZoneCollider : zone.GetComponent<Collider2D>();
                if (area == null || !area.enabled) continue;

                Color outline = zone.IsActive
                    ? new Color(1f, .42f, .12f, 1f)
                    : new Color(.1f, .8f, 1f, 1f);
                Color fill = new(outline.r, outline.g, outline.b, zone.IsActive ? .16f : .09f);
                DrawCombatZoneCollider(area, fill, outline);

                if (!showCombatZoneLabels) continue;
                Bounds bounds = area.bounds;
                GUIStyle labelStyle = new(EditorStyles.boldLabel);
                labelStyle.normal.textColor = outline;
                Handles.Label(
                    new Vector3(bounds.center.x, bounds.max.y, zone.transform.position.z),
                    $"{GetCombatZoneDisplayName(zone)}\n{bounds.size.x:F1} × {bounds.size.y:F1}",
                    labelStyle);
            }
        }
        finally
        {
            Handles.zTest = previousZTest;
        }
    }

    private static void DrawCombatZoneCollider(Collider2D area, Color fill, Color outline)
    {
        if (area is BoxCollider2D box)
        {
            Vector2 halfSize = box.size * .5f;
            Vector2 offset = box.offset;
            Vector3[] corners =
            {
                box.transform.TransformPoint(offset + new Vector2(-halfSize.x, -halfSize.y)),
                box.transform.TransformPoint(offset + new Vector2(-halfSize.x, halfSize.y)),
                box.transform.TransformPoint(offset + new Vector2(halfSize.x, halfSize.y)),
                box.transform.TransformPoint(offset + new Vector2(halfSize.x, -halfSize.y))
            };
            Handles.DrawSolidRectangleWithOutline(corners, fill, outline);
            return;
        }

        if (area is CircleCollider2D circle)
        {
            Vector3 center = circle.transform.TransformPoint(circle.offset);
            Vector3 scale = circle.transform.lossyScale;
            float radius = circle.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            Handles.color = fill;
            Handles.DrawSolidDisc(center, Vector3.forward, radius);
            Handles.color = outline;
            Handles.DrawWireDisc(center, Vector3.forward, radius);
            return;
        }

        Bounds bounds = area.bounds;
        Vector3[] boundsCorners =
        {
            new(bounds.min.x, bounds.min.y, area.transform.position.z),
            new(bounds.min.x, bounds.max.y, area.transform.position.z),
            new(bounds.max.x, bounds.max.y, area.transform.position.z),
            new(bounds.max.x, bounds.min.y, area.transform.position.z)
        };
        Handles.DrawSolidRectangleWithOutline(boundsCorners, fill, outline);
    }

    private static string GetCombatZoneDisplayName(ArenaCombatZone zone)
    {
        Transform parent = zone.transform.parent;
        return parent != null && parent.name.StartsWith("Root_") ? parent.name : zone.name;
    }

    private void DrawSpawnPointDisplayControls()
    {
        EditorGUI.BeginChangeCheck();
        showAllEnemySpawnPoints = EditorGUILayout.ToggleLeft(
            "打开工具时在 Scene 显示全部已布置点位",
            showAllEnemySpawnPoints);
        using (new EditorGUI.DisabledScope(!showAllEnemySpawnPoints))
        {
            showSpawnPointLabels = EditorGUILayout.ToggleLeft("显示 Wave / 兵种 / 序号标签", showSpawnPointLabels);
        }

        if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();

        EditorGUILayout.LabelField(
            "颜色：刀兵=红　弓兵=绿　盾兵=蓝　枪兵=紫",
            EditorStyles.miniLabel);
    }

    private void DrawAllSceneSpawnPoints()
    {
        if (!showAllEnemySpawnPoints || Event.current.type != EventType.Repaint) return;

        ArenaWaveSpawner[] spawners = Object.FindObjectsByType<ArenaWaveSpawner>(FindObjectsInactive.Include);
        UnityEngine.Rendering.CompareFunction previousZTest = Handles.zTest;
        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

        try
        {
            foreach (ArenaWaveSpawner spawner in spawners)
            {
                if (spawner == null
                    || EditorUtility.IsPersistent(spawner)
                    || !spawner.gameObject.scene.IsValid()
                    || !spawner.gameObject.scene.isLoaded)
                    continue;

                SerializedObject serializedSpawner = new(spawner);
                serializedSpawner.UpdateIfRequiredOrScript();
                SerializedProperty waves = serializedSpawner.FindProperty("waves");
                if (waves == null) continue;

                for (int waveIndex = 0; waveIndex < waves.arraySize; waveIndex++)
                    DrawWaveSpawnPoints(waves.GetArrayElementAtIndex(waveIndex), waveIndex);
            }
        }
        finally
        {
            Handles.zTest = previousZTest;
        }
    }

    private void DrawWaveSpawnPoints(SerializedProperty wave, int waveIndex)
    {
        SerializedProperty spawns = wave.FindPropertyRelative("spawns");
        if (spawns == null) return;

        for (int entryIndex = 0; entryIndex < spawns.arraySize; entryIndex++)
        {
            SerializedProperty entry = spawns.GetArrayElementAtIndex(entryIndex);
            SerializedProperty typeProperty = entry.FindPropertyRelative("enemyType");
            EnemySpawner.EnemyType type = typeProperty != null
                ? (EnemySpawner.EnemyType)typeProperty.intValue
                : EnemySpawner.EnemyType.Swordsman;
            SerializedProperty spawnPoints = entry.FindPropertyRelative("spawnPoints");
            if (spawnPoints == null) continue;

            Color typeColor = GetEnemyTypeColor(type);
            GUIStyle labelStyle = new(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.LowerCenter
            };
            labelStyle.normal.textColor = typeColor;

            for (int pointIndex = 0; pointIndex < spawnPoints.arraySize; pointIndex++)
            {
                Transform point = spawnPoints.GetArrayElementAtIndex(pointIndex).objectReferenceValue as Transform;
                if (point == null) continue;

                float size = HandleUtility.GetHandleSize(point.position) * .09f;
                Handles.color = new Color(typeColor.r, typeColor.g, typeColor.b, .28f);
                Handles.DrawSolidDisc(point.position, Vector3.forward, size);
                Handles.color = typeColor;
                Handles.DrawWireDisc(point.position, Vector3.forward, size);
                Handles.DrawLine(
                    point.position + Vector3.left * size * .65f,
                    point.position + Vector3.right * size * .65f);
                Handles.DrawLine(
                    point.position + Vector3.down * size * .65f,
                    point.position + Vector3.up * size * .65f);

                if (showSpawnPointLabels)
                {
                    Handles.Label(
                        point.position + Vector3.up * size * 1.25f,
                        $"W{waveIndex + 1:00} · {GetEnemyTypeName(type)} {pointIndex + 1:00}",
                        labelStyle);
                }
            }
        }
    }

    private void RepaintSceneVisualization()
    {
        Repaint();
        SceneView.RepaintAll();
    }

    private static int FindSpawnEntryIndex(SerializedProperty spawns, EnemySpawner.EnemyType type)
    {
        for (int i = 0; i < spawns.arraySize; i++)
        {
            SerializedProperty typeProperty = spawns.GetArrayElementAtIndex(i).FindPropertyRelative("enemyType");
            if (typeProperty != null && typeProperty.intValue == (int)type) return i;
        }
        return -1;
    }

    private static string[] GetWaveNames(SerializedProperty waves)
    {
        string[] names = new string[waves.arraySize];
        for (int i = 0; i < waves.arraySize; i++)
        {
            SerializedProperty wave = waves.GetArrayElementAtIndex(i);
            string waveName = wave.FindPropertyRelative("waveName").stringValue;
            names[i] = string.IsNullOrWhiteSpace(waveName) ? $"Wave {i + 1}" : $"{i + 1}: {waveName}";
        }
        return names;
    }

    private static string GetSelectedWaveName(SerializedProperty wave)
    {
        string waveName = wave.FindPropertyRelative("waveName").stringValue;
        return string.IsNullOrWhiteSpace(waveName) ? "Wave" : waveName;
    }

    private static int AddWave(ArenaWaveSpawner spawner)
    {
        Undo.RecordObject(spawner, "Add Arena Wave");
        SerializedObject serializedSpawner = new(spawner);
        serializedSpawner.Update();
        SerializedProperty waves = serializedSpawner.FindProperty("waves");
        int newIndex = waves.arraySize;
        waves.arraySize++;
        SerializedProperty wave = waves.GetArrayElementAtIndex(newIndex);
        wave.FindPropertyRelative("waveName").stringValue = $"Wave {newIndex + 1}";
        wave.FindPropertyRelative("delayBeforeWave").floatValue = 0f;
        wave.FindPropertyRelative("spawns").arraySize = 0;
        serializedSpawner.ApplyModifiedProperties();
        EditorUtility.SetDirty(spawner);
        EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);
        return newIndex;
    }

    private void DrawSelectedWaveSummary(SerializedProperty wave)
    {
        int[] counts = new int[EnemyTypeLabels.Length];
        SerializedProperty spawns = wave.FindPropertyRelative("spawns");
        for (int i = 0; i < spawns.arraySize; i++)
        {
            SerializedProperty entry = spawns.GetArrayElementAtIndex(i);
            int typeIndex = entry.FindPropertyRelative("enemyType").intValue;
            SerializedProperty points = entry.FindPropertyRelative("spawnPoints");
            if (typeIndex >= 0 && typeIndex < counts.Length && points != null) counts[typeIndex] += points.arraySize;
        }
        EditorGUILayout.LabelField(
            "当前 Wave",
            $"刀 {counts[0]} / 弓 {counts[1]} / 盾 {counts[2]} / 枪 {counts[3]}");
    }

    private void TryUseSpawnerFromSelection()
    {
        GameObject selectedObject = Selection.activeGameObject;
        if (selectedObject == null) return;

        ArenaWaveSpawner selectedSpawner = selectedObject.GetComponent<ArenaWaveSpawner>()
            ?? selectedObject.GetComponentInParent<ArenaWaveSpawner>();
        if (selectedSpawner == null)
        {
            ArenaWaveSpawner[] childSpawners = selectedObject.GetComponentsInChildren<ArenaWaveSpawner>(true);
            if (childSpawners.Length == 1) selectedSpawner = childSpawners[0];
        }

        if (selectedSpawner == null || selectedSpawner == targetWaveSpawner) return;
        isPlacingEnemy = false;
        targetWaveSpawner = selectedSpawner;
        selectedWaveIndex = 0;
        SceneView.RepaintAll();
    }

    private void TryAssignKnownEnemyPrefabs()
    {
        swordsmanPrefab ??= FindEnemyPrefab("刀兵", "Swordsman");
        archerPrefab ??= FindEnemyPrefab("弓兵", "Archer");
        shieldBearerPrefab ??= FindEnemyPrefab("盾兵", "ShieldBearer");
        spearmanPrefab ??= FindEnemyPrefab("枪兵", "Spearman");
    }

    private static GameObject FindEnemyPrefab(params string[] searchNames)
    {
        foreach (string searchName in searchNames)
        {
            string[] guids = AssetDatabase.FindAssets($"{searchName} t:Prefab", new[] { "Assets" });
            foreach (string guid in guids)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab != null && prefab.GetComponentInChildren<EnemyAgent>(true) != null) return prefab;
            }
        }
        return null;
    }

    private GameObject GetPrefab(EnemySpawner.EnemyType type)
    {
        return type switch
        {
            EnemySpawner.EnemyType.Archer => archerPrefab,
            EnemySpawner.EnemyType.ShieldBearer => shieldBearerPrefab,
            EnemySpawner.EnemyType.Spearman => spearmanPrefab,
            _ => swordsmanPrefab
        };
    }

    private void SetPrefab(EnemySpawner.EnemyType type, GameObject prefab)
    {
        switch (type)
        {
            case EnemySpawner.EnemyType.Archer:
                archerPrefab = prefab;
                break;
            case EnemySpawner.EnemyType.ShieldBearer:
                shieldBearerPrefab = prefab;
                break;
            case EnemySpawner.EnemyType.Spearman:
                spearmanPrefab = prefab;
                break;
            default:
                swordsmanPrefab = prefab;
                break;
        }
    }

    private void ApplyPrefabToAllWaveEntries(EnemySpawner.EnemyType type, GameObject prefab)
    {
        if (targetWaveSpawner == null) return;

        SerializedObject serializedSpawner = new(targetWaveSpawner);
        serializedSpawner.Update();
        SerializedProperty waves = serializedSpawner.FindProperty("waves");
        if (waves == null) return;

        Undo.RecordObject(targetWaveSpawner, "Assign Wave Enemy Prefab");
        bool changed = false;
        for (int waveIndex = 0; waveIndex < waves.arraySize; waveIndex++)
        {
            SerializedProperty spawns = waves.GetArrayElementAtIndex(waveIndex).FindPropertyRelative("spawns");
            for (int entryIndex = 0; entryIndex < spawns.arraySize; entryIndex++)
            {
                SerializedProperty entry = spawns.GetArrayElementAtIndex(entryIndex);
                if (entry.FindPropertyRelative("enemyType").intValue != (int)type) continue;
                entry.FindPropertyRelative("enemyPrefab").objectReferenceValue = prefab;
                changed = true;
            }
        }

        if (!changed) return;
        serializedSpawner.ApplyModifiedProperties();
        EditorUtility.SetDirty(targetWaveSpawner);
        EditorSceneManager.MarkSceneDirty(targetWaveSpawner.gameObject.scene);
    }

    private static string GetEnemyTypeName(EnemySpawner.EnemyType type)
    {
        int index = Mathf.Clamp((int)type, 0, EnemyTypeLabels.Length - 1);
        return EnemyTypeLabels[index];
    }

    private static Color GetEnemyTypeColor(EnemySpawner.EnemyType type)
    {
        return type switch
        {
            EnemySpawner.EnemyType.Archer => new Color(.25f, .85f, .35f, 1f),
            EnemySpawner.EnemyType.ShieldBearer => new Color(.25f, .65f, 1f, 1f),
            EnemySpawner.EnemyType.Spearman => new Color(.75f, .4f, 1f, 1f),
            _ => new Color(1f, .35f, .25f, 1f)
        };
    }

    private static Transform GetOrCreateChild(Transform parent, string childName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName) return child;
        }

        GameObject childObject = new(childName);
        Undo.RegisterCreatedObjectUndo(childObject, $"Create {childName}");
        childObject.transform.SetParent(parent, false);
        return childObject.transform;
    }

    private void DrawSceneAreaSelector(SceneView sceneView)
    {
        DrawAllCombatZones();
        DrawAllSceneSpawnPoints();

        if (isPlacingEnemy)
        {
            DrawEnemyPlacement(sceneView);
            return;
        }

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

    private static Vector3 GetMouseWorldPosition(Vector2 guiPosition, float worldZ)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(guiPosition);
        Plane mapPlane = new(Vector3.forward, new Vector3(0f, 0f, worldZ));
        return mapPlane.Raycast(ray, out float distance) ? ray.GetPoint(distance) : new Vector3(0f, 0f, worldZ);
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
        zoneObject.transform.SetParent(root.transform, false);
        BoxCollider2D zoneCollider = zoneObject.AddComponent<BoxCollider2D>();
        zoneCollider.size = size;
        zoneCollider.isTrigger = true;
        ArenaCombatZone zone = zoneObject.AddComponent<ArenaCombatZone>();

        GameObject gatesGroup = new("Group_BoundaryGates");
        gatesGroup.transform.SetParent(root.transform, false);
        ArenaBoundaryGate[] gates =
        {
            CreateGate("Gate_North", gatesGroup.transform, new Vector2(0f, size.y * .5f), new Vector2(size.x + gateThickness * 2f, gateThickness)),
            CreateGate("Gate_South", gatesGroup.transform, new Vector2(0f, -size.y * .5f), new Vector2(size.x + gateThickness * 2f, gateThickness)),
            CreateGate("Gate_East", gatesGroup.transform, new Vector2(size.x * .5f, 0f), new Vector2(gateThickness, size.y)),
            CreateGate("Gate_West", gatesGroup.transform, new Vector2(-size.x * .5f, 0f), new Vector2(gateThickness, size.y))
        };

        GameObject respawnObject = new("Group_RespawnPoint");
        respawnObject.transform.SetParent(root.transform, false);
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

    private static ArenaWaveSpawner CreateWaveSpawner(ArenaCombatZone zone)
    {
        GameObject spawnerObject = new("Group_WaveSpawner");
        Undo.RegisterCreatedObjectUndo(spawnerObject, "Create Arena Wave Spawner");
        spawnerObject.transform.SetParent(zone.transform.parent != null ? zone.transform.parent : zone.transform, false);
        spawnerObject.transform.position = zone.transform.position;
        ArenaWaveSpawner spawner = spawnerObject.AddComponent<ArenaWaveSpawner>();

        GameObject pointsGroup = new("Group_SpawnPoints");
        pointsGroup.transform.SetParent(spawnerObject.transform, false);

        GameObject enemiesGroup = new("Group_SpawnedEnemies");
        enemiesGroup.transform.SetParent(spawnerObject.transform, false);

        SerializedObject serializedSpawner = new(spawner);
        serializedSpawner.FindProperty("spawnedEnemyParent").objectReferenceValue = enemiesGroup.transform;
        SerializedProperty waves = serializedSpawner.FindProperty("waves");
        waves.arraySize = 1;
        SerializedProperty firstWave = waves.GetArrayElementAtIndex(0);
        firstWave.FindPropertyRelative("waveName").stringValue = "Wave 1";
        SerializedProperty spawns = firstWave.FindPropertyRelative("spawns");
        spawns.arraySize = 0;
        serializedSpawner.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject serializedZone = new(zone);
        SerializedProperty spawners = serializedZone.FindProperty("waveSpawners");
        int index = spawners.arraySize;
        spawners.arraySize++;
        spawners.GetArrayElementAtIndex(index).objectReferenceValue = spawner;
        serializedZone.ApplyModifiedPropertiesWithoutUndo();

        Selection.activeGameObject = spawnerObject;
        EditorSceneManager.MarkSceneDirty(zone.gameObject.scene);
        return spawner;
    }

    private static ArenaBoundaryGate CreateGate(string name, Transform parent, Vector2 position, Vector2 size)
    {
        GameObject gateObject = new(name);
        gateObject.transform.SetParent(parent, false);
        gateObject.transform.localPosition = position;
        BoxCollider2D collider = gateObject.AddComponent<BoxCollider2D>();
        collider.size = size;
        collider.isTrigger = false;
        return gateObject.AddComponent<ArenaBoundaryGate>();
    }
}
