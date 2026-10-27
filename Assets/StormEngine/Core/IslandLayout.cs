using System;
using System.Collections.Generic;

namespace StormEngine.Core
{
    /// <summary>
    /// Everything needed to draw a player's island: style, size, machine levels and decorations.
    /// Published as public data so other players can visit, and used for the built-in showcases.
    /// </summary>
    public sealed class IslandLayout
    {
        public const int FormatVersion = 1;

        public string Name;
        public string Title;
        public int Level = 1;
        public string Skin;
        public string Cloud;
        public string Bolt;
        public string World;
        public float Radius = 9.5f;
        /// <summary>Machine levels by machine id (only built machines are drawn).</summary>
        public readonly Dictionary<string, int> Machines = new Dictionary<string, int>();
        public readonly List<PlacedDecor> Decor = new List<PlacedDecor>();
        public double BestTournament;

        public static IslandLayout FromGame(Game g, string name)
        {
            var c = g.C;
            var l = new IslandLayout
            {
                Name = string.IsNullOrEmpty(name) ? "Storm Chaser" : name,
                Title = g.EquippedCosmetics[(int)CosmeticSlot.Title],
                Level = g.Level,
                Skin = g.Equipped(CosmeticSlot.Skin)?.Id,
                Cloud = g.Equipped(CosmeticSlot.Cloud)?.Id,
                Bolt = g.Equipped(CosmeticSlot.Bolt)?.Id,
                World = g.Equipped(CosmeticSlot.World)?.Id,
                Radius = g.IslandRadius,
                BestTournament = g.Tournament.AllTimeBest
            };
            for (int i = 0; i < c.Machines.Length; i++)
                if (g.Build.Machines[i] > 0) l.Machines[c.Machines[i].Id] = g.Build.Machines[i];
            foreach (var p in g.Placed) l.Decor.Add(new PlacedDecor { Id = p.Id, X = p.X, Z = p.Z, Rot = p.Rot });
            return l;
        }

        public int MachineLevel(string id) => Machines.TryGetValue(id, out int level) ? level : 0;

        public string ToJson()
        {
            var w = new JsonWriter().BeginObject();
            w.Prop("v", FormatVersion).Prop("name", Name).Prop("title", Title).Prop("level", Level)
                .Prop("skin", Skin).Prop("cloud", Cloud).Prop("bolt", Bolt).Prop("world", World)
                .Prop("radius", Math.Round(Radius * 100) / 100).Prop("best", BestTournament);
            w.Key("machines").BeginObject();
            foreach (var kv in Machines) w.Prop(kv.Key, kv.Value);
            w.EndObject();
            w.Key("decor").BeginArray();
            foreach (var d in Decor)
                w.BeginArray().Value(d.Id).Value(Math.Round(d.X * 100) / 100).Value(Math.Round(d.Z * 100) / 100).Value(Math.Round(d.Rot)).EndArray();
            w.EndArray();
            return w.EndObject().ToString();
        }

        /// <summary>Parses and sanitizes a layout. Unknown items fall back to defaults; returns null if unusable.</summary>
        public static IslandLayout FromJson(string json, Content c)
        {
            if (string.IsNullOrEmpty(json) || !JsonReader.TryParse(json, out var root) || !(root is Dictionary<string, object> o)) return null;
            string Known(string id, CosmeticSlot slot)
            {
                int i = c.CosmeticIndex(id);
                return i >= 0 && c.Cosmetics[i].Slot == slot ? id : null;
            }
            var l = new IslandLayout
            {
                Name = Clip(NameFilter.Display(JsonReader.Str(o, "name"), null) ?? L.T("Storm Chaser"), 24),
                Title = Known(JsonReader.Str(o, "title"), CosmeticSlot.Title),
                Level = Math.Max(1, Math.Min(c.B.MaxEngineLevel, (int)JsonReader.Num(o, "level", 1))),
                Skin = Known(JsonReader.Str(o, "skin"), CosmeticSlot.Skin),
                Cloud = Known(JsonReader.Str(o, "cloud"), CosmeticSlot.Cloud),
                Bolt = Known(JsonReader.Str(o, "bolt"), CosmeticSlot.Bolt),
                World = Known(JsonReader.Str(o, "world"), CosmeticSlot.World),
                BestTournament = Math.Max(0, JsonReader.Num(o, "best")),
            };
            float maxRadius = c.B.BaseIslandRadius;
            foreach (var e in c.Expansions) maxRadius = Math.Max(maxRadius, e.Radius);
            l.Radius = (float)Math.Max(c.B.BaseIslandRadius, Math.Min(maxRadius, JsonReader.Num(o, "radius", c.B.BaseIslandRadius)));

            var machines = JsonReader.Obj(o, "machines");
            if (machines != null)
                foreach (var kv in machines)
                    if (c.MachineIndex(kv.Key) >= 0 && kv.Value is double d) l.Machines[kv.Key] = (int)Math.Max(0, Math.Min(100000, d));

            var decor = JsonReader.Arr(o, "decor");
            int capacity = c.B.BaseDecorCapacity;
            foreach (var e in c.Expansions) capacity += e.Capacity;
            if (decor != null)
                foreach (var item in decor)
                {
                    if (l.Decor.Count >= capacity) break;
                    if (!(item is List<object> a) || a.Count < 4 || !(a[0] is string id) || c.DecorationIndex(id) < 0) continue;
                    float x = (float)(a[1] is double dx ? dx : 0), z = (float)(a[2] is double dz ? dz : 0);
                    if (float.IsNaN(x) || float.IsNaN(z) || Math.Abs(x) > 40 || Math.Abs(z) > 40) continue;
                    l.Decor.Add(new PlacedDecor { Id = id, X = x, Z = z, Rot = (float)(a[3] is double r ? r : 0) });
                }
            return l;
        }

        static string Clip(string s, int max) => s.Length > max ? s.Substring(0, max) : s;
    }
}
