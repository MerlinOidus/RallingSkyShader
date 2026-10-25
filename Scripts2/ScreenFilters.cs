using System;
using System.Collections.Generic;
using UnityEngine;

namespace RTXOn
{
    /// <summary>The screen filter triggers' effects (shader RTXOn/ScreenFilters).</summary>
    public enum ScreenEffect { Glitch, Grayscale, Negative, Chromatic, ScanLines, HueShift }

    /// <summary>
    /// Screen filter trigger: from <see cref="row"/> on, one effect fades to <see cref="strength"/> over
    /// <see cref="fade"/> rows of ball travel. <see cref="rows"/> above 0 makes it a burst: after that many rows it
    /// fades back out. Strength 0 turns the effect off. Different effects add up (Grayscale + Scan Lines...).
    /// </summary>
    [Serializable]
    public class FilterChange
    {
        public int row;
        [Tooltip("Glitch, Grayscale, Negative, Chromatic, ScanLines or HueShift.")]
        public string effect = nameof(ScreenEffect.Glitch);
        [Tooltip("0 = off, 1 = full.")]
        [Range(0f, 1f)] public float strength = 1f;
        [Tooltip("Rows the fade in (and out) takes. 0 = at once.")]
        public float fade = 2f;
        [Tooltip("Rows it stays before fading out again. 0 = stays until the next trigger of this effect.")]
        public float rows = 0f;
        [Tooltip("Glitch: how often it jumps. Scan Lines: how fast they scroll. Hue Shift: turns per second (0 = fixed opposite colours).")]
        public float speed = 1f;

        public ScreenEffect Effect
        {
            get => ScreenFilters.Parse(effect);
            set => effect = value.ToString();
        }

        public FilterChange Clone() => (FilterChange)MemberwiseClone();
    }

    /// <summary>
    /// Holds the strength of each screen effect and hands it to the shader as globals. <see cref="ScreenFilterFeature"/>
    /// draws the effects only while one is on, and only on <see cref="Target"/> (the game camera, or the level editor's
    /// 3D view while it previews), so menus and other cameras stay clean.
    /// </summary>
    public static class ScreenFilters
    {
        public const string ShaderName = "RTXOn/ScreenFilters";
        public const int Count = 6;
        /// <summary>Names to show, in <see cref="ScreenEffect"/> order.</summary>
        public static readonly string[] Names = { "Glitch", "Grayscale", "Negative", "Chromatic", "Scan Lines", "Hue Shift" };

        static readonly float[] values = new float[Count];
        static readonly float[] speeds = new float[Count];
        static readonly int FilterA = Shader.PropertyToID("_RTXFilterA");
        static readonly int FilterB = Shader.PropertyToID("_RTXFilterB");
        static readonly int FilterSpeed = Shader.PropertyToID("_RTXFilterSpeed");
        static readonly int FilterTime = Shader.PropertyToID("_RTXFilterTime");

        /// <summary>True while any effect is above 0.</summary>
        public static bool Active { get; private set; }
        /// <summary>The camera the effects are drawn on (null = every game camera).</summary>
        public static Camera Target { get; set; }

        public static string NameOf(ScreenEffect e) => Names[(int)e];

        public static ScreenEffect Parse(string name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                string key = name.Replace(" ", "");
                foreach (ScreenEffect e in Enum.GetValues(typeof(ScreenEffect)))
                    if (string.Equals(e.ToString(), key, StringComparison.OrdinalIgnoreCase)) return e;
            }
            return ScreenEffect.Glitch;
        }

        /// <summary>Sets every effect from the level's triggers at <paramref name="row"/> (the ball's row).</summary>
        public static void ApplyAt(IList<FilterChange> changes, float row)
        {
            for (int i = 0; i < Count; i++) values[i] = ValueAt(changes, (ScreenEffect)i, row, out speeds[i]);
            Push();
        }

        /// <summary>Level editor: only this trigger's effect, at its full strength.</summary>
        public static void Preview(FilterChange change)
        {
            Array.Clear(values, 0, Count);
            if (change != null)
            {
                int i = (int)change.Effect;
                values[i] = Mathf.Clamp01(change.strength);
                speeds[i] = change.speed;
            }
            Push();
        }

        /// <summary>Every effect off.</summary>
        public static void Clear()
        {
            Array.Clear(values, 0, Count);
            Push();
        }

        /// <summary>
        /// Strength of one effect at a row: the last trigger of that effect at or before the row fades from what the
        /// effect was when it started to its strength; a burst (rows above 0) fades back to 0 after its rows.
        /// </summary>
        public static float ValueAt(IList<FilterChange> changes, ScreenEffect effect, float row, out float speed)
        {
            speed = 1f;
            if (changes == null) return 0f;
            FilterChange current = null;
            float from = 0f;
            foreach (var c in changes) // sorted by row
            {
                if (c == null || c.Effect != effect) continue;
                if (c.row > row) break;
                from = current == null ? 0f : Segment(current, from, c.row);
                current = c;
            }
            if (current == null) return 0f;
            speed = current.speed;
            return Segment(current, from, row);
        }

        static float Segment(FilterChange c, float from, float row)
        {
            float target = Mathf.Clamp01(c.strength);
            float fade = Mathf.Max(0f, c.fade);
            float v = Mathf.Lerp(from, target, fade <= 0f ? 1f : Mathf.Clamp01((row - c.row) / fade));
            if (c.rows > 0f)
            {
                float end = c.row + fade + c.rows;
                if (row > end) v = Mathf.Lerp(target, 0f, fade <= 0f ? 1f : Mathf.Clamp01((row - end) / fade));
            }
            return v;
        }

        static void Push()
        {
            bool active = false;
            for (int i = 0; i < Count; i++) active |= values[i] > 0.001f;
            Active = active;
            Shader.SetGlobalVector(FilterA, new Vector4(values[0], values[1], values[2], values[3]));
            Shader.SetGlobalVector(FilterB, new Vector4(values[4], values[5], 0f, 0f));
            Shader.SetGlobalVector(FilterSpeed, new Vector4(speeds[0], speeds[4], speeds[5], 0f));
            Shader.SetGlobalFloat(FilterTime, Time.unscaledTime);
        }
    }
}
