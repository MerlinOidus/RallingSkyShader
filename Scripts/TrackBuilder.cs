using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RTXOn
{
    /// <summary>
    /// Streams the level in rows around the ball: builds rows ahead, removes rows behind.
    /// Row i sits at z = i, column c at x = c - 2. Floor top is y = 0.
    /// </summary>
    public class TrackBuilder : MonoBehaviour
    {
        public TrackTheme theme;
        public int rowsAhead = 55;
        public int rowsBehind = 10;
        [Tooltip("Dynamic Loading: rows built per frame while playing (the start builds all it needs at once). " +
                 "Spreading the work keeps the frame rate even; rows close to the ball are always built at once.")]
        [Range(1, 20)] public int rowsPerFrame = 3;
        [Tooltip("Rows nearer the ball than this are built straight away, whatever Rows Per Frame says.")]
        public int urgentRows = 20;
        [Tooltip("Width and length of every floor tile's collider, in tiles (Adrian 2026-10-21: 1.25). Above 1 the colliders " +
                 "of tiles that only touch at a corner overlap, so the ball can roll along a diagonal path; a hole of one " +
                 "tile still drops the ball. 1 = exactly the tile.")]
        [Range(1f, 1.5f)] public float floorColliderSize = 1.25f;

        [Header("Jump pads")]
        public float jumpRows = 4f;
        public float jumpHeight = 1.6f;
        public float superJumpRows = 8f;
        public float superJumpHeight = 3.2f;
        [Tooltip("Width (in lanes) of the deadly box of one-tile hazards. Below 1 leaves a gap between lanes so the ball can slip past.")]
        [Range(0.5f, 1f)] public float hazardWidth = 0.8f;

        const float TileSize = 0.97f;
        const float TileDepth = 0.5f;

        LevelData level;
        Transform ball;
        int nextRow;
        MoverGrid movers;
        FragileSections fragiles;
        readonly Dictionary<int, FragileGroup> fragileGroups = new Dictionary<int, FragileGroup>(); // by section number
        FragileStyle fragileStyle;  // tints for empty fragile slots and the section borders
        readonly Dictionary<string, Mesh> borderMeshes = new Dictionary<string, Mesh>();
        readonly Dictionary<int, GameObject> built = new Dictionary<int, GameObject>();
        readonly Dictionary<int, Transform> holders = new Dictionary<int, Transform>(); // floor tile per cell (row * Width + lane)
        readonly List<int> toRemove = new List<int>();
        readonly Dictionary<int, List<PropPlacement>> propsByRow = new Dictionary<int, List<PropPlacement>>();
        readonly HashSet<string> missingProps = new HashSet<string>();
        readonly HashSet<int> missingCustoms = new HashSet<int>();
        readonly List<Renderer> rendererBuffer = new List<Renderer>();
        readonly HashSet<int> switchedRows = new HashSet<int>();   // rows the Switch power-up turned over
        readonly Dictionary<PartKind, Material> powerMaterials = new Dictionary<PartKind, Material>();

        /// <summary>Rows that get a crown (set by GameManager before Init).</summary>
        [System.NonSerialized] public List<int> checkpointRows;

        struct LookChange { public int row; public int fade; public TrackTheme look; }
        readonly List<LookChange> lookChanges = new List<LookChange>();
        readonly List<Material> lookMaterials = new List<Material>();
        TrackTheme baseTheme;
        IList<ThemeChange> pendingChanges;
        System.Func<string, TrackTheme> findLook;

        // Look colours: every row uses the same "live" materials (the base theme's runtime copies). A look change
        // fades them to the new look's colours as the ball passes its row, so the whole track changes colour at once.
        readonly Dictionary<TrackTheme, TrackTheme> lookViews = new Dictionary<TrackTheme, TrackTheme>();
        Material[] live;            // live materials, in LookSlots order
        Color[] liveEmission;       // their emission before the environment's glow boost
        Material[] fadeStart, fadeTarget;
        Color[] emissionStart, emissionTarget;
        BackgroundView backdrop;    // the looks' background pictures, behind the track
        Midground midground;        // the looks' midground blocks, rising beside and under the track
        Material ballLive;          // the ball's own copy of the Ball material, coloured by the look (TrackTheme.ballColor)
        Color ballBase, ballFrom, ballTo;
        int lookIndex = -1;         // last look change the ball has reached
        bool fading;
        float fadeFrom, fadeRows;

        /// <summary>The ball's material in the look's colour (for the shards when it breaks).</summary>
        public Material BallMaterial => ballLive != null ? ballLive : baseTheme != null ? baseTheme.ball : null;

        /// <summary>Not used any more (looks fade the shared materials); kept for GameManager.</summary>
        public IList<Material> LookMaterials => lookMaterials;

        /// <summary>Theme changes with a "look" switch the track's TrackTheme from their row. Call before Init.</summary>
        public void UseLooks(IList<ThemeChange> changes, System.Func<string, TrackTheme> find)
        {
            pendingChanges = changes;
            findLook = find;
        }

        public void Init(LevelData data, Transform ballTransform)
        {
            level = data;
            CustomObstacles.AddFromLevel(level); // registered: each custom obstacle keeps its own OBJ mesh and texture
            ball = ballTransform;
            // Continuing from a checkpoint: rows far behind the ball are never seen, so don't build them.
            nextRow = Mathf.Max(0, Mathf.FloorToInt(ballTransform.position.z) - rowsBehind);
            fragiles = new FragileSections(data);
            fragileGroups.Clear();
            switchedRows.Clear();
            GroupTriggers.Reset(data, ballTransform.position.z); // trigger tiles and their Trigger Objects / Custom Moves
            fragileStyle?.Dispose();
            fragileStyle = new FragileStyle();
            FindLandmarks(data);

            // Own copies of the materials, so the environment can change glow at runtime
            // without editing the assets.
            if (theme != null) theme = theme.CreateRuntimeCopy();
            baseTheme = theme;
            backdrop = BackgroundView.On(Camera.main);
            if (backdrop != null)
            {
                    backdrop.Show(baseTheme);
            }

            // Moving platforms: only levels with mover tiles get the grid and the director.
            movers = new MoverGrid(data);
            if (movers.HasMovers)
            {
                EnsureMoverMaterials();
                var director = GetComponent<MoverDirector>();
                if (director == null) director = gameObject.AddComponent<MoverDirector>();
                director.Init(this, movers);
            }
            else movers = null;

            ResolveLooks();
            UseBallMaterial(ballTransform);
            midground = GetComponent<Midground>();
            if (midground == null) midground = gameObject.AddComponent<Midground>();
            midground.Init(baseTheme, LookAt, ballTransform, data.RowCount);
            StartInLook(ballTransform.position.z);

            propsByRow.Clear();
            foreach (var p in data.Props)
            {
                if (!propsByRow.TryGetValue(p.row, out var list)) propsByRow[p.row] = list = new List<PropPlacement>();
                list.Add(p);
            }

            Stream(int.MaxValue);
        }

        void Update()
        {
            if (level == null) return;
            UpdateLooks();
            UpdateShadows();
            Stream(rowsPerFrame);
        }

        Light shadowSun;
        LightShadows sunShadows = LightShadows.Soft; // the sun's own shadow type, put back when a look turns them on
        bool shadowsOn = true;

        /// <summary>Turns the sun's shadows on or off with the look the ball is in (TrackTheme.shadows).</summary>
        void UpdateShadows()
        {
            var look = lookIndex >= 0 && lookIndex < lookChanges.Count ? lookChanges[lookIndex].look : baseTheme;
            bool want = (look == null || look.shadows) && GraphicsQuality.ShadowsOn; // Settings > Graphics (Android: only Ultra + Shadow)
            var env = Environment();
            var sun = env != null && env.sun != null ? env.sun : RenderSettings.sun;
            if (sun == null) return;
            if (sun != shadowSun)
            {
                shadowSun = sun;
                if (sun.shadows != LightShadows.None) sunShadows = sun.shadows;
                shadowsOn = sun.shadows != LightShadows.None;
            }
            if (want == shadowsOn) return;
            shadowsOn = want;
            sun.shadows = want ? GraphicsQuality.ShadowType(sunShadows) : LightShadows.None;
        }

        // After the environment has set this frame's glow, so the fragile copies glow with their sources.
        void LateUpdate()
        {
            fragileStyle?.Sync(false);
        }

        void OnDestroy()
        {
            fragileStyle?.Dispose();
            foreach (var m in borderMeshes.Values) if (m != null) Destroy(m);
            borderMeshes.Clear();
            foreach (var m in powerMaterials.Values) if (m != null) Destroy(m);
            powerMaterials.Clear();
            if (ballLive != null) Destroy(ballLive);
        }

        /// <param name="budget">Most rows to build now; rows within Urgent Rows of the ball are built regardless.</param>
        void Stream(int budget)
        {
            int ballRow = Mathf.FloorToInt(ball.position.z);
            int count = 0;
            while (nextRow < level.RowCount && nextRow <= ballRow + rowsAhead &&
                   (count < budget || nextRow <= ballRow + urgentRows))
            {
                // A row that fails to build must not stop the track (Adrian's Android video: the track ended at 20%
                // because the same row threw every frame and the rows after it were never built).
                try { built[nextRow] = BuildRow(nextRow); }
                catch (System.Exception e) { ReportBuildProblem(nextRow, -1, e); }
                nextRow++;
                count++;
            }

            toRemove.Clear();
            foreach (var kv in built)
                if (kv.Key < ballRow - rowsBehind) toRemove.Add(kv.Key);
            foreach (var r in toRemove)
            {
                Destroy(built[r]);
                built.Remove(r);
                for (int col = 0; col < LevelData.Width; col++) holders.Remove(r * LevelData.Width + col);
            }
        }

        /// <summary>
        /// A cell (or row) that could not be built: an error in the log, and the first one per level reaches the game
        /// (<see cref="BuildProblem"/>, shown on screen) so it can be reported from a phone without a log.
        /// </summary>
        public static event System.Action<string> BuildProblem;
        int problemsReported;

        void ReportBuildProblem(int row, int col, System.Exception e)
        {
            string where = col >= 0 ? $"row {row}, lane {col + 1} (tile {level.Get(row, col)}, object {level.GetObject(row, col)}, collect {level.GetCollect(row, col)})" : $"row {row}";
            Debug.LogError($"RTXOn: could not build {where}: {e}");
            if (problemsReported++ == 0) BuildProblem?.Invoke($"Build error at {where}: {e.GetType().Name}: {e.Message}");
        }

        /// <summary>Last row that has been built so far (rows are built ahead of the ball).</summary>
        public int LastBuiltRow => nextRow - 1;

        void ResolveLooks()
        {
            lookChanges.Clear();
            lookViews.Clear();
            lookIndex = -1;
            fading = false;
            live = LookSlots(baseTheme);
            liveEmission = new Color[live.Length];
            for (int i = 0; i < live.Length; i++) liveEmission[i] = Emission(live[i]);
            if (pendingChanges == null || findLook == null || baseTheme == null) return;
            foreach (var c in pendingChanges)
            {
                if (c == null || string.IsNullOrEmpty(c.look)) continue;
                var asset = findLook(c.look);
                if (asset == null) continue;
                lookChanges.Add(new LookChange { row = c.row, fade = Mathf.Max(0, c.lookRows), look = asset });
            }
            lookChanges.Sort((a, b) => a.row.CompareTo(b.row));
        }

        /// <summary>The materials a look change fades, in a fixed order.</summary>
        static Material[] LookSlots(TrackTheme t) => t == null ? new Material[26] : new[]
        {
            t.floor, t.fragile, t.jump, t.hazard, t.hazardSafe, t.gem, t.star,
            t.trunk, t.foliage, t.beamRed, t.beamWhite, t.mover, t.moverAuto, t.moverArrow,
            t.fragile1, t.fragile2, t.fragileChecker, t.fragileRow, t.fragileColumn,
            t.tileGeneral, t.tileFragile, t.tileFragileActive, t.tileMover, t.tileMoverAuto, t.floorSide, t.fragileSide,
        };

        static Color Emission(Material m) => m != null && m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;

        /// <summary>
        /// Starting part way through the level (checkpoint, editor test): the look of that row at once, no fade.
        /// </summary>
        void StartInLook(float ballZ)
        {
            int index = -1;
            for (int i = 0; i < lookChanges.Count; i++) if (lookChanges[i].row <= ballZ) index = i;
            if (index < 0) return;
            lookIndex = index;
            BeginFade(lookChanges[index].look, ballZ, 0);
            FinishFade();
        }

        /// <summary>Runs look fades: Look Rows 2 = the colours fade over 2 rows of ball travel, 0 = they switch at once.</summary>
        void UpdateLooks()
        {
            if (live == null || ball == null) return;
            float z = ball.position.z;
            while (lookIndex + 1 < lookChanges.Count && z >= lookChanges[lookIndex + 1].row)
            {
                if (fading) FinishFade();
                var c = lookChanges[++lookIndex];
                BeginFade(c.look, c.row, c.fade);
            }
            if (!fading) return;
            float t = fadeRows > 0f ? Mathf.Clamp01((z - fadeFrom) / fadeRows) : 1f;
            if (t >= 1f)
            {
                FinishFade();
                return;
            }
            float s = t * t * (3f - 2f * t);
            if (backdrop != null) backdrop.SetFade(s);
            SetBallColor(Color.Lerp(ballFrom, ballTo, s));
            var env = Environment();
            for (int i = 0; i < live.Length; i++)
            {
                var m = live[i];
                if (m == null || fadeTarget[i] == null || fadeStart[i] == null || m.shader != fadeTarget[i].shader) continue;
                m.Lerp(fadeStart[i], fadeTarget[i], s);
                liveEmission[i] = Color.Lerp(emissionStart[i], emissionTarget[i], s);
                if (env != null) env.SetNeonBase(m, liveEmission[i]);
                else m.SetColor("_EmissionColor", liveEmission[i]);
            }
        }

        void BeginFade(TrackTheme look, float fromRow, int rows)
        {
            var targets = LookSlots(look);
            fadeStart = new Material[live.Length];
            fadeTarget = new Material[live.Length];
            emissionStart = (Color[])liveEmission.Clone();
            emissionTarget = new Color[live.Length];
            for (int i = 0; i < live.Length; i++)
            {
                fadeTarget[i] = targets[i];
                emissionTarget[i] = targets[i] != null ? Emission(targets[i]) : liveEmission[i];
                if (live[i] != null && targets[i] != null) fadeStart[i] = new Material(live[i]);
            }
            fadeFrom = fromRow;
            fadeRows = rows;
            fading = true;
            if (backdrop != null) backdrop.BeginFade(look);
            if (ballLive != null)
            {
                ballFrom = BallColor(ballLive);
                ballTo = BallTarget(look);
            }
        }

        void FinishFade()
        {
            var env = Environment();
            for (int i = 0; i < live.Length; i++)
            {
                var m = live[i];
                if (m != null && fadeTarget[i] != null)
                {
                    // Textures and keywords too, which a colour fade can't blend.
                    if (m.shader != fadeTarget[i].shader) m.shader = fadeTarget[i].shader;
                    m.CopyPropertiesFromMaterial(fadeTarget[i]);
                    m.renderQueue = fadeTarget[i].renderQueue; // opaque or see-through, like the new look
                    liveEmission[i] = emissionTarget[i];
                    if (env != null) env.SetNeonBase(m, liveEmission[i]);
                }
                if (fadeStart[i] != null) Destroy(fadeStart[i]);
            }
            fading = false;
            if (backdrop != null) backdrop.FinishFade();
            SetBallColor(ballTo);
            fragileStyle?.Sync(true); // a look can change textures too
        }

        /// <summary>The theme asset of a row: the last look change at or before it, or the level's own theme.</summary>
        TrackTheme LookAt(int row)
        {
            TrackTheme look = baseTheme;
            foreach (var c in lookChanges)
            {
                if (row < c.row) break;
                look = c.look;
            }
            return look;
        }

        const string BallLiveSuffix = " (ball colour)";
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        /// <summary>Gives the ball's sphere its own copy of the Ball material, so a look can colour it (Ball Color).</summary>
        void UseBallMaterial(Transform ballTransform)
        {
            ballLive = null;
            var source = baseTheme != null ? baseTheme.ball : null;
            if (source == null || ballTransform == null) return;
            foreach (var r in ballTransform.GetComponentsInChildren<Renderer>(true))
            {
                var m = r.sharedMaterial;
                if (m == null || (m != source && !m.name.EndsWith(BallLiveSuffix, System.StringComparison.Ordinal))) continue;
                if (ballLive == null)
                {
                    ballLive = new Material(source) { name = source.name + BallLiveSuffix };
                    ballBase = BallColor(source);
                }
                r.sharedMaterial = ballLive;
            }
            if (ballLive == null) return;
            ballFrom = ballTo = BallTarget(baseTheme);
            SetBallColor(ballTo);
        }

        static Color BallColor(Material m) =>
            m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : m.HasProperty(ColorId) ? m.GetColor(ColorId) : Color.white;

        /// <summary>The ball colour of a look: its Ball Color, or the Ball material's own colour when that has alpha 0.</summary>
        Color BallTarget(TrackTheme look)
        {
            if (look == null || look.ballColor.a <= 0f) return ballBase;
            var c = look.ballColor;
            c.a = ballBase.a; // the alpha only says "use it"; the ball keeps its own see-through
            return c;
        }

        void SetBallColor(Color c)
        {
            if (ballLive == null) return;
            if (ballLive.HasProperty(BaseColorId)) ballLive.SetColor(BaseColorId, c);
            if (ballLive.HasProperty(ColorId)) ballLive.SetColor(ColorId, c);
        }

        static EnvironmentDirector Environment() => GameManager.Instance != null ? GameManager.Instance.environment : null;

        /// <summary>
        /// The theme a row is built with: the models of the last look change at or before it, the shared live materials
        /// (so their colours follow the fade).
        /// </summary>
        TrackTheme ThemeForRow(int row)
        {
            TrackTheme look = null;
            foreach (var c in lookChanges)
            {
                if (row < c.row) break;
                look = c.look;
            }
            if (look == null) return baseTheme;
            if (!lookViews.TryGetValue(look, out var view))
            {
                view = Instantiate(look);
                view.name = look.name + " (models)";
                view.floor = baseTheme.floor;
                view.floorSide = baseTheme.floorSide;
                view.fragile = baseTheme.fragile;
                view.fragile1 = baseTheme.fragile1;
                view.fragile2 = baseTheme.fragile2;
                view.fragileChecker = baseTheme.fragileChecker;
                view.fragileRow = baseTheme.fragileRow;
                view.fragileColumn = baseTheme.fragileColumn;
                view.fragileSide = baseTheme.fragileSide;
                view.jump = baseTheme.jump;
                view.hazard = baseTheme.hazard;
                view.hazardSafe = baseTheme.hazardSafe;
                view.gem = baseTheme.gem;
                view.star = baseTheme.star;
                view.pickupMaterial = baseTheme.pickupMaterial;
                view.trunk = baseTheme.trunk;
                view.foliage = baseTheme.foliage;
                view.beamRed = baseTheme.beamRed;
                view.beamWhite = baseTheme.beamWhite;
                view.mover = baseTheme.mover;
                view.moverAuto = baseTheme.moverAuto;
                view.moverArrow = baseTheme.moverArrow;
                view.tileGeneral = baseTheme.tileGeneral;
                view.tileFragile = baseTheme.tileFragile;
                view.tileFragileActive = baseTheme.tileFragileActive;
                view.tileMover = baseTheme.tileMover;
                view.tileMoverAuto = baseTheme.tileMoverAuto;
                view.ball = baseTheme.ball;
                lookViews[look] = view;
            }
            return view;
        }

        readonly List<int> partIds = new List<int>(3);

        GameObject BuildRow(int row)
        {
            theme = ThemeForRow(row);
            try { return BuildRowWithTheme(row); }
            finally { theme = baseTheme; }
        }

        GameObject BuildRowWithTheme(int row)
        {
            var root = new GameObject($"Row {row}");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(0f, 0f, row);

            // All slider tiles of a row move as one piece with the leftmost one, like the original
            // (LevelConstructor: every slider in a row is an anim child of the first slider in that row).
            Transform sliderLeader = null;

            for (int col = 0; col < LevelData.Width; col++)
            try
            {
                int id = level.Get(row, col);
                int obstacle = level.GetObject(row, col), pickup = level.GetCollect(row, col);
                if (id == 0 && obstacle == 0 && pickup == 0) continue;
                var spec = TileCatalog.Decode(id);
                float x = col - 2f;
                bool customTile = CustomObstacle.IsCustomId(id); // a Custom Obstacle made as a floor tile: its model is the floor

                var tint = movers != null ? movers.TintAt(row, col) : MoverKind.None;
                bool slider = spec.floor == FloorKind.SlideLeft || spec.floor == FloorKind.SlideRight;
                Transform anchor = customTile ? null : BuildFloor(spec.floor, root.transform, x, tint, row, col, sliderChild: slider && sliderLeader != null);
                if (slider)
                {
                    if (sliderLeader == null) sliderLeader = anchor;
                    else anchor.SetParent(sliderLeader, true); // keeps its place next to the leader
                }
                if (anchor != null)
                {
                    holders[row * LevelData.Width + col] = anchor;
                    if (movers != null)
                    {
                        var arrowKind = movers.ArrowAt(row, col, out var dir);
                        if (arrowKind != MoverKind.None) MakeArrow(anchor, dir, arrowKind);
                    }
                }
                else
                {
                    anchor = new GameObject("Air").transform;
                    anchor.SetParent(root.transform, false);
                    anchor.localPosition = new Vector3(x, 0f, 0f);
                }
                // The cell's layers: its tile, then the obstacle and the pickup standing on it.
                if (customTile) BuildCustom(id, anchor, row, col);
                else if (spec.item != ItemKind.None) BuildItemParts(spec, anchor);
                int firstObstaclePart = anchor.childCount;
                if (CustomObstacle.IsCustomId(obstacle)) BuildCustom(obstacle, anchor, row, col);
                else if (obstacle != 0) BuildItemParts(level.ItemSpec(obstacle, row, col), anchor);
                // The obstacle's pieces, for the Hide Objects power-up.
                for (int i = firstObstaclePart; i < anchor.childCount; i++) anchor.GetChild(i).gameObject.AddComponent<HideableObject>();
                int firstPickupPart = anchor.childCount;
                if (CustomObstacle.IsCustomId(pickup)) BuildCustom(pickup, anchor, row, col);
                else if (pickup != 0) BuildItemParts(level.ItemSpec(pickup, row, col), anchor);
                int pickupParts = anchor.childCount - firstPickupPart;
                if (spec.item != ItemKind.None || obstacle != 0 || pickup != 0)
                {
                    PinModels(anchor, anchor);
                    NarrowHazards(anchor);
                }
                // One part per layer: a Custom Move Tile can carry a Trigger Object, a trigger tile a power-up...
                level.PartIdsAt(row, col, partIds);
                var riders = new List<Transform>();
                var objectPieces = new List<Transform>();
                for (int i = 0; i < pickupParts; i++) riders.Add(anchor.GetChild(firstPickupPart + i));
                for (int i = firstObstaclePart; i < firstPickupPart; i++) objectPieces.Add(anchor.GetChild(i));
                foreach (int part in partIds)
                {
                    int before = anchor.childCount;
                    BuildPart(part, row, col, anchor);
                    var partLayer = TileCatalog.LayerOf(part);
                    if (partLayer == TileLayer.Collect)
                        for (int i = before; i < anchor.childCount; i++) riders.Add(anchor.GetChild(i)); // a power-up
                    else if (partLayer == TileLayer.Object)
                        for (int i = before; i < anchor.childCount; i++) objectPieces.Add(anchor.GetChild(i));
                }
                RidePickups(anchor, riders);
                ApplyEventGroups(root.transform, anchor, row, col, objectPieces, riders);
            }
            catch (System.Exception e)
            {
                ReportBuildProblem(row, col, e); // one broken cell: the rest of the row is still built
            }

            if (propsByRow.TryGetValue(row, out var props))
                foreach (var p in props) BuildProp(p, root.transform);
            if (checkpointRows != null && checkpointRows.Contains(row)) MakeCheckpoint(root.transform);
            if (row == finishRow) AddLandmark(root.transform, theme.finishModel, 0f, true);
            if (portalRows.TryGetValue(row, out float portalX)) AddLandmark(root.transform, theme.portalModel, portalX, false);
            if (switchedRows.Contains(row)) root.transform.localScale = new Vector3(-1f, 1f, 1f); // built after a Switch
            if (row >= hideFrom && row < hideTo && Time.time < hideUntil) // built while Hide Objects is on
                foreach (var h in root.GetComponentsInChildren<HideableObject>()) h.Hide(hideUntil - Time.time);

            return root;
        }

        // ---------- Finish and portal ----------

        int finishRow = -1;
        readonly Dictionary<int, float> portalRows = new Dictionary<int, float>();

        /// <summary>Finish = last row made only of 110; portal = centre of the 113s in each row that has them.</summary>
        void FindLandmarks(LevelData data)
        {
            finishRow = -1;
            portalRows.Clear();
            for (int r = 0; r < data.RowCount; r++)
            {
                int finish = 0, portals = 0;
                float sum = 0f;
                for (int c = 0; c < LevelData.Width; c++)
                {
                    int id = data.Get(r, c);
                    if (id == TileCatalog.FinishId) finish++;
                    if (id == TileCatalog.PortalId) { portals++; sum += c - 2f; }
                }
                if (finish == LevelData.Width) finishRow = r;
                if (portals > 0) portalRows[r] = sum / portals;
            }
        }

        /// <summary>
        /// Original finish / portal mesh at its real size and the prefab's own rotation (no collider).
        /// No model: the finish gets a glowing line, the portal nothing.
        /// </summary>
        void AddLandmark(Transform row, GameObject model, float x, bool finish)
        {
            if (model == null)
            {
                if (finish)
                    AddVisual(row, null, MeshFactory.BeveledBox(new Vector3(5.2f, 0.06f, 0.25f), 0.02f),
                        theme.star != null ? theme.star : theme.gem, new Vector3(0f, 0.04f, 0f), Vector3.one);
                return;
            }
            var holder = new GameObject(model.name).transform;
            holder.SetParent(row, false);
            holder.localPosition = new Vector3(x, 0f, 0f);
            var v = Instantiate(model, holder, false);
            v.transform.localPosition = Vector3.zero;
            // The original 03_finish / 04_portal are built in the original's frame (floor = XY, up = -Z). A root
            // without the stand-up turn (03_finish ships that way) is stood up like the floor meshes.
            if (v.transform.localRotation == Quaternion.identity &&
                (model.name.StartsWith("03_finish", System.StringComparison.OrdinalIgnoreCase) ||
                 model.name.StartsWith("04_portal", System.StringComparison.OrdinalIgnoreCase)))
            {
                v.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                // GoalWall.ShowAt: the original wall stands half a tile up and half a tile before the finish row.
                if (finish) v.transform.localPosition = new Vector3(0f, 0.5f, -0.5f);
            }
            StripColliders(v);
            var adjust = theme.FindAdjust(model);
            ApplyModelMaterial(v, adjust, model);
            AddModelAnimation(v, model);
            // The original finish wall shatter and end portal waves; nothing for a model without their pieces.
            if (finish) { var wall = v.AddComponent<FinishWall>(); if (!wall.Setup()) Destroy(wall); }
            else { var portal = v.AddComponent<PortalRig>(); if (!portal.Setup(false)) Destroy(portal); }
            if (adjust != null) holder.localRotation = Quaternion.Euler(adjust.rotation);
            ApplyAdjust(holder, adjust, holder.localPosition);
        }

        /// <summary>
        /// Level Settings > Start Portal: the original start portal (04_portal lying on the floor, no brackets) under
        /// the ball at the start. Not part of a row, it removes itself once the ball is far ahead. Null without a
        /// Portal Model that has the original portal pieces.
        /// </summary>
        public PortalRig AddStartPortal(Vector3 at)
        {
            var model = theme != null ? theme.portalModel : null;
            if (model == null) return null;
            var holder = new GameObject("Start Portal").transform;
            holder.SetParent(transform, false);
            holder.position = new Vector3(at.x, 0f, at.z);
            var v = Instantiate(model, holder, false);
            v.transform.localPosition = Vector3.zero;
            if (v.transform.localRotation == Quaternion.identity) v.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            StripColliders(v);
            var adjust = theme.FindAdjust(model);
            ApplyModelMaterial(v, adjust, model);
            var portal = v.AddComponent<PortalRig>();
            if (!portal.Setup(true))
            {
                Destroy(holder.gameObject);
                return null;
            }
            if (adjust != null) holder.localRotation = Quaternion.Euler(adjust.rotation);
            ApplyAdjust(holder, adjust, holder.localPosition);
            return portal;
        }

        // ---------- Floors ----------

        /// <param name="tint">Part of a moving platform: uses the touch or auto mover colour.</param>
        /// <param name="row">Row and lane of the tile, used to join fragile tiles into sections.</param>
        /// <param name="sliderChild">A slider that rides on the row's first slider, so it gets no motion of its own.</param>
        Transform BuildFloor(FloorKind kind, Transform parent, float x, MoverKind tint = MoverKind.None, int row = -1, int lane = -1, bool sliderChild = false)
        {
            if (kind == FloorKind.None) return null;

            Material mat = theme.floor;
            if (tint == MoverKind.Touch && theme.mover != null && !theme.MoverPlain) mat = theme.mover;
            if (tint == MoverKind.Auto && theme.moverAuto != null && !theme.MoverPlain) mat = theme.moverAuto;
            // Each fragile ID has its own material slot (Fragile 1, Fragile Checker...); an empty slot = Fragile, tinted.
            var fragileKind = kind == FloorKind.Fragile
                ? (row >= 0 && lane >= 0 ? FragileSections.KindOf(level.Get(row, lane)) : FragileKind.Fragile0)
                : FragileKind.None;
            if (fragileKind == FragileKind.None && kind == FloorKind.Fragile) fragileKind = FragileKind.Fragile0;
            if (kind == FloorKind.Fragile) mat = FragileMaterial(fragileKind);
            if (kind == FloorKind.Jump || kind == FloorKind.SuperJump) mat = theme.jump;
            // Model Look RTX: the RTX Models slot (empty = built-in slab). Model Look Classic: the original tile meshes.
            var model = theme.ClassicModels ? null : theme.FloorModelFor(kind, fragileKind, tint);

            var holder = new GameObject(kind.ToString());
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = new Vector3(x, 0f, 0f);

            var col = holder.AddComponent<BoxCollider>();
            // Jump pads stay exactly one tile, so a ball passing beside one is not launched by its bigger collider.
            float colliderSize = kind == FloorKind.Jump || kind == FloorKind.SuperJump ? 1f : floorColliderSize;
            col.size = new Vector3(colliderSize, TileDepth, colliderSize);
            col.center = new Vector3(0f, -TileDepth * 0.5f, 0f);

            // Fragile tiles of one section close the gap between them, so the section reads as one slab, like the original.
            var size = new Vector3(TileSize, TileDepth, TileSize);
            var centre = new Vector3(0f, -TileDepth * 0.5f, 0f);
            if (kind == FloorKind.Fragile && fragiles != null && row >= 0)
            {
                const float half = (1f - TileSize) * 0.5f;
                bool l = fragiles.JoinedTo(row, lane, Vector2Int.left), r = fragiles.JoinedTo(row, lane, Vector2Int.right);
                bool f = fragiles.JoinedTo(row, lane, Vector2Int.up), b = fragiles.JoinedTo(row, lane, Vector2Int.down);
                size.x += half * ((l ? 1 : 0) + (r ? 1 : 0));
                size.z += half * ((f ? 1 : 0) + (b ? 1 : 0));
                centre.x = half * 0.5f * ((r ? 1 : 0) - (l ? 1 : 0));
                centre.z = half * 0.5f * ((f ? 1 : 0) - (b ? 1 : 0));
            }
            bool jumpKind = kind == FloorKind.Jump || kind == FloorKind.SuperJump;
            if (UsesTileMesh(kind, model, row, lane))
            {
                // Tile Type: the original mesh, shaped by the neighbours.
                PlaceTileType(holder.transform, kind, tint, fragileKind, row, lane, mat);
            }
            else
            {
                // Texture Look Classic paints a model with its atlas (a built-in slab stays RTX: the atlas needs the tile UVs).
                if (model != null && theme.ClassicTiles)
                {
                    var atlas = jumpKind ? theme.tileGeneral : theme.TileAtlasFor(kind, tint);
                    if (kind == FloorKind.Fragile && atlas != null && fragileStyle != null) atlas = fragileStyle.Get(atlas, fragileKind);
                    if (atlas != null) mat = atlas;
                }
                var visual = AddVisual(holder.transform, model, MeshFactory.BeveledBox(size, 0.07f),
                    mat, centre, Vector3.one, useModelMaterial: false, alignY: 1f); // top flush with the collider
                bool ownMaterial = model != null && !PaintFloorModel(visual, model, mat);
                if (kind == FloorKind.Fragile)
                {
                    if (ownMaterial) ColorFragileModel(visual, fragileKind);
                    if (theme.fragileBorders && fragiles != null && row >= 0) AddFragileBorder(holder.transform, mat, size, centre, row, lane, model != null);
                }
            }

            switch (kind)
            {
                case FloorKind.Fragile:
                {
                    var tile = holder.AddComponent<FragileTile>();
                    tile.row = row;
                    tile.lane = lane;
                    int section = fragiles != null ? fragiles.SectionAt(row, lane) : -1;
                    if (section >= 0)
                    {
                        if (!fragileGroups.TryGetValue(section, out var group))
                            fragileGroups[section] = group = new FragileGroup();
                        tile.group = group;
                    }
                    if (theme.ClassicTiles && theme.tileFragileActive != null && row >= 0)
                        tile.activeMaterial = fragileStyle != null ? fragileStyle.Get(theme.tileFragileActive, fragileKind) : theme.tileFragileActive;
                    break;
                }
                case FloorKind.Jump:
                    AddPad(holder, jumpRows, jumpHeight);
                    break;
                case FloorKind.SuperJump:
                    AddPad(holder, superJumpRows, superJumpHeight);
                    break;
                case FloorKind.SlideLeft:
                case FloorKind.SlideRight:
                    if (sliderChild) break;
                    holder.AddComponent<SliderFloor>().left = kind == FloorKind.SlideLeft; // swings across the track, again and again
                    AddSound(holder, Sfx.Slide, 8.5f, 0.5f);
                    break;
            }
            return holder.transform;
        }

        /// <summary>
        /// A floor cell is an original tile mesh: Model Look Classic, or Model Look RTX with an empty slot and the Classic
        /// texture (the atlas needs the tile's UVs). Jump pads need 07_jump (Tile Jump).
        /// </summary>
        bool UsesTileMesh(FloorKind kind, GameObject model, int row, int lane)
        {
            if (!theme.HasTileTypes || row < 0 || lane < 0) return false;
            if (!theme.ClassicModels && (model != null || !theme.ClassicTiles)) return false;
            bool jumpKind = kind == FloorKind.Jump || kind == FloorKind.SuperJump;
            return !jumpKind || theme.tileJump != null;
        }

        /// <summary>
        /// Paints a floor model (RTX Models) with its floor material: Adjust > Material wins, Keep Own Material keeps
        /// the model's. False when it keeps its own materials.
        /// </summary>
        bool PaintFloorModel(GameObject visual, GameObject model, Material mat)
        {
            if (visual == null) return false;
            var adjust = theme.FindAdjust(model);
            if (adjust != null && adjust.keepOwnMaterial) return false;
            var use = adjust != null && adjust.material != null ? adjust.material : mat;
            if (use == null) return false;
            theme.PaintModel(visual, use, false);
            return true;
        }

        /// <summary>
        /// Tile Type (original floor meshes, <see cref="TileShapes"/>): the mesh for this cell, picked and turned by
        /// which neighbours it joins. RTX look: painted with <paramref name="rtx"/> (the slab's own material: Floor,
        /// Fragile per ID, Mover) on per-cell UVs. Classic look: the atlas material of its kind.
        /// </summary>
        void PlaceTileType(Transform holder, FloorKind kind, MoverKind tint, FragileKind fragileKind, int row, int lane, Material rtx)
        {
            if (kind == FloorKind.Jump || kind == FloorKind.SuperJump)
            {
                // 07_jump: the General atlas, or the RTX Jump material on per-cell UVs.
                bool rtxJump = !theme.ClassicTiles && rtx != null;
                TileShapes.Place(holder, theme.tileJump, 0, rtxJump ? rtx : theme.tileGeneral, "Tile Jump", true, rtxJump && !theme.IsTileAtlas(rtx));
                return;
            }
            var shape = TileShapes.Choose(TileShapes.Sides(level, fragiles, row, lane, theme.moverEdge), out int rotations);
            bool alone = TileShapes.MovesAlone(level, row, lane);
            var model = alone ? theme.AloneTileModel(shape, ref rotations) : theme.TileModel(shape);
            var underside = alone && theme.tileOD != null ? theme.tileO : null; // 16_OD on top, 15_O flipped under it
            if (!theme.ClassicTiles && rtx != null) // an empty slab slot falls back to the atlas
            {
                // An atlas material in a slab slot keeps the atlas UVs.
                // Plain floor and fragile: their sides can have their own material (Floor Side, Fragile Side).
                var side = rtx == theme.floor ? theme.floorSide : kind == FloorKind.Fragile ? theme.fragileSide : null;
                var tile = TileShapes.Place(holder, model, rotations, rtx, "Tile " + shape, false, !theme.IsTileAtlas(rtx), side);
                TileShapes.AddUnderside(tile, underside, rtx, !theme.IsTileAtlas(rtx), side);
                if (kind == FloorKind.Fragile) TileShapes.MarkFragileJoins(tile, rotations, fragiles, row, lane);
                return;
            }
            Material mat = theme.tileGeneral;
            if (theme.MoverPlain) { } // without edge: General, like plain floor (the arrow keeps the mover atlas)
            else if (tint == MoverKind.Touch && theme.tileMover != null) mat = theme.tileMover;
            else if (tint == MoverKind.Auto && theme.tileMoverAuto != null) mat = theme.tileMoverAuto;
            if (kind == FloorKind.Fragile && theme.tileFragile != null)
                mat = fragileStyle != null ? fragileStyle.Get(theme.tileFragile, fragileKind) : theme.tileFragile;
            TileShapes.AddUnderside(TileShapes.Place(holder, model, rotations, mat, "Tile " + shape, false), underside, mat);
        }

        /// <summary>The material of a fragile variant: its own slot, else the Fragile material with the variant's tint.</summary>
        Material FragileMaterial(FragileKind kind)
        {
            var own = theme.FragileMaterial(kind);
            if (own != null) return own;
            return fragileStyle != null ? fragileStyle.Get(theme.fragile, kind) : theme.fragile;
        }

        /// <summary>A Fragile Model keeps its own materials for Fragile 0; the other IDs get their slot's material or a tint.</summary>
        void ColorFragileModel(GameObject visual, FragileKind kind)
        {
            if (visual == null || kind == FragileKind.Fragile0) return;
            var own = theme.FragileMaterial(kind);
            visual.GetComponentsInChildren(true, rendererBuffer);
            foreach (var r in rendererBuffer)
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = own != null ? own : fragileStyle != null ? fragileStyle.Get(mats[i], kind) : mats[i];
                r.sharedMaterials = mats;
            }
            rendererBuffer.Clear();
        }

        /// <summary>
        /// Hatched strips on the sides of a fragile tile that do not join its section, so every section has a border
        /// round it, like the original. Meshes are shared between tiles with the same shape.
        /// </summary>
        void AddFragileBorder(Transform holder, Material mat, Vector3 size, Vector3 centre, int row, int lane, bool onModel)
        {
            bool l = !fragiles.JoinedTo(row, lane, Vector2Int.left), r = !fragiles.JoinedTo(row, lane, Vector2Int.right);
            bool f = !fragiles.JoinedTo(row, lane, Vector2Int.up), b = !fragiles.JoinedTo(row, lane, Vector2Int.down);
            float inset = onModel ? 0.04f : 0.07f; // inside the bevel of the built-in slab
            const float width = 0.09f;
            string key = $"{size}{centre}{inset}{l}{r}{f}{b}";
            if (!borderMeshes.TryGetValue(key, out var mesh))
                borderMeshes[key] = mesh = FragileStyle.BorderMesh(size, centre, inset, width, l, r, f, b);
            if (mesh == null) return;

            var go = new GameObject("Section Border");
            go.transform.SetParent(holder, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = fragileStyle != null ? fragileStyle.Get(mat, FragileKind.None, true) : mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>Lasers: 08_Laser as they fire at 5 rows, then 09_Explosion as they kick back (FireLaser 5 to 4.5 rows).</summary>
        const float ExplosionRows = 4.5f;

        /// <summary>Original roller wheel in its prefab frame: middle 0.5 above the tile, face towards the ball.</summary>
        public static readonly Vector3 RollerOffset = new Vector3(0f, 0.5f, 0.5f);

        /// <summary>Height of a risen floater's middle, like the original (2 blocks up).</summary>
        public const float FloaterHeight = 2.2f;

        /// <summary>Size of the floater's deadly box, in blocks (1 = one whole block around it).</summary>
        public const float FloaterHitbox = 1f;

        static void AddSound(GameObject go, Sfx sfx, float atRows, float volume = 1f)
        {
            var ps = go.AddComponent<ProximitySound>();
            ps.sound = sfx;
            ps.atRows = atRows;
            ps.volume = volume;
        }

        static void AddPad(GameObject holder, float rows, float height)
        {
            var pad = holder.AddComponent<JumpPad>();
            pad.rows = rows;
            pad.height = height;
        }

        // ---------- Items ----------

        void BuildItemParts(TileSpec spec, Transform anchor)
        {
            switch (spec.item)
            {
                case ItemKind.None: return;
                case ItemKind.MoverArrow: return; // MoverGrid reads it; MakeArrow draws it on the floor

                case ItemKind.Gem: MakeGem(anchor, 0.55f, false); break;
                case ItemKind.FloatingGem:
                case ItemKind.FloatingStar:
                {
                    bool star = spec.item == ItemKind.FloatingStar;
                    var g = MakeGem(anchor, 1.6f, star);
                    // On a jump pad the gem sits at the top of the arc.
                    if (spec.floor == FloorKind.Jump)
                        g.transform.localPosition = new Vector3(0f, jumpHeight + 0.3f, jumpRows * 0.5f);
                    break;
                }
                case ItemKind.Star: MakeGem(anchor, 0.6f, true); break;
                case ItemKind.Crown: MakeCrown(anchor); break;
                case ItemKind.PowerUp: break; // BuildPart makes it, with its cell's settings
                case ItemKind.CustomBlock:
                case ItemKind.TriggerObject: break;    // BuildPart too

                case ItemKind.Riser:
                {
                    var riserModel = theme.RiserModel(spec.param); // 13-18 = Riser Model, 151-153 = its variants
                    bool rig = AddOriginalEnemy(anchor, riserModel);
                    var b = MakeBlock(anchor, "Riser", new Vector3(0.9f, 0.9f, 0.9f), theme.hazard, true, riserModel, !rig);
                    b.GetComponent<Hazard>().jumpSafe = true;
                    var m = b.AddComponent<ProximityMover>();
                    m.fromLocal = new Vector3(0f, -0.6f, 0f);
                    m.toLocal = new Vector3(0f, 0.45f, 0f);
                    m.triggerRows = 8f;
                    m.moveRows = 3f;
                    AddSound(b, Sfx.Riser, 8f, 0.7f);
                    break;
                }
                case ItemKind.Crusher:
                case ItemKind.CrusherSafe:
                    MakeCrusher(anchor, spec.item == ItemKind.CrusherSafe);
                    break;
                case ItemKind.Tree:
                case ItemKind.Palm:
                    MakeTree(anchor, spec.item == ItemKind.Palm, spec.param);
                    break;

                case ItemKind.RollerLeft:
                case ItemKind.RollerRight:
                    MakeRoller(anchor, spec.item == ItemKind.RollerLeft);
                    break;

                case ItemKind.FloaterGround:
                case ItemKind.FloaterAir:
                {
                    var b = MakeBlock(anchor, "Floater", new Vector3(0.75f, 0.75f, 0.75f), theme.hazard, true, theme.floaterModel);
                    var f = b.AddComponent<Floater>();
                    // The original rises 2 above the tile plus its 0.2 offset (GameManager.JumpHeight 2): it hangs at the
                    // top of a jump, so a rolling ball passes under it and a jumping ball hits it. Air floaters too.
                    f.baseHeight = FloaterHeight;
                    // Hitbox = one block around the floater (FloaterHitbox), moving with it as it rises.
                    var fc = b.GetComponent<BoxCollider>();
                    fc.center = Vector3.zero;
                    fc.size = Vector3.one * FloaterHitbox;
                    break;
                }
                case ItemKind.BinaryLeft:
                case ItemKind.BinaryRight:
                {
                    MakeBinary(anchor, spec);
                    break;
                }
                case ItemKind.Pillar:
                case ItemKind.PillarSafe:
                case ItemKind.LaserPillar:
                {
                    bool safe = spec.item == ItemKind.PillarSafe;
                    int h = Mathf.Clamp(spec.param, 1, 4);
                    if (!safe)
                    {
                        // The danger is the whole tile (the original's tile check), not the moving blocks.
                        var b = MakeBlock(anchor, "Pillar", new Vector3(0.8f, h, 0.8f), theme.hazard, true, null, false);
                        b.GetComponent<Hazard>().jumpSafe = true;
                    }
                    if (spec.item == ItemKind.LaserPillar && IsPillarMesh(theme.pillarLaserModel))
                    {
                        MakePillarLaser(anchor, h);
                        break;
                    }
                    var top = MakePillar(anchor, h, safe);
                    if (spec.item == ItemKind.LaserPillar)
                    {
                        var laser = MakeLaser(anchor, h);
                        if (top != null)
                        {
                            // Rides up on top of the pillar.
                            laser.transform.SetParent(top, false);
                            laser.transform.localPosition = Vector3.zero;
                            laser.transform.rotation = anchor.rotation;
                        }
                    }
                    break;
                }
                case ItemKind.Spotlight:
                    MakeSpotlight(anchor);
                    break;
                case ItemKind.Laser:
                    MakeLaser(anchor, 0f);
                    break;
            }
        }

        GameObject MakeGem(Transform anchor, float height, bool star)
        {
            var go = new GameObject(star ? "Star" : "Gem");
            go.transform.SetParent(anchor, false);
            go.transform.localPosition = new Vector3(0f, height, 0f);
            var sc = go.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 0.4f;
            var g = go.AddComponent<Gem>();
            g.value = star ? 5 : 1;
            float s = star ? 0.32f : 0.22f;
            var model = star ? (theme.starModel != null ? theme.starModel : theme.gemModel) : theme.gemModel;
            // Gems and stars wear the original M_Fragile (Pickup Material, Adrian 2026-10-23), not the theme's Fragile slot,
            // which can be the glass M_FragileRTX. Empty = the old gem / star material.
            var mat = theme.pickupMaterial != null ? theme.pickupMaterial : star && theme.star != null ? theme.star : theme.gem;
            var visual = AddVisual(go.transform, model, MeshFactory.Octahedron(), mat,
                Vector3.zero, new Vector3(s, s * 1.5f, s), useModelMaterial: false, alignY: 0.5f);
            // The original 01_pickup / 02_star carry Built-in materials (magenta in URP): they take that material.
            if (model != null && mat != null)
                foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                    r.sharedMaterials = mats;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                }
            return go;
        }

        /// <summary>
        /// Binary like the original (TileAnimator.SpinSpinner): it stays on its tile and spins on the spot, half a turn
        /// every 2 rows, with an arm that makes the lanes on its side deadly (thin line at the row centre, like the
        /// original's 0.125 hit margin). Jumping over it is safe.
        /// </summary>
        void MakeBinary(Transform anchor, TileSpec spec)
        {
            bool left = spec.item == ItemKind.BinaryLeft;
            float side = left ? -1f : 1f;
            float h = spec.param >= 2 ? 0.9f : 0.45f;
            // Original BinaryOffset (77-80): the body is drawn one lane away from its side and this tile is only
            // a thin deadly line, like the cells its arm reaches.
            float bodyX = spec.offset ? -side : 0f;

            var go = new GameObject("Binary");
            go.transform.SetParent(anchor, false);
            go.transform.localPosition = new Vector3(0f, h * 0.5f, 0f);
            var bc = go.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = spec.offset ? new Vector3(0.9f, 0.8f, 0.25f) : new Vector3(0.9f, h, 0.9f) * 0.92f;
            var hz = go.AddComponent<Hazard>();
            hz.lethal = true;
            hz.jumpSafe = true;

            var s = go.AddComponent<BinarySpinner>();
            s.clockwise = !left;

            GameObject model;
            // Each kind has its own tiles now (Adrian: a random Slasher / Hammer was "the impostor"): 65-68 / 77-80
            // Slasher, 190-193 Hammer; a theme without that model uses the other one.
            if (spec.param == TileCatalog.BinaryHammer)
                model = theme.binaryHammerModel != null ? theme.binaryHammerModel : theme.binarySlasherModel;
            else if (spec.param >= TileCatalog.BinarySlasher)
                model = theme.binarySlasherModel != null ? theme.binarySlasherModel : theme.binaryHammerModel;
            else
                model = theme.binaryMiniModel != null ? theme.binaryMiniModel : theme.binarySlasherModel;
            // Original BinaryHammer (TileAnimator.PoundHammer): swings down and back on its "anim" Z, no spin.
            s.hammer = model != null && model == theme.binaryHammerModel;
            if (model != null && FindDeep(model.transform, "anim") != null)
            {
                // Original enemy mesh (anim / altAnim / base): built like Tile.CreateAnimator, at its real size, not
                // fitted or turned to the ball. Only "anim" spins; "altAnim" is flipped for left binaries.
                var holder = new GameObject(model.name).transform;
                holder.SetParent(anchor, false);
                holder.localPosition = new Vector3(bodyX, 0f, 0f);
                var v = Instantiate(model, holder, false);
                v.transform.localPosition = Vector3.zero;
                v.transform.localRotation = Quaternion.identity;
                StripColliders(v);
                AddModelAnimation(v, model);
                var adjust = theme.FindAdjust(model);
                ApplyModelMaterial(v, adjust, model);
                if (adjust != null) holder.localRotation = Quaternion.Euler(adjust.rotation);
                ApplyAdjust(holder, adjust, holder.localPosition);

                var alt = s.hammer ? null : FindDeep(v.transform, "altAnim");
                if (alt != null) alt.localEulerAngles = new Vector3(left ? 180f : 0f, 0f, 0f);
                s.spin = FindDeep(v.transform, "anim");
                s.pivot = holder;
            }
            else
            {
                var spin = new GameObject("Spin").transform;
                spin.SetParent(go.transform, false);
                spin.localPosition = new Vector3(bodyX, 0f, 0f);
                if (model != null || theme.blockModel != null)
                {
                    AddModel(spin, model != null ? model : theme.blockModel, Vector3.zero, new Vector3(0.9f, h, 0.9f), true);
                }
                else
                {
                    AddVisual(spin, null, MeshFactory.BeveledBox(new Vector3(0.6f, h, 0.6f), 0.08f), theme.hazard, Vector3.zero, Vector3.one);
                    int armLanes = spec.reach + (spec.offset ? 1 : 0);
                    if (armLanes > 0)
                    {
                        // Built-in arm reaching over the deadly lanes. Built pointing left (-x) like the original meshes' rest
                        // pose: BinarySpinner turns right binaries half a turn, so the arm is on its side when the ball comes.
                        float len = armLanes + 0.3f;
                        AddVisual(spin, null, MeshFactory.BeveledBox(new Vector3(len, 0.18f, 0.18f), 0.04f), theme.hazard,
                            new Vector3(-(0.3f + len * 0.5f) * 0.98f, h * 0.25f, 0f), Vector3.one);
                    }
                }
                s.spin = spin;
            }

            for (int k = 1; k <= spec.reach; k++)
            {
                var arm = new GameObject("Binary Reach");
                arm.transform.SetParent(anchor, false);
                arm.transform.localPosition = new Vector3(side * k, 0.4f, 0f);
                var ac = arm.AddComponent<BoxCollider>();
                ac.isTrigger = true;
                ac.size = new Vector3(0.9f, 0.8f, 0.25f);
                var ah = arm.AddComponent<Hazard>();
                ah.lethal = true;
                ah.jumpSafe = true;
            }
            AddSound(go, Sfx.Woosh, 5.5f, 0.6f);
        }

        /// <summary>Checkpoint: a glowing line across the track (GameManager saves the run when the ball passes it).</summary>
        void MakeCheckpoint(Transform row)
        {
            var go = new GameObject("Checkpoint").transform;
            go.SetParent(row, false);
            go.localPosition = new Vector3(0f, 0f, -0.5f);
            var mat = theme.star != null ? theme.star : theme.gem;
            AddVisual(go, null, MeshFactory.BeveledBox(new Vector3(5.2f, 0.04f, 0.1f), 0.015f), mat, new Vector3(0f, 0.03f, 0f), Vector3.one);
        }

        /// <summary>Crown pickup (tile 150): a spinning gold crown at ball height, taken by touching it.</summary>
        void MakeCrown(Transform anchor)
        {
            var crown = new GameObject("Crown").transform;
            crown.SetParent(anchor, false);
            crown.localPosition = new Vector3(0f, 0.75f, 0f);
            var sc = crown.gameObject.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 0.45f;
            var pickup = crown.gameObject.AddComponent<CrownPickup>();
            var visual = new GameObject("Visual").transform;
            visual.SetParent(crown, false);
            pickup.visual = visual;
            var mat = theme.star != null ? theme.star : theme.gem;
            if (theme.crownModel != null)
            {
                AddModel(visual, theme.crownModel, Vector3.zero, new Vector3(0.6f, 0.5f, 0.6f), false, 0.5f);
                return;
            }
            // Built-in crown: a band of 10 blocks with 5 points and a gem in front.
            const int band = 10;
            for (int i = 0; i < band; i++)
            {
                float a = i * Mathf.PI * 2f / band;
                var part = AddVisual(visual, null, MeshFactory.BeveledBox(new Vector3(0.16f, 0.14f, 0.05f), 0.02f), mat,
                    new Vector3(Mathf.Sin(a) * 0.24f, 0f, Mathf.Cos(a) * 0.24f), Vector3.one);
                part.transform.localRotation = Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f);
            }
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f;
                AddVisual(visual, null, MeshFactory.Octahedron(), mat,
                    new Vector3(Mathf.Sin(a) * 0.24f, 0.15f, Mathf.Cos(a) * 0.24f), new Vector3(0.08f, 0.18f, 0.08f));
            }
            AddVisual(visual, null, MeshFactory.Octahedron(), theme.gem, new Vector3(0f, 0f, -0.27f), new Vector3(0.06f, 0.08f, 0.035f));
        }

        /// <summary>The ball passed a checkpoint: the rows ahead hop one after another.</summary>
        public void PassCheckpoint(int row)
        {
            for (int r = row + 3; r < row + 15; r++)
                if (built.TryGetValue(r, out var go)) RowFx.Play(go, RowFx.Kind.Pop, (r - row - 3) * 0.035f);
        }

        /// <summary>Rows from <paramref name="fromRow"/> on fly up into place one after another (continue, play test).</summary>
        public void RiseRows(int fromRow)
        {
            foreach (var kv in built)
                if (kv.Key >= fromRow) RowFx.Play(kv.Value, RowFx.Kind.Rise, (kv.Key - fromRow) * 0.025f);
        }

        /// <summary>
        /// Original Rolling Sky enemy mesh (riser, trees, palm, tower, small pyramid): placed at its real size on the
        /// tile, not fitted or turned, and animated by <see cref="EnemyRig"/> like the original. False for other models.
        /// </summary>
        bool AddOriginalEnemy(Transform anchor, GameObject model)
        {
            var kind = EnemyRig.KindOf(model);
            if (kind == EnemyRig.Kind.None) return false;
            PlaceOriginal(anchor, model).AddComponent<EnemyRig>().Setup(kind);
            return true;
        }

        /// <summary>An original Rolling Sky enemy mesh on the tile at its real size: not fitted or turned to the ball.</summary>
        GameObject PlaceOriginal(Transform anchor, GameObject model)
        {
            var holder = new GameObject(model.name).transform;
            holder.SetParent(anchor, false);
            var v = Instantiate(model, holder, false);
            v.transform.localPosition = Vector3.zero; // keeps the prefab's own rotation, which stands it upright
            StripColliders(v);
            var adjust = theme.FindAdjust(model);
            ApplyModelMaterial(v, adjust, model);
            AddModelAnimation(v, model);
            if (adjust != null) holder.localRotation = Quaternion.Euler(adjust.rotation);
            ApplyAdjust(holder, adjust, Vector3.zero);
            return v;
        }

        /// <summary>
        /// Crusher like the original (<see cref="CrusherRig"/>): lifts and slams on a 24-row cycle. The danger is the
        /// whole tile, the original way: deadly unless the ball is jumping; a safe crusher never hurts.
        /// </summary>
        void MakeCrusher(Transform anchor, bool safe)
        {
            var go = new GameObject("Crusher");
            go.transform.SetParent(anchor, false);
            go.transform.localPosition = new Vector3(0f, 0.475f, 0f);
            var bc = go.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(0.95f, 0.95f, 0.95f) * 0.92f;
            var hz = go.AddComponent<Hazard>();
            hz.lethal = !safe;
            hz.jumpSafe = true;

            var model = theme.crusherModel;
            if (model != null && FindDeep(model.transform, "anim") != null)
            {
                // Original 02_enemyCrusher: real size; "anim" stands upright on the tile like Tile.CreateAnimator.
                var v = PlaceOriginal(anchor, model);
                var anim = FindDeep(v.transform, "anim");
                if (anim != null) anim.rotation = anchor.rotation * v.transform.parent.localRotation;
                v.AddComponent<CrusherRig>().Setup(safe);
                return;
            }
            // Any other model (or the built-in block) lifts and slams the same way.
            var lift = new GameObject("Lift").transform;
            lift.SetParent(go.transform, false);
            AddVisual(lift, model != null ? model : theme.blockModel, MeshFactory.BeveledBox(new Vector3(0.9f, 0.6f, 0.9f), 0.08f),
                safe ? theme.hazardSafe : theme.hazard, new Vector3(0f, -0.175f, 0f), Vector3.one, useModelMaterial: true);
            go.AddComponent<CrusherRig>().Setup(safe, lift);
        }

        /// <summary>
        /// Pillar like the original (<see cref="PillarRig"/>): a stack of blocks that slams up out of the floor,
        /// <paramref name="h"/> blocks high (safe pillars leave the floor-level gap). The original 18_enemyPillar is
        /// used at its real size; otherwise each block is Pillar Model (else Block Model, else a built-in block)
        /// fitted into a 1x1x1 cube. Returns the moving top of the stack.
        /// </summary>
        Transform MakePillar(Transform anchor, int h, bool safe)
        {
            var model = theme.pillarModel;
            GameObject root;
            if (IsPillarMesh(model))
            {
                root = PlaceOriginal(anchor, model);
                var a = FindDeep(root.transform, "anim");
                if (a != null) a.rotation = anchor.rotation * root.transform.parent.localRotation;
            }
            else
            {
                root = new GameObject("Pillar Blocks");
                root.transform.SetParent(anchor, false);
                var anim = new GameObject("anim").transform;
                anim.SetParent(root.transform, false);
                var block = model != null ? model : theme.blockModel;
                for (int i = 0; i < 4; i++)
                {
                    var part = new GameObject("part" + i).transform;
                    part.SetParent(anim, false);
                    part.localPosition = new Vector3(0f, -i, 0f);
                    AddVisual(part, block, MeshFactory.BeveledBox(new Vector3(0.96f, 0.96f, 0.96f), 0.06f),
                        safe ? theme.hazardSafe : theme.hazard, new Vector3(0f, -0.5f, 0f), Vector3.one, useModelMaterial: true);
                }
            }
            root.AddComponent<PillarRig>().Setup(h, safe);
            return FindDeep(root.transform, "anim");
        }

        /// <summary>An original pillar mesh: "anim" holding the blocks "part0".."part3".</summary>
        static bool IsPillarMesh(GameObject model) =>
            model != null && FindDeep(model.transform, "anim") != null && FindDeep(model.transform, "part0") != null;

        /// <summary>
        /// Pillar laser like the original (tile 105 on 19_enemyPillarLaser, <see cref="PillarLaserRig"/>): rises like a
        /// pillar, shakes, and at 5 rows shows its "state2" beam and makes the 15 cells of its lane towards the ball deadly.
        /// </summary>
        void MakePillarLaser(Transform anchor, int h)
        {
            const float BeamRows = 15f, BeamWidth = LaserWidth, BeamY = 0.45f;

            var root = PlaceOriginal(anchor, theme.pillarLaserModel);
            var anim = FindDeep(root.transform, "anim");
            if (anim != null) anim.rotation = anchor.rotation * root.transform.parent.localRotation;
            root.AddComponent<PillarRig>().Setup(h, false);

            var state1 = FindDeep(root.transform, "state1");
            var state2 = FindDeep(root.transform, "state2");
            if (state2 != null)
                foreach (var r in state2.GetComponentsInChildren<Renderer>(true))
                {
                    // Glows like the remake's other beams (the original material is Built-in only).
                    if (theme.beamRed != null) r.sharedMaterial = theme.beamRed;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                }

            // The danger: the beam's cells along the lane, towards the ball, at ball height.
            var beam = new GameObject("Beam");
            beam.transform.SetParent(anchor, false);
            beam.transform.localPosition = new Vector3(0f, BeamY, 0f);
            var bc = beam.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(BeamWidth, BeamWidth, BeamRows);
            bc.center = new Vector3(0f, 0f, -BeamRows * 0.5f);
            beam.AddComponent<Hazard>().jumpSafe = true;
            if (state2 == null)
            {
                var mesh = MakeBeamMesh(beam.transform, theme.beamRed, BeamWidth, BeamRows);
                mesh.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                mesh.transform.localPosition = new Vector3(0f, 0f, -BeamRows * 0.5f);
            }
            var l = MakeLight(beam.transform, theme.laserLightColor, LightType.Point);
            l.transform.localPosition = new Vector3(0f, 0.2f, -0.6f);
            l.range = 3.5f;

            root.AddComponent<PillarLaserRig>().Setup(anim, state1 != null ? state1.gameObject : null,
                state2 != null ? state2.gameObject : null, beam);
            AddSound(root, Sfx.Laser, 5f, 0.6f);
            AddSound(root, Sfx.Explosion, ExplosionRows, 0.6f);
        }

        /// <summary>
        /// One of <paramref name="main"/> and <paramref name="extra"/> (empty slots skipped), the same every build for
        /// a given tile, like the original's coin flip between tree / hammer looks.
        /// </summary>
        static GameObject Pick(Transform anchor, GameObject main, GameObject[] extra)
        {
            int count = main != null ? 1 : 0;
            if (extra != null) foreach (var e in extra) if (e != null) count++;
            if (count <= 1 && main != null) return main;
            if (count == 0) return null;

            var p = anchor.position;
            unchecked
            {
                int h = Mathf.RoundToInt(p.z) * 73856093 ^ Mathf.RoundToInt(p.x * 2f) * 19349663;
                int i = (h & 0x7fffffff) % count;
                if (main != null) { if (i == 0) return main; i--; }
                foreach (var e in extra)
                {
                    if (e == null) continue;
                    if (i == 0) return e;
                    i--;
                }
            }
            return main;
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform c in root)
            {
                var f = FindDeep(c, name);
                if (f != null) return f;
            }
            return null;
        }

        GameObject MakeBlock(Transform anchor, string name, Vector3 size, Material mat, bool lethal, GameObject model, bool visual = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(anchor, false);
            go.transform.localPosition = new Vector3(0f, size.y * 0.5f, 0f);
            var bc = go.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = size * 0.92f;
            go.AddComponent<Hazard>().lethal = lethal;
            if (visual)
                AddVisual(go.transform, model != null ? model : theme.blockModel, MeshFactory.BeveledBox(size, 0.08f), mat,
                Vector3.zero, Vector3.one, useModelMaterial: true);
            return go;
        }

        void MakeTree(Transform anchor, bool palm, int variant)
        {
            var go = new GameObject(palm ? "Palm" : "Tree");
            go.transform.SetParent(anchor, false);
            float trunkH = palm ? 1.8f : 0.9f;
            float height = trunkH + 1.6f;
            var bc = go.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(0.55f, height, 0.55f);
            bc.center = new Vector3(0f, height * 0.5f, 0f);
            go.AddComponent<Hazard>();
            if (!palm) AddSound(go, Sfx.Riser, 8f, 0.7f); // trees and their variants rise with 10_Movement1, like risers

            // 21-24 = Tree Model, 154-156 = its variants (each its own ID, never picked at random).
            var model = palm ? (theme.palmModel != null ? theme.palmModel : theme.treeModel) : theme.TreeModel(variant);
            if (AddOriginalEnemy(anchor, model)) return;
            if (model != null)
            {
                AddModel(go.transform, model, new Vector3(0f, height * 0.5f, 0f), new Vector3(0.8f, height, 0.8f), true);
                return;
            }
            AddVisual(go.transform, null, MeshFactory.BeveledBox(new Vector3(0.22f, trunkH, 0.22f), 0.04f), theme.trunk,
                new Vector3(0f, trunkH * 0.5f, 0f), Vector3.one);
            AddVisual(go.transform, null, MeshFactory.Octahedron(), theme.foliage,
                new Vector3(0f, trunkH + 0.7f, 0f), palm ? new Vector3(0.75f, 0.45f, 0.75f) : new Vector3(0.55f, 1.0f, 0.55f));
            if (!palm)
                AddVisual(go.transform, null, MeshFactory.Octahedron(), theme.foliage,
                    new Vector3(0f, trunkH + 1.35f, 0f), new Vector3(0.38f, 0.7f, 0.38f));
        }

        void MakeRoller(Transform anchor, bool left)
        {
            var go = new GameObject("Roller");
            go.transform.SetParent(anchor, false);
            go.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            var bc = go.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(0.7f, 0.7f, 0.9f);
            go.AddComponent<Hazard>();

            var r = go.AddComponent<Roller>();
            r.left = left;
            AddSound(go, Sfx.Woosh, 3f, 0.7f);

            if (theme.rollerModel != null && FindDeep(theme.rollerModel.transform, "altAnim") != null)
            {
                // Original 03_enemyRoller: a wheel standing on its edge with its face (the prefab's -Z) towards the ball,
                // like the prefab preview (Adrian: the original's top-down drum looked like it was lying down). Its
                // "altAnim" turns round the wheel's axis as it paces, rolling without slipping. The prefab's offset
                // (0.5 along -Z) is undone so the wheel's middle sits 0.5 above the tile.
                var holder = new GameObject(theme.rollerModel.name).transform;
                holder.SetParent(go.transform, false);
                holder.localPosition = new Vector3(0f, -go.transform.localPosition.y, 0f);
                var v = Instantiate(theme.rollerModel, holder, false);
                v.transform.localPosition = RollerOffset;
                v.transform.localRotation = Quaternion.identity;
                StripColliders(v);
                var adjust = theme.FindAdjust(theme.rollerModel);
                ApplyModelMaterial(v, adjust, theme.rollerModel);
                AddModelAnimation(v, theme.rollerModel);
                if (adjust != null) holder.localRotation = Quaternion.Euler(adjust.rotation);
                ApplyAdjust(holder, adjust, holder.localPosition);
                foreach (var t in v.GetComponentsInChildren<Transform>(true))
                    if (t.name == "shadow" || t.name == "shadowAlt") t.gameObject.SetActive(false);
                TileShapes.SwitchState(v.transform, false);
                r.drum = FindDeep(v.transform, "altAnim");
                return;
            }

            // The pivot spins around Z (the roll); the log inside lies along Z.
            var pivot = new GameObject("Spin").transform;
            pivot.SetParent(go.transform, false);
            if (theme.rollerModel != null)
            {
                AddModel(pivot, theme.rollerModel, Vector3.zero, new Vector3(0.8f, 0.8f, 0.95f), true, 0.5f); // centred on the spin axis
            }
            else
            {
                var log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                DropCollider(log);
                log.transform.SetParent(pivot, false);
                log.transform.localScale = new Vector3(0.8f, 0.47f, 0.8f);
                log.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                log.GetComponent<MeshRenderer>().sharedMaterial = theme.hazard;
            }

            r.spinner = pivot;
        }

        /// <summary>Lamp hanging above the track that swings ±30° like the original, lit within 15 rows.</summary>
        void MakeSpotlight(Transform anchor)
        {
            if (theme.spotlightModel != null)
            {
                MakeSpotlightModel(anchor);
                return;
            }
            const float pivotHeight = 5.5f;
            var go = new GameObject("Spotlight");
            go.transform.SetParent(anchor, false);
            go.transform.localPosition = new Vector3(0f, pivotHeight, 0f);

            // In the original only the lamp's own tile is deadly; the swinging beam is just light. The hazard
            // therefore stays still on the tile: a collider on the swinging lamp reached into the next lane
            // (5.5 high, so a few degrees of swing moved its bottom most of a lane sideways).
            var danger = new GameObject("Spotlight Danger");
            danger.transform.SetParent(anchor, false);
            var bc = danger.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(0.45f, 1.2f, 0.45f);
            bc.center = new Vector3(0f, 0.6f, 0f);
            danger.AddComponent<Hazard>().jumpSafe = true;

            var beam = MakeBeamMesh(go.transform, theme.beamWhite, 0.7f, pivotHeight);
            beam.transform.localPosition = new Vector3(0f, -pivotHeight * 0.5f, 0f);

            var l = MakeLight(go.transform, theme.spotlightColor, LightType.Spot);
            l.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            l.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            l.spotAngle = 30f;
            l.range = 9f;

            beam.AddComponent<BeamPulse>().beamLight = l;
            var sw = go.AddComponent<Sweeper>();
            sw.beam = beam.GetComponent<Renderer>();
            sw.lamp = l;
            AddSound(go, Sfx.Woosh, 6f, 0.4f);
        }

        /// <summary>
        /// Spotlight with the theme's Spotlight Model, like the original: the lamp stands on its tile and swings its
        /// beam up into the sky (±30°, the model's "anim" swings with it); only the lamp's tile is deadly.
        /// </summary>
        void MakeSpotlightModel(Transform anchor)
        {
            const float BeamLength = 9f;
            var danger = new GameObject("Spotlight Danger");
            danger.transform.SetParent(anchor, false);
            var bc = danger.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(0.45f, 1.2f, 0.45f);
            bc.center = new Vector3(0f, 0.6f, 0f);
            danger.AddComponent<Hazard>().jumpSafe = true;

            var holder = AddHazardModel(anchor, theme.spotlightModel, new Vector3(0.7f, 1f, 0.7f));
            Transform modelAnim = null;
            GameObject state1 = null, state2 = null;
            foreach (var t in holder.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "anim" && modelAnim == null) modelAnim = t;
                else if (t.name == "state1" && state1 == null) state1 = t.gameObject;
                else if (t.name == "state2" && state2 == null) state2 = t.gameObject;
                else if (t.name == "laserBeam") t.gameObject.SetActive(false); // the remake's glowing beam replaces it
            }

            // The swinging part: pivots at the lamp on the ground, the beam goes up.
            var go = new GameObject("Spotlight");
            go.transform.SetParent(anchor, false);
            go.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            var beam = MakeBeamMesh(go.transform, theme.beamWhite, 0.7f, BeamLength);
            beam.transform.localPosition = new Vector3(0f, BeamLength * 0.5f, 0f);
            var l = MakeLight(go.transform, theme.spotlightColor, LightType.Point);
            l.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            l.range = 3f;
            beam.AddComponent<BeamPulse>().beamLight = l;

            var sw = go.AddComponent<Sweeper>();
            sw.beam = beam.GetComponent<Renderer>();
            sw.lamp = l;
            sw.modelAnim = modelAnim;
            sw.modelState1 = state1;
            sw.modelState2 = state2;
            // The model's own Z turn has to match the beam's in the world (a model turned round swings the other way).
            sw.modelSign = Vector3.Dot(holder.forward, anchor.forward) >= 0f ? 1f : -1f;
            AddSound(go, Sfx.Woosh, 6f, 0.4f);
        }

        /// <summary>
        /// Laser turret: a small block that, when the ball is 5 rows away, fires a red beam 15 rows back along its
        /// lane towards the ball (the original's EnemyLaser). Stay out of its lane or jump over it.
        /// </summary>
        GameObject MakeLaser(Transform anchor, float baseY)
        {
            const float BeamRows = 15f;  // original: the 15 cells behind the turret turn deadly
            const float BeamWidth = LaserWidth;
            const float BeamY = 0.45f;

            var go = new GameObject("Laser");
            go.transform.SetParent(anchor, false);
            go.transform.localPosition = new Vector3(0f, baseY, 0f);

            var turret = MakeBlock(go.transform, "Turret", new Vector3(0.5f, 0.5f, 0.5f), theme.hazard, true, null, theme.laserModel == null);
            turret.GetComponent<Hazard>().jumpSafe = true;
            var laserModel = AddHazardModel(go.transform, theme.laserModel, new Vector3(0.8f, 0.8f, 0.8f));
            if (laserModel != null) laserModel.gameObject.AddComponent<LaserRig>().Setup(); // the original's look, timed like the beam

            // The beam root holds the collider, the hazard and the light; the laser switches it on and off.
            var root = new GameObject("Beam");
            root.transform.SetParent(go.transform, false);
            root.transform.localPosition = new Vector3(0f, BeamY, 0f);
            var bc = root.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(BeamWidth, BeamWidth, BeamRows);
            bc.center = new Vector3(0f, 0f, -BeamRows * 0.5f);
            root.AddComponent<Hazard>().jumpSafe = true;

            var beam = MakeBeamMesh(root.transform, theme.beamRed, BeamWidth, BeamRows);
            beam.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // the cylinder's length now runs along z
            beam.transform.localPosition = new Vector3(0f, 0f, -BeamRows * 0.5f);

            var l = MakeLight(root.transform, theme.laserLightColor, LightType.Point);
            l.transform.localPosition = new Vector3(0f, 0.2f, -0.6f);
            l.range = 3.5f;
            beam.AddComponent<BeamPulse>().beamLight = l;

            go.AddComponent<LaserBeam>().beam = root;
            AddSound(go, Sfx.Laser, 5f, 0.6f);
            AddSound(go, Sfx.Explosion, ExplosionRows, 0.6f);
            return go;
        }

        /// <summary>
        /// Width of a laser beam and its deadly box (Adrian: almost a tile, 0.75). The original's 0.5 box with the
        /// ball-centre hit test let a ball a little off the lane centre roll through the beam.
        /// </summary>
        const float LaserWidth = 0.75f;

        /// <summary>
        /// Takes the collider off a CreatePrimitive shape. A build with Strip Engine Code (Android) can leave a cylinder
        /// without its CapsuleCollider (Unity strips the class when nothing else uses it), so it may be missing.
        /// </summary>
        static void DropCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c == null) return;
            c.enabled = false;
            Destroy(c);
        }

        static GameObject MakeBeamMesh(Transform parent, Material mat, float width, float length)
        {
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beam.name = "Beam";
            // Parent first: a shape left at the world origin by an error showed as a big magenta pillar at the start.
            beam.transform.SetParent(parent, false);
            DropCollider(beam);
            beam.transform.localScale = new Vector3(width, length * 0.5f, width); // Unity cylinder is 2 units tall
            var mr = beam.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            return beam;
        }

        static Light MakeLight(Transform parent, Color hdrColor, LightType type)
        {
            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(parent, false);
            var l = lightGo.AddComponent<Light>();
            l.type = type;
            float max = hdrColor.maxColorComponent;
            l.color = max > 0f ? hdrColor / max : Color.white;
            l.intensity = max;
            l.shadows = LightShadows.None;
            return l;
        }

        // ---------- Parts: power-ups and animated tiles ----------

        /// <summary>
        /// A power-up cube (160-168), or the motion of an animated tile (170-177, 194-199), with the settings of its cell
        /// (level.json "parts", else the defaults).
        /// </summary>
        void BuildPart(int id, int row, int col, Transform anchor)
        {
            var args = level.ArgsAt(row, col, id);
            var kind = args.Kind;
            float start = Mathf.Max(0f, args.startRows);
            switch (kind)
            {
                case PartKind.GemRise:
                case PartKind.GemDrop:
                case PartKind.CrownRise:
                case PartKind.CrownDrop:
                {
                    bool crown = kind == PartKind.CrownRise || kind == PartKind.CrownDrop;
                    var pickup = anchor.Find(crown ? "Crown" : "Gem");
                    if (pickup == null) return;
                    float floor = pickup.localPosition.y;
                    bool rise = kind == PartKind.GemRise || kind == PartKind.CrownRise;
                    var m = pickup.gameObject.AddComponent<ProximityMover>();
                    m.fromLocal = new Vector3(0f, rise ? floor : args.height, 0f);
                    m.toLocal = new Vector3(0f, rise ? args.height : floor, 0f);
                    m.triggerRows = start;
                    m.moveRows = Mathf.Clamp(start * 0.5f, 1f, 4f);
                    return;
                }
                case PartKind.SlideFromLeft:
                case PartKind.SlideFromRight:
                {
                    var slide = anchor.gameObject.AddComponent<SlideIn>();
                    slide.offsetX = kind == PartKind.SlideFromLeft ? -4f : 4f;
                    slide.slideRows = Mathf.Clamp(start * 0.6f, 1f, 5f);
                    slide.settleRows = Mathf.Max(0.5f, start - slide.slideRows);
                    AddSound(anchor.gameObject, Sfx.Slide, start, 0.4f);
                    return;
                }
                case PartKind.RiseUp:
                case PartKind.SinkDown:
                {
                    bool up = kind == PartKind.RiseUp;
                    var home = anchor.localPosition;
                    var low = home + Vector3.down * Mathf.Max(0.5f, args.height);
                    var m = anchor.gameObject.AddComponent<ProximityMover>();
                    m.fromLocal = up ? low : home;
                    m.toLocal = up ? home : low;
                    m.triggerRows = start;
                    m.moveRows = up ? Mathf.Clamp(start * 0.5f, 1f, 5f) : Mathf.Clamp(start * 0.6f, 0.5f, 4f);
                    AddSound(anchor.gameObject, Sfx.Riser, start, 0.5f);
                    return;
                }
                case PartKind.DropIn:
                {
                    var home = anchor.localPosition;
                    var m = anchor.gameObject.AddComponent<ProximityMover>();
                    m.fromLocal = home + Vector3.up * Mathf.Max(0.5f, args.height);
                    m.toLocal = home;
                    m.triggerRows = start;
                    m.moveRows = Mathf.Clamp(start * 0.5f, 1f, 5f);
                    m.ease = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2f, 0f)); // falls faster and faster
                    AddSound(anchor.gameObject, Sfx.Riser, start - m.moveRows * 0.7f, 0.4f);
                    return;
                }
                case PartKind.Shuffle:
                {
                    // Always the same other lane for this cell, so every run looks the same.
                    int other = (int)((uint)(row * 73856093 ^ col * 19349663) % (LevelData.Width - 1));
                    if (other >= col) other++;
                    var slide = anchor.gameObject.AddComponent<SlideIn>();
                    slide.offsetX = other - col;
                    slide.slideRows = Mathf.Clamp(start * 0.6f, 1f, 5f);
                    slide.settleRows = Mathf.Max(0.5f, start - slide.slideRows);
                    AddSound(anchor.gameObject, Sfx.Slide, start, 0.4f);
                    return;
                }
                case PartKind.Wave:
                case PartKind.SpinIn:
                case PartKind.Extend:
                case PartKind.FallAway:
                {
                    var anim = anchor.gameObject.AddComponent<FloorAnimation>();
                    anim.mode = kind == PartKind.Wave ? FloorAnimation.Mode.Wave
                              : kind == PartKind.SpinIn ? FloorAnimation.Mode.SpinIn
                              : kind == PartKind.Extend ? FloorAnimation.Mode.Extend
                              : FloorAnimation.Mode.FallAway;
                    anim.startRows = start;
                    anim.height = Mathf.Max(0.05f, args.height);
                    anim.moveRows = kind == PartKind.FallAway ? 4f : Mathf.Clamp(start * 0.6f, 1f, 5f);
                    anim.spinLeft = ((row * 31 + col * 17) & 1) == 0;
                    if (kind == PartKind.SpinIn) AddSound(anchor.gameObject, Sfx.Slide, start, 0.35f);
                    return;
                }
                case PartKind.FlipIn:
                case PartKind.PopIn:
                case PartKind.Drawbridge:
                case PartKind.ScrewUp:
                case PartKind.RollIn:
                case PartKind.FlyAway:
                case PartKind.BlinkIn:
                {
                    var anim = anchor.gameObject.AddComponent<FloorAnimation>();
                    anim.mode = FloorAnimation.Mode.FlipIn + (kind - PartKind.FlipIn);
                    anim.startRows = start;
                    anim.height = Mathf.Max(0.05f, args.height);
                    anim.moveRows = kind == PartKind.FlyAway ? 4f : kind == PartKind.BlinkIn ? Mathf.Clamp(start * 0.8f, 1f, 8f) : Mathf.Clamp(start * 0.6f, 1f, 5f);
                    anim.spinLeft = ((row * 31 + col * 17) & 1) == 0;
                    if (kind != PartKind.FlyAway && kind != PartKind.BlinkIn) AddSound(anchor.gameObject, Sfx.Slide, start, 0.35f);
                    return;
                }
                case PartKind.CustomMoveTile:
                    AddCustomMotion(anchor.gameObject, args);
                    return;
                case PartKind.CustomMoveBlock:
                {
                    // Kill off = safe: a hazard that does not kill, the floor's look, and the cell's gem / crown / power-up rides on top of it
                    // (moved there after the whole cell is built, RidePickups).
                    var block = MakeBlock(anchor, "Custom Move Block", new Vector3(0.8f, 0.8f, 0.8f), args.kill ? theme.hazard : theme.floor, args.kill, null);
                    block.AddComponent<HideableObject>();
                    AddCustomMotion(block, args);
                    return;
                }
                case PartKind.TriggerTile:
                    MakeTriggerPad(anchor, args);
                    return;
                case PartKind.TriggerObject:
                    MakeTriggerObject(anchor, args);
                    return;
            }
            if (PartArgs.IsPowerUp(kind)) MakePowerUp(anchor, args, row);
        }

        /// <summary>A safe Custom Move Block carries the cell's gem, crown or power-up on top of it.</summary>
        /// <summary>
        /// Event Groups of a cell (Level Editor > Edit > GROUP): the layer's pieces go under a holder with a
        /// <see cref="GroupEventRunner"/> (its groups' events move it, Wait for trigger keeps its animation still), and
        /// a layer that Fires groups gets a <see cref="GroupFire"/>. A tile's holder carries what stands on it; an object
        /// or collect without groups of its own on such a tile gets an empty holder, so the tile's sleep is not its own.
        /// </summary>
        void ApplyEventGroups(Transform rowRoot, Transform anchor, int row, int col, List<Transform> objectPieces, List<Transform> collectPieces)
        {
            if (level.CellGroups.Count == 0) return;
            var tile = level.CellGroupAt(row, col, TileLayer.Tile);
            var obj = level.CellGroupAt(row, col, TileLayer.Object);
            var pick = level.CellGroupAt(row, col, TileLayer.Collect);
            if (tile == null && obj == null && pick == null) return;
            var ids = new List<int>(4);
            bool tileRuns = tile != null && EventGroups.Parse(tile.groups, ids).Count > 0;

            Transform Holder(List<Transform> pieces, string name)
            {
                var own = pieces.FindAll(p => p != null && p.parent == anchor); // pickups riding a block stay on it
                if (own.Count == 0) return null;
                var h = new GameObject(name).transform;
                h.SetParent(anchor, false);
                foreach (var p in own) p.SetParent(h, false); // the holder sits at the cell: same place
                return h;
            }
            void Run(Transform target, CellGroup cell, TileLayer layer)
            {
                var runner = target.gameObject.AddComponent<GroupEventRunner>();
                runner.Setup(level, cell != null ? EventGroups.Parse(cell.groups) : null, cell != null && cell.sleep, layer);
            }
            void Fires(Transform target, CellGroup cell, TileLayer layer)
            {
                if (cell == null || target == null) return;
                var fires = EventGroups.Parse(cell.fires);
                if (fires.Count == 0) return;
                var f = target.gameObject.AddComponent<GroupFire>();
                f.groups = fires;
                f.wholeRow = cell.wholeRow;
                f.layer = layer;
            }

            if (obj != null || tileRuns)
            {
                var h = Holder(objectPieces, "Object Group");
                if (h != null)
                {
                    Run(h, obj, TileLayer.Object);
                    Fires(h, obj, TileLayer.Object);
                }
            }
            if (pick != null || tileRuns)
            {
                var h = Holder(collectPieces, "Collect Group");
                if (h != null)
                {
                    Run(h, pick, TileLayer.Collect);
                    Fires(h, pick, TileLayer.Collect);
                }
            }
            if (tile == null) return;
            if (tileRuns && anchor.parent == rowRoot) // a slider riding the row's first slider keeps its place
            {
                var holder = new GameObject("Tile Group").transform;
                holder.SetParent(rowRoot, false);
                anchor.SetParent(holder, false); // the holder sits at the row: the tile keeps its place
                Run(holder, tile, TileLayer.Tile);
            }
            Fires(anchor, tile, TileLayer.Tile);
        }

        static void RidePickups(Transform anchor, List<Transform> riders)
        {
            if (riders.Count == 0) return;
            var block = anchor.Find("Custom Move Block");
            var hazard = block != null ? block.GetComponent<Hazard>() : null;
            if (block == null || hazard != null && hazard.lethal) return; // a deadly block: the pickups stay where they are
            foreach (var r in riders)
            {
                if (r == null || r == block) continue;
                r.SetParent(block, true);
                r.localPosition += new Vector3(0f, 0.8f / Mathf.Max(0.01f, block.lossyScale.y), 0f); // on top of the block
            }
        }

        static void AddCustomMotion(GameObject go, PartArgs args)
        {
            var m = go.AddComponent<CustomMotion>();
            m.move = args.Move;
            m.spin = args.spin;
            m.withBall = args.withBall;
            m.seconds = Mathf.Max(0.05f, args.seconds);
            m.loop = args.loop;
            m.startRows = Mathf.Max(0f, args.startRows);
            m.group = args.group;
            m.speed = Mathf.Max(0f, args.speed);
            m.keep = args.keep;
            m.vanish = Mathf.Clamp(args.vanish, 1f, 3f);
        }

        /// <summary>CustomTriggerTiles: a glowing pad on the floor with its Group number.</summary>
        void MakeTriggerPad(Transform anchor, PartArgs args)
        {
            var go = new GameObject("Trigger " + args.group);
            go.transform.SetParent(anchor, false);
            var pad = new GameObject("Pad").transform;
            pad.SetParent(go.transform, false);
            pad.localPosition = new Vector3(0f, 0.03f, 0f);
            AddVisual(pad, null, MeshFactory.BeveledBox(new Vector3(0.62f, 0.06f, 0.62f), 0.02f), PowerMaterial(PartKind.TriggerTile),
                Vector3.zero, Vector3.one, castShadows: false);
            var label = new GameObject("Number");
            label.transform.SetParent(pad, false);
            label.transform.localPosition = new Vector3(0f, 0.035f, 0f);
            label.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var text = label.AddComponent<TextMesh>();
            text.font = UiFactory.DefaultFont;
            text.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
            text.text = args.group.ToString();
            text.fontSize = 64;
            text.characterSize = 0.06f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = new Color(0.05f, 0.15f, 0.08f);
            var trigger = go.AddComponent<TriggerPad>();
            trigger.group = args.group;
            trigger.wholeRow = args.wholeRow;
            trigger.pad = pad;
        }

        /// <summary>
        /// Trigger Object (169): the theme's Trigger Object Model with its one clip (TriggerAnimator times it: idle frames,
        /// then the rest after the trigger) and a hitbox on its animated parts; a model without a clip goes down whole;
        /// no model: two posts and a glowing bar at Height that slams down to the ball's height.
        /// </summary>
        void MakeTriggerObject(Transform anchor, PartArgs args)
        {
            var go = new GameObject("Trigger Object " + args.group);
            go.transform.SetParent(anchor, false);
            go.AddComponent<HideableObject>();
            var rig = go.AddComponent<TriggerAnimator>();
            rig.group = args.group;
            // No trigger tile for its Group: it starts when the ball is Wake Rows away (it used to wait forever).
            rig.startRows = GroupTriggers.HasTrigger(args.group) ? -1f : TrackBehaviour.WakeRows;
            rig.frames = args.frames;
            rig.loop = args.loop;
            rig.seconds = args.seconds;
            float up = Mathf.Max(0.9f, args.height);
            var model = theme.triggerObjectModel;
            if (model != null)
            {
                var holder = AddModel(go.transform, model, new Vector3(0f, 0.6f, 0f), new Vector3(1f, 1.2f, 1f), true);
                var instance = holder.transform.childCount > 0 ? holder.transform.GetChild(0).gameObject : holder;
                // Its clip: the Trigger Object Clip slot, else the one set for the model in Models.
                var animator = instance.GetComponentInChildren<ModelAnimator>();
                if (theme.triggerObjectClip != null)
                {
                    if (animator != null) Destroy(animator);
                    animator = instance.AddComponent<ModelAnimator>();
                    animator.settings = new ModelAnimation { model = model, clip = theme.triggerObjectClip };
                }
                if (animator != null)
                {
                    animator.manual = true;
                    rig.clip = animator;
                    AnimatedHitbox.Add(instance, go, false);
                }
                else
                {
                    // No clip: the whole model comes down from Height.
                    var mover = new GameObject("Mover").transform;
                    mover.SetParent(go.transform, false);
                    holder.transform.SetParent(mover, true);
                    rig.mover = mover;
                    rig.moveFrom = new Vector3(0f, up - 0.3f, 0f);
                    rig.moveTo = Vector3.zero;
                    rig.shake = 0.2f;
                    AnimatedHitbox.Add(holder, go, false);
                }
                return;
            }
            var postMat = theme.floor;
            AddVisual(go.transform, null, MeshFactory.BeveledBox(new Vector3(0.08f, up + 0.2f, 0.12f), 0.02f), postMat,
                new Vector3(-0.46f, (up + 0.2f) * 0.5f, 0f), Vector3.one);
            AddVisual(go.transform, null, MeshFactory.BeveledBox(new Vector3(0.08f, up + 0.2f, 0.12f), 0.02f), postMat,
                new Vector3(0.46f, (up + 0.2f) * 0.5f, 0f), Vector3.one);
            var bar = new GameObject("Bar");
            bar.transform.SetParent(go.transform, false);
            var bc = bar.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(0.84f, 0.18f, 0.2f);
            bar.AddComponent<Hazard>();
            AddVisual(bar.transform, null, MeshFactory.BeveledBox(new Vector3(0.84f, 0.18f, 0.2f), 0.05f), PowerMaterial(PartKind.TriggerObject),
                Vector3.zero, Vector3.one, castShadows: false);
            rig.mover = bar.transform;
            rig.moveFrom = new Vector3(0f, up, 0f);
            rig.moveTo = new Vector3(0f, 0.3f, 0f);
            rig.shake = 0.2f;
            bar.transform.localPosition = rig.moveFrom;
        }

        /// <summary>A power-up cube floating on its tile, glowing in its colour (or the theme's power-up model).</summary>
        void MakePowerUp(Transform anchor, PartArgs args, int row)
        {
            var go = new GameObject("PowerUp " + args.Kind);
            go.transform.SetParent(anchor, false);
            go.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            var bc = go.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(0.8f, 0.9f, 0.8f);
            var power = go.AddComponent<PowerUp>();
            power.args = args;
            power.row = row;

            var visual = new GameObject("Visual").transform;
            visual.SetParent(go.transform, false);
            power.visual = visual;
            var model = theme.PowerUpModel(args.Kind - PartKind.FlyJump);
            if (model != null)
            {
                AddModel(visual, model, Vector3.zero, Vector3.one * 0.6f, true, 0.5f);
                return;
            }
            var mat = PowerMaterial(args.Kind);
            AddVisual(visual, null, MeshFactory.BeveledBox(new Vector3(0.52f, 0.52f, 0.52f), 0.1f), mat, Vector3.zero, Vector3.one, castShadows: false);
            // A white core: an arrow-like gem for the jumps, a flat bar for the rest.
            var core = theme.star != null ? theme.star : theme.gem;
            bool jump = args.Kind == PartKind.FlyJump || args.Kind == PartKind.ShortJump || args.Kind == PartKind.Bounce;
            AddVisual(visual, null, jump ? MeshFactory.Octahedron() : MeshFactory.BeveledBox(new Vector3(0.36f, 0.08f, 0.08f), 0.02f), core,
                Vector3.zero, jump ? new Vector3(0.14f, 0.24f, 0.14f) : Vector3.one, castShadows: false);
        }

        /// <summary>The glowing material of a power-up kind: the theme's jump (or gem) material in the kind's colour.</summary>
        Material PowerMaterial(PartKind kind)
        {
            if (powerMaterials.TryGetValue(kind, out var m) && m != null) return m;
            var source = theme.jump != null ? theme.jump : theme.gem != null ? theme.gem : theme.floor;
            if (source == null) return null;
            var color = PartArgs.ColorOf(kind);
            m = new Material(source) { name = "PowerUp " + kind };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * 2.5f);
                if (m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", null); // the whole cube glows
            }
            powerMaterials[kind] = m;
            return m;
        }

        /// <summary>
        /// Switch power-up: rows <paramref name="fromRow"/> to fromRow + <paramref name="count"/> - 1 turn over
        /// left-right one after another (built rows animate, rows built later come already turned). Switching a
        /// switched row turns it back.
        /// </summary>
        public void SwitchRows(int fromRow, int count)
        {
            var gm = GameManager.Instance;
            float speed = gm != null && gm.ball != null ? Mathf.Max(1f, gm.ball.Speed) : 6f;
            float duration = Mathf.Min(0.3f, 1.2f / speed);
            for (int r = fromRow; r < fromRow + count; r++)
            {
                bool now = !switchedRows.Contains(r);
                if (now) switchedRows.Add(r);
                else switchedRows.Remove(r);
                if (!built.TryGetValue(r, out var go) || go == null) continue;
                var old = go.GetComponent<RowFlip>();
                if (old != null) Destroy(old);
                var flip = go.AddComponent<RowFlip>();
                flip.target = now ? -1f : 1f;
                flip.delay = (r - fromRow) * 0.02f;
                flip.duration = duration;
            }
            GameAudio.Play(Sfx.Woosh, 0.8f);
        }

        int hideFrom, hideTo;
        float hideUntil = -1f;

        /// <summary>
        /// Hide Objects power-up: the obstacles of rows <paramref name="fromRow"/> to fromRow + <paramref name="count"/> - 1
        /// disappear for <paramref name="seconds"/> (built rows now, rows built later as they come). Only hidden: they still kill.
        /// </summary>
        public void HideObjects(int fromRow, int count, float seconds)
        {
            hideFrom = fromRow;
            hideTo = fromRow + count;
            hideUntil = Time.time + seconds;
            foreach (var kv in built)
            {
                if (kv.Key < hideFrom || kv.Key >= hideTo || kv.Value == null) continue;
                foreach (var h in kv.Value.GetComponentsInChildren<HideableObject>()) h.Hide(seconds);
            }
            GameAudio.Play(Sfx.Woosh, 0.6f);
        }

        // ---------- Moving platforms ----------

        /// <summary>Floor tile at a cell, or null when it has no floor or its row is not built.</summary>
        public Transform HolderAt(int row, int lane)
        {
            return holders.TryGetValue(row * LevelData.Width + lane, out var t) && t != null ? t : null;
        }

        /// <summary>
        /// Arrow on a mover tile, pointing where the platform will go: the original 01_moveArrow / 02_moveAutoArrow
        /// (dark until the ball sets it off, then lit) in the Classic Tile Type look, else a flat glowing arrow.
        /// </summary>
        void MakeArrow(Transform holder, Vector2Int dir, MoverKind kind)
        {
            var original = kind == MoverKind.Auto ? theme.tileMoverAutoArrow : theme.tileMoverArrow;
            if (OriginalArrow(holder) && original != null)
            {
                int turns = dir == Vector2Int.up ? 0 : dir == Vector2Int.right ? 1 : dir == Vector2Int.down ? 2 : 3;
                var atlas = kind == MoverKind.Auto ? theme.tileMoverAuto : theme.tileMover;
                // Texture Look RTX: the RTX arrow material (dark / lit states look the same then).
                if (!theme.ClassicTiles && theme.moverArrow != null) atlas = theme.moverArrow;
                TileShapes.Place(holder, original, turns, atlas, "Arrow", true);
                return;
            }
            var go = new GameObject("Arrow");
            go.transform.SetParent(holder, false);
            go.transform.localRotation = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y));
            go.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Arrow();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = theme.moverArrow != null ? theme.moverArrow : theme.jump;
            mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>The original arrow goes on a mover that is an original tile mesh (not an RTX model or slab).</summary>
        bool OriginalArrow(Transform holder)
        {
            if (theme.ClassicModels) return true;
            foreach (Transform c in holder)
                if (c.name.StartsWith("Tile ")) return true;
            return false;
        }

        /// <summary>A fired arrow stops glowing: it takes the platform's own material.</summary>
        public void ActivateArrow(Transform holder)
        {
            var original = holder.Find("Arrow");
            if (original != null && TileShapes.SwitchState(original, true)) return; // original arrow: lights up
            if (theme.MoverPlain) return; // the platform looks like floor: the arrow stays as it is, or it would vanish
            MeshRenderer arrow = null;
            Material slab = null;
            foreach (var r in holder.GetComponentsInChildren<MeshRenderer>())
            {
                if (r.name == "Arrow") arrow = r;
                else if (slab == null) slab = r.sharedMaterial;
            }
            if (arrow != null && slab != null) arrow.sharedMaterial = slab;
        }

        /// <summary>Invisible floor where a platform moves into an empty cell; MoverDirector adds it when the slide starts.</summary>
        public void AddMovedFloor(int row, int lane, int sourceRow)
        {
            Transform parent = transform;
            if (built.TryGetValue(row, out var r) && r != null) parent = r.transform;
            else if (built.TryGetValue(sourceRow, out var s) && s != null) parent = s.transform;

            var go = new GameObject("Moved Floor");
            go.transform.SetParent(parent, false);
            go.transform.position = transform.TransformPoint(new Vector3(lane - 2f, 0f, row));
            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(floorColliderSize, TileDepth, floorColliderSize);
            col.center = new Vector3(0f, -TileDepth * 0.5f, 0f);
        }

        /// <summary>The cell a platform left is a hole now.</summary>
        public void RemoveFloorCollider(int row, int lane)
        {
            var holder = HolderAt(row, lane);
            if (holder == null) return;
            foreach (var c in holder.GetComponents<Collider>()) c.enabled = false;
        }

        /// <summary>Mover colours for themes made before movers existed (Build Gameplay Scene adds real ones).</summary>
        void EnsureMoverMaterials()
        {
            if (theme.mover == null) theme.mover = Tinted(theme.floor, new Color(0.75f, 0.3f, 1f) * 3.2f, false);
            if (theme.moverAuto == null) theme.moverAuto = Tinted(theme.floor, new Color(0.25f, 1f, 0.45f) * 3.2f, false);
            if (theme.moverArrow == null) theme.moverArrow = Tinted(theme.floor, Color.white * 4f, true);
        }

        static Material Tinted(Material source, Color emission, bool solidGlow)
        {
            if (source == null) return null;
            var m = new Material(source) { name = source.name + " (mover)" };
            if (!m.HasProperty("_EmissionColor")) return m;
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            if (solidGlow && m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", null); // glow everywhere, not just the edge lines
            return m;
        }

        // ---------- Props (level.json) ----------

        void BuildProp(PropPlacement p, Transform rowRoot)
        {
            var owner = theme;
            var model = theme.FindProp(p.model);
            // A prop picked in the Level Editor from a later look's Props list.
            for (int i = 0; model == null && i < lookChanges.Count; i++)
                if (lookChanges[i].look != null && (model = lookChanges[i].look.FindProp(p.model)) != null) owner = lookChanges[i].look;
            if (model == null)
            {
                if (missingProps.Add(p.model))
                    Debug.LogWarning($"RTXOn: prop '{p.model}' is not a prop of the theme (Models: tick Prop, or RTX ON > Add Selected Models As Props).");
                return;
            }
            var holder = new GameObject(model.name).transform; // takes the placement, so an animated root stays in place
            holder.SetParent(rowRoot, false);
            var v = Instantiate(model, holder, false);
            StripColliders(v);
            var adjust = owner.FindAdjust(model);
            ApplyModelMaterial(v, adjust, model); // Model Adjust's Material / Keep Own Material count for props too
            holder.localPosition = new Vector3(p.x, p.y, 0f);
            holder.localRotation = Quaternion.Euler(0f, p.rotY + (owner.modelsFaceBall ? 180f : 0f), 0f) *
                                   Quaternion.Euler(adjust != null ? adjust.rotation : Vector3.zero);
            holder.localScale = Vector3.one * p.scale * (adjust != null && adjust.scale > 0f ? adjust.scale : 1f);
            if (adjust != null) holder.localPosition += adjust.offset;
            AddModelAnimation(v, model);
        }

        /// <summary>A Custom Obstacle (Enemy ID 200-999) from the level's "customs", made in the Level Editor.</summary>
        void BuildCustom(int id, Transform anchor, int row, int col)
        {
            var def = CustomObstacles.Get(id) ?? level.FindCustom(id);
            if (def == null)
            {
                if (missingCustoms.Add(id))
                    Debug.LogWarning($"RTXOn: custom obstacle {id} is not in the level's \"customs\" (open the level in the Level Editor and Save it).");
                return;
            }
            // The cell's ARGS (defaults when it has none): Group, Idle frames, Seconds, Loop, Start rows.
            var args = level.ArgsAt(row, col, id);
            var root = CustomObstacleBuilder.Build(anchor, def, theme, CustomThemes(), CustomObstacleBuilder.Mode.Game, false, args);
            // What rides on the obstacle and goes along with its animation: its Dialogue bubble and its power-up.
            var motion = root != null ? root.transform.Find("Motion") : null;
            if (motion == null) return;
            if (!string.IsNullOrEmpty(def.dialogue))
            {
                var talk = motion.gameObject.AddComponent<ObstacleDialogue>();
                talk.text = def.dialogue;
                talk.rows = def.dialogueRows;
                talk.seconds = def.dialogueSeconds;
                talk.height = def.dialogueHeight;
            }
            if (PartArgs.IsPowerUp(PartArgs.KindOf(def.powerUp)))
            {
                int before = motion.childCount;
                MakePowerUp(motion, PartArgs.Defaults(def.powerUp, row, col), row);
                for (int i = before; i < motion.childCount; i++)
                    motion.GetChild(i).localPosition = new Vector3(0f, def.powerUpHeight + 0.5f, 0f);
            }
        }

        /// <summary>Where custom obstacles find project models by name: this theme's Props, then the later looks'.</summary>
        IEnumerable<TrackTheme> CustomThemes()
        {
            yield return theme;
            foreach (var c in lookChanges) if (c.look != null) yield return c.look;
        }

        // ---------- Helpers ----------

        /// <summary>Adds either the theme model (fitted into the shape's box) or a built-in mesh as a child visual.</summary>
        GameObject AddVisual(Transform parent, GameObject model, Mesh mesh, Material mat, Vector3 localPos, Vector3 localScale,
            bool castShadows = true, bool useModelMaterial = true, float alignY = 0f)
        {
            if (model != null)
            {
                var size = mesh != null ? Vector3.Scale(mesh.bounds.size, localScale) : localScale;
                return AddModel(parent, model, localPos, size, useModelMaterial, alignY);
            }

            var v = new GameObject("Visual");
            v.transform.SetParent(parent, false);
            v.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = v.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            v.transform.localScale = localScale;
            v.transform.localPosition = localPos;
            return v;
        }

        /// <summary>
        /// Instantiates a model under <paramref name="parent"/> and fits it into the given local box.
        /// <paramref name="alignY"/>: 0 = sits on the box bottom, 0.5 = centred, 1 = top flush with the box top.
        /// </summary>
        GameObject AddModel(Transform parent, GameObject model, Vector3 boxCenter, Vector3 boxSize, bool useModelMaterial, float alignY = 0f)
        {
            // The model sits inside a holder that takes the fit (place, scale, turn), so an animation that moves the
            // model's own root (made in Blender) plays relative to its spot on the track instead of replacing the fit.
            var holder = new GameObject(model.name).transform;
            holder.SetParent(parent, false);
            var v = Instantiate(model, holder, false);
            StripColliders(v);
            var adjust = theme.FindAdjust(model);
            if (useModelMaterial) ApplyModelMaterial(v, adjust, model);
            holder.localRotation = Quaternion.Euler(0f, theme.modelsFaceBall ? 180f : 0f, 0f) *
                                   Quaternion.Euler(adjust != null ? adjust.rotation : DefaultTurn(model));
            AddModelAnimation(v, model);

            if (!theme.fitModels)
            {
                holder.localPosition = boxCenter;
                ApplyAdjust(holder, adjust, boxCenter);
                return holder.gameObject;
            }

            if (!LocalBounds(holder.gameObject, parent, out var b))
            {
                holder.localPosition = boxCenter;
                ApplyAdjust(holder, adjust, boxCenter);
                return holder.gameObject;
            }

            // Uniform scale so the model fits inside the box on every axis.
            float s = float.MaxValue;
            for (int i = 0; i < 3; i++)
                if (b.size[i] > 0.0001f) s = Mathf.Min(s, boxSize[i] / b.size[i]);
            if (s == float.MaxValue) s = 1f;
            holder.localScale *= s;

            // Scaling happens about the holder (at the parent origin), so the bounds scale with it.
            Vector3 center = b.center * s;
            float bottom = (b.center.y - b.extents.y) * s;
            float height = b.size.y * s;
            float boxBottom = boxCenter.y - boxSize.y * 0.5f;
            holder.localPosition = new Vector3(boxCenter.x - center.x, boxBottom + (boxSize.y - height) * alignY - bottom, boxCenter.z - center.z);
            ApplyAdjust(holder, adjust, new Vector3(boxCenter.x, boxBottom, boxCenter.z));
            return holder.gameObject;
        }

        /// <summary>
        /// Turn for a model without Model Adjust: the original floater mesh (07_enemyFloater) is made lying on its side
        /// (the original game stands it up with X 90), everything else as it is.
        /// </summary>
        static Vector3 DefaultTurn(GameObject model) =>
            model != null && model.name.Contains("enemyFloater") ? new Vector3(90f, 0f, 0f) : Vector3.zero;

        /// <summary>
        /// Laser or spotlight model: an original Rolling Sky mesh (it has an "anim" child) is placed at its real size on
        /// the spot, keeping its own rotation; another model is fitted into <paramref name="fitSize"/>.
        /// Returns the holder, or null without a model.
        /// </summary>
        Transform AddHazardModel(Transform parent, GameObject model, Vector3 fitSize)
        {
            if (model == null) return null;
            bool original = false;
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                if (t.name == "anim") { original = true; break; }
            if (!original)
                return AddModel(parent, model, new Vector3(0f, fitSize.y * 0.5f, 0f), fitSize, true).transform;

            var holder = new GameObject(model.name).transform;
            holder.SetParent(parent, false);
            var v = Instantiate(model, holder, false);
            v.transform.localPosition = Vector3.zero;
            StripColliders(v);
            var adjust = theme.FindAdjust(model);
            ApplyModelMaterial(v, adjust, model);
            AddModelAnimation(v, model);
            if (adjust != null) holder.localRotation = Quaternion.Euler(adjust.rotation);
            ApplyAdjust(holder, adjust, Vector3.zero);
            return holder;
        }

        /// <summary>
        /// Code Motion off: keeps the model where it was built, relative to its tile, while the obstacle's code moves
        /// the hazard (collider) above it. Called once the obstacle is fully built.
        /// </summary>
        /// <summary>Trims the deadly boxes of one-tile hazards to <see cref="hazardWidth"/> lanes (wide ones are left alone).</summary>
        void NarrowHazards(Transform anchor)
        {
            foreach (var hz in anchor.GetComponentsInChildren<Hazard>(true))
            {
                foreach (var box in hz.GetComponents<BoxCollider>())
                {
                    var size = box.size;
                    float width = size.x * Mathf.Abs(box.transform.lossyScale.x);
                    if (width > 1.05f || width <= hazardWidth) continue;
                    size.x *= hazardWidth / width;
                    box.size = size;
                }
            }
        }

        static void PinModels(Transform obstacle, Transform tile)
        {
            foreach (var pin in obstacle.GetComponentsInChildren<PinnedModel>(true))
                pin.Pin(tile);
        }

        /// <summary>Scales the fitted model about <paramref name="anchor"/> (the bottom centre of its space) and moves it.</summary>
        static void ApplyAdjust(Transform holder, ModelAdjust adjust, Vector3 anchor)
        {
            if (adjust == null) return;
            if (!adjust.codeMotion) holder.gameObject.AddComponent<PinnedModel>();
            float k = adjust.scale > 0f ? adjust.scale : 1f;
            holder.localScale *= k;
            holder.localPosition = anchor + (holder.localPosition - anchor) * k + adjust.offset;
        }

        /// <summary>Plays the model's clip from the theme's Models list (Animation > Clip), if it has one.</summary>
        void AddModelAnimation(GameObject instance, GameObject model)
        {
            var settings = theme.FindAnimation(model);
            if (settings == null) return;
            var animator = instance.AddComponent<ModelAnimator>();
            animator.settings = settings;
            // Adrian 2026-10-24: these loop all the time; every other model waits for the ball (TrackBehaviour.WakeRows).
            animator.alwaysLoop = model == theme.rollerModel || model == theme.spotlightModel ||
                                  model == theme.binaryMiniModel || model == theme.binarySlasherModel || model == theme.binaryHammerModel;
        }

        /// <summary>Renderer bounds of <paramref name="go"/> in the local space of <paramref name="space"/>.</summary>
        bool LocalBounds(GameObject go, Transform space, out Bounds result)
        {
            result = default;
            bool any = false;
            go.GetComponentsInChildren(rendererBuffer);
            foreach (var r in rendererBuffer)
            {
                var wb = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = wb.center + Vector3.Scale(wb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var local = space.InverseTransformPoint(corner);
                    if (!any) { result = new Bounds(local, Vector3.zero); any = true; }
                    else result.Encapsulate(local);
                }
            }
            return any;
        }

        static void StripColliders(GameObject v)
        {
            // Disable first: Destroy only happens at the end of the frame and the ball's ground check runs before that.
            foreach (var c in v.GetComponentsInChildren<Collider>())
            {
                c.enabled = false;
                Destroy(c);
            }
        }

        /// <summary>
        /// The model's own Model Adjust material, else the floor material for Finish / Portal / Pillar / Block
        /// (<see cref="TrackTheme.GeneralModelMaterial"/>), else the theme's Model Material, unless Keep Own Material.
        /// </summary>
        void ApplyModelMaterial(GameObject v, ModelAdjust adjust, GameObject model)
        {
            if (adjust != null && adjust.keepOwnMaterial) return;
            bool general = (adjust == null || adjust.material == null) && theme.UsesGeneralMaterial(model);
            var m = adjust != null && adjust.material != null ? adjust.material
                : general ? theme.GeneralModelMaterial : theme.modelMaterial;
            theme.PaintModel(v, m, general);
            if (adjust == null || adjust.material == null) theme.ApplyLandmarkParts(v, model);
        }
    }
}
