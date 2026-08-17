#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;

/// <summary>
/// Makes player builds use the same saved scene data currently visible in the editor.
/// </summary>
public sealed class SaveScenesBeforePlayerBuild : IPreprocessBuildWithReport
{
    public int callbackOrder => int.MinValue;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new BuildFailedException("Exit Play Mode before building so scene changes can be saved correctly.");

        if (!EditorSceneManager.SaveOpenScenes())
            throw new BuildFailedException("Failed to save open scenes. The player build was cancelled to avoid using stale spawn positions.");

        AssetDatabase.SaveAssets();
    }
}
#endif
