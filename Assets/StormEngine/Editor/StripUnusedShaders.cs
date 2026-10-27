using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Rendering;
using UnityEngine;

namespace StormEngine.EditorTools
{
    /// <summary>
    /// Keeps shaders the game never uses out of player builds. The Unity AI tools in this project
    /// (com.unity.ai.inference, "Sentis") keep about 29 MB of neural-network shaders in a Resources
    /// folder, and Unity ships everything in Resources folders. The Editor keeps the package working;
    /// only the compiled variants in player builds are dropped, so the assets become tiny stubs.
    /// </summary>
    sealed class StripUnusedShaders : IPreprocessShaders, IPreprocessComputeShaders, IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        const string SentisPackage = "Packages/com.unity.ai.inference/";

        static readonly Dictionary<int, bool> Unused = new();
        static int _stripped;

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            Unused.Clear();
            _stripped = 0;
        }

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data) => Strip(shader, data);

        public void OnProcessComputeShader(ComputeShader shader, string kernelName, IList<ShaderCompilerData> data) => Strip(shader, data);

        static void Strip(Object shader, IList<ShaderCompilerData> data)
        {
            if (shader == null || data.Count == 0 || !IsUnused(shader)) return;
            _stripped += data.Count;
            data.Clear();
        }

        static bool IsUnused(Object shader)
        {
            int id = shader.GetInstanceID();
            if (!Unused.TryGetValue(id, out bool unused))
            {
                string path = AssetDatabase.GetAssetPath(shader);
                unused = path != null && path.StartsWith(SentisPackage);
                Unused[id] = unused;
            }
            return unused;
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (_stripped > 0) Debug.Log("[StormEngine] Left " + _stripped + " unused Sentis shader variants out of the build.");
        }
    }
}
