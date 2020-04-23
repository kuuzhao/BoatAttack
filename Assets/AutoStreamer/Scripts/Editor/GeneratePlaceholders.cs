using System;
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

        foreach (var scenePath in scenePaths)
        {
            ICollection<string> assetPaths = AssetDatabase.GetDependencies(scenePath, true);
            foreach(var assetPath in assetPaths)
            {
                System.Type type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                if (type == typeof(Texture2D))
                {
                    string relativePathToAssets = assetPath.Substring(assetPath.IndexOf('/') + 1);
                    string fileExtension = Path.GetExtension(relativePathToAssets);
                    string relativePathToAssetsWoExt = relativePathToAssets.Substring(0, relativePathToAssets.Length - fileExtension.Length);
                    string newPath = string.Format("Assets/Placeholders/{0}{1}", relativePathToAssetsWoExt+"-0", fileExtension);

                    if (!File.Exists(newPath))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(newPath));
                        // The import settings are also duplicated in this case.
                        AssetDatabase.CopyAsset(assetPath, newPath);

                        TextureImporter texImporter = TextureImporter.GetAtPath(newPath) as TextureImporter;
                        texImporter.maxTextureSize = 32;

                        TextureImporterPlatformSettings tips = texImporter.GetPlatformTextureSettings("Standalone");
                        if (tips.overridden)
                        {
                            tips.maxTextureSize = 32;
                            texImporter.SetPlatformTextureSettings(tips);
                        }
                        
                        AssetDatabase.ImportAsset(newPath, ImportAssetOptions.ForceUpdate);

                        Debug.Log("Generate Placeholder: " + newPath);
                    }
                }
            }
        }

        Debug.Log("DoneGeneratePlaceholders");
    }
}
