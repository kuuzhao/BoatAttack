using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using UnityEngine;

using UnityEditor;
using System;
using UnityEditor.IMGUI.Controls;
using System.IO;
using UnityEngine.Profiling;
using System.Linq;

namespace AutoStreamer
{
    public class AsTextureWindow : EditorWindow {
        enum Mode
        {
            TextureAB = 0,
            SceneAB = 1,
        }

        const string kAutoStreamerRoot = "Assets/AutoStreamerData";
        const string kAsTextureConfigAssetPath = "Assets/AutoStreamerData/AsTextureConfig.asset";
        const string kOutputSceneABDir = "Assets/AutoStreamerData/SceneABs";
        const string kOutputTextureABDir = "Assets/AutoStreamerData/TextureABs";
        const string kEditorPlaceholdersDir = "Assets/AutoStreamerData/Placeholders";
        const string kAutoStreamerAbLutDir = "Assets/AutoStreamerData/autostreamerablut";

        const string kPlaceholdersABName = "Placeholders.abas";

        public AsTexturesTreeView m_TexturesView;
        public AsScenesTreeView m_SceneView;

        AsTextureConfig m_TextureConfig;
        List<AsSceneTreeDataItem> m_SceneData;

        [SerializeField] TreeViewState m_TexturesTreeViewState; // Serialized in the window layout file so it survives assembly reloading
        [SerializeField] TreeViewState m_ScenesTreeViewState;
        [SerializeField] MultiColumnHeaderState m_TexMultiColumnHeaderState;
        [SerializeField] MultiColumnHeaderState m_SceneMultiColumnHeaderState;

        [NonSerialized] bool m_SceneViewInitialized;
        [NonSerialized] bool m_TexViewInitialized;

        bool m_SceneForceRebuildAssetBundle;
        Mode m_Mode;

        Rect modeTabRect
        {
            get { return new Rect(0, 2f, position.width, 20f); }
        }

        Rect toolbar1Rect
        {
            get { return new Rect(0, modeTabRect.yMax, 
                position.width, 20f); }
        }

        Rect treeViewRect
        {
            get
            {
                return new Rect(0, toolbar1Rect.yMax,
                    position.width, position.height - toolbar1Rect.yMax);
            }
        }


	    [MenuItem("Window/Auto Streamer")]
        public static AsTextureWindow CreateWindow()
        {
            var window = GetWindow<AsTextureWindow>();
            window.titleContent = new GUIContent("Auto Streamer");
            window.Focus();
            window.Repaint();

            return window;
        }

        public List<AsTextureTreeDataItem> GetTextureData()
        {
            if (m_TextureConfig == null)
            {
                if (!File.Exists(kAsTextureConfigAssetPath))
                {
                    m_TextureConfig = ScriptableObject.CreateInstance<AsTextureConfig>();

                    Directory.CreateDirectory(kAutoStreamerRoot);
                    AssetDatabase.CreateAsset(m_TextureConfig, kAsTextureConfigAssetPath);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
                else
                {
                    m_TextureConfig = AssetDatabase.LoadAssetAtPath<AsTextureConfig>(kAsTextureConfigAssetPath);
                }
            }

            return m_TextureConfig.textureItems;
        }

        public void UpdateAndSaveData()
        {
            if (m_TextureConfig != null)
            {
                EditorUtility.SetDirty(m_TextureConfig);
                AssetDatabase.SaveAssets();
            }
        }

        void OnGUI()
        {
            InitIfNeeded();

            m_Mode = (Mode)GUILayout.Toolbar((int)m_Mode, new string[] {"Texture Streaming", "Scene Streaming"});

            switch(m_Mode)
            {
            case Mode.TextureAB:
                Toolbar1Texture(toolbar1Rect);
                m_TexturesView.OnGUI(treeViewRect);
                    break;
            case Mode.SceneAB:
                Toolbar1Scene(toolbar1Rect);
                m_SceneView.OnGUI(treeViewRect);
                    break;
            }
        }

        void InitIfNeeded()
        {
            if (!m_TexViewInitialized)
            {
                if (m_TexturesTreeViewState == null)
                    m_TexturesTreeViewState = new TreeViewState();

                bool texFirstInit = m_TexMultiColumnHeaderState == null;

                var texHeaderState = AsTexturesTreeView.CreateDefaultMultiColumnHeaderState(treeViewRect.width);
                if (MultiColumnHeaderState.CanOverwriteSerializedFields(m_TexMultiColumnHeaderState, texHeaderState))
                    MultiColumnHeaderState.OverwriteSerializedFields(m_TexMultiColumnHeaderState, texHeaderState);
                m_TexMultiColumnHeaderState = texHeaderState;

                var texMultiColumnHeader = new MyMultiColumnHeader(texHeaderState);
                if (texFirstInit)
                    texMultiColumnHeader.ResizeToFit();

                var texTreeModel = new TreeModelT<AsTextureTreeDataItem>(GetTextureData());
                m_TexturesView = new AsTexturesTreeView(m_TexturesTreeViewState, texMultiColumnHeader, texTreeModel);

                m_TexViewInitialized = true;
            }

            if (!m_SceneViewInitialized)
            {
                if (m_SceneData == null)
                {
                    m_SceneData = new List<AsSceneTreeDataItem>();
                    var root = new AsSceneTreeDataItem("Root", -1, 0);
                    m_SceneData.Add(root);
                }

                if (m_ScenesTreeViewState == null)
                    m_ScenesTreeViewState = new TreeViewState();

                bool sceneFirstInit = m_SceneMultiColumnHeaderState == null;

                var sceneHeaderState = AsScenesTreeView.CreateDefaultMultiColumnHeaderState(treeViewRect.width);
                if (MultiColumnHeaderState.CanOverwriteSerializedFields(m_SceneMultiColumnHeaderState, sceneHeaderState))
                    MultiColumnHeaderState.OverwriteSerializedFields(m_SceneMultiColumnHeaderState, sceneHeaderState);
                m_SceneMultiColumnHeaderState = sceneHeaderState;

                var sceneMultiColumnHeader = new MyMultiColumnHeader(sceneHeaderState);
                if (sceneFirstInit)
                    sceneMultiColumnHeader.ResizeToFit();

                var sceneTreeModel = new TreeModelT<AsSceneTreeDataItem>(m_SceneData);
                m_SceneView = new AsScenesTreeView(m_ScenesTreeViewState, sceneMultiColumnHeader, sceneTreeModel);

                m_SceneViewInitialized = true;
            }
        }

        void Toolbar1Texture(Rect rect)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, rect.height-4), Color.yellow);

