using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

/// <summary>Creates a fresh opening-video identity for every full or incremental player build.</summary>
public sealed class OpeningVideoBuildStamp : IPreprocessBuildWithReport
{
    private const string StampAssetPath = "Assets/Resources/Build/OpeningVideoBuildStamp.txt";

    public int callbackOrder => -1000;

    public void OnPreprocessBuild(BuildReport report)
    {
        string directory = Path.GetDirectoryName(StampAssetPath);
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

        File.WriteAllText(StampAssetPath, Guid.NewGuid().ToString("N"), new UTF8Encoding(false));
        AssetDatabase.ImportAsset(
            StampAssetPath,
            ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
    }
}
