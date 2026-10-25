using System.Collections.Generic;
using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// Makes the fragile variants look different, so a player (and the level designer) can tell the sections apart:
    /// <list type="bullet">
    /// <item>each variant (Fragile 0/1/2, Checker, Row, Column) uses its own material slot of the TrackTheme; an empty slot
    /// gets a tinted copy of the Fragile material instead;</item>
    /// <item>every section gets a hatched border along its outline, like the original: tiles that fall together share one
    /// border, so two sections side by side show a border between them.</item>
    /// </list>
    /// The tinted and border materials are copies of live materials and follow them every frame, so look fades and the
    /// night glow (T_FragileGlow) still reach them.
    /// </summary>
    public class FragileStyle
    {
        /// <summary>Tints for empty slots: Fragile 0, Fragile 1, Fragile 2, Checker, Row, Column.</summary>
        static readonly Color[] Tints =
        {
            new Color(1f, 1f, 1f),        // Fragile 0: the material as it is
            new Color(1f, 0.78f, 0.5f),   // Fragile 1: amber
            new Color(0.62f, 1f, 0.7f),   // Fragile 2: mint
            new Color(1f, 0.66f, 0.9f),   // Checker: pink
            new Color(0.66f, 0.84f, 1f),  // Row: blue
            new Color(0.84f, 0.7f, 1f),   // Column: violet
        };

        /// <summary>Tint of a variant without a material of its own (white for None).</summary>
        public static Color TintOf(FragileKind kind)
        {
            int i = (int)kind - 1;
            return i >= 0 && i < Tints.Length ? Tints[i] : Color.white;
        }

        class Variant
        {
            public Material source, mat;
            public Color tint;
            public bool border;
        }

        readonly Dictionary<(Material, FragileKind, bool), Variant> variants = new Dictionary<(Material, FragileKind, bool), Variant>();
        Texture2D hatch;

        /// <summary>
        /// <paramref name="source"/> tinted for <paramref name="kind"/> (None = untinted), or its section border version.
        /// </summary>
        public Material Get(Material source, FragileKind kind, bool border = false)
        {
            if (source == null) return null;
            var tint = TintOf(kind);
            if (!border && tint == Color.white) return source; // nothing to change: keep the shared material
            var key = (source, kind, border);
            if (variants.TryGetValue(key, out var v)) return v.mat;
            v = new Variant { source = source, mat = new Material(source), tint = tint, border = border };
            v.mat.name = source.name + (border ? " Border " : " ") + kind;
            Apply(v, true);
            variants[key] = v;
            return v.mat;
        }

        /// <summary>
        /// Follows the source materials. <paramref name="full"/>: copies everything (after a look switched textures),
        /// otherwise only the colours, which is all a fade or the night glow changes.
        /// </summary>
        public void Sync(bool full)
        {
            foreach (var v in variants.Values) Apply(v, full);
        }

        void Apply(Variant v, bool full)
        {
            var s = v.source;
            var m = v.mat;
            if (s == null || m == null) return;
            if (full)
            {
                if (m.shader != s.shader) m.shader = s.shader;
                m.CopyPropertiesFromMaterial(s);
                m.renderQueue = s.renderQueue; // see-through (Transparent) fragile stays see-through
                if (v.border) MakeBorder(m);
            }

            var c = BaseColor(s);
            var tinted = new Color(c.r * v.tint.r, c.g * v.tint.g, c.b * v.tint.b, c.a);
            if (v.border) tinted = new Color(Mathf.Lerp(tinted.r, 1f, 0.55f), Mathf.Lerp(tinted.g, 1f, 0.55f), Mathf.Lerp(tinted.b, 1f, 0.55f), Mathf.Max(c.a, 0.85f));
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tinted);
            if (m.HasProperty("_Color")) m.SetColor("_Color", tinted);

            if (s.HasProperty("_EmissionColor") && m.HasProperty("_EmissionColor"))
            {
                var e = s.GetColor("_EmissionColor");
                float boost = v.border ? 1.3f : 1f;
                m.SetColor("_EmissionColor", new Color(e.r * v.tint.r * boost, e.g * v.tint.g * boost, e.b * v.tint.b * boost, e.a));
            }
        }

        static Color BaseColor(Material m)
        {
            if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor");
            if (m.HasProperty("_Color")) return m.GetColor("_Color");
            return Color.white;
        }

        /// <summary>The border strip: diagonal hatching for both colour and night glow, no normal map.</summary>
        void MakeBorder(Material m)
        {
            var tex = Hatch();
            foreach (var name in new[] { "_BaseMap", "_MainTex" })
                if (m.HasProperty(name))
                {
                    m.SetTexture(name, tex);
                    m.SetTextureScale(name, Vector2.one);
                    m.SetTextureOffset(name, Vector2.zero);
                }
            if (m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", tex);
            if (m.HasProperty("_BumpMap")) m.SetTexture("_BumpMap", null);
            m.DisableKeyword("_NORMALMAP");
            m.DisableKeyword("_PARALLAXMAP");
            if (m.HasProperty("_ParallaxMap")) m.SetTexture("_ParallaxMap", null);
        }

        /// <summary>Light and dark diagonal stripes, one stripe pair per half tile along the strip.</summary>
        Texture2D Hatch()
        {
            if (hatch != null) return hatch;
            const int Size = 64;
            hatch = new Texture2D(Size, Size, TextureFormat.RGBA32, true)
            {
                name = "Fragile Border Hatch",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 4,
            };
            var px = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float d = Mathf.Repeat(x + y, Size * 0.5f) / (Size * 0.5f); // 0..1 across one stripe pair
                    float stripe = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Mathf.Abs(d - 0.5f) - 0.2f) * 12f));
                    byte v = (byte)Mathf.RoundToInt(Mathf.Lerp(255f, 120f, stripe));
                    px[y * Size + x] = new Color32(v, v, v, 255);
                }
            hatch.SetPixels32(px);
            hatch.Apply(true);
            return hatch;
        }

        public void Dispose()
        {
            foreach (var v in variants.Values) if (v.mat != null) Object.Destroy(v.mat);
            variants.Clear();
            if (hatch != null) Object.Destroy(hatch);
            hatch = null;
        }

        /// <summary>
        /// The border mesh of one tile: flat strips just above the top, along the sides that do not join a tile of the
        /// same section. <paramref name="size"/>/<paramref name="centre"/>: the tile's visual box; <paramref name="inset"/>:
        /// distance from the box edge (the bevel). Returns null when every side is joined.
        /// </summary>
        public static Mesh BorderMesh(Vector3 size, Vector3 centre, float inset, float width, bool left, bool right, bool front, bool back)
        {
            if (!left && !right && !front && !back) return null;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            float y = 0.004f;
            float x0 = centre.x - size.x * 0.5f + inset, x1 = centre.x + size.x * 0.5f - inset;
            float z0 = centre.z - size.z * 0.5f + inset, z1 = centre.z + size.z * 0.5f - inset;

            // Each strip runs the full length of its side, so corners are covered; u runs along the side (2 per tile).
            void Strip(Vector3 a, Vector3 b, Vector3 inward)
            {
                int i = verts.Count;
                float len = Vector3.Distance(a, b) * 2f;
                verts.Add(a); verts.Add(b); verts.Add(b + inward * width); verts.Add(a + inward * width);
                uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(len, 0f)); uvs.Add(new Vector2(len, 1f)); uvs.Add(new Vector2(0f, 1f));
                // Facing up whichever way the strip runs (Unity: cross(b - a, c - a) points to the front).
                var n = Vector3.Cross(b - a, inward);
                if (n.y >= 0f) { tris.Add(i); tris.Add(i + 1); tris.Add(i + 2); tris.Add(i); tris.Add(i + 2); tris.Add(i + 3); }
                else { tris.Add(i); tris.Add(i + 2); tris.Add(i + 1); tris.Add(i); tris.Add(i + 3); tris.Add(i + 2); }
            }

            if (back) Strip(new Vector3(x0, y, z0), new Vector3(x1, y, z0), Vector3.forward);
            if (front) Strip(new Vector3(x0, y, z1), new Vector3(x1, y, z1), Vector3.back);
            if (left) Strip(new Vector3(x0, y, z0), new Vector3(x0, y, z1), Vector3.right);
            if (right) Strip(new Vector3(x1, y, z0), new Vector3(x1, y, z1), Vector3.left);

            var mesh = new Mesh { name = "Fragile Border" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            var normals = new Vector3[verts.Count];
            for (int k = 0; k < normals.Length; k++) normals[k] = Vector3.up;
            mesh.normals = normals;
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }
}
