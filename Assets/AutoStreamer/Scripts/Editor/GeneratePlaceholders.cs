using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class GeneratePlaceholders
{
    [MenuItem("AutoStreamer/GeneratePlaceholdersForScenes")]
    // Update is called once per frame
    static void DoGeneratePlaceholders()
    {
        if (Selection.assetGUIDs.Length == 0)
        {
            Debug.LogWarning("Please select unity scenes.");
            return;
        }

        List<string> scenePaths = new List<string>();
        for (int i = 0; i < Selection.assetGUIDs.Length; ++i)
        {
            string guid = Selection.assetGUIDs[i];
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (!assetPath.EndsWith(".unity"))
            {
                Debug.LogWarning("Please select unity scenes.");
                return;
            }
            scenePaths.Add(assetPath);
        }

        List<AssetBundleBuild> abs = new List<AssetBundleBuild>();
        List<string> tex2DAssets = new List<string>();

        foreach (var scenePath in scenePaths)
        {
            ICollection<string> assetPaths = AssetDatabase.GetDependencies(scenePath, true);
            foreach(var assetPath in assetPaths)
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

        Debug.Log("DoneGeneratePlaceholders");
    }

    static string GetPlaceholderAssetPath(string assetPath)
    {
        string fileExtension = Path.GetExtension(assetPath);
        string assetPathWoExt = assetPath.Substring(0, assetPath.Length - fileExtension.Length);
        string newPath = string.Format("Assets/Placeholders/{0}{1}", assetPathWoExt + "-0", fileExtension);
        return newPath;
    }

    static void GeneratePlaceholdersForTexture2D(List<string> tex2DAssets)
    {
        foreach(var tex2DAsset in tex2DAssets)
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
        string absDir = "Assets/Placeholders/ABs/" + EditorUserBuildSettings.activeBuildTarget.ToString();
        Directory.CreateDirectory(absDir);
        BuildPipeline.BuildAssetBundles(absDir, abs.ToArray(), BuildAssetBundleOptions.None, EditorUserBuildSettings.activeBuildTarget);
    }
}
