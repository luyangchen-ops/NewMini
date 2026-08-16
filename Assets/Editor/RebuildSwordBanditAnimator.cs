using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class RebuildSwordBanditAnimator
{
    private const string Root = "Assets/Resources/Animation/SwordBandit";
    private const string AtlasPath = Root + "/SwordBandit_AllStates.png";
    private const string ControllerPath = Root + "/SwordBandit.controller";
    private const int CellSize = 256;

    [MenuItem("Tools/Animation/Rebuild Sword Bandit Animator")]
    public static void Rebuild()
    {
        ConfigureAtlas();

        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(AtlasPath)
            .OfType<Sprite>()
            .OrderBy(sprite => sprite.name, StringComparer.Ordinal)
            .ToArray();

        if (sprites.Length != 28)
        {
            throw new InvalidOperationException($"Expected 28 sword-bandit sprites, found {sprites.Length}.");
        }

        Dictionary<string, Sprite> spritesByName = sprites.ToDictionary(sprite => sprite.name);
        AnimationClip idle = CreateClip("SwordBandit_Idle", GetFrames(spritesByName, "Idle", 4), 6f, true);
        AnimationClip run = CreateClip("SwordBandit_Run", GetFrames(spritesByName, "Run", 8), 12f, true);
        AnimationClip attack = CreateClip("SwordBandit_Attack", GetFrames(spritesByName, "Attack", 8), 12f, false);
        AnimationClip death = CreateClip("SwordBandit_Death", GetFrames(spritesByName, "Death", 8), 10f, false);
        CreateController(idle, run, attack, death);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Sword Bandit atlas, clips, and Animator Controller rebuilt successfully.");
    }

    private static void ConfigureAtlas()
    {
        AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(AtlasPath) as TextureImporter;
        if (importer == null)
        {
            throw new InvalidOperationException($"TextureImporter not found for {AtlasPath}.");
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 60f;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;

#pragma warning disable CS0618
        importer.spritesheet = BuildSpriteMetadata();
#pragma warning restore CS0618
        importer.SaveAndReimport();
    }

    private static SpriteMetaData[] BuildSpriteMetadata()
    {
        List<SpriteMetaData> metadata = new(28);
        for (int row = 0; row < 7; row++)
        {
            for (int column = 0; column < 4; column++)
            {
                (string state, int frame) = GetStateAndFrame(row, column);
                metadata.Add(new SpriteMetaData
                {
                    name = $"SwordBandit_{state}_{frame:00}",
                    rect = new Rect(column * CellSize, (6 - row) * CellSize, CellSize, CellSize),
                    alignment = (int)SpriteAlignment.Custom,
                    pivot = new Vector2(.5f, 8f / CellSize),
                    border = Vector4.zero
                });
            }
        }

        return metadata.ToArray();
    }

    private static (string state, int frame) GetStateAndFrame(int row, int column)
    {
        if (row == 0) return ("Idle", column + 1);
        if (row <= 2) return ("Run", (row - 1) * 4 + column + 1);
        if (row <= 4) return ("Attack", (row - 3) * 4 + column + 1);
        return ("Death", (row - 5) * 4 + column + 1);
    }

    private static Sprite[] GetFrames(IReadOnlyDictionary<string, Sprite> sprites, string state, int count)
    {
        Sprite[] frames = new Sprite[count];
        for (int index = 0; index < count; index++)
        {
            string name = $"SwordBandit_{state}_{index + 1:00}";
            if (!sprites.TryGetValue(name, out frames[index]))
            {
                throw new InvalidOperationException($"Missing sprite {name}.");
            }
        }

        return frames;
    }

    private static AnimationClip CreateClip(string name, IReadOnlyList<Sprite> frames, float frameRate, bool loop)
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

    private static void CreateController(AnimationClip idle, AnimationClip run, AnimationClip attack, AnimationClip death)
    {
        AssetDatabase.DeleteAsset(ControllerPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState idleState = machine.AddState("Idle", new Vector3(260f, 40f));
        AnimatorState runState = machine.AddState("Run", new Vector3(520f, 40f));
        AnimatorState attackState = machine.AddState("Attack", new Vector3(520f, 170f));
        AnimatorState deathState = machine.AddState("Death", new Vector3(520f, 300f));
        idleState.motion = idle;
        runState.motion = run;
        attackState.motion = attack;
        deathState.motion = death;
        machine.defaultState = idleState;

        AddBoolTransition(idleState, runState, true);
        AddBoolTransition(runState, idleState, false);

        AnimatorStateTransition attackTransition = machine.AddAnyStateTransition(attackState);
        attackTransition.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
        ConfigureImmediateTransition(attackTransition);
        attackTransition.canTransitionToSelf = false;

        AnimatorStateTransition deathTransition = machine.AddAnyStateTransition(deathState);
        deathTransition.AddCondition(AnimatorConditionMode.If, 0f, "Death");
        ConfigureImmediateTransition(deathTransition);
        deathTransition.canTransitionToSelf = false;

        AnimatorStateTransition attackExit = attackState.AddTransition(idleState);
        attackExit.hasExitTime = true;
        attackExit.exitTime = 1f;
        attackExit.duration = 0f;

        EditorUtility.SetDirty(controller);
    }

    private static void AddBoolTransition(AnimatorState from, AnimatorState to, bool value)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, "IsMoving");
        ConfigureImmediateTransition(transition);
    }

    private static void ConfigureImmediateTransition(AnimatorStateTransition transition)
    {
        transition.hasExitTime = false;
        transition.duration = .03f;
        transition.hasFixedDuration = true;
    }
}
