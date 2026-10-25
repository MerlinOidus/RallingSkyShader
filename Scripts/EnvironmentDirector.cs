using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RTXOn
{
    /// <summary>
    /// Drives sun, sky, fog, ambient light, neon glow and post-processing from the level's theme changes.
    /// Blends are tied to the ball's distance (rows), so a sunset always happens at the same place in the song.
    /// Works on runtime copies of the sky, materials and volume profile, so play mode never edits your assets.
    /// </summary>
    public class EnvironmentDirector : MonoBehaviour
    {
        public Light sun;
        public Volume volume;
        [Tooltip("How often (seconds) ambient light and reflections are refreshed while blending.")]
        public float environmentRefresh = 0.2f;
        [Tooltip("Editor only: while playing, changes to the theme's Environments in the Inspector show at once. " +
                 "Edits made in Play mode stay in the theme asset after you stop.")]
        public bool liveEdit = true;

        readonly List<ThemeChange> schedule = new List<ThemeChange>();
        /// <summary>A glowing material: its emission before the boost, and whether it is edge glow (night only).</summary>
        class Neon
        {
            public Material material;
            public Color baseColor;
            public bool edge;
            public float minEdge; // jump pads keep a little glow by day, so they still read as pads
        }

        readonly List<Neon> neon = new List<Neon>();
        List<EnvironmentPreset> presets;

        EnvironmentPreset from;
        EnvironmentPreset to;
        EnvironmentPreset current;
        EnvironmentPreset settled; // the theme's environment the world sits in (not a copy), for live editing
        string settledJson;
        bool liveRefresh;
        Quaternion fromRot, toRot;
        float blendStart, blendEnd;
        bool blending;
        int nextChange;
        float refreshTimer;
        // Sun and moon angles set by theme triggers (Sun angle / Moon angle); until then each environment's own.
        bool sunTurned, moonTurned;
        Quaternion sunAngle, moonAngle;

        Material sky;
        const string SkyShader = "RTXOn/SkyGradient";
        // The sky's HDRI layers: blending from skyFrom to skyTo (the same environment when not blending).
        EnvironmentPreset skyFrom, skyTo;
        float skyBlend;
        Bloom bloom;
        const float BloomClamp = 12f; // brightest value bloom takes from a pixel (the neon edges stay below it)
        ColorAdjustments colorAdjust;

        public EnvironmentPreset Current => current;

        /// <param name="runtimeTheme">The track's runtime theme copy (its materials are safe to change).</param>
        public void Init(TrackTheme runtimeTheme, IList<ThemeChange> changes)
        {
            presets = runtimeTheme != null && runtimeTheme.environments != null && runtimeTheme.environments.Count > 0
                ? runtimeTheme.environments
                : new List<EnvironmentPreset> { EnvironmentPreset.Day(), EnvironmentPreset.Sunset(), EnvironmentPreset.Night() };

            if (sun == null) sun = RenderSettings.sun;
            if (sun == null)
                foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Directional) { sun = l; break; }

            // An environment with an HDRI needs the RTX ON sky (it draws the HDRIs and fades between them), also when
            // the scene's Skybox Material was set to another sky by hand.
            bool anyHdri = false;
            foreach (var p in presets) if (p != null && p.skyHdri != null) anyHdri = true;
            if (anyHdri && (RenderSettings.skybox == null || RenderSettings.skybox.shader == null || RenderSettings.skybox.shader.name != SkyShader))
            {
                var shader = Shader.Find(SkyShader);
                if (shader != null) RenderSettings.skybox = new Material(shader) { name = "RTX ON Sky" };
                else Debug.LogWarning($"RTXOn: shader '{SkyShader}' not found, so the HDRIs of the environments are not shown. Run RTX ON > Build Gameplay Scene.");
            }
            if (RenderSettings.skybox != null)
            {
                sky = new Material(RenderSettings.skybox) { name = RenderSettings.skybox.name + " (runtime)" };
                RenderSettings.skybox = sky;
            }

            if (volume != null)
            {
                var profile = volume.profile; // getting .profile makes a runtime copy of the shared profile
                profile.TryGet(out bloom);
                profile.TryGet(out colorAdjust);
                // A tiny very bright spot (a chrome shard catching the sun) must not bloom over the whole screen.
                if (bloom != null) bloom.clamp.Override(BloomClamp);
            }
            var cam = Camera.main;
            if (cam != null) cam.GetUniversalAdditionalCameraData().stopNaN = true; // a broken pixel must not spread through bloom

            neon.Clear();
            if (runtimeTheme != null)
            {
                // Edge glow: lines on the floor, fragile cracks, jump pads, hazard edges, moving platforms.
                AddNeon(runtimeTheme.floor, true);
                AddNeon(runtimeTheme.floorSide, true);
                AddNeon(runtimeTheme.fragile, true);
                AddNeon(runtimeTheme.fragile1, true);
                AddNeon(runtimeTheme.fragile2, true);
                AddNeon(runtimeTheme.fragileChecker, true);
                AddNeon(runtimeTheme.fragileRow, true);
                AddNeon(runtimeTheme.fragileColumn, true);
                AddNeon(runtimeTheme.fragileSide, true);
                AddNeon(runtimeTheme.jump, true, 0.35f);
                AddNeon(runtimeTheme.hazard, true);
                AddNeon(runtimeTheme.mover, true);
                AddNeon(runtimeTheme.moverAuto, true);
                // Tile Type atlases (original floor meshes): their Emission glows day and night, boosted by Neon Boost.
                AddNeon(runtimeTheme.tileGeneral, false);
                AddNeon(runtimeTheme.tileFragile, false);
                AddNeon(runtimeTheme.tileFragileActive, false);
                AddNeon(runtimeTheme.tileMover, false);
                AddNeon(runtimeTheme.tileMoverAuto, false);
                // Enemies: their glow map (T_EnemyGlow) lights up at sunset and night, like the edge glow.
                if (HasGlowMap(runtimeTheme.modelMaterial)) AddNeon(runtimeTheme.modelMaterial, true);
                // Always glowing: pickups and the mover arrow.
                AddNeon(runtimeTheme.gem, false);
                AddNeon(runtimeTheme.star, false);
                AddNeon(runtimeTheme.moverArrow, false);
            }

            schedule.Clear();
            if (changes != null)
                foreach (var c in changes)
                    if (c != null && (!string.IsNullOrEmpty(c.preset) || c.turnSun || c.turnMoon)) schedule.Add(c);
            schedule.Sort((a, b) => a.row.CompareTo(b.row));

            // Start in the preset of the first change at row 0, else the first preset.
            current = presets[0].Clone();
            nextChange = 0;
            sunTurned = moonTurned = false;
            while (nextChange < schedule.Count && schedule[nextChange].row <= 0)
            {
                var change = schedule[nextChange++];
                TakeAngles(change);
                if (string.IsNullOrEmpty(change.preset)) continue;
                var p = Find(change.preset);
                if (p != null) current = p.Clone();
            }
            settled = null;
            foreach (var p in presets)
                if (p != null && p.name == current.name) { settled = p; break; }
            settledJson = null;
            blending = false;
            skyFrom = skyTo = current;
            skyBlend = 0f;
            Apply(current, LightRotation(current));
            RefreshEnvironment();
        }

        /// <summary>
        /// Adds a material made after Init (another look, or a look's colour gradient) to the neon glow, at the
        /// current glow level.
        /// </summary>
        public void RegisterNeon(Material m)
        {
            if (m == null || !m.HasProperty("_EmissionColor")) return;
            foreach (var n in neon) if (n.material == m) return;
            AddNeon(m, false);
            if (current != null) ApplyNeon(neon[neon.Count - 1], current);
        }

        /// <summary>
        /// New emission (before the glow boost) for a glowing material, e.g. while a look change fades the track's
        /// colours. Applied at once with the current glow.
        /// </summary>
        public void SetNeonBase(Material m, Color baseColor)
        {
            if (m == null) return;
            foreach (var n in neon)
            {
                if (n.material != m) continue;
                n.baseColor = baseColor;
                if (current != null) ApplyNeon(n, current);
                return;
            }
            m.SetColor("_EmissionColor", baseColor);
        }

        static bool HasGlowMap(Material m) => m != null && m.HasProperty("_EmissionMap") && m.GetTexture("_EmissionMap") != null;

        void AddNeon(Material m, bool edge, float minEdge = 0f)
        {
            if (m == null || !m.HasProperty("_EmissionColor")) return;
            // Emission on, even if the material starts dark (a day look), so the glow can come up at night.
            m.EnableKeyword("_EMISSION");
            neon.Add(new Neon { material = m, baseColor = m.GetColor("_EmissionColor"), edge = edge, minEdge = minEdge });
        }

        static void ApplyNeon(Neon n, EnvironmentPreset p)
        {
            float k = p.neonBoost * (n.edge ? Mathf.Max(n.minEdge, p.EdgeGlow()) : 1f);
            n.material.SetColor("_EmissionColor", n.baseColor * k);
        }

        EnvironmentPreset Find(string presetName)
        {
            foreach (var p in presets)
                if (p != null && string.Equals(p.name, presetName, System.StringComparison.OrdinalIgnoreCase)) return p;
            Debug.LogWarning($"RTXOn: environment preset '{presetName}' not found in the theme.");
            return null;
        }

        void Update()
        {
            if (current == null) return;
            var gm = GameManager.Instance;
            if (gm == null || gm.ball == null) return;
            float z = gm.ball.Position.z;

            // Start the next change once the ball reaches its row.
            while (nextChange < schedule.Count && z >= schedule[nextChange].row)
            {
                var change = schedule[nextChange++];
                TakeAngles(change);
                // No environment ("Keep"): only the sun or moon turns, towards the environment it is already going to.
                var target = string.IsNullOrEmpty(change.preset) ? (blending && to != null ? to : current) : Find(change.preset);
                if (target == null) continue;
                from = current.Clone();
                fromRot = sun != null ? sun.transform.rotation : LightRotation(current);
                to = target;
                toRot = LightRotation(target);
                blendStart = change.row;
                blendEnd = change.row + Mathf.Max(0f, change.rows);
                blending = true;
            }

            if (!blending)
            {
                LiveEdit();
                return;
            }

            float t = blendEnd > blendStart ? Mathf.Clamp01((z - blendStart) / (blendEnd - blendStart)) : 1f;
            float s = t * t * (3f - 2f * t);
            current = EnvironmentPreset.Lerp(from, to, s);
            skyFrom = from;
            skyTo = to;
            skyBlend = s;
            // Sun <-> moon: don't drag the sun back across the sky. Fade it out, jump, fade the moon in.
            bool swap = Mathf.Abs(to.stars - from.stars) > 0.5f;
            if (swap) Apply(current, s < 0.5f ? fromRot : toRot, Mathf.Abs(s * 2f - 1f));
            else Apply(current, Quaternion.Slerp(fromRot, toRot, s));

            refreshTimer -= Time.unscaledDeltaTime;
            if (refreshTimer <= 0f || t >= 1f)
            {
                refreshTimer = environmentRefresh;
                RefreshEnvironment();
            }
            if (t >= 1f)
            {
                blending = false;
                skyFrom = skyTo = to;
                skyBlend = 0f;
                settled = to;
                settledJson = null;
            }
        }

        /// <summary>
        /// Editor only: when the environment the world sits in was changed in the Inspector, shows it at once.
        /// (While blending, the target environment is read live anyway.)
        /// </summary>
        void LiveEdit()
        {
#if UNITY_EDITOR
            if (!liveEdit || settled == null) return;
            var json = JsonUtility.ToJson(settled);
            if (json != settledJson)
            {
                bool first = settledJson == null;
                settledJson = json;
                if (!first)
                {
                    current = settled.Clone();
                    skyFrom = skyTo = current;
                    skyBlend = 0f;
                    Apply(current, LightRotation(current));
                    liveRefresh = true;
                }
            }
            // Ambient light and reflections are slower to rebuild: at most every Environment Refresh seconds.
            if (!liveRefresh) return;
            refreshTimer -= Time.unscaledDeltaTime;
            if (refreshTimer > 0f) return;
            refreshTimer = environmentRefresh;
            liveRefresh = false;
            RefreshEnvironment();
#endif
        }

        /// <summary>Remembers the Sun angle / Moon angle a trigger sets; they stay until another trigger turns them.</summary>
        void TakeAngles(ThemeChange change)
        {
            if (change.turnSun)
            {
                sunTurned = true;
                sunAngle = Quaternion.Euler(change.sun.x, change.sun.y, 0f);
            }
            if (change.turnMoon)
            {
                moonTurned = true;
                moonAngle = Quaternion.Euler(change.moon.x, change.moon.y, 0f);
            }
        }

        /// <summary>
        /// Where the light of an environment comes from: a night (starry) environment is lit by the moon, the others by
        /// the sun; each at the angle a trigger gave it, else the environment's own angle.
        /// </summary>
        Quaternion LightRotation(EnvironmentPreset p) => LightRotation(p, sunTurned, sunAngle, moonTurned, moonAngle);

        public static Quaternion LightRotation(EnvironmentPreset p, bool sunTurned, Quaternion sunAngle, bool moonTurned, Quaternion moonAngle)
        {
            if (IsNight(p)) return moonTurned ? moonAngle : Quaternion.Euler(p.sunEuler);
            return sunTurned ? sunAngle : Quaternion.Euler(p.sunEuler);
        }

        /// <summary>True for an environment lit by the moon (one with stars).</summary>
        public static bool IsNight(EnvironmentPreset p) => p != null && p.stars > 0.5f;

        void Apply(EnvironmentPreset p, Quaternion sunRotation, float lightScale = 1f)
        {
            if (sun != null)
            {
                sun.transform.rotation = sunRotation;
                sun.color = p.sunColor;
                sun.intensity = p.sunIntensity * lightScale;
            }

            if (sky != null)
            {
                sky.SetColor("_TopColor", p.skyTop);
                sky.SetColor("_HorizonColor", p.skyHorizon);
                sky.SetColor("_BottomColor", p.skyBottom);
                sky.SetColor("_SunColor", p.sunDisc * lightScale);
                sky.SetFloat("_SunSize", p.sunDiscSize);
                sky.SetFloat("_Stars", p.stars);
                sky.SetVector("_SunDir", -(sunRotation * Vector3.forward));
                SetHdri("A", skyFrom ?? p);
                SetHdri("B", skyTo ?? p);
                sky.SetFloat("_HdriBlend", skyBlend);
            }

            RenderSettings.fogColor = p.fogColor;
            RenderSettings.fogDensity = p.fogDensity;
            // Ambient light from the HDRI sky (rebuilt by RefreshEnvironment) or from the environment's colours.
            var main = skyBlend < 0.5f ? skyFrom : skyTo;
            bool fromHdri = sky != null && main != null && main.skyHdri != null && main.hdriLighting;
            RenderSettings.ambientMode = fromHdri ? AmbientMode.Skybox : AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = p.ambientSky;
            RenderSettings.ambientEquatorColor = p.ambientEquator;
            RenderSettings.ambientGroundColor = p.ambientGround;

            foreach (var n in neon) ApplyNeon(n, p);

            if (bloom != null) bloom.intensity.Override(p.bloomIntensity * GraphicsQuality.BloomStrength); // Settings > Bloom Strength
            if (colorAdjust != null) colorAdjust.postExposure.Override(p.postExposure);
        }

        void SetHdri(string layer, EnvironmentPreset p)
        {
            if (!sky.HasProperty("_Hdri" + layer)) return; // another sky shader
            sky.SetTexture("_Hdri" + layer, p.skyHdri);
            sky.SetFloat("_HdriMix" + layer, p.skyHdri != null ? 1f : 0f);
            sky.SetFloat("_HdriExposure" + layer, p.hdriExposure);
            sky.SetFloat("_HdriRotation" + layer, p.hdriRotation);
            sky.SetColor("_HdriTint" + layer, p.hdriTint);
        }

        static void RefreshEnvironment()
        {
            // Rebuilds the ambient probe and the default (sky) reflection from the current settings.
            DynamicGI.UpdateEnvironment();
        }
    }
}
