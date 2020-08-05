using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AutoStreamer
{

    public class AsSceneAssetVisitor
    {
	    public object userData;
	
	    public void VisitAllAssets(System.Type assetType, Action<int, string, object> lambda)
        {
            var bsScenes = EditorBuildSettings.scenes;
            List<string> scenePaths = new List<string>();
            for (int i = 0; i < bsScenes.Length; ++i)
            {
                var bsScene = bsScenes[i];
                scenePaths.Add(bsScene.path);
            }

            for (int i = 0; i < scenePaths.Count; ++i)
            {
                string scenePath = scenePaths[i];

                EditorUtility.DisplayProgressBar("AutoStreamer", "Process scene: "+ scenePath, (float)i / scenePaths.Count);

                ICollection<string> assetPaths = AssetDatabase.GetDependencies(scenePath, true);
                foreach (var assetPath in assetPaths)
                {
                    System.Type type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                    if (type == assetType)
                    {
                        lambda(i, assetPath, userData);
                    }
                }
            }
        }
    }

    public class AsAssetBundleVisitor
    {
        public object userData;

        public void VisitAllAssets(System.Type assetType, Action<int, string, object> lambda)
        {
            var abs = AssetDatabase.GetAllAssetBundleNames();

            for (int i = 0; i < abs.Length; ++i)
            {
                string abName = abs[i];
                EditorUtility.DisplayProgressBar("AutoStreamer", "Process AssetBundle: "+ abName, (float)i / abs.Length);

                foreach(var assetPath1 in AssetDatabase.GetAssetPathsFromAssetBundle(abName))
                {
                    ICollection<string> assetPaths = AssetDatabase.GetDependencies(assetPath1, true);
                    foreach (var assetPath in assetPaths)
                    {
                        System.Type type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                        if (type == assetType)
                        {
                            lambda(-1, assetPath, userData);
                        }
                    }
                }
            }
        }
    }

} // AutoStreamer