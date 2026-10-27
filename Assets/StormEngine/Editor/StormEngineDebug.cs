using System;
using System.Reflection;
using StormEngine.Core;
using UnityEditor;
using UnityEngine;

namespace StormEngine.EditorTools
{
    /// <summary>Play-mode helpers for testing progression quickly (menu: Storm Engine/Debug).</summary>
    public static class StormEngineDebug
    {
        static Game G => StormApp.Instance != null ? StormApp.Instance.Game : null;

        [MenuItem("Storm Engine/Debug/Grant 1M Energy + 100 Cores + 20 Research", priority = 100)]
        public static void GrantMenu() => Grant(1e6, 100, 20);

        [MenuItem("Storm Engine/Debug/Fill Storm Charge", priority = 101)]
        public static void FillCharge()
        {
            if (G == null) return;
            G.StormCharge = 1;
        }

        [MenuItem("Storm Engine/Debug/Simulate 10 Minutes", priority = 102)]
        public static void SimulateMenu() => Simulate(600, true);

        /// <summary>
        /// Plays with a separate, offline save ("storm_save.test.json") so tests never touch the real save
        /// or its cloud data. Lasts until "Use My Real Save" or an Editor restart.
        /// </summary>
        [MenuItem("Storm Engine/Debug/Use Test Save (offline)", priority = 120)]
        public static void UseTestSave() => SetTestProfile("test");

        [MenuItem("Storm Engine/Debug/Use My Real Save", priority = 121)]
        public static void UseRealSave() => SetTestProfile("");

        [MenuItem("Storm Engine/Debug/Use Test Save (offline)", true)]
        static bool CanUseTestSave() => !EditorApplication.isPlaying && !SaveSystem.IsTestProfile;

        [MenuItem("Storm Engine/Debug/Use My Real Save", true)]
        static bool CanUseRealSave() => !EditorApplication.isPlaying && SaveSystem.IsTestProfile;

        public static void SetTestProfile(string name)
        {
            SaveSystem.TestProfile = name;
            Debug.Log(string.IsNullOrEmpty(name)
                ? "[StormEngine] Play mode uses your real save again."
                : "[StormEngine] Play mode now uses the offline test save " + SaveSystem.CurrentPath + ". Your real save is untouched.");
        }

        public static void Grant(double energy, long cores, long research)
        {
            if (G == null) return;
            G.Energy += energy;
            G.RunEnergy += energy;
            G.LifetimeEnergy += energy;
            G.Cores += cores;
            G.Research += research;
            G.ResearchLifetime += research;
        }

        /// <summary>Fast-forwards the game with a simple greedy player (buys the best-value machine).</summary>
        public static string Simulate(double seconds, bool autoBuy)
        {
            var g = G;
            if (g == null) return "not playing";
            var c = g.C;
            double t = 0;
            double nextBuy = 0;
            while (t < seconds)
            {
                if (autoBuy && t >= nextBuy && !g.IsStorming)
                {
                    nextBuy = t + 2;
                    for (int guard = 0; guard < 100; guard++)
                    {
                        int best = -1;
                        double bestValue = 0;
                        for (int m = 0; m < c.Machines.Length; m++)
                        {
                            if (!g.MachineUnlocked[m] || g.IsMaxed(m) || g.CostOf(m, 1) > g.Energy) continue;
                            double v = c.Machines[m].Id == "chamber" ? (g.CostOf(m, 1) < g.Energy * 0.05 ? 1 : 0) : g.MachineValue(m);
                            if (v > bestValue)
                            {
                                bestValue = v;
                                best = m;
                            }
                        }
                        if (best < 0) break;
                        g.BuyMachine(best, 1);
                    }
                    for (int m = 0; m < c.Modules.Length; m++)
                    {
                        if (!g.IsModuleUnlocked(m) && g.ModuleRequirementsMet(m) && g.Cores >= c.Modules[m].UnlockCost) g.UnlockModule(m);
                        if (g.Build.IsEquipped(m) && g.CanUpgradeModule(m)) g.UpgradeModule(m);
                    }
                }
                if (g.CanUnleash)
                {
                    for (int b = 0; b < c.Boosts.Length; b++)
                        if (g.IsBoostReady(b)) g.ActivateBoost(b);
                    g.Unleash();
                }
                g.Tick(0.25);
                t += 0.25;
            }
            return $"Simulated {seconds}s: SP {Num.Format(g.StormPower)}, energy {Num.Format(g.Energy)}, levels [{string.Join(",", g.Build.Machines)}]";
        }

