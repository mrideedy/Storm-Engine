using StormEngine.Core;
using UnityEngine;

namespace StormEngine.World
{
    public enum PropKind { RoundTrees, Cacti, Pines, Palms, NightTrees, Crystals, Volcanic, Sakura, NeonPylons }

    /// <summary>All colours and styles the diorama needs, resolved from the equipped style items.</summary>
    public sealed class Theme
    {
        public string WorldId;
        public Color Grass, GrassDark, Rock, RockDark;
        public Color SkyTop, SkyHorizon, SkyBottom, Sun, Fog;
        public Color LightColor;
        public float LightIntensity = 1.15f;
        public Vector3 LightEuler = new Vector3(40, 38, 0);
        public float Stars;
        public PropKind Props;
        public Color Trunk, Leaves, LeavesAlt, Flower1, Flower2;
        public bool Water;
        public Color WaterColor;
        /// <summary>Extra glow on the ground (neon grid, lava cracks).</summary>
        public Color GroundGlow;

        public string SkinId;
        public string Shape = "classic";
        public Color Body, Trim, Accent;
        public string CloudStyle = "puffy";
        public Color Cloud, CloudShade, CloudGlow;
        public string BoltStyle = "classic";
        public Color Bolt, BoltCore, BoltGlow;

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);

        public static Theme From(Game g) =>
            From(g.C, g.Equipped(CosmeticSlot.Skin)?.Id, g.Equipped(CosmeticSlot.Cloud)?.Id, g.Equipped(CosmeticSlot.Bolt)?.Id, g.Equipped(CosmeticSlot.World)?.Id);

        public static Theme From(Content c, IslandLayout l) => From(c, l.Skin, l.Cloud, l.Bolt, l.World);

        static CosmeticDef Pick(Content c, string id, CosmeticSlot slot)
        {
            int i = c.CosmeticIndex(id);
            if (i >= 0 && c.Cosmetics[i].Slot == slot) return c.Cosmetics[i];
            foreach (var d in c.Cosmetics) if (d.Slot == slot && d.Default) return d;
            return null;
        }

