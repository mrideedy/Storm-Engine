using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
#if UNITY_ANDROID
using UnityEditor.Android;
#endif

namespace StormEngine.EditorTools
{
    /// <summary>
    /// Release helpers for Google Play: app icons, player settings and signed builds.
    /// Menu: Storm Engine > Release. See Docs/PlayStore/CHECKLIST.md for the whole launch flow.
    /// </summary>
    public static class StormEngineRelease
    {
        const string IconDir = "Assets/StormEngine/Branding/Icons/";
        const string BuildDir = "Build";

        /// <summary>Selects the release settings asset (ad network ids, privacy policy and store links).</summary>
        [MenuItem("Storm Engine/Release Settings", priority = 190)]
        public static void SelectReleaseConfig()
        {
            var asset = AssetDatabase.LoadAssetAtPath<ReleaseConfig>("Assets/StormEngine/Resources/StormEngine/ReleaseConfig.asset");
            if (asset == null)
            {
                Debug.LogWarning("[StormEngine] ReleaseConfig.asset is missing (Assets/StormEngine/Resources/StormEngine).");
                return;
            }
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        [MenuItem("Storm Engine/Release/Apply App Icons", priority = 200)]
        public static void ApplyIconsMenu() => Debug.Log("[StormEngine] " + ApplyIcons());

        /// <summary>Imports the generated icons (Tools/marketing/make_icon.py) and assigns every Android icon slot.</summary>
        public static string ApplyIcons()
        {
            var background = Icon("icon_background");
            var foreground = Icon("icon_foreground");
            var legacy = Icon("icon_legacy");
            var round = Icon("icon_round");
            if (background == null || foreground == null || legacy == null || round == null)
                return "Icons missing: run python Tools/marketing/make_icon.py first.";

            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { legacy }, IconKind.Any);
#if UNITY_ANDROID
            var android = NamedBuildTarget.Android;
            var adaptive = PlayerSettings.GetPlatformIcons(android, AndroidPlatformIconKind.Adaptive);
            foreach (var icon in adaptive) icon.SetTextures(background, foreground);
            PlayerSettings.SetPlatformIcons(android, AndroidPlatformIconKind.Adaptive, adaptive);
            var rounds = PlayerSettings.GetPlatformIcons(android, AndroidPlatformIconKind.Round);
            foreach (var icon in rounds) icon.SetTexture(round);
            PlayerSettings.SetPlatformIcons(android, AndroidPlatformIconKind.Round, rounds);
            var legacies = PlayerSettings.GetPlatformIcons(android, AndroidPlatformIconKind.Legacy);
            foreach (var icon in legacies) icon.SetTexture(legacy);
            PlayerSettings.SetPlatformIcons(android, AndroidPlatformIconKind.Legacy, legacies);
#endif
            AssetDatabase.SaveAssets();
            return "App icons applied (adaptive, round, legacy).";
        }

        static Texture2D Icon(string name)
        {
            string path = IconDir + name + ".png";
            if (!File.Exists(path)) return null;
            AssetDatabase.ImportAsset(path);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                bool dirty = importer.textureType != TextureImporterType.Default || importer.mipmapEnabled
                    || importer.textureCompression != TextureImporterCompression.Uncompressed || !importer.alphaIsTransparency
                    || importer.npotScale != TextureImporterNPOTScale.None || importer.maxTextureSize != 1024;
                if (dirty)
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.mipmapEnabled = false;
                    importer.alphaIsTransparency = true;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.maxTextureSize = 1024;
                    importer.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        [MenuItem("Storm Engine/Release/Apply Release Settings", priority = 201)]
        public static void ApplySettingsMenu() => Debug.Log("[StormEngine] " + ApplySettings());

        /// <summary>
        /// Player settings for a Google Play release: IL2CPP + ARM64 (Play requires 64-bit), target API
        /// "automatic" (the newest installed, currently 36), no Unity splash (optional since Unity 6),
        /// cheap logging (no stack traces for plain logs and warnings) and native crash symbols for Play Console.
        /// </summary>
        public static string ApplySettings()
        {
            var android = NamedBuildTarget.Android;
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            if (PlayerSettings.Android.minSdkVersion < AndroidSdkVersions.AndroidApiLevel25)
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.backgroundColor = new Color(0.34f, 0.72f, 0.98f);
            PlayerSettings.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            PlayerSettings.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
            PlayerSettings.SetStackTraceLogType(LogType.Error, StackTraceLogType.ScriptOnly);
            PlayerSettings.SetStackTraceLogType(LogType.Assert, StackTraceLogType.ScriptOnly);
            PlayerSettings.SetStackTraceLogType(LogType.Exception, StackTraceLogType.ScriptOnly);
            PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.Minimal);
#if UNITY_ANDROID
            // The symbol table travels inside the bundle so Play Console shows readable native crash reports;
            // Google Play leaves it out of what players download.
            UserBuildSettings.DebugSymbols.level = Unity.Android.Types.DebugSymbolLevel.SymbolTable;
            UserBuildSettings.DebugSymbols.format = Unity.Android.Types.DebugSymbolFormat.IncludeInBundle;
#endif
            AssetDatabase.SaveAssets();
            return "Release settings applied: " + PlayerSettings.GetApplicationIdentifier(android) + " " + PlayerSettings.bundleVersion
                + " (" + PlayerSettings.Android.bundleVersionCode + "), IL2CPP ARM64, target API auto, splash off.";
        }

