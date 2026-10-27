using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace StormEngine.World
{
    /// <summary>A simple looping animation on part of a decoration.</summary>
    public struct DecorAnim
    {
        public enum Kind { Spin, Bob, Sway, Orbit, Pulse }

        public Transform T;
        public Kind Type;
        public Vector3 Axis;
        public float Speed;
        public float Amount;
        public float Phase;
        public Vector3 Base;
        /// <summary>Spin and sway scale with the wind.</summary>
        public bool Windy;
    }

    /// <summary>
    /// Procedural models for island decorations. Meshes are white and shared between copies;
    /// colour comes from cached toon materials.
    /// </summary>
    public static class DecorModels
    {
        static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();
        static Material _jet;

        static Material JetMaterial => _jet != null ? _jet : (_jet = Mats.Glow(new Color(0.7f, 0.9f, 1f, 0.7f), false));

        static Mesh Get(string key, Func<Mesh> make)
        {
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            m = make();
            m.name = "decor_" + key;
            Cache[key] = m;
            return m;
        }

        /// <summary>Height above the ground for air decorations.</summary>
        public static float AirHeight(string model)
        {
            switch (model)
            {
                case "balloon": return 7.2f;
                case "blimp": return 9f;
                case "whale": return 10f;
                case "lanterns": return 5.2f;
                default: return 6f;
            }
        }

        /// <summary>Approximate footprint radius (for spacing and tap targets).</summary>
        public static float Footprint(string model)
        {
            switch (model)
            {
                case "pond": return 1.05f;
                case "arch": return 1.1f;
                case "statue":
                case "windmill":
                case "lavapool": return 0.8f;
                case "blimp":
                case "whale": return 1.6f;
                case "balloon": return 1.0f;
                default: return 0.55f;
            }
        }

        public static GameObject Build(string model, Transform parent, Color primary, Color accent, List<DecorAnim> anims)
        {
            var root = new GameObject("Decor_" + model).transform;
            root.SetParent(parent, false);
            var w = Color.white;
            Material P(Color c, float outline = 0.003f) => Mats.Toon(c, outline);
            Material Glow(Color c, float power = 1.6f) => Mats.Toon(c, 0.002f, c * power);
            GameObject Part(string key, Func<Mesh> make, Material mat, Vector3 pos, Quaternion? rot = null, Vector3? scale = null, Transform under = null, bool shadows = true)
            {
                var go = new GameObject(key);
                go.transform.SetParent(under != null ? under : root, false);
                go.transform.localPosition = pos;
                go.transform.localRotation = rot ?? Quaternion.identity;
                go.transform.localScale = scale ?? Vector3.one;
                go.AddComponent<MeshFilter>().sharedMesh = Get(key, make);
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                return go;
            }
            Transform Pivot(string name, Vector3 pos, Transform under = null)
            {
                var t = new GameObject(name).transform;
                t.SetParent(under != null ? under : root, false);
                t.localPosition = pos;
                return t;
            }
            var wood = new Color(0.55f, 0.36f, 0.2f);
            var stone = new Color(0.72f, 0.74f, 0.78f);
            var water = new Color(0.33f, 0.72f, 0.95f);

            switch (model)
            {
                case "flowerbed":
                {
                    Part("bed_soil", () => MeshKit.Lathe(MeshKit.CapsuleProfile(0.5f, 0.12f, 0.05f), 16, w), P(new Color(0.45f, 0.3f, 0.2f)), Vector3.zero);
                    for (int i = 0; i < 10; i++)
                    {
                        float a = i / 10f * Mathf.PI * 2;
                        Part("bed_stone", () => MeshKit.Sphere(0.09f, w, 8, 5), P(stone), new Vector3(Mathf.Cos(a) * 0.52f, 0.06f, Mathf.Sin(a) * 0.52f), null, new Vector3(1, 0.7f, 1));
                    }
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i / 6f * Mathf.PI * 2 + 0.3f;
                        Part("bed_flower", () => MeshKit.Flower(w, new Color(0.3f, 0.7f, 0.3f), 1.2f), P(i % 2 == 0 ? primary : accent, 0.0015f),
                            new Vector3(Mathf.Cos(a) * 0.28f, 0.1f, Mathf.Sin(a) * 0.28f), null, null, null, false);
                    }
                    break;
                }
                case "bench":
                {
                    for (int i = 0; i < 3; i++)
                        Part("bench_plank", () => MeshKit.RoundedBox(new Vector3(1.1f, 0.06f, 0.14f), 0.025f, w, 1), P(primary), new Vector3(0, 0.38f, -0.12f + i * 0.13f));
                    for (int i = 0; i < 2; i++)
                        Part("bench_back", () => MeshKit.RoundedBox(new Vector3(1.1f, 0.12f, 0.05f), 0.02f, w, 1), P(primary), new Vector3(0, 0.55f + i * 0.17f, 0.2f), Quaternion.Euler(-10, 0, 0));
                    for (int i = 0; i < 2; i++)
                        Part("bench_leg", () => MeshKit.RoundedBox(new Vector3(0.08f, 0.8f, 0.5f), 0.03f, w, 1), P(accent), new Vector3(i == 0 ? -0.45f : 0.45f, 0.35f, 0.02f));
                    break;
                }
                case "lamppost":
                {
                    Part("lamp_base", () => MeshKit.Cone(0.18f, 0.1f, 0.22f, w, 10), P(primary), Vector3.zero);
                    Part("lamp_pole", () => MeshKit.Capsule(0.05f, 1.6f, 0.02f, w, 8), P(primary), new Vector3(0, 0.2f, 0));
                    Part("lamp_head", () => MeshKit.Cone(0.22f, 0.05f, 0.25f, w, 8), P(primary), new Vector3(0, 1.98f, 0));
                    var bulb = Part("lamp_bulb", () => MeshKit.Sphere(0.15f, w, 12, 8), Glow(accent, 2.2f), new Vector3(0, 1.88f, 0), null, null, null, false);
                    anims.Add(new DecorAnim { T = bulb.transform, Type = DecorAnim.Kind.Pulse, Speed = 2f, Amount = 0.06f, Base = Vector3.one });
                    break;
                }
                case "gnome":
                {
                    Part("gnome_body", () => MeshKit.Cone(0.22f, 0.1f, 0.45f, w, 12), P(accent), Vector3.zero);
                    Part("gnome_head", () => MeshKit.Sphere(0.13f, w, 12, 8), P(new Color(1f, 0.82f, 0.68f)), new Vector3(0, 0.52f, 0));
                    Part("gnome_beard", () => MeshKit.Cone(0.13f, 0.01f, 0.25f, w, 10), P(Color.white), new Vector3(0, 0.5f, -0.06f), Quaternion.Euler(180, 0, 0));
                    Part("gnome_hat", () => MeshKit.Cone(0.15f, 0.01f, 0.42f, w, 10), P(primary), new Vector3(0, 0.6f, 0), Quaternion.Euler(0, 0, 8));
                    Part("gnome_rod", () => MeshKit.Tube(new[] { Vector3.zero, new Vector3(0, 0.25f, 0) }, 0.015f, w, 5, 2), Glow(new Color(1f, 0.9f, 0.4f), 1.5f), new Vector3(0.03f, 1.0f, 0), null, null, null, false);
                    break;
                }
                case "mushroom":
                {
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i / 5f * Mathf.PI * 2;
                        float s = 0.8f + (i % 3) * 0.2f;
                        var p = new Vector3(Mathf.Cos(a) * 0.38f, 0, Mathf.Sin(a) * 0.38f);
                        Part("mush_stem", () => MeshKit.Capsule(0.06f, 0.25f, 0.03f, w, 8), P(new Color(0.98f, 0.95f, 0.88f)), p, null, Vector3.one * s);
                        Part("mush_cap", () => MeshKit.Lathe(MeshKit.DomeProfile(0.16f, 6, 0), 12, w), P(primary), p + new Vector3(0, 0.24f * s, 0), null, Vector3.one * s);
                        Part("mush_dot", () => MeshKit.Sphere(0.03f, w, 6, 4), P(accent, 0f), p + new Vector3(0.05f * s, 0.36f * s, -0.05f * s), null, null, null, false);
                    }
                    break;
                }
                case "pond":
                {
                    Part("pond_bed", () => MeshKit.Lathe(new[] { new Vector2(0, -0.12f), new Vector2(0.95f, -0.08f), new Vector2(1.0f, 0.02f) }, 28, w), P(new Color(0.35f, 0.3f, 0.25f)), Vector3.zero);
                    Part("pond_water", () => MeshKit.Lathe(MeshKit.CapsuleProfile(0.95f, 0.03f, 0.01f), 28, w), Mats.Toon(primary, 0.0f, null, new Color(0.85f, 0.95f, 1f)), new Vector3(0, 0.02f, 0), null, null, null, false);
                    for (int i = 0; i < 14; i++)
                    {
                        float a = i / 14f * Mathf.PI * 2;
                        Part("pond_stone", () => MeshKit.Sphere(0.12f, w, 8, 5), P(stone), new Vector3(Mathf.Cos(a) * 1.0f, 0.04f, Mathf.Sin(a) * 1.0f), Quaternion.Euler(0, i * 40, 0), new Vector3(1.2f, 0.7f, 1f));
                    }
                    break;
                }
                case "lilypads":
                {
                    for (int i = 0; i < 3; i++)
                    {
                        float a = i / 3f * Mathf.PI * 2;
                        var p = new Vector3(Mathf.Cos(a) * 0.2f, 0.05f, Mathf.Sin(a) * 0.2f);
                        var pad = Part("lily_pad", () => MeshKit.Lathe(MeshKit.CapsuleProfile(0.16f, 0.02f, 0.01f), 12, w), P(primary, 0.002f), p, Quaternion.Euler(0, i * 50, 0), null, null, false);
                        anims.Add(new DecorAnim { T = pad.transform, Type = DecorAnim.Kind.Bob, Speed = 1.2f, Amount = 0.01f, Phase = i, Base = p });
                    }
                    Part("lily_flower", () => MeshKit.Flower(w, new Color(0.3f, 0.7f, 0.3f), 1f), P(accent, 0.0015f), new Vector3(0.2f, 0.02f, 0), null, null, null, false);
                    break;
                }
                case "ducks":
                {
                    for (int i = 0; i < 3; i++)
                    {
                        var duck = Pivot("duck", Vector3.zero);
                        float s = i == 0 ? 1f : 0.65f;
                        Part("duck_body", () => MeshKit.Sphere(0.12f, w, 12, 8), P(primary), new Vector3(0, 0.06f, 0), null, new Vector3(1.3f, 0.8f, 1f) * s, duck);
                        Part("duck_head", () => MeshKit.Sphere(0.07f, w, 10, 6), P(primary), new Vector3(0.1f, 0.16f, 0) * s, null, Vector3.one * s, duck);
                        Part("duck_beak", () => MeshKit.Cone(0.03f, 0.005f, 0.07f, w, 6), P(accent), new Vector3(0.17f, 0.15f, 0) * s, Quaternion.Euler(0, 0, -90), Vector3.one * s, duck, false);
                        anims.Add(new DecorAnim { T = duck, Type = DecorAnim.Kind.Orbit, Speed = 18f + i * 4, Amount = 0.25f + i * 0.08f, Phase = i * 120f, Base = Vector3.zero });
                    }
                    break;
                }
                case "windmill":
                {
                    Part("mill_tower", () => MeshKit.Lathe(MeshKit.ConeProfile(0.42f, 0.24f, 1.5f), 8, w), P(primary), Vector3.zero);
                    Part("mill_roof", () => MeshKit.Cone(0.32f, 0.02f, 0.45f, w, 8), P(accent), new Vector3(0, 1.5f, 0));
                    var hub = Pivot("mill_hub", new Vector3(0, 1.25f, -0.32f));
                    Part("mill_axle", () => MeshKit.Sphere(0.08f, w, 8, 6), P(wood), Vector3.zero, null, null, hub);
                    for (int i = 0; i < 4; i++)
                    {
                        var rot = Quaternion.Euler(0, 0, i * 90f);
                        Part("mill_blade", () => MeshKit.RoundedBox(new Vector3(0.14f, 0.75f, 0.03f), 0.02f, w, 1), P(new Color(0.95f, 0.93f, 0.86f)), rot * new Vector3(0, 0.42f, 0), rot, null, hub);
                    }
                    anims.Add(new DecorAnim { T = hub, Type = DecorAnim.Kind.Spin, Axis = Vector3.forward, Speed = 60f, Windy = true });
                    break;
                }
                case "vane":
                {
                    Part("vane_pole", () => MeshKit.Capsule(0.035f, 1.4f, 0.015f, w, 6), P(primary), Vector3.zero);
                    Part("vane_ball", () => MeshKit.Sphere(0.07f, w, 8, 6), P(accent), new Vector3(0, 1.4f, 0));
                    var arrow = Pivot("vane_arrow", new Vector3(0, 1.3f, 0));
                    Part("vane_shaft", () => MeshKit.RoundedBox(new Vector3(0.7f, 0.04f, 0.04f), 0.01f, w, 1), P(accent), Vector3.zero, null, null, arrow);
                    Part("vane_tail", () => MeshKit.RoundedBox(new Vector3(0.18f, 0.18f, 0.02f), 0.01f, w, 1), P(accent), new Vector3(-0.3f, 0, 0), Quaternion.Euler(0, 0, 45), null, arrow);
                    Part("vane_tip", () => MeshKit.Cone(0.07f, 0.005f, 0.14f, w, 6), P(accent), new Vector3(0.38f, 0, 0), Quaternion.Euler(0, 0, -90), null, arrow);
                    anims.Add(new DecorAnim { T = arrow, Type = DecorAnim.Kind.Sway, Axis = Vector3.up, Speed = 0.6f, Amount = 35f, Windy = true });
                    break;
                }
                case "fountain":
                {
                    Part("fount_base", () => MeshKit.Lathe(new[] { new Vector2(0, 0), new Vector2(0.4f, 0), new Vector2(0.45f, 0.15f), new Vector2(0.4f, 0.18f), new Vector2(0, 0.14f) }, 16, w), P(stone), Vector3.zero);
                    Part("fount_pillar", () => MeshKit.Capsule(0.07f, 0.45f, 0.03f, w, 10), P(stone), new Vector3(0, 0.1f, 0));
                    Part("fount_top", () => MeshKit.Lathe(new[] { new Vector2(0, 0), new Vector2(0.22f, 0.05f), new Vector2(0.24f, 0.12f), new Vector2(0, 0.1f) }, 14, w), P(stone), new Vector3(0, 0.52f, 0));
                    var jet = Part("fount_jet", () => MeshKit.Lathe(new[] { new Vector2(0, 0), new Vector2(0.05f, 0.1f), new Vector2(0.03f, 0.4f), new Vector2(0, 0.45f) }, 10, w),
                        JetMaterial, new Vector3(0, 0.6f, 0), null, null, null, false);
                    anims.Add(new DecorAnim { T = jet.transform, Type = DecorAnim.Kind.Pulse, Speed = 5f, Amount = 0.15f, Base = Vector3.one });
                    break;
                }
                case "sailboat":
                {
                    var boat = Pivot("boat", Vector3.zero);
                    Part("boat_hull", () => MeshKit.Sphere(0.2f, w, 12, 6), P(accent), new Vector3(0, 0.04f, 0), null, new Vector3(1.6f, 0.5f, 0.8f), boat);
                    Part("boat_mast", () => MeshKit.Capsule(0.015f, 0.4f, 0.005f, w, 5), P(wood), new Vector3(0, 0.05f, 0), null, null, boat);
                    Part("boat_sail", () => MeshKit.Lathe(new[] { new Vector2(0, 0), new Vector2(0.14f, 0), new Vector2(0.14f, 0), new Vector2(0, 0.32f) }, 3, w), P(primary, 0.002f), new Vector3(0.05f, 0.1f, 0), null, new Vector3(1, 1, 0.2f), boat);
                    anims.Add(new DecorAnim { T = boat, Type = DecorAnim.Kind.Orbit, Speed = 14f, Amount = 0.3f, Base = Vector3.zero });
                    break;
                }
                case "koi":
                {
                    for (int i = 0; i < 2; i++)
                    {
                        var fish = Pivot("koi", Vector3.zero);
                        Part("koi_body", () => MeshKit.Sphere(0.1f, w, 10, 6), P(i == 0 ? primary : accent), new Vector3(0, -0.01f, 0), null, new Vector3(1.8f, 0.5f, 0.8f), fish, false);
                        Part("koi_tail", () => MeshKit.Cone(0.07f, 0.005f, 0.12f, w, 4), P(i == 0 ? accent : primary), new Vector3(-0.2f, -0.01f, 0), Quaternion.Euler(0, 0, 90), new Vector3(1, 1, 0.3f), fish, false);
                        anims.Add(new DecorAnim { T = fish, Type = DecorAnim.Kind.Orbit, Speed = 30f + i * 10, Amount = 0.35f - i * 0.1f, Phase = i * 180f, Base = Vector3.zero });
                    }
                    break;
                }
                case "statue":
                {
                    Part("statue_base", () => MeshKit.RoundedBox(new Vector3(0.9f, 0.4f, 0.9f), 0.06f, w, 2), P(stone), new Vector3(0, 0.2f, 0));
                    Part("statue_legs", () => MeshKit.Capsule(0.2f, 0.6f, 0.1f, w, 12), P(primary), new Vector3(0, 0.4f, 0));
                    Part("statue_body", () => MeshKit.Sphere(0.32f, w, 14, 10), P(primary), new Vector3(0, 1.15f, 0), null, new Vector3(1f, 1.2f, 0.8f));
                    Part("statue_head", () => MeshKit.Sphere(0.2f, w, 12, 8), P(primary), new Vector3(0, 1.62f, 0));
                    Part("statue_arm", () => MeshKit.Tube(new[] { new Vector3(0.25f, 1.25f, 0), new Vector3(0.5f, 1.55f, 0), new Vector3(0.55f, 1.95f, 0) }, 0.07f, w, 8, 4), P(primary), Vector3.zero);
                    var bolt = Part("statue_bolt", () => MeshKit.Lathe(new[] { new Vector2(0, 0), new Vector2(0.1f, 0.1f), new Vector2(0.03f, 0.35f), new Vector2(0.12f, 0.4f), new Vector2(0, 0.8f) }, 4, w),
                        Glow(accent, 2f), new Vector3(0.55f, 1.9f, 0), Quaternion.Euler(0, 45, -10), new Vector3(1, 1, 0.35f), null, false);
                    anims.Add(new DecorAnim { T = bolt.transform, Type = DecorAnim.Kind.Pulse, Speed = 3f, Amount = 0.08f, Base = new Vector3(1, 1, 0.35f) });
                    break;
                }
                case "crystal":
                {
                    Part("crystal_body", () => MeshKit.Flatten(MeshKit.Crystal(w, 0.8f)), Glow(primary, 0.8f), Vector3.zero);
                    Part("crystal_small", () => MeshKit.Shard(0.1f, 0.4f, w), Glow(accent, 1.2f), new Vector3(0.3f, 0, 0.15f), Quaternion.Euler(0, 0, -25));
                    Part("crystal_small2", () => MeshKit.Shard(0.08f, 0.32f, w), Glow(accent, 1.2f), new Vector3(-0.25f, 0, -0.2f), Quaternion.Euler(20, 0, 20));
                    break;
                }
                case "beacon":
                {
                    Part("beacon_base", () => MeshKit.Lathe(MeshKit.ConeProfile(0.45f, 0.28f, 0.5f), 8, w), P(primary), Vector3.zero);
                    Part("beacon_mast", () => MeshKit.Capsule(0.08f, 1.3f, 0.03f, w, 8), P(stone), new Vector3(0, 0.5f, 0));
                    for (int i = 0; i < 3; i++)
                        Part("beacon_ring", () => MeshKit.Torus(0.22f - i * 0.03f, 0.04f, w, 16, 6), P(new Color(0.95f, 0.62f, 0.3f)), new Vector3(0, 0.8f + i * 0.28f, 0));
                    var orb = Part("beacon_orb", () => MeshKit.Sphere(0.2f, w, 14, 10), Glow(accent, 2.4f), new Vector3(0, 1.95f, 0), null, null, null, false);
                    anims.Add(new DecorAnim { T = orb.transform, Type = DecorAnim.Kind.Pulse, Speed = 6f, Amount = 0.15f, Base = Vector3.one });
                    break;
                }
                case "arch":
                {
                    var colors = new[] { new Color(1f, 0.35f, 0.3f), new Color(1f, 0.7f, 0.2f), new Color(1f, 0.92f, 0.3f), new Color(0.35f, 0.8f, 0.4f), new Color(0.3f, 0.6f, 1f), new Color(0.6f, 0.4f, 1f) };
                    for (int i = 0; i < colors.Length; i++)
                    {
                        float r = 1.05f - i * 0.1f;
                        var pts = new List<Vector3>();
                        for (int k = 0; k <= 10; k++)
                        {
                            float a = k / 10f * Mathf.PI;
                            pts.Add(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0));
                        }
                        string key = "arch_band" + i;
                        Part(key, () => MeshKit.Tube(pts, 0.055f, w, 6, 3), Mats.Toon(colors[i], 0.002f, colors[i] * 0.25f), Vector3.zero);
                    }
                    Part("arch_cloudL", () => MeshKit.CloudPuff(w, 3, 3), P(Color.white, 0f), new Vector3(-0.8f, 0.05f, 0), null, Vector3.one * 0.25f, null, false);
                    Part("arch_cloudR", () => MeshKit.CloudPuff(w, 5, 3), P(Color.white, 0f), new Vector3(0.8f, 0.05f, 0), null, Vector3.one * 0.25f, null, false);
                    break;
                }
                case "snowman":
                {
                    Part("snow_bottom", () => MeshKit.Sphere(0.3f, w, 14, 10), P(Color.white), new Vector3(0, 0.26f, 0));
                    Part("snow_middle", () => MeshKit.Sphere(0.22f, w, 14, 10), P(Color.white), new Vector3(0, 0.66f, 0));
                    Part("snow_head", () => MeshKit.Sphere(0.16f, w, 12, 8), P(Color.white), new Vector3(0, 0.98f, 0));
                    Part("snow_nose", () => MeshKit.Cone(0.035f, 0.005f, 0.16f, w, 6), P(accent), new Vector3(0, 0.98f, -0.14f), Quaternion.Euler(-90, 0, 0));
                    Part("snow_hat", () => MeshKit.Capsule(0.11f, 0.18f, 0.02f, w, 12), P(new Color(0.15f, 0.15f, 0.2f)), new Vector3(0, 1.1f, 0));
                    Part("snow_brim", () => MeshKit.Lathe(MeshKit.CapsuleProfile(0.17f, 0.03f, 0.01f), 14, w), P(new Color(0.15f, 0.15f, 0.2f)), new Vector3(0, 1.1f, 0));
                    break;
                }
                case "pumpkin":
                {
                    Part("pump_body", () => MeshKit.Flatten(MeshKit.Lathe(MeshKit.SphereProfile(0.3f, 8), 10, w)), P(primary), new Vector3(0, 0.25f, 0), null, new Vector3(1.2f, 0.85f, 1.2f));
                    Part("pump_stem", () => MeshKit.Capsule(0.04f, 0.12f, 0.02f, w, 6), P(new Color(0.35f, 0.55f, 0.2f)), new Vector3(0, 0.48f, 0));
                    var face = Part("pump_face", () => MeshKit.Sphere(0.06f, w, 6, 4), Glow(accent, 2.5f), new Vector3(0, 0.25f, -0.34f), null, new Vector3(2.2f, 0.6f, 0.5f), null, false);
                    anims.Add(new DecorAnim { T = face.transform, Type = DecorAnim.Kind.Pulse, Speed = 8f, Amount = 0.12f, Base = new Vector3(2.2f, 0.6f, 0.5f) });
                    break;
                }
                case "totem":
                {
                    var colors = new[] { primary, accent, primary };
                    for (int i = 0; i < 3; i++)
                        Part("totem_block" + i, () => MeshKit.RoundedBox(new Vector3(0.42f, 0.4f, 0.42f), 0.06f, w, 2), P(colors[i]), new Vector3(0, 0.2f + i * 0.42f, 0));
                    Part("totem_wings", () => MeshKit.RoundedBox(new Vector3(1.1f, 0.12f, 0.1f), 0.04f, w, 1), P(accent), new Vector3(0, 1.1f, 0));
                    var eye = Part("totem_eyes", () => MeshKit.RoundedBox(new Vector3(0.3f, 0.07f, 0.04f), 0.02f, w, 1), Glow(accent, 2f), new Vector3(0, 1.08f, -0.22f), null, null, null, false);
                    break;
                }
                case "topiary":
                {
                    Part("topi_pot", () => MeshKit.Lathe(MeshKit.ConeProfile(0.22f, 0.3f, 0.3f), 12, w), P(new Color(0.75f, 0.45f, 0.3f)), Vector3.zero);
                    var offsets = new[] { new Vector3(0.1f, 0.5f, 0), new Vector3(-0.08f, 0.78f, 0), new Vector3(0.14f, 1.02f, 0), new Vector3(-0.02f, 1.28f, 0) };
                    for (int i = 0; i < offsets.Length; i++)
                        Part("topi_leaf", () => MeshKit.Sphere(0.2f, w, 10, 6), P(primary), offsets[i], null, new Vector3(1.3f, 0.9f, 0.9f));
                    Part("topi_star", () => MeshKit.Sphere(0.08f, w, 8, 5), Glow(accent, 1.5f), new Vector3(0, 1.5f, 0), null, null, null, false);
                    break;
                }
                case "lavapool":
                {
                    Part("lava_rim", () => MeshKit.Lathe(new[] { new Vector2(0, -0.05f), new Vector2(0.8f, -0.02f), new Vector2(0.9f, 0.1f), new Vector2(0.7f, 0.08f), new Vector2(0, 0.02f) }, 14, w), P(accent), Vector3.zero);
                    var lava = Part("lava_surface", () => MeshKit.Lathe(MeshKit.CapsuleProfile(0.72f, 0.04f, 0.015f), 16, w), Glow(primary, 1.8f), new Vector3(0, 0.03f, 0), null, null, null, false);
                    for (int i = 0; i < 3; i++)
                    {
                        var bubble = Part("lava_bubble", () => MeshKit.Sphere(0.07f, w, 8, 5), Glow(new Color(1f, 0.8f, 0.3f), 2f), new Vector3(0.2f * (i - 1), 0.08f, 0.15f * ((i * 7) % 3 - 1)), null, null, null, false);
                        anims.Add(new DecorAnim { T = bubble.transform, Type = DecorAnim.Kind.Pulse, Speed = 3f + i, Amount = 0.6f, Phase = i * 2f, Base = Vector3.one });
                    }
                    break;
                }
                case "obsidian":
                {
                    Part("obs_spire", () => MeshKit.Shard(0.28f, 1.4f, w), P(primary), Vector3.zero);
                    Part("obs_spire2", () => MeshKit.Shard(0.18f, 0.8f, w), P(primary), new Vector3(0.3f, 0, 0.1f), Quaternion.Euler(0, 0, -20));
                    Part("obs_crack", () => MeshKit.RoundedBox(new Vector3(0.05f, 0.7f, 0.05f), 0.015f, w, 1), Glow(accent, 2.2f), new Vector3(0, 0.5f, -0.14f), Quaternion.Euler(0, 0, 8), null, null, false);
                    break;
                }
                case "toro":
                {
                    Part("toro_base", () => MeshKit.Lathe(MeshKit.ConeProfile(0.28f, 0.2f, 0.15f), 6, w), P(primary), Vector3.zero);
                    Part("toro_post", () => MeshKit.Capsule(0.08f, 0.55f, 0.02f, w, 6), P(primary), new Vector3(0, 0.15f, 0));
                    Part("toro_box", () => MeshKit.RoundedBox(new Vector3(0.36f, 0.26f, 0.36f), 0.03f, w, 1), P(primary), new Vector3(0, 0.83f, 0));
                    Part("toro_light", () => MeshKit.RoundedBox(new Vector3(0.2f, 0.16f, 0.4f), 0.02f, w, 1), Glow(new Color(1f, 0.8f, 0.5f), 2f), new Vector3(0, 0.83f, 0), null, null, null, false);
                    Part("toro_roof", () => MeshKit.Lathe(new[] { new Vector2(0, 0), new Vector2(0.4f, 0), new Vector2(0.42f, 0.05f), new Vector2(0.1f, 0.25f), new Vector2(0, 0.3f) }, 6, w), P(primary), new Vector3(0, 0.96f, 0));
                    break;
                }
                case "neonsign":
                {
                    Part("sign_post", () => MeshKit.Capsule(0.04f, 0.8f, 0.01f, w, 6), P(new Color(0.2f, 0.2f, 0.28f)), new Vector3(-0.35f, 0, 0));
                    Part("sign_post2", () => MeshKit.Capsule(0.04f, 0.8f, 0.01f, w, 6), P(new Color(0.2f, 0.2f, 0.28f)), new Vector3(0.35f, 0, 0));
                    Part("sign_board", () => MeshKit.RoundedBox(new Vector3(0.95f, 0.5f, 0.06f), 0.04f, w, 1), P(new Color(0.08f, 0.06f, 0.16f)), new Vector3(0, 0.95f, 0));
                    var bolt = Part("sign_bolt", () => MeshKit.Lathe(new[] { new Vector2(0, 0), new Vector2(0.06f, 0.08f), new Vector2(0.02f, 0.2f), new Vector2(0.07f, 0.22f), new Vector2(0, 0.4f) }, 4, w),
                        Glow(primary, 2.5f), new Vector3(-0.2f, 0.76f, -0.05f), Quaternion.Euler(0, 45, -15), new Vector3(1, 1, 0.3f), null, false);
                    Part("sign_bar", () => MeshKit.RoundedBox(new Vector3(0.45f, 0.06f, 0.03f), 0.02f, w, 1), Glow(accent, 2.5f), new Vector3(0.15f, 1.0f, -0.05f), null, null, null, false);
                    Part("sign_bar2", () => MeshKit.RoundedBox(new Vector3(0.35f, 0.06f, 0.03f), 0.02f, w, 1), Glow(accent, 2.5f), new Vector3(0.1f, 0.88f, -0.05f), null, null, null, false);
                    anims.Add(new DecorAnim { T = bolt.transform, Type = DecorAnim.Kind.Pulse, Speed = 7f, Amount = 0.1f, Base = new Vector3(1, 1, 0.3f) });
                    break;
                }
                case "holotree":
                {
                    Part("holo_base", () => MeshKit.Lathe(MeshKit.CapsuleProfile(0.3f, 0.12f, 0.04f), 6, w), P(new Color(0.15f, 0.14f, 0.28f)), Vector3.zero);
                    for (int i = 0; i < 5; i++)
                    {
                        var ring = Part("holo_ring" + i, () => MeshKit.Torus(0.55f - i * 0.1f, 0.03f, w, 24, 5), Glow(i % 2 == 0 ? primary : accent, 2.2f), new Vector3(0, 0.3f + i * 0.28f, 0), null, null, null, false);
                        anims.Add(new DecorAnim { T = ring.transform, Type = DecorAnim.Kind.Spin, Axis = Vector3.up, Speed = 40f - i * 12f });
                    }
                    Part("holo_top", () => MeshKit.Shard(0.08f, 0.3f, w), Glow(accent, 2.5f), new Vector3(0, 1.7f, 0), null, null, null, false);
                    break;
                }
                // ---------------------------------------------------------- air
                case "kite":
                {
                    var kite = Pivot("kite", Vector3.zero);
                    Part("kite_sail", () => MeshKit.Lathe(new[] { new Vector2(0, -0.6f), new Vector2(0.4f, 0f), new Vector2(0.4f, 0f), new Vector2(0, 0.35f) }, 4, w),
                        P(primary), Vector3.zero, Quaternion.Euler(90, 45, 0), new Vector3(1, 1, 0.12f), kite);
                    var tail = new List<Vector3>();
                    for (int k = 0; k < 6; k++) tail.Add(new Vector3(0, -0.6f - k * 0.25f, Mathf.Sin(k) * 0.15f));
                    Part("kite_tail", () => MeshKit.Tube(tail, 0.02f, w, 4, 3), P(accent), Vector3.zero, null, null, kite, false);
                    anims.Add(new DecorAnim { T = kite, Type = DecorAnim.Kind.Sway, Axis = Vector3.forward, Speed = 1.3f, Amount = 18f, Windy = true });
                    break;
                }
                case "balloon":
                {
                    var b = Pivot("balloon", Vector3.zero);
                    Part("balloon_env", () => MeshKit.Lathe(new[] { new Vector2(0, 0), new Vector2(0.25f, 0.1f), new Vector2(0.75f, 0.7f), new Vector2(0.85f, 1.2f), new Vector2(0.6f, 1.65f), new Vector2(0, 1.8f) }, 12, w),
                        P(primary), Vector3.zero, null, null, b);
                    for (int i = 0; i < 3; i++)
                        Part("balloon_band", () => MeshKit.Torus(0.76f, 0.05f, w, 24, 5), P(accent), new Vector3(0, 0.95f + (i - 1) * 0.3f, 0), null, new Vector3(1f - Mathf.Abs(i - 1) * 0.1f, 1, 1f - Mathf.Abs(i - 1) * 0.1f), b, false);
                    Part("balloon_basket", () => MeshKit.RoundedBox(new Vector3(0.3f, 0.22f, 0.3f), 0.04f, w, 1), P(wood), new Vector3(0, -0.45f, 0), null, null, b);
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i / 4f * Mathf.PI * 2 + Mathf.PI / 4;
                        Part("balloon_rope", () => MeshKit.Tube(new[] { Vector3.zero, new Vector3(0, 0.45f, 0) }, 0.01f, w, 4, 2), P(wood), new Vector3(Mathf.Cos(a) * 0.14f, -0.35f, Mathf.Sin(a) * 0.14f), null, null, b, false);
                    }
                    anims.Add(new DecorAnim { T = b, Type = DecorAnim.Kind.Bob, Speed = 0.5f, Amount = 0.35f, Base = Vector3.zero });
                    break;
                }
                case "lanterns":
                {
                    for (int i = 0; i < 5; i++)
                    {
                        var p = new Vector3(Mathf.Cos(i * 1.3f) * 0.9f, i * 0.35f, Mathf.Sin(i * 1.3f) * 0.9f);
                        var l = Part("lantern", () => MeshKit.Capsule(0.12f, 0.3f, 0.1f, w, 10), Glow(i % 2 == 0 ? primary : accent, 2f), p, null, null, null, false);
                        anims.Add(new DecorAnim { T = l.transform, Type = DecorAnim.Kind.Bob, Speed = 0.7f + i * 0.13f, Amount = 0.3f, Phase = i, Base = p });
                    }
                    break;
                }
                case "blimp":
                {
                    var b = Pivot("blimp", Vector3.zero);
                    Part("blimp_body", () => MeshKit.Capsule(0.55f, 2.6f, 0.55f, w, 16), P(primary), new Vector3(0, 0, -1.3f), Quaternion.Euler(90, 0, 0), null, b);
                    Part("blimp_band", () => MeshKit.Torus(0.56f, 0.05f, w, 24, 5), P(accent), Vector3.zero, Quaternion.Euler(90, 0, 0), null, b, false);
                    Part("blimp_gondola", () => MeshKit.RoundedBox(new Vector3(0.3f, 0.2f, 0.7f), 0.06f, w, 2), P(accent), new Vector3(0, -0.6f, 0.1f), null, null, b);
                    for (int i = 0; i < 4; i++)
                    {
                        var rot = Quaternion.Euler(0, 0, i * 90f);
                        Part("blimp_fin", () => MeshKit.RoundedBox(new Vector3(0.05f, 0.5f, 0.45f), 0.03f, w, 1), P(accent), rot * new Vector3(0, 0.45f, 0) + new Vector3(0, 0, 1.1f), rot, null, b);
                    }
                    anims.Add(new DecorAnim { T = b, Type = DecorAnim.Kind.Orbit, Speed = 6f, Amount = 5f, Base = Vector3.zero });
                    break;
                }
                case "whale":
                {
                    var b = Pivot("whale", Vector3.zero);
                    Part("whale_body", () => MeshKit.Sphere(0.9f, w, 16, 10), Mats.Toon(primary, 0.002f, primary * 0.15f), Vector3.zero, null, new Vector3(1f, 0.8f, 2.1f), b);
                    Part("whale_belly", () => MeshKit.Sphere(0.75f, w, 14, 8), P(Color.white, 0.002f), new Vector3(0, -0.25f, 0.1f), null, new Vector3(0.9f, 0.6f, 1.8f), b, false);
                    Part("whale_tail", () => MeshKit.RoundedBox(new Vector3(1.4f, 0.08f, 0.4f), 0.05f, w, 1), P(primary, 0.002f), new Vector3(0, 0.2f, -2f), Quaternion.Euler(15, 0, 0), null, b);
                    Part("whale_finL", () => MeshKit.RoundedBox(new Vector3(0.8f, 0.06f, 0.3f), 0.04f, w, 1), P(primary, 0.002f), new Vector3(-0.8f, -0.3f, 0.4f), Quaternion.Euler(0, 20, -25), null, b);
                    Part("whale_finR", () => MeshKit.RoundedBox(new Vector3(0.8f, 0.06f, 0.3f), 0.04f, w, 1), P(primary, 0.002f), new Vector3(0.8f, -0.3f, 0.4f), Quaternion.Euler(0, -20, 25), null, b);
                    Part("whale_eye", () => MeshKit.Sphere(0.08f, w, 8, 5), P(new Color(0.1f, 0.12f, 0.2f), 0f), new Vector3(0.62f, 0.15f, 1.1f), null, null, b, false);
                    Part("whale_puff", () => MeshKit.CloudPuff(w, 21, 4), P(Color.white, 0f), new Vector3(0, 0.9f, 0.7f), null, Vector3.one * 0.25f, b, false);
                    anims.Add(new DecorAnim { T = b, Type = DecorAnim.Kind.Orbit, Speed = 4f, Amount = 6f, Base = Vector3.zero });
                    break;
                }
                default:
                    Part("unknown", () => MeshKit.Sphere(0.3f, w, 10, 6), P(primary), new Vector3(0, 0.3f, 0));
                    break;
            }
            return root.gameObject;
        }
    }
}