            GUILayout.BeginArea(rect);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(5);

                var style = "miniButton";
                if (GUILayout.Button("Sync Textures", style, GUILayout.Width(100)))
                {
                    SyncTextures();
                }
                if (GUILayout.Button("Generate AssetBundles", style, GUILayout.Width(150)))
                {
                    GenerateTextureAssetBundles();
                }
                if (GUILayout.Button("Generate Placeholders", style, GUILayout.Width(140)))
                {
                    GeneratePlaceholders();
                }

                GUILayout.FlexibleSpace();

                string statusReport = "";
                if (m_TextureConfig != null)
                {
                    var placeholderItems = m_TextureConfig.textureItems.Where(x => x.usePlaceholder);
                    statusReport = string.Format("Placeholder: {0}/{1}, AB: {2}",
                        placeholderItems.Count(),
                        m_TextureConfig.textureItems.Count - 1,
                        EditorUtility.FormatBytes(placeholderItems.Select(x => x.assetBundleSize).Sum()));
                }
                GUILayout.Label(statusReport);
            }

            GUILayout.EndArea();
        }

        void Toolbar1Scene(Rect rect)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, rect.height-4), Color.yellow);

            GUILayout.BeginArea(rect);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(5);

                var style = "miniButton";
                if (GUILayout.Button("Sync Scenes", style, GUILayout.Width(90)))
                {
                    SyncScenes();
                }

                GUILayout.Space(20);
                m_SceneForceRebuildAssetBundle = GUILayout.Toggle(m_SceneForceRebuildAssetBundle, "Force Rebuild");

                if (GUILayout.Button("Generate AssetBundles", style, GUILayout.Width(150)))
                {
                    GenerateSceneAssetBundles();
                }

                GUILayout.FlexibleSpace();

                string statusReport = "";
                if (m_TextureConfig != null)
                {
                    statusReport = string.Format("Scene: {0}, AB: {1}",
                        m_SceneData.Count - 1,
                        EditorUtility.FormatBytes(m_SceneData.Select(x => x.assetBundleSize).Sum()));
                }
                GUILayout.Label(statusReport);
            }

            GUILayout.EndArea();
        }

        class SyncTexureItem
        {
            public AsTextureTreeDataItem texItem;
            public bool touched;

            public SyncTexureItem(AsTextureTreeDataItem item)
            {
                texItem = item;
                touched = false;
            }
        }

        static void SyncOneTexture(int sceneIdx, string assetPath, object userData)
        {
            Dictionary<string, SyncTexureItem> syncMap = userData as Dictionary<string, SyncTexureItem>;
            string textureABDir = Path.Combine(kOutputTextureABDir, EditorUserBuildSettings.activeBuildTarget.ToString());

            SyncTexureItem syncItem;
            if (syncMap.ContainsKey(assetPath))
            {
                syncItem = syncMap[assetPath];
            }
            else
            {
                syncItem = new SyncTexureItem(new AsTextureTreeDataItem(assetPath, 0, 0));
                syncMap.Add(assetPath, syncItem);
            }

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);

            // TODO: LZ: 
            //      expose TextureUtil.GetStorageMemorySizeLong
            syncItem.texItem.runtimeMemory = (int)(Profiler.GetRuntimeMemorySizeLong(tex) / 2);
            syncItem.texItem.width = tex.width;
            syncItem.texItem.height = tex.height;
            syncItem.texItem.refScenes.Add(sceneIdx);

            var placeholderAssetPath = AssetPathToPlaceholderPath(assetPath);
            syncItem.texItem.placehoderAssetPath = (File.Exists(placeholderAssetPath) ? placeholderAssetPath : null);

            string abPath = Path.Combine(textureABDir, AssetDatabase.AssetPathToGUID(assetPath) + ".abas");
            if (File.Exists(abPath))
            {
                syncItem.texItem.assetBundleSize = (int)(new FileInfo(abPath).Length);
            }
            else
            {
                syncItem.texItem.assetBundleSize = 0;
            }

            syncItem.touched = true;
        }

        void SyncTextures()
        {
            var texItems = GetTextureData();

            Dictionary<string, SyncTexureItem> syncMap = new Dictionary<string, SyncTexureItem>();
            foreach(var item in texItems)
            {
                item.refScenes.Clear();
                syncMap.Add(item.assetPath, new SyncTexureItem(item));
            }

            // 1. process scenes
            AsSceneAssetVisitor texVisitor = new AsSceneAssetVisitor();
            texVisitor.userData = syncMap;
            texVisitor.VisitAllAssets(typeof(Texture2D), SyncOneTexture);

            // 2. process asset bundles
            AsAssetBundleVisitor abVisitor = new AsAssetBundleVisitor();
            abVisitor.userData = syncMap;
            abVisitor.VisitAllAssets(typeof(Texture2D), SyncOneTexture);

            texItems.Clear();
            texItems.Add(new AsTextureTreeDataItem("Root", -1, 0));
            foreach(var syncItem in syncMap)
            {
                if (syncItem.Value.touched)
                {
                    syncItem.Value.texItem.id = texItems.Count;
                    texItems.Add(syncItem.Value.texItem);
                }
            }

            EditorUtility.ClearProgressBar();

            UpdateAndSaveData();

            m_TexViewInitialized = false;
        }

        void SyncScenes()
        {
            string sceneABDir = Path.Combine(kOutputSceneABDir, EditorUserBuildSettings.activeBuildTarget.ToString());

            m_SceneData.Clear();
            m_SceneData.Add(new AsSceneTreeDataItem("Root", -1, 0));

            List<string> scenesList = new List<string>();
            EditorBuildSettingsScene[] editorScenes = EditorBuildSettings.scenes;
            foreach (EditorBuildSettingsScene scene in editorScenes)
            {
                if (scene.enabled)
                    scenesList.Add(scene.path);
            }

            foreach(var scenePath in scenesList)
            {
                var sceneItem = new AsSceneTreeDataItem(scenePath, 0, m_SceneData.Count);

                string abPath = Path.Combine(sceneABDir, AssetDatabase.AssetPathToGUID(scenePath) + ".abas");
                if (File.Exists(abPath))
                {
                    sceneItem.assetBundleSize = (int)(new FileInfo(abPath).Length);
                }

                m_SceneData.Add(sceneItem);
            }

            m_SceneViewInitialized = false;
        }

        List<string> GetExistingPlaceholders()
        {
            List<string> placeholderAssetPaths = new List<string>();
            string [] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[]{kEditorPlaceholdersDir});
            foreach(var textureGuid in textureGuids)
            {
                placeholderAssetPaths.Add(AssetDatabase.GUIDToAssetPath(textureGuid));
            }
            return placeholderAssetPaths;
        }

        void GeneratePlaceholders()
        {
            var texItems = GetTextureData();

            Dictionary<string, SyncTexureItem> syncMap = new Dictionary<string, SyncTexureItem>();
            foreach(var item in texItems)
            {
                item.refScenes.Clear();
                syncMap.Add(item.assetPath, new SyncTexureItem(item));
            }

            // 1. Delete
            List<string> existingPlaceholders = GetExistingPlaceholders();
            foreach(var placeholderPath in existingPlaceholders)
            {
                string assetPath = PlaceholderPathToAssetPath(placeholderPath);
                if (syncMap.ContainsKey(assetPath))
                {
                    var syncItem = syncMap[assetPath];

                    if (syncItem.texItem.usePlaceholder)
                    {
                        syncItem.touched = true;
                        continue;
                    }
                }

                AssetDatabase.DeleteAsset(placeholderPath);
                Debug.Log(String.Format("DeleteAsset({0})", placeholderPath));
            }

            // 2. Generate
            List<string> toGeneratePlaceholders = new List<string>();
            foreach(var kv in syncMap)
            {
                var syncItem = kv.Value;
                if (syncItem.texItem.usePlaceholder && !syncItem.touched)
                {
                    toGeneratePlaceholders.Add(syncItem.texItem.assetPath);
                }
            }
            GeneratePlaceholdersForTexture2D(toGeneratePlaceholders);

            // 3. Update m_TextureConfig
            foreach(var texItem in texItems)
            {
                var placeholderAssetPath = AssetPathToPlaceholderPath(texItem.assetPath);
                if (File.Exists(placeholderAssetPath))
                {
                    texItem.placehoderAssetPath = placeholderAssetPath;
                }
                else
                {
                    texItem.placehoderAssetPath = null;
                }
            }
            UpdateAndSaveData();
        }

        List<string> GetExistingAssetBundles()
        {
            List<string> abPaths = new List<string>();
            string [] assetGuids = AssetDatabase.FindAssets("", new[]{Path.Combine(kOutputTextureABDir, EditorUserBuildSettings.activeBuildTarget.ToString())});
            foreach(var assetGuid in assetGuids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);

                if (assetPath.EndsWith(".abas"))
                {
                    abPaths.Add(assetPath);
                }
            }
            return abPaths;
        }

        void GenerateSceneAssetBundles()
        {
            // back up
            bool oldUseAutoStreamer = PlayerSettings.autoStreamer;

            // Disable AutoStreamer when building AssetBundles for the original textures.
            PlayerSettings.autoStreamer = true;

            List<AssetBundleBuild> abs = new List<AssetBundleBuild>();

            // placeholders AB
            {
                var texItems = GetTextureData();
                List<string> placeholderPaths = new List<string>();
                foreach(var texItem in texItems)
                {
                    if (texItem.usePlaceholder)
                    {
                        placeholderPaths.Add(texItem.assetPath);
                    }
                }
                AssetBundleBuild ab = new AssetBundleBuild();
                ab.assetNames = placeholderPaths.ToArray();
                ab.assetBundleName = kPlaceholdersABName;
                abs.Add(ab);
            }

            // scenes ABs
            List<string> sceneList = new List<string>();
            EditorBuildSettingsScene[] editorScenes = EditorBuildSettings.scenes;
            foreach (EditorBuildSettingsScene scene in editorScenes)
            {
                if (scene.enabled)
                    sceneList.Add(scene.path);
            }

            Directory.CreateDirectory(kAutoStreamerAbLutDir);
            List<string> sceneABLines = new List<string>();
            sceneABLines.Add("scenes");
            sceneABLines.Add((sceneList.Count * 2).ToString());
            foreach (var scenePath in sceneList)
            {
                AssetBundleBuild ab = new AssetBundleBuild();
                ab.assetBundleName = AssetDatabase.AssetPathToGUID(scenePath) + ".abas";
                ab.assetNames = new string[] { scenePath };

                sceneABLines.Add(scenePath);
                sceneABLines.Add(AssetDatabase.AssetPathToGUID(scenePath));

                abs.Add(ab);
            }
            File.WriteAllText(Path.Combine(kAutoStreamerAbLutDir, "scenes"), string.Join("\n", sceneABLines.ToArray()));

            string sceneABDir = Path.Combine(kOutputSceneABDir, EditorUserBuildSettings.activeBuildTarget.ToString());
            Directory.CreateDirectory(sceneABDir);
            BuildPipeline.BuildAssetBundles(sceneABDir, abs.ToArray(),
                m_SceneForceRebuildAssetBundle ? BuildAssetBundleOptions.ForceRebuildAssetBundle : BuildAssetBundleOptions.None,
                EditorUserBuildSettings.activeBuildTarget);

            // restore
            PlayerSettings.autoStreamer = oldUseAutoStreamer;

            AssetDatabase.Refresh();

            SyncScenes();
        }

        void GenerateTextureAssetBundles()
        {
            var texItems = GetTextureData();

            Dictionary<string, AsTextureTreeDataItem> syncMap = new Dictionary<string, AsTextureTreeDataItem>();
            foreach(var item in texItems)
            {
                item.refScenes.Clear();
                syncMap.Add(AssetDatabase.AssetPathToGUID(item.assetPath), item);
            }

            //////////////////////////////////////////////////////////////////
            // 1. delete
            List<string> existingABPaths = GetExistingAssetBundles();
            foreach(var abPath in existingABPaths)
            {
                string abGuid = Path.GetFileNameWithoutExtension(abPath);
                if (syncMap.ContainsKey(abGuid) && syncMap[abGuid].usePlaceholder)
                    continue;

                AssetDatabase.DeleteAsset(abPath);

                string manifestPath = abPath+".manifest";
                if (File.Exists(manifestPath))
                {
                    AssetDatabase.DeleteAsset(manifestPath);
                }
            }

            //////////////////////////////////////////////////////////////////
            // 2. generate
            // back up
            bool oldUseAutoStreamer = PlayerSettings.autoStreamer;

            // Disable AutoStreamer when building AssetBundles for the original textures.
            PlayerSettings.autoStreamer = false;

            List<AssetBundleBuild> abs = new List<AssetBundleBuild>();
            foreach(var item in texItems)
            {
                if (item.usePlaceholder)
                {
                    // Generate an AssetBundle for the original asset which can be downloaded at runtime.
                    AssetBundleBuild ab = new AssetBundleBuild();
                    ab.assetBundleName = AssetDatabase.AssetPathToGUID(item.assetPath) + ".abas";
                    ab.assetNames = new string[] { item.assetPath };
                    abs.Add(ab);
                }
            }

            if (abs.Count > 0)
            {
                string absDir = Path.Combine(kOutputTextureABDir, EditorUserBuildSettings.activeBuildTarget.ToString());
                Directory.CreateDirectory(absDir);
                BuildPipeline.BuildAssetBundles(absDir, abs.ToArray(), BuildAssetBundleOptions.None, EditorUserBuildSettings.activeBuildTarget);
            }

            // restore
            PlayerSettings.autoStreamer = oldUseAutoStreamer;

            AssetDatabase.Refresh();

            SyncTextures();
        }

        static string AssetPathToPlaceholderPath(string assetPath)
        {
            string fileExtension = Path.GetExtension(assetPath);
            string assetPathWoExt = assetPath.Substring(0, assetPath.Length - fileExtension.Length);
            string newPath = string.Format("{0}/{1}{2}", kEditorPlaceholdersDir, assetPathWoExt + "-0", fileExtension);
            return newPath;
        }

        static string PlaceholderPathToAssetPath(string placeholderPath)
        {
            string fileExtension = Path.GetExtension(placeholderPath);
            string assetPathWoExt = placeholderPath.Substring(0, placeholderPath.Length - fileExtension.Length - 2);
            assetPathWoExt = assetPathWoExt.Substring((kEditorPlaceholdersDir + "/").Length);
            string newPath = string.Format("{0}{1}", assetPathWoExt, fileExtension);
            return newPath;
        }

        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode)]
        static extern bool CreateHardLink(
        string lpFileName,
        string lpExistingFileName,
        IntPtr lpSecurityAttributes
        );

        static void GeneratePlaceholdersForTexture2D(List<string> tex2DAssets)
        {
            float i = 0;
            foreach (var tex2DAsset in tex2DAssets)
            {
                string originalAssetFullPath = Path.GetFullPath(tex2DAsset);
                EditorUtility.DisplayProgressBar("AutoStreamer", "Generate assets: " + i+"/"+ tex2DAssets.Count, i++ / tex2DAssets.Count);
                string placeholderPath = AssetPathToPlaceholderPath(tex2DAsset);

                Directory.CreateDirectory(Path.GetDirectoryName(placeholderPath));
                // The import settings are also duplicated in this case.
                //AssetDatabase.CopyAsset(tex2DAsset, placeholderPath);
                CreateHardLink(placeholderPath, originalAssetFullPath, IntPtr.Zero);
            }
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();

            i = 0;
            foreach (var tex2DAsset in tex2DAssets)
            {
                EditorUtility.DisplayProgressBar("AutoStreamer", "Resize generated assets: " + i + "/" + tex2DAssets.Count, i++ / tex2DAssets.Count);

                string placeholderPath = AssetPathToPlaceholderPath(tex2DAsset);

                TextureImporter sourceImporter = TextureImporter.GetAtPath(tex2DAsset) as TextureImporter;

                TextureImporter texImporter = TextureImporter.GetAtPath(placeholderPath) as TextureImporter;
                EditorUtility.CopySerialized(sourceImporter, texImporter);

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
            EditorUtility.ClearProgressBar();
        }
    }
} 