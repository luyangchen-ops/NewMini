using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>Builds the authored directional sprite clips and the controller used by every hero scene.</summary>
[InitializeOnLoad]
public static class BuildHeroDirectionalAnimations
{
    private const string ArtRoot = "Assets/Resources/InkWuxiaHero/Art/Sprites";
    private const string ClipsRoot = "Assets/Resources/InkWuxiaHero/Animations/Clips";
    private const string ControllerPath = "Assets/Resources/InkWuxiaHero/Animations/Controller/Hero_InkWuxia.controller";
    private const float RunFps = 14f;
    private const float AttackFps = 16f;
    private const string AlignmentVersion = "InkWuxiaHero.FootAnchored.v1";
    private const int RunAnchorX = 512;
    // Texture2D pixels use a bottom-left origin. Source feet sit 32 pixels above it.
    private const int RunAnchorFootY = 32;

    static BuildHeroDirectionalAnimations()
    {
        EditorApplication.delayCall += BuildIfNeeded;
    }

    private static void BuildIfNeeded()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += BuildIfNeeded;
            return;
        }

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null || AssetDatabase.LoadAssetAtPath<AnimationClip>($"{ClipsRoot}/Hero_RunUp.anim") == null ||
            Array.Find(controller.parameters, parameter => parameter.name == "VerticalDirection") == null || NeedsRunFrameAlignment())
        {
            NormalizeRunFrames();
            Build();
        }
    }

    [MenuItem("Tools/NewMini/Hero/Rebuild Directional Animations")]
    public static void Build()
    {
        Directory.CreateDirectory(ClipsRoot);

        AnimationClip idle = CreateClip("Hero_Idle", "Idle", 1, 1f, true);
        AnimationClip run = CreateClip("Hero_Run", "Run", 12, RunFps, true);
        AnimationClip runUp = CreateClip("Hero_RunUp", "RunUp", 12, RunFps, true);
        AnimationClip runDown = CreateClip("Hero_RunDown", "RunDown", 12, RunFps, true);
        AnimationClip attack = CreateClip("Hero_NormalAttack", "NormalAttack", 8, AttackFps, false);
        AnimationClip attackUp = CreateClip("Hero_NormalAttackUp", "NormalAttackUp", 8, AttackFps, false);
        AnimationClip attackDown = CreateClip("Hero_NormalAttackDown", "NormalAttackDown", 8, AttackFps, false);
        AnimationClip dash = LoadClip("Hero_DashAttack");
        AnimationClip roll = LoadClip("Hero_Roll");
        AnimationClip hurt = LoadClip("Hero_Hurt");
        AnimationClip death = LoadClip("Hero_Death");

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.parameters = new[]
        {
            Float("Speed"), Int("VerticalDirection"), Trigger("NormalAttack"), Trigger("DashAttack"),
            Trigger("Roll"), Trigger("Hurt"), Bool("IsDead")
        };

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState child in machine.states) machine.RemoveState(child.state);

        AnimatorState idleState = State(machine, "Idle", idle, 200, 0);
        AnimatorState runState = State(machine, "Run", run, 450, 0);
        AnimatorState runUpState = State(machine, "Run Up", runUp, 450, 80);
        AnimatorState runDownState = State(machine, "Run Down", runDown, 450, 160);
        AnimatorState attackState = State(machine, "Normal Attack", attack, 700, 0);
        AnimatorState attackUpState = State(machine, "Normal Attack Up", attackUp, 700, 80);
        AnimatorState attackDownState = State(machine, "Normal Attack Down", attackDown, 700, 160);
        AnimatorState dashState = State(machine, "Dash Attack", dash, 700, 240);
        AnimatorState rollState = State(machine, "Roll", roll, 700, 320);
        AnimatorState hurtState = State(machine, "Hurt", hurt, 700, 400);
        AnimatorState deathState = State(machine, "Death", death, 700, 480);
        machine.defaultState = idleState;

        AddMovementTransition(idleState, runState, AnimatorConditionMode.Equals, 0f);
        AddMovementTransition(idleState, runUpState, AnimatorConditionMode.Equals, 1f);
        AddMovementTransition(idleState, runDownState, AnimatorConditionMode.Equals, -1f);
        AddStopTransition(runState, idleState);
        AddStopTransition(runUpState, idleState);
        AddStopTransition(runDownState, idleState);
        AddRunDirectionTransition(runState, runUpState, 1f);
        AddRunDirectionTransition(runState, runDownState, -1f);
        AddRunDirectionTransition(runUpState, runState, 0f);
        AddRunDirectionTransition(runUpState, runDownState, -1f);
        AddRunDirectionTransition(runDownState, runState, 0f);
        AddRunDirectionTransition(runDownState, runUpState, 1f);

        AddAnyTransition(machine, attackState, "NormalAttack", 0f, "VerticalDirection");
        AddAnyTransition(machine, attackUpState, "NormalAttack", 1f, "VerticalDirection");
        AddAnyTransition(machine, attackDownState, "NormalAttack", -1f, "VerticalDirection");
        AddAnyTransition(machine, dashState, "DashAttack");
        AddAnyTransition(machine, rollState, "Roll");
        AddAnyTransition(machine, hurtState, "Hurt");
        AddAnyTransition(machine, deathState, "IsDead");
        AddExit(attackState, idleState); AddExit(attackUpState, idleState); AddExit(attackDownState, idleState);
        AddExit(dashState, idleState); AddExit(rollState, idleState); AddExit(hurtState, idleState);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Hero directional animations and Animator Controller rebuilt.");
    }

    [MenuItem("Tools/NewMini/Hero/Normalize Directional Run Frames")]
    public static void NormalizeRunFrames()
    {
        NormalizeFolder("RunUp", 12);
        NormalizeFolder("RunDown", 12);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Directional run frames aligned to a shared foot anchor.");
    }

    private static bool NeedsRunFrameAlignment()
    {
        var importer = AssetImporter.GetAtPath($"{ArtRoot}/RunUp/RunUp_00.png");
        return importer == null || importer.userData != AlignmentVersion;
    }

    private static void NormalizeFolder(string folder, int frameCount)
    {
        for (int index = 0; index < frameCount; index++)
        {
            string path = $"{ArtRoot}/{folder}/{folder}_{index:00}.png";
            byte[] png = File.ReadAllBytes(path);
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(source, png, false))
                throw new InvalidOperationException($"Unable to read directional hero sprite: {path}");

            Color32[] pixels = source.GetPixels32();
            FindOpaqueBounds(pixels, source.width, source.height, out int left, out int footY, out int right);
            int currentCenterX = Mathf.RoundToInt((left + right) * .5f);
            int shiftX = RunAnchorX - currentCenterX;
            int shiftY = RunAnchorFootY - footY;
            var aligned = new Color32[pixels.Length];

            for (int y = 0; y < source.height; y++)
            for (int x = 0; x < source.width; x++)
            {
                int destinationX = x + shiftX;
                int destinationY = y + shiftY;
                if (destinationX < 0 || destinationX >= source.width || destinationY < 0 || destinationY >= source.height) continue;
                aligned[destinationY * source.width + destinationX] = pixels[y * source.width + x];
            }

            var output = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            output.SetPixels32(aligned);
            output.Apply(false, false);
            File.WriteAllBytes(path, output.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(source);
            UnityEngine.Object.DestroyImmediate(output);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path);
            if (importer != null) { importer.userData = AlignmentVersion; EditorUtility.SetDirty(importer); }
        }
    }

    private static void FindOpaqueBounds(Color32[] pixels, int width, int height, out int left, out int footY, out int right)
    {
        left = width; right = -1; footY = height;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            if (pixels[y * width + x].a <= 8) continue;
            left = Mathf.Min(left, x);
            right = Mathf.Max(right, x);
            footY = Mathf.Min(footY, y);
        }
        if (right < left) throw new InvalidOperationException("Directional hero frame contains no visible pixels.");
    }

    private static AnimationClip CreateClip(string name, string folder, int count, float fps, bool loop)
    {
        var frames = new List<Sprite>(count);
        for (int i = 0; i < count; i++)
        {
            string path = $"{ArtRoot}/{folder}/{folder}_{i:00}.png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new FileNotFoundException($"Missing hero frame: {path}");
            frames.Add(sprite);
        }
        string pathToClip = $"{ClipsRoot}/{name}.anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(pathToClip);
        if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, pathToClip); }
        clip.frameRate = fps;
        var keys = new ObjectReferenceKeyframe[count];
        for (int i = 0; i < count; i++) keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[i] };
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
        var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings); EditorUtility.SetDirty(clip);
        return clip;
    }

    private static AnimationClip LoadClip(string name) => AssetDatabase.LoadAssetAtPath<AnimationClip>($"{ClipsRoot}/{name}.anim");
    private static AnimatorControllerParameter Float(string name) => new AnimatorControllerParameter { name = name, type = AnimatorControllerParameterType.Float };
    private static AnimatorControllerParameter Int(string name) => new AnimatorControllerParameter { name = name, type = AnimatorControllerParameterType.Int };
    private static AnimatorControllerParameter Trigger(string name) => new AnimatorControllerParameter { name = name, type = AnimatorControllerParameterType.Trigger };
    private static AnimatorControllerParameter Bool(string name) => new AnimatorControllerParameter { name = name, type = AnimatorControllerParameterType.Bool };
    private static AnimatorState State(AnimatorStateMachine m, string name, Motion motion, float x, float y) { var s = m.AddState(name, new Vector3(x, y)); s.motion = motion; return s; }
    private static void AddMovementTransition(AnimatorState from, AnimatorState to, AnimatorConditionMode verticalMode, float vertical) { var t = from.AddTransition(to); t.hasExitTime = false; t.duration = .08f; t.AddCondition(AnimatorConditionMode.Greater, .1f, "Speed"); t.AddCondition(verticalMode, vertical, "VerticalDirection"); }
    private static void AddStopTransition(AnimatorState from, AnimatorState to) { var t = from.AddTransition(to); t.hasExitTime = false; t.duration = .08f; t.AddCondition(AnimatorConditionMode.Less, .1f, "Speed"); }
    private static void AddRunDirectionTransition(AnimatorState from, AnimatorState to, float vertical) { var t = from.AddTransition(to); t.hasExitTime = false; t.duration = .08f; t.AddCondition(AnimatorConditionMode.Equals, vertical, "VerticalDirection"); }
    private static void AddAnyTransition(AnimatorStateMachine m, AnimatorState to, string trigger, float vertical = 0f, string verticalParameter = null) { var t = m.AddAnyStateTransition(to); t.hasExitTime = false; t.duration = .04f; t.AddCondition(AnimatorConditionMode.If, 0f, trigger); if (verticalParameter != null) t.AddCondition(AnimatorConditionMode.Equals, vertical, verticalParameter); }
    private static void AddExit(AnimatorState from, AnimatorState to) { var t = from.AddTransition(to); t.hasExitTime = true; t.exitTime = 1f; t.duration = .05f; }
}
