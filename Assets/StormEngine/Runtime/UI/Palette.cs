using UnityEngine;

namespace StormEngine.UI
{
    /// <summary>Cartoon palette from the art direction: sky blue, cloud white, deep navy, warm yellow, orange accents.</summary>
    public static class Palette
    {
        public static readonly Color Navy = Hex(0x1B2A4A);
        public static readonly Color NavySoft = Hex(0x2C416B);
        public static readonly Color Ink = Hex(0x14203A);
        public static readonly Color White = Hex(0xFFFFFF);
        public static readonly Color Cloud = Hex(0xF4F9FF);
        public static readonly Color SkyTop = Hex(0x3FA9EE);
        public static readonly Color SkyBottom = Hex(0xBFE9FF);
        public static readonly Color StormTop = Hex(0x22264A);
        public static readonly Color StormBottom = Hex(0x5B5A8C);
        public static readonly Color Yellow = Hex(0xFFC83D);
        public static readonly Color YellowDark = Hex(0xD9941A);
        public static readonly Color Orange = Hex(0xFF8A3D);
        public static readonly Color OrangeDark = Hex(0xD9621E);
        public static readonly Color Green = Hex(0x4CD07D);
        public static readonly Color GreenDark = Hex(0x2E9E58);
        public static readonly Color Red = Hex(0xFF5A4E);
        public static readonly Color Heat = Hex(0xFF6B3D);
        public static readonly Color Water = Hex(0x3A8DFF);
        public static readonly Color Wind = Hex(0x2FC7B0);
        public static readonly Color CloudCat = Hex(0x9D8CFF);
        public static readonly Color Electric = Hex(0xFFD23F);
        public static readonly Color Research = Hex(0x9B6BFF);
        public static readonly Color Core = Hex(0x39D3E6);
        public static readonly Color Energy = Hex(0xFFC83D);
        public static readonly Color Metal = Hex(0x8FA3BF);
        public static readonly Color MetalDark = Hex(0x5B6F8E);
        public static readonly Color Grass = Hex(0x6CCB4E);
        public static readonly Color GrassDark = Hex(0x4AA23A);
        public static readonly Color Chaos = Hex(0xE04BD8);

        public static readonly Color[] Category = { Heat, Water, Wind, CloudCat, Electric };

        public static Color Hex(int rgb, float a = 1f) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, a);

        public static Color WithAlpha(this Color c, float a) => new Color(c.r, c.g, c.b, a);

        public static Color Lighten(this Color c, float t) => Color.Lerp(c, Color.white, t);

        public static Color Darken(this Color c, float t) => Color.Lerp(c, Color.black, t);

        public static Color ForPheno(string id)
        {
            switch (id)
            {
                case "thunderstorm": return Hex(0x5C7CFA);
                case "heat_storm": return Heat;
                case "monsoon": return Water;
                case "cyclone": return Wind;
                case "supercell": return Hex(0x8E5CFF);
                case "overcharge": return Electric;
                case "anomaly": return Chaos;
                default: return Metal;
            }
        }
    }
}
