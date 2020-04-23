using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public class ReplaceMaterial
{
    static void ReplaceMaterialForGORecursively(GameObject go, Material mat)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
        {
            Debug.Log(renderer.name);
            int matCnt = renderer.sharedMaterials.Length;
            var matArr = new Material[matCnt];
            for (int i = 0; i < matCnt; ++i)
                matArr[i] = mat;

            renderer.sharedMaterials = matArr;
        }

        if (go.transform.childCount > 0)
        {
            for (int i = 0; i < go.transform.childCount; ++i)
                ReplaceMaterialForGORecursively(go.transform.GetChild(i).gameObject, mat);
        }
    }

    [MenuItem("AutoStreamer/ReplaceMaterial")]
    static void DoReplaceMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath("Assets/AutoStreamer/Materials/White.mat", typeof(Material)) as Material;

        Debug.Log("AutoStreamer/ReplaceMaterial");

        for (int i = 0; i < SceneManager.sceneCount; ++i)
        {
            var scene = SceneManager.GetSceneAt(i);
            Debug.Log(scene.path);

            var gos = scene.GetRootGameObjects();
            foreach(var go in gos)
            {
                // Debug.Log(go.name);
                ReplaceMaterialForGORecursively(go, mat);
            }

            EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
