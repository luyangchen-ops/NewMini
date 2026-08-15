using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class OccluderFadeSetupWindow : EditorWindow
{
    private const string WindowTitle = "Occluder Fade Setup";
    private const string TriggerName = "Trigger_Occlusion";
    private const string AnchorName = "Anchor_Sort";
    private const string BlockingColliderName = "Collider_Blocking";

    [SerializeField, Range(0.1f, 1f)] private float triggerWidthRatio = 0.55f;
    [SerializeField, Range(0f, 0.5f)] private float groundLineRatio = 0.12f;
    [SerializeField, Range(0.1f, 1f)] private float blockingWidthRatio = 0.8f;
    [SerializeField, Range(0.02f, 0.5f)] private float blockingHeightRatio = 0.15f;
    [SerializeField, Min(0f)] private float behindThreshold = 0.05f;
    [SerializeField] private float targetGroundYOffset = -0.72f;
    [SerializeField, Range(0f, 1f)] private float fadedAlpha = 0.35f;
    [SerializeField, Min(0.01f)] private float fadeSpeed = 6f;
    [SerializeField] private bool overwriteExistingNumericSettings;

    [MenuItem("Tools/NewMini/Occluder Fade Setup")]
    private static void OpenWindow()
    {
        GetWindow<OccluderFadeSetupWindow>(WindowTitle).minSize = new Vector2(390f, 390f);
    }

    [MenuItem("Assets/NewMini/Create or Setup Occluder Prefab", false, 2000)]
    private static void SetupFromAssetsMenu()
    {
        OccluderFadeSetupWindow window = GetWindow<OccluderFadeSetupWindow>(WindowTitle);
        window.SetupSelectedPrefabs();
    }

    [MenuItem("Assets/NewMini/Create or Setup Occluder Prefab", true)]
    private static bool ValidateAssetsMenu()
    {
        return GetSelectedInputPaths().Count > 0;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Top-down Occluder Fade", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Select PNG/Sprite assets, Prefab assets, or scene Prefab instances. "
            + "Images are converted into Prefabs first. The tool then adds "
            + "TopDownOccluderFade, Trigger_Occlusion, Anchor_Sort, and an editable "
            + "blocking BoxCollider2D.",
            MessageType.Info);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Generated Trigger", EditorStyles.boldLabel);
        triggerWidthRatio = EditorGUILayout.Slider(
            new GUIContent("Width Ratio", "Fraction of the combined SpriteRenderer width used by the trigger."),
            triggerWidthRatio,
            0.1f,
            1f);
        groundLineRatio = EditorGUILayout.Slider(
            new GUIContent("Ground Line", "Normalized height from the bottom of the sprite bounds."),
            groundLineRatio,
            0f,
            0.5f);
        blockingWidthRatio = EditorGUILayout.Slider(
            new GUIContent("Blocking Width", "Initial physical collider width relative to sprite bounds."),
            blockingWidthRatio,
            0.1f,
            1f);
        blockingHeightRatio = EditorGUILayout.Slider(
            new GUIContent("Blocking Height", "Initial physical collider height relative to sprite bounds."),
            blockingHeightRatio,
            0.02f,
            0.5f);
        behindThreshold = Mathf.Max(
            0f,
            EditorGUILayout.FloatField("Behind Threshold", behindThreshold));

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Fade Defaults", EditorStyles.boldLabel);
        targetGroundYOffset = EditorGUILayout.FloatField("Player Ground Y Offset", targetGroundYOffset);
        fadedAlpha = EditorGUILayout.Slider("Faded Alpha", fadedAlpha, 0f, 1f);
        fadeSpeed = Mathf.Max(
            0.01f,
            EditorGUILayout.FloatField("Fade Speed", fadeSpeed));
        overwriteExistingNumericSettings = EditorGUILayout.ToggleLeft(
            "Overwrite numeric settings on existing fade components",
            overwriteExistingNumericSettings);

        EditorGUILayout.Space(8f);
        List<string> paths = GetSelectedInputPaths();
        EditorGUILayout.LabelField($"Selected Assets: {paths.Count}", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(paths.Count == 0))
        {
            if (GUILayout.Button("Setup Selected Prefab(s)", GUILayout.Height(34f)))
            {
                SetupSelectedPrefabs();
            }
        }

        if (paths.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "Select a PNG/Sprite, a Prefab in the Project window, or a Prefab instance in the scene.",
                MessageType.Warning);
        }
        else
        {
            foreach (string path in paths)
            {
                EditorGUILayout.LabelField(Path.GetFileNameWithoutExtension(path), EditorStyles.miniLabel);
            }
        }
    }

    private void SetupSelectedPrefabs()
    {
        List<string> paths = GetSelectedInputPaths();
        if (paths.Count == 0)
        {
            ShowNotification(new GUIContent("No supported asset selected"));
            return;
        }

        int successCount = 0;
        var errors = new List<string>();

        foreach (string path in paths)
        {
            try
            {
                string prefabPath = IsPrefabPath(path)
                    ? path
                    : CreateOrUpdatePrefabFromImage(path);
                SetupPrefab(prefabPath);
                successCount++;
            }
            catch (Exception exception)
            {
                errors.Add($"{Path.GetFileName(path)}: {exception.Message}");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string message = $"Created/configured {successCount} Prefab(s).";
        if (errors.Count > 0)
        {
            message += "\n\nSkipped:\n" + string.Join("\n", errors);
        }

        EditorUtility.DisplayDialog(WindowTitle, message, "OK");
    }

    private void SetupPrefab(string prefabPath)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers.Length == 0)
            {
                throw new InvalidOperationException("No SpriteRenderer found.");
            }

            if (!TryCalculateLocalBounds(root.transform, renderers, out Bounds localBounds))
            {
                throw new InvalidOperationException("Unable to calculate sprite bounds.");
            }

            TopDownOccluderFade fade = root.GetComponent<TopDownOccluderFade>();
            bool componentWasAdded = fade == null;
            if (componentWasAdded)
            {
                fade = root.AddComponent<TopDownOccluderFade>();
            }

            Transform anchor = GetOrCreateChild(root.transform, AnchorName);
            float groundY = Mathf.Lerp(localBounds.min.y, localBounds.max.y, groundLineRatio);
            anchor.localPosition = new Vector3(localBounds.center.x, groundY, 0f);

            Transform triggerTransform = GetOrCreateChild(root.transform, TriggerName);
            BoxCollider2D trigger = triggerTransform.GetComponent<BoxCollider2D>();
            if (trigger == null)
            {
                trigger = triggerTransform.gameObject.AddComponent<BoxCollider2D>();
            }

            float triggerBottom = groundY + behindThreshold;
            float triggerTop = Mathf.Max(triggerBottom + 0.05f, localBounds.max.y);
            float triggerWidth = Mathf.Max(0.05f, localBounds.size.x * triggerWidthRatio);
            float triggerHeight = Mathf.Max(0.05f, triggerTop - triggerBottom);

            triggerTransform.localPosition = new Vector3(
                localBounds.center.x,
                (triggerBottom + triggerTop) * 0.5f,
                0f);
            triggerTransform.localRotation = Quaternion.identity;
            triggerTransform.localScale = Vector3.one;
            trigger.isTrigger = true;
            trigger.offset = Vector2.zero;
            trigger.size = new Vector2(triggerWidth, triggerHeight);

            Transform blockingTransform = GetOrCreateChild(root.transform, BlockingColliderName);
            BoxCollider2D blockingCollider = blockingTransform.GetComponent<BoxCollider2D>();
            if (blockingCollider == null)
            {
                blockingCollider = blockingTransform.gameObject.AddComponent<BoxCollider2D>();
            }

            float blockingWidth = Mathf.Max(0.05f, localBounds.size.x * blockingWidthRatio);
            float blockingHeight = Mathf.Max(0.05f, localBounds.size.y * blockingHeightRatio);
            float blockingCenterY = localBounds.min.y + blockingHeight * 0.5f;
            blockingTransform.localPosition = new Vector3(
                localBounds.center.x,
                blockingCenterY,
                0f);
            blockingTransform.localRotation = Quaternion.identity;
            blockingTransform.localScale = Vector3.one;
            blockingCollider.isTrigger = false;
            blockingCollider.offset = Vector2.zero;
            blockingCollider.size = new Vector2(blockingWidth, blockingHeight);

            SerializedObject serializedFade = new SerializedObject(fade);
            serializedFade.FindProperty("sortAnchor").objectReferenceValue = anchor;
            serializedFade.FindProperty("occlusionZone").objectReferenceValue = trigger;
            AssignRendererArray(serializedFade.FindProperty("occludingRenderers"), renderers);

            if (componentWasAdded || overwriteExistingNumericSettings)
            {
                serializedFade.FindProperty("targetGroundYOffset").floatValue = targetGroundYOffset;
                serializedFade.FindProperty("behindThreshold").floatValue = behindThreshold;
                serializedFade.FindProperty("fadedAlpha").floatValue = fadedAlpha;
                serializedFade.FindProperty("fadeSpeed").floatValue = fadeSpeed;
                serializedFade.FindProperty("horizontalPadding").floatValue = 0f;
                serializedFade.FindProperty("verticalPadding").floatValue = 0f;
            }

            serializedFade.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static string CreateOrUpdatePrefabFromImage(string imagePath)
    {
        EnsureSpriteImportSettings(imagePath);
        Sprite sprite = LoadPrimarySprite(imagePath);
        if (sprite == null)
        {
            throw new InvalidOperationException("The image did not import as a Sprite.");
        }

        string prefabPath = GetGeneratedPrefabPath(imagePath);
        EnsureAssetFolder(Path.GetDirectoryName(prefabPath)?.Replace('\\', '/'));

        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
        {
            GameObject existingRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                SpriteRenderer existingRenderer = existingRoot.GetComponent<SpriteRenderer>();
                if (existingRenderer == null)
                {
                    existingRenderer = existingRoot.AddComponent<SpriteRenderer>();
                }

                existingRenderer.sprite = sprite;
                EditorUtility.SetDirty(existingRoot);
                PrefabUtility.SaveAsPrefabAsset(existingRoot, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(existingRoot);
            }

            return prefabPath;
        }

        string objectName = Path.GetFileNameWithoutExtension(imagePath);
        var prefabRoot = new GameObject(objectName);
        try
        {
            SpriteRenderer renderer = prefabRoot.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        }
        finally
        {
            DestroyImmediate(prefabRoot);
        }

        return prefabPath;
    }

    private static void EnsureSpriteImportSettings(string imagePath)
    {
        if (AssetImporter.GetAtPath(imagePath) is not TextureImporter importer)
        {
            return;
        }

        bool changed = false;
        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            changed = true;
        }

        if (!importer.alphaIsTransparency)
        {
            importer.alphaIsTransparency = true;
            changed = true;
        }

        if (importer.mipmapEnabled)
        {
            importer.mipmapEnabled = false;
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }
    }

    private static Sprite LoadPrimarySprite(string imagePath)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(imagePath);
        if (sprite != null)
        {
            return sprite;
        }

        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(imagePath))
        {
            if (asset is Sprite childSprite)
            {
                return childSprite;
            }
        }

        return null;
    }

    private static string GetGeneratedPrefabPath(string imagePath)
    {
        string normalizedPath = imagePath.Replace('\\', '/');
        const string artSegment = "/Art/";
        int artIndex = normalizedPath.IndexOf(artSegment, StringComparison.OrdinalIgnoreCase);

        string directory;
        if (artIndex >= 0)
        {
            string levelRoot = normalizedPath.Substring(0, artIndex);
            string relativePath = normalizedPath.Substring(artIndex + artSegment.Length);
            string relativeDirectory = Path.GetDirectoryName(relativePath)?.Replace('\\', '/');
            directory = string.IsNullOrEmpty(relativeDirectory)
                ? levelRoot + "/Prefabs"
                : levelRoot + "/Prefabs/" + relativeDirectory;
        }
        else
        {
            string sourceDirectory = Path.GetDirectoryName(normalizedPath)?.Replace('\\', '/');
            directory = sourceDirectory + "/Prefabs";
        }

        return directory + "/" + Path.GetFileNameWithoutExtension(normalizedPath) + ".prefab";
    }

    private static void EnsureAssetFolder(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath) || AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
        string folderName = Path.GetFileName(folderPath);
        EnsureAssetFolder(parent);
        AssetDatabase.CreateFolder(parent, folderName);
    }

    private static Transform GetOrCreateChild(Transform root, string childName)
    {
        Transform child = root.Find(childName);
        if (child != null)
        {
            return child;
        }

        var childObject = new GameObject(childName);
        childObject.transform.SetParent(root, false);
        return childObject.transform;
    }

    private static bool TryCalculateLocalBounds(
        Transform root,
        IReadOnlyList<SpriteRenderer> renderers,
        out Bounds bounds)
    {
        bounds = default;
        bool initialized = false;

        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == null || renderer.sprite == null)
            {
                continue;
            }

            Bounds worldBounds = renderer.bounds;
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
            Vector3[] corners =
            {
                new Vector3(min.x, min.y, 0f),
                new Vector3(min.x, max.y, 0f),
                new Vector3(max.x, min.y, 0f),
                new Vector3(max.x, max.y, 0f)
            };

            foreach (Vector3 corner in corners)
            {
                Vector3 localPoint = root.InverseTransformPoint(corner);
                if (!initialized)
                {
                    bounds = new Bounds(localPoint, Vector3.zero);
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(localPoint);
                }
            }
        }

        return initialized;
    }

    private static void AssignRendererArray(
        SerializedProperty arrayProperty,
        IReadOnlyList<SpriteRenderer> renderers)
    {
        arrayProperty.arraySize = renderers.Count;
        for (int index = 0; index < renderers.Count; index++)
        {
            arrayProperty.GetArrayElementAtIndex(index).objectReferenceValue = renderers[index];
        }
    }

    private static List<string> GetSelectedInputPaths()
    {
        var paths = new HashSet<string>();
        foreach (UnityEngine.Object selectedObject in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(selectedObject);
            if (!string.IsNullOrEmpty(path) && (IsPrefabPath(path) || IsSupportedImagePath(path)))
            {
                paths.Add(path);
                continue;
            }

            if (selectedObject is not GameObject selectedGameObject)
            {
                continue;
            }

            string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(selectedGameObject);
            if (!string.IsNullOrEmpty(prefabPath))
            {
                paths.Add(prefabPath);
            }
        }

        return new List<string>(paths);
    }

    private static bool IsPrefabPath(string path)
    {
        return path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSupportedImagePath(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".psd", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".tga", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase);
    }
}
