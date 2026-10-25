using System;
using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// One look for the world: sun/moon, sky, fog, ambient light and how hard the neon glows.
    /// A level switches between presets at given rows ("themes" in level.json); the
    /// EnvironmentDirector blends them over a few rows, so the sun visibly moves.
    /// </summary>
    [Serializable]
    public class EnvironmentPreset
    {
        public string name = "Day";

        [Header("Sun / moon (directional light)")]
        public Vector3 sunEuler = new Vector3(42f, -35f, 0f);
        public Color sunColor = new Color(1f, 0.95f, 0.86f);
        public float sunIntensity = 1.6f;

        [Header("Sky")]
        public Color skyTop = new Color(0.2f, 0.45f, 0.88f);
        public Color skyHorizon = new Color(0.82f, 0.92f, 1f);
        public Color skyBottom = new Color(0.74f, 0.86f, 0.98f);
        [ColorUsage(false, true)] public Color sunDisc = new Color(6f, 5.6f, 4.8f);
        [Range(0.001f, 0.2f)] public float sunDiscSize = 0.03f;
        [Range(0f, 1f)] public float stars = 0f;

        [Header("HDRI sky (optional)")]
        [Tooltip("An HDRI (Texture Shape = Cube) shown as the sky instead of the colours above, with its own sun and " +
                 "stars. Day, Sunset and Night can each have one; a theme change fades from one to the next. Empty = the gradient sky.")]
        public Cubemap skyHdri;
        [Range(0f, 8f)] public float hdriExposure = 1f;
        [Tooltip("Turns the HDRI around (degrees), e.g. to put its sun where the Sun / moon light comes from.")]
        [Range(0f, 360f)] public float hdriRotation = 0f;
        public Color hdriTint = Color.white;
        [Tooltip("On: ambient light comes from the HDRI (it matches the sky). Off: from the Ambient colours below.")]
        public bool hdriLighting = true;

        [Header("Fog and ambient")]
        public Color fogColor = new Color(0.74f, 0.86f, 0.98f);
        public float fogDensity = 0.018f;
        public Color ambientSky = new Color(0.62f, 0.76f, 0.95f);
        public Color ambientEquator = new Color(0.55f, 0.64f, 0.75f);
        public Color ambientGround = new Color(0.3f, 0.34f, 0.42f);

        [Header("Glow")]
        [Tooltip("Multiplies the emission of the neon lines, gems and jump pads.")]
        public float neonBoost = 1f;
        [Tooltip("How much the edge glow (T_EdgeGlow on floors and hazards, T_FragileGlow, T_JumpGlow) lights up, 0..1. " +
                 "-1 = automatic: off by day, a little at sunset, full at night, so the edges light up by themselves as night falls.")]
        [Range(-1f, 1f)] public float edgeGlow = -1f;
        public float postExposure = 0.2f;
        public float bloomIntensity = 0.9f;

        public EnvironmentPreset Clone() => (EnvironmentPreset)MemberwiseClone();

        /// <summary>Edge glow 0..1, with -1 (automatic) worked out from the preset's name or how bright its sun is.</summary>
        public float EdgeGlow()
        {
            if (edgeGlow >= 0f) return edgeGlow;
            if (string.Equals(name, "Day", StringComparison.OrdinalIgnoreCase)) return 0f;
            if (string.Equals(name, "Sunset", StringComparison.OrdinalIgnoreCase)) return 0.35f;
            if (string.Equals(name, "Night", StringComparison.OrdinalIgnoreCase)) return 1f;
            return Mathf.Clamp01((1.6f - sunIntensity) / 1.15f);
        }

        /// <summary>Blends every value except the sun rotation (the director slerps that itself).</summary>
        public static EnvironmentPreset Lerp(EnvironmentPreset a, EnvironmentPreset b, float t)
        {
            var p = a.Clone();
            p.name = t < 0.5f ? a.name : b.name;
            p.sunEuler = b.sunEuler;
            p.sunColor = Color.Lerp(a.sunColor, b.sunColor, t);
            p.sunIntensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, t);
            p.skyTop = Color.Lerp(a.skyTop, b.skyTop, t);
            p.skyHorizon = Color.Lerp(a.skyHorizon, b.skyHorizon, t);
            p.skyBottom = Color.Lerp(a.skyBottom, b.skyBottom, t);
            p.sunDisc = Color.Lerp(a.sunDisc, b.sunDisc, t);
            p.sunDiscSize = Mathf.Lerp(a.sunDiscSize, b.sunDiscSize, t);
            p.stars = Mathf.Lerp(a.stars, b.stars, t);
            p.fogColor = Color.Lerp(a.fogColor, b.fogColor, t);
            p.fogDensity = Mathf.Lerp(a.fogDensity, b.fogDensity, t);
            p.ambientSky = Color.Lerp(a.ambientSky, b.ambientSky, t);
            p.ambientEquator = Color.Lerp(a.ambientEquator, b.ambientEquator, t);
            p.ambientGround = Color.Lerp(a.ambientGround, b.ambientGround, t);
            p.neonBoost = Mathf.Lerp(a.neonBoost, b.neonBoost, t);
            p.edgeGlow = Mathf.Lerp(a.EdgeGlow(), b.EdgeGlow(), t);
            p.postExposure = Mathf.Lerp(a.postExposure, b.postExposure, t);
            p.bloomIntensity = Mathf.Lerp(a.bloomIntensity, b.bloomIntensity, t);
            return p;
        }

        // ---------- Massif presets ----------

        public static EnvironmentPreset Day() => new EnvironmentPreset { name = "Day" };

        /// <summary>Low warm sun in front of the camera, pink-orange horizon, long shadows.</summary>
        public static EnvironmentPreset Sunset() => new EnvironmentPreset
        {
            name = "Sunset",
            sunEuler = new Vector3(9f, 200f, 0f),
            sunColor = new Color(1f, 0.62f, 0.36f),
            sunIntensity = 1.35f,
            skyTop = new Color(0.27f, 0.25f, 0.55f),
            skyHorizon = new Color(1f, 0.62f, 0.45f),
            skyBottom = new Color(0.93f, 0.62f, 0.58f),
            sunDisc = new Color(7f, 3.6f, 1.6f),
            sunDiscSize = 0.045f,
            stars = 0f,
            fogColor = new Color(0.93f, 0.62f, 0.58f),
            fogDensity = 0.02f,
            ambientSky = new Color(0.55f, 0.45f, 0.62f),
            ambientEquator = new Color(0.75f, 0.5f, 0.45f),
            ambientGround = new Color(0.28f, 0.22f, 0.3f),
            neonBoost = 1.5f,
            postExposure = 0.15f,
            bloomIntensity = 1.1f,
        };

        /// <summary>Cold moonlight, starry navy sky, neon lines glowing hard.</summary>
        public static EnvironmentPreset Night() => new EnvironmentPreset
        {
            name = "Night",
            sunEuler = new Vector3(38f, 25f, 0f),
            sunColor = new Color(0.55f, 0.66f, 1f),
            sunIntensity = 0.45f,
            skyTop = new Color(0.01f, 0.02f, 0.07f),
            skyHorizon = new Color(0.07f, 0.12f, 0.26f),
            skyBottom = new Color(0.05f, 0.08f, 0.18f),
            sunDisc = new Color(2.2f, 2.4f, 3f),
            sunDiscSize = 0.025f,
            stars = 1f,
            fogColor = new Color(0.05f, 0.08f, 0.18f),
            fogDensity = 0.024f,
            ambientSky = new Color(0.12f, 0.16f, 0.3f),
            ambientEquator = new Color(0.08f, 0.1f, 0.18f),
            ambientGround = new Color(0.03f, 0.03f, 0.06f),
            neonBoost = 2.6f,
            postExposure = 0.1f,
            bloomIntensity = 1.5f,
        };
    }
}
