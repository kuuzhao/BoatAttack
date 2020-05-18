using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GenerateAutoStreamerData
{
    const string kEditorPlaceholdersDir = "Assets/AutoStreamerData/Placeholders";
    const string kOutputABsDir = "Assets/AutoStreamerData/ABs";

    [MenuItem("AutoStreamer/Generate AutoStreamerData")]
    static void DoGenerateAutoStreamerData()
    {
        var bsScenes = EditorBuildSettings.scenes;
        if (bsScenes.Length < 1)
            return;

        List<string> scenePaths = new List<string>();
        for (int i = 0; i < bsScenes.Length; ++i)
        {
            var bsScene = bsScenes[i];
            scenePaths.Add(bsScene.path);
        }

        string projectRootFolder = Directory.GetParent(Application.dataPath).FullName;
        string autoStreamerDataFolder = Path.Combine(projectRootFolder, "Assets/AutoStreamerData");
        if (Directory.Exists(autoStreamerDataFolder))
        {
            EditorUtility.DisplayDialog("Error", "Please delete Assets/AutoStreamerData first", "Ok");
            return;
        }

        AssetDatabase.Refresh();

        List<AssetBundleBuild> abs = new List<AssetBundleBuild>();
        List<string> tex2DAssets = new List<string>();

        for (int i = 0; i < scenePaths.Count; ++i)
        {
            string scenePath = scenePaths[i];
            ICollection<string> assetPaths = AssetDatabase.GetDependencies(scenePath, true);
            foreach (var assetPath in assetPaths)
            {
                string placeholderPath = GetPlaceholderAssetPath(assetPath);
                if (File.Exists(placeholderPath))
                    continue;

                bool needPlaceholder = false;
                System.Type type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                if (type == typeof(Texture2D))
                {
                    if (!tex2DAssets.Contains(assetPath))
                    {
                        tex2DAssets.Add(assetPath);
                        needPlaceholder = true;
                    }
                }
                // else if (type == typeof(Mesh))

                if (needPlaceholder)
                {
                    // Generate an AssetBundle for the original asset which can be downloaded at runtime.
                    AssetBundleBuild ab = new AssetBundleBuild();
                    ab.assetBundleName = AssetDatabase.AssetPathToGUID(assetPath) + ".abas";
                    ab.assetNames = new string[] { assetPath };
                    abs.Add(ab);
                }
            }
        }

        // Generate Asset Bundles
        if (abs.Count > 0)
            BuildAssetBundles(abs);

        // Create placeholders: Texture2D
        if (tex2DAssets.Count > 0)
            GeneratePlaceholdersForTexture2D(tex2DAssets);

        // Create placeholders: other assets

        AssetDatabase.Refresh();

        AssetDatabase.Refresh();
        Debug.Log("Done Generate AutoStreamerData");
    }

    static string GetPlaceholderAssetPath(string assetPath)
    {
        string fileExtension = Path.GetExtension(assetPath);
        string assetPathWoExt = assetPath.Substring(0, assetPath.Length - fileExtension.Length);
        string newPath = string.Format("{0}/{1}{2}", kEditorPlaceholdersDir, assetPathWoExt + "-0", fileExtension);
        return newPath;
    }

    static void GeneratePlaceholdersForTexture2D(List<string> tex2DAssets)
    {
        foreach (var tex2DAsset in tex2DAssets)
        {
            string placeholderPath = GetPlaceholderAssetPath(tex2DAsset);

            Directory.CreateDirectory(Path.GetDirectoryName(placeholderPath));
            // The import settings are also duplicated in this case.
            AssetDatabase.CopyAsset(tex2DAsset, placeholderPath);

            TextureImporter texImporter = TextureImporter.GetAtPath(placeholderPath) as TextureImporter;
            texImporter.maxTextureSize = 32;

            TextureImporterPlatformSettings tipsStandalone = texImporter.GetPlatformTextureSettings("Standalone");
            if (tipsStandalone.overridden)
            {
                tipsStandalone.maxTextureSize = 32;
                texImporter.SetPlatformTextureSettings(tipsStandalone);
            }
            TextureImporterPlatformSettings tipsAndroid = texImporter.GetPlatformTextureSettings("Android");
            if (tipsAndroid.overridden)
            {
                tipsAndroid.maxTextureSize = 32;
                texImporter.SetPlatformTextureSettings(tipsAndroid);
            }

            AssetDatabase.ImportAsset(placeholderPath, ImportAssetOptions.ForceUpdate);
            Debug.Log("Generate Placeholder: " + placeholderPath);
        }
    }

    static void BuildAssetBundles(List<AssetBundleBuild> abs)
    {
        string absDir = Path.Combine(kOutputABsDir, EditorUserBuildSettings.activeBuildTarget.ToString());
        Directory.CreateDirectory(absDir);
        BuildPipeline.BuildAssetBundles(absDir, abs.ToArray(), BuildAssetBundleOptions.None, EditorUserBuildSettings.activeBuildTarget);
    }

}
