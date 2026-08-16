using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Rebuilds only the Bandit Spearman V4 clips/controller from the artist's
/// existing manual Sprite slices. This tool never changes V4 texture import data.
/// </summary>
public static class BuildBanditSpearmanAnimator
{
    private const string Root = "Assets/Resources/Animation/Spearman";
    private const string AtlasV4Path = Root + "/BanditSpearman_Atlas_v4.png";
    private const string ControllerPath = Root + "/BanditSpearman.controller";

    [MenuItem("Tools/Animation/Update Bandit Spearman Animator From V4")]
    public static void BuildFromV4()
    {
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(AtlasV4Path).OfType<Sprite>().ToArray();
        if (sprites.Length == 0)
        {
            throw new InvalidOperationException($"No Sprite sub-assets found at {AtlasV4Path}.");
        }

        AnimationClip idle = CreateClip("BanditSpearman_Idle", GetFramesByRow(sprites, 700f, float.PositiveInfinity), 6f, true);
        AnimationClip move = CreateClip("BanditSpearman_Move", GetFramesByRow(sprites, 490f, 700f), 10f, true);
        AnimationClip dash = CreateClip("BanditSpearman_DashAttack", GetFramesByRow(sprites, 220f, 490f), 12f, false);
        AnimationClip death = CreateClip("BanditSpearman_Death", GetFramesByRow(sprites, float.NegativeInfinity, 220f), 8f, false);
        CreateController(idle, move, dash, death);
        AssetDatabase.SaveAssets();
    }

    private static Sprite[] GetFramesByRow(IEnumerable<Sprite> sprites, float minY, float maxY)
    {
        Sprite[] frames = sprites.Where(sprite => sprite.rect.y >= minY && sprite.rect.y < maxY)
            .OrderBy(sprite => sprite.rect.x).ToArray();
        if (frames.Length == 0)
        {
            throw new InvalidOperationException($"No V4 frames found in vertical range {minY}..{maxY}.");
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
            keys[index] = new ObjectReferenceKeyframe { time = index / frameRate, value = frames[index] };
        }
        keys[^1] = new ObjectReferenceKeyframe { time = frames.Count / frameRate, value = frames[^1] };
        AnimationUtility.SetObjectReferenceCurve(clip, new EditorCurveBinding
        {
            path = string.Empty,
            type = typeof(SpriteRenderer),
            propertyName = "m_Sprite"
        }, keys);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        settings.loopBlend = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static void CreateController(AnimationClip idle, AnimationClip move, AnimationClip dash, AnimationClip death)
    {
        AssetDatabase.DeleteAsset(ControllerPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("DashAttack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState idleState = machine.AddState("Idle", new Vector3(220f, 40f));
        AnimatorState moveState = machine.AddState("Move", new Vector3(460f, 40f));
        AnimatorState dashState = machine.AddState("DashAttack", new Vector3(460f, 170f));
        AnimatorState deathState = machine.AddState("Death", new Vector3(460f, 300f));
        idleState.motion = idle;
        moveState.motion = move;
        dashState.motion = dash;
        deathState.motion = death;
        machine.defaultState = idleState;

        AddMoveTransition(idleState, moveState, true);
        AddMoveTransition(moveState, idleState, false);
        AddTriggerTransition(machine, dashState, "DashAttack");
        AddTriggerTransition(machine, dashState, "Attack");
        AddTriggerTransition(machine, deathState, "Death");
        AddExitTransition(dashState, idleState);
        EditorUtility.SetDirty(controller);
    }

    private static void AddMoveTransition(AnimatorState from, AnimatorState to, bool value)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, "IsMoving");
        ConfigureImmediate(transition);
    }

    private static void AddTriggerTransition(AnimatorStateMachine machine, AnimatorState destination, string trigger)
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
    }

    private static void ConfigureImmediate(AnimatorStateTransition transition)
    {
        transition.hasExitTime = false;
        transition.duration = .03f;
        transition.hasFixedDuration = true;
    }
}