        public static void Open(string tab) => StormApp.Instance?.Ui?.Open(tab);

        public static int CloseAllPopups()
        {
            var app = StormApp.Instance;
            if (app == null) return 0;
            var root = app.GetComponent<UnityEngine.UIElements.UIDocument>().rootVisualElement;
            int closed = 0;
            for (int i = 0; i < 100; i++)
            {
                var p = UnityEngine.UIElements.UQueryExtensions.Q<StormEngine.UI.Popup>(root);
                if (p == null) break;
                p.Close();
                closed++;
            }
            return closed;
        }

        public static void Screenshot(string path) => ScreenCapture.CaptureScreenshot(path);

        /// <summary>Simulates a notch, punch-hole camera or gesture bar in the Editor (pixels). ClearSafeArea goes back to the device value.</summary>
        public static void SetSafeAreaInsets(float left, float top, float right, float bottom) =>
            StormEngine.UI.SafeArea.DebugInsets = new Vector4(left, top, right, bottom);

        public static void ClearSafeArea() => StormEngine.UI.SafeArea.DebugInsets = null;

        /// <summary>
        /// Selects (adding if needed) a fixed Game view resolution, e.g. 1080x2340 to preview phones.
        /// Returns the previously selected size index so it can be restored.
        /// </summary>
        public static int SetGameViewSize(int width, int height)
        {
            var asm = typeof(Editor).Assembly;
            var sizesType = asm.GetType("UnityEditor.GameViewSizes");
            var single = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var instance = single.GetProperty("instance").GetValue(null, null);
            // The Game view lists the sizes of the active build target's group (Android, Standalone...).
            var current = sizesType.GetProperty("currentGroupType")?.GetValue(instance, null);
            var group = sizesType.GetMethod("GetGroup").Invoke(instance, new[] { current ?? GameViewSizeGroupType.Standalone });
            var groupType = group.GetType();
            int total = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
            int index = -1;
            for (int i = 0; i < total; i++)
            {
                var size = groupType.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                var st = size.GetType();
                if ((int)st.GetProperty("width").GetValue(size, null) == width && (int)st.GetProperty("height").GetValue(size, null) == height)
                {
                    index = i;
                    break;
                }
            }
            if (index < 0)
            {
                var sizeType = asm.GetType("UnityEditor.GameViewSize");
                var kindType = asm.GetType("UnityEditor.GameViewSizeType");
                var ctor = sizeType.GetConstructor(new[] { kindType, typeof(int), typeof(int), typeof(string) });
                var created = ctor.Invoke(new object[] { Enum.ToObject(kindType, 1), width, height, "Storm " + width + "x" + height });
                groupType.GetMethod("AddCustomSize").Invoke(group, new[] { created });
                index = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null) - 1;
            }
            return SelectGameViewSize(index);
        }

        /// <summary>Selects a Game view size by index and returns the previous index.</summary>
        public static int SelectGameViewSize(int index)
        {
            var gameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
            var window = EditorWindow.GetWindow(gameViewType, false, null, false);
            var prop = gameViewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            int previous = (int)prop.GetValue(window, null);
            prop.SetValue(window, index, null);
            window.Repaint();
            return previous;
        }

        /// <summary>Jumps to an Engine Level (unlocking everything up to it) for testing.</summary>
        public static void SetLevel(int level)
        {
            if (G == null) return;
            G.SetProgress(level, 0);
            G.Refresh();
        }

        /// <summary>Owns and equips a cosmetic by id, ignoring requirements.</summary>
        public static bool Wear(string id)
        {
            if (G == null) return false;
            int i = G.C.CosmeticIndex(id);
            if (i < 0) return false;
            G.OwnedCosmetics.Add(id);
            return G.EquipCosmetic(i);
        }
    }
}