        /// <summary>Raises the version for the next upload (Play rejects a version code it has seen before).</summary>
        [MenuItem("Storm Engine/Release/Bump Version Code", priority = 220)]
        public static void BumpVersionCode()
        {
            PlayerSettings.Android.bundleVersionCode++;
            AssetDatabase.SaveAssets();
            Debug.Log("[StormEngine] Version code is now " + PlayerSettings.Android.bundleVersionCode + " (" + PlayerSettings.bundleVersion + ").");
        }

        [MenuItem("Storm Engine/Release/Build Test APK (release, debug-signed)", priority = 240)]
        public static void BuildTestApkMenu() => Debug.Log("[StormEngine] " + BuildTestApk());

        /// <summary>
        /// A non-development APK signed with the debug key: install it with USB (adb install) to test
        /// real performance on a phone. Not accepted by Google Play.
        /// </summary>
        public static string BuildTestApk()
        {
            string path = Path.Combine(BuildDir, "StormEngine-" + PlayerSettings.bundleVersion + "-test.apk");
            return Build(path, false);
        }

        [MenuItem("Storm Engine/Release/Build Google Play Bundle (AAB)", priority = 241)]
        public static void BuildBundleMenu() => Debug.Log("[StormEngine] " + BuildPlayBundle());

        /// <summary>
        /// The upload for Google Play: an Android App Bundle signed with your upload key. Set the key
        /// in Project Settings > Player > Android > Publishing Settings (Custom Keystore) first; the
        /// passwords can also come from the STORM_KEYSTORE_PASS and STORM_KEY_PASS environment variables.
        /// </summary>
        public static string BuildPlayBundle()
        {
            if (!PlayerSettings.Android.useCustomKeystore || string.IsNullOrEmpty(PlayerSettings.Android.keystoreName))
                return "No upload key: set a custom keystore in Player Settings > Android > Publishing Settings (see Docs/PlayStore/CHECKLIST.md).";
            string store = Environment.GetEnvironmentVariable("STORM_KEYSTORE_PASS");
            string key = Environment.GetEnvironmentVariable("STORM_KEY_PASS");
            if (!string.IsNullOrEmpty(store)) PlayerSettings.Android.keystorePass = store;
            if (!string.IsNullOrEmpty(key)) PlayerSettings.Android.keyaliasPass = key;
            if (string.IsNullOrEmpty(PlayerSettings.Android.keystorePass) || string.IsNullOrEmpty(PlayerSettings.Android.keyaliasPass))
                return "Enter the keystore and key passwords in Publishing Settings (Unity forgets them when it restarts).";
            string path = Path.Combine(BuildDir, "StormEngine-" + PlayerSettings.bundleVersion + "-" + PlayerSettings.Android.bundleVersionCode + ".aab");
            return Build(path, true);
        }

        static string Build(string path, bool appBundle)
        {
            if (EditorApplication.isPlaying) return "Stop Play mode first.";
            Directory.CreateDirectory(BuildDir);
            ApplySettings();
            bool previousBundle = EditorUserBuildSettings.buildAppBundle;
            try
            {
                EditorUserBuildSettings.buildAppBundle = appBundle;
                var options = new BuildPlayerOptions
                {
                    scenes = Scenes(),
                    locationPathName = path,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.None
                };
                var report = BuildPipeline.BuildPlayer(options);
                var s = report.summary;
                if (s.result != BuildResult.Succeeded) return "Build " + s.result + " with " + s.totalErrors + " errors. See the Console.";
                // summary.totalSize counts every build artifact (symbols, IL2CPP sources); report the file itself.
                long bytes = File.Exists(path) ? new FileInfo(path).Length : (long)s.totalSize;
                return "Built " + path + " (" + (bytes / 1e6).ToString("0.0") + " MB) in " + s.totalTime.TotalMinutes.ToString("0.0") + " min.";
            }
            finally
            {
                EditorUserBuildSettings.buildAppBundle = previousBundle;
            }
        }

        static string[] Scenes()
        {
            var list = new System.Collections.Generic.List<string>();
            foreach (var s in EditorBuildSettings.scenes)
                if (s.enabled) list.Add(s.path);
            return list.ToArray();
        }
    }
}
