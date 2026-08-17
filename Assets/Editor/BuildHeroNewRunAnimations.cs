using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Extracts the approved New Run sheets into equally sized, foot-anchored sprites and
/// assigns the resulting clips to the existing hero Animator states.
/// </summary>
[InitializeOnLoad]
public static class BuildHeroNewRunAnimations
{
    private const string NewRunRoot = "Assets/Resources/InkWuxiaHero/Animations/Clips/New Run";
    private const string HorizontalSourcePath = NewRunRoot + "/CharacterRunV2.png";
    private const string VerticalSourcePath = NewRunRoot + "/Character RunUPandDown.png";
    private const string AlignedRoot = NewRunRoot + "/Aligned";
    private const string ControllerPath = "Assets/Resources/InkWuxiaHero/Animations/Controller/Hero_InkWuxia.controller";
    private const string RunClipPath = NewRunRoot + "/Hero_RunV2.anim";
    private const string RunUpClipPath = NewRunRoot + "/Hero_RunUpV2.anim";
    private const string RunDownClipPath = NewRunRoot + "/Hero_RunDownV2.anim";
    private const string BuildVersion = "InkWuxiaHero.NewRun.ScaleMatchedPPU50.v3";

    private const int CanvasSize = 256;
    private const int TargetCharacterHeight = 180;
    private const int CanvasPadding = 12;
    private const int FootAnchorX = CanvasSize / 2;
    private const int FootAnchorY = 20;
    private const int AlphaThreshold = 8;
    private const float SpritePixelsPerUnit = 50f;
    private const float HorizontalRunFps = 12f;
    private const float VerticalRunFps = 16f;

    private static bool isBuilding;

    static BuildHeroNewRunAnimations()
    {
        EditorApplication.delayCall += BuildIfNeeded;
    }

    private static void BuildIfNeeded()
    {
        if (isBuilding) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += BuildIfNeeded;
            return;
        }

