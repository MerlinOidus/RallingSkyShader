using System.Collections.Generic;
using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// Procedural meshes with hard edges. The bevels catch specular light,
    /// which is most of what makes the tiles look "HD" instead of flat cubes.
    /// </summary>
    public static class MeshFactory
    {
        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

        class Builder
        {
            readonly List<Vector3> verts = new List<Vector3>();
            readonly List<Vector3> normals = new List<Vector3>();
            readonly List<Vector2> uvs = new List<Vector2>();
            readonly List<int> tris = new List<int>();
            readonly Vector3 size;

            public Builder(Vector3 size) { this.size = size; }

            // Adds a convex face (3 or 4 points) of a shape centred on the origin.
            // Winding is fixed up automatically so the face points outward.
            public void Face(params Vector3[] p)
            {
                Vector3 center = Vector3.zero;
                foreach (var v in p) center += v;
                center /= p.Length;

                Vector3 n = Vector3.Cross(p[1] - p[0], p[2] - p[0]).normalized;
                if (Vector3.Dot(n, center) < 0f)
                {
                    System.Array.Reverse(p);
                    n = -n;
                }

                int start = verts.Count;
                foreach (var v in p)
                {
                    verts.Add(v);
                    normals.Add(n);
                    uvs.Add(Project(v, n));
                }
                for (int i = 1; i < p.Length - 1; i++)
                {
                    tris.Add(start);
                    tris.Add(start + i);
                    tris.Add(start + i + 1);
                }
            }

            Vector2 Project(Vector3 v, Vector3 n)
            {
                float sx = Mathf.Max(size.x, 0.0001f), sy = Mathf.Max(size.y, 0.0001f), sz = Mathf.Max(size.z, 0.0001f);
                if (Mathf.Abs(n.y) > 0.3f) return new Vector2(v.x / sx + 0.5f, v.z / sz + 0.5f); // top / bevels: u = X, v = forward
                if (Mathf.Abs(n.z) >= Mathf.Abs(n.x)) return new Vector2(v.x / sx + 0.5f, v.y / sy + 0.5f);
                return new Vector2(v.z / sz + 0.5f, v.y / sy + 0.5f);
            }

            public Mesh Build(string name)
            {
                var m = new Mesh { name = name };
                m.SetVertices(verts);
                m.SetNormals(normals);
                m.SetUVs(0, uvs);
                m.SetTriangles(tris, 0);
                m.RecalculateBounds();
                m.RecalculateTangents();
                return m;
            }
        }

        /// <summary>Box centred on the origin with a chamfer around the top edges.</summary>
        public static Mesh BeveledBox(Vector3 size, float bevel)
        {
            string key = $"box{size}{bevel}";
            if (cache.TryGetValue(key, out var cached) && cached != null) return cached;

            float hx = size.x * 0.5f, hy = size.y * 0.5f, hz = size.z * 0.5f;
            float b = Mathf.Min(bevel, hx * 0.9f, hy * 0.9f, hz * 0.9f);
            var B = new Builder(size);

            // top inner (y = hy), mid ring (y = hy - b), bottom ring (y = -hy)
            Vector3 t00 = new Vector3(-hx + b, hy, -hz + b), t10 = new Vector3(hx - b, hy, -hz + b);
            Vector3 t11 = new Vector3(hx - b, hy, hz - b), t01 = new Vector3(-hx + b, hy, hz - b);
            float my = hy - b;
            Vector3 m00 = new Vector3(-hx, my, -hz), m10 = new Vector3(hx, my, -hz);
            Vector3 m11 = new Vector3(hx, my, hz), m01 = new Vector3(-hx, my, hz);
            Vector3 b00 = new Vector3(-hx, -hy, -hz), b10 = new Vector3(hx, -hy, -hz);
            Vector3 b11 = new Vector3(hx, -hy, hz), b01 = new Vector3(-hx, -hy, hz);

            B.Face(t00, t01, t11, t10);            // top
            B.Face(t00, t10, m10, m00);            // bevel back (-z)
            B.Face(t01, m01, m11, t11);            // bevel front (+z)
            B.Face(t00, m00, m01, t01);            // bevel left
            B.Face(t10, t11, m11, m10);            // bevel right
            B.Face(m00, m10, b10, b00);            // side -z
            B.Face(m01, b01, b11, m11);            // side +z
            B.Face(m00, b00, b01, m01);            // side -x
            B.Face(m10, m11, b11, b10);            // side +x
            B.Face(b00, b10, b11, b01);            // bottom

            var mesh = B.Build("RTX_BeveledBox");
            cache[key] = mesh;
            return mesh;
        }

        /// <summary>Flat arrow lying just above a tile top, pointing forward (+z). Used on mover tiles.</summary>
        public static Mesh Arrow()
        {
            const string key = "arrow";
            if (cache.TryGetValue(key, out var cached) && cached != null) return cached;

            const float y = 0.012f;
            var B = new Builder(new Vector3(0.6f, 0.02f, 0.7f));
            B.Face(new Vector3(-0.09f, y, -0.32f), new Vector3(0.09f, y, -0.32f), new Vector3(0.09f, y, 0.02f), new Vector3(-0.09f, y, 0.02f)); // shaft
            B.Face(new Vector3(-0.27f, y, 0.02f), new Vector3(0.27f, y, 0.02f), new Vector3(0f, y, 0.34f));                                 // head
            var mesh = B.Build("RTX_Arrow");
            cache[key] = mesh;
            return mesh;
        }

        /// <summary>Faceted diamond used for gems and low-poly foliage.</summary>
        public static Mesh Octahedron()
        {
            const string key = "octa";
            if (cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var B = new Builder(Vector3.one * 2f);
            Vector3 up = Vector3.up, down = Vector3.down;
            Vector3[] ring = { Vector3.right, Vector3.forward, Vector3.left, Vector3.back };
            for (int i = 0; i < 4; i++)
            {
                var a = ring[i];
                var c = ring[(i + 1) % 4];
                B.Face(up, a, c);
                B.Face(down, a, c);
            }
            var mesh = B.Build("RTX_Octahedron");
            cache[key] = mesh;
            return mesh;
        }
    }
}
