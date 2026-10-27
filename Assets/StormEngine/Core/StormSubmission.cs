using System;
using System.Collections.Generic;

namespace StormEngine.Core
{
    /// <summary>
    /// A replayable record of one storm: the frozen build, boost timers at the start and every
    /// boost activation. The server recomputes the score from this, so a client can never submit
    /// a number that its build and timing could not produce.
    /// </summary>
    public sealed class StormSubmission
    {
        public struct BoostStart
        {
            public string Id;
            public int Active;
            public int Cooldown;
        }

        public int Version;
        public int Week;
        public string Weekly;
        public readonly List<KeyValuePair<string, int>> Machines = new List<KeyValuePair<string, int>>();
        public readonly List<KeyValuePair<string, int>> Modules = new List<KeyValuePair<string, int>>();
        public readonly List<KeyValuePair<string, int>> Research = new List<KeyValuePair<string, int>>();
        public readonly List<BoostStart> Boosts = new List<BoostStart>();
        /// <summary>Equipped style item ids (their bonuses count, capped, in tournaments).</summary>
        public readonly List<string> Style = new List<string>();
        public readonly List<KeyValuePair<int, string>> Events = new List<KeyValuePair<int, string>>();
        public double Score;
        public string Class;
        public int Ticks;
        public int Strikes;
        public long Unix;

        public static string ClassId(Content c, int classIndex) => classIndex < 0 ? "squall" : c.Phenomena[classIndex].Id;
        public static string ClassName(Content c, int classIndex) => classIndex < 0 ? L.T("Squall") : c.Phenomena[classIndex].Name;

        public static StormSubmission From(Content c, StormSim sim, int week, long unix)
        {
            var b = sim.Setup.Build;
            var s = new StormSubmission
            {
                Version = c.Version,
                Week = week,
                Weekly = b.Weekly >= 0 ? c.Weekly[b.Weekly].Id : null,
                Score = sim.Result.Score,
                Class = ClassId(c, sim.Result.ClassIndex),
                Ticks = sim.Result.Ticks,
                Strikes = sim.Result.Strikes,
                Unix = unix
            };
            for (int i = 0; i < c.Machines.Length; i++)
                if (b.Machines[i] > 0) s.Machines.Add(new KeyValuePair<string, int>(c.Machines[i].Id, b.Machines[i]));
            foreach (int m in b.Equipped)
                s.Modules.Add(new KeyValuePair<string, int>(c.Modules[m].Id, b.ModuleLevels[m]));
            for (int i = 0; i < c.Research.Length; i++)
                if (b.Research[i] > 0) s.Research.Add(new KeyValuePair<string, int>(c.Research[i].Id, b.Research[i]));
            for (int i = 0; i < c.Boosts.Length; i++)
                s.Boosts.Add(new BoostStart { Id = c.Boosts[i].Id, Active = sim.Setup.BoostActiveTicks[i], Cooldown = sim.Setup.BoostCooldownTicks[i] });
            if (b.Style != null)
                foreach (int item in b.Style)
                    if (item >= 0 && item < c.Cosmetics.Length && c.Cosmetics[item].Effects.Length > 0) s.Style.Add(c.Cosmetics[item].Id);
            foreach (var e in sim.Result.Events)
                s.Events.Add(new KeyValuePair<int, string>(e.Tick, c.Boosts[e.Boost].Id));
            return s;
        }

        public string ToJson()
        {
            var w = new JsonWriter().BeginObject();
            w.Prop("v", Version).Prop("week", Week).Prop("weekly", Weekly);
            WriteLevels(w, "machines", Machines);
            WriteLevels(w, "modules", Modules);
            WriteLevels(w, "research", Research);
            w.Key("boosts").BeginArray();
            foreach (var b in Boosts) w.BeginObject().Prop("id", b.Id).Prop("a", b.Active).Prop("c", b.Cooldown).EndObject();
            w.EndArray();
            w.Key("events").BeginArray();
            foreach (var e in Events) w.BeginArray().Value(e.Key).Value(e.Value).EndArray();
            w.EndArray();
            w.Key("style").BeginArray();
            foreach (var id in Style) w.Value(id);
            w.EndArray();
            w.Prop("score", Score).Prop("cls", Class).Prop("ticks", Ticks).Prop("strikes", Strikes).Prop("t", Unix);
            return w.EndObject().ToString();
        }

