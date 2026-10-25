using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RTXOn
{
    /// <summary>
    /// The theme's Background picture (original Background1..8) behind the whole track, like the original game's
    /// background: a picture fixed to the camera at the far end of its view, as tall as the screen (never
    /// stretched; landscape shows the sky beside it). Only the sky shows it; the track, fog and everything solid stay in front. A look change fades from
    /// one look's picture to the next. A look without a picture shows the Environments' sky.
    /// The game camera's background also decides what the ball (and glossy tiles) reflect (Adrian 2026-10-23): while a
    /// picture shows, the live reflection probes clear to the picture's colour instead of drawing the skybox (the sky
    /// is never seen behind the picture); with no picture they show the skybox again.
    /// </summary>
    [DisallowMultipleComponent]
    public class BackgroundView : MonoBehaviour
    {
        const string ShaderName = "RTXOn/Background";

        [Tooltip("How far away the picture hangs, as a part of the camera's Far Clip Plane.")]
        [Range(0.5f, 0.99f)] public float distance = 0.95f;

        [Tooltip("Set by the game (TrackBuilder): reflections follow this background. Previews leave it off.")]
        public bool drivesReflections;

        Camera cam;
        Transform quad;
        Material material;
        TrackTheme from, to;
        float blend;
        bool missingShader;

        /// <summary>The background of <paramref name="camera"/> (added on first use). Null without a camera.</summary>
        public static BackgroundView On(Camera camera)
        {
            if (camera == null) return null;
            var view = camera.GetComponent<BackgroundView>();
            if (view == null) view = camera.gameObject.AddComponent<BackgroundView>();
            view.cam = camera;
            return view;
        }

        /// <summary>Shows <paramref name="look"/>'s picture at once (null or no picture = the sky).</summary>
        public void Show(TrackTheme look)
        {
            if (look == from && blend == 0f && look == to) return;
            from = to = look;
            blend = 0f;
            Apply();
        }

        /// <summary>Starts fading from the picture on screen to <paramref name="look"/>'s; drive it with SetFade.</summary>
        public void BeginFade(TrackTheme look)
        {
            if (blend >= 0.5f) from = to;
            to = look;
            blend = 0f;
            Apply();
        }

        /// <summary>0 = the old picture, 1 = the new one.</summary>
        public void SetFade(float t)
        {
            blend = Mathf.Clamp01(t);
            Apply();
        }

        public void FinishFade()
        {
            from = to;
            blend = 0f;
            Apply();
        }

        void Apply()
        {
            bool any = Has(from) || Has(to);
            ApplyReflections();
            if (!any)
            {
                if (quad != null) quad.gameObject.SetActive(false);
                return;
            }
            if (!Build()) return;
            quad.gameObject.SetActive(true);
            material.SetTexture("_MainTex", Has(from) ? from.background : null);
            material.SetTexture("_AltTex", Has(to) ? to.background : null);
            material.SetFloat("_AlphaA", Has(from) ? 1f : 0f);
            material.SetFloat("_AlphaB", Has(to) ? 1f : 0f);
            material.SetColor("_TintA", from != null ? from.backgroundTint : Color.white);
            material.SetColor("_TintB", to != null ? to.backgroundTint : Color.white);
            material.SetFloat("_GradA", from != null ? (int)from.backgroundGradient : 0);
            material.SetFloat("_GradB", to != null ? (int)to.backgroundGradient : 0);
            material.SetFloat("_Blend", blend);
            Fit();
        }

        static bool Has(TrackTheme look) => look != null && look.background != null;

        bool Build()
        {
            if (quad != null) return true;
            if (cam == null) cam = GetComponent<Camera>();
            if (cam == null || missingShader) return false;
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                missingShader = true;
                Debug.LogWarning($"RTXOn: shader '{ShaderName}' not found (Shaders/Resources/RTXOnBackground.shader), so no background picture.");
                return false;
            }
            material = new Material(shader) { name = "Background (runtime)" };

            var go = new GameObject("Background Picture");
            go.layer = LowestLayer(cam.cullingMask);
            quad = go.transform;
            quad.SetParent(cam.transform, false);
            var mesh = new Mesh { name = "Background Quad" };
            mesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(1f, 1f, 0.01f));
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return true;
        }

        /// <summary>The first layer the camera draws, so the picture shows in that camera only (editor 3D view: 31).</summary>
        static int LowestLayer(int mask)
        {
            for (int i = 0; i < 32; i++)
                if ((mask & (1 << i)) != 0) return i;
            return 0;
        }

        void LateUpdate()
        {
            if (drivesReflections && Time.frameCount % 60 == 0) ApplyReflections(); // probes made later, quality changes
            if (quad == null || !quad.gameObject.activeSelf) return;
            // Gradient: the picture fades into the fog colour of the environment on screen (it changes with day and night).
            material.SetColor("_GradColor", RenderSettings.fogColor);
            Fit();
        }

        /// <summary>
        /// Full height at the picture's distance, keeping the picture's shape (Adrian 2026-10-24): portrait fills top to
        /// bottom (the sides are cropped), landscape leaves the left and right showing the sky.
        /// </summary>
        void Fit()
        {
            if (quad == null || cam == null) return;
            var look = blend >= 0.5f ? (Has(to) ? to : from) : (Has(from) ? from : to);
            float d = cam.farClipPlane * distance;
            float h = 2f * d * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float texAspect = 1f;
            if (Has(look) && look.background.height > 0) texAspect = (float)look.background.width / look.background.height;
            float y = look != null ? look.backgroundY * h : 0f;
            float need = h + 2f * Mathf.Abs(y); // moved up or down, it still covers the whole view
            float qh = need, qw = need * texAspect;
            quad.localPosition = new Vector3(0f, y, d);
            quad.localRotation = Quaternion.identity;
            quad.localScale = new Vector3(qw, qh, 1f);
        }

        // ---------------- Reflections ----------------

        static readonly Dictionary<Texture, Color> averages = new Dictionary<Texture, Color>();
        static Cubemap solidSky;
        static bool savedSky;
        static DefaultReflectionMode savedMode;
        static Texture savedCubemap;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            averages.Clear();
            solidSky = null;
            savedSky = false;
        }

        /// <summary>
        /// Picture on screen: live reflection probes clear to its colour (no skybox), and objects without a probe get a
        /// cubemap of that colour too (Graphics Low turns the ball's probe off). No picture: the skybox as before.
        /// </summary>
        void ApplyReflections()
        {
            if (!drivesReflections) return;
            bool any = Has(from) || Has(to);
            var color = any ? ReflectedColor() : Color.black;
            foreach (var probe in FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None))
            {
                if (probe.mode != ReflectionProbeMode.Realtime) continue;
                var flags = any ? ReflectionProbeClearFlags.SolidColor : ReflectionProbeClearFlags.Skybox;
                if (probe.clearFlags != flags) probe.clearFlags = flags;
                if (any) probe.backgroundColor = color;
            }
            if (any) UseSolidSky(color);
            else RestoreSky();
        }

        /// <summary>The colour of the picture on screen (mixed during a fade), with its tint and gradient (fog) colour.</summary>
        Color ReflectedColor()
        {
            Color a = Has(from) ? LookColor(from) : LookColor(to);
            Color b = Has(to) ? LookColor(to) : a;
            var c = Color.Lerp(a, b, blend);
            c.a = 1f;
            return c;
        }

        static Color LookColor(TrackTheme look)
        {
            var c = Average(look.background) * look.backgroundTint;
            if (look.backgroundGradient != BackgroundGradient.None) c = Color.Lerp(c, RenderSettings.fogColor, 0.3f);
            return c;
        }

        /// <summary>The average colour of a picture, worked out once (on the GPU: textures need not be readable).</summary>
        static Color Average(Texture tex)
        {
            if (averages.TryGetValue(tex, out var known)) return known;
            const int Size = 64;
            var rt = RenderTexture.GetTemporary(Size, Size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(tex, rt);
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var read = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0f, 0f, Size, Size), 0, 0);
            read.Apply(false);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            float r = 0f, g = 0f, b = 0f;
            var px = read.GetPixels32();
            foreach (var p in px)
            {
                r += p.r;
                g += p.g;
                b += p.b;
            }
            Destroy(read);
            float n = Mathf.Max(1, px.Length) * 255f;
            var c = new Color(r / n, g / n, b / n, 1f);
            averages[tex] = c;
            return c;
        }

        static void UseSolidSky(Color color)
        {
            if (!savedSky)
            {
                savedSky = true;
                savedMode = RenderSettings.defaultReflectionMode;
                savedCubemap = RenderSettings.customReflectionTexture;
            }
            if (solidSky == null) solidSky = new Cubemap(8, TextureFormat.RGBA32, false) { name = "Background Reflection" };
            var px = new Color[8 * 8];
            for (int i = 0; i < px.Length; i++) px[i] = color;
            for (int f = 0; f < 6; f++) solidSky.SetPixels(px, (CubemapFace)f);
            solidSky.Apply(false);
            RenderSettings.customReflectionTexture = solidSky;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        }

        static void RestoreSky()
        {
            if (!savedSky) return;
            RenderSettings.defaultReflectionMode = savedMode;
            RenderSettings.customReflectionTexture = savedCubemap;
            savedSky = false;
        }

        void OnDestroy()
        {
            if (material != null) Destroy(material);
            if (drivesReflections) RestoreSky();
        }
    }
}
