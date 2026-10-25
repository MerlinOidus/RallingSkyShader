using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RTXOn
{
    /// <summary>
    /// The original game's midground (MidgroundManager): one block per row, left or right of the track (or none, see
    /// Midground Blank), from the theme's Midground Models. A block waits deep below and rises as the camera comes
    /// near, until its top is Midground Height below the track, about when the camera passes it. The blocks are unlit
    /// with the Tile Fragile texture and fade to the theme's Mid Fog colour with distance (original 6_Midground).
    /// Each row uses the look of that row; which block, which side and how far out are fixed per row, so a restart or a
    /// checkpoint shows the same blocks.
    /// </summary>
    [DisallowMultipleComponent]
    public class Midground : MonoBehaviour
    {
        const string ShaderName = "RTXOn/Midground";

        [Tooltip("Rows a block rises over, up to where it is fully risen (original 30).")]
        public int depth = 30;
        [Tooltip("Rows behind the ball where a block is fully risen (the original: just behind the camera).")]
        public float risenBehind = 4f;
        [Tooltip("Rows behind the ball a block stays before it goes (behind the camera).")]
        public int rowsBehind = 10;

        struct Block { public Transform t; public float x, height, rise; }

        readonly Dictionary<int, Block> blocks = new Dictionary<int, Block>(); // by row; rows with no block are left out
        readonly Dictionary<TrackTheme, Material> materials = new Dictionary<TrackTheme, Material>();
        readonly List<int> toRemove = new List<int>();
        System.Func<int, TrackTheme> lookAt;
        Transform ball;
        int rowCount, firstRow = int.MinValue, lastRow = -1;
        bool missingShader;

        /// <summary>
        /// Starts the midground. <paramref name="look"/> gives the theme of a row (the level's looks);
        /// <paramref name="baseLook"/> is the theme the level starts with, whose material is made now, before any look fade.
        /// </summary>
        public void Init(TrackTheme baseLook, System.Func<int, TrackTheme> look, Transform ballTransform, int rows)
        {
            Clear();
            lookAt = look;
            ball = ballTransform;
            rowCount = rows;
            if (baseLook != null) MaterialFor(baseLook);
            Refresh();
        }

        void Clear()
        {
            foreach (var b in blocks.Values) if (b.t != null) Destroy(b.t.gameObject);
            blocks.Clear();
            firstRow = int.MinValue;
            lastRow = -1;
        }

        void OnDestroy()
        {
            Clear();
            foreach (var m in materials.Values) if (m != null) Destroy(m);
            materials.Clear();
        }

        void LateUpdate() => Refresh();

        // The ball, not the camera: it moves every frame before LateUpdate, so the blocks rise smoothly with it.
        float ViewZ()
        {
            if (ball != null) return ball.position.z;
            var cam = Camera.main;
            return cam != null ? cam.transform.position.z : 0f;
        }

        void Refresh()
        {
            if (lookAt == null) return;
            float z = ViewZ();
            float risen = z - risenBehind; // where a block is fully up
            int from = Mathf.FloorToInt(z) - rowsBehind;
            int to = Mathf.FloorToInt(risen) + Mathf.Max(1, depth);

            toRemove.Clear();
            foreach (var kv in blocks)
                if (kv.Key < from || kv.Key > to) toRemove.Add(kv.Key);
            foreach (var r in toRemove)
            {
                if (blocks[r].t != null) Destroy(blocks[r].t.gameObject);
                blocks.Remove(r);
            }
            // New rows at the far end (and every row on the first frame or after a jump back).
            if (from != firstRow || to != lastRow)
            {
                for (int r = from; r <= to; r++)
                    if (!blocks.ContainsKey(r)) Make(r);
                firstRow = from;
                lastRow = to;
            }

            foreach (var kv in blocks)
            {
                var b = kv.Value;
                if (b.t == null) continue;
                // 0 = just made at the far end, 1 = fully risen as the ball nears it. Measured from the ball's exact
                // position (not its row), so a block moves a little every frame instead of a step per row.
                float p = Mathf.Clamp01(1f - (kv.Key - risen) / Mathf.Max(1, depth));
                float e = 1f - (1f - p) * (1f - p);
                b.t.position = new Vector3(b.x, -(b.height + b.rise * (1f - e)), kv.Key);
            }
        }

        void Make(int row)
        {
            if (row < 0 || (rowCount > 0 && row >= rowCount + depth)) return;
            var look = lookAt(row);
            if (look == null || look.midgroundModels == null || look.midgroundModels.Length == 0) return;

            // Original GetPrefab: a number 0..1+Blank; 0 = right side, 1 = left side, anything else = no block.
            uint h = Hash(row);
            int pick = (int)(h % (uint)(2 + Mathf.Max(0, look.midgroundBlank)));
            if (pick > 1) return;
            var model = look.midgroundModels[(int)((h >> 8) % (uint)look.midgroundModels.Length)];
            if (model == null) return;
            float side = pick == 1 ? -1f : 1f;
            float t = ((h >> 16) & 0xffff) / 65535f;
            float x = Mathf.Lerp(look.midgroundMinX, look.midgroundMaxX, t) * side;

            var go = TileShapes.Place(transform, model, 0, MaterialFor(look), $"Midground {row}", false);
            if (go == null) return;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            blocks[row] = new Block { t = go.transform, x = x, height = look.midgroundHeight, rise = Mathf.Max(0f, look.midgroundRise) };
        }

        static uint Hash(int row)
        {
            uint h = (uint)row * 2654435761u + 0x9e3779b9u;
            h ^= h >> 15; h *= 0x2c1b3c6du;
            h ^= h >> 12; h *= 0x297a2d39u;
            h ^= h >> 15;
            return h;
        }

        /// <summary>The look's Midground Material, or an unlit Mid Fog material with its Tile Fragile texture.</summary>
        Material MaterialFor(TrackTheme look)
        {
            if (look.midgroundMaterial != null) return look.midgroundMaterial;
            if (materials.TryGetValue(look, out var m)) return m;
            if (missingShader) return look.tileFragile;
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                missingShader = true;
                Debug.LogWarning($"RTXOn: shader '{ShaderName}' not found (Shaders/Resources/RTXOnMidground.shader), so the midground uses Tile Fragile as it is.");
                return look.tileFragile;
            }
            m = new Material(shader) { name = look.name + " Midground (runtime)" };
            var src = look.tileFragile;
            Texture tex = null;
            if (src != null) tex = src.HasProperty("_BaseMap") ? src.GetTexture("_BaseMap") : src.mainTexture;
            if (tex != null) m.SetTexture("_MainTex", tex);
            m.SetColor("_Tint", look.midgroundTint);
            m.SetColor("_FogColor", look.midFogColor);
            m.SetFloat("_FogStart", look.midFogStart);
            m.SetFloat("_FogEnd", look.midFogEnd);
            materials[look] = m;
            return m;
        }
    }
}
