using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RTXOn
{
    /// <summary>
    /// Settings > Graphics. PC: Low .. RTX (default RTX). Android: Low .. Ultra (default High), and RTX there is
    /// Ultra + Shadow: Ultra with hard shadows only close to the ball (<see cref="GraphicsQuality.MobileShadowRows"/>).
    /// </summary>
    public enum GraphicsLevel { Low, Medium, High, Ultra, RTX }

    /// <summary>Settings > Graphics > Bloom. Auto = on from Medium up (the quality preset decides).</summary>
    public enum BloomSetting { Auto, On, Off }

    /// <summary>
    /// Settings > Graphics > Upscaler (PC only): render fewer pixels and scale the picture up, for more FPS.
    /// FSR = AMD FidelityFX Super Resolution 1, STP = Unity's Spatial-Temporal Post-processing; both are built into URP.
    /// </summary>
    public enum UpscalerChoice { Off, FSR, STP }

    /// <summary>
    /// The graphics quality the player chose (Settings > Graphics), applied to every scene as it loads:
    /// shadows, anti-aliasing, bloom, the ball's live reflection, render scale and SSAO.
    /// Android (Adrian 2026-10-23): its own presets Low .. Ultra (High by default, no RTX), no shadows, and Dynamic
    /// Loading always on with at most <see cref="MobileMaxRows"/> rows ahead. Adrian 2026-10-24 added Ultra + Shadow
    /// (the RTX slot on Android): Ultra plus hard shadows from one cascade that only reach about
    /// <see cref="MobileShadowRows"/> rows past the ball.
    /// The Unity editor never changes the URP asset or its renderer (that would save into the project); render scale
    /// and SSAO only change in a build.
    /// </summary>
    public static class GraphicsQuality
    {
        const string Key = "RTXOn.Graphics";
        /// <summary>Android: Dynamic Loading builds at most this many rows ahead of the ball.</summary>
        public const int MobileMaxRows = 50;
        /// <summary>Android Ultra + Shadow: shadows reach about this many rows past the ball.</summary>
        public const int MobileShadowRows = 10;

        static readonly string[] PcNames = { "LOW", "MEDIUM", "HIGH", "ULTRA", "RTX" };
        static readonly string[] MobileNames = { "LOW", "MEDIUM", "HIGH", "ULTRA", "ULTRA+SHADOW" };

        const string BloomKey = "RTXOn.Bloom";
        const string BloomStrengthKey = "RTXOn.BloomStrength";
        const string UpscalerKey = "RTXOn.Upscaler";
        const string UpscaleModeKey = "RTXOn.UpscaleMode";

        public static readonly string[] BloomNames = { "AUTO", "ON", "OFF" };
        public static readonly string[] UpscalerNames = { "OFF", "FSR", "STP" };
        public static readonly string[] UpscaleModeNames = { "QUALITY", "BALANCED", "PERFORMANCE" };
        /// <summary>Render scale of each Upscale Mode (share of the screen's width and height actually rendered).</summary>
        static readonly float[] UpscaleScales = { 0.77f, 0.67f, 0.5f };

        static bool loaded;
        static GraphicsLevel level;

        /// <summary>An Android (or iOS) build, or the editor with Android as the build target (to preview it).</summary>
        public static bool Mobile
        {
            get
            {
#if UNITY_ANDROID || UNITY_IOS
                return true;
#else
                return Application.isMobilePlatform;
#endif
            }
        }

        public static GraphicsLevel Default => Mobile ? GraphicsLevel.High : GraphicsLevel.RTX;
        static GraphicsLevel Highest => GraphicsLevel.RTX; // Android: Ultra + Shadow

        /// <summary>The choices Settings shows, in order (index = (int)GraphicsLevel).</summary>
        public static string[] Names => Mobile ? MobileNames : PcNames;

        public static GraphicsLevel Level
        {
            get
            {
                if (!loaded)
                {
                    loaded = true;
                    level = (GraphicsLevel)PlayerPrefs.GetInt(Key, (int)Default);
                    if (level > Highest || level < GraphicsLevel.Low) level = Default;
                }
                return level;
            }
            set
            {
                loaded = true;
                level = value > Highest ? Highest : value < GraphicsLevel.Low ? GraphicsLevel.Low : value;
                PlayerPrefs.SetInt(Key, (int)level);
                PlayerPrefs.Save();
                Apply();
            }
        }

        public static BloomSetting BloomChoice
        {
            get => (BloomSetting)Mathf.Clamp(PlayerPrefs.GetInt(BloomKey, 0), 0, (int)BloomSetting.Off);
            set { PlayerPrefs.SetInt(BloomKey, (int)value); PlayerPrefs.Save(); Apply(); }
        }

        /// <summary>Settings > Graphics > Bloom Strength: 1 = as each environment sets it, 0 .. 2.</summary>
        public static float BloomStrength
        {
            get => Mathf.Clamp(PlayerPrefs.GetFloat(BloomStrengthKey, 1f), 0f, 2f);
            set { PlayerPrefs.SetFloat(BloomStrengthKey, Mathf.Clamp(value, 0f, 2f)); PlayerPrefs.Save(); Apply(); }
        }

        public static bool BloomOn => BloomChoice == BloomSetting.On || (BloomChoice == BloomSetting.Auto && Level >= GraphicsLevel.Medium);

        /// <summary>PC only; Android always renders without an upscaler.</summary>
        public static UpscalerChoice Upscaler
        {
            get => Mobile ? UpscalerChoice.Off : (UpscalerChoice)Mathf.Clamp(PlayerPrefs.GetInt(UpscalerKey, 0), 0, (int)UpscalerChoice.STP);
            set { PlayerPrefs.SetInt(UpscalerKey, (int)value); PlayerPrefs.Save(); Apply(); }
        }

        /// <summary>0 = Quality, 1 = Balanced, 2 = Performance.</summary>
        public static int UpscaleMode
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(UpscaleModeKey, 0), 0, UpscaleScales.Length - 1);
            set { PlayerPrefs.SetInt(UpscaleModeKey, Mathf.Clamp(value, 0, UpscaleScales.Length - 1)); PlayerPrefs.Save(); Apply(); }
        }

        /// <summary>Any shadows at all: PC from High up; Android only on Ultra + Shadow.</summary>
        public static bool ShadowsOn => Mobile ? Level == GraphicsLevel.RTX : Level >= GraphicsLevel.High;

        /// <summary>The shadow type a light gets: High and Android = hard shadows, Ultra / RTX = the light's own (soft).</summary>
        public static LightShadows ShadowType(LightShadows wanted)
        {
            if (!ShadowsOn || wanted == LightShadows.None) return LightShadows.None;
            return Mobile || Level == GraphicsLevel.High ? LightShadows.Hard : wanted;
        }

        /// <summary>Rows Dynamic Loading builds ahead: on Android at most <see cref="MobileMaxRows"/>.</summary>
        public static int RowsAhead(int wanted) => Mobile ? Mathf.Min(wanted, MobileMaxRows) : wanted;

        // ---------------- Applying ----------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            loaded = false;
            if (Mobile) Application.targetFrameRate = 60; // phones default to 30
            var go = new GameObject("Graphics Quality");
            Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            go.AddComponent<GraphicsQualityRunner>();
        }

        /// <summary>Applies the level now (Settings calls it on a change; every scene gets it as it loads).</summary>
        public static void Apply()
        {
            ApplyPipeline();
            ApplyScene();
        }

        static void ApplyScene()
        {
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) ApplyCamera(cam);
            foreach (var volume in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None)) ApplyVolume(volume);
            ApplyLightsAndProbes();
        }

        /// <summary>Lights and reflection probes: also re-checked every second (looks and the editor add new ones).</summary>
        internal static void ApplyLightsAndProbes()
        {
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.shadows == LightShadows.None) continue;
                var s = ShadowType(light.shadows);
                if (s != light.shadows) light.shadows = s;
            }
            foreach (var probe in Object.FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (probe.mode == ReflectionProbeMode.Realtime) ApplyProbe(probe);
        }

        static void ApplyCamera(Camera cam)
        {
            if (cam.targetTexture != null && cam.targetTexture.width <= 512) return; // small previews keep their look
            if (!cam.TryGetComponent<UniversalAdditionalCameraData>(out var data)) return;
            data.renderShadows = ShadowsOn;
            switch (Level)
            {
                case GraphicsLevel.Low:
                    data.antialiasing = AntialiasingMode.None;
                    break;
                case GraphicsLevel.Medium:
                    data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
                    break;
                case GraphicsLevel.High:
                    data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                    data.antialiasingQuality = AntialiasingQuality.Medium;
                    break;
                default:
                    data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                    data.antialiasingQuality = AntialiasingQuality.High;
                    break;
            }
        }

        static void ApplyVolume(Volume volume)
        {
            if (volume.sharedProfile == null) return;
            var profile = volume.profile; // a copy for this run: the project's profile is never changed
            if (profile.TryGet<Bloom>(out var bloom))
            {
                bloom.active = BloomOn;
                // Gameplay: EnvironmentDirector sets the intensity per environment, times BloomStrength. Elsewhere
                // (main menu, level editor) the profile's own intensity times BloomStrength.
                if (Object.FindAnyObjectByType<EnvironmentDirector>() == null &&
                    volume.sharedProfile.TryGet<Bloom>(out var original))
                    bloom.intensity.Override(original.intensity.value * BloomStrength);
                bloom.highQualityFiltering.overrideState = true;
                bloom.highQualityFiltering.value = Level >= GraphicsLevel.Ultra;
            }
            if (Level < GraphicsLevel.High)
            {
                if (profile.TryGet<MotionBlur>(out var blur)) blur.active = false;
                if (profile.TryGet<DepthOfField>(out var dof)) dof.active = false;
            }
        }

        /// <summary>
        /// The ball's probe box only holds the ball itself (box smaller than the ball's distance to the floor tile's
        /// centre). A big box made every tile and model inside it take the reflection rendered at the ball, and those
        /// near its edge popped between that and the sky's reflection as the ball moved (Adrian's "environment turns off").
        /// </summary>
        static void BallOnly(ReflectionProbe probe)
        {
            var box = Vector3.one * 0.6f;
            if (probe.size != box) probe.size = box;
            if (probe.center != Vector3.zero) probe.center = Vector3.zero;
            if (probe.blendDistance != 0f) probe.blendDistance = 0f;
        }

        /// <summary>The ball's live reflection: off on Low, updated less often on Medium / High, every frame above.</summary>
        static void ApplyProbe(ReflectionProbe probe)
        {
            var tick = probe.GetComponent<ProbeTicker>();
            if (tick == null)
            {
                tick = probe.gameObject.AddComponent<ProbeTicker>();
                tick.resolution = probe.resolution;
                tick.refreshMode = probe.refreshMode;
                tick.slicing = probe.timeSlicingMode;
            }
            probe.enabled = Level > GraphicsLevel.Low;
            if (probe.GetComponentInParent<BallController>() != null) BallOnly(probe);
            switch (Level)
            {
                case GraphicsLevel.Medium:
                    tick.Use(32, 10);
                    break;
                case GraphicsLevel.High:
                    tick.Use(64, 4);
                    break;
                case GraphicsLevel.Ultra:
                    tick.Use(Mathf.Min(128, tick.resolution), 0);
                    break;
                case GraphicsLevel.RTX when Mobile: // Ultra + Shadow
                    tick.Use(Mathf.Min(128, tick.resolution), 0);
                    break;
                case GraphicsLevel.RTX:
                    tick.Use(tick.resolution, 0); // as the scene was built
                    break;
            }
        }

        static float RenderScale
        {
            get
            {
                if (Mobile) return Level == GraphicsLevel.Low ? 0.6f : Level == GraphicsLevel.Medium ? 0.75f : Level == GraphicsLevel.High ? 0.85f : 1f;
                return Level == GraphicsLevel.Low ? 0.7f : Level == GraphicsLevel.Medium ? 0.85f : 1f;
            }
        }

        /// <summary>
        /// From the camera. Android: the camera is about 6 rows behind the ball (CameraRig offset), so 6 + 10 rows.
        /// </summary>
        static float ShadowDistance => Mobile ? 6f + MobileShadowRows :
            Level == GraphicsLevel.High ? 30f : Level == GraphicsLevel.Ultra ? 45f : 60f;

        /// <summary>
        /// Builds only: render scale, upscaler, shadow distance and SSAO on the URP asset (the editor keeps the
        /// project's, so the upscaler only shows in a build).
        /// </summary>
        static void ApplyPipeline()
        {
            if (Application.isEditor) return;
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)) return;
            var upscaler = Upscaler;
            if (upscaler == UpscalerChoice.Off)
            {
                urp.upscalingFilter = UpscalingFilterSelection.Auto;
                urp.renderScale = RenderScale;
            }
            else
            {
                urp.upscalingFilter = upscaler == UpscalerChoice.FSR ? UpscalingFilterSelection.FSR : UpscalingFilterSelection.STP;
                urp.renderScale = UpscaleScales[UpscaleMode];
                if (upscaler == UpscalerChoice.FSR)
                {
                    urp.fsrOverrideSharpness = true;
                    urp.fsrSharpness = 0.9f; // FSR's sharpening pass (RCAS): fewer soft edges after the upscale
                }
            }
            if (ShadowsOn)
            {
                urp.shadowDistance = ShadowDistance;
                if (Mobile)
                {
                    // The phone's URP asset may come with shadows off: on, one cascade, a small shadow map.
                    SetPrivate(urp, "m_MainLightShadowsSupported", true);
                    SetPrivate(urp, "m_MainLightShadowmapResolution", UnityEngine.Rendering.Universal.ShadowResolution._1024);
                    SetPrivate(urp, "m_SoftShadowsSupported", false);
                    urp.shadowCascadeCount = 1;
                }
            }
            bool ssao = Mobile ? Level >= GraphicsLevel.Ultra : Level >= GraphicsLevel.High;
            var field = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.Instance | BindingFlags.NonPublic);
            if (!(field?.GetValue(urp) is ScriptableRendererData[] list)) return;
            foreach (var data in list)
            {
                if (data == null) continue;
                foreach (var feature in data.rendererFeatures)
                    if (feature != null && feature.GetType().Name == "ScreenSpaceAmbientOcclusion") feature.SetActive(ssao);
            }
        }

        static void SetPrivate(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null && field.FieldType == value.GetType()) field.SetValue(target, value);
        }
    }
}
