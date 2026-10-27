namespace StormEngine.Core
{
    /// <summary>
    /// Serializes everything the storm replay needs into JSON. The editor embeds this into the
    /// Cloud Code validator so the server always scores storms with the same numbers as the client.
    /// </summary>
    public static class ContentExport
    {
        public static string ToJson(Content c)
        {
            var w = new JsonWriter().BeginObject();
            w.Prop("version", c.Version);

            var b = c.B;
            w.Key("balance").BeginObject()
                .Prop("lightningPerElecLog", b.LightningPerElecLog)
                .Prop("cloudStabilizerPerLog", b.CloudStabilizerPerLog)
                .Prop("anomalyThreshold", b.AnomalyThreshold)
                .Prop("anomalyBase", b.AnomalyBase)
                .Prop("anomalyPerLevel", b.AnomalyPerLevel);
            IntArray(w, "megaCounts", b.MegaCounts);
            NumArray(w, "megaMults", b.MegaMults);
            IntArray(w, "perfectCounts", b.PerfectCounts);
            NumArray(w, "perfectMults", b.PerfectMults);
            w.EndObject();

            w.Key("stats").BeginArray();
            foreach (var s in c.Stats)
                w.BeginObject().Prop("id", s.Id.ToString()).Prop("base", s.Base).Prop("phase", s.Phase.ToString()).EndObject();
            w.EndArray();

            IntArray(w, "milestones", c.MilestoneLevels);

            w.Key("machines").BeginArray();
            foreach (var m in c.Machines)
            {
                w.BeginObject().Prop("id", m.Id).Prop("maxLevel", m.MaxLevel)
                    .Prop("milestoneStat", m.MilestoneStat.ToString()).Prop("milestoneMult", m.MilestoneMult);
                Effects(w, m.Effects);
                w.EndObject();
            }
            w.EndArray();

            w.Key("boosts").BeginArray();
            foreach (var bd in c.Boosts)
            {
                w.BeginObject().Prop("id", bd.Id).Prop("target", bd.Target.ToString()).Prop("factor", bd.Factor)
                    .Prop("duration", bd.Duration).Prop("cooldown", bd.Cooldown)
                    .Prop("unlockMachine", bd.UnlockMachine).Prop("unlockLevel", bd.UnlockLevel)
                    .Prop("extraStat", bd.ExtraStat.ToString()).Prop("extraFactor", bd.ExtraFactor)
                    .EndObject();
            }
            w.EndArray();

            w.Key("phenomena").BeginArray();
            foreach (var p in c.Phenomena)
            {
                w.BeginObject().Prop("id", p.Id).Prop("stat", p.Stat.ToString()).Prop("baseMult", p.BaseMult)
                    .Prop("alpha", p.Alpha).Prop("perk", p.Perk.ToString()).Prop("perkValue", p.PerkValue).Prop("special", p.Special);
                w.Key("ingredients").BeginArray();
                foreach (var ing in p.Ingredients)
                    w.BeginObject().Prop("cat", (long)ing.Cat).Prop("threshold", ing.Threshold).EndObject();
                w.EndArray();
                w.EndObject();
            }
            w.EndArray();

            w.Key("modules").BeginArray();
            foreach (var m in c.Modules)
            {
                w.BeginObject().Prop("id", m.Id).Prop("maxLevel", m.MaxLevel);
                Effects(w, m.Effects);
                w.EndObject();
            }
            w.EndArray();

            w.Key("research").BeginArray();
            foreach (var r in c.Research)
            {
                w.BeginObject().Prop("id", r.Id).Prop("maxLevel", r.MaxLevel);
                Effects(w, r.Effects);
                w.EndObject();
            }
            w.EndArray();

            w.Key("weekly").BeginArray();
            foreach (var wk in c.Weekly)
            {
                w.BeginObject().Prop("id", wk.Id);
                Effects(w, wk.Effects);
                w.EndObject();
            }
            w.EndArray();

            w.Key("tournament").BeginObject().Prop("styleCap", b.TournamentStyleCap).EndObject();

            w.Key("styles").BeginArray();
            foreach (var cos in c.Cosmetics)
            {
                if (cos.Effects.Length == 0) continue;
                w.BeginObject().Prop("id", cos.Id).Prop("slot", (long)cos.Slot);
                Effects(w, cos.Effects);
                w.EndObject();
            }
            w.EndArray();

            return w.EndObject().ToString();
        }

        static void Effects(JsonWriter w, EffectDef[] effects)
        {
            w.Key("effects").BeginArray();
            foreach (var e in effects)
            {
                w.BeginObject()
                    .Prop("stat", e.Stat.ToString())
                    .Prop("op", e.Op.ToString())
                    .Prop("mode", e.Mode.ToString())
                    .Prop("base", e.Base)
                    .Prop("per", e.PerLevel)
                    .Prop("from", e.From.ToString())
                    .Prop("cond", e.Cond.ToString())
                    .Prop("condIndex", e.CondIndex)
                    .EndObject();
            }
            w.EndArray();
        }

        static void IntArray(JsonWriter w, string key, int[] values)
        {
            w.Key(key).BeginArray();
            foreach (var v in values) w.Value(v);
            w.EndArray();
        }

        static void NumArray(JsonWriter w, string key, double[] values)
        {
            w.Key(key).BeginArray();
            foreach (var v in values) w.Value(v);
            w.EndArray();
        }
    }
}
