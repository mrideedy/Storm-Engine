using System;
using System.IO;
using Unity.Notifications;
using UnityEditor;
using UnityEngine;

namespace StormEngine.Notify.EditorTools
{
    /// <summary>
    /// Draws the notification icons (a white bolt for the status bar, a storm cloud badge for the
    /// large icon) and registers them in Project Settings > Mobile Notifications.
    /// </summary>
    public static class NotificationIcons
    {
        const string Folder = "Assets/StormEngine/Notify/Icons";
        const string SmallPath = Folder + "/storm_small.png";
        const string LargePath = Folder + "/storm_large.png";

        // The HUD's lightning bolt (y down, 0..1).
        static readonly Vector2[] Bolt =
        {
            new Vector2(0.60f, 0.03f), new Vector2(0.19f, 0.56f), new Vector2(0.47f, 0.56f), new Vector2(0.36f, 0.97f),
            new Vector2(0.81f, 0.41f), new Vector2(0.53f, 0.41f), new Vector2(0.67f, 0.03f)
        };

        [MenuItem("Storm Engine/Set Up Notification Icons")]
        public static void SetUp()
        {
            Directory.CreateDirectory(Folder);
            // Status bar icons must be a white shape on transparency.
            Write(SmallPath, Render(96, (u, v) => InBolt(u, v, 0.06f, 1f) ? Color.white : Color.clear));
            Write(LargePath, Render(192, Badge));
            AssetDatabase.Refresh();
            var small = Import(SmallPath);
            var large = Import(LargePath);
            NotificationSettings.AndroidSettings.RemoveDrawableResource("storm_small");
            NotificationSettings.AndroidSettings.RemoveDrawableResource("storm_large");
            NotificationSettings.AndroidSettings.AddDrawableResource("storm_small", small, NotificationIconType.Small);
            NotificationSettings.AndroidSettings.AddDrawableResource("storm_large", large, NotificationIconType.Large);
            NotificationSettings.AndroidSettings.RescheduleOnDeviceRestart = true;
            Debug.Log("[StormEngine] Notification icons registered (storm_small, storm_large).");
        }

        static Color Badge(float u, float v)
        {
            float dx = u - 0.5f, dy = v - 0.5f;
            if (dx * dx + dy * dy > 0.25f) return Color.clear;
            // Storm sky, a white cloud and a yellow bolt with an ink outline.
            var sky = Color.Lerp(new Color(0.24f, 0.66f, 0.93f), new Color(0.13f, 0.15f, 0.29f), v);
            bool cloud = Blob(u, v, 0.36f, 0.40f, 0.15f) || Blob(u, v, 0.52f, 0.33f, 0.18f) || Blob(u, v, 0.67f, 0.42f, 0.13f)
                         || u > 0.3f && u < 0.74f && v > 0.40f && v < 0.52f;
            if (InBolt(u, v, 0.15f, 0.62f, 0.54f)) return new Color(1f, 0.78f, 0.24f);
            if (InBolt(u, v, 0.15f, 0.70f, 0.54f)) return new Color(0.08f, 0.13f, 0.23f);
            return cloud ? new Color(0.96f, 0.98f, 1f) : sky;
        }

        static bool Blob(float u, float v, float cx, float cy, float r) => (u - cx) * (u - cx) + (v - cy) * (v - cy) < r * r;

        /// <summary>Point in the bolt, drawn inside a box of <paramref name="scale"/> centred at (0.5, <paramref name="cy"/>).</summary>
        static bool InBolt(float u, float v, float margin, float scale, float cy = 0.5f)
        {
            float s = (1 - 2 * margin) * scale;
            float x = (u - 0.5f) / s + 0.5f, y = (v - cy) / s + 0.5f;
            bool inside = false;
            for (int i = 0, j = Bolt.Length - 1; i < Bolt.Length; j = i++)
            {
                var a = Bolt[i];
                var b = Bolt[j];
                if (a.y > y != b.y > y && x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }

        /// <summary>Renders with 4x4 supersampling (v runs top to bottom).</summary>
        static Texture2D Render(int size, Func<float, float, Color> shade)
        {
            const int ss = 4;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float r = 0, g = 0, b = 0, a = 0;
                    for (int sy = 0; sy < ss; sy++)
                        for (int sx = 0; sx < ss; sx++)
                        {
                            float u = (x + (sx + 0.5f) / ss) / size;
                            float v = 1f - (y + (sy + 0.5f) / ss) / size;
                            var c = shade(u, v);
                            r += c.r * c.a;
                            g += c.g * c.a;
                            b += c.b * c.a;
                            a += c.a;
                        }
                    pixels[y * size + x] = a > 0 ? new Color(r / a, g / a, b / a, a / (ss * ss)) : Color.clear;
                }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        static void Write(string path, Texture2D tex)
        {
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        static Texture2D Import(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
