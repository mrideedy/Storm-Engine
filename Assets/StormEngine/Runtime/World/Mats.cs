using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace StormEngine.World
{
    /// <summary>Shader lookup, cached toon materials and small procedural textures for the diorama.</summary>
    public static class Mats
    {
        static Shader _toon, _glass, _sky, _glow, _ghost;
        static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();
        static Texture2D _softDot, _streak, _softLine;

        public static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        public static readonly int EmissionId = Shader.PropertyToID("_Emission");
        public static readonly int ShadeColorId = Shader.PropertyToID("_ShadeColor");
        public static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
        public static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        public static readonly int ColorId = Shader.PropertyToID("_Color");
        public static readonly int StormAmbientId = Shader.PropertyToID("_StormAmbient");
        public static readonly int StormDarkenId = Shader.PropertyToID("_StormDarken");

        public static Shader ToonShader => _toon ??= Find("StormEngine/Toon");
        public static Shader GlassShader => _glass ??= Find("StormEngine/Glass");
        public static Shader SkyShader => _sky ??= Find("StormEngine/Sky");
        public static Shader GlowShader => _glow ??= Find("StormEngine/Glow");
        public static Shader GhostShader => _ghost ??= Find("StormEngine/Ghost");

        static Shader Find(string name)
        {
            var s = Shader.Find(name);
            if (s == null) Debug.LogError("[StormEngine] Missing shader " + name);
            return s;
        }

        /// <summary>Shared toon material for a colour (vertex colours still tint it).</summary>
        public static Material Toon(Color color, float outline = 0.0035f, Color? emission = null, Color? shade = null)
        {
            string key = "toon" + ColorUtility.ToHtmlStringRGBA(color) + outline.ToString("0.0000") +
                         (emission.HasValue ? ColorUtility.ToHtmlStringRGBA(emission.Value) : "") +
                         (shade.HasValue ? ColorUtility.ToHtmlStringRGBA(shade.Value) : "");
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            m = NewToon(color, outline, emission, shade);
            Cache[key] = m;
            return m;
        }

        /// <summary>A unique toon material (for parts whose colour animates).</summary>
        public static Material NewToon(Color color, float outline = 0.0035f, Color? emission = null, Color? shade = null)
        {
            var m = new Material(ToonShader) { name = "Toon" };
            m.SetColor(BaseColorId, color);
            m.SetFloat(OutlineWidthId, outline);
            if (emission.HasValue) m.SetColor(EmissionId, emission.Value);
            if (shade.HasValue) m.SetColor(ShadeColorId, shade.Value);
            m.enableInstancing = true;
            return m;
        }

        public static Material Glass()
        {
            var m = new Material(GlassShader) { name = "Glass", renderQueue = (int)RenderQueue.Transparent + 5 };
            return m;
        }

        public static Material Ghost()
        {
            if (Cache.TryGetValue("ghost", out var m) && m != null) return m;
            m = new Material(GhostShader) { name = "Ghost" };
            Cache["ghost"] = m;
            return m;
        }

        public static Material Glow(Color color, bool additive, Texture texture = null, float soft = 0)
        {
            var m = new Material(GlowShader) { name = additive ? "GlowAdd" : "GlowAlpha" };
            m.SetColor(ColorId, color);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_Soft", soft);
            if (texture != null) m.SetTexture("_MainTex", texture);
            return m;
        }

        public static Material Sky() => new Material(SkyShader) { name = "Sky" };

        /// <summary>Soft round sprite for particles.</summary>
        public static Texture2D SoftDot
        {
            get
            {
                if (_softDot != null) return _softDot;
                const int size = 64;
                _softDot = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "SoftDot" };
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f) / size * 2 - 1, dy = (y + 0.5f) / size * 2 - 1;
                        float a = Mathf.Clamp01(1 - Mathf.Sqrt(dx * dx + dy * dy));
                        a = a * a * (3 - 2 * a);
                        px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                _softDot.SetPixels32(px);
                _softDot.Apply();
                return _softDot;
            }
        }

        /// <summary>Thin vertical streak for rain drops.</summary>
        public static Texture2D Streak
        {
            get
            {
                if (_streak != null) return _streak;
                const int w = 16, h = 64;
                _streak = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Streak" };
                var px = new Color32[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float dx = Mathf.Abs((x + 0.5f) / w * 2 - 1);
                        float fy = (y + 0.5f) / h;
                        float a = Mathf.Clamp01(1 - dx * 1.6f) * Mathf.Sin(fy * Mathf.PI);
                        px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                _streak.SetPixels32(px);
                _streak.Apply();
                return _streak;
            }
        }

        /// <summary>Soft across its width (v), solid along its length (u): glow for line renderers.</summary>
        public static Texture2D SoftLine
        {
            get
            {
                if (_softLine != null) return _softLine;
                const int w = 4, h = 32;
                _softLine = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "SoftLine" };
                var px = new Color32[w * h];
                for (int y = 0; y < h; y++)
                {
                    float d = Mathf.Abs((y + 0.5f) / h * 2 - 1);
                    float a = Mathf.Clamp01(1 - d);
                    a = a * a;
                    for (int x = 0; x < w; x++) px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
                _softLine.SetPixels32(px);
                _softLine.Apply();
                return _softLine;
            }
        }

        public static void SetStormGlobals(Color ambient, float darken)
        {
            Shader.SetGlobalColor(StormAmbientId, ambient);
            Shader.SetGlobalFloat(StormDarkenId, darken);
        }
    }
}
