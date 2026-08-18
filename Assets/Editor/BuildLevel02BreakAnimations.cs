using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class BuildLevel02BreakAnimations
{
    private const string ScenePath = "Assets/Scenes/Level_02_InkSnowCourtyard.unity";
    private const string AnimationRoot = "Assets/Resources/Animation/Map/Level_02_InkSnowCourtyard";
    private const string PrefabRoot = "Assets/Prefabs/Map/Level_02_InkSnowCourtyard";
    private const string BarrelRoot = AnimationRoot + "/Barrel";
    private const string ChestRoot = AnimationRoot + "/ChestSnow";
    private const string BarrelControllerPath = BarrelRoot + "/Barrel_Break.controller";
    private const string ChestControllerPath = ChestRoot + "/ChestSnow_Break.controller";
    private const string BarrelPrefabPath = PrefabRoot + "/Prefab_BarrelBreakable.prefab";
    private const string ChestPrefabPath = PrefabRoot + "/Prefab_ChestSnowBreakable.prefab";
    private const float FramesPerSecond = 12f;

    static BuildLevel02BreakAnimations()
    {
        EditorApplication.delayCall += AutoBuild;
    }

    [MenuItem("Tools/NewMini/Level 02/Rebuild Break Animations")]
    public static void BuildFromMenu()
    {
        BuildAll(true);
    }

    private static void AutoBuild()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += AutoBuild;
            return;
        }

        try
        {
            BuildAll(false);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static void BuildAll(bool forceRebuild)
    {
        var barrelController = AssetDatabase.LoadAssetAtPath<AnimatorController>(BarrelControllerPath);
        var chestController = AssetDatabase.LoadAssetAtPath<AnimatorController>(ChestControllerPath);

        if (forceRebuild || barrelController == null || chestController == null)
        {
            var barrelFrames = LoadFrames(BarrelRoot + "/Frames", "Barrel_Break", 6);
            var chestFrames = LoadFrames(ChestRoot + "/Frames", "ChestSnow_Break", 6);

            barrelController = BuildController(BarrelRoot, "Barrel", barrelFrames);
            chestController = BuildController(ChestRoot, "ChestSnow", chestFrames);

            CreatePrefab(BarrelPrefabPath, "Prop_BarrelBreakable", barrelController, barrelFrames[0], new Vector2(0.85f, 1.1f));
            CreatePrefab(ChestPrefabPath, "Prop_ChestSnowBreakable", chestController, chestFrames[0], new Vector2(1.45f, 1.15f));
            AssetDatabase.SaveAssets();
        }

        var barrelIdle = LoadFrame(BarrelRoot + "/Frames/Barrel_Break_01.png");
        var chestIdle = LoadFrame(ChestRoot + "/Frames/ChestSnow_Break_01.png");
        SetupScene(barrelController, chestController, barrelIdle, chestIdle);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static List<Sprite> LoadFrames(string folder, string prefix, int count)
    {
        var frames = new List<Sprite>(count);
        for (var index = 1; index <= count; index++)
        {
            var path = $"{folder}/{prefix}_{index:00}.png";
            ConfigureTexture(path);
            frames.Add(LoadFrame(path));
        }

        return frames;
    }

    private static Sprite LoadFrame(string path)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            throw new FileNotFoundException($"Break animation sprite was not imported: {path}");
        }

        return sprite;
    }

    private static void ConfigureTexture(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            throw new FileNotFoundException($"Texture importer was not found: {path}");
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    private static AnimatorController BuildController(string folder, string name, IReadOnlyList<Sprite> frames)
    {
        Directory.CreateDirectory(folder);
        var idle = CreateClip($"{folder}/{name}_Idle.anim", "Visual", new[] { frames[0] }, true);
        var breaking = CreateClip($"{folder}/{name}_Break.anim", "Visual", frames, false);
        var broken = CreateClip($"{folder}/{name}_Broken.anim", "Visual", new[] { frames[frames.Count - 1] }, true);
        var controllerPath = $"{folder}/{name}_Break.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        }

        controller.parameters = new[]
        {
            new AnimatorControllerParameter
            {
                name = "Break",
                type = AnimatorControllerParameterType.Trigger
            }
        };

        var stateMachine = controller.layers[0].stateMachine;
        foreach (var childState in stateMachine.states)
        {
            stateMachine.RemoveState(childState.state);
        }

        var intactState = stateMachine.AddState("Intact", new Vector3(220f, 40f));
        intactState.motion = idle;
        stateMachine.defaultState = intactState;

        var breakingState = stateMachine.AddState("Breaking", new Vector3(460f, 40f));
        breakingState.motion = breaking;
        var brokenState = stateMachine.AddState("Broken", new Vector3(700f, 40f));
        brokenState.motion = broken;

        var startTransition = intactState.AddTransition(breakingState);
        startTransition.hasExitTime = false;
        startTransition.duration = 0f;
        startTransition.AddCondition(AnimatorConditionMode.If, 0f, "Break");

        var finishTransition = breakingState.AddTransition(brokenState);
        finishTransition.hasExitTime = true;
        finishTransition.exitTime = 1f;
        finishTransition.duration = 0f;

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static AnimationClip CreateClip(string path, string rendererPath, IReadOnlyList<Sprite> frames, bool loop)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip { frameRate = FramesPerSecond };
            AssetDatabase.CreateAsset(clip, path);
        }

        clip.frameRate = FramesPerSecond;
        var binding = EditorCurveBinding.PPtrCurve(rendererPath, typeof(SpriteRenderer), "m_Sprite");
        var keys = new ObjectReferenceKeyframe[frames.Count + (frames.Count > 1 ? 1 : 0)];
        for (var index = 0; index < frames.Count; index++)
        {
            keys[index] = new ObjectReferenceKeyframe
            {
                time = index / FramesPerSecond,
                value = frames[index]
            };
        }

        if (frames.Count > 1)
        {
            keys[keys.Length - 1] = new ObjectReferenceKeyframe
            {
                time = frames.Count / FramesPerSecond,
                value = frames[frames.Count - 1]
            };
        }

        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static void CreatePrefab(string path, string objectName, RuntimeAnimatorController controller, Sprite idleSprite, Vector2 colliderSize)
    {
        var root = new GameObject(objectName);
        var visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        var renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = idleSprite;
        renderer.sortingOrder = 820;

        var animator = root.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        root.AddComponent<BreakableMapProp>();
        var propCollider = root.AddComponent<BoxCollider2D>();
        propCollider.size = colliderSize;

        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? PrefabRoot);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
    }

    private static void SetupScene(RuntimeAnimatorController barrelController, RuntimeAnimatorController chestController, Sprite barrelIdle, Sprite chestIdle)
    {
        var scene = SceneManager.GetSceneByPath(ScenePath);
        var openedTemporarily = !scene.IsValid() || !scene.isLoaded;
        if (openedTemporarily)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        }

        var changes = 0;
        changes += SetupNamedProp(scene, "Prop_Barrel_Talisman_A", barrelController, barrelIdle);
        changes += SetupNamedProp(scene, "Prop_Barrel_Talisman_B", barrelController, barrelIdle);
        changes += SetupNamedProp(scene, "Prop_Chest_Snow_A", chestController, chestIdle);
        changes += SetupNamedProp(scene, "Prop_Chest_Snow_B", chestController, chestIdle);

        if (changes > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"Level 02 break animations configured on {changes} scene props.");
        }

        if (openedTemporarily)
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static int SetupNamedProp(Scene scene, string objectName, RuntimeAnimatorController controller, Sprite idleSprite)
    {
        var prop = FindByName(scene, objectName);
        if (prop == null)
        {
            Debug.LogWarning($"Could not find {objectName} in {ScenePath}.");
            return 0;
        }

        var changed = false;
        var visualTransform = prop.transform.Find("Visual");
        var rootRenderer = prop.GetComponent<SpriteRenderer>();
        SpriteRenderer visualRenderer;

        if (visualTransform == null)
        {
            var visual = new GameObject("Visual");
            visualTransform = visual.transform;
            visualTransform.SetParent(prop.transform, false);
            visualRenderer = visual.AddComponent<SpriteRenderer>();

            if (rootRenderer != null)
            {
                CopyRenderer(rootRenderer, visualRenderer);
                if (rootRenderer.sprite != null)
                {
                    var oldPivot = rootRenderer.sprite.rect.position + rootRenderer.sprite.pivot;
                    var newPivot = idleSprite.rect.position + idleSprite.pivot;
                    var ppu = idleSprite.pixelsPerUnit;
                    visualTransform.localPosition = (newPivot - oldPivot) / ppu;
                }

                UnityEngine.Object.DestroyImmediate(rootRenderer, true);
            }

            changed = true;
        }
        else
        {
            visualRenderer = visualTransform.GetComponent<SpriteRenderer>();
            if (visualRenderer == null)
            {
                visualRenderer = visualTransform.gameObject.AddComponent<SpriteRenderer>();
                changed = true;
            }
        }

        if (visualRenderer.sprite != idleSprite)
        {
            visualRenderer.sprite = idleSprite;
            changed = true;
        }

        var animator = prop.GetComponent<Animator>();
        if (animator == null)
        {
            animator = prop.AddComponent<Animator>();
            changed = true;
        }

        if (animator.runtimeAnimatorController != controller)
        {
            animator.runtimeAnimatorController = controller;
            changed = true;
        }

        if (prop.GetComponent<BreakableMapProp>() == null)
        {
            prop.AddComponent<BreakableMapProp>();
            changed = true;
        }

        if (GameObjectUtility.GetStaticEditorFlags(prop) != 0)
        {
            GameObjectUtility.SetStaticEditorFlags(prop, 0);
            changed = true;
        }

        return changed ? 1 : 0;
    }

    private static void CopyRenderer(SpriteRenderer source, SpriteRenderer destination)
    {
        destination.sprite = source.sprite;
        destination.color = source.color;
        destination.flipX = source.flipX;
        destination.flipY = source.flipY;
        destination.sharedMaterial = source.sharedMaterial;
        destination.sortingLayerID = source.sortingLayerID;
        destination.sortingOrder = source.sortingOrder;
        destination.maskInteraction = source.maskInteraction;
        destination.drawMode = source.drawMode;
        destination.size = source.size;
    }

    private static GameObject FindByName(Scene scene, string objectName)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var found = FindRecursive(root.transform, objectName);
            if (found != null)
            {
                return found.gameObject;
            }
        }

        return null;
    }

    private static Transform FindRecursive(Transform current, string objectName)
    {
        if (current.name == objectName)
        {
            return current;
        }

        for (var index = 0; index < current.childCount; index++)
        {
            var found = FindRecursive(current.GetChild(index), objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