        public static Theme From(Content c, string skinId, string cloudId, string boltId, string worldId)
        {
            var t = new Theme();
            var skin = Pick(c, skinId, CosmeticSlot.Skin);
            var cloud = Pick(c, cloudId, CosmeticSlot.Cloud);
            var bolt = Pick(c, boltId, CosmeticSlot.Bolt);
            var world = Pick(c, worldId, CosmeticSlot.World);

            t.SkinId = skin.Id;
            t.Shape = string.IsNullOrEmpty(skin.Style) ? "classic" : skin.Style;
            t.Body = Hex(skin.Primary);
            t.Trim = Hex(skin.Secondary);
            t.Accent = Hex(skin.Accent);
            t.CloudStyle = string.IsNullOrEmpty(cloud.Style) ? "puffy" : cloud.Style;
            t.Cloud = Hex(cloud.Primary);
            t.CloudShade = Hex(cloud.Secondary);
            t.CloudGlow = Hex(cloud.Accent);
            t.BoltStyle = string.IsNullOrEmpty(bolt.Style) ? "classic" : bolt.Style;
            t.Bolt = Hex(bolt.Primary);
            t.BoltCore = Hex(bolt.Secondary);
            t.BoltGlow = Hex(bolt.Accent);

            t.WorldId = world.Id;
            var ground = Hex(world.Primary);
            t.SkyTop = Hex(world.Secondary);
            t.SkyBottom = Hex(world.Accent);
            t.Grass = ground;
            t.GrassDark = Color.Lerp(ground, Color.black, 0.18f);
            t.Sun = new Color(1f, 0.95f, 0.72f);
            t.LightColor = new Color(1f, 0.97f, 0.9f);
            t.Trunk = Hex(0x8A5A2B);
            t.Leaves = Hex(0x3FAE4A);
            t.LeavesAlt = Hex(0x5CC45A);
            t.Flower1 = Hex(0xFF7AB6);
            t.Flower2 = Hex(0xFFE36B);
            t.Rock = Hex(0xA0785A);
            t.RockDark = Hex(0x7A5A44);
            t.Props = PropKind.RoundTrees;

            switch (world.Style)
            {
                case "desert":
                    t.Rock = Hex(0xD08A55);
                    t.RockDark = Hex(0xA85A36);
                    t.Props = PropKind.Cacti;
                    t.Leaves = Hex(0x5FA84A);
                    t.Flower1 = Hex(0xFF8A3D);
                    t.Flower2 = Hex(0xFFD23F);
                    t.Sun = new Color(1f, 0.85f, 0.55f);
                    t.LightColor = new Color(1f, 0.9f, 0.75f);
                    t.LightEuler = new Vector3(34, 58, 0);
                    break;
                case "snow":
                    t.GrassDark = Hex(0xCFE3F5);
                    t.Rock = Hex(0x8A9BB5);
                    t.RockDark = Hex(0x66758F);
                    t.Props = PropKind.Pines;
                    t.Leaves = Hex(0x2E7D5B);
                    t.Flower1 = Hex(0xBFE3FF);
                    t.Flower2 = Hex(0xFFFFFF);
                    t.LightColor = new Color(0.9f, 0.95f, 1f);
                    t.Sun = new Color(1f, 0.98f, 0.92f);
                    break;
                case "tropic":
                    t.Grass = Hex(0x74D35A);
                    t.GrassDark = Hex(0xF2DFA0);
                    t.Rock = Hex(0xE3C78A);
                    t.RockDark = Hex(0xB8965C);
                    t.Props = PropKind.Palms;
                    t.Leaves = Hex(0x2FB35A);
                    t.Trunk = Hex(0xA07040);
                    t.Water = true;
                    t.WaterColor = Hex(0x35C0E6);
                    t.Flower1 = Hex(0xFF5A8A);
                    t.Flower2 = Hex(0xFFB020);
                    break;
                case "night":
                    t.Rock = Hex(0x4A4A66);
                    t.RockDark = Hex(0x33334D);
                    t.Props = PropKind.NightTrees;
                    t.Leaves = Hex(0x1F5A3E);
                    t.LeavesAlt = Hex(0x2A6E4C);
                    t.Stars = 1f;
                    t.Sun = new Color(0.85f, 0.9f, 1f);
                    t.LightColor = new Color(0.55f, 0.65f, 1f);
                    t.LightIntensity = 0.75f;
                    t.LightEuler = new Vector3(52, 24, 0);
                    t.Flower1 = Hex(0x7FE7FF);
                    t.Flower2 = Hex(0xB99CFF);
                    break;
                case "alien":
                    t.GrassDark = Hex(0x6B3FD0);
                    t.Rock = Hex(0x3A2466);
                    t.RockDark = Hex(0x24164A);
                    t.Props = PropKind.Crystals;
                    t.Leaves = Hex(0x39F3FF);
                    t.LeavesAlt = Hex(0xFF8BF0);
                    t.Stars = 0.6f;
                    t.Sun = new Color(1f, 0.6f, 0.9f);
                    t.LightColor = new Color(0.95f, 0.8f, 1f);
                    t.LightIntensity = 1.0f;
                    t.Flower1 = Hex(0x7CFF6B);
                    t.Flower2 = Hex(0x39F3FF);
                    break;
                case "volcano":
                    t.GrassDark = Hex(0x33282A);
                    t.Rock = Hex(0x2A1F1E);
                    t.RockDark = Hex(0x171112);
                    t.Props = PropKind.Volcanic;
                    t.Leaves = Hex(0xFF6A1A);
                    t.LeavesAlt = Hex(0xFFB020);
                    t.GroundGlow = Hex(0xFF4A10);
                    t.Stars = 0.2f;
                    t.Sun = new Color(1f, 0.55f, 0.3f);
                    t.LightColor = new Color(1f, 0.75f, 0.6f);
                    t.LightIntensity = 1.0f;
                    t.Flower1 = Hex(0xFF8A3D);
                    t.Flower2 = Hex(0xFFD23F);
                    break;
                case "sakura":
                    t.GrassDark = Hex(0x74B96E);
                    t.Rock = Hex(0xB89A8A);
                    t.RockDark = Hex(0x8E7466);
                    t.Props = PropKind.Sakura;
                    t.Trunk = Hex(0x5A3A30);
                    t.Leaves = Hex(0xF7A1C4);
                    t.LeavesAlt = Hex(0xFFD1E1);
                    t.Water = true;
                    t.WaterColor = Hex(0x9ED8F0);
                    t.Flower1 = Hex(0xFFB7D0);
                    t.Flower2 = Hex(0xFFFFFF);
                    t.LightColor = new Color(1f, 0.93f, 0.95f);
                    break;
                case "neon":
                    t.GrassDark = Hex(0x13132B);
                    t.Rock = Hex(0x1A1033);
                    t.RockDark = Hex(0x0C0820);
                    t.Props = PropKind.NeonPylons;
                    t.Leaves = Hex(0x39F3FF);
                    t.LeavesAlt = Hex(0xFF3DDC);
                    t.GroundGlow = Hex(0x39F3FF);
                    t.Stars = 0.8f;
                    t.Sun = new Color(1f, 0.4f, 0.85f);
                    t.LightColor = new Color(0.7f, 0.65f, 1f);
                    t.LightIntensity = 0.85f;
                    t.Flower1 = Hex(0x39F3FF);
                    t.Flower2 = Hex(0xFF3DDC);
                    break;
            }
            t.SkyHorizon = Color.Lerp(t.SkyBottom, Color.white, 0.25f);
            t.Fog = Color.Lerp(t.SkyHorizon, t.SkyTop, 0.2f);
            return t;
        }

        public string Key => WorldId + SkinId + CloudStyle + BoltStyle + ColorUtility.ToHtmlStringRGB(Cloud) + ColorUtility.ToHtmlStringRGB(Bolt);
    }
}
