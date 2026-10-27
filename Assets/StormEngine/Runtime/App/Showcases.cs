using System.Collections.Generic;
using StormEngine.Core;
using StormEngine.World;
using UnityEngine;

namespace StormEngine
{
    /// <summary>
    /// Built-in islands to visit, so there is always something to admire (and envy), even offline.
    /// Decorations are laid out with the same placement rules as the player's own island.
    /// </summary>
    public static class Showcases
    {
        static IslandLayout[] _cache;

        public static IslandLayout[] All(Content c)
        {
            if (_cache != null) return _cache;
            _cache = new[]
            {
                Make(c, "Captain Cumulus", "title_rainmaker", 24, "skin_ocean", "cloud_candy", "bolt_electric", "world_tropic", 12.5f, 4.2e9, 11,
                    new[] { "dec_pond", "dec_pond", "dec_lilypads", "dec_ducks", "dec_sailboat", "dec_fountain", "dec_windmill", "dec_balloon", "dec_kite",
                        "dec_flowerbed", "dec_flowerbed", "dec_flowerbed", "dec_bench", "dec_bench", "dec_lamppost", "dec_lamppost", "dec_arch", "dec_vane",
                        "dec_mushroom", "dec_gnome", "dec_lanterns" }),
                Make(c, "Volcana", "title_firelord", 31, "skin_magma", "cloud_ash", "bolt_lava", "world_volcano", 14f, 3.1e10, 23,
                    new[] { "dec_lavapool", "dec_lavapool", "dec_obsidian", "dec_obsidian", "dec_obsidian", "dec_beacon", "dec_beacon", "dec_crystal",
                        "dec_crystal", "dec_crystal", "dec_statue", "dec_blimp", "dec_lanterns", "dec_lanterns", "dec_totem", "dec_lamppost", "dec_lamppost",
                        "dec_whale", "dec_pumpkin" }),
                Make(c, "Sakura Sato", "title_zen", 19, "skin_blossom", "cloud_petal", "bolt_blossom", "world_sakura", 11f, 7.5e8, 37,
                    new[] { "dec_pond", "dec_koi", "dec_koi", "dec_lilypads", "dec_pond", "dec_fountain", "dec_toro", "dec_toro", "dec_toro",
                        "dec_flowerbed", "dec_flowerbed", "dec_flowerbed", "dec_flowerbed", "dec_topiary", "dec_bench", "dec_arch", "dec_lanterns", "dec_kite" }),
            };
            return _cache;
        }

        static IslandLayout Make(Content c, string name, string title, int level, string skin, string cloud, string bolt, string world,
            float radius, double best, int seed, string[] decor)
        {
            var l = new IslandLayout
            {
                Name = name,
                Title = title,
                Level = level,
                Skin = skin,
                Cloud = cloud,
                Bolt = bolt,
                World = world,
                Radius = radius,
                BestTournament = best
            };
            int scale = level * 6;
            string[] machines = { "pump", "hydro", "condenser", "tesla", "furnace", "chamber", "turbine" };
            for (int i = 0; i < machines.Length; i++)
                if (c.MachineIndex(machines[i]) >= 0) l.Machines[machines[i]] = Mathf.Max(1, machines[i] == "chamber" ? level / 2 : scale - i * 9);

            // Ponds first so the water decorations have somewhere to float.
            var ordered = new List<string>();
            foreach (var id in decor) if (id == "dec_pond") ordered.Add(id);
            foreach (var id in decor) if (id != "dec_pond") ordered.Add(id);
            int n = 0;
            foreach (var id in ordered)
            {
                int d = c.DecorationIndex(id);
                if (d < 0) continue;
                if (!DecorRig.FindSpot(c, c.Decorations[d], l.Decor, radius, null, seed * 7919 + n * 131, out var spot)) continue;
                l.Decor.Add(new PlacedDecor { Id = id, X = spot.x, Z = spot.y, Rot = (seed * 37 + n * 53) % 360 });
                n++;
            }
            return l;
        }
    }
}
