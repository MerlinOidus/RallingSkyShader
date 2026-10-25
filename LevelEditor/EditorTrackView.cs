using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace RTXOn
{
    /// <summary>
    /// The level editor's 3D view (button 3D, key V): the rows near the grid's scroll position drawn as the track,
    /// with the look of each row (its TrackTheme's materials and models), seen from a camera you can tilt and zoom.
    /// Painting still works: the grid turns a click on the picture into the tile under it (<see cref="CellAt"/>).
    /// Everything sits on one layer that only this view's camera draws, so the editor's own camera never shows it.
    /// The camera, sun and tiles are scene objects of their own (not under the UI, whose canvas moves and scales
    /// everything under it). A grid of lines lies on the track like the 2D grid, empty cells included; row numbers
    /// sit on the right, the music visual (loudness and beats) runs along the left, trigger rows show coloured bars,
    /// and while Play runs a yellow line marks the ball and the camera follows it.
    /// </summary>
    public class EditorTrackView : MonoBehaviour
    {
        public const int Layer = 31;

        [Range(15f, 90f)] public float tilt = 35f;    // 90 = straight down, like the 2D grid
        [Range(4f, 30f)] public float zoom = 11f;     // camera distance
        public float rowsBehind = 3f;                 // rows drawn behind the bottom row of the grid
        [Tooltip("Rows drawn in front of the bottom row of the grid (the level's Load Distance sets it).")]
        public float rowsAhead = 55f;
        public Color gridColor = new Color(1f, 1f, 1f, 0.55f);
        public Color themeBarColor = new Color(0.85f, 0.3f, 1f);
        public Color cameraBarColor = new Color(0.25f, 0.95f, 0.8f);
        public Color playheadColor = new Color(1f, 0.85f, 0.3f);

        /// <summary>Set while a camera trigger is being edited: the view shows the game camera of that trigger.</summary>
        [System.NonSerialized] public CameraChange preview;

        /// <summary>
        /// SpriteMaker 3D: a level of its own (LevelTexturePreview.json) drawn instead of the editor's, with the camera at
        /// <see cref="SourceRow"/>. Null = the editor's level.
        /// </summary>
        public LevelData SourceLevel { get; private set; }
        public float SourceRow { get; private set; }
        LevelData Lvl => SourceLevel ?? editor.Level;
        int First => SourceLevel != null ? Mathf.FloorToInt(SourceRow) : editor.FirstRow;
        bool Own => SourceLevel == null; // the editor's level: start cell, just placed, triggers and props come from it

        LevelEditor editor;
        RawImage image;
        RenderTexture target;
        Camera cam;
        BackgroundView backdrop; // the look's background picture behind the 3D view
        Light sun;
        Transform root;
        MeshFilter gridLines, musicRibbon, partLines;
        Material gridMaterial, musicMaterial, themeMaterial, cameraMaterial, filterMaterial, playheadMaterial, partMaterial;
        readonly Dictionary<PartKind, Material> powerMaterials = new Dictionary<PartKind, Material>();
        readonly List<float> partMarks = new List<float>();
        GameObject playhead;
        int gridFrom = -1, gridTo = -1;
        int viewFrom, viewTo; // rows the last Refresh drew
        float playRow = -1f;

        class Cell
        {
            public int id, obstacle, pickup; // the three layers
            public TrackTheme look;
            public bool start;
            public int shape; // Tile Type mesh and turns (they change with the neighbours), -1 = none
            public int custom; // CustomObstacles.Version when its custom obstacle was built (0 = none)
            public int parts;  // ARGS the look shows (trigger number, gate up / down), see PreviewKey
            public GameObject go;
        }
        FragileSections tileFragiles; // fragile sections for Tile Type shapes, made again on every refresh
        readonly Dictionary<long, Cell> cells = new Dictionary<long, Cell>();
        readonly List<long> stale = new List<long>();

        // Per row: the number, checkpoint line and trigger bars.
        class RowMark
        {
            public string key;
            public GameObject go;
        }
        readonly Dictionary<int, RowMark> rowMarks = new Dictionary<int, RowMark>();
        readonly List<int> staleRows = new List<int>();

        public bool Visible => image != null && image.enabled;

        /// <summary>
        /// Theme preview (Theme Settings and Custom Theme windows): the track drawn from a game-like camera, without the
        /// grid, row numbers and music, into <see cref="Texture"/>, also while the 3D view itself is hidden.
        /// </summary>
        public bool ThemePreview { get; private set; }

        /// <summary>The picture the 3D view draws into.</summary>
        public Texture Texture => target;

        bool Drawing => Visible || ThemePreview;
        /// <summary>The 3D view's camera (screen effects preview on it).</summary>
        public Camera ViewCamera => cam;
        /// <summary>Music Play's row, below 0 when it is not playing.</summary>
        public float PlayRow => playRow;

        /// <summary>True for the 3D picture itself (where painting starts).</summary>
        public bool IsPicture(GameObject go) => image != null && go == image.gameObject;

        /// <summary>
        /// Makes the picture (a rect of <paramref name="parent"/>, bottom-left at <paramref name="position"/>), the camera,
        /// a sun and the root of the tiles. The picture catches the pointer, so painting, erasing and the wheel work on it.
        /// </summary>
        public void Build(LevelEditor owner, RectTransform parent, Vector2 position, Vector2 size)
        {
            editor = owner;

            var pic = UiFactory.MakeRect(parent, "3D View");
            UiFactory.Place(pic, Vector2.zero, Vector2.zero, position, size);
            image = pic.gameObject.AddComponent<RawImage>();
            image.raycastTarget = true; // its pointer events reach the grid (LevelGridView), which asks CellAt
            image.enabled = false;

            target = new RenderTexture(Mathf.RoundToInt(size.x * 1.25f), Mathf.RoundToInt(size.y * 1.25f), 24) { name = "Editor 3D View", antiAliasing = 4 };
            image.texture = target;

            var camGo = new GameObject("Editor 3D Camera");
            cam = camGo.AddComponent<Camera>();
            cam.targetTexture = target;
            cam.cullingMask = 1 << Layer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 250f;
            cam.enabled = false;
            backdrop = BackgroundView.On(cam);

            var sunGo = new GameObject("Editor 3D Sun");
            sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.cullingMask = 1 << Layer;
            sun.shadows = LightShadows.Soft;
            sun.enabled = false;

            root = new GameObject("Editor 3D Track").transform;
            root.gameObject.SetActive(false);

            // Lines, bars and the music visual use an unlit shader every pipeline has (vertex colours work too).
            gridMaterial = Unlit("Editor Grid", gridColor);
            musicMaterial = Unlit("Editor Music", Color.white);
            themeMaterial = Unlit("Editor Theme Trigger", themeBarColor);
            cameraMaterial = Unlit("Editor Camera Trigger", cameraBarColor);
            filterMaterial = Unlit("Editor Screen FX Trigger", TilePalette.FilterColor);
            playheadMaterial = Unlit("Editor Playhead", playheadColor);
            partMaterial = Unlit("Editor Parts", Color.white);
            gridLines = MeshObject("Editor 3D Grid", gridMaterial);
            musicRibbon = MeshObject("Editor 3D Music", musicMaterial);
            partLines = MeshObject("Editor 3D Power-up Lines", partMaterial);

            playhead = new GameObject("Editor 3D Playhead");
            playhead.transform.SetParent(root, false);
            playhead.AddComponent<MeshFilter>().sharedMesh = MeshFactory.BeveledBox(new Vector3(7.6f, 0.04f, 0.1f), 0.01f);
            playhead.AddComponent<MeshRenderer>().sharedMaterial = playheadMaterial;
            playhead.layer = Layer;
            playhead.SetActive(false);

            // The editor's own camera must not draw the 3D tiles behind the UI.
            foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (c != cam) c.cullingMask &= ~(1 << Layer);
        }

        static Material Unlit(string name, Color color) => new Material(Shader.Find("Sprites/Default")) { name = name, color = color };

        MeshFilter MeshObject(string name, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.layer = Layer;
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = new Mesh { name = name };
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            return mf;
        }

        // ---------------- Theme preview environment ----------------
        // Like the game (EnvironmentDirector): the sky gradient (or HDRI), fog and ambient light of the environment,
        // only while the theme preview draws. The editor's own fog / ambient / skybox come back afterwards.

        Material skyMaterial;
        bool envShown, savedFog;
        FogMode savedFogMode;
        Color savedFogColor, savedAmbientSky, savedAmbientEquator, savedAmbientGround;
        float savedFogDensity;
        AmbientMode savedAmbientMode;
        Material savedSkybox;

        void ShowEnvironment(EnvironmentPreset env)
        {
            if (!envShown)
            {
                savedFog = RenderSettings.fog;
                savedFogMode = RenderSettings.fogMode;
                savedFogColor = RenderSettings.fogColor;
                savedFogDensity = RenderSettings.fogDensity;
                savedAmbientMode = RenderSettings.ambientMode;
                savedAmbientSky = RenderSettings.ambientSkyColor;
                savedAmbientEquator = RenderSettings.ambientEquatorColor;
                savedAmbientGround = RenderSettings.ambientGroundColor;
                savedSkybox = RenderSettings.skybox;
                envShown = true;
            }
            if (env == null)
            {
                RenderSettings.fog = false;
                cam.clearFlags = CameraClearFlags.SolidColor;
                return;
            }
            RenderSettings.fog = env.fogDensity > 0f;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = env.fogColor;
            RenderSettings.fogDensity = env.fogDensity;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = env.ambientSky;
            RenderSettings.ambientEquatorColor = env.ambientEquator;
            RenderSettings.ambientGroundColor = env.ambientGround;

            if (skyMaterial == null)
            {
                var shader = Shader.Find("RTXOn/SkyGradient");
                if (shader != null) skyMaterial = new Material(shader) { name = "Editor Preview Sky" };
            }
            if (skyMaterial == null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor; // no sky shader in this build: the sky colour as before
                return;
            }
            skyMaterial.SetColor("_TopColor", env.skyTop);
            skyMaterial.SetColor("_HorizonColor", env.skyHorizon);
            skyMaterial.SetColor("_BottomColor", env.skyBottom);
            skyMaterial.SetColor("_SunColor", env.sunDisc);
            skyMaterial.SetFloat("_SunSize", env.sunDiscSize);
            skyMaterial.SetFloat("_Stars", env.stars);
            skyMaterial.SetVector("_SunDir", -(sun.transform.rotation * Vector3.forward));
            if (skyMaterial.HasProperty("_HdriA"))
            {
                skyMaterial.SetTexture("_HdriA", env.skyHdri);
                skyMaterial.SetFloat("_HdriMixA", env.skyHdri != null ? 1f : 0f);
                skyMaterial.SetFloat("_HdriExposureA", env.hdriExposure);
                skyMaterial.SetFloat("_HdriRotationA", env.hdriRotation);
                skyMaterial.SetColor("_HdriTintA", env.hdriTint);
                if (skyMaterial.HasProperty("_HdriBlend")) skyMaterial.SetFloat("_HdriBlend", 0f);
            }
            var box = cam.GetComponent<Skybox>();
            if (box == null) box = cam.gameObject.AddComponent<Skybox>();
            box.material = skyMaterial;
            box.enabled = true;
            RenderSettings.skybox = skyMaterial; // the 3D view's camera is the only one that clears to the sky
            cam.clearFlags = CameraClearFlags.Skybox;
        }

        void HideEnvironment()
        {
            if (!envShown) return;
            envShown = false;
            RenderSettings.fog = savedFog;
            RenderSettings.fogMode = savedFogMode;
            RenderSettings.fogColor = savedFogColor;
            RenderSettings.fogDensity = savedFogDensity;
            RenderSettings.ambientMode = savedAmbientMode;
            RenderSettings.ambientSkyColor = savedAmbientSky;
            RenderSettings.ambientEquatorColor = savedAmbientEquator;
            RenderSettings.ambientGroundColor = savedAmbientGround;
            RenderSettings.skybox = savedSkybox;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                var box = cam.GetComponent<Skybox>();
                if (box != null) box.enabled = false;
            }
        }

        void OnDestroy()
        {
            HideEnvironment();
            if (skyMaterial != null) Destroy(skyMaterial);
            if (cam != null) cam.targetTexture = null;
            if (target != null) target.Release();
            if (cam != null) Destroy(cam.gameObject);
            if (sun != null) Destroy(sun.gameObject);
            if (gridLines != null) Destroy(gridLines.sharedMesh);
            if (musicRibbon != null) Destroy(musicRibbon.sharedMesh);
            if (partLines != null) Destroy(partLines.sharedMesh);
            if (root != null) Destroy(root.gameObject);
            foreach (var m in new[] { gridMaterial, musicMaterial, themeMaterial, cameraMaterial, filterMaterial, playheadMaterial, partMaterial })
                if (m != null) Destroy(m);
            foreach (var m in powerMaterials.Values) if (m != null) Destroy(m);
        }

        public void Show(bool on)
        {
            if (!on) StopMovers();
            image.enabled = on;
            cam.enabled = on || ThemePreview;
            sun.enabled = on || ThemePreview;
            root.gameObject.SetActive(on || ThemePreview);
        }

        /// <summary>Starts or ends the theme preview (see <see cref="ThemePreview"/>).</summary>
        public void SetThemePreview(bool on)
        {
            if (cam == null || ThemePreview == on) return;
            ThemePreview = on;
            if (!on)
            {
                SourceLevel = null;
                HideEnvironment();
            }
            bool draw = Drawing;
            cam.enabled = draw;
            sun.enabled = draw;
            root.gameObject.SetActive(draw);
            gridLines.gameObject.SetActive(!on);
            musicRibbon.gameObject.SetActive(!on);
            partLines.gameObject.SetActive(!on);
            foreach (var m in rowMarks.Values) if (m.go != null) m.go.SetActive(!on);
            if (on) playhead.SetActive(false);
            else SetPlayhead(playRow);
            Refresh();
        }

        /// <summary>Draws <paramref name="level"/> (null = the editor's level again) in the theme preview, from row <paramref name="row"/>.</summary>
        public void SetSource(LevelData level, float row)
        {
            bool changed = SourceLevel != level;
            SourceLevel = level;
            SourceRow = Mathf.Max(0f, row);
            if (changed) Refresh();
            else UpdateCamera();
        }

        /// <summary>Play: the yellow line at this row (below 0 = hidden), and the camera follows it.</summary>
        public void SetPlayhead(float row)
        {
            playRow = row;
            if (playhead == null) return;
            playhead.SetActive(row >= 0f);
            if (row >= 0f) playhead.transform.localPosition = new Vector3(-0.8f, 0.03f, row);
            if (Visible) UpdateCamera();
        }

        /// <summary>The camera: the trigger being edited, else looking along the track (following Play).</summary>
        public void UpdateCamera()
        {
            if (cam == null) return;
            if (preview != null)
            {
                cam.transform.SetPositionAndRotation(new Vector3(0f, 0f, preview.row) + preview.position, Quaternion.Euler(preview.rotation));
                return;
            }
            if (ThemePreview)
            {
                // About where the game's camera sits behind the ball.
                cam.transform.SetPositionAndRotation(new Vector3(0f, 4.5f, (SourceLevel != null ? SourceRow : editor.FirstRow) - 1.5f), Quaternion.Euler(32f, 0f, 0f));
                return;
            }
            float first = playRow >= 0f ? playRow - 2f : First;
            var focus = new Vector3(0f, 0f, first + LevelEditor.VisibleRows * (tilt > 75f ? 0.5f : 0.3f));
            var rot = Quaternion.Euler(tilt, 0f, 0f);
            cam.transform.SetPositionAndRotation(focus - rot * Vector3.forward * zoom, rot);
        }

        /// <summary>Brings the tiles, camera and colours up to date with the level and the grid's scroll position.</summary>
        public void Refresh()
        {
            if (!Drawing) return;
            var level = Lvl;
            int first = First;
            int from = Mathf.Max(0, first - Mathf.CeilToInt(rowsBehind));
            int to = first + Mathf.CeilToInt(rowsAhead);
            StopMovers(); // the cells may be built again
            viewFrom = from;
            viewTo = to;
            UpdateCamera();

            var look = editor.LookAt(first);
            var env = editor.EnvironmentAt(first);
            if (backdrop != null) backdrop.Show(look);
            sun.shadows = look == null || look.shadows ? LightShadows.Soft : LightShadows.None;
            if (env != null)
            {
                cam.backgroundColor = Color.Lerp(env.skyHorizon, env.skyTop, 0.35f);
                sun.transform.rotation = editor.LightRotationAt(first, env);
                sun.color = env.sunColor;
                sun.intensity = Mathf.Max(0.6f, env.sunIntensity);
            }
            else
            {
                cam.backgroundColor = new Color(0.55f, 0.75f, 0.95f);
                sun.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
                sun.color = Color.white;
                sun.intensity = 1.4f;
            }
            if (ThemePreview) ShowEnvironment(env);
            else HideEnvironment();

            gridMaterial.color = gridColor;
            if (from != gridFrom || to != gridTo) BuildGrid(from, to);
            BuildMusic(level, from, to);
            BuildPartLines(level, from, to);

            // Tiles: only cells that changed are built again.
            stale.Clear();
            foreach (var kv in cells)
            {
                int row = (int)(kv.Key / LevelData.Width);
                if (row < from || row > to) stale.Add(kv.Key);
            }
            foreach (var k in stale)
            {
                Destroy(cells[k].go);
                cells.Remove(k);
            }
            tileFragiles = null;
            for (int row = from; row <= to; row++)
            {
                var rowLook = editor.LookAt(row);
                for (int lane = 0; lane < LevelData.Width; lane++)
                {
                    int id = row < level.RowCount ? level.Get(row, lane) : 0;
                    int obstacle = level.GetObject(row, lane), pickup = level.GetCollect(row, lane);
                    bool start = Own && editor.IsStartCell(row, lane);
                    int shape = TileTypeKey(level, row, lane, id, rowLook, out _, out _);
                    int custom = CustomObstacle.IsCustomId(obstacle) || CustomObstacle.IsCustomId(id) || CustomObstacle.IsCustomId(pickup)
                        ? CustomObstacles.Version : 0; // an edited custom obstacle is built again
                    int parts = PreviewKey(level, row, lane, id, obstacle);
                    long key = (long)row * LevelData.Width + lane;
                    if (cells.TryGetValue(key, out var c))
                    {
                        if (c.id == id && c.obstacle == obstacle && c.pickup == pickup && c.look == rowLook && c.start == start && c.shape == shape && c.custom == custom && c.parts == parts) continue;
                        if (c.go != null) Destroy(c.go);
                    }
                    else cells[key] = c = new Cell();
                    c.id = id;
                    c.obstacle = obstacle;
                    c.pickup = pickup;
                    c.look = rowLook;
                    c.start = start;
                    c.shape = shape;
                    c.custom = custom;
                    c.parts = parts;
                    c.go = BuildCell(level, row, lane, id, obstacle, pickup, rowLook, start);
                    // Just painted: its animation once (an animated tile moves like in the game, others pop in).
                    if (c.go != null && Own && editor.WasJustPlaced(row, lane))
                        PlacedAnimation.Play(c.go, PartArgs.KindOf(level.PartIdAt(row, lane)), level.ArgsAt(row, lane), row, lane);
                }
            }

            // Row numbers, checkpoints and trigger bars.
            staleRows.Clear();
            foreach (var kv in rowMarks) if (kv.Key < from || kv.Key > to) staleRows.Add(kv.Key);
            foreach (var r in staleRows)
            {
                Destroy(rowMarks[r].go);
                rowMarks.Remove(r);
            }
            for (int row = from; row <= to; row++)
            {
                bool checkpoint = Own && editor.IsCheckpointRow(row);
                bool theme = Own && editor.ThemeTriggerAt(row) != null;
                bool camera = Own && editor.CameraTriggerAt(row) != null;
                bool filter = Own && editor.FilterTriggerAt(row) != null;
                bool past = row >= level.RowCount;
                var rowLook = editor.LookAt(row);
                string key = $"{checkpoint}{theme}{camera}{filter}{past}|{(rowLook != null ? rowLook.name : "")}|{(Own ? editor.PropsKey(row) : "")}";
                if (rowMarks.TryGetValue(row, out var m))
                {
                    if (m.key == key) continue;
                    Destroy(m.go);
                }
                else rowMarks[row] = m = new RowMark();
                m.key = key;
                m.go = BuildRowMark(row, checkpoint, theme, camera, filter, past, rowLook);
                if (ThemePreview) m.go.SetActive(false);
            }
        }

        /// <summary>The point of the picture under a screen position, 0..1 across and up.</summary>
        public bool ViewportAt(Vector2 screen, Camera eventCamera, out Vector2 viewport)
        {
            viewport = default;
            var rt = image.rectTransform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screen, eventCamera, out var local)) return false;
            var r = rt.rect;
            viewport = new Vector2((local.x - r.xMin) / r.width, (local.y - r.yMin) / r.height);
            return true;
        }

        /// <summary>
        /// The cell under a point of the picture (0..1 across and up). False when the point is off the track.
        /// </summary>
        public bool CellAt(Vector2 viewport, out int row, out int lane)
        {
            row = lane = -1;
            if (!Visible) return false;
            var ray = cam.ViewportPointToRay(new Vector3(viewport.x, viewport.y, 0f));
            if (Mathf.Abs(ray.direction.y) < 1e-4f) return false;
            float t = -ray.origin.y / ray.direction.y; // the floor top, y = 0
            if (t <= 0f) return false;
            var p = ray.origin + ray.direction * t;
            lane = Mathf.RoundToInt(p.x + 2f);
            row = Mathf.FloorToInt(p.z + 0.5f);
            return lane >= 0 && lane < LevelData.Width && row >= 0;
        }

        /// <summary>Lines round every cell of rows from..to: 6 lengthwise, one across per row edge.</summary>
        void BuildGrid(int from, int to)
        {
            gridFrom = from;
            gridTo = to;
            const float W = 0.03f, Y = 0.012f;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            float z0 = from - 0.5f, z1 = to + 0.5f;
            for (int l = 0; l <= LevelData.Width; l++)
            {
                float x = l - 2.5f;
                Quad(verts, tris, new Vector3(x - W, Y, z0), new Vector3(x - W, Y, z1), new Vector3(x + W, Y, z1), new Vector3(x + W, Y, z0));
            }
            for (int r = from; r <= to + 1; r++)
            {
                float z = r - 0.5f;
                Quad(verts, tris, new Vector3(-2.5f, Y, z - W), new Vector3(-2.5f, Y, z + W), new Vector3(2.5f, Y, z + W), new Vector3(2.5f, Y, z - W));
            }
            SetMesh(gridLines.sharedMesh, verts, tris, null);
        }

        /// <summary>
        /// Music visual along the left of the track, lined up with the rows like the grid's strip: the song's loudness
        /// as a ribbon (wider = louder).
        /// </summary>
        void BuildMusic(LevelData level, int from, int to)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var colors = new List<Color>();
            var wave = editor.Wave;
            if (wave != null && wave.HasSong)
            {
                const int Steps = 4;
                const float Cx = -3.7f, Half = 0.9f, Y = 0.01f;
                float beat = 0f; // no BPM any more, so no beat ticks
                for (int row = from; row <= to; row++)
                {
                    var color = row < level.RowCount ? wave.waveColor : wave.pastEndColor;
                    for (int k = 0; k < Steps; k++)
                    {
                        float r0 = row - 0.5f + (float)k / Steps, r1 = r0 + 1f / Steps;
                        float t0 = editor.RowTime(r0), t1 = editor.RowTime(r1);
                        float half = Mathf.Max(0.04f, wave.LoudnessBetween(t0, t1) * Half);
                        int i = verts.Count;
                        Quad(verts, tris, new Vector3(Cx - half, Y, r0), new Vector3(Cx - half, Y, r1), new Vector3(Cx + half, Y, r1), new Vector3(Cx + half, Y, r0));
                        for (int n = 0; n < 4; n++) colors.Add(color);
                        if (beat > 0f && Mathf.Floor(t1 / beat) > Mathf.Floor(t0 / beat))
                        {
                            Quad(verts, tris, new Vector3(Cx - Half, Y + 0.002f, r1 - 0.02f), new Vector3(Cx - Half, Y + 0.002f, r1 + 0.02f),
                                new Vector3(Cx + Half, Y + 0.002f, r1 + 0.02f), new Vector3(Cx + Half, Y + 0.002f, r1 - 0.02f));
                            for (int n = 0; n < 4; n++) colors.Add(wave.beatColor);
                        }
                    }
                }
            }
            SetMesh(musicRibbon.sharedMesh, verts, tris, colors);
        }

        /// <summary>The reach of the power-ups (like the grid's lines): a bar along the lane and a diamond on each landing.</summary>
        void BuildPartLines(LevelData level, int from, int to)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var colors = new List<Color>();
            const float W = 0.06f, Y = 0.03f, D = 0.18f;
            int end = Mathf.Min(level.RowCount - 1, to);
            for (int row = Mathf.Max(0, from - 200); row <= end; row++)
                for (int lane = 0; lane < LevelData.Width; lane++)
                {
                    var kind = PartArgs.KindOf(level.PartIdAt(row, lane));
                    if (!PartArgs.IsPowerUp(kind)) continue;
                    bool hasLine = level.ArgsAt(row, lane).Line(partMarks, out float a, out float b, out Color color);
                    if (PartArgs.IsRowTrigger(kind) && row >= from && row <= to)
                    {
                        // Trigger line across the whole row.
                        float half = LevelData.Width * 0.5f;
                        Quad(verts, tris, new Vector3(-half, Y + 0.004f, row - W), new Vector3(-half, Y + 0.004f, row + W),
                            new Vector3(half, Y + 0.004f, row + W), new Vector3(half, Y + 0.004f, row - W));
                        for (int n = 0; n < 4; n++) colors.Add(color);
                    }
                    if (!hasLine) continue;
                    float x = PartArgs.CentredLine(kind) ? 0f : lane - 2f;
                    float z0 = Mathf.Max(from - 0.5f, row + a), z1 = Mathf.Min(to + 0.5f, row + b);
                    if (z1 > z0)
                    {
                        Quad(verts, tris, new Vector3(x - W, Y, z0), new Vector3(x - W, Y, z1), new Vector3(x + W, Y, z1), new Vector3(x + W, Y, z0));
                        for (int n = 0; n < 4; n++) colors.Add(color);
                    }
                    foreach (float m in partMarks)
                    {
                        float z = row + m;
                        if (z < from - 0.5f || z > to + 0.5f) continue;
                        Quad(verts, tris, new Vector3(x - D, Y + 0.002f, z), new Vector3(x, Y + 0.002f, z + D),
                            new Vector3(x + D, Y + 0.002f, z), new Vector3(x, Y + 0.002f, z - D));
                        for (int n = 0; n < 4; n++) colors.Add(color);
                    }
                }
            SetMesh(partLines.sharedMesh, verts, tris, colors);
        }

        static void Quad(List<Vector3> verts, List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }

        static void SetMesh(Mesh mesh, List<Vector3> verts, List<int> tris, List<Color> colors)
        {
            mesh.Clear();
            mesh.indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(verts);
            if (colors != null) mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
        }

        // ---------------- Play Mover ----------------

        /// <summary>How long Play Mover runs before everything goes back.</summary>
        public const float MoverPreviewSeconds = 3f;
        // Rows of "ball travel" per second for the preview: one slide takes half a second, each auto mover of a chain
        // starts 0.35 s after the one that set it off (MoverGrid.ChainDelayRows), slower than the game so you can follow it.
        const float MoverPreviewRate = 2f;

        class PreviewSlide
        {
            public float offset;
            public Vector3 dir;
            public readonly List<Transform> parts = new List<Transform>();
            public readonly List<Vector3> homes = new List<Vector3>();
        }
        readonly List<PreviewSlide> moverPreview = new List<PreviewSlide>();
        float moverTime = -1f;

        /// <summary>
        /// Fires every touch mover on screen at once, with the auto movers each one sets off (the game's rules,
        /// MoverGrid), and slides their cells like MoverDirector does. Returns how many platforms move.
        /// </summary>
        public int PlayMovers()
        {
            StopMovers();
            var level = Lvl;
            if (!Visible || level == null) return 0;
            var grid = new MoverGrid(level);
            if (!grid.HasMovers) return 0;
            int last = Mathf.Min(viewTo, level.RowCount - 1);
            for (int row = Mathf.Max(0, viewFrom); row <= last; row++)
                for (int lane = 0; lane < LevelData.Width; lane++)
                {
                    if (!grid.IsReadyTouch(row, lane)) continue;
                    foreach (var move in grid.Trigger(row, lane, 0f, last))
                    {
                        var slide = new PreviewSlide { offset = move.offset, dir = new Vector3(move.dir.x, 0f, move.dir.y) };
                        foreach (var cell in move.section)
                            if (cells.TryGetValue((long)cell.y * LevelData.Width + cell.x, out var c) && c.go != null)
                            {
                                slide.parts.Add(c.go.transform);
                                slide.homes.Add(c.go.transform.localPosition);
                            }
                        if (slide.parts.Count > 0) moverPreview.Add(slide);
                    }
                }
            if (moverPreview.Count > 0) moverTime = 0f;
            return moverPreview.Count;
        }

        /// <summary>Puts every moved cell back where it was painted.</summary>
        public void StopMovers()
        {
            foreach (var slide in moverPreview)
                for (int i = 0; i < slide.parts.Count; i++)
                    if (slide.parts[i] != null) slide.parts[i].localPosition = slide.homes[i];
            moverPreview.Clear();
            moverTime = -1f;
        }

        void Update()
        {
            if (moverTime < 0f) return;
            moverTime += Time.unscaledDeltaTime;
            if (moverTime >= MoverPreviewSeconds)
            {
                StopMovers();
                return;
            }
            float clock = moverTime * MoverPreviewRate;
            foreach (var slide in moverPreview)
            {
                float v = clock - slide.offset;
                var shift = v <= 0f ? Vector3.zero : slide.dir * MoverDirector.Impact(Mathf.Clamp01(v));
                for (int i = 0; i < slide.parts.Count; i++)
                    if (slide.parts[i] != null) slide.parts[i].localPosition = slide.homes[i] + shift;
            }
        }

        // ---------------- Rows ----------------

        GameObject BuildRowMark(int row, bool checkpoint, bool theme, bool camera, bool filter, bool past, TrackTheme look)
        {
            var holder = new GameObject($"Row {row}");
            holder.transform.SetParent(root, false);
            holder.transform.localPosition = new Vector3(0f, 0f, row);

            // Row number, lying on the ground right of the track (cyan every 10 rows, like the grid).
            var label = new GameObject("Number");
            label.transform.SetParent(holder.transform, false);
            label.transform.localPosition = new Vector3(2.75f, 0.02f, 0f);
            label.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var text = label.AddComponent<TextMesh>();
            text.font = UiFactory.DefaultFont;
            text.text = row == 0 ? "START" : row.ToString();
            text.fontSize = 64;
            text.characterSize = row % 10 == 0 ? 0.075f : 0.055f;
            text.anchor = TextAnchor.MiddleLeft;
            text.color = past ? new Color(1f, 1f, 1f, 0.35f) : row % 10 == 0 ? new Color(0.4f, 0.9f, 1f) : Color.white;
            label.GetComponent<MeshRenderer>().sharedMaterial = UiFactory.DefaultFont.material;

            if (checkpoint)
                Box(holder.transform, new Vector3(5f, 0.04f, 0.12f), new Vector3(0f, 0.03f, 0f), look != null ? look.gem : null).name = "Checkpoint";
            // Trigger bars across the row, like the fan editor's coloured lines.
            if (theme)
                Box(holder.transform, new Vector3(5.6f, 0.03f, 0.14f), new Vector3(0f, 0.035f, -0.32f), themeMaterial).name = "Theme Trigger";
            if (camera)
                Box(holder.transform, new Vector3(5.6f, 0.03f, 0.14f), new Vector3(0f, 0.035f, 0.32f), cameraMaterial).name = "Camera Trigger";
            if (filter)
                Box(holder.transform, new Vector3(5.6f, 0.03f, 0.1f), new Vector3(0f, 0.04f, 0.16f), filterMaterial).name = "Screen FX Trigger";
            if (Own) foreach (var p in editor.PropsAt(row)) Prop(holder.transform, p);
            SetLayer(holder);
            return holder;
        }

        // ---------------- Tiles ----------------

        /// <summary>A Custom Obstacle at rest, on whichever layer it is painted.</summary>
        void Custom(Transform holder, LevelData level, int id, TrackTheme look)
        {
            var def = CustomObstacles.Get(id) ?? level.FindCustom(id);
            if (def != null) CustomObstacleBuilder.Build(holder, def, look, editor.CustomThemes(), CustomObstacleBuilder.Mode.EditorView);
        }

        GameObject BuildCell(LevelData level, int row, int lane, int id, int obstacle, int pickup, TrackTheme look, bool start)
        {
            if (id == 0 && obstacle == 0 && pickup == 0 && !start) return null;
            var holder = new GameObject($"Cell {row},{lane}");
            holder.transform.SetParent(root, false);
            holder.transform.localPosition = new Vector3(lane - 2f, 0f, row);

            if (look != null)
            {
                var spec = TileCatalog.Decode(id);
                // Mover colour: an old arrow tile, else a numbered platform; the arrow comes from the tile or the Object layer.
                var mover = TileCatalog.MoverOf(id, out var arrowDir);
                bool arrow = mover != MoverKind.None;
                if (!arrow && TileCatalog.MoverGroupOf(id, out var groupKind) > 0) mover = groupKind;
                if (!arrow && spec.floor != FloorKind.None && TileCatalog.ArrowOf(obstacle, out var objectArrow))
                {
                    arrow = true;
                    arrowDir = objectArrow;
                    if (mover == MoverKind.None) mover = MoverKind.Auto;
                }
                if (CustomObstacle.IsCustomId(id)) Custom(holder.transform, level, id, look); // a custom floor tile: its model is the floor
                else
                {
                    if (spec.floor != FloorKind.None) Floor(holder.transform, spec.floor, id, mover, look, row, lane, arrow ? arrowDir : Vector2Int.zero);
                    if (spec.item != ItemKind.None) Item(holder.transform, spec, look);
                }
                // The Object and Collect layers stand on the tile.
                if (CustomObstacle.IsCustomId(obstacle)) Custom(holder.transform, level, obstacle, look);
                else if (obstacle != 0)
                {
                    int first = holder.transform.childCount;
                    Item(holder.transform, level.ItemSpec(obstacle, row, lane), look);
                    if (obstacle == PartArgs.CustomMoveBlockId) Group(holder.transform, first, "Object"); // the placed preview moves it alone
                }
                if (CustomObstacle.IsCustomId(pickup)) Custom(holder.transform, level, pickup, look);
                else if (pickup != 0) Item(holder.transform, level.ItemSpec(pickup, row, lane), look);
                PartLooks(holder.transform, level, row, lane, id, obstacle, look);
            }
            else if (id != 0)
            {
                Box(holder.transform, new Vector3(0.95f, 0.5f, 0.95f), new Vector3(0f, -0.25f, 0f), null);
            }

            if (start)
            {
                // A small flag: pole and banner.
                var flagMat = look == null ? null : look.moverAuto != null ? look.moverAuto : look.gem;
                Box(holder.transform, new Vector3(0.12f, 1.2f, 0.12f), new Vector3(0f, 0.6f, 0f), flagMat).name = "Start Pos";
                Box(holder.transform, new Vector3(0.45f, 0.3f, 0.06f), new Vector3(0.22f, 1.05f, 0f), flagMat);
            }
            SetLayer(holder);
            return holder;
        }

        /// <summary>Plays part <paramref name="id"/>'s animation once on a built cell (its ARGS were just edited).</summary>
        public void PreviewPart(int row, int lane, int id)
        {
            var level = Lvl;
            if (level == null || !cells.TryGetValue((long)row * LevelData.Width + lane, out var c) || c.go == null) return;
            foreach (var old in c.go.GetComponents<PlacedAnimation>()) DestroyImmediate(old); // back home first
            PlacedAnimation.Play(c.go, PartArgs.KindOf(id), level.ArgsAt(row, lane, id), row, lane);
        }

        /// <summary>The ARGS a cell's look depends on (trigger number, trigger object height), so it is built again when they change.</summary>
        static int PreviewKey(LevelData level, int row, int lane, int id, int obstacle)
        {
            int key = 0;
            if (id == PartArgs.TriggerTileId) key = level.ArgsAt(row, lane, id).group + 1;
            if (obstacle == PartArgs.TriggerObjectId)
            {
                var a = level.ArgsAt(row, lane, obstacle);
                key = key * 997 + 1 + Mathf.RoundToInt(a.height * 10f) * 4;
            }
            return key;
        }

        /// <summary>Moves the children from <paramref name="first"/> on under one new child.</summary>
        static void Group(Transform parent, int first, string name)
        {
            var g = new GameObject(name).transform;
            g.SetParent(parent, false);
            while (parent.childCount > first + 1) parent.GetChild(first).SetParent(g, false);
        }

        /// <summary>Trigger tile pad with its number, and the Trigger Object as it waits (the bar high).</summary>
        void PartLooks(Transform holder, LevelData level, int row, int lane, int id, int obstacle, TrackTheme look)
        {
            if (id == PartArgs.TriggerTileId)
            {
                var a = level.ArgsAt(row, lane, id);
                Box(holder, new Vector3(0.62f, 0.06f, 0.62f), new Vector3(0f, 0.03f, 0f), PowerMaterial(PartKind.TriggerTile)).name = "Pad";
                var label = new GameObject("Number");
                label.transform.SetParent(holder, false);
                label.transform.localPosition = new Vector3(0f, 0.065f, 0f);
                label.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var text = label.AddComponent<TextMesh>();
                text.font = UiFactory.DefaultFont;
                text.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
                text.text = a.group.ToString();
                text.fontSize = 64;
                text.characterSize = 0.06f;
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.color = new Color(0.05f, 0.15f, 0.08f);
            }
            if (obstacle == PartArgs.TriggerObjectId)
            {
                var a = level.ArgsAt(row, lane, obstacle);
                float up = Mathf.Max(0.9f, a.height);
                var gate = new GameObject("Trigger Object").transform;
                gate.SetParent(holder, false);
                if (look.triggerObjectModel != null)
                {
                    Model(gate, look.triggerObjectModel, look, new Vector3(0f, 0.6f, 0f), new Vector3(1f, 1.2f, 1f), 0f, true);
                    return;
                }
                Box(gate, new Vector3(0.08f, up + 0.2f, 0.12f), new Vector3(-0.46f, (up + 0.2f) * 0.5f, 0f), look.floor);
                Box(gate, new Vector3(0.08f, up + 0.2f, 0.12f), new Vector3(0.46f, (up + 0.2f) * 0.5f, 0f), look.floor);
                Box(gate, new Vector3(0.84f, 0.18f, 0.2f), new Vector3(0f, up, 0f), PowerMaterial(PartKind.TriggerObject)).name = "Bar";
            }
        }

        /// <summary>
        /// Tile Type of a cell when its look has the original floor meshes: shape * 4 + quarter turns, -1 otherwise.
        /// Kept per cell, so a cell is built again when a neighbour changes its shape.
        /// </summary>
        int TileTypeKey(LevelData level, int row, int lane, int id, TrackTheme look, out TileShape shape, out int turns)
        {
            shape = TileShape.X;
            turns = 0;
            if (look == null || !look.HasTileTypes || id == 0 || row >= level.RowCount) return -1;
            var floor = TileCatalog.Decode(id).floor;
            if (floor == FloorKind.None || floor == FloorKind.Jump || floor == FloorKind.SuperJump) return -1;
            if (tileFragiles == null) tileFragiles = new FragileSections(level);
            shape = TileShapes.Choose(TileShapes.Sides(level, tileFragiles, row, lane, look.moverEdge), out turns);
            bool alone = look.tileOD != null && TileShapes.MovesAlone(level, row, lane); // animated tile: Tile OD
            if (alone) turns = 0;
            return (int)shape * 4 + turns + (look.ClassicTiles ? 100 : 0) + (look.ClassicModels ? 200 : 0) + (alone ? 400 : 0);
        }

        /// <param name="arrowDir">Mover arrow on this cell; zero = none.</param>
        void Floor(Transform parent, FloorKind kind, int id, MoverKind mover, TrackTheme look, int row, int lane, Vector2Int arrowDir)
        {
            // Like TrackBuilder.BuildFloor. Model Look Classic: the original mesh shaped by its neighbours (07_jump,
            // original arrows); Model Look RTX: the RTX Models slot, else the built-in slab. Texture Look Classic: the
            // tile set's atlas; Texture Look RTX: the slab materials (on per-cell UVs on the original meshes).
            bool jump = kind == FloorKind.Jump || kind == FloorKind.SuperJump;
            var slab = SlabMaterial(kind, id, mover, look);
            var fragileKind = kind == FloorKind.Fragile ? FragileSections.KindOf(id) : FragileKind.None;
            if (kind == FloorKind.Fragile && fragileKind == FragileKind.None) fragileKind = FragileKind.Fragile0;
            GameObject model = look.ClassicModels ? null : look.FloorModelFor(kind, fragileKind, mover);
            bool tileMesh = look.HasTileTypes && (look.ClassicModels || (model == null && look.ClassicTiles)) && (!jump || look.tileJump != null);

            if (tileMesh && !look.ClassicTiles && slab != null)
            {
                if (jump)
                {
                    TileShapes.Place(parent, look.tileJump, 0, slab, "Tile Jump", true, !look.IsTileAtlas(slab));
                    return;
                }
                TileTypeKey(Lvl, row, lane, id, look, out var rtxShape, out int rtxTurns);
                bool rtxAlone = TileShapes.MovesAlone(Lvl, row, lane);
                var rtxModel = rtxAlone ? look.AloneTileModel(rtxShape, ref rtxTurns) : look.TileModel(rtxShape);
                var rtxSide = slab == look.floor ? look.floorSide : kind == FloorKind.Fragile ? look.fragileSide : null;
                var tile = TileShapes.Place(parent, rtxModel, rtxTurns, slab, "Tile " + rtxShape, false, !look.IsTileAtlas(slab), rtxSide);
                if (rtxAlone && look.tileOD != null) TileShapes.AddUnderside(tile, look.tileO, slab, !look.IsTileAtlas(slab), rtxSide);
                if (kind == FloorKind.Fragile) TileShapes.MarkFragileJoins(tile, rtxTurns, tileFragiles, row, lane);
                if (arrowDir != Vector2Int.zero) Arrow(parent, mover, look, arrowDir,
                    look.moverArrow != null ? look.moverArrow : mover == MoverKind.Auto ? look.tileMoverAuto : look.tileMover);
                return;
            }
            if (tileMesh)
            {
                if (jump)
                {
                    TileShapes.Place(parent, look.tileJump, 0, look.tileGeneral, "Tile Jump", true);
                    return;
                }
                TileTypeKey(Lvl, row, lane, id, look, out var shape, out int turns);
                Material atlas = look.tileGeneral, moverAtlas = look.tileGeneral;
                if (mover == MoverKind.Touch && look.tileMover != null) moverAtlas = look.tileMover;
                if (mover == MoverKind.Auto && look.tileMoverAuto != null) moverAtlas = look.tileMoverAuto;
                if (!look.MoverPlain) atlas = moverAtlas; // Mover Edge = Never: the platform looks like plain floor
                if (kind == FloorKind.Fragile && look.tileFragile != null) atlas = look.tileFragile;
                bool alone = TileShapes.MovesAlone(Lvl, row, lane);
                var tileModel = alone ? look.AloneTileModel(shape, ref turns) : look.TileModel(shape);
                var tileGo = TileShapes.Place(parent, tileModel, turns, atlas, "Tile " + shape, false);
                if (alone && look.tileOD != null) TileShapes.AddUnderside(tileGo, look.tileO, atlas);
                if (arrowDir != Vector2Int.zero) Arrow(parent, mover, look, arrowDir, moverAtlas);
                return;
            }

            Material mat = slab;
            if (model != null && look.ClassicTiles)
            {
                var atlas = jump ? look.tileGeneral : look.TileAtlasFor(kind, mover);
                if (atlas != null) mat = atlas;
            }
            var size = new Vector3(0.97f, 0.5f, 0.97f);
            if (model != null)
            {
                Model(parent, model, look, new Vector3(0f, -0.25f, 0f), size, 1f, false);
                // Painted with the floor's material unless Adjust says otherwise (like TrackBuilder.PaintFloorModel).
                var adjust = look.FindAdjust(model);
                var use = adjust != null && adjust.material != null ? adjust.material : mat;
                if ((adjust == null || !adjust.keepOwnMaterial) && use != null && parent.childCount > 0)
                    look.PaintModel(parent.GetChild(parent.childCount - 1).gameObject, use, false);
            }
            else
            {
                var v = Shape(parent, MeshFactory.BeveledBox(size, 0.07f), mat);
                v.transform.localPosition = new Vector3(0f, -0.25f, 0f);
            }
            if (arrowDir != Vector2Int.zero)
            {
                var a = Shape(parent, MeshFactory.Arrow(), look.moverArrow != null ? look.moverArrow : look.jump);
                a.transform.localRotation = Quaternion.LookRotation(new Vector3(arrowDir.x, 0f, arrowDir.y));
            }
        }

        /// <summary>
        /// The original arrow (01_moveArrow / 02_moveAutoArrow) on a tile mesh, painted with <paramref name="mat"/>
        /// (the mover atlas, or the RTX arrow material); without one, the flat glowing arrow.
        /// </summary>
        void Arrow(Transform parent, MoverKind mover, TrackTheme look, Vector2Int dir, Material mat)
        {
            var original = mover == MoverKind.Auto ? look.tileMoverAutoArrow : look.tileMoverArrow;
            if (original != null && mat != null)
            {
                int t = dir == Vector2Int.up ? 0 : dir == Vector2Int.right ? 1 : dir == Vector2Int.down ? 2 : 3;
                TileShapes.Place(parent, original, t, mat, "Arrow", true);
                return;
            }
            var a = Shape(parent, MeshFactory.Arrow(), look.moverArrow != null ? look.moverArrow : look.jump);
            a.transform.localRotation = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y));
        }

        /// <summary>The built-in slab's material for a floor: Floor, the mover colours, Fragile per ID, Jump.</summary>
        static Material SlabMaterial(FloorKind kind, int id, MoverKind mover, TrackTheme look)
        {
            Material mat = look.floor;
            if (mover == MoverKind.Touch && look.mover != null && !look.MoverPlain) mat = look.mover;
            if (mover == MoverKind.Auto && look.moverAuto != null && !look.MoverPlain) mat = look.moverAuto;
            if (kind == FloorKind.Fragile)
            {
                var k = FragileSections.KindOf(id);
                var own = look.FragileMaterial(k == FragileKind.None ? FragileKind.Fragile0 : k);
                mat = own != null ? own : look.fragile;
            }
            if (kind == FloorKind.Jump || kind == FloorKind.SuperJump) mat = look.jump;
            return mat;
        }

        void Item(Transform parent, TileSpec spec, TrackTheme look)
        {
            switch (spec.item)
            {
                case ItemKind.MoverArrow: return; // drawn on the floor by Floor
                case ItemKind.TriggerObject: return;       // PartLooks, with its ARGS
                case ItemKind.Gem:
                case ItemKind.FloatingGem:
                case ItemKind.Star:
                case ItemKind.FloatingStar:
                {
                    bool star = spec.item == ItemKind.Star || spec.item == ItemKind.FloatingStar;
                    bool floating = spec.item == ItemKind.FloatingGem || spec.item == ItemKind.FloatingStar;
                    var model = star ? look.starModel : look.gemModel;
                    var at = new Vector3(0f, floating ? 1.6f : 0.55f, 0f);
                    if (model != null) Model(parent, model, look, at, Vector3.one * 0.5f, 0.5f, true);
                    else
                    {
                        var g = Shape(parent, MeshFactory.Octahedron(), star ? look.star : look.gem);
                        g.transform.localPosition = at;
                        g.transform.localScale = new Vector3(0.22f, 0.3f, 0.22f);
                    }
                    return;
                }
                case ItemKind.PowerUp:
                {
                    // The power-up cube, floating on its tile in its colour.
                    var kind = PartKind.FlyJump + spec.param;
                    var model = look.PowerUpModel(spec.param);
                    if (model != null) Model(parent, model, look, new Vector3(0f, 0.5f, 0f), Vector3.one * 0.6f, 0.5f, true);
                    else Box(parent, new Vector3(0.52f, 0.52f, 0.52f), new Vector3(0f, 0.5f, 0f), PowerMaterial(kind));
                    return;
                }
                case ItemKind.Crown:
                    if (look.crownModel != null) Model(parent, look.crownModel, look, new Vector3(0f, 0.75f, 0f), Vector3.one * 0.6f, 0.5f, true);
                    else Box(parent, new Vector3(0.5f, 0.3f, 0.5f), new Vector3(0f, 0.75f, 0f), look.star);
                    return;
            }

            switch (spec.item)
            {
                case ItemKind.Pillar:
                case ItemKind.PillarSafe:
                case ItemKind.LaserPillar:
                    PillarPreview(parent, spec, look);
                    return;
                case ItemKind.Crusher:
                case ItemKind.CrusherSafe:
                    if (look.crusherModel != null && EnemyRig.Find(look.crusherModel.transform, "anim") != null)
                    {
                        // Original 02_enemyCrusher at its real size, resting on the tile like in the game.
                        bool safeCrusher = spec.item == ItemKind.CrusherSafe;
                        var v = Original(parent, look.crusherModel, look);
                        var a = EnemyRig.Find(v.transform, "anim");
                        if (a != null) a.rotation = v.transform.parent.rotation;
                        var s1 = EnemyRig.Find(v.transform, "state1");
                        var s2 = EnemyRig.Find(v.transform, "state2");
                        if (s1 != null) s1.gameObject.SetActive(safeCrusher);
                        if (s2 != null) s2.gameObject.SetActive(!safeCrusher);
                        foreach (var t in v.GetComponentsInChildren<Transform>(true))
                            if (t.name == "shadow" || t.name == "shadowAlt") t.gameObject.SetActive(false);
                        return;
                    }
                    break;
                case ItemKind.RollerLeft:
                case ItemKind.RollerRight:
                    if (look.rollerModel != null && EnemyRig.Find(look.rollerModel.transform, "altAnim") != null)
                    {
                        // Original 03_enemyRoller: a wheel facing the ball at its real size, like the game (Roller).
                        var v = Original(parent, look.rollerModel, look);
                        v.transform.localPosition = TrackBuilder.RollerOffset;
                        foreach (var t in v.GetComponentsInChildren<Transform>(true))
                            if (t.name == "shadow" || t.name == "shadowAlt") t.gameObject.SetActive(false);
                        return;
                    }
                    break;
            }

            // Obstacles: the look's model, else a block in the hazard colour.
            var m = ModelFor(spec, look);
            bool safe = spec.item == ItemKind.CrusherSafe || spec.item == ItemKind.PillarSafe;
            float height = spec.item == ItemKind.Pillar || spec.item == ItemKind.PillarSafe || spec.item == ItemKind.LaserPillar
                ? Mathf.Max(1, spec.param) * 0.9f
                : spec.item == ItemKind.Tree || spec.item == ItemKind.Palm ? 1.8f : 0.9f;
            bool floater = spec.item == ItemKind.FloaterAir || spec.item == ItemKind.FloaterGround;
            float y = floater ? TrackBuilder.FloaterHeight - height * 0.5f : 0f; // risen, like the game
            var box = new Vector3(0.9f, height, 0.9f);
            if (m != null) Model(parent, m, look, new Vector3(0f, y + height * 0.5f, 0f), box, 0f, true); // standing on the floor
            else Box(parent, box, new Vector3(0f, y + height * 0.5f, 0f), safe ? look.hazardSafe : look.hazard);
        }

        static GameObject ModelFor(TileSpec spec, TrackTheme t)
        {
            switch (spec.item)
            {
                case ItemKind.Riser: { var r = t.RiserModel(spec.param); return r != null ? r : t.blockModel; }
                case ItemKind.Crusher:
                case ItemKind.CrusherSafe: return t.crusherModel != null ? t.crusherModel : t.blockModel;
                case ItemKind.FloaterGround:
                case ItemKind.FloaterAir: return t.floaterModel != null ? t.floaterModel : t.blockModel;
                case ItemKind.BinaryLeft:
                case ItemKind.BinaryRight:
                    if (spec.param == TileCatalog.BinaryHammer) return t.binaryHammerModel != null ? t.binaryHammerModel : t.binarySlasherModel;
                    if (spec.param == TileCatalog.BinaryMini && t.binaryMiniModel != null) return t.binaryMiniModel;
                    return t.binarySlasherModel != null ? t.binarySlasherModel : t.binaryHammerModel;
                case ItemKind.Pillar:
                case ItemKind.PillarSafe:
                case ItemKind.LaserPillar: return t.pillarModel != null ? t.pillarModel : t.blockModel;
                case ItemKind.RollerLeft:
                case ItemKind.RollerRight: return t.rollerModel;
                case ItemKind.Tree: return t.TreeModel(spec.param);
                case ItemKind.Palm: return t.palmModel;
                default: return t.blockModel;
            }
        }

        Material PowerMaterial(PartKind kind)
        {
            if (!powerMaterials.TryGetValue(kind, out var m) || m == null)
                powerMaterials[kind] = m = Unlit("Editor PowerUp " + kind, PartArgs.ColorOf(kind));
            return m;
        }

        // ---------------- Building blocks ----------------

        GameObject Shape(Transform parent, Mesh mesh, Material mat)
        {
            var go = new GameObject("Shape");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat != null ? mat : PlainMaterial; // no material = magenta
            mr.shadowCastingMode = ShadowCastingMode.On;
            return go;
        }

        static Material plainMaterial;

        /// <summary>Light grey stand-in for a missing theme material (no Theme on the Level Editor object).</summary>
        static Material PlainMaterial
        {
            get
            {
                if (plainMaterial != null) return plainMaterial;
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                plainMaterial = new Material(shader) { name = "Editor Plain", color = new Color(0.78f, 0.8f, 0.86f) };
                return plainMaterial;
            }
        }

        GameObject Box(Transform parent, Vector3 size, Vector3 centre, Material mat)
        {
            var go = Shape(parent, MeshFactory.BeveledBox(size, Mathf.Min(0.06f, size.y * 0.3f)), mat);
            go.transform.localPosition = centre;
            return go;
        }

        /// <summary>A pillar fully risen, block by block like the game (<see cref="PillarRig"/> in preview mode).</summary>
        void PillarPreview(Transform parent, TileSpec spec, TrackTheme look)
        {
            bool safe = spec.item == ItemKind.PillarSafe;
            int h = Mathf.Clamp(spec.param, 1, 4);
            var laser = look.pillarLaserModel;
            bool laserMesh = spec.item == ItemKind.LaserPillar && laser != null
                && EnemyRig.Find(laser.transform, "anim") != null && EnemyRig.Find(laser.transform, "part0") != null;
            var m = laserMesh ? laser : look.pillarModel;
            GameObject root;
            if (m != null && EnemyRig.Find(m.transform, "anim") != null && EnemyRig.Find(m.transform, "part0") != null)
            {
                root = Original(parent, m, look);
                var a = EnemyRig.Find(root.transform, "anim");
                if (a != null) a.rotation = root.transform.parent.rotation;
            }
            else
            {
                root = new GameObject("Pillar Blocks");
                root.transform.SetParent(parent, false);
                var anim = new GameObject("anim").transform;
                anim.SetParent(root.transform, false);
                var block = m != null ? m : look.blockModel;
                for (int i = 0; i < 4; i++)
                {
                    var part = new GameObject("part" + i).transform;
                    part.SetParent(anim, false);
                    part.localPosition = new Vector3(0f, -i, 0f);
                    var centre = new Vector3(0f, -0.5f, 0f);
                    if (block != null) Model(part, block, look, centre, Vector3.one * 0.96f, 0f, true);
                    else Box(part, Vector3.one * 0.96f, centre, safe ? look.hazardSafe : look.hazard);
                }
            }
            var rig = root.AddComponent<PillarRig>();
            rig.preview = true;
            rig.Setup(h, safe);
        }

        /// <summary>A prop (Props tab) placed like the game's TrackBuilder.BuildProp: real size, Turn, Size and Model Adjust.</summary>
        void Prop(Transform rowHolder, PropPlacement p)
        {
            var model = editor.FindPropModel(p.model, out var owner);
            if (model == null) return;
            var v = Original(rowHolder, model, owner);
            var holder = v.transform.parent;
            var adjust = owner.FindAdjust(model);
            holder.localPosition = new Vector3(p.x, p.y, 0f) + (adjust != null ? adjust.offset : Vector3.zero);
            holder.localRotation = Quaternion.Euler(0f, p.rotY + (owner.modelsFaceBall ? 180f : 0f), 0f) *
                                   Quaternion.Euler(adjust != null ? adjust.rotation : Vector3.zero);
            holder.localScale = Vector3.one * p.scale * (adjust != null && adjust.scale > 0f ? adjust.scale : 1f);
        }

        /// <summary>An original Rolling Sky enemy mesh at its real size, not fitted or turned (like the game).</summary>
        GameObject Original(Transform parent, GameObject model, TrackTheme look)
        {
            var holder = new GameObject(model.name).transform;
            holder.SetParent(parent, false);
            var v = Instantiate(model, holder, false);
            v.transform.localPosition = Vector3.zero;
            foreach (var col in v.GetComponentsInChildren<Collider>(true)) Destroy(col);
            foreach (var anim in v.GetComponentsInChildren<Animator>(true)) anim.enabled = false;
            var adjust = look.FindAdjust(model);
            if (adjust == null || !adjust.keepOwnMaterial)
            {
                bool general = (adjust == null || adjust.material == null) && look.UsesGeneralMaterial(model);
                var m = adjust != null && adjust.material != null ? adjust.material
                    : general ? look.GeneralModelMaterial : look.modelMaterial;
                look.PaintModel(v, m, general);
                if (adjust == null || adjust.material == null) look.ApplyLandmarkParts(v, model);
            }
            if (adjust != null)
            {
                holder.localRotation = Quaternion.Euler(adjust.rotation);
                holder.localScale *= adjust.scale <= 0f ? 1f : adjust.scale;
                holder.localPosition += adjust.offset;
            }
            return v;
        }

        /// <summary>A theme model fitted into a box like the game does (uniform scale), with its Model Adjust.</summary>
        void Model(Transform parent, GameObject model, TrackTheme look, Vector3 centre, Vector3 box, float alignY, bool useModelMaterial)
        {
            var holder = new GameObject(model.name).transform;
            holder.SetParent(parent, false);
            var v = Instantiate(model, holder, false);
            foreach (var col in v.GetComponentsInChildren<Collider>(true)) Destroy(col);
            foreach (var anim in v.GetComponentsInChildren<Animator>(true)) anim.enabled = false;
            var adjust = look.FindAdjust(model);
            if (useModelMaterial && (adjust == null || !adjust.keepOwnMaterial))
            {
                bool general = (adjust == null || adjust.material == null) && look.UsesGeneralMaterial(model);
                var m = adjust != null && adjust.material != null ? adjust.material
                    : general ? look.GeneralModelMaterial : look.modelMaterial;
                look.PaintModel(v, m, general);
                if (adjust == null || adjust.material == null) look.ApplyLandmarkParts(v, model);
            }
            holder.localRotation = Quaternion.Euler(0f, look.modelsFaceBall ? 180f : 0f, 0f) *
                                   Quaternion.Euler(adjust != null ? adjust.rotation : Vector3.zero);

            if (look.fitModels && Bounds(holder, parent, out var b))
            {
                float s = float.MaxValue;
                for (int i = 0; i < 3; i++)
                    if (b.size[i] > 0.0001f) s = Mathf.Min(s, box[i] / b.size[i]);
                if (s == float.MaxValue) s = 1f;
                holder.localScale *= s;
                // alignY 1 = top flush with the box top (floors), 0.5 = centred, 0 = on the box bottom.
                float boxY = centre.y + (alignY - 0.5f) * box.y;
                float modelY = b.center.y * s + (alignY - 0.5f) * b.size.y * s;
                holder.localPosition = new Vector3(centre.x - b.center.x * s, boxY - modelY, centre.z - b.center.z * s);
            }
            else holder.localPosition = centre;

            if (adjust != null)
            {
                holder.localScale *= adjust.scale <= 0f ? 1f : adjust.scale;
                holder.localPosition += adjust.offset;
            }
        }

        static bool Bounds(Transform holder, Transform space, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (var r in holder.GetComponentsInChildren<Renderer>(true))
            {
                var wb = r.bounds;
                var c = wb.center;
                var e = wb.extents;
                for (int i = 0; i < 8; i++)
                {
                    var corner = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                    var local = space.InverseTransformPoint(corner);
                    if (!any) { bounds = new Bounds(local, Vector3.zero); any = true; }
                    else bounds.Encapsulate(local);
                }
            }
            return any;
        }

        static void SetLayer(GameObject go)
        {
            go.layer = Layer;
            foreach (Transform t in go.transform) SetLayer(t.gameObject);
        }
    }
}
