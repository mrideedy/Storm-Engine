using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace StormEngine.World
{
    /// <summary>
    /// Procedural mesh toolkit for the cartoon diorama: lathed shapes, rounded boxes, tubes,
    /// islands and props. Every mesh carries vertex colours so one toon material can shade many parts.
    /// </summary>
    public static class MeshKit
    {
        /// <summary>Accumulates geometry from several shapes into one mesh.</summary>
        public sealed class Builder
        {
            readonly List<Vector3> _v = new List<Vector3>();
            readonly List<Vector3> _n = new List<Vector3>();
            readonly List<Color> _c = new List<Color>();
            readonly List<Vector2> _uv = new List<Vector2>();
            readonly List<int> _t = new List<int>();

            public int VertexCount => _v.Count;

            public int Vertex(Vector3 p, Vector3 n, Color c, Vector2 uv = default)
            {
                _v.Add(p);
                _n.Add(n);
                _c.Add(c);
                _uv.Add(uv);
                return _v.Count - 1;
            }

            public void Tri(int a, int b, int c)
            {
                _t.Add(a);
                _t.Add(b);
                _t.Add(c);
            }

            /// <summary>Quad from a grid cell: a = bottom-left, b = bottom-right, c = top-right, d = top-left.</summary>
            public void Quad(int a, int b, int c, int d)
            {
                Tri(a, d, c);
                Tri(a, c, b);
            }

            public void Add(Mesh mesh, Matrix4x4 m, Color? tint = null)
            {
                var verts = mesh.vertices;
                var norms = mesh.normals;
                var cols = mesh.colors;
                var uvs = mesh.uv;
                var tris = mesh.triangles;
                var nm = m.inverse.transpose;
                int start = _v.Count;
                for (int i = 0; i < verts.Length; i++)
                {
                    _v.Add(m.MultiplyPoint3x4(verts[i]));
                    _n.Add(norms.Length > i ? nm.MultiplyVector(norms[i]).normalized : Vector3.up);
                    var c = cols.Length > i ? cols[i] : Color.white;
                    _c.Add(tint.HasValue ? c * tint.Value : c);
                    _uv.Add(uvs.Length > i ? uvs[i] : Vector2.zero);
                }
                foreach (int t in tris) _t.Add(start + t);
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                if (_v.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(_v);
                mesh.SetNormals(_n);
                mesh.SetColors(_c);
                mesh.SetUVs(0, _uv);
                mesh.SetTriangles(_t, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        // ------------------------------------------------------------------ lathe

        /// <summary>
        /// Revolves a profile of (radius, height) points around Y. Profiles run from the bottom of
        /// the surface to the top; repeat a point to get a sharp crease.
        /// </summary>
        public static Mesh Lathe(IList<Vector2> profile, int segments, Color color, bool closed = false)
        {
            var b = new Builder();
            LatheInto(b, profile, segments, color, Matrix4x4.identity, closed);
            return b.ToMesh("lathe");
        }

        public static void LatheInto(Builder b, IList<Vector2> profile, int segments, Color color, Matrix4x4 m, bool closed = false)
        {
            int k = profile.Count;
            var normals2 = new Vector2[k];
            for (int i = 0; i < k; i++)
            {
                Vector2 prev = i > 0 ? profile[i - 1] : (closed ? profile[k - 2] : profile[i]);
                Vector2 next = i < k - 1 ? profile[i + 1] : (closed ? profile[1] : profile[i]);
                if (i < k - 1 && (profile[i + 1] - profile[i]).sqrMagnitude < 1e-10f) next = profile[i];
                if (i > 0 && (profile[i - 1] - profile[i]).sqrMagnitude < 1e-10f) prev = profile[i];
                var t = next - prev;
                if (t.sqrMagnitude < 1e-12f) t = Vector2.up;
                t.Normalize();
                normals2[i] = new Vector2(t.y, -t.x);
            }

            var nm = m.inverse.transpose;
            int baseIndex = b.VertexCount;
            for (int i = 0; i < k; i++)
            {
                for (int s = 0; s <= segments; s++)
                {
                    float a = s / (float)segments * Mathf.PI * 2f;
                    float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                    var p = new Vector3(profile[i].x * ca, profile[i].y, profile[i].x * sa);
                    var n = new Vector3(normals2[i].x * ca, normals2[i].y, normals2[i].x * sa);
                    b.Vertex(m.MultiplyPoint3x4(p), nm.MultiplyVector(n).normalized, color, new Vector2(s / (float)segments, i / (float)(k - 1)));
                }
            }
            int row = segments + 1;
            for (int i = 0; i < k - 1; i++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int a0 = baseIndex + i * row + s;
                    int b0 = baseIndex + i * row + s + 1;
                    int c0 = baseIndex + (i + 1) * row + s + 1;
                    int d0 = baseIndex + (i + 1) * row + s;
                    b.Quad(a0, b0, c0, d0);
                }
            }
        }

        public static Vector2[] SphereProfile(float r, int rings)
        {
            var p = new Vector2[rings + 1];
            for (int i = 0; i <= rings; i++)
            {
                float a = -Mathf.PI / 2 + Mathf.PI * i / rings;
                p[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            }
            p[0].x = 0;
            p[rings].x = 0;
            return p;
        }

        public static Mesh Sphere(float r, Color color, int segments = 24, int rings = 14) =>
            Lathe(SphereProfile(r, rings), segments, color);

        /// <summary>Upper half-sphere (dome), open at the bottom.</summary>
        public static Vector2[] DomeProfile(float r, int rings, float startAngleDeg = 0)
        {
            var p = new Vector2[rings + 1];
            for (int i = 0; i <= rings; i++)
            {
                float a = Mathf.Lerp(startAngleDeg * Mathf.Deg2Rad, Mathf.PI / 2, i / (float)rings);
                p[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            }
            p[rings].x = 0;
            return p;
        }

        /// <summary>Cylinder with rounded rims. Height measured from y = 0.</summary>
        public static Vector2[] CapsuleProfile(float r, float h, float round, int roundSteps = 4)
        {
            round = Mathf.Min(round, r * 0.95f, h * 0.5f);
            var p = new List<Vector2> { new Vector2(0, 0) };
            for (int i = 0; i <= roundSteps; i++)
            {
                float a = -Mathf.PI / 2 + Mathf.PI / 2 * i / roundSteps;
                p.Add(new Vector2(r - round + Mathf.Cos(a) * round, round + Mathf.Sin(a) * round));
            }
            for (int i = 0; i <= roundSteps; i++)
            {
                float a = Mathf.PI / 2 * i / roundSteps;
                p.Add(new Vector2(r - round + Mathf.Cos(a) * round, h - round + Mathf.Sin(a) * round));
            }
            p.Add(new Vector2(0, h));
            return p.ToArray();
        }

        public static Mesh Capsule(float r, float h, float round, Color color, int segments = 24) =>
            Lathe(CapsuleProfile(r, h, round), segments, color);

        /// <summary>Cone or frustum with a flat bottom and crease edges.</summary>
        public static Vector2[] ConeProfile(float bottom, float top, float h)
        {
            return new[]
            {
                new Vector2(0, 0), new Vector2(bottom, 0), new Vector2(bottom, 0),
                new Vector2(top, h), new Vector2(top, h), new Vector2(0, h)
            };
        }

        public static Mesh Torus(float major, float minor, Color color, int segments = 32, int sides = 12)
        {
            var profile = new Vector2[sides + 1];
            for (int i = 0; i <= sides; i++)
            {
                // Counter-clockwise in the (radius, height) plane so lathe normals face outwards.
                float a = -Mathf.PI / 2 + Mathf.PI * 2 * i / sides;
                profile[i] = new Vector2(major + Mathf.Cos(a) * minor, Mathf.Sin(a) * minor);
            }
            return Lathe(profile, segments, color, true);
        }

        // ------------------------------------------------------------------ boxes & tubes

        public static Mesh RoundedBox(Vector3 size, float radius, Color color, int seg = 4)
        {
            var b = new Builder();
            RoundedBoxInto(b, size, radius, color, Matrix4x4.identity, seg);
            return b.ToMesh("rounded_box");
        }

        public static void RoundedBoxInto(Builder b, Vector3 size, float radius, Color color, Matrix4x4 m, int seg = 4)
        {
            var h = size * 0.5f;
            radius = Mathf.Min(radius, h.x, h.y, h.z);
            var inner = h - Vector3.one * radius;
            var nm = m.inverse.transpose;
            var faces = new[]
            {
                (Vector3.right, Vector3.up), (Vector3.left, Vector3.up), (Vector3.forward, Vector3.up),
                (Vector3.back, Vector3.up), (Vector3.up, Vector3.forward), (Vector3.down, Vector3.forward)
            };
            int res = seg * 2 + 2;
            foreach (var (n, v) in faces)
            {
                var u = Vector3.Cross(n, v);
                int start = b.VertexCount;
                for (int j = 0; j <= res; j++)
                {
                    for (int i = 0; i <= res; i++)
                    {
                        float fu = i / (float)res * 2 - 1;
                        float fv = j / (float)res * 2 - 1;
                        var p = Vector3.Scale(n + u * fu + v * fv, h);
                        var clamped = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                        var dir = p - clamped;
                        var normal = dir.sqrMagnitude > 1e-10f ? dir.normalized : n;
                        var pos = clamped + normal * radius;
                        b.Vertex(m.MultiplyPoint3x4(pos), nm.MultiplyVector(normal).normalized, color);
                    }
                }
                int row = res + 1;
                for (int j = 0; j < res; j++)
                    for (int i = 0; i < res; i++)
                        b.Quad(start + j * row + i, start + j * row + i + 1, start + (j + 1) * row + i + 1, start + (j + 1) * row + i);
            }
        }

        /// <summary>Tube along a Catmull-Rom path through the points, with rounded ends.</summary>
        public static Mesh Tube(IList<Vector3> points, float radius, Color color, int sides = 10, int samplesPerSegment = 8)
        {
            var b = new Builder();
            TubeInto(b, points, radius, color, sides, samplesPerSegment);
            return b.ToMesh("tube");
        }

        public static void TubeInto(Builder b, IList<Vector3> points, float radius, Color color, int sides = 10, int samplesPerSegment = 8)
        {
            var path = new List<Vector3>();
            for (int i = 0; i < points.Count - 1; i++)
            {
                var p0 = points[Mathf.Max(0, i - 1)];
                var p1 = points[i];
                var p2 = points[i + 1];
                var p3 = points[Mathf.Min(points.Count - 1, i + 2)];
                for (int s = 0; s < samplesPerSegment; s++) path.Add(CatmullRom(p0, p1, p2, p3, s / (float)samplesPerSegment));
            }
            path.Add(points[points.Count - 1]);

            Vector3 prevNormal = Vector3.zero;
            int start = b.VertexCount;
            for (int i = 0; i < path.Count; i++)
            {
                var tangent = (path[Mathf.Min(path.Count - 1, i + 1)] - path[Mathf.Max(0, i - 1)]).normalized;
                Vector3 normal;
                if (i == 0)
                {
                    normal = Vector3.Cross(tangent, Mathf.Abs(tangent.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
                }
                else
                {
                    normal = (prevNormal - tangent * Vector3.Dot(prevNormal, tangent)).normalized;
                }
                prevNormal = normal;
                var binormal = Vector3.Cross(tangent, normal);
                for (int s = 0; s <= sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2;
                    var dir = normal * Mathf.Cos(a) + binormal * Mathf.Sin(a);
                    b.Vertex(path[i] + dir * radius, dir, color);
                }
            }
            int row = sides + 1;
            for (int i = 0; i < path.Count - 1; i++)
                for (int s = 0; s < sides; s++)
                {
                    int a0 = start + i * row + s;
                    int b0 = start + i * row + s + 1;
                    int c0 = start + (i + 1) * row + s + 1;
                    int d0 = start + (i + 1) * row + s;
                    b.Tri(a0, c0, d0);
                    b.Tri(a0, b0, c0);
                }
            // Round caps.
            var cap = Sphere(radius, color, sides, 6);
            b.Add(cap, Matrix4x4.Translate(path[0]));
            b.Add(cap, Matrix4x4.Translate(path[path.Count - 1]));
        }

        public static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        /// <summary>Flat quad facing -Z with 0..1 UVs (billboards, glows).</summary>
        public static Mesh Quad(float width, float height, Color color)
        {
            var b = new Builder();
            float w = width * 0.5f, h = height * 0.5f;
            int a = b.Vertex(new Vector3(-w, -h, 0), Vector3.back, color, new Vector2(0, 0));
            int bb = b.Vertex(new Vector3(w, -h, 0), Vector3.back, color, new Vector2(1, 0));
            int c = b.Vertex(new Vector3(w, h, 0), Vector3.back, color, new Vector2(1, 1));
            int d = b.Vertex(new Vector3(-w, h, 0), Vector3.back, color, new Vector2(0, 1));
            b.Quad(a, bb, c, d);
            return b.ToMesh("quad");
        }

        /// <summary>Cone or frustum mesh standing on y = 0.</summary>
        public static Mesh Cone(float bottom, float top, float height, Color color, int segments = 16) =>
            Lathe(ConeProfile(bottom, top, height), segments, color);

        /// <summary>
        /// Splits shared vertices so every triangle gets its own face normal: a crisp faceted,
        /// low-poly look for crystal, neon and chaos shapes.
        /// </summary>
        public static Mesh Flatten(Mesh source)
        {
            var verts = source.vertices;
            var cols = source.colors;
            var tris = source.triangles;
            var v = new Vector3[tris.Length];
            var n = new Vector3[tris.Length];
            var c = new Color[tris.Length];
            var t = new int[tris.Length];
            for (int i = 0; i < tris.Length; i += 3)
            {
                var a = verts[tris[i]];
                var b = verts[tris[i + 1]];
                var d = verts[tris[i + 2]];
                var normal = Vector3.Cross(b - a, d - a).normalized;
                for (int k = 0; k < 3; k++)
                {
                    v[i + k] = verts[tris[i + k]];
                    n[i + k] = normal;
                    c[i + k] = cols.Length > 0 ? cols[tris[i + k]] : Color.white;
                    t[i + k] = i + k;
                }
            }
            var mesh = new Mesh { name = source.name + "_flat" };
            if (v.Length > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = v;
            mesh.normals = n;
            mesh.colors = c;
            mesh.triangles = t;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Flat gear: a short cylinder with square teeth around it, lying in the XZ plane.</summary>
        public static Mesh Gear(float radius, float thickness, int teeth, Color color)
        {
            var b = new Builder();
            LatheInto(b, CapsuleProfile(radius, thickness, thickness * 0.3f), Math.Max(12, teeth * 2), color, Matrix4x4.Translate(new Vector3(0, -thickness * 0.5f, 0)));
            float tooth = radius * 0.28f;
            for (int i = 0; i < teeth; i++)
            {
                float a = i / (float)teeth * 360f;
                var rot = Quaternion.Euler(0, a, 0);
                RoundedBoxInto(b, new Vector3(tooth, thickness * 0.9f, tooth * 0.8f), tooth * 0.18f, color,
                    Matrix4x4.TRS(rot * new Vector3(radius + tooth * 0.35f, 0, 0), rot, Vector3.one), 1);
            }
            LatheInto(b, CapsuleProfile(radius * 0.3f, thickness * 1.3f, thickness * 0.3f), 12, Color.Lerp(color, Color.black, 0.25f),
                Matrix4x4.Translate(new Vector3(0, -thickness * 0.65f, 0)));
            return b.ToMesh("gear");
        }

        /// <summary>Crystal shard: an elongated octahedron.</summary>
        public static Mesh Shard(float width, float height, Color color)
        {
            var profile = new[] { new Vector2(0, 0), new Vector2(width, height * 0.35f), new Vector2(width, height * 0.35f), new Vector2(0, height) };
            return Flatten(Lathe(profile, 5, color));
        }

        // ------------------------------------------------------------------ nature

        static float Hash(float x, float y) => Mathf.Repeat(Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f, 1f);

        public static float Noise(float x, float y)
        {
            float ix = Mathf.Floor(x), iy = Mathf.Floor(y);
            float fx = x - ix, fy = y - iy;
            float ux = fx * fx * (3 - 2 * fx), uy = fy * fy * (3 - 2 * fy);
            float a = Hash(ix, iy), b = Hash(ix + 1, iy), c = Hash(ix, iy + 1), d = Hash(ix + 1, iy + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, ux), Mathf.Lerp(c, d, ux), uy);
        }

        /// <summary>Height of the island's top surface at a point (shared by props that sit on it).</summary>
        public static float IslandHeight(float x, float z, float radius)
        {
            float r = Mathf.Sqrt(x * x + z * z) / radius;
            float hills = (Noise(x * 0.35f + 11, z * 0.35f + 3) - 0.5f) * 0.9f + (Noise(x * 0.9f, z * 0.9f) - 0.5f) * 0.25f;
            float flatCenter = Mathf.SmoothStep(0.25f, 0.55f, r);
            float edgeDrop = Mathf.SmoothStep(0.86f, 1.0f, r) * 0.35f;
            return hills * flatCenter - edgeDrop;
        }

        /// <summary>Floating island: grassy top with gentle hills and a rocky underside.</summary>
        public static Mesh IslandTop(float radius, Color grass, Color grassDark, int rings = 18, int segments = 64)
        {
            var b = new Builder();
            int center = b.Vertex(new Vector3(0, IslandHeight(0, 0, radius), 0), Vector3.up, grass);
            var ringStart = new List<int>();
            for (int r = 1; r <= rings; r++)
            {
                ringStart.Add(b.VertexCount);
                float rr = radius * r / rings;
                for (int s = 0; s < segments; s++)
                {
                    float a = s / (float)segments * Mathf.PI * 2;
                    float wobble = 1 + (Noise(Mathf.Cos(a) * 3 + 5, Mathf.Sin(a) * 3) - 0.5f) * 0.12f * (r / (float)rings);
                    float x = Mathf.Cos(a) * rr * wobble, z = Mathf.Sin(a) * rr * wobble;
                    float y = IslandHeight(x, z, radius);
                    float patch = Noise(x * 0.5f + 40, z * 0.5f);
                    var col = Color.Lerp(grass, grassDark, Mathf.SmoothStep(0.45f, 0.7f, patch));
                    // Sunlit patches and a slightly darker rim give the ground a painted look.
                    float sun = Noise(x * 0.32f - 13, z * 0.32f + 7);
                    col = Color.Lerp(col, Color.Lerp(grass, new Color(0.9f, 1f, 0.42f), 0.28f), Mathf.SmoothStep(0.6f, 0.82f, sun) * 0.85f);
                    col = Color.Lerp(col, grassDark, Mathf.SmoothStep(0.82f, 1f, r / (float)rings) * 0.35f);
                    b.Vertex(new Vector3(x, y, z), Vector3.up, col);
                }
            }
            for (int s = 0; s < segments; s++)
                b.Tri(center, ringStart[0] + (s + 1) % segments, ringStart[0] + s);
            for (int r = 0; r < rings - 1; r++)
                for (int s = 0; s < segments; s++)
                {
                    int s1 = (s + 1) % segments;
                    int a = ringStart[r] + s, bb = ringStart[r] + s1, c = ringStart[r + 1] + s1, d = ringStart[r + 1] + s;
                    b.Tri(a, bb, c);
                    b.Tri(a, c, d);
                }
            var mesh = b.ToMesh("island_top");
            mesh.RecalculateNormals();
            return mesh;
        }

        public static Mesh IslandRock(float radius, Color rock, Color rockDark, int segments = 64)
        {
            // Profile from the pointed bottom up to the rim, just under the grass edge.
            var profile = new List<Vector2>
            {
                new Vector2(0, -5.2f), new Vector2(radius * 0.18f, -4.6f), new Vector2(radius * 0.42f, -3.4f),
                new Vector2(radius * 0.7f, -2.0f), new Vector2(radius * 0.9f, -1.0f), new Vector2(radius * 1.0f, -0.45f),
                new Vector2(radius * 1.02f, -0.25f), new Vector2(radius * 1.0f, -0.12f)
            };
            var b = new Builder();
            LatheInto(b, profile, segments, rock, Matrix4x4.identity);
            var mesh = b.ToMesh("island_rock");
            // Break up the silhouette and colour it in strata.
            var verts = mesh.vertices;
            var cols = new Color[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                var v = verts[i];
                float a = Mathf.Atan2(v.z, v.x);
                float n = Noise(Mathf.Cos(a) * 4 + v.y * 0.7f, Mathf.Sin(a) * 4 - v.y * 0.5f);
                float scale = 1 + (n - 0.5f) * 0.18f;
                if (v.y < -0.2f) verts[i] = new Vector3(v.x * scale, v.y, v.z * scale);
                float strata = Mathf.Repeat(v.y * 1.4f + n * 0.8f, 1f);
                cols[i] = Color.Lerp(rock, rockDark, strata > 0.5f ? 0.55f : 0.1f);
            }
            mesh.vertices = verts;
            mesh.colors = cols;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh RoundTree(Color trunk, Color leaves, float scale, int seed)
        {
            var rng = new System.Random(seed);
            var b = new Builder();
            LatheInto(b, ConeProfile(0.14f, 0.09f, 1.1f), 10, trunk, Matrix4x4.Scale(Vector3.one * scale));
            int blobs = 3;
            for (int i = 0; i < blobs; i++)
            {
                float r = (0.55f + (float)rng.NextDouble() * 0.2f) * scale;
                var offset = new Vector3(((float)rng.NextDouble() - 0.5f) * 0.5f, 1.2f + i * 0.28f, ((float)rng.NextDouble() - 0.5f) * 0.5f) * scale;
                LatheInto(b, SphereProfile(r, 10), 16, Color.Lerp(leaves, leaves * 1.15f, i / (float)blobs), Matrix4x4.Translate(offset));
            }
            return b.ToMesh("tree");
        }

        public static Mesh PineTree(Color trunk, Color leaves, Color snowTip, float scale)
        {
            var b = new Builder();
            LatheInto(b, ConeProfile(0.12f, 0.1f, 0.6f), 8, trunk, Matrix4x4.Scale(Vector3.one * scale));
            for (int i = 0; i < 3; i++)
            {
                float w = (0.75f - i * 0.18f) * scale;
                float y = (0.45f + i * 0.5f) * scale;
                var col = i == 2 ? snowTip : leaves;
                LatheInto(b, ConeProfile(w, 0.02f * scale, 0.9f * scale), 12, col, Matrix4x4.Translate(new Vector3(0, y, 0)));
            }
            return b.ToMesh("pine");
        }

        public static Mesh Cactus(Color body, float scale)
        {
            var b = new Builder();
            LatheInto(b, CapsuleProfile(0.22f, 1.6f, 0.22f), 12, body, Matrix4x4.Scale(Vector3.one * scale));
            var arm = CapsuleProfile(0.14f, 0.7f, 0.14f);
            LatheInto(b, arm, 10, body, Matrix4x4.TRS(new Vector3(0.28f, 0.6f, 0) * scale, Quaternion.identity, Vector3.one * scale));
            LatheInto(b, arm, 10, body, Matrix4x4.TRS(new Vector3(-0.26f, 0.8f, 0) * scale, Quaternion.identity, Vector3.one * scale * 0.8f));
            b.Add(Tube(new[] { new Vector3(0.1f, 0.62f, 0), new Vector3(0.28f, 0.62f, 0) }, 0.1f, body, 8, 2), Matrix4x4.Scale(Vector3.one * scale));
            b.Add(Tube(new[] { new Vector3(-0.1f, 0.84f, 0), new Vector3(-0.26f, 0.84f, 0) }, 0.08f, body, 8, 2), Matrix4x4.Scale(Vector3.one * scale));
            return b.ToMesh("cactus");
        }

        public static Mesh PalmTree(Color trunk, Color leaves, float scale)
        {
            var b = new Builder();
            var path = new List<Vector3>();
            for (int i = 0; i <= 5; i++) path.Add(new Vector3(Mathf.Sin(i * 0.25f) * 0.35f, i * 0.42f, 0) * scale);
            TubeInto(b, path, 0.09f * scale, trunk, 8, 4);
            var top = path[path.Count - 1];
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f;
                var rot = Quaternion.Euler(0, a, 0) * Quaternion.Euler(0, 0, -28);
                b.Add(Sphere(0.5f, leaves, 10, 6), Matrix4x4.TRS(top + rot * new Vector3(0.45f, 0, 0) * scale, rot, new Vector3(1.0f, 0.12f, 0.32f) * scale));
            }
            return b.ToMesh("palm");
        }

        public static Mesh Crystal(Color color, float scale)
        {
            var b = new Builder();
            for (int i = 0; i < 3; i++)
            {
                var rot = Quaternion.Euler(i * 18 - 18, i * 120, i * 12 - 10);
                var shape = new[] { new Vector2(0, 0), new Vector2(0.18f, 0.2f), new Vector2(0.18f, 0.2f), new Vector2(0.14f, 0.9f), new Vector2(0.14f, 0.9f), new Vector2(0, 1.15f) };
                LatheInto(b, shape, 6, Color.Lerp(color, Color.white, i * 0.15f), Matrix4x4.TRS(Vector3.zero, rot, Vector3.one * scale * (1 - i * 0.2f)));
            }
            return b.ToMesh("crystal");
        }

        /// <summary>Volcanic rock spires: a cluster of jagged dark cones.</summary>
        public static Mesh Spires(Color color, float scale, int seed)
        {
            var rng = new System.Random(seed);
            var b = new Builder();
            int count = 2 + rng.Next(0, 3);
            for (int i = 0; i < count; i++)
            {
                float h = (0.8f + (float)rng.NextDouble() * 1.2f) * scale;
                float w = (0.22f + (float)rng.NextDouble() * 0.15f) * scale;
                var offset = new Vector3(((float)rng.NextDouble() - 0.5f) * 0.6f * scale, 0, ((float)rng.NextDouble() - 0.5f) * 0.6f * scale);
                var rot = Quaternion.Euler(((float)rng.NextDouble() - 0.5f) * 16f, (float)rng.NextDouble() * 360f, ((float)rng.NextDouble() - 0.5f) * 16f);
                LatheInto(b, ConeProfile(w, w * 0.08f, h), 5, Color.Lerp(color, Color.black, i * 0.15f), Matrix4x4.TRS(offset, rot, Vector3.one));
            }
            return Flatten(b.ToMesh("spires"));
        }

        /// <summary>Neon pylon: a slim pole wrapped in glowing rings.</summary>
        public static Mesh Pylon(Color pole, Color glow, float scale)
        {
            var b = new Builder();
            LatheInto(b, CapsuleProfile(0.07f, 2.2f, 0.05f), 8, pole, Matrix4x4.Scale(Vector3.one * scale));
            for (int i = 0; i < 3; i++)
            {
                var ring = Torus(0.2f - i * 0.03f, 0.035f, glow, 16, 6);
                b.Add(ring, Matrix4x4.TRS(new Vector3(0, (0.9f + i * 0.45f) * scale, 0), Quaternion.identity, Vector3.one * scale));
            }
            b.Add(Sphere(0.12f, glow, 10, 6), Matrix4x4.TRS(new Vector3(0, 2.25f * scale, 0), Quaternion.identity, Vector3.one * scale));
            return b.ToMesh("pylon");
        }

        public static Mesh Rock(Color color, float scale, int seed)
        {
            var mesh = Sphere(0.5f, color, 10, 6);
            var verts = mesh.vertices;
            var rng = new System.Random(seed);
            float sx = 0.8f + (float)rng.NextDouble() * 0.6f, sz = 0.8f + (float)rng.NextDouble() * 0.6f;
            for (int i = 0; i < verts.Length; i++)
            {
                var v = verts[i];
                float n = Noise(v.x * 3 + seed, v.z * 3 + v.y * 2);
                verts[i] = new Vector3(v.x * sx, Mathf.Max(-0.05f, v.y * 0.7f), v.z * sz) * scale * (0.85f + n * 0.3f);
            }
            mesh.vertices = verts;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh CloudPuff(Color color, int seed, int blobs = 6)
        {
            var rng = new System.Random(seed);
            var b = new Builder();
            for (int i = 0; i < blobs; i++)
            {
                float t = blobs == 1 ? 0.5f : i / (float)(blobs - 1);
                float r = Mathf.Lerp(0.55f, 1.0f, Mathf.Sin(t * Mathf.PI)) * (0.85f + (float)rng.NextDouble() * 0.3f);
                var pos = new Vector3(Mathf.Lerp(-1.6f, 1.6f, t), r * 0.35f + (float)rng.NextDouble() * 0.2f, ((float)rng.NextDouble() - 0.5f) * 0.6f);
                LatheInto(b, SphereProfile(r, 10), 16, color, Matrix4x4.Translate(pos));
            }
            return b.ToMesh("cloud");
        }

        public static Mesh Flower(Color petal, Color stem, float scale)
        {
            var b = new Builder();
            b.Add(Tube(new[] { Vector3.zero, new Vector3(0, 0.28f, 0) }, 0.02f, stem, 5, 2), Matrix4x4.Scale(Vector3.one * scale));
            for (int i = 0; i < 5; i++)
            {
                var rot = Quaternion.Euler(0, i * 72, 0);
                b.Add(Sphere(0.06f, petal, 8, 5), Matrix4x4.TRS(new Vector3(0, 0.3f, 0) * scale + rot * new Vector3(0.07f, 0, 0) * scale, rot, new Vector3(1.2f, 0.4f, 0.8f) * scale));
            }
            b.Add(Sphere(0.045f, new Color(1f, 0.85f, 0.3f), 8, 5), Matrix4x4.Translate(new Vector3(0, 0.31f, 0) * scale));
            return b.ToMesh("flower");
        }
    }
}
