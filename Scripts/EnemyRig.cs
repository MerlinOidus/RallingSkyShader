using System.Collections.Generic;
using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// Plays the Rolling Sky 1.0 code animation (TileAnimator) on an original enemy mesh: its "anim", "altAnim",
    /// "part" and "state" children are moved exactly like the original, from the ball's distance.
    /// Only the look: the hazard keeps the remake's own collider and timing.
    /// </summary>
    public class EnemyRig : TrackBehaviour
    {
        public enum Kind { None, Riser, TreeSmall1, TreeSmall2, TreeTall, Palm, Tower, Pyramid }

        public Kind kind;
        Transform anim;
        Transform altAnim;
        Transform state1, state2;
        Transform[] parts;
        bool landed = true; // no shake for a pyramid that is already down when the level starts

        /// <summary>The original animation for a model, read from its name (01_enemyRiser, 10_enemyTreeSmall1, ...).</summary>
        public static Kind KindOf(GameObject model)
        {
            if (model == null) return Kind.None;
            string n = model.name;
            if (n.Contains("TreeSmall2")) return Kind.TreeSmall2;
            if (n.Contains("TreeSmall")) return Kind.TreeSmall1;
            if (n.Contains("TreeTall")) return Kind.TreeTall;
            if (n.Contains("TreePalm") || n.Contains("enemyPalm")) return Kind.Palm;
            if (n.Contains("enemyTower")) return Kind.Tower;
            if (n.Contains("PyramidSmall")) return Kind.Pyramid;
            if (n.Contains("enemyRiser")) return Kind.Riser;
            return Kind.None;
        }

        /// <summary>Tile.CreateAnimator: finds the moving pieces and puts them in their starting pose.</summary>
        public void Setup(Kind k)
        {
            kind = k;
            anim = Find(transform, "anim");
            altAnim = Find(transform, "altAnim");
            state1 = Find(transform, "state1");
            state2 = Find(transform, "state2");
            parts = FindParts(transform, k == Kind.Tower);

            switch (kind)
            {
                case Kind.Riser:
                case Kind.TreeSmall1:
                case Kind.TreeSmall2:
                case Kind.TreeTall:
                    foreach (var p in parts) SetY(p, 0f);
                    break;
                case Kind.Palm:
                    for (int i = 0; i < parts.Length; i++)
                    {
                        if (i < 10) parts[i].localPosition = Vector3.zero;
                        else parts[i].localEulerAngles = Vector3.zero;
                    }
                    break;
                case Kind.Tower:
                    for (int i = 0; i < parts.Length; i++)
                    {
                        if (i < 4) SetY(parts[i], i != 3 ? 0.01f : 0.125f);
                        else parts[i].localEulerAngles = new Vector3(0f, 0f, -(i < 8 ? 35f : (i >= 12 ? 30f : 20f)));
                    }
                    break;
                case Kind.Pyramid:
                    if (altAnim != null) { altAnim.localPosition = Vector3.zero; altAnim.localEulerAngles = Vector3.zero; }
                    if (anim != null) anim.localEulerAngles = Vector3.zero;
                    break;
            }
            Update();
        }

        /// <summary>Rows earlier than the original that trees, palms, towers and pyramids rise.</summary>
        public const float TreeLeadRows = 1f;

        void Update()
        {
            if (parts == null) return;
            float d = Ahead;
            // Adrian 2026-10-24: trees finished rising just after the ball had passed; they rise 1 row earlier
            // (risers keep the original timing, which he found right).
            if (kind != Kind.Riser) d -= TreeLeadRows;
            switch (kind)
            {
                case Kind.Riser: RiseRiser(d); break;
                case Kind.TreeSmall1:
                case Kind.TreeTall: RiseTree(d, false); break;
                case Kind.TreeSmall2: RiseTree(d, true); break;
                case Kind.Palm: RisePalmTree(d); break;
                case Kind.Tower: RiseTower(d); break;
                case Kind.Pyramid: RisePyramid(d); break;
            }
        }

        void RiseRiser(float d)
        {
            float value = P01(d, 5.65f, 2.65f, out _, out bool above);
            int n = parts.Length;
            for (int i = 0; i < n; i++)
            {
                float k = n - (i + 1f);
                if (i == 0) k += 1.4444447f;
                SetY(parts[i], P01(value, 0.225f * k, 1f) * 0.625f);
            }
            // SwitchAddition: passive parts (state1) give way to the risen block (state2).
            if (state1 != null) state1.gameObject.SetActive(!above);
            if (state2 != null) state2.gameObject.SetActive(above);
        }

        void RiseTree(float d, bool special)
        {
            float rise = special ? 0.85f : 0.4f;
            float partRise = special ? 0.075f : 0.3f;
            float overshoot = special ? 1.6666666f : 1.1666666f;
            float v = P01(d, 12.65f, 2.65f, out bool below, out _);
            int n = parts.Length;
            if (below)
            {
                foreach (var p in parts) SetY(p, 0f);
                SetY(anim, 0f);
                return;
            }
            if (special)
            {
                SetY(anim, Impact(Mathf.Clamp01(v), 0.125f, 0.25f) * rise);
                float o = v >= 0.25f ? Overshoot(P01(v, 0.25f, 0.4f), overshoot) : 0f;
                foreach (var p in parts) SetY(p, o * partRise);
                return;
            }
            float step = 1f / (n + 2);
            for (int j = 0; j <= n; j++)
            {
                if (j == n)
                {
                    // Before its window the trunk waits at its start (the original leaves it where the prefab had it).
                    SetY(anim, Impact(P01(v, step * j, 1f), 0.1f, 0.25f) * rise);
                }
                else
                {
                    SetY(parts[j], Overshoot(P01(v, step * j, step * (j + 1f)), overshoot) * partRise);
                }
            }
        }

        void RisePalmTree(float d)
        {
            float value = P01(d, 6f, 4f, out bool below, out _);
            for (int i = 0; i < 10 && i < parts.Length; i++)
                SetY(parts[i], below ? 0f : P01(value, 0.1f * i, 0.1f * (i + 1f)) * 0.2f);

            float value2 = P01(d, 6f, 2.25f, out below, out _);
            float z = below ? 0f : Impact(value2, 0.1f, 0.5f) * 120f;
            for (int j = 10; j < parts.Length; j++)
                parts[j].localEulerAngles = new Vector3(0f, 0f, z);
        }

        void RiseTower(float d)
        {
            float value = P01(d, 8.65f, 2.65f, out bool below, out _);
            const int count = 4;
            const float step = 0.2f;
            for (int i = 0; i < count && i < parts.Length; i++)
            {
                bool top = i == count - 1;
                float p = P01(value, step * i, step * (i + (top ? 0.5f : 1f)), out bool below2, out _);
                if (below || below2)
                {
                    SetY(parts[i], top ? 0.125f : 0.01f);
                    p = 0f;
                }
                else
                {
                    float percent = Impact(p, 0.1f, 0.75f);
                    SetY(parts[i], top ? Mathf.LerpUnclamped(0.125f, 0.25f, percent) : Mathf.LerpUnclamped(0.01f, 0.5f, percent));
                }
                if (top)
                {
                    float fold = -(1f - p);
                    for (int j = count; j < parts.Length; j++)
                    {
                        float a = j >= 8 ? (j >= 12 ? (j >= 16 ? 0f : 30f) : 20f) : 35f;
                        parts[j].localEulerAngles = new Vector3(0f, 0f, a * fold);
                    }
                }
            }
            if (below || parts.Length <= 16) return;

            // The top piece turns to watch the ball.
            var gm = GameManager.Instance;
            if (gm == null || gm.ball == null) return;
            float xDistance = -(parts[16].position.x - gm.ball.Position.x);
            float yDistance = -d;
            float angle;
            if (yDistance == 0f) angle = xDistance <= 0f ? 90f : -90f;
            else
            {
                angle = Mathf.Atan(xDistance / yDistance) * Mathf.Rad2Deg;
                if (yDistance > 0f) angle += 180f;
            }
            parts[16].localEulerAngles = new Vector3(0f, Mathf.Clamp(angle, -90f, 90f), 0f);
        }

        void RisePyramid(float d)
        {
            const float jump = 1.25f;      // small pyramid (alternateAnim false)
            const float stepRise = 0.1875f;
            float stepPeak = stepRise * 1.3f;
            float v = P01(d, 10f, 1f, out bool below, out bool above);
            int n = parts.Length;
            if (below)
            {
                for (int i = 1; i < n; i++) SetY(parts[i], 0f);
                SetY(anim, 0f);
                return;
            }
            if (above)
            {
                for (int i = 1; i < n; i++) SetY(parts[i], stepRise);
                SetY(anim, 0f);
                if (altAnim != null) altAnim.localEulerAngles = Vector3.zero;
                if (!landed) CameraRig.Shake(0.25f); // the original shakes the camera as it lands
                landed = true;
                return;
            }
            landed = false;
            SetY(anim, PulseWeighted(v, 0.85f, 0.5f) * jump);
            if (v < 0.4f)
            {
                float value = v / 0.4f;
                float w = 1f / (n - 1);
                for (int j = 1; j < n; j++) SetY(parts[j], P01(value, w * j, w * (j + 1f)) * stepPeak);
            }
            else if (v < 0.8f)
            {
                float y = Mathf.LerpUnclamped(stepPeak, stepRise, (v - 0.4f) / 0.4f);
                for (int k = 1; k < n; k++) SetY(parts[k], y);
            }
            else
            {
                for (int l = 1; l < n; l++) SetY(parts[l], stepRise);
            }
        }

        // ---- Rolling Sky 1.0 helpers (MathUtils / FloatAnim) ----

        /// <summary>MathUtils.ToPercent01: 0..1 from a to b (b may be below a), clamped.</summary>
        internal static float P01(float value, float a, float b, out bool below, out bool above)
        {
            float t = (value - a) / (b - a);
            below = t < 0f;
            above = t > 1f;
            return Mathf.Clamp01(t);
        }

        internal static float P01(float value, float a, float b) => P01(value, a, b, out _, out _);

        /// <summary>FloatAnim.Impact: rises to 1, then bounces back by <paramref name="rebound"/>.</summary>
        internal static float Impact(float value, float rebound, float whenToFirstHit1)
        {
            if (rebound < 0f || rebound > 1f) rebound = 0.25f;
            float freq = 0.25f / whenToFirstHit1;
            float shift = -whenToFirstHit1;
            float add = 1f, div = 1f;
            float inv = 1f / rebound;
            if (value > whenToFirstHit1)
            {
                float k = Mathf.Pow(3f, (int)(whenToFirstHit1 * 4f) - 1) / 6f;
                freq = k / whenToFirstHit1;
                add = -inv;
                div = -inv;
            }
            return (Mathf.Sin((value + shift) * Mathf.PI * 2f * freq) + add) / div;
        }

        /// <summary>FloatAnim.Overshoot.</summary>
        static float Overshoot(float value, float overshootTo, float whenToPass1 = 0.5f)
        {
            float pulse = PulseWeighted(value, whenToPass1, 0.5f);
            return value < whenToPass1 ? overshootTo * pulse : (overshootTo - 1f) * pulse + 1f;
        }

        /// <summary>FloatAnim.CalculatePulseWeighted.</summary>
        internal static float PulseWeighted(float value, float crest, float smooth)
        {
            if (crest == 0.5f || crest >= 1f || crest <= 0f)
                return (-Mathf.Cos(value * Mathf.PI * 2f) + 1f) / 2f;
            smooth = Mathf.Clamp01(smooth);
            float rise = value * Mathf.PI / crest;
            float fall = (1f + (value - crest) / (1f - crest)) * Mathf.PI;
            float radians;
            if (value < crest)
                radians = crest < 0.5f && smooth > 0f ? Lerp01(rise, fall, Pct(value, (1f - smooth) * crest, crest)) : rise;
            else
                radians = crest > 0.5f && smooth > 0f ? Lerp01(rise, fall, Pct(value, crest, crest + (1f - crest) * smooth)) : fall;
            return (-Mathf.Cos(radians) + 1f) / 2f;
        }

        static float Pct(float v, float a, float b) => (v - a) / (b - a);
        static float Lerp01(float a, float b, float w) { w = Mathf.Clamp01(w); return a * (1f - w) + b * w; }

        internal static void SetY(Transform t, float y)
        {
            if (t == null) return;
            var p = t.localPosition;
            p.y = y;
            t.localPosition = p;
        }

        internal static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        /// <summary>TransformUtils.FindAll("part"): hierarchy order, or by the number in the name (tower).</summary>
        static Transform[] FindParts(Transform root, bool sortByNumber)
        {
            var list = new List<Transform>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("part")) list.Add(t);
            if (sortByNumber) list.Sort((a, b) => Number(a.name).CompareTo(Number(b.name)));
            return list.ToArray();
        }

        static int Number(string name)
        {
            int n = 0;
            foreach (char c in name)
                if (c >= '0' && c <= '9') n = n * 10 + (c - '0');
            return n;
        }
    }
}
