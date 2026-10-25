using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RTXOn
{
    /// <summary>
    /// The Main Menu's 3D LevelPreview (Adrian 2026-10-25): a level's LevelPreview (10 rows, <see cref="LevelPreviews"/>)
    /// built by the game's own TrackBuilder, seen from the default CameraRig place in the Day environment, drawn into a
    /// picture (<see cref="Texture"/>). Only to look at: the camera does not move and nothing can be played. Its
    /// objects animate as in the game (from the ball's distance). <see cref="PlayIntro"/> (the level was picked) rolls
    /// the ball over it so the tiles and objects play, then calls back for the Loading screen.
    /// </summary>
    public class LevelPreviewStage : MonoBehaviour
    {
        /// <summary>The layer the preview is drawn on: only its own camera sees it, never the menu's camera.</summary>
        public const int Layer = 30;
        /// <summary>Seconds the ball rolls over the preview after the level was picked.</summary>
        public const float IntroSeconds = 1.3f;

        /// <summary>The preview's ball while a preview is shown: track pieces work out their animation from it (<see cref="TrackBehaviour"/>).</summary>
        public static Transform Ball { get; private set; }

        /// <summary>The picture of the preview (null until <see cref="Show"/>).</summary>
        public Texture Texture => target;
        /// <summary>True while the ball rolls after the level was picked.</summary>
        public bool Playing => intro != null;

        // CameraRig's defaults (offset, look ahead, how much it follows the ball sideways, field of view).
        static readonly Vector3 CameraOffset = new Vector3(0f, 5.2f, -5.8f);
        const float LookAhead = 6f, FollowX = 0.45f, FieldOfView = 60f, BallRadius = 0.3f;

        Camera cam;
        Light sun;
        RenderTexture target;
        Transform world;
        GameObject trackGo;
        TrackBuilder track;
        EnvironmentDirector environment;
        Transform ball;
        Material ballMaterial, skyMaterial;
        const string SkyShader = "RTXOn/SkyGradient";
        readonly List<TrackTheme> looks = new List<TrackTheme>();
        TrackTheme defaultTheme;
        float speed = LevelData.DefaultSpeed, layerTimer;
        Action intro;
        float introTime;

        // The scene's own render settings, put back when the preview goes away.
        bool saved, savedFog;
        FogMode savedFogMode;
        Color savedFogColor, savedAmbientSky, savedAmbientEquator, savedAmbientGround;
        float savedFogDensity;
        UnityEngine.Rendering.AmbientMode savedAmbientMode;
        Material savedSkybox;
        Light savedSun;

        /// <summary>Makes the stage (camera, sun, picture of <paramref name="size"/> pixels). <paramref name="fallbackTheme"/>: the look of a level without one.</summary>
        public static LevelPreviewStage Create(Vector2 size, TrackTheme fallbackTheme)
        {
            var go = new GameObject("Level Preview Stage");
            var stage = go.AddComponent<LevelPreviewStage>();
            stage.defaultTheme = fallbackTheme;
            stage.Build(size);
            return stage;
        }

        void Build(Vector2 size)
        {
            world = new GameObject("Level Preview World").transform;
            world.SetParent(transform, false);

            target = new RenderTexture(Mathf.Max(64, Mathf.RoundToInt(size.x)), Mathf.Max(64, Mathf.RoundToInt(size.y)), 24)
            {
                name = "Level Preview",
                antiAliasing = GraphicsQuality.Mobile ? 2 : 4,
            };
            var camGo = new GameObject("Level Preview Camera");
            camGo.transform.SetParent(transform, false);
            cam = camGo.AddComponent<Camera>();
            cam.targetTexture = target;
            cam.cullingMask = 1 << Layer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.74f, 0.86f, 0.98f);
            cam.fieldOfView = FieldOfView;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 300f;
            cam.depth = -10f;

            var sunGo = new GameObject("Level Preview Sun");
            sunGo.transform.SetParent(transform, false);
            sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.cullingMask = 1 << Layer;
            sun.shadows = LightShadows.Soft;

            // The menu's cameras must not draw the preview's world.
            foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (c != cam) c.cullingMask &= ~(1 << Layer);

            ThemeRegistry.AddTo(looks); // every official theme, also in a build
            if (defaultTheme == null) defaultTheme = FindTheme("Massif") ?? (looks.Count > 0 ? looks[0] : null);
        }

        TrackTheme FindTheme(string name)
        {
            foreach (var t in looks)
                if (t != null && (string.Equals(t.name, name, StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(t.name, name + "Theme", StringComparison.OrdinalIgnoreCase))) return t;
            return null;
        }

        /// <summary>Shows <paramref name="preview"/> (a LevelPreview, already 10 rows). Null clears the stage.</summary>
        public void Show(LevelData preview)
        {
            intro = null;
            Clear();
            if (preview == null) return;
            SaveRenderSettings();
            speed = preview.SpeedStart > 0f ? preview.SpeedStart : LevelData.DefaultSpeed;

            // The ball where a level starts (the first row's safe lane), or at the preview's Start Pos: any cell of its
            // 10 rows (Adrian 2026-10-25), even one without floor, like the editor's Play.
            int startRow = preview.HasTestStart ? Mathf.Clamp(preview.TestStartRow, 0, LevelPreviews.Rows - 1) : 0;
            float startX = preview.HasTestStart && preview.TestStartLane < LevelData.Width ? preview.TestStartLane - 2f : preview.SafeLaneX(startRow);
            var ballGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballGo.name = "Preview Ball";
            var collider = ballGo.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            ballGo.transform.SetParent(world, false);
            ballGo.transform.localScale = Vector3.one * BallRadius * 2f;
            ballGo.transform.position = new Vector3(startX, BallRadius, startRow);
            ball = ballGo.transform;
            Ball = ball;

            // The look like the game picks it: the level's look (official or Custom Theme) with its Theme Settings.
            ThemeLibrary.LoadBundled();
            ThemeLibrary.AddFromLevel(preview);
            var look = string.IsNullOrEmpty(preview.Look) ? null : ThemeLibrary.ResolveKey(preview.Look, looks, preview.ThemeSettings, defaultTheme);
            var theme = look != null ? look : ThemeLibrary.WithSettings(defaultTheme, preview.ThemeSettings, looks);

            trackGo = new GameObject("Preview Track");
            trackGo.transform.SetParent(world, false);
            track = trackGo.AddComponent<TrackBuilder>();
            track.theme = theme;
            track.rowsAhead = LevelPreviews.Rows + 2;
            track.rowsBehind = 4;

            // TrackBuilder and the environment put their background on Camera.main: for a moment that is this camera.
            var retagged = new List<Camera>();
            foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (c != cam && c.CompareTag("MainCamera")) { c.tag = "Untagged"; retagged.Add(c); }
            cam.tag = "MainCamera";
            RenderSettings.sun = sun;
            // The game's sky (gradient or HDRI) for the Day environment; the menu's own sky comes back with Clear.
            if (skyMaterial == null)
            {
                var shader = Shader.Find(SkyShader);
                if (shader != null) skyMaterial = new Material(shader) { name = "Level Preview Sky" };
            }
            if (skyMaterial != null) RenderSettings.skybox = skyMaterial;
            try
            {
                track.Init(preview, ball);
                environment = trackGo.AddComponent<EnvironmentDirector>();
                environment.sun = sun;
                environment.liveEdit = false;
                // The Day environment only (Adrian): the theme's "Day", else its first one.
                environment.Init(track.theme, new List<ThemeChange> { new ThemeChange { row = 0, preset = "Day", rows = 0f } });
                foreach (var m in track.LookMaterials) environment.RegisterNeon(m);
            }
            catch (Exception e)
            {
                Debug.LogWarning("RTXOn: the LevelPreview could not be built: " + e);
            }
            finally
            {
                cam.tag = "Untagged";
                foreach (var c in retagged) if (c != null) c.tag = "MainCamera";
            }
            cam.clearFlags = RenderSettings.skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            if (environment != null && environment.Current != null) cam.backgroundColor = environment.Current.skyHorizon;

            ballMaterial = track.BallMaterial;
            var ballRenderer = ball.GetComponent<Renderer>();
            if (ballRenderer != null && ballMaterial != null) ballRenderer.sharedMaterial = ballMaterial;
            PlaceCamera();
            SetLayers();
        }

        /// <summary>The level was picked: the ball rolls over the preview (tiles and objects play), then <paramref name="done"/>.</summary>
        public void PlayIntro(Action done)
        {
            if (ball == null)
            {
                done?.Invoke();
                return;
            }
            intro = done;
            introTime = 0f;
        }

        void Update()
        {
            if (ball == null) return;
            if (intro != null)
            {
                introTime += Time.deltaTime;
                var p = ball.position;
                p.z = Mathf.Min(p.z + speed * Time.deltaTime, LevelPreviews.Rows - 1f);
                ball.position = p;
                ball.Rotate(Vector3.right, speed * Time.deltaTime / BallRadius * Mathf.Rad2Deg, Space.World);
                if (introTime >= IntroSeconds)
                {
                    var done = intro;
                    intro = null;
                    done?.Invoke();
                }
            }
            PlaceCamera();
            // Pieces made after the first frame (power-up effects, pickups) go on the preview's layer too.
            layerTimer -= Time.unscaledDeltaTime;
            if (layerTimer <= 0f) SetLayers();
        }

        /// <summary>The default CameraRig place: behind and above the ball, looking a little ahead.</summary>
        void PlaceCamera()
        {
            if (cam == null || ball == null) return;
            var b = ball.position;
            float x = b.x * FollowX;
            cam.transform.position = new Vector3(x, 0f, b.z) + CameraOffset;
            var focus = new Vector3(x * 0.6f, 0f, b.z + LookAhead);
            cam.transform.rotation = Quaternion.LookRotation(focus - cam.transform.position, Vector3.up);
        }

        void SetLayers()
        {
            layerTimer = 0.25f;
            SetLayer(world);
            SetLayer(cam.transform);
        }

        static void SetLayer(Transform t)
        {
            t.gameObject.layer = Layer;
            for (int i = 0; i < t.childCount; i++) SetLayer(t.GetChild(i));
        }

        void SaveRenderSettings()
        {
            if (saved) return;
            saved = true;
            savedFog = RenderSettings.fog;
            savedFogMode = RenderSettings.fogMode;
            savedFogColor = RenderSettings.fogColor;
            savedFogDensity = RenderSettings.fogDensity;
            savedAmbientMode = RenderSettings.ambientMode;
            savedAmbientSky = RenderSettings.ambientSkyColor;
            savedAmbientEquator = RenderSettings.ambientEquatorColor;
            savedAmbientGround = RenderSettings.ambientGroundColor;
            savedSkybox = RenderSettings.skybox;
            savedSun = RenderSettings.sun;
        }

        void RestoreRenderSettings()
        {
            if (!saved) return;
            saved = false;
            RenderSettings.fog = savedFog;
            RenderSettings.fogMode = savedFogMode;
            RenderSettings.fogColor = savedFogColor;
            RenderSettings.fogDensity = savedFogDensity;
            RenderSettings.ambientMode = savedAmbientMode;
            RenderSettings.ambientSkyColor = savedAmbientSky;
            RenderSettings.ambientEquatorColor = savedAmbientEquator;
            RenderSettings.ambientGroundColor = savedAmbientGround;
            RenderSettings.skybox = savedSkybox;
            RenderSettings.sun = savedSun;
        }

        /// <summary>Takes the shown preview away (the camera and picture stay for the next one).</summary>
        public void Clear()
        {
            if (Ball == ball) Ball = null;
            ball = null;
            track = null;
            environment = null;
            for (int i = world.childCount - 1; i >= 0; i--)
            {
                var child = world.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
            trackGo = null;
            RestoreRenderSettings();
        }

        void OnDestroy()
        {
            if (Ball == ball) Ball = null;
            RestoreRenderSettings();
            if (cam != null) cam.targetTexture = null;
            if (target != null) target.Release();
            if (skyMaterial != null) Destroy(skyMaterial);
        }
    }
}
