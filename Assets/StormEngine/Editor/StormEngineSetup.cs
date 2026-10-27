using System.IO;
using System.Text.RegularExpressions;
using StormEngine.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace StormEngine.EditorTools
{
    /// <summary>Editor utilities: scene setup, Cloud Code content export and save management.</summary>
    public static class StormEngineSetup
    {
        const string Root = "Assets/StormEngine";
        const string ResourcesDir = Root + "/Resources/StormEngine";
        const string PanelPath = ResourcesDir + "/StormPanelSettings.asset";
        const string ThemePath = ResourcesDir + "/StormTheme.tss";
        const string ScenePath = Root + "/Scenes/StormEngine.unity";
        const string CloudCodePath = Root + "/Server/CloudCode/SubmitStorm.js";
        const string RendererPath = Root + "/Settings/StormRenderer.asset";
        const string ContentStart = "/*<content>*/";
        const string ContentEnd = "/*</content>*/";

        [MenuItem("Storm Engine/Setup Game Scene", priority = 0)]
        public static void SetupScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureRenderPipeline();
            var panel = EnsurePanelSettings();
            CreateScene(panel);
        }

        /// <summary>Non-interactive variant used by automation.</summary>
        public static string SetupSceneSilently()
        {
            EnsureRenderPipeline();
            var panel = EnsurePanelSettings();
            CreateScene(panel);
            return ScenePath;
        }

        /// <summary>
        /// The diorama needs the 3D Universal Renderer (real-time lights, shadows, post-processing).
        /// Projects created from the 2D template use the 2D Renderer, so switch the active URP asset
        /// to a Universal Renderer and tune it for a small, sharp cartoon scene.
        /// </summary>
        [MenuItem("Storm Engine/Configure Render Pipeline", priority = 1)]
        public static string EnsureRenderPipeline()
        {
            var asset = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (asset == null) asset = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            if (asset == null) return "No URP asset is assigned in Graphics settings.";

            var so = new SerializedObject(asset);
            var list = so.FindProperty("m_RendererDataList");
            var current = list.arraySize > 0 ? list.GetArrayElementAtIndex(0).objectReferenceValue : null;
            string log = "";
            if (!(current is UniversalRendererData))
            {
                var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
                if (renderer == null)
                {
                    EnsureFolder(Path.GetDirectoryName(RendererPath).Replace(Path.DirectorySeparatorChar, '/'));
                    const string builtin = "Assets/UniversalRenderer.asset";
                    if (AssetDatabase.LoadAssetAtPath<UniversalRendererData>(builtin) == null)
                    {
                        AssetDatabase.DeleteAsset(builtin);
                        asset.LoadBuiltinRendererData(RendererType.UniversalRenderer);
                        AssetDatabase.SaveAssets();
                    }
                    string moved = AssetDatabase.MoveAsset(builtin, RendererPath);
                    if (!string.IsNullOrEmpty(moved)) Debug.LogWarning("[StormEngine] " + moved);
                    renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
                    if (renderer == null) renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(builtin);
                }
                if (renderer == null) return "Could not create a Universal Renderer asset.";
                so.Update();
                list = so.FindProperty("m_RendererDataList");
                if (list.arraySize == 0) list.arraySize = 1;
                list.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
                so.FindProperty("m_DefaultRendererIndex").intValue = 0;
                log += "Switched to the Universal Renderer. ";
            }
            so.FindProperty("m_SoftShadowsSupported").boolValue = true;
            so.FindProperty("m_MainLightShadowsSupported").boolValue = true;
            so.FindProperty("m_MainLightShadowmapResolution").intValue = 2048;
            so.FindProperty("m_ShadowDistance").floatValue = 80f;
            so.FindProperty("m_ShadowCascadeCount").intValue = 2;
            so.FindProperty("m_Cascade2Split").floatValue = 0.35f;
            so.FindProperty("m_MSAA").intValue = 4;
            so.FindProperty("m_SupportsHDR").boolValue = true;
            so.FindProperty("m_RenderScale").floatValue = 1f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            log += "URP asset tuned (MSAA 4x, HDR, soft shadows).";
            Debug.Log("[StormEngine] " + log);
            return log;
        }

        static void CreateScene(PanelSettings panel)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = false;
            cam.fieldOfView = 30;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.backgroundColor = new Color(0.25f, 0.66f, 0.93f);
            camGo.transform.SetPositionAndRotation(new Vector3(0.7f, 12.5f, -25f), Quaternion.Euler(23, 0, 0));
            camGo.AddComponent<AudioListener>();
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;

            var appGo = new GameObject("Storm Engine");
            var doc = appGo.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            var app = appGo.AddComponent<StormApp>();
            var so = new SerializedObject(app);
            so.FindProperty("document").objectReferenceValue = doc;
            so.FindProperty("panelSettings").objectReferenceValue = panel;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[StormEngine] Scene created at " + ScenePath);
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace(Path.DirectorySeparatorChar, '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        public static PanelSettings EnsurePanelSettings()
        {
            Directory.CreateDirectory(ResourcesDir);
            if (!File.Exists(ThemePath))
            {
                File.WriteAllText(ThemePath, "@import url(\"unity-theme://default\");\n");
                AssetDatabase.ImportAsset(ThemePath);
            }
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);

            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, PanelPath);
            }
            panel.themeStyleSheet = theme;
            panel.scaleMode = PanelScaleMode.ConstantPixelSize;
            panel.scale = 1f;
            panel.sortingOrder = 0;
            EditorUtility.SetDirty(panel);
            AssetDatabase.SaveAssets();
            return panel;
        }

        [MenuItem("Storm Engine/Export Content To Cloud Code", priority = 20)]
        public static void ExportCloudCode()
        {
            string result = ExportCloudCodeSilently();
            EditorUtility.DisplayDialog("Storm Engine", result, "OK");
        }

        /// <summary>Embeds the current balance data into the Cloud Code validator.</summary>
        public static string ExportCloudCodeSilently()
        {
            if (!File.Exists(CloudCodePath)) return "Cloud Code script not found at " + CloudCodePath;
            string js = File.ReadAllText(CloudCodePath);
            int a = js.IndexOf(ContentStart, System.StringComparison.Ordinal);
            int b = js.IndexOf(ContentEnd, System.StringComparison.Ordinal);
            if (a < 0 || b < a) return "Content markers not found in " + CloudCodePath;
            string json = ContentExport.ToJson(Content.CreateDefault());
            string updated = js.Substring(0, a + ContentStart.Length) + json + js.Substring(b);
            if (updated == js) return "Cloud Code content is already up to date.";
            File.WriteAllText(CloudCodePath, updated);
            AssetDatabase.ImportAsset(CloudCodePath);
            return "Cloud Code content updated (content version " + Content.CreateDefault().Version + "). Deploy SubmitStorm.js again.";
        }

        [MenuItem("Storm Engine/Open Save Folder", priority = 40)]
        public static void OpenSaveFolder() => EditorUtility.RevealInFinder(Application.persistentDataPath);

        [MenuItem("Storm Engine/Delete Local Save", priority = 41)]
        public static void DeleteSave()
        {
            if (!EditorUtility.DisplayDialog("Storm Engine", "Delete the local save file?", "Delete", "Cancel")) return;
            SaveSystem.Delete();
            Debug.Log("[StormEngine] Local save deleted.");
        }

        public static bool IsCloudCodeContentCurrent()
        {
            if (!File.Exists(CloudCodePath)) return false;
            var m = Regex.Match(File.ReadAllText(CloudCodePath), Regex.Escape(ContentStart) + "(.*?)" + Regex.Escape(ContentEnd), RegexOptions.Singleline);
            return m.Success && m.Groups[1].Value == ContentExport.ToJson(Content.CreateDefault());
        }
    }
}