        if (NeedsBuild()) BuildAndAssign();
        else AssignClipsToController();
    }

    [MenuItem("Tools/NewMini/Hero/Rebuild New Run Animations")]
    public static void BuildAndAssign()
    {
        if (isBuilding) return;
        isBuilding = true;
        try
        {
            Texture2D horizontalSheet = LoadReadableTexture(HorizontalSourcePath);
            Texture2D verticalSheet = LoadReadableTexture(VerticalSourcePath);

            // CharacterRunV2 is arranged as 2 columns x 3 rows, read left-to-right
            // and top-to-bottom. The new vertical sheet is 8 columns x 2 rows:
            // the authored upper row faces down and the lower row faces up.
            List<ExtractedFrame> run = ExtractGrid(horizontalSheet, 2, 3, 0, 3);
            // The authored down-facing row reserves its lowest 70 pixels for
            // frame numbers. Exclude that annotation band before component QA.
            List<ExtractedFrame> runDown = ExtractGrid(verticalSheet, 8, 2, 0, 1, 70);
            List<ExtractedFrame> runUp = ExtractGrid(verticalSheet, 8, 2, 1, 1);

            WriteAlignedFrames("Run", run);
            WriteAlignedFrames("RunDown", runDown);
            WriteAlignedFrames("RunUp", runUp);

            UnityEngine.Object.DestroyImmediate(horizontalSheet);
            UnityEngine.Object.DestroyImmediate(verticalSheet);

            AnimationClip runClip = CreateClip(RunClipPath, "Run", run.Count, HorizontalRunFps);
            AnimationClip runUpClip = CreateClip(RunUpClipPath, "RunUp", runUp.Count, VerticalRunFps);
            AnimationClip runDownClip = CreateClip(RunDownClipPath, "RunDown", runDown.Count, VerticalRunFps);
            MarkBuilt(runClip, runUpClip, runDownClip);
            AssignClipsToController(runClip, runUpClip, runDownClip);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("New hero run animations rebuilt: 6 horizontal, 8 up, 8 down; shared size and foot anchors applied.");
        }
        finally
        {
            isBuilding = false;
        }
    }

    private static bool NeedsBuild()
    {
        if (!File.Exists(HorizontalSourcePath) || !File.Exists(VerticalSourcePath)) return false;
        if (!File.Exists(RunClipPath) || !File.Exists(RunUpClipPath) || !File.Exists(RunDownClipPath)) return true;

        AssetImporter importer = AssetImporter.GetAtPath(RunDownClipPath);
        if (importer == null || importer.userData != BuildVersion) return true;

        DateTime newestSource = Max(File.GetLastWriteTimeUtc(HorizontalSourcePath), File.GetLastWriteTimeUtc(VerticalSourcePath));
        DateTime oldestClip = Min(File.GetLastWriteTimeUtc(RunClipPath),
            Min(File.GetLastWriteTimeUtc(RunUpClipPath), File.GetLastWriteTimeUtc(RunDownClipPath)));
        return newestSource > oldestClip;
    }

    private static Texture2D LoadReadableTexture(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Missing New Run source sheet.", path);
        byte[] bytes = File.ReadAllBytes(path);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!ImageConversion.LoadImage(texture, bytes, false))
            throw new InvalidOperationException($"Unable to decode New Run source sheet: {path}");
        return texture;
    }

    private static List<ExtractedFrame> ExtractGrid(Texture2D sheet, int columns, int rows,
        int firstRowFromTop, int rowCount, int bottomCropPixels = 0)
    {
        var frames = new List<ExtractedFrame>(columns * rowCount);
        for (int rowOffset = 0; rowOffset < rowCount; rowOffset++)
        {
            int rowFromTop = firstRowFromTop + rowOffset;
            int bottom = Mathf.RoundToInt((rows - rowFromTop - 1) * sheet.height / (float)rows);
            int top = Mathf.RoundToInt((rows - rowFromTop) * sheet.height / (float)rows);
            bottom = Mathf.Min(top - 1, bottom + bottomCropPixels);
            for (int column = 0; column < columns; column++)
            {
                int left = Mathf.RoundToInt(column * sheet.width / (float)columns);
                int right = Mathf.RoundToInt((column + 1) * sheet.width / (float)columns);
                frames.Add(ExtractLargestComponent(sheet, new RectInt(left, bottom, right - left, top - bottom)));
            }
        }
        return frames;
    }

    private static ExtractedFrame ExtractLargestComponent(Texture2D sheet, RectInt slot)
    {
        Color32[] pixels = sheet.GetPixels32();
        bool[] visited = new bool[slot.width * slot.height];
        var queue = new Queue<int>();
        List<int> largest = null;

        for (int localY = 0; localY < slot.height; localY++)
        for (int localX = 0; localX < slot.width; localX++)
        {
            int localIndex = localY * slot.width + localX;
            if (visited[localIndex]) continue;
            visited[localIndex] = true;
            if (AlphaAt(pixels, sheet.width, slot.x + localX, slot.y + localY) <= AlphaThreshold) continue;

            var component = new List<int>();
            queue.Enqueue(localIndex);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                component.Add(current);
                int x = current % slot.width;
                int y = current / slot.width;

                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx;
                    int ny = y + dy;
                    if (nx < 0 || nx >= slot.width || ny < 0 || ny >= slot.height) continue;
                    int neighbor = ny * slot.width + nx;
                    if (visited[neighbor]) continue;
                    visited[neighbor] = true;
                    if (AlphaAt(pixels, sheet.width, slot.x + nx, slot.y + ny) > AlphaThreshold)
                        queue.Enqueue(neighbor);
                }
            }

            if (largest == null || component.Count > largest.Count) largest = component;
        }

        if (largest == null || largest.Count == 0)
            throw new InvalidOperationException($"No character pixels found in source slot {slot}.");

        int minX = slot.width;
        int minY = slot.height;
        int maxX = -1;
        int maxY = -1;
        foreach (int index in largest)
        {
            int x = index % slot.width;
            int y = index / slot.width;
            minX = Mathf.Min(minX, x);
            minY = Mathf.Min(minY, y);
            maxX = Mathf.Max(maxX, x);
            maxY = Mathf.Max(maxY, y);
        }

        int width = maxX - minX + 1;
        int height = maxY - minY + 1;
        var croppedPixels = new Color32[width * height];
        foreach (int index in largest)
        {
            int localX = index % slot.width;
            int localY = index / slot.width;
            croppedPixels[(localY - minY) * width + localX - minX] =
                pixels[(slot.y + localY) * sheet.width + slot.x + localX];
        }

        var cropped = new Texture2D(width, height, TextureFormat.RGBA32, false);
        cropped.SetPixels32(croppedPixels);
        cropped.Apply(false, false);
        Vector2 foot = FindFootAnchor(croppedPixels, width, height);
        float visualCenterX = FindVisualCenterX(croppedPixels, width);
        return new ExtractedFrame(cropped, foot, visualCenterX);
    }

    private static byte AlphaAt(Color32[] pixels, int width, int x, int y) => pixels[y * width + x].a;

    private static Vector2 FindFootAnchor(Color32[] pixels, int width, int height)
    {
        // Thin swords and source-sheet frame numbers can reach lower than the boots.
        // A foot row has substantially more horizontal mass than either of those marks.
        int minimumRowMass = Mathf.Max(8, width / 15);
        int footY = 0;
        int searchHeight = Mathf.Max(1, Mathf.CeilToInt(height * .4f));
        for (int y = 0; y < searchHeight; y++)
        {
            int rowMass = 0;
            for (int x = 0; x < width; x++)
                if (pixels[y * width + x].a > AlphaThreshold) rowMass++;
            if (rowMass >= minimumRowMass) { footY = y; break; }
        }

        long weightedX = 0;
        long alphaSum = 0;
        int bandTop = Mathf.Min(height, footY + Mathf.Max(10, height / 18));
        for (int y = footY; y < bandTop; y++)
        for (int x = 0; x < width; x++)
        {
            byte alpha = pixels[y * width + x].a;
            if (alpha <= AlphaThreshold) continue;
            weightedX += (long)x * alpha;
            alphaSum += alpha;
        }

        float footX = alphaSum > 0 ? weightedX / (float)alphaSum : (width - 1) * .5f;
        return new Vector2(footX, footY);
    }

    private static float FindVisualCenterX(Color32[] pixels, int width)
    {
        long weightedX = 0;
        long alphaSum = 0;
        for (int index = 0; index < pixels.Length; index++)
        {
            byte alpha = pixels[index].a;
            if (alpha <= AlphaThreshold) continue;
            weightedX += (long)(index % width) * alpha;
            alphaSum += alpha;
        }
        return alphaSum > 0 ? weightedX / (float)alphaSum : (width - 1) * .5f;
    }

    private static void WriteAlignedFrames(string direction, List<ExtractedFrame> frames)
    {
        string folder = AlignedRoot + "/" + direction;
        Directory.CreateDirectory(folder);

        int maxWidth = 1;
        int maxHeight = 1;
        foreach (ExtractedFrame frame in frames)
        {
            maxWidth = Mathf.Max(maxWidth, frame.Texture.width);
            maxHeight = Mathf.Max(maxHeight, frame.Texture.height);
        }
        float scale = Mathf.Min(TargetCharacterHeight / (float)maxHeight,
            (CanvasSize - CanvasPadding * 2) / (float)maxWidth);

        for (int index = 0; index < frames.Count; index++)
        {
            ExtractedFrame frame = frames[index];
            Texture2D resized = ResizeBilinear(frame.Texture, scale);
            var canvas = new Texture2D(CanvasSize, CanvasSize, TextureFormat.RGBA32, false);
            canvas.SetPixels32(new Color32[CanvasSize * CanvasSize]);

            // Alternating feet must not drive horizontal placement; doing so makes
            // the whole character sway left and right every step. X follows the
            // stable visual mass center while Y remains locked to the foot line.
            int offsetX = FootAnchorX - Mathf.RoundToInt(frame.VisualCenterX * scale);
            int offsetY = FootAnchorY - Mathf.RoundToInt(frame.Foot.y * scale);
            Composite(canvas, resized, offsetX, offsetY);
            canvas.Apply(false, false);

            string path = $"{folder}/{direction}_{index:00}.png";
            File.WriteAllBytes(path, canvas.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(resized);
            UnityEngine.Object.DestroyImmediate(canvas);
            UnityEngine.Object.DestroyImmediate(frame.Texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            ConfigureSpriteImporter(path);
        }
    }

    private static Texture2D ResizeBilinear(Texture2D source, float scale)
    {
        int width = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
        int height = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));
        var output = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var colors = new Color[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            colors[y * width + x] = source.GetPixelBilinear((x + .5f) / width, (y + .5f) / height);
        output.SetPixels(colors);
        output.Apply(false, false);
        return output;
    }

    private static void Composite(Texture2D canvas, Texture2D sprite, int offsetX, int offsetY)
    {
        Color[] destination = canvas.GetPixels();
        Color[] source = sprite.GetPixels();
        for (int y = 0; y < sprite.height; y++)
        for (int x = 0; x < sprite.width; x++)
        {
            int destinationX = offsetX + x;
            int destinationY = offsetY + y;
            if (destinationX < 0 || destinationX >= canvas.width || destinationY < 0 || destinationY >= canvas.height) continue;
            Color foreground = source[y * sprite.width + x];
            if (foreground.a <= 0f) continue;
            destination[destinationY * canvas.width + destinationX] = foreground;
        }
        canvas.SetPixels(destination);
    }

    private static void ConfigureSpriteImporter(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        // The source artwork is roughly half the pixel height of the shipped
        // 1024x768 action frames. PPU 50 restores the same in-world character
        // height while keeping the compact, foot-anchored 256px canvases.
        importer.spritePixelsPerUnit = SpritePixelsPerUnit;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = new Vector2(FootAnchorX / (float)CanvasSize, FootAnchorY / (float)CanvasSize);
        importer.SetTextureSettings(settings);
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.userData = BuildVersion;
        importer.SaveAndReimport();
    }

    private static AnimationClip CreateClip(string clipPath, string direction, int frameCount, float fps)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, clipPath);
        }
        clip.frameRate = fps;

        var keys = new ObjectReferenceKeyframe[frameCount];
        for (int index = 0; index < frameCount; index++)
        {
            string spritePath = $"{AlignedRoot}/{direction}/{direction}_{index:00}.png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite == null) throw new FileNotFoundException("Aligned run sprite was not imported.", spritePath);
            keys[index] = new ObjectReferenceKeyframe { time = index / fps, value = sprite };
        }

        AnimationUtility.SetObjectReferenceCurve(clip,
            EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite"), keys);
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static void MarkBuilt(params AnimationClip[] clips)
    {
        AssetDatabase.SaveAssets();
        foreach (AnimationClip clip in clips)
        {
            string path = AssetDatabase.GetAssetPath(clip);
            AssetImporter importer = AssetImporter.GetAtPath(path);
            if (importer == null) continue;
            importer.userData = BuildVersion;
            AssetDatabase.WriteImportSettingsIfDirty(path);
        }
    }

    private static void AssignClipsToController()
    {
        AssignClipsToController(
            AssetDatabase.LoadAssetAtPath<AnimationClip>(RunClipPath),
            AssetDatabase.LoadAssetAtPath<AnimationClip>(RunUpClipPath),
            AssetDatabase.LoadAssetAtPath<AnimationClip>(RunDownClipPath));
    }

    private static void AssignClipsToController(AnimationClip run, AnimationClip runUp, AnimationClip runDown)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null || run == null || runUp == null || runDown == null) return;

        bool changed = false;
        foreach (AnimatorControllerLayer layer in controller.layers)
        {
            changed |= AssignMotion(layer.stateMachine, "Run", run);
            changed |= AssignMotion(layer.stateMachine, "Run Up", runUp);
            changed |= AssignMotion(layer.stateMachine, "Run Down", runDown);
        }
        if (!changed) return;
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
    }

    private static bool AssignMotion(AnimatorStateMachine machine, string stateName, Motion motion)
    {
        bool changed = false;
        foreach (ChildAnimatorState child in machine.states)
        {
            if (child.state.name != stateName || child.state.motion == motion) continue;
            child.state.motion = motion;
            changed = true;
        }
        foreach (ChildAnimatorStateMachine child in machine.stateMachines)
            changed |= AssignMotion(child.stateMachine, stateName, motion);
        return changed;
    }

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;

    private sealed class ExtractedFrame
    {
        public readonly Texture2D Texture;
        public readonly Vector2 Foot;
        public readonly float VisualCenterX;

        public ExtractedFrame(Texture2D texture, Vector2 foot, float visualCenterX)
        {
            Texture = texture;
            Foot = foot;
            VisualCenterX = visualCenterX;
        }
    }
}
