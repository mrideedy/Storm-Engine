using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace StormEngine.EditorTools
{
    /// <summary>
    /// Builds the trailer recorder: a Windows copy of the game with the STORM_TRAILER define, which
    /// adds Runtime/Trailer/TrailerDirector.cs. Run it with Unity closed:
    /// <c>Unity.exe -batchmode -quit -projectPath &lt;project&gt; -executeMethod StormEngine.EditorTools.TrailerBuild.Build -trailerOut &lt;folder&gt;</c>.
    /// The Windows defines and scripting backend are put back afterwards and the active platform is
    /// switched back to Android, so release builds are unaffected.
    /// </summary>
    public static class TrailerBuild
    {
        public static void Build()
        {
            string output = Arg("-trailerOut") ?? Path.GetFullPath("Build/Trailer");
            var standalone = NamedBuildTarget.Standalone;
            string defines = PlayerSettings.GetScriptingDefineSymbols(standalone);
            var backend = PlayerSettings.GetScriptingBackend(standalone);
            bool ok = false;
            try
            {
                var symbols = defines.Split(';').Where(s => s.Length > 0).ToList();
                if (!symbols.Contains("STORM_TRAILER")) symbols.Add("STORM_TRAILER");
                PlayerSettings.SetScriptingDefineSymbols(standalone, string.Join(";", symbols));
                PlayerSettings.SetScriptingBackend(standalone, ScriptingImplementation.Mono2x);
                Directory.CreateDirectory(output);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                    locationPathName = Path.Combine(output, "StormEngine.exe"),
                    target = BuildTarget.StandaloneWindows64,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = BuildOptions.None
                });
                ok = report.summary.result == BuildResult.Succeeded;
                Debug.Log("[Trailer] build " + report.summary.result + " (" + report.summary.totalErrors + " errors) -> " + output);
            }
            catch (Exception e)
            {
                Debug.LogError("[Trailer] build failed: " + e);
            }
            finally
            {
                PlayerSettings.SetScriptingDefineSymbols(standalone, defines);
                PlayerSettings.SetScriptingBackend(standalone, backend);
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                AssetDatabase.SaveAssets();
            }
            if (!ok) EditorApplication.Exit(1);
        }

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