        static void WriteLevels(JsonWriter w, string key, List<KeyValuePair<string, int>> levels)
        {
            w.Key(key).BeginObject();
            foreach (var kv in levels) w.Prop(kv.Key, kv.Value);
            w.EndObject();
        }

        public static StormSubmission FromJson(string json)
        {
            var root = JsonReader.Parse(json) as Dictionary<string, object>;
            if (root == null) throw new FormatException("Submission is not an object");
            var s = new StormSubmission
            {
                Version = (int)JsonReader.Num(root, "v"),
                Week = (int)JsonReader.Num(root, "week"),
                Weekly = JsonReader.Str(root, "weekly"),
                Score = JsonReader.Num(root, "score"),
                Class = JsonReader.Str(root, "cls"),
                Ticks = (int)JsonReader.Num(root, "ticks"),
                Strikes = (int)JsonReader.Num(root, "strikes"),
                Unix = (long)JsonReader.Num(root, "t")
            };
            ReadLevels(root, "machines", s.Machines);
            ReadLevels(root, "modules", s.Modules);
            ReadLevels(root, "research", s.Research);
            var boosts = JsonReader.Arr(root, "boosts");
            if (boosts != null)
                foreach (var o in boosts)
                    s.Boosts.Add(new BoostStart
                    {
                        Id = JsonReader.Str(o, "id"),
                        Active = (int)JsonReader.Num(o, "a"),
                        Cooldown = (int)JsonReader.Num(o, "c")
                    });
            var style = JsonReader.Arr(root, "style");
            if (style != null)
                foreach (var o in style)
                    if (o is string id) s.Style.Add(id);
            var events = JsonReader.Arr(root, "events");
            if (events != null)
                foreach (var o in events)
                    if (o is List<object> pair && pair.Count == 2 && pair[0] is double t && pair[1] is string id)
                        s.Events.Add(new KeyValuePair<int, string>((int)t, id));
            return s;
        }

        static void ReadLevels(Dictionary<string, object> root, string key, List<KeyValuePair<string, int>> into)
        {
            var o = JsonReader.Obj(root, key);
            if (o == null) return;
            foreach (var kv in o)
                if (kv.Value is double d) into.Add(new KeyValuePair<string, int>(kv.Key, (int)d));
        }

