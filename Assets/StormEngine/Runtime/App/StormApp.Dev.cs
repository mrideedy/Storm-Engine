using StormEngine.Core;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using StormEngine.Audio;

namespace StormEngine
{
    /// <summary>
    /// Developer cheats for testing. Compiled only in the Editor and Development builds, so a release
    /// build (Development Build unchecked) leaves all of this out automatically.
    /// </summary>
    public sealed partial class StormApp
    {
        /// <summary>Max Engine Level, every machine, module, style, bundle, decoration, expansion and track, plus currency.</summary>
        public void DevUnlockEverything()
        {
            var g = Game;
            var c = Content;
            if (g.IsStorming)
            {
                Sfx.Deny();
                Ui.Toast("storm", L.T("Wait for the storm to finish first."));
                return;
            }
            g.SetProgress(c.B.MaxEngineLevel, 0);
            for (int m = 0; m < g.MachineUnlocked.Length; m++) g.MachineUnlocked[m] = true;
            for (int p = 0; p < g.Discovered.Length; p++) g.Discovered[p] = true;
            for (int m = 0; m < c.Modules.Length; m++)
                if (g.Build.ModuleLevels[m] <= 0) g.Build.ModuleLevels[m] = 1;
            foreach (var cos in c.Cosmetics) g.OwnedCosmetics.Add(cos.Id);
            foreach (var b in c.Bundles) g.BundleAds[b.Id] = b.AdsRequired;
            foreach (var d in c.Decorations)
                if (g.DecorCopies(d.Id) <= 0) g.DecorOwned[d.Id] = 1;
            foreach (var e in c.Expansions) g.Expansions.Add(e.Id);
            foreach (var t in MusicLibrary.Tracks)
                if (!Save.musicOwned.Contains(t.Id)) Save.musicOwned.Add(t.Id);
            foreach (var w in c.Weekly)
                if (!Save.weeklySeen.Contains(w.Id)) Save.weeklySeen.Add(w.Id);
            g.Gems += 100000;
            g.Cores += 100000;
            g.Research += 10000;
            g.SyncStyle();
            g.SyncDecor();
            g.MarkDirty();
            g.Refresh();
            SaveNow(false);
            Sfx.Discovery();
            Ui.Toast("star", L.T("Developer: everything unlocked (+100K Gems, +100K Cores, +10K Research)."));
        }
    }
}
#endif
