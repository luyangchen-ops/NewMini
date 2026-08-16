using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class RebuildShieldWarriorAnimator
{
    private const string Root = "Assets/Resources/Animation/盾兵";
    private const string AtlasPath = Root + "/ShieldWarrior_ReferenceInkStyle 1.png";
    private const string ControllerPath = Root + "/ShieldWarrior.controller";
    private const int ExpectedSpriteCount = 38;

    private static readonly (string State, int Count, float FrameRate, bool Loop)[] Rows =
    {
        ("Idle", 8, 8f, true),
        ("Run", 8, 12f, true),
        ("Block", 8, 14f, false),
        ("Attack", 7, 8f, false),
        ("Death", 7, 8f, false)
    };

    [MenuItem("Tools/Animation/Rebuild Shield Warrior Animator")]
    public static void Rebuild()
    {
        ConfigureAtlas();

        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(AtlasPath)
            .OfType<Sprite>()
            .ToArray();
        if (sprites.Length != ExpectedSpriteCount)
        {
            throw new InvalidOperationException(
                $"Expected {ExpectedSpriteCount} shield-warrior sprites, found {sprites.Length} at {AtlasPath}.");
        }

        Dictionary<string, Sprite> spritesByName = sprites.ToDictionary(sprite => sprite.name);
        Dictionary<string, AnimationClip> clips = new(StringComparer.Ordinal);
        foreach ((string state, int count, float frameRate, bool loop) in Rows)
        {
            clips[state] = CreateClip(
                $"ShieldWarrior_{state}",
                GetFrames(spritesByName, state, count),
                frameRate,
                loop);
        }

        CreateController(clips);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Shield Warrior atlas, five clips, and Animator Controller rebuilt successfully.");
    }

    private static void ConfigureAtlas()
    {
        AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(AtlasPath) as TextureImporter;
        if (importer == null)
        {
            throw new InvalidOperationException($"TextureImporter not found for {AtlasPath}.");
        }

#pragma warning disable CS0618
        SpriteMetaData[] sourceMetadata = importer.spritesheet;
#pragma warning restore CS0618
        if (sourceMetadata.Length != ExpectedSpriteCount)
        {
            throw new InvalidOperationException(
                $"The adjusted atlas must contain {ExpectedSpriteCount} valid slices before rebuilding; found {sourceMetadata.Length}.");
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 100f;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;
        importer.isReadable = true;
        importer.SaveAndReimport();

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
        if (texture == null)
        {
            throw new InvalidOperationException($"Texture could not be loaded at {AtlasPath}.");
        }

        SpriteMetaData[] rebuiltMetadata = BuildMetadata(sourceMetadata, texture);
#pragma warning disable CS0618
        importer.spritesheet = rebuiltMetadata;
#pragma warning restore CS0618
        importer.isReadable = false;
        importer.SaveAndReimport();
    }

    private static SpriteMetaData[] BuildMetadata(IReadOnlyCollection<SpriteMetaData> source, Texture2D texture)
    {
        List<SpriteMetaData> sorted = source
            .OrderByDescending(item => item.rect.center.y)
            .ToList();
        List<SpriteMetaData> result = new(ExpectedSpriteCount);
        int offset = 0;

        foreach ((string state, int count, _, _) in Rows)
        {
            List<SpriteMetaData> row = sorted
                .Skip(offset)
                .Take(count)
                .OrderBy(item => item.rect.center.x)
                .ToList();
            offset += count;

            float groundLine = row.Max(item => item.rect.y);
            for (int frame = 0; frame < row.Count; frame++)
            {
                SpriteMetaData sourceItem = row[frame];
                Vector2 pivot = CalculateFootPivot(texture, sourceItem.rect, groundLine);
                result.Add(new SpriteMetaData
                {
                    name = $"ShieldWarrior_{state}_{frame + 1:00}",
                    rect = sourceItem.rect,
                    alignment = (int)SpriteAlignment.Custom,
                    pivot = pivot,
                    border = Vector4.zero
                });
            }
        }

        return result.ToArray();
    }

    private static Vector2 CalculateFootPivot(Texture2D texture, Rect rect, float groundLine)
    {
        int minX = Mathf.RoundToInt(rect.xMin);
        int maxX = Mathf.RoundToInt(rect.xMax);
        int minY = Mathf.Clamp(Mathf.RoundToInt(groundLine), Mathf.RoundToInt(rect.yMin), Mathf.RoundToInt(rect.yMax) - 1);
        int maxY = Mathf.Min(Mathf.RoundToInt(rect.yMax), minY + Mathf.Max(12, Mathf.RoundToInt(rect.height * .32f)));
        double weightedX = 0d;
        double totalWeight = 0d;

        for (int y = minY; y < maxY; y++)
        {
            for (int x = minX; x < maxX; x++)
            {
                float alpha = texture.GetPixel(x, y).a;
                if (alpha <= .08f) continue;
                weightedX += (x + .5d) * alpha;
                totalWeight += alpha;
            }
        }

        float footCenterX = totalWeight > 0d
            ? (float)(weightedX / totalWeight)
            : rect.center.x;
        return new Vector2(
            Mathf.Clamp01((footCenterX - rect.x) / rect.width),
            Mathf.Clamp01((groundLine - rect.y) / rect.height));
    }

    private static Sprite[] GetFrames(
        IReadOnlyDictionary<string, Sprite> sprites,
        string state,
        int count)
    {
        Sprite[] frames = new Sprite[count];
        for (int index = 0; index < count; index++)
        {
            string name = $"ShieldWarrior_{state}_{index + 1:00}";
            if (!sprites.TryGetValue(name, out frames[index]))
            {
                throw new InvalidOperationException($"Missing sprite {name}.");
            }
        }

        return frames;
    }

    private static AnimationClip CreateClip(
        string name,
        IReadOnlyList<Sprite> frames,
        float frameRate,
        bool loop)
    {
        string path = $"{Root}/{name}.anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip { name = name };
            AssetDatabase.CreateAsset(clip, path);
        }
        else
        {
            clip.ClearCurves();
        }

        clip.frameRate = frameRate;
        ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[frames.Count + 1];
        for (int index = 0; index < frames.Count; index++)
        {
            keys[index] = new ObjectReferenceKeyframe
            {
                time = index / frameRate,
                value = frames[index]
            };
        }

        keys[^1] = new ObjectReferenceKeyframe
        {
            time = frames.Count / frameRate,
            value = frames[^1]
        };

        EditorCurveBinding binding = new()
        {
            path = string.Empty,
            type = typeof(SpriteRenderer),
            propertyName = "m_Sprite"
        };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        settings.loopBlend = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static void CreateController(IReadOnlyDictionary<string, AnimationClip> clips)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        }

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState child in machine.states.ToArray()) machine.RemoveState(child.state);
        foreach (AnimatorControllerParameter parameter in controller.parameters.ToArray()) controller.RemoveParameter(parameter);

        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Block", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);

        AnimatorState idleState = machine.AddState("Idle", new Vector3(240f, 40f));
        AnimatorState runState = machine.AddState("Run", new Vector3(500f, 40f));
        AnimatorState blockState = machine.AddState("Block", new Vector3(500f, 150f));
        AnimatorState attackState = machine.AddState("Attack", new Vector3(500f, 260f));
        AnimatorState deathState = machine.AddState("Death", new Vector3(500f, 370f));
        idleState.motion = clips["Idle"];
        runState.motion = clips["Run"];
        blockState.motion = clips["Block"];
        attackState.motion = clips["Attack"];
        deathState.motion = clips["Death"];
        machine.defaultState = idleState;

        AddBoolTransition(idleState, runState, true);
        AddBoolTransition(runState, idleState, false);
        AddTriggerTransition(machine, deathState, "Death");
        AddTriggerTransition(machine, blockState, "Block");
        AddTriggerTransition(machine, attackState, "Attack");
        AddExitTransition(blockState, idleState);
        AddExitTransition(attackState, idleState);

        EditorUtility.SetDirty(controller);
    }

    private static void AddBoolTransition(AnimatorState from, AnimatorState to, bool value)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, "IsMoving");
        ConfigureImmediate(transition);
    }

    private static void AddTriggerTransition(
        AnimatorStateMachine machine,
        AnimatorState destination,
        string trigger)
    {
        AnimatorStateTransition transition = machine.AddAnyStateTransition(destination);
        transition.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        transition.canTransitionToSelf = false;
        ConfigureImmediate(transition);
    }

    private static void AddExitTransition(AnimatorState from, AnimatorState to)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = true;
        transition.exitTime = 1f;
        transition.duration = 0f;
        transition.hasFixedDuration = true;
    }

    private static void ConfigureImmediate(AnimatorStateTransition transition)
    {
        transition.hasExitTime = false;
        transition.duration = .03f;
        transition.hasFixedDuration = true;
    }
}
