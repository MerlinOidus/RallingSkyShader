using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace RTXOn
{
    /// <summary>Switch to an environment preset (by name) when the ball reaches <see cref="row"/>.</summary>
    [Serializable]
    public class ThemeChange
    {
        public int row;
        public string preset = "Day";
        [Tooltip("Rows the blend takes. 0 = instant.")]
        public float rows = 30f;
        [Tooltip("Optional: switch the track look (a TrackTheme in Game > Looks, e.g. \"IndonesiaTheme\") from this row on. Empty = keep.")]
        public string look = "";
        [Tooltip("When the ball reaches this row, the track's colours fade into the new look over this many rows of ball " +
                 "travel: 2 = a quick fade, 0 = an instant hard switch. Models change from this row on.")]
        public int lookRows = 2;
        [Tooltip("Optional: from this row on the objects (enemy models, their material and Models rows) come from this " +
                 "theme, separate from the look (Theme set). Empty = keep, \"Own\" = the look's own objects again.")]
        public string objects = "";

        /// <summary><see cref="objects"/> value for "the look's own objects".</summary>
        public const string OwnObjects = "Own";

        [Tooltip("Turn the sun to Sun angle with this trigger (otherwise the environment's own sun angle).")]
        public bool turnSun;
        [Tooltip("Sun angle in degrees: X = height above the horizon (0..90), Y = compass direction (0 = straight ahead, 180 = behind the camera).")]
        public Vector2 sun = new Vector2(42f, -35f);
        [Tooltip("Turn the moon to Moon angle with this trigger (otherwise the environment's own moon angle).")]
        public bool turnMoon;
        [Tooltip("Moon angle in degrees, like Sun angle. The moon is the light of a night environment (one with stars).")]
        public Vector2 moon = new Vector2(38f, 25f);
    }

    /// <summary>
    /// Camera trigger: from <see cref="row"/> on, the game camera moves to <see cref="position"/> (relative to the ball)
    /// and turns to <see cref="rotation"/>, blending over <see cref="rows"/> rows of ball travel.
    /// </summary>
    [Serializable]
    public class CameraChange
    {
        public int row;
        [Tooltip("Camera position relative to the ball (x follows the ball partly, like the normal camera). Default preset 0, 4.5, -1.46.")]
        public Vector3 position = new Vector3(0f, 4.5f, -1.46f);
        [Tooltip("Camera angles in degrees (X = looking down).")]
        public Vector3 rotation = new Vector3(40f, 0f, 0f);
        [Tooltip("Rows the move takes (Offset). 0 = cut at once.")]
        public float rows = 0f;
        [Tooltip("Curve of the move: 0 = even, above 0 = starts slow, below 0 = starts fast and settles (-0.9 = smooth stop).")]
        [Range(-1f, 1f)] public float curve = -0.9f;

        // Presets from Adrian's fan-editor camera (2026-10-20): Offset 0 (cut at once), Curve -0.9, looking down 40 degrees.
        public static CameraChange Default(int row) => new CameraChange { row = row, position = new Vector3(0f, 4.5f, -1.46f), rotation = new Vector3(40f, 0f, 0f), rows = 0f };
        public static CameraChange High(int row) => new CameraChange { row = row, position = new Vector3(0f, 5.84f, -2.85f), rotation = new Vector3(40f, 0f, 0f), rows = 0f };
        public static CameraChange Far(int row) => new CameraChange { row = row, position = new Vector3(0f, 5.95f, -1.78f), rotation = new Vector3(40f, 0f, 0f), rows = 0f };

        /// <summary>0..1 eased by <see cref="curve"/>.</summary>
        public float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            float k = 1f + 3f * Mathf.Abs(curve);
            if (curve > 0f) return Mathf.Pow(t, k);
            if (curve < 0f) return 1f - Mathf.Pow(1f - t, k);
            return t;
        }
    }

    /// <summary>A decoration model placed next to (or over) the track. Has no collider.</summary>
    [Serializable]
    public class PropPlacement
    {
        public int row;
        [Tooltip("Lanes are x = -2..2. Beside the track is about ±3.5 and beyond.")]
        public float x;
        public float y;
        [Tooltip("Name of a model ticked Prop in the theme's Models list.")]
        public string model;
        public float rotY;
        public float scale = 1f;
    }

    /// <summary>
    /// A level: 5-wide rows of Rolling Sky tile IDs plus optional speed, music, theme changes and props.
    /// Reads either a Tiled "Flare" .txt (the Rolling Sky 1.0 Level.txt, one layer per level)
    /// or a level.json. Row 0 is where the ball starts and rows grow along +Z.
    /// </summary>
    public class LevelData
    {
        public const int Width = 5;

        readonly List<int[]> rows;    // Tiles layer: the floor of each cell
        readonly List<int[]> objects = new List<int[]>();  // Object layer: obstacles (0 = none)
        readonly List<int[]> collects = new List<int[]>(); // Collect layer: gems, stars, crowns, power-ups (0 = none)

        public string Name { get; set; }
        /// <summary>Text of the level list (Main Menu): team and author.</summary>
        public string Team { get; set; } = "";
        public string Author { get; set; } = "";
        /// <summary>Stars on the level list: 1 = Easy ... 5 = Very Hard (default), in half stars. Shown only, it changes nothing in the game.</summary>
        public float Difficulty { get; set; } = DefaultDifficulty;
        public const float DefaultDifficulty = 5f;
        /// <summary>Banner picture of the level list (texture or file name); empty = "World N" by level number.</summary>
        public string Banner { get; set; } = "";
        /// <summary>Gem and crown numbers shown in the level list; 0 = count the level's gems / crown tiles.</summary>
        public int HeaderGems { get; set; }
        public int HeaderCrowns { get; set; }
        public int GemsForHeader => HeaderGems > 0 ? HeaderGems : CountGems();
        public int CrownsForHeader => HeaderCrowns > 0 ? HeaderCrowns : CountCrowns();
        /// <summary>Hazards don't kill the ball (falling off the track still does).</summary>
        public bool Invincible { get; set; }
        /// <summary>
        /// Start Portal: the level starts on a portal lying on the floor and the ball jumps out of it at the first tap.
        /// Only a start from row 0; a Start Pos or a checkpoint starts without it.
        /// </summary>
        public bool StartPortal { get; set; }

        /// <summary>1..5 in tenths of a star (1.3, 4.2).</summary>
        public static float ClampDifficulty(float stars) => Mathf.Clamp(Mathf.Round(stars * 10f) / 10f, 1f, 5f);

        public static string DifficultyName(float stars)
        {
            switch (Mathf.Clamp(Mathf.FloorToInt(stars), 1, 5))
            {
                case 1: return "Easy";
                case 2: return "Normal";
                case 3: return "Hard";
                case 4: return "Harder";
                default: return "Very Hard";
            }
        }
        public string Music { get; set; }
        /// <summary>Name of the TrackTheme this level uses (e.g. "IndonesiaTheme"), or empty for the scene's theme.</summary>
        public string Look { get; set; }
        /// <summary>Rows per second at the start. 0 = use the GameManager's speed.</summary>
        public float SpeedStart { get; set; }
        /// <summary>Speed at the Speed Max Point (rows per second); 0 = no ramp.</summary>
        public float SpeedEnd { get; set; }
        /// <summary>Rows over which the speed ramps from start to end (Speed Max Point). 0 = constant.</summary>
        public int SpeedRampRows { get; set; }
        public List<ThemeChange> ThemeChanges { get; } = new List<ThemeChange>();
        /// <summary>Camera triggers, by row.</summary>
        public List<CameraChange> CameraChanges { get; } = new List<CameraChange>();
        /// <summary>Screen filter triggers (Glitch, Grayscale...), by row.</summary>
        public List<FilterChange> FilterChanges { get; } = new List<FilterChange>();
        /// <summary>Crown rows (checkpoints): after dying past one, the retry starts there.</summary>
        public List<int> Checkpoints { get; } = new List<int>();

        /// <summary>
        /// Dynamic Loading (on): the game builds rows only Load Distance ahead of the ball and removes them behind it,
        /// a few per frame, so long levels stay smooth. Off: the whole level is built at the start.
        /// </summary>
        public bool DynamicLoading { get; set; } = true;
        /// <summary>Rows built ahead of the ball; 0 = the game's default (Track > Rows Ahead).</summary>
        public int LoadDistance { get; set; }
        public const int DefaultLoadDistance = 55;
        public int LoadDistanceOrDefault => LoadDistance > 0 ? LoadDistance : DefaultLoadDistance;

        /// <summary>
        /// Start Pos of the level editor's Test (row and lane; -1 = none). Only the editor's Test uses it: the game
        /// itself always starts at row 0.
        /// </summary>
        public int TestStartRow { get; set; } = -1;
        public int TestStartLane { get; set; } = -1;
        public bool HasTestStart => TestStartRow >= 0 && TestStartLane >= 0;
        public List<PropPlacement> Props { get; } = new List<PropPlacement>();
        /// <summary>Settings (ARGS) of power-ups and animated tiles, per cell. A part without one uses its defaults.</summary>
        public List<PartArgs> Parts { get; } = new List<PartArgs>();
        /// <summary>Event Groups of the cells: which groups a tile / object / collect belongs to and fires (empty = none).</summary>
        public List<CellGroup> CellGroups { get; } = new List<CellGroup>();
        /// <summary>The level's Event Groups: number, name (Run, Prank...) and events.</summary>
        public List<EventGroup> Groups { get; } = new List<EventGroup>();
        /// <summary>Custom Obstacles the Object layer paints (IDs 200-999), kept in the level so it plays without the editor's library.</summary>
        public List<CustomObstacle> Customs { get; } = new List<CustomObstacle>();

        /// <summary>Custom Themes (.themert) the level's look and theme changes use, kept in the level so it plays anywhere.</summary>
        public List<CustomTheme> CustomThemes { get; } = new List<CustomTheme>();
        /// <summary>Level Editor > Theme: Roller, Block, Midground from another theme, Mover Edge, background Offset and Gradient.</summary>
        public LevelThemeSettings ThemeSettings { get; set; } = new LevelThemeSettings();

        /// <summary>
        /// Name of a look with its objects: "Look" alone, or "Look|Objects" when the objects come from another theme
        /// (Theme trigger > Objects). An empty look part = the default theme.
        /// </summary>
        public static string LookKey(string look, string objects) =>
            string.IsNullOrEmpty(objects) ? look ?? "" : (look ?? "") + "|" + objects;

        /// <summary>The look key (<see cref="LookKey"/>) in use at <paramref name="row"/>, from the level's look and its theme triggers.</summary>
        public string LookKeyAt(int row)
        {
            string look = Look ?? "", objects = "";
            foreach (var c in ThemeChanges)
            {
                if (c == null) continue;
                if (c.row > row) break;
                if (!string.IsNullOrEmpty(c.look)) look = c.look;
                if (!string.IsNullOrEmpty(c.objects)) objects = c.objects == ThemeChange.OwnObjects ? "" : c.objects;
            }
            return LookKey(look, objects);
        }

        /// <summary>
        /// The theme triggers as the track sees them: a copy of each trigger that changes the look or the objects, its
        /// look set to the full look key from that row on ("|" = the default theme with its own objects).
        /// </summary>
        public List<ThemeChange> LookChanges()
        {
            var list = new List<ThemeChange>();
            string look = Look ?? "", objects = "";
            foreach (var c in ThemeChanges)
            {
                if (c == null) continue;
                bool l = !string.IsNullOrEmpty(c.look), o = !string.IsNullOrEmpty(c.objects);
                if (!l && !o) continue;
                if (l) look = c.look;
                if (o) objects = c.objects == ThemeChange.OwnObjects ? "" : c.objects;
                var copy = JsonUtility.FromJson<ThemeChange>(JsonUtility.ToJson(c));
                string key = LookKey(look, objects);
                copy.look = key.Length == 0 ? "|" : key;
                list.Add(copy);
            }
            return list;
        }

        public CustomTheme FindCustomTheme(string name)
        {
            foreach (var t in CustomThemes)
                if (t != null && string.Equals(t.name, name, StringComparison.OrdinalIgnoreCase)) return t;
            return null;
        }

        public CustomObstacle FindCustom(int id)
        {
            foreach (var c in Customs) if (c != null && c.id == id) return c;
            return null;
        }

        public int RowCount => rows.Count;

        LevelData(string name, List<int[]> rows)
        {
            Name = name;
            this.rows = rows;
        }

        public int Get(int row, int col)
        {
            if (row < 0 || row >= rows.Count || col < 0 || col >= Width) return 0;
            return rows[row][col];
        }

        public int[] GetRow(int row) => rows[row];

        /// <summary>The obstacle on a cell (an Object-layer ID on plain floor, e.g. 13 riser), 0 = none.</summary>
        public int GetObject(int row, int col) => LayerGet(objects, row, col);

        /// <summary>The pickup on a cell (a Collect-layer ID: 49 gem, 150 crown, 160-166 power-ups...), 0 = none.</summary>
        public int GetCollect(int row, int col) => LayerGet(collects, row, col);

        public int Get(TileLayer layer, int row, int col) =>
            layer == TileLayer.Object ? GetObject(row, col) : layer == TileLayer.Collect ? GetCollect(row, col) : Get(row, col);

        static int LayerGet(List<int[]> layer, int row, int col)
        {
            if (row < 0 || row >= layer.Count || col < 0 || col >= Width) return 0;
            return layer[row][col];
        }

        /// <summary>
        /// An item of the Object or Collect layer as it stands on its cell: the item of <paramref name="itemId"/>
        /// with the floor of the cell's tile (a gem on a jump pad is taken as the ball takes off, and so on).
        /// </summary>
        public TileSpec ItemSpec(int itemId, int row, int col)
        {
            var spec = TileCatalog.Decode(itemId);
            spec.floor = TileCatalog.Decode(Get(row, col)).floor;
            return spec;
        }

        /// <summary>
        /// Moves the item of every combined original ID (14 = riser on fragile, 54 = gem on a jump pad...) to its
        /// layer and leaves the floor tile in the Tiles layer, so every level reads the same, old or new.
        /// </summary>
        void SplitLayers()
        {
            for (int r = 0; r < rows.Count; r++)
                for (int c = 0; c < Width; c++)
                {
                    if (!TileCatalog.Split(rows[r][c], out int tile, out int item, out var layer)) continue;
                    var list = layer == TileLayer.Object ? objects : collects;
                    if (LayerGet(list, r, c) != 0) continue; // the layer already has its own: keep the original ID
                    LayerRow(list, r)[c] = item;
                    rows[r][c] = tile;
                }
        }

        int[] LayerRow(List<int[]> layer, int row)
        {
            while (rows.Count <= row) rows.Add(new int[Width]);
            while (layer.Count <= row) layer.Add(new int[Width]);
            return layer[row];
        }

        // ---------------- Editing (used by the level editor) ----------------

        /// <summary>A new level with <paramref name="startRows"/> rows of "0,1,1,1,0" to start on.</summary>
        public static LevelData CreateEmpty(string name, int startRows)
        {
            var list = new List<int[]>();
            for (int i = 0; i < Mathf.Max(1, startRows); i++) list.Add(new[] { 0, 1, 1, 1, 0 });
            return new LevelData(name, list);
        }

        /// <summary>Sets one tile (Tiles layer). Painting past the last row adds empty rows first.</summary>
        public void Set(int row, int col, int id) => Set(TileLayer.Tile, row, col, id);

        /// <summary>Sets one cell of a layer. Painting past the last row adds empty rows first.</summary>
        public void Set(TileLayer layer, int row, int col, int id)
        {
            if (row < 0 || col < 0 || col >= Width) return;
            var cells = layer == TileLayer.Object ? LayerRow(objects, row) : layer == TileLayer.Collect ? LayerRow(collects, row) : LayerRow(rows, row);
            if (PartArgs.IsSpeed(PartArgs.KindOf(cells[col])) || PartArgs.IsSpeed(PartArgs.KindOf(id))) PartsChanged();
            cells[col] = id;
        }

        // ---------------- Parts (power-ups, animated tiles) ----------------

        /// <summary>
        /// The part on a cell (a Collect part first, then an Object part, else a Tiles part), 0 = none. A cell can hold
        /// one part per layer (a Trigger Object on a Custom Move Tile); <see cref="PartIdsAt"/> gives them all.
        /// </summary>
        public int PartIdAt(int row, int col)
        {
            int c = GetCollect(row, col);
            if (PartArgs.IsPart(c)) return c;
            int o = GetObject(row, col);
            if (PartArgs.IsPart(o)) return o;
            int t = Get(row, col);
            return PartArgs.IsPart(t) ? t : 0;
        }

        /// <summary>Every part on a cell (Tiles, Object, Collect), into <paramref name="ids"/> (cleared first).</summary>
        public void PartIdsAt(int row, int col, List<int> ids)
        {
            ids.Clear();
            int t = Get(row, col), o = GetObject(row, col), c = GetCollect(row, col);
            if (PartArgs.IsPart(t)) ids.Add(t);
            if (PartArgs.IsPart(o)) ids.Add(o);
            if (PartArgs.IsPart(c)) ids.Add(c);
        }

        /// <summary>True when the cell's layer of <paramref name="id"/> holds that ID.</summary>
        public bool HasPart(int row, int col, int id) => PartArgs.IsPart(id) && Get(TileCatalog.LayerOf(id), row, col) == id;

        /// <summary>The saved settings of the part on a cell, or null (also when they were made for another tile ID).</summary>
        public PartArgs PartAt(int row, int col) => PartAt(row, col, PartIdAt(row, col));

        /// <summary>The saved settings of part <paramref name="id"/> on a cell, or null.</summary>
        public PartArgs PartAt(int row, int col, int id)
        {
            if (id == 0) return null;
            foreach (var p in Parts)
                if (p != null && p.row == row && p.lane == col && p.id == id) return p;
            return null;
        }

        /// <summary>The settings the part on a cell plays with: its saved ones, else the defaults of its tile.</summary>
        public PartArgs ArgsAt(int row, int col) => ArgsAt(row, col, PartIdAt(row, col));

        public PartArgs ArgsAt(int row, int col, int id) => PartAt(row, col, id) ?? PartArgs.Defaults(id, row, col);

        /// <summary>The level editor's ARGS window: the cell's settings, added to <see cref="Parts"/> the first time.</summary>
        public PartArgs EditArgs(int row, int col) => EditArgs(row, col, PartIdAt(row, col));

        /// <summary>The settings of part <paramref name="id"/> on a cell to edit, added to <see cref="Parts"/> the first time
        /// (old settings of another part on the same layer go).</summary>
        public PartArgs EditArgs(int row, int col, int id)
        {
            var p = PartAt(row, col, id);
            if (p != null) return p;
            var layer = TileCatalog.LayerOf(id);
            Parts.RemoveAll(x => x == null || (x.row == row && x.lane == col && TileCatalog.LayerOf(x.id) == layer));
            p = PartArgs.Defaults(id, row, col);
            Parts.Add(p);
            return p;
        }

        /// <summary>Drops settings whose cell no longer holds that part (painted over or erased).</summary>
        public void PruneParts()
        {
            Parts.RemoveAll(p => p == null || !PartArgs.IsPart(p.id) || !HasPart(p.row, p.lane, p.id));
            Parts.Sort((a, b) => a.row != b.row ? a.row.CompareTo(b.row) : a.lane.CompareTo(b.lane));
        }

        // ---------------- Event Groups ----------------

        /// <summary>The groups of one layer of a cell, or null.</summary>
        public CellGroup CellGroupAt(int row, int lane, TileLayer layer)
        {
            foreach (var g in CellGroups)
                if (g != null && g.row == row && g.lane == lane && g.layer == (int)layer) return g;
            return null;
        }

        /// <summary>The groups of one layer of a cell to edit, added the first time.</summary>
        public CellGroup EditCellGroup(int row, int lane, TileLayer layer)
        {
            var g = CellGroupAt(row, lane, layer);
            if (g != null) return g;
            g = new CellGroup { row = row, lane = lane, layer = (int)layer };
            CellGroups.Add(g);
            return g;
        }

        /// <summary>Drops cell groups that are empty or whose layer of the cell is empty.</summary>
        public void PruneCellGroups()
        {
            CellGroups.RemoveAll(g => g == null || g.IsEmpty || g.row < 0 || g.row >= rows.Count || g.lane < 0 || g.lane >= Width ||
                                      Get(g.Layer, g.row, g.lane) == 0);
            CellGroups.Sort((a, b) => a.row != b.row ? a.row.CompareTo(b.row) : a.lane != b.lane ? a.lane.CompareTo(b.lane) : a.layer.CompareTo(b.layer));
        }

        public EventGroup FindGroup(int id)
        {
            foreach (var g in Groups) if (g != null && g.id == id) return g;
            return null;
        }

        /// <summary>"5 Run" for a group with a name, else "5".</summary>
        public string GroupLabel(int id)
        {
            var g = FindGroup(id);
            return g != null && !string.IsNullOrEmpty(g.name) ? $"{id} {g.name}" : id.ToString();
        }

        /// <summary>A speed part or its settings changed: the music timing is worked out again.</summary>
        public void PartsChanged() => speedZones = null;

        /// <summary>A Speed Up / Speed Down stretch: rows [from, to) at <see cref="multiple"/> times the speed.</summary>
        public struct SpeedZone
        {
            public int row, lane;
            public float from, to, multiple;
            public bool musicOffset;
        }

        List<SpeedZone> speedZones;

        /// <summary>Every Speed Up / Speed Down tile with its stretch, by row.</summary>
        public List<SpeedZone> SpeedZones
        {
            get
            {
                if (speedZones != null) return speedZones;
                speedZones = new List<SpeedZone>();
                for (int r = 0; r < rows.Count; r++)
                    for (int c = 0; c < Width; c++)
                    {
                        if (!PartArgs.IsSpeed(PartArgs.KindOf(PartIdAt(r, c)))) continue;
                        var a = ArgsAt(r, c);
                        speedZones.Add(new SpeedZone { row = r, lane = c, from = r, to = r + a.distance, multiple = a.multiple, musicOffset = a.musicOffset });
                    }
                return speedZones;
            }
        }

        /// <summary>
        /// The speed change at <paramref name="row"/> when the ball takes every Speed Up / Speed Down on its way: the
        /// last one at or before the row, if its stretch reaches the row (a newer one replaces the one before, as in
        /// the game). True only when that one has Incorporate music offset on, so the music timing counts it.
        /// </summary>
        public bool MusicZoneAt(float row, out SpeedZone zone)
        {
            zone = default;
            bool any = false;
            foreach (var z in SpeedZones)
            {
                if (z.from > row) break; // sorted by row
                zone = z;
                any = true;
            }
            return any && row < zone.to && zone.musicOffset;
        }

        /// <summary>Rows per second the music timing uses: <see cref="SpeedAt"/> times the speed change counted there.</summary>
        public float MusicSpeedAt(float row, float fallback)
        {
            float s = SpeedAt(row, fallback);
            return MusicZoneAt(row, out var z) ? Mathf.Min(MaxSpeed * 2f, s * z.multiple) : s;
        }

        /// <summary>Removes empty rows at the end of the level (keeps at least one row).</summary>
        public void TrimEmptyEnd()
        {
            while (rows.Count > 1 && RowEmpty(rows.Count - 1))
            {
                int last = rows.Count - 1;
                rows.RemoveAt(last);
                if (objects.Count > last) objects.RemoveRange(last, objects.Count - last);
                if (collects.Count > last) collects.RemoveRange(last, collects.Count - last);
            }
        }

        // ---------------- Insert / delete rows (level editor Row + / Row -) ----------------

        /// <summary>
        /// Row +: a new empty row at <paramref name="row"/>. That row and every row after it move one row on, with what
        /// belongs to them: objects, pickups, ARGS, props, checkpoints, theme / camera triggers and the Start Pos.
        /// </summary>
        public void InsertRow(int row)
        {
            if (row < 0) return;
            row = Mathf.Min(row, rows.Count);
            rows.Insert(row, new int[Width]);
            if (objects.Count > row) objects.Insert(row, new int[Width]);
            if (collects.Count > row) collects.Insert(row, new int[Width]);
            ShiftRows(row, 1);
        }

        /// <summary>Row -: deletes <paramref name="row"/> and what belongs to it; the rows after it move one row back.</summary>
        public void DeleteRow(int row)
        {
            if (row < 0 || row >= rows.Count || rows.Count <= 1) return;
            rows.RemoveAt(row);
            if (objects.Count > row) objects.RemoveAt(row);
            if (collects.Count > row) collects.RemoveAt(row);
            Parts.RemoveAll(p => p == null || p.row == row);
            CellGroups.RemoveAll(g => g == null || g.row == row);
            Props.RemoveAll(p => p == null || p.row == row);
            // A trigger on row 0 is the level's start setting: it stays.
            ThemeChanges.RemoveAll(t => t == null || (t.row == row && row > 0));
            CameraChanges.RemoveAll(c => c == null || (c.row == row && row > 0));
            FilterChanges.RemoveAll(f => f == null || (f.row == row && row > 0));
            Checkpoints.Remove(row);
            if (TestStartRow == row) TestStartRow = TestStartLane = -1;
            ShiftRows(row + 1, -1);
        }

        /// <summary>
        /// Keeps only the first <paramref name="count"/> rows (a LevelPreview keeps 10): the rows after them and what
        /// belongs to them (ARGS, Event Groups, props, triggers, checkpoints, Start Pos) are dropped.
        /// </summary>
        public void KeepRows(int count)
        {
            count = Mathf.Max(1, count);
            if (rows.Count <= count) return;
            rows.RemoveRange(count, rows.Count - count);
            if (objects.Count > count) objects.RemoveRange(count, objects.Count - count);
            if (collects.Count > count) collects.RemoveRange(count, collects.Count - count);
            Parts.RemoveAll(p => p == null || p.row >= count);
            CellGroups.RemoveAll(g => g == null || g.row >= count);
            Props.RemoveAll(p => p == null || p.row >= count);
            ThemeChanges.RemoveAll(t => t == null || t.row >= count);
            CameraChanges.RemoveAll(c => c == null || c.row >= count);
            FilterChanges.RemoveAll(f => f == null || f.row >= count);
            Checkpoints.RemoveAll(r => r >= count);
            if (TestStartRow >= count) TestStartRow = TestStartLane = -1;
            PartsChanged();
        }

        /// <summary>Moves everything kept by row number from row <paramref name="from"/> on by <paramref name="by"/> rows.</summary>
        void ShiftRows(int from, int by)
        {
            int Shift(int r) => r >= from ? r + by : r;
            int ShiftTrigger(int r) => r > 0 ? Shift(r) : r; // row 0 = start setting
            foreach (var p in Parts) if (p != null) p.row = Shift(p.row);
            foreach (var g in CellGroups) if (g != null) g.row = Shift(g.row);
            foreach (var p in Props) if (p != null) p.row = Shift(p.row);
            foreach (var t in ThemeChanges) if (t != null) t.row = ShiftTrigger(t.row);
            foreach (var c in CameraChanges) if (c != null) c.row = ShiftTrigger(c.row);
            foreach (var f in FilterChanges) if (f != null) f.row = ShiftTrigger(f.row);
            for (int i = 0; i < Checkpoints.Count; i++) Checkpoints[i] = Shift(Checkpoints[i]);
            Checkpoints.RemoveAll(r => r <= 0);
            if (HasTestStart) TestStartRow = Shift(TestStartRow);
            PartsChanged();
        }

        // ---------------- Marquee (level editor: copy / delete / paste rows) ----------------

        /// <summary>Copied rows: their tiles, objects, pickups, ARGS, Event Groups and props (rows counted from 0).</summary>
        public sealed class RowsClip
        {
            internal readonly List<int[]> tiles = new List<int[]>(), objects = new List<int[]>(), collects = new List<int[]>();
            internal readonly List<PartArgs> parts = new List<PartArgs>();
            internal readonly List<CellGroup> groups = new List<CellGroup>();
            internal readonly List<PropPlacement> props = new List<PropPlacement>();
            public int Count => tiles.Count;
        }

        /// <summary>Copies rows <paramref name="from"/> .. <paramref name="from"/> + <paramref name="count"/> - 1.</summary>
        public RowsClip CopyRows(int from, int count)
        {
            var clip = new RowsClip();
            from = Mathf.Clamp(from, 0, Mathf.Max(0, rows.Count - 1));
            count = Mathf.Clamp(count, 1, rows.Count - from);
            for (int i = 0; i < count; i++)
            {
                int r = from + i;
                clip.tiles.Add((int[])rows[r].Clone());
                clip.objects.Add(r < objects.Count ? (int[])objects[r].Clone() : new int[Width]);
                clip.collects.Add(r < collects.Count ? (int[])collects[r].Clone() : new int[Width]);
            }
            foreach (var p in Parts)
                if (p != null && p.row >= from && p.row < from + count && HasPart(p.row, p.lane, p.id))
                {
                    var c = p.Clone();
                    c.row -= from;
                    clip.parts.Add(c);
                }
            foreach (var g in CellGroups)
                if (g != null && g.row >= from && g.row < from + count)
                {
                    var c = g.Clone();
                    c.row -= from;
                    clip.groups.Add(c);
                }
            foreach (var p in Props)
                if (p != null && p.row >= from && p.row < from + count)
                {
                    var c = JsonUtility.FromJson<PropPlacement>(JsonUtility.ToJson(p));
                    c.row -= from;
                    clip.props.Add(c);
                }
            return clip;
        }

        /// <summary>Deletes <paramref name="count"/> rows from <paramref name="from"/> (at least one row stays).</summary>
        public void DeleteRows(int from, int count)
        {
            for (int i = 0; i < count && from < rows.Count && rows.Count > 1; i++) DeleteRow(from);
        }

        /// <summary>
        /// Puts copied rows in at <paramref name="at"/>. Insert: the rows from there on move on to make room. Otherwise
        /// they replace what is there (the level grows when they go past its end).
        /// </summary>
        public void PasteRows(RowsClip clip, int at, bool insert)
        {
            if (clip == null || clip.Count == 0 || at < 0) return;
            int count = clip.Count;
            if (insert)
            {
                at = Mathf.Min(at, rows.Count);
                for (int i = 0; i < count; i++) InsertRow(at);
            }
            else
            {
                Parts.RemoveAll(p => p == null || p.row >= at && p.row < at + count);
                CellGroups.RemoveAll(g => g == null || g.row >= at && g.row < at + count);
                Props.RemoveAll(p => p == null || p.row >= at && p.row < at + count);
            }
            for (int i = 0; i < count; i++)
                for (int c = 0; c < Width; c++)
                {
                    Set(TileLayer.Tile, at + i, c, clip.tiles[i][c]);
                    Set(TileLayer.Object, at + i, c, clip.objects[i][c]);
                    Set(TileLayer.Collect, at + i, c, clip.collects[i][c]);
                }
            foreach (var p in clip.parts) { var c = p.Clone(); c.row += at; Parts.Add(c); }
            foreach (var g in clip.groups) { var c = g.Clone(); c.row += at; CellGroups.Add(c); }
            foreach (var p in clip.props) { var c = JsonUtility.FromJson<PropPlacement>(JsonUtility.ToJson(p)); c.row += at; Props.Add(c); }
            PartsChanged();
        }

        /// <summary>Everything <see cref="InsertRow"/> and <see cref="DeleteRow"/> change, for the editor's Undo.</summary>
        public sealed class RowsSnapshot
        {
            internal List<int[]> rows, objects, collects;
            internal List<string> parts, props, themes, cameras, filters, cellGroups;
            internal List<int> checkpoints;
            internal int startRow, startLane;
        }

        public RowsSnapshot SnapshotRows()
        {
            List<int[]> Copy(List<int[]> l) => l.ConvertAll(r => (int[])r.Clone());
            List<string> Json<T>(List<T> l) => l.ConvertAll(x => JsonUtility.ToJson(x));
            return new RowsSnapshot
            {
                rows = Copy(this.rows), objects = Copy(this.objects), collects = Copy(this.collects),
                parts = Json(Parts), cellGroups = Json(CellGroups), props = Json(Props), themes = Json(ThemeChanges), cameras = Json(CameraChanges), filters = Json(FilterChanges),
                checkpoints = new List<int>(Checkpoints), startRow = TestStartRow, startLane = TestStartLane,
            };
        }

        public void RestoreRows(RowsSnapshot s)
        {
            if (s == null) return;
            void Put(List<int[]> to, List<int[]> from) { to.Clear(); foreach (var r in from) to.Add((int[])r.Clone()); }
            void PutJson<T>(List<T> to, List<string> from) { to.Clear(); foreach (var j in from) to.Add(JsonUtility.FromJson<T>(j)); }
            Put(rows, s.rows);
            Put(objects, s.objects);
            Put(collects, s.collects);
            PutJson(Parts, s.parts);
            PutJson(CellGroups, s.cellGroups ?? new List<string>());
            PutJson(Props, s.props);
            PutJson(ThemeChanges, s.themes);
            PutJson(CameraChanges, s.cameras);
            PutJson(FilterChanges, s.filters);
            Checkpoints.Clear();
            Checkpoints.AddRange(s.checkpoints);
            TestStartRow = s.startRow;
            TestStartLane = s.startLane;
            PartsChanged();
        }

        /// <summary>Takes name, music, speed, themes and props from another copy of the level; the rows stay.</summary>
        public void CopySettingsFrom(LevelData other)
        {
            Name = other.Name;
            Team = other.Team;
            Author = other.Author;
            Difficulty = other.Difficulty;
            Banner = other.Banner;
            HeaderGems = other.HeaderGems;
            HeaderCrowns = other.HeaderCrowns;
            Invincible = other.Invincible;
            StartPortal = other.StartPortal;
            ThemeSettings = (other.ThemeSettings ?? new LevelThemeSettings()).Clone();
            CustomThemes.Clear();
            foreach (var t in other.CustomThemes) if (t != null) CustomThemes.Add(t.Clone());
            Music = other.Music;
            Look = other.Look;
            SpeedStart = other.SpeedStart;
            SpeedEnd = other.SpeedEnd;
            SpeedRampRows = other.SpeedRampRows;
            ThemeChanges.Clear();
            ThemeChanges.AddRange(other.ThemeChanges);
            CameraChanges.Clear();
            CameraChanges.AddRange(other.CameraChanges);
            FilterChanges.Clear();
            foreach (var f in other.FilterChanges) if (f != null) FilterChanges.Add(f.Clone());
            Props.Clear();
            Props.AddRange(other.Props);
            Parts.Clear();
            foreach (var p in other.Parts) if (p != null) Parts.Add(p.Clone());
            CellGroups.Clear();
            foreach (var g in other.CellGroups) if (g != null) CellGroups.Add(g.Clone());
            Groups.Clear();
            foreach (var g in other.Groups) if (g != null) Groups.Add(g.Clone());
            PartsChanged();
            Checkpoints.Clear();
            Checkpoints.AddRange(other.Checkpoints);
            DynamicLoading = other.DynamicLoading;
            LoadDistance = other.LoadDistance;
            TestStartRow = other.TestStartRow;
            TestStartLane = other.TestStartLane;
        }

        /// <summary>False when every tile is 0.</summary>
        public bool HasTiles
        {
            get
            {
                for (int r = 0; r < rows.Count; r++) if (!RowEmpty(r)) return true;
                return false;
            }
        }

        public const float BpmPerSpeed = 15f; // old files: 90 BPM = 6 rows per second
        public const float MaxSpeed = 20f, MinSpeed = 1f;
        public const float DefaultSpeed = 7f; // new levels

        /// <summary>Old files only (the editor has no BPM any more): rows per second for a BPM (90 = 6), capped at <see cref="MaxSpeed"/>.</summary>
        public static float SpeedForBpm(float bpm) => Mathf.Min(MaxSpeed, bpm / BpmPerSpeed);

        /// <summary>
        /// Rows per second at <paramref name="row"/>: the start speed ("speed", else <paramref name="fallback"/>),
        /// ramping to End Speed at the Speed Max Point (no Max Point = the end of the level).
        /// </summary>
        public float SpeedAt(float row, float fallback)
        {
            float start = SpeedStart > 0f ? SpeedStart : fallback;
            int ramp = SpeedRampRows > 0 ? SpeedRampRows : rows.Count - 1;
            if (SpeedEnd <= 0f || ramp <= 0) return Mathf.Min(MaxSpeed, start);
            return Mathf.Min(MaxSpeed, Mathf.Lerp(start, SpeedEnd, Mathf.Clamp01(row / ramp)));
        }

        /// <summary>Gem and star pickups in the level (Rolling Sky levels aim for 10).</summary>
        public int CountGems()
        {
            int n = 0;
            for (int r = 0; r < rows.Count; r++)
                for (int c = 0; c < Width; c++)
                {
                    int id = GetCollect(r, c);
                    if (CustomObstacle.IsCustomId(id)) { if (CustomCollectOf(id) == CustomCollect.Gem) n++; continue; }
                    var item = TileCatalog.Decode(id).item;
                    if (item == ItemKind.Gem || item == ItemKind.FloatingGem || item == ItemKind.Star || item == ItemKind.FloatingStar) n++;
                }
            return n;
        }

        /// <summary>
        /// Lane (x) with the longest run of plain floor without hazards from <paramref name="fromRow"/>
        /// (centre lane wins ties): where the ball starts, continues from a crown, and where a crown floats.
        /// </summary>
        public float SafeLaneX(int fromRow)
        {
            int best = 2, bestRun = -1;
            foreach (int col in new[] { 2, 1, 3, 0, 4 })
            {
                int run = 0;
                while (fromRow + run < rows.Count && SafeCell(fromRow + run, col)) run++;
                if (run > bestRun) { bestRun = run; best = col; }
            }
            return best - 2f;
        }

        bool SafeCell(int row, int col)
        {
            var tile = TileCatalog.Decode(Get(row, col));
            var pickup = TileCatalog.Decode(GetCollect(row, col)).item;
            return tile.floor == FloorKind.Normal && tile.item == ItemKind.None && GetObject(row, col) == 0 &&
                   (pickup == ItemKind.None || pickup == ItemKind.Gem || pickup == ItemKind.Star);
        }

        /// <summary>Crown tiles (150, 172, 173) in the level (Rolling Sky levels have 3).</summary>
        public int CountCrowns()
        {
            int n = 0;
            for (int r = 0; r < rows.Count; r++)
                for (int c = 0; c < Width; c++)
                {
                    int id = GetCollect(r, c);
                    if (CustomObstacle.IsCustomId(id) ? CustomCollectOf(id) == CustomCollect.Crown
                                                      : TileCatalog.Decode(id).item == ItemKind.Crown) n++; // 150, Crown Rise, Crown Drop
                }
            return n;
        }

        /// <summary>What a custom pickup on the Collect layer counts as (Nothing when it is not a pickup or not known).</summary>
        public CustomCollect CustomCollectOf(int id)
        {
            var def = CustomObstacles.Get(id) ?? FindCustom(id);
            return def != null && def.layer == CustomLayer.Collect ? def.collectAs : CustomCollect.Nothing;
        }

        /// <summary>
        /// Seconds from the start until the ball reaches <paramref name="row"/> (follows BPM, speed ramps and the
        /// Speed Up / Speed Down tiles that count for the music).
        /// </summary>
        public float TimeAtRow(float row, float fallbackSpeed)
        {
            float t = 0f;
            int whole = Mathf.FloorToInt(row);
            for (int r = 0; r < whole; r++) t += 1f / Mathf.Max(0.01f, MusicSpeedAt(r + 0.5f, fallbackSpeed));
            return t + (row - whole) / Mathf.Max(0.01f, MusicSpeedAt(whole + 0.5f, fallbackSpeed));
        }

        /// <summary>Row the ball is on after <paramref name="seconds"/> (inverse of <see cref="TimeAtRow"/>).</summary>
        public float RowAtTime(float seconds, float fallbackSpeed)
        {
            float t = 0f;
            for (int r = 0; r < 100000; r++)
            {
                float step = 1f / Mathf.Max(0.01f, MusicSpeedAt(r + 0.5f, fallbackSpeed));
                if (t + step > seconds) return r + (seconds - t) / step;
                t += step;
            }
            return 100000f;
        }

        /// <summary>Loads a level.json or a Flare .txt (layer chosen by <paramref name="layerName"/>).</summary>
        public static LevelData Load(string text, string layerName)
        {
            if (string.IsNullOrEmpty(text))
                throw new ArgumentException("RTXOn: level text is empty.");
            return text.TrimStart().StartsWith("{") ? ParseJson(text) : Parse(text, layerName);
        }

        // ---------------- level.json ----------------

        [Serializable]
        class LevelJson
        {
            public string name;
            public string team;
            public string author;
            [Tooltip("Stars on the level list: 1 = Easy ... 5 = Very Hard, halves allowed (3.5). 0 = 5.")]
            public float difficulty;
            [Tooltip("Banner picture name (a texture in Main Menu > Banners, or a .png/.jpg in a Banners folder). Empty = World N.")]
            public string banner;
            [Tooltip("Gem number in the level list. 0 = count the gems.")]
            public int gems;
            [Tooltip("Crown number in the level list. 0 = count the crown tiles.")]
            public int crowns;
            [Tooltip("Hazards don't kill the ball.")]
            public bool invincible;
            public bool startPortal;
            public string music;
            [Tooltip("TrackTheme name: which models, colours and environments this level uses.")]
            public string look;
            public float speed;
            [Tooltip("Old files only: beats per minute, read as speed = BPM / 15 (90 = 6). New files only write \"speed\".")]
            public float bpm;
            public float speedEnd;
            public int speedRampRows;
            [Tooltip("Tiles layer (the floor). First entry = start of the level. Each entry is 5 tile IDs, e.g. \"0,1,1,1,0\".")]
            public string[] rows;
            [Tooltip("Object layer, row by row like rows: obstacles on the floor (13 riser, 85 pillar...), 0 = none.")]
            public string[] objects;
            [Tooltip("Collect layer, row by row like rows: gems, stars, crowns, power-ups (49, 150, 160...), 0 = none.")]
            public string[] collect;
            public ThemeChange[] themes;
            [Tooltip("Camera triggers: { \"row\", \"position\": {x,y,z}, \"rotation\": {x,y,z}, \"rows\", \"curve\" }.")]
            public CameraChange[] cameras;
            [Tooltip("Screen filter triggers: { \"row\", \"effect\" (Glitch, Grayscale, Negative, Chromatic, ScanLines, HueShift), \"strength\", \"fade\", \"rows\", \"speed\" }.")]
            public FilterChange[] filters;
            public PropPlacement[] props;
            [Tooltip("Crown (checkpoint) rows, e.g. [120, 260, 400].")]
            public int[] checkpoints;
            [Tooltip("Level editor Test only: [row, lane] where Test starts. The game ignores it.")]
            public int[] testStart;
            [Tooltip("Rows built ahead of the ball (Dynamic Load Distance). 0 = default.")]
            public int loadDistance;
            [Tooltip("True = Dynamic Loading off: the whole level is built at the start.")]
            public bool loadAll;
            [Tooltip("Settings (ARGS) of power-ups, animated tiles, custom moves, trigger tiles and gates: { \"row\", \"lane\", \"id\", \"distance\", \"group\", ... }.")]
            public PartArgs[] parts;
            [Tooltip("Event Groups of cells: { \"row\", \"lane\", \"layer\" (0 Tile, 1 Object, 2 Collect), \"groups\": \"5,6\", \"fires\": \"7\", \"sleep\", \"wholeRow\" }.")]
            public CellGroup[] cellGroups;
            [Tooltip("Event Groups: { \"id\", \"name\", \"events\": [{ \"type\" (Move, MoveFrom, Rotate, Scale, Hide, Show, Shake), \"delay\", \"seconds\", \"x\", \"y\", \"z\", \"loop\", \"curve\" }] }.")]
            public EventGroup[] groups;
            [Tooltip("Custom Obstacles (the Level Editor's + button), the same as their .rsrt files.")]
            public CustomObstacle[] customs;
            [Tooltip("Custom Themes the level uses, the same as their .themert files.")]
            public CustomTheme[] customThemes;
            [Tooltip("Level Editor > Theme: { \"roller\", \"block\", \"midground\", \"moverEdge\", \"backgroundOffset\", \"gradient\" }.")]
            public LevelThemeSettings themeSettings;
        }

        public static LevelData ParseJson(string json)
        {
            var j = JsonUtility.FromJson<LevelJson>(json);
            if (j == null || j.rows == null || j.rows.Length == 0)
                throw new ArgumentException("RTXOn: level.json has no \"rows\".");

            var list = new List<int[]>(j.rows.Length);
            for (int i = 0; i < j.rows.Length; i++)
                list.Add(ParseRow(j.rows[i], $"row {i}"));

            var data = new LevelData(string.IsNullOrEmpty(j.name) ? "Level" : j.name, list);
            data.ReadLayer(data.objects, j.objects, "objects");
            data.ReadLayer(data.collects, j.collect, "collect");
            data.SplitLayers();
            data.SetFromJson(j);
            return data;
        }

        void ReadLayer(List<int[]> layer, string[] lines, string what)
        {
            if (lines == null) return;
            for (int i = 0; i < lines.Length; i++)
            {
                var cells = ParseRow(lines[i], $"{what} {i}");
                if (!IsEmpty(cells)) Array.Copy(cells, LayerRow(layer, i), Width);
            }
        }

        void SetFromJson(LevelJson j)
        {
            var data = this;
            Music = j.music;
            Look = j.look;
            SpeedStart = j.bpm > 0f ? SpeedForBpm(j.bpm) : j.speed; // an old file's BPM still wins, as it did
            SpeedEnd = j.speedEnd;
            SpeedRampRows = j.speedRampRows;
            Team = j.team ?? "";
            Author = j.author ?? "";
            Difficulty = j.difficulty > 0f ? ClampDifficulty(j.difficulty) : DefaultDifficulty;
            Banner = j.banner ?? "";
            HeaderGems = Mathf.Max(0, j.gems);
            HeaderCrowns = Mathf.Max(0, j.crowns);
            Invincible = j.invincible;
            StartPortal = j.startPortal;
            ThemeSettings = j.themeSettings ?? new LevelThemeSettings();
            ThemeSettings.Normalize();
            if (j.customThemes != null)
                foreach (var t in j.customThemes)
                    if (t != null && t.format == CustomTheme.FormatName && FindCustomTheme(t.name) == null)
                    {
                        t.Normalize();
                        CustomThemes.Add(t);
                    }
            if (j.themes != null)
                foreach (var t in j.themes)
                    if (t != null && (!string.IsNullOrEmpty(t.preset) || !string.IsNullOrEmpty(t.look) || !string.IsNullOrEmpty(t.objects) || t.turnSun || t.turnMoon)) data.ThemeChanges.Add(t);
            data.ThemeChanges.Sort((a, b) => a.row.CompareTo(b.row));
            if (j.cameras != null)
                foreach (var c in j.cameras)
                    if (c != null) data.CameraChanges.Add(c);
            data.CameraChanges.Sort((a, b) => a.row.CompareTo(b.row));
            if (j.filters != null)
                foreach (var f in j.filters)
                    if (f != null)
                    {
                        f.Effect = f.Effect; // unknown names become Glitch, "Scan Lines" becomes ScanLines
                        data.FilterChanges.Add(f);
                    }
            data.FilterChanges.Sort((a, b) => a.row.CompareTo(b.row));
            if (j.checkpoints != null) data.Checkpoints.AddRange(j.checkpoints);
            data.Checkpoints.Sort();
            data.LoadDistance = Mathf.Max(0, j.loadDistance);
            data.DynamicLoading = !j.loadAll;
            if (j.testStart != null && j.testStart.Length >= 2 && j.testStart[0] >= 0 && j.testStart[1] >= 0 && j.testStart[1] < Width)
            {
                data.TestStartRow = j.testStart[0];
                data.TestStartLane = j.testStart[1];
            }
            if (j.parts != null)
                foreach (var p in j.parts)
                    if (p != null && PartArgs.IsPart(p.id))
                    {
                        p.Normalize();
                        data.Parts.Add(p);
                    }
            if (j.cellGroups != null)
                foreach (var g in j.cellGroups)
                    if (g != null)
                    {
                        g.Normalize();
                        if (!g.IsEmpty) data.CellGroups.Add(g);
                    }
            if (j.groups != null)
                foreach (var g in j.groups)
                    if (g != null && g.id > 0 && data.FindGroup(g.id) == null)
                    {
                        g.Normalize();
                        data.Groups.Add(g);
                    }
            data.Groups.Sort((a, b) => a.id.CompareTo(b.id));
            if (j.customs != null)
                foreach (var c in j.customs)
                    if (c != null && CustomObstacle.IsCustomId(c.id) && data.FindCustom(c.id) == null)
                    {
                        c.Normalize();
                        data.Customs.Add(c);
                    }
            if (j.props != null)
                foreach (var p in j.props)
                    if (p != null && !string.IsNullOrEmpty(p.model))
                    {
                        if (p.scale <= 0f) p.scale = 1f;
                        data.Props.Add(p);
                    }
        }

        /// <summary>Writes this level as level.json text (rows start-first).</summary>
        public string ToJson(string name, string music, float speed, float speedEnd, int speedRampRows,
            IList<ThemeChange> themes, IList<PropPlacement> props = null, bool withTestStart = true)
        {
            // JsonUtility would put every row on its own multi-line block; write it by hand so rows stay one per line.
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append($"  \"name\": \"{Escape(string.IsNullOrEmpty(name) ? Name : name)}\",\n");
            if (!string.IsNullOrEmpty(Team)) sb.Append($"  \"team\": \"{Escape(Team)}\",\n");
            if (!string.IsNullOrEmpty(Author)) sb.Append($"  \"author\": \"{Escape(Author)}\",\n");
            sb.Append($"  \"difficulty\": {F(ClampDifficulty(Difficulty))},\n");
            if (!string.IsNullOrEmpty(Banner)) sb.Append($"  \"banner\": \"{Escape(Banner)}\",\n");
            if (HeaderGems > 0) sb.Append($"  \"gems\": {HeaderGems},\n");
            if (HeaderCrowns > 0) sb.Append($"  \"crowns\": {HeaderCrowns},\n");
            if (Invincible) sb.Append("  \"invincible\": true,\n");
            if (StartPortal) sb.Append("  \"startPortal\": true,\n");
            sb.Append($"  \"music\": \"{Escape(music)}\",\n");
            if (!string.IsNullOrEmpty(Look)) sb.Append($"  \"look\": \"{Escape(Look)}\",\n");
            sb.Append($"  \"speed\": {F(speed)},\n");
            sb.Append($"  \"speedEnd\": {F(speedEnd)},\n");
            sb.Append($"  \"speedRampRows\": {speedRampRows},\n");
            sb.Append("  \"themes\": [");
            for (int i = 0; themes != null && i < themes.Count; i++)
            {
                var t = themes[i];
                sb.Append(i == 0 ? "\n" : ",\n");
                string look = string.IsNullOrEmpty(t.look) ? "" : $", \"look\": \"{Escape(t.look)}\", \"lookRows\": {t.lookRows}";
                if (!string.IsNullOrEmpty(t.objects))
                    look += $", \"objects\": \"{Escape(t.objects)}\"" + (string.IsNullOrEmpty(t.look) ? $", \"lookRows\": {t.lookRows}" : "");
                string sky = (t.turnSun ? $", \"turnSun\": true, \"sun\": {V2(t.sun)}" : "") + (t.turnMoon ? $", \"turnMoon\": true, \"moon\": {V2(t.moon)}" : "");
                sb.Append($"    {{ \"row\": {t.row}, \"preset\": \"{Escape(t.preset)}\", \"rows\": {F(t.rows)}{look}{sky} }}");
            }
            sb.Append(themes != null && themes.Count > 0 ? "\n  ],\n" : "],\n");
            if (CameraChanges.Count > 0)
            {
                sb.Append("  \"cameras\": [");
                for (int i = 0; i < CameraChanges.Count; i++)
                {
                    var c = CameraChanges[i];
                    sb.Append(i == 0 ? "\n" : ",\n");
                    sb.Append($"    {{ \"row\": {c.row}, \"position\": {V(c.position)}, \"rotation\": {V(c.rotation)}, \"rows\": {F(c.rows)}, \"curve\": {F(c.curve)} }}");
                }
                sb.Append("\n  ],\n");
            }
            if (FilterChanges.Count > 0)
            {
                sb.Append("  \"filters\": [");
                for (int i = 0; i < FilterChanges.Count; i++)
                {
                    var f = FilterChanges[i];
                    sb.Append(i == 0 ? "\n" : ",\n");
                    sb.Append($"    {{ \"row\": {f.row}, \"effect\": \"{f.Effect}\", \"strength\": {F(f.strength)}, \"fade\": {F(f.fade)}, \"rows\": {F(f.rows)}, \"speed\": {F(f.speed)} }}");
                }
                sb.Append("\n  ],\n");
            }
            if (Checkpoints.Count > 0) sb.Append($"  \"checkpoints\": [{string.Join(", ", Checkpoints)}],\n");
            if (LoadDistance > 0) sb.Append($"  \"loadDistance\": {LoadDistance},\n");
            if (!DynamicLoading) sb.Append("  \"loadAll\": true,\n");
            if (withTestStart && HasTestStart) sb.Append($"  \"testStart\": [{TestStartRow}, {TestStartLane}],\n");
            PruneParts();
            if (Parts.Count > 0)
            {
                sb.Append("  \"parts\": [");
                for (int i = 0; i < Parts.Count; i++)
                {
                    var p = Parts[i];
                    sb.Append(i == 0 ? "\n" : ",\n");
                    sb.Append($"    {{ \"row\": {p.row}, \"lane\": {p.lane}, \"id\": {p.id}");
                    if (p.Uses(PartArgs.Field.Distance)) sb.Append($", \"distance\": {F(p.distance)}");
                    if (p.Uses(PartArgs.Field.Height)) sb.Append($", \"height\": {F(p.height)}");
                    if (p.Uses(PartArgs.Field.Pattern)) sb.Append($", \"pattern\": \"{Escape(p.pattern)}\"");
                    if (p.Uses(PartArgs.Field.Multiple)) sb.Append($", \"multiple\": {F(p.multiple)}");
                    if (p.Uses(PartArgs.Field.MusicOffset)) sb.Append($", \"musicOffset\": {(p.musicOffset ? "true" : "false")}");
                    if (p.Uses(PartArgs.Field.Seconds)) sb.Append($", \"seconds\": {F(p.seconds)}");
                    if (p.Uses(PartArgs.Field.StartRows)) sb.Append($", \"startRows\": {F(p.startRows)}");
                    if (p.Uses(PartArgs.Field.Text)) sb.Append($", \"text\": \"{Escape(p.text)}\"");
                    if (p.Uses(PartArgs.Field.Group)) sb.Append($", \"group\": {p.group}");
                    if (p.Uses(PartArgs.Field.MoveX)) sb.Append($", \"moveX\": {F(p.moveX)}, \"moveY\": {F(p.moveY)}, \"moveZ\": {F(p.moveZ)}");
                    if (p.Uses(PartArgs.Field.Spin)) sb.Append($", \"spin\": {F(p.spin)}");
                    if (p.Uses(PartArgs.Field.WithBall)) sb.Append($", \"withBall\": {(p.withBall ? "true" : "false")}");
                    if (p.Uses(PartArgs.Field.Loop)) sb.Append($", \"loop\": {(p.loop ? "true" : "false")}");
                    if (p.Uses(PartArgs.Field.Frames)) sb.Append($", \"frames\": {p.frames}");
                    if (p.Uses(PartArgs.Field.StartUp)) sb.Append($", \"startUp\": {(p.startUp ? "true" : "false")}");
                    if (p.Uses(PartArgs.Field.WholeRow)) sb.Append($", \"wholeRow\": {(p.wholeRow ? "true" : "false")}");
                    if (p.Uses(PartArgs.Field.Keep)) sb.Append($", \"keep\": {(p.keep ? "true" : "false")}, \"vanish\": {F(p.vanish)}");
                    if (p.Uses(PartArgs.Field.Speed)) sb.Append($", \"speed\": {F(p.speed)}");
                    if (p.Uses(PartArgs.Field.Kill)) sb.Append($", \"kill\": {(p.kill ? "true" : "false")}");
                    sb.Append(" }");
                }
                sb.Append("\n  ],\n");
            }
            PruneCellGroups();
            if (CellGroups.Count > 0)
            {
                sb.Append("  \"cellGroups\": [");
                for (int i = 0; i < CellGroups.Count; i++)
                    sb.Append(i == 0 ? "\n    " : ",\n    ").Append(JsonUtility.ToJson(CellGroups[i], false));
                sb.Append("\n  ],\n");
            }
            if (Groups.Count > 0)
            {
                Groups.Sort((a, b) => a.id.CompareTo(b.id));
                sb.Append("  \"groups\": [");
                for (int i = 0; i < Groups.Count; i++)
                    sb.Append(i == 0 ? "\n    " : ",\n    ").Append(JsonUtility.ToJson(Groups[i], false));
                sb.Append("\n  ],\n");
            }
            sb.Append("  \"props\": [");
            for (int i = 0; props != null && i < props.Count; i++)
            {
                var p = props[i];
                sb.Append(i == 0 ? "\n" : ",\n");
                sb.Append($"    {{ \"row\": {p.row}, \"x\": {F(p.x)}, \"y\": {F(p.y)}, \"model\": \"{Escape(p.model)}\", \"rotY\": {F(p.rotY)}, \"scale\": {F(p.scale)} }}");
            }
            sb.Append(props != null && props.Count > 0 ? "\n  ],\n" : "],\n");
            if (Customs.Count > 0)
            {
                sb.Append("  \"customs\": [");
                for (int i = 0; i < Customs.Count; i++)
                    sb.Append(i == 0 ? "\n    " : ",\n    ").Append(Customs[i].ToJson(false));
                sb.Append("\n  ],\n");
            }
            if (CustomThemes.Count > 0)
            {
                sb.Append("  \"customThemes\": [");
                for (int i = 0; i < CustomThemes.Count; i++)
                    sb.Append(i == 0 ? "\n    " : ",\n    ").Append(CustomThemes[i].ToJson(false));
                sb.Append("\n  ],\n");
            }
            if (ThemeSettings != null && !ThemeSettings.IsDefault)
                sb.Append("  \"themeSettings\": ").Append(JsonUtility.ToJson(ThemeSettings, false)).Append(",\n");
            AppendLayer(sb, "objects", objects);
            AppendLayer(sb, "collect", collects);
            sb.Append("  \"rows\": [\n");
            for (int r = 0; r < rows.Count; r++)
            {
                sb.Append("    \"");
                sb.Append(string.Join(",", rows[r]));
                sb.Append(r < rows.Count - 1 ? "\",\n" : "\"\n");
            }
            sb.Append("  ]\n}\n");
            return sb.ToString();
        }

        /// <summary>An Object or Collect layer, one line per row like "rows"; nothing when the layer is empty.</summary>
        void AppendLayer(StringBuilder sb, string key, List<int[]> layer)
        {
            int count = Mathf.Min(layer.Count, rows.Count);
            while (count > 0 && IsEmpty(layer[count - 1])) count--;
            if (count == 0) return;
            sb.Append($"  \"{key}\": [\n");
            for (int r = 0; r < count; r++)
            {
                sb.Append("    \"");
                sb.Append(string.Join(",", layer[r]));
                sb.Append(r < count - 1 ? "\",\n" : "\"\n");
            }
            sb.Append("  ],\n");
        }

        static string F(float v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        static string V2(Vector2 v) => $"{{ \"x\": {F(v.x)}, \"y\": {F(v.y)} }}";
        static string V(Vector3 v) => $"{{ \"x\": {F(v.x)}, \"y\": {F(v.y)}, \"z\": {F(v.z)} }}";
        static string Escape(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");

        // ---------------- Tiled Flare .txt ----------------

        /// <summary>
        /// Reads one layer of a Tiled "Flare" .txt level. The bottom of the file is the start of
        /// the level, so rows are flipped. Empty rows at the start and end are trimmed.
        /// </summary>
        public static LevelData Parse(string text, string layerName)
        {
            if (string.IsNullOrEmpty(text))
                throw new ArgumentException("RTXOn: level text is empty.");

            var lines = text.Replace("\r", "").Split('\n');
            bool inWantedLayer = false;
            bool inData = false;
            var fileRows = new List<int[]>();
            var layerNames = new List<string>();

            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.StartsWith("["))
                {
                    if (inData) break; // finished the wanted layer
                    inWantedLayer = false;
                    continue;
                }
                if (line.StartsWith("type="))
                {
                    var name = line.Substring(5).Trim();
                    layerNames.Add(name);
                    inWantedLayer = string.Equals(name, layerName, StringComparison.OrdinalIgnoreCase);
                    continue;
                }
                if (inWantedLayer && line.StartsWith("data="))
                {
                    inData = true;
                    continue;
                }
                if (!inData) continue;
                if (line.Length == 0) continue;

                fileRows.Add(ParseRow(line, $"layer '{layerName}'"));
            }

            if (fileRows.Count == 0)
                throw new ArgumentException($"RTXOn: layer '{layerName}' not found or empty. Layers in file: {string.Join(", ", layerNames)}");

            fileRows.Reverse(); // bottom of file = start of level

            int first = 0, last = fileRows.Count - 1;
            while (first <= last && IsEmpty(fileRows[first])) first++;
            while (last >= first && IsEmpty(fileRows[last])) last--;
            if (first > last)
                throw new ArgumentException($"RTXOn: layer '{layerName}' has no tiles.");

            var data = new LevelData(layerName, fileRows.GetRange(first, last - first + 1));
            data.SplitLayers();
            return data;
        }

        /// <summary>Every layer name in a Flare .txt, in file order.</summary>
        public static List<string> LayerNames(string text)
        {
            var names = new List<string>();
            foreach (var raw in text.Replace("\r", "").Split('\n'))
            {
                var line = raw.Trim();
                if (line.StartsWith("type=")) names.Add(line.Substring(5).Trim());
            }
            return names;
        }

        static int[] ParseRow(string line, string where)
        {
            var cells = (line ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var row = new int[Width];
            for (int c = 0; c < Width && c < cells.Length; c++)
            {
                if (!int.TryParse(cells[c].Trim(), out row[c]))
                    Debug.LogWarning($"RTXOn: bad tile '{cells[c]}' in {where}.");
            }
            return row;
        }

        /// <summary>No tile, object or pickup in the row.</summary>
        bool RowEmpty(int row) =>
            IsEmpty(rows[row]) && (row >= objects.Count || IsEmpty(objects[row])) && (row >= collects.Count || IsEmpty(collects[row]));

        static bool IsEmpty(int[] row)
        {
            foreach (var id in row) if (id != 0) return false;
            return true;
        }
    }
}