        /// <summary>Rebuilds the storm start state. Returns false with a reason for malformed data.</summary>
        public bool TryCreateSetup(Content c, out StormSetup setup, out List<StormEvent> events, out string error)
        {
            setup = null;
            events = null;
            var build = new Build(c);

            foreach (var kv in Machines)
            {
                int i = c.MachineIndex(kv.Key);
                if (i < 0) { error = "unknown machine " + kv.Key; return false; }
                var def = c.Machines[i];
                if (kv.Value < 0 || kv.Value > 100000 || (def.MaxLevel > 0 && kv.Value > def.MaxLevel)) { error = "bad level for " + kv.Key; return false; }
                build.Machines[i] = kv.Value;
            }
            foreach (var kv in Research)
            {
                int i = c.ResearchIndex(kv.Key);
                if (i < 0) { error = "unknown research " + kv.Key; return false; }
                if (kv.Value < 0 || kv.Value > c.Research[i].MaxLevel) { error = "bad level for " + kv.Key; return false; }
                build.Research[i] = kv.Value;
            }
            foreach (var kv in Modules)
            {
                int i = c.ModuleIndex(kv.Key);
                if (i < 0) { error = "unknown module " + kv.Key; return false; }
                if (kv.Value < 1 || kv.Value > c.Modules[i].MaxLevel) { error = "bad level for " + kv.Key; return false; }
                if (build.Equipped.Contains(i)) { error = "duplicate module " + kv.Key; return false; }
                build.ModuleLevels[i] = kv.Value;
                build.Equipped.Add(i);
            }

            int weekly = WeekClock.ConditionIndex(Week, c.Weekly.Length);
            string expected = weekly >= 0 ? c.Weekly[weekly].Id : null;
            if (Weekly != expected) { error = "weekly condition does not match week"; return false; }
            build.Weekly = weekly;

            // Tournament rules: style bonuses count, but capped.
            build.StyleCap = c.B.TournamentStyleCap;
            foreach (var id in Style)
            {
                int i = c.CosmeticIndex(id);
                if (i < 0) { error = "unknown style " + id; return false; }
                int slot = (int)c.Cosmetics[i].Slot;
                if (build.Style[slot] >= 0) { error = "two styles in one slot"; return false; }
                build.Style[slot] = i;
            }

            var probe = ModifierSet.Create(c, build);
            if (build.Equipped.Count > probe.ModuleSlots) { error = "too many modules"; return false; }

            setup = new StormSetup
            {
                Build = build,
                BoostActiveTicks = new int[c.Boosts.Length],
                BoostCooldownTicks = new int[c.Boosts.Length]
            };
            foreach (var b in Boosts)
            {
                int i = c.BoostIndex(b.Id);
                if (i < 0) { error = "unknown boost " + b.Id; return false; }
                int maxActive = StormSim.TicksOf(probe.BoostDuration[i]);
                int maxCooldown = StormSim.TicksOf(probe.BoostCooldown[i]);
                if (b.Active < 0 || b.Active > maxActive + 1 || b.Cooldown < 0 || b.Cooldown > maxCooldown + 1)
                {
                    error = "bad boost timer " + b.Id;
                    return false;
                }
                if (b.Active > 0 && !probe.BoostUnlocked[i]) { error = "locked boost " + b.Id; return false; }
                // Every tournament run starts with fresh boosts.
                if (b.Active != 0 || b.Cooldown != 0) { error = "tournament runs start with fresh boosts"; return false; }
            }

            events = new List<StormEvent>();
            foreach (var e in Events)
            {
                int i = c.BoostIndex(e.Value);
                if (i < 0 || e.Key < 0) { error = "bad event"; return false; }
                events.Add(new StormEvent(e.Key, i));
            }

            error = null;
            return true;
        }
    }

    public static class StormValidator
    {
        public const double Tolerance = 1e-6;

        /// <summary>Replays a submission exactly like the Cloud Code validator does.</summary>
        public static bool Validate(Content c, StormSubmission s, out StormResult result, out string error)
        {
            result = null;
            if (s.Version != c.Version) { error = "content version mismatch"; return false; }
            if (!s.TryCreateSetup(c, out var setup, out var events, out error)) return false;
            result = StormSim.Replay(c, setup, events, out bool legal);
            if (!legal) { error = "illegal boost timing"; return false; }
            double diff = Math.Abs(result.Score - s.Score) / Math.Max(1, Math.Abs(s.Score));
            if (diff > Tolerance) { error = "score mismatch"; return false; }
            if (StormSubmission.ClassId(c, result.ClassIndex) != s.Class) { error = "class mismatch"; return false; }
            error = null;
            return true;
        }

        /// <summary>Compact build description stored with leaderboard entries ("Build View").</summary>
        public static string Summary(Content c, Build build, StormResult r)
        {
            var ms = ModifierSet.Create(c, build);
            var snap = new Snapshot(c);
            Formula.Evaluate(c, ms, new EvalContext(c.Boosts.Length), snap);
            var w = new JsonWriter().BeginObject();
            w.Prop("cls", StormSubmission.ClassId(c, r.ClassIndex));
            w.Key("cat").BeginArray();
            for (int i = 0; i < Content.CatCount; i++) w.Value(Math.Round(snap.CatValue(i) * 100) / 100);
            w.EndArray();
            w.Key("mods").BeginArray();
            foreach (int m in build.Equipped) w.Value(c.Modules[m].Id);
            w.EndArray();
            w.Key("phen").BeginArray();
            for (int p = 0; p < r.PhenoSeen.Length; p++) if (r.PhenoSeen[p]) w.Value(c.Phenomena[p].Id);
            w.EndArray();
            w.Prop("wk", build.Weekly >= 0 ? c.Weekly[build.Weekly].Id : null);
            w.Prop("chain", Math.Round(r.PeakChain * 100) / 100);
            w.Prop("strikes", r.Strikes);
            w.Prop("sec", r.Seconds);
            return w.EndObject().ToString();
        }
    }
}
