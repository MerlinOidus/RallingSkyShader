using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace RTXOn
{
    /// <summary>Optional picture for one tile ID in the editor. Tiles without one show their number.</summary>
    [Serializable]
    public class TileIcon
    {
        public int id;
        public Sprite icon;
    }

    /// <summary>
    /// In-game level editor: a 5-lane grid to paint tiles on, a tile palette, Undo, Save, Load, level settings
    /// (music, speed), checkpoints, a music visual beside the rows, Play (the song plays and the grid scrolls
    /// with it) and Test (plays the level in the Gameplay scene).
    /// The UI is built in code when the scene starts, so the scene only needs this component.
    /// Save writes the Level File (a level.json) that the Gameplay scene plays (Game > Level File).
    /// </summary>
    public class LevelEditor : MonoBehaviour
    {
        [Header("Level")]
        [Tooltip("The level.json that opens and that Save writes. Empty, or an old Level.txt: Save makes NewLevel.json in the same folder.")]
        public TextAsset levelFile;
        [Tooltip("The levels Load offers (Level1, Level2... with their banners). Open Level Editor Scene fills it; in the Unity Editor every level.json next to Level1.json is listed anyway.")]
        public TextAsset[] levelFiles;
        [Tooltip("The LevelPreviews shipped with the game (Previews/LevelPreview1.json...): LevelPreview1 is where a new level's " +
                 "preview starts in a built game. Open Level Editor Scene fills it.")]
        public TextAsset[] previewFiles;

        [Header("Test in game")]
        [Tooltip("Scene that Test opens (it must be in File > Build Profiles > Scene List; Build Gameplay Scene adds it).")]
        public string gameplayScene = "Gameplay";
        [Tooltip("Scene Exit (and Esc) goes back to (the Main Menu level list), if it is in the build scene list.")]
        public string menuScene = "MainMenu";

        [Header("Music")]
        [Tooltip("Songs the Music setting offers: Music1..5 and your own clips (Open Level Editor Scene fills this). " +
                 "The Gameplay scene must know them too (Game > Audio: Level Music or Custom Music). " +
                 "Song files (.ogg .mp3 .wav) in Assets/StreamingAssets/Music show up as well.")]
        public AudioClip[] music;
        [Tooltip("Rows per second for Play and the music visual when the level has no \"speed\" " +
                 "(the game's Speed, 5.98 = World 1).")]
        public float defaultSpeed = 5.98f;

        const int NewLevelRows = 120;
        const string NewLevelMusic = "Massif";
        const int NewLevelGems = 10;

        [Header("Looks (3D view and grid colours)")]
        [Tooltip("Look of a level without \"look\" (the Gameplay scene's Track theme). Open Level Editor Scene fills it.")]
        public TrackTheme theme;
        [Tooltip("Every TrackTheme the Look button offers, and the looks of the level's theme changes. Open Level Editor Scene fills it.")]
        public TrackTheme[] looks;

        [Header("Banners (Level Settings)")]
        [Tooltip("Pictures Level Settings offers as the level's banner in the Main Menu (\"World N\" textures and a Banners folder). Open Level Editor Scene fills it.")]
        public Texture2D[] banners;

        [Header("Icons (optional)")]
        [Tooltip("A picture per tile ID for the palette and grid. Tiles without one show their number.")]
        public List<TileIcon> icons = new List<TileIcon>();

        public const int VisibleRows = 10;
        const float CellSize = 92f;
        const float RowLabelWidth = 90f;
        const float SliderWidth = 44f;
        const float TopBarHeight = 90f;
        const float LeftPanelWidth = 500f;
        const float RightPanelWidth = 300f;
        const float Margin = 10f;
        const float PaletteCell = 104f;
        const float FrameWidth = 5f;

        static Color BackgroundColor => EditorColors.Background;
        static Color BarColor => EditorColors.Bar;
        static Color PanelColor => EditorColors.Panel;
        static Color ButtonColor => EditorColors.Button;
        static Color AccentColor => EditorColors.Accent;
        static Color PlayColor => EditorColors.Go;
        static Color ExitColor => EditorColors.Danger;
        static Color SoftText => EditorColors.Soft;

        public LevelData Level { get; private set; }
        public int SelectedId { get; private set; } = 1;
        public int FirstRow { get; private set; }

        /// <summary>Highest FirstRow: lets you scroll one screen of empty rows past the end, to make the level longer.</summary>
        public int MaxFirstRow => Level == null ? 0 : Mathf.Max(0, Level.RowCount);

        LevelGridView grid;
        Slider scrollSlider;
        bool syncingSlider;
        Text fileLabel, statusLabel, rowsLabel, selectedName, selectedNumber, selectedHint;
        Text musicLabel, infoLabel, playButtonLabel, lookButtonLabel, view3DButtonLabel, dynamicLabel;
        InputField loadDistanceInput;
        const int MinLoadDistance = 10;
        static int MaxLoadDistance => GraphicsQuality.Mobile ? GraphicsQuality.MobileMaxRows : 200; // Android: 50
        EditorTrackView view3D;
        GameObject view3DControls;
        TriggerPanel triggerPanel;
        LevelSettingsPanel levelSettings;
        LevelPickerPanel levelPicker;
        PartPanel partPanel;
        CustomObstaclePanel customPanel;
        ThemeSettingsPanel themePanel;
        CustomThemePanel customThemePanel;
        SpriteMakerPanel spriteMaker;
        FileChooser fileChooser;
        int lookCacheVersion = -1;
        string lookCacheSettings;
        Button editCustomButton; // side panel: EDIT for the selected custom obstacle
        readonly List<GameObject> customButtons = new List<GameObject>(); // the Object tab's custom obstacles, after the + button
        readonly List<int> customIds = new List<int>();
        int customVersion = -1;
        Button editArgsButton; // side panel: the ARGS of the selected power-up / animated tile, for the tiles painted next
        // The settings each part gets when painted (EDIT ARGS), by tile ID; static, so they stay over a play test.
        static readonly Dictionary<int, PartArgs> brushArgs = new Dictionary<int, PartArgs>();
        bool triggerStrokeDone; // a drag with a trigger tool opens one trigger
        readonly Dictionary<string, TrackTheme> lookCache = new Dictionary<string, TrackTheme>(StringComparer.OrdinalIgnoreCase);
        InputField speedInput;
        Slider speedSlider;
        bool syncingSettings;

        // Play: the song in the editor, the grid scrolling with it.
        MusicWaveView wave;
        Image playhead;
        AudioSource preview;
        AudioClip song;
        bool songLoading;
        Coroutine songRoutine;
        bool playing;
        float playStartSeconds, playStartTime;
        Image selectedSwatch;
        ScrollRect paletteScroll;
        // Selection frames and favourite stars per ID (a favourite has a second button on the Collection tab).
        readonly Dictionary<int, List<Image>> paletteFrames = new Dictionary<int, List<Image>>();
        readonly Dictionary<int, List<Image>> favoriteBadges = new Dictionary<int, List<Image>>();
        readonly List<GameObject> collectionButtons = new List<GameObject>();
        Image favoriteStar; // side panel: the selected tile is a favourite
        Image musicPlayIcon, view3DIcon; // top bar icons that change with the button (Music / Pause, Camera / Map)
        Image favoriteButton; // the star's button: gold while collecting
        CellPanel cellPanel; // the Edit tool's window
        EventGroupsPanel eventsPanel; // top bar > Events, Edit tool > GROUP
        readonly Dictionary<int, Text> paletteNames = new Dictionary<int, Text>(); // Object tab: named after the look's models
        readonly List<KeyValuePair<TileGroup, GameObject>> paletteButtons = new List<KeyValuePair<TileGroup, GameObject>>();
        readonly Dictionary<TileGroup, Image> groupTabs = new Dictionary<TileGroup, Image>(); // the Tiles tab's pages
        readonly Image[] layerTabs = new Image[5]; // Tiles, Collect, Object, Collection, Triggers (Props tab removed: Custom Obstacles do it)
        TileGroup currentGroup = TileGroup.Floor;
        TileGroup lastTilesPage = TileGroup.Floor;
        readonly Dictionary<int, Sprite> iconLookup = new Dictionary<int, Sprite>();

        // Props tab: one button per model in the Props lists of the level's looks (ID PropToolFirst - index).
        readonly List<string> propModels = new List<string>();
        readonly List<GameObject> propButtons = new List<GameObject>();
        readonly List<GameObject> propSettings = new List<GameObject>(); // Place / Turn / Size, in the page row
        Text propPlaceLabel, propTurnLabel, propSizeLabel;
        Transform paletteContent;
        static readonly string[] PropPlaces = { "Cell", "Left", "Right", "Both" };
        static readonly float[] PropSizes = { 0.5f, 0.75f, 1f, 1.5f, 2f, 3f };
        const float PropSideX = 3.5f; // beside the track: one lane out from lane 1 / lane 5 and a half more
        static int propPlace, propTurn, propSize = 2; // static: they stay over a play test
        readonly HashSet<int> propStrokeCells = new HashSet<int>(); // a drag places each cell's prop once
        bool propStrokeSaved;

        // Undo: one entry per stroke (press, drag, release), holding what each painted cell was before.
        // col = CheckpointChange: the checkpoint of that row, before = 1 if it had one.
        struct Change
        {
            public int row, col, before;
            public TileLayer layer;
            public List<PropPlacement> props; // PropChange: every prop before the stroke
            public LevelData.RowsSnapshot rows; // RowsChange: the level before Row + / Row -
        }
        const int CheckpointChange = -1;
        const int StartChange = -2; // the Start Pos: before = row * Width + lane it had, -1 = none
        const int PropChange = -3;
        const int RowsChange = -4;
        bool startStrokeDone;       // a drag with the Start Pos tool sets it once
        Stack<List<Change>> undo = new Stack<List<Change>>();
        Stack<List<Change>> redo = new Stack<List<Change>>(); // what Undo took back; a new stroke empties it
        readonly HashSet<long> placed = new HashSet<long>();  // cells painted since the last refresh (row * Width + lane)
        List<Change> stroke;
        readonly HashSet<int> checkpointStrokeRows = new HashSet<int>(); // a drag with the Checkpoint tool flips each row once

        // Kept while the play test runs, so Undo still works back in the editor.
        static Stack<List<Change>> playTestUndo, playTestRedo;
        static TextAsset playTestFile;  // the level that was open, reopened when the play test comes back
        static string playTestPath;
        static int playTestNumber;

        bool dirty;
        bool confirmExit;      // a first Exit / Esc with unsaved changes only warns
        TextAsset openedFile;  // the Level File that was loaded
        string customPath;     // or the file that was loaded: a New+ level, or one that only exists in the build's save folder
        int listNumber;        // its number in the Load list (0 = not opened from the list)
        string previewOwner;   // editing a LevelPreview: the level file it belongs to (null = editing a level)
        LevelData pendingNew;  // what LoadLevel opens when the file does not exist yet (a LevelPreview's first rows)
        string savePath;       // where Save writes, fixed at Load: changing the Level File field or its text in Play mode cannot redirect it
        string saveName;       // file name of savePath, shown in the top bar
        string brokenPath;     // the saved file exists but could not be read
        bool confirmOverwrite; // a first Save press over a broken file only warns

        // Autosave: unsaved changes are written to Application.persistentDataPath/Autosave every AutosaveSeconds and
        // when the editor stops (Exit, Test, Play mode ending, Unity rebuilding the scripts during Play). Load gives
        // them back; Save or Exit without saving removes them.
        const float AutosaveSeconds = 15f;
        float autosaveAt = -1f;
        string restoredAutosave;  // the autosave this Load opened (opening the level again drops it)
        bool dropChanges;         // Exit without saving: nothing is autosaved on the way out


        void Awake()
        {
            // Every official theme, also in a build (Android): not only the ones in the scene's Looks.
            var all = new System.Collections.Generic.List<TrackTheme>();
            if (looks != null) all.AddRange(looks);
            ThemeRegistry.AddTo(all);
            looks = all.ToArray();
            // No Theme on the Level Editor object (its theme asset was renamed or deleted): use MassifTheme or the
            // first look, so the 3D view never draws with empty materials (magenta).
            if (theme == null && looks != null)
            {
                foreach (var l in looks) if (l != null && l.name == "MassifTheme") { theme = l; break; }
                if (theme == null) foreach (var l in looks) if (l != null) { theme = l; break; }
            }
            // The editor needs a wide screen: locked to landscape on a phone; the menu and the game turn freely again.
            ScreenRotation.Landscape();

            foreach (var i in icons)
                if (i != null && i.icon != null) iconLookup[i.id] = i.icon;

            preview = gameObject.AddComponent<AudioSource>();
            preview.playOnAwake = false;
            preview.spatialBlend = 0f; // 2D: same loudness wherever the camera is
            preview.volume = 1f;
            preview.ignoreListenerPause = true;
            EnsureCameraAndEventSystem();
            // Edit in the Main Menu's level list: open that level.
            if (LevelSelect.EditLevel != null || LevelSelect.EditPath != null)
            {
                levelFile = LevelSelect.EditLevel;
                customPath = LevelSelect.EditPath;
                LevelSelect.EditLevel = null;
                LevelSelect.EditPath = null;
            }
            // Back from Test: the level that was open, not the scene's Level File.
            if (PlayTest.Returning && (playTestFile != null || playTestPath != null))
            {
                levelFile = playTestFile;
                customPath = playTestPath;
                listNumber = playTestNumber;
            }
            playTestFile = null;
            playTestPath = null;
            playTestNumber = 0;
            CustomObstacles.LoadLibrary(); // the .rsrt files in Levels/Obstacles
            ThemeLibrary.LoadLibrary();    // the .themert files in Levels/Themes
            ThemeLibrary.LoadBundled();    // build: the themes shipped with the game
            EditorColors.Load();           // the editor wears the Main Menu's colour set
            BuildUi();
            if (OpensOfficialInBuild()) OpenFirstCustomLevel();
            else LoadLevel();
            if (PlayTest.Returning) ReturnFromPlayTest();
            Select(SelectedId);
            ShowGroup(TileGroup.Floor);
        }

        void OnDisable()
        {
            if (collecting) EndCollecting(); // closing the editor keeps what was collected
            if (dirty && !dropChanges) Autosave();
        }

        void OnDestroy()
        {
            ScreenFilters.Clear();
            ScreenFilters.Target = null;
            // The next scene sets its own turning (Main Menu free, Gameplay portrait).
        }

        public Sprite IconFor(int id) =>
            CustomObstacle.IsCustomId(id) ? CustomObstacles.IconOf(id) : iconLookup.TryGetValue(id, out var s) ? s : null;

        // ---------------- Editing ----------------

        public void Select(int id)
        {
            if (id == TilePalette.NewCustomTool) // the + button: a new custom obstacle, the selection stays
            {
                OpenCustomObstacle(null);
                return;
            }
            if (id == TilePalette.PasteTool && copied == null) id = TilePalette.CopyTool; // nothing copied yet
            SetFrames(SelectedId, false);
            SelectedId = id;
            SetFrames(id, true);

            // The Copy brush shows the copied cell (its top: object, else pickup, else tile).
            int look = id == TilePalette.PasteTool ? copied.Top : id;
            var icon = IconFor(look);
            selectedSwatch.sprite = icon;
            selectedSwatch.color = icon != null ? Color.white : TilePalette.ColorOf(look);
            selectedNumber.text = icon != null ? "" : id == TilePalette.PasteTool && look != 0 ? TilePalette.Label(look) : TilePalette.Label(id);
            selectedNumber.color = TilePalette.TextColorOf(look);
            selectedName.text = id == 0 ? "Erase Tile" : TilePalette.IsPropTool(id) ? PropModelName(id) ?? "Prop"
                              : id == TilePalette.PasteTool ? "Copy: " + copied.Name : DisplayName(id);
            selectedHint.text = TilePalette.Hint(id);
            UpdateFavoriteStar();
            ShowMarqueeBar();

            // Custom Obstacles have ARGS too (their Group), but only per cell (Edit tool); their side button is EDIT.
            bool part = PartArgs.IsPart(id) && !CustomObstacle.IsCustomId(id);
            if (editArgsButton != null) editArgsButton.gameObject.SetActive(part);
            if (editCustomButton != null) editCustomButton.gameObject.SetActive(CustomObstacles.TryGet(id, out _));
            // ARGS window open for new tiles: it follows the selection.
            if (partPanel != null && partPanel.IsBrush)
            {
                if (part) partPanel.Open(BrushArgs(id), true);
                else partPanel.Close();
            }
        }

        public void BeginStroke()
        {
            marqueeStroke = false;
            stroke = new List<Change>();
            checkpointStrokeRows.Clear();
            startStrokeDone = false;
            triggerStrokeDone = false;
            propStrokeCells.Clear();
            propStrokeSaved = false;
        }

        public void Paint(int row, int col, int id)
        {
            if (Level == null || row < 0 || col < 0 || col >= LevelData.Width) return;
            if (id == TilePalette.CheckpointTool)
            {
                ToggleCheckpoint(row);
                return;
            }
            if (id == TilePalette.StartTool)
            {
                ToggleStart(row, col);
                return;
            }
            if (id == TilePalette.EditTool || id == TilePalette.CopyTool)
            {
                if (triggerStrokeDone) return; // one cell per click
                triggerStrokeDone = true;
                if (id == TilePalette.EditTool) OpenCellEditor(row, col);
                else CopyCell(row, col);
                return;
            }
            if (id == TilePalette.PasteTool)
            {
                if (PasteCell(row, col)) RefreshView();
                return;
            }
            if (id == TilePalette.ErasePropTool || TilePalette.IsPropTool(id))
            {
                if (PaintProp(row, col, id)) RefreshView();
                return;
            }
            if (id == TilePalette.MarqueeTool)
            {
                MarqueePaint(row);
                return;
            }
            if (TilePalette.IsRowTool(id))
            {
                if (triggerStrokeDone) return; // one row per click, not one per cell of a drag
                triggerStrokeDone = true;
                ChangeRows(row, id == TilePalette.RowInsertTool);
                return;
            }
            if (id == TilePalette.EraseObjectTool) // everything but the floor
            {
                bool cleared = SetCell(TileLayer.Object, row, col, 0);
                cleared |= SetCell(TileLayer.Collect, row, col, 0);
                cleared |= PaintProp(row, col, TilePalette.ErasePropTool);
                if (cleared) RefreshView();
                return;
            }
            if (id == TilePalette.ThemeTool || id == TilePalette.CameraTool || id == TilePalette.FilterTool)
            {
                if (triggerStrokeDone) return;
                triggerStrokeDone = true;
                if (id == TilePalette.ThemeTool) OpenThemeTrigger(row);
                else if (id == TilePalette.FilterTool) OpenFilterTrigger(row);
                else OpenCameraTrigger(row);
                return;
            }
            // Layers: a tile is the floor; objects and pickups stand on it (an empty cell gets plain floor first).
            var layer = TilePalette.LayerOf(id);
            int value = Mathf.Max(0, id); // the erase tools paint 0 on their layer
            bool changed;
            if (layer == TileLayer.Tile)
            {
                changed = SetCell(TileLayer.Tile, row, col, value);
                if (value == 0) // the floor is gone: so is what stood on it
                {
                    changed |= SetCell(TileLayer.Object, row, col, 0);
                    changed |= SetCell(TileLayer.Collect, row, col, 0);
                }
            }
            else
            {
                changed = SetCell(layer, row, col, value);
                if (value != 0 && value != 31 && Level.Get(row, col) == 0) changed |= SetCell(TileLayer.Tile, row, col, 1); // 31 floats in the air
            }
            // A power-up or animated tile gets the settings set with EDIT ARGS, no window per tile.
            if (PartArgs.IsPart(id) && !CustomObstacle.IsCustomId(id) && Level.HasPart(row, col, id) && StampArgs(row, col, id)) changed = true;
            if (changed) RefreshView();
        }

        bool SetCell(TileLayer layer, int row, int col, int id)
        {
            int before = Level.Get(layer, row, col);
            if (before == id) return false;
            Level.Set(layer, row, col, id);
            stroke?.Add(new Change { row = row, col = col, before = before, layer = layer });
            if (stroke != null && id != 0) placed.Add((long)row * LevelData.Width + col); // the views animate it once
            SetDirty(true);
            return true;
        }

        /// <summary>What the right mouse button paints: Erase Tile in the Tiles tab, Erase Object in the others.</summary>
        public int EraserId => TilePalette.IsTilesPage(currentGroup) ||
                               (currentGroup == TileGroup.Collection && SelectedId > 0 && TilePalette.LayerOf(SelectedId) == TileLayer.Tile)
            ? 0 : TilePalette.EraseObjectTool;

        /// <summary>Row + / Row -: an empty row in at <paramref name="row"/>, or that row out; the rows after it move.</summary>
        void ChangeRows(int row, bool insert)
        {
            if (row < 0) return;
            if (!insert && (row >= Level.RowCount || Level.RowCount <= 1))
            {
                Status(row >= Level.RowCount ? $"Row {row} is past the end of the level: nothing to delete." : "The level needs at least one row.");
                return;
            }
            stroke?.Add(new Change { row = row, col = RowsChange, rows = Level.SnapshotRows() });
            if (insert) Level.InsertRow(row);
            else Level.DeleteRow(row);
            SetDirty(true);
            RefreshView();
            Status(insert ? $"Empty row put in at row {row}: the rows after it moved one on. Undo takes it out."
                          : $"Row {row} deleted: the rows after it moved one back. Undo brings it back.");
        }

        public void EndStroke()
        {
            if (stroke != null && stroke.Count > 0)
            {
                undo.Push(stroke);
                redo.Clear(); // a new change: what was undone can't come back on top of it
            }
            stroke = null;
            if (Level == null) return;
            Level.TrimEmptyEnd(); // erasing the last rows makes the level shorter
            RefreshView();
        }

        public void Undo()
        {
            // Undo pressed with a second finger while painting: finish that stroke first, so it is the one undone.
            grid.CancelStroke();
            EndStroke();
            if (undo.Count == 0)
            {
                Status("Nothing to undo.");
                return;
            }
            var last = undo.Pop();
            redo.Push(Revert(last));
            Status($"Undid {last.Count} change(s). {undo.Count} more undo step(s), {redo.Count} redo step(s).");
        }

        /// <summary>Puts back what the last Undo took away (Ctrl+Y or Ctrl+Shift+Z). A new paint after Undo clears Redo.</summary>
        public void Redo()
        {
            grid.CancelStroke();
            EndStroke();
            if (redo.Count == 0)
            {
                Status("Nothing to redo.");
                return;
            }
            var next = redo.Pop();
            undo.Push(Revert(next));
            Status($"Redid {next.Count} change(s). {redo.Count} more redo step(s).");
        }

        /// <summary>
        /// Puts every change of a step back to its Before (last change first) and returns the opposite step: what each
        /// one was just before, so running Revert on that step again brings this state back (Undo and Redo use each other's).
        /// </summary>
        List<Change> Revert(List<Change> step)
        {
            var back = new List<Change>(step.Count);
            for (int i = step.Count - 1; i >= 0; i--)
            {
                var c = step[i];
                if (c.col == CheckpointChange)
                {
                    back.Add(new Change { row = c.row, col = CheckpointChange, before = Level.Checkpoints.Contains(c.row) ? 1 : 0 });
                    SetCheckpoint(c.row, c.before == 1);
                }
                else if (c.col == StartChange)
                {
                    back.Add(new Change { row = c.row, col = StartChange, before = Level.HasTestStart ? Level.TestStartRow * LevelData.Width + Level.TestStartLane : -1 });
                    SetStart(c.before);
                }
                else if (c.col == RowsChange)
                {
                    back.Add(new Change { row = c.row, col = RowsChange, rows = Level.SnapshotRows() });
                    Level.RestoreRows(c.rows);
                }
                else if (c.col == PropChange)
                {
                    back.Add(new Change { col = PropChange, props = new List<PropPlacement>(Level.Props) });
                    Level.Props.Clear();
                    Level.Props.AddRange(c.props);
                }
                else
                {
                    back.Add(new Change { row = c.row, col = c.col, layer = c.layer, before = Level.Get(c.layer, c.row, c.col) });
                    Level.Set(c.layer, c.row, c.col, c.before);
                }
            }
            // Kept in this order: Revert runs a step from its end, so the opposite step puts the first change back first.
            Level.TrimEmptyEnd(); // undoing a stroke that made the level longer makes it short again
            SetDirty(true);
            RefreshView();
            return back;
        }

        // ---------------- Checkpoints ----------------

        /// <summary>True for a checkpoint row (crowns are tiles, 150, and show in the grid like other tiles).</summary>
        public bool IsCheckpointRow(int row) => Level != null && row > 0 && Level.Checkpoints.Contains(row);

        /// <summary>Checkpoint tool: puts a checkpoint on a row or takes it off (once per row per stroke).</summary>
        void ToggleCheckpoint(int row)
        {
            if (row <= 0 || row >= Level.RowCount - 1 || !checkpointStrokeRows.Add(row)) return;
            bool had = Level.Checkpoints.Contains(row);
            SetCheckpoint(row, !had);
            stroke?.Add(new Change { row = row, col = CheckpointChange, before = had ? 1 : 0 });
            SetDirty(true);
            RefreshView();
        }

        // ---------------- Start Pos (Test only) ----------------

        /// <summary>The cell where Test starts, or false when the level has none (Test then starts at the bottom row on screen).</summary>
        public bool IsStartCell(int row, int col) => Level != null && Level.HasTestStart && Level.TestStartRow == row && Level.TestStartLane == col;

        /// <summary>Start Pos tool: puts the Test start on a cell, or takes it off when it is already there.</summary>
        void ToggleStart(int row, int col)
        {
            if (startStrokeDone) return;
            startStrokeDone = true;
            int before = Level.HasTestStart ? Level.TestStartRow * LevelData.Width + Level.TestStartLane : -1;
            int after = IsStartCell(row, col) ? -1 : row * LevelData.Width + col;
            SetStart(after);
            stroke?.Add(new Change { row = row, col = StartChange, before = before });
            SetDirty(true);
            RefreshView();
            Status(after < 0 ? "Start Pos taken off: Test starts at the bottom row on screen."
                             : EditingPreview && row >= LevelPreviews.Rows ? $"Start Pos at row {row}: a LevelPreview has only rows 0-{LevelPreviews.Rows - 1}, so Save takes it off."
                             : EditingPreview ? $"Start Pos at row {row}, lane {col + 1}: Play (T) and the Main Menu's LevelPreview start the ball there."
                             : $"Start Pos at row {row}, lane {col + 1}: Play (T) starts the ball there.");
        }

        void SetStart(int cell)
        {
            Level.TestStartRow = cell < 0 ? -1 : cell / LevelData.Width;
            Level.TestStartLane = cell < 0 ? -1 : cell % LevelData.Width;
        }

        void SetCheckpoint(int row, bool on)
        {
            Level.Checkpoints.Remove(row);
            if (on) Level.Checkpoints.Add(row);
            Level.Checkpoints.Sort();
        }

        // ---------------- Props ----------------

        /// <summary>Prop tool: puts its model on the cell (or beside the track, see Place); Erase Object takes them off.</summary>
        bool PaintProp(int row, int col, int id)
        {
            if (!propStrokeCells.Add(row * LevelData.Width + col)) return false;
            if (id == TilePalette.ErasePropTool)
            {
                bool any = false;
                foreach (var p in Level.Props)
                    if (p != null && p.row == row && PropInCell(p, col)) { any = true; break; }
                if (!any) return false;
                SaveProps();
                Level.Props.RemoveAll(p => p == null || (p.row == row && PropInCell(p, col)));
                SetDirty(true);
                return true;
            }
            string model = PropModelName(id);
            if (model == null) return false;
            bool changed = false;
            foreach (float x in PropXs(col))
            {
                if (Level.Props.Exists(p => p != null && p.row == row && Mathf.Abs(p.x - x) < 0.01f &&
                                            string.Equals(p.model, model, StringComparison.OrdinalIgnoreCase))) continue;
                if (!changed) SaveProps();
                changed = true;
                Level.Props.Add(new PropPlacement { row = row, x = x, y = 0f, model = model, rotY = propTurn * 90f, scale = PropSizes[propSize] });
            }
            if (changed) SetDirty(true);
            return changed;
        }

        /// <summary>Where Place puts a prop painted on lane <paramref name="col"/> (0..4): the cell, or beside the track.</summary>
        static IEnumerable<float> PropXs(int col)
        {
            switch (propPlace)
            {
                case 1: yield return -PropSideX; break;
                case 2: yield return PropSideX; break;
                case 3: yield return -PropSideX; yield return PropSideX; break;
                default: yield return col - 2f; break;
            }
        }

        /// <summary>True when the prop shows in that grid cell: lane 1 and 5 also hold the props beside the track.</summary>
        static bool PropInCell(PropPlacement p, int col)
        {
            if (col == 0 && p.x <= -1.5f) return true;
            if (col == LevelData.Width - 1 && p.x >= 1.5f) return true;
            return Mathf.Abs(p.x - (col - 2f)) < 0.5f;
        }

        /// <summary>The first prop change of a stroke keeps every prop as it was, for Undo.</summary>
        void SaveProps()
        {
            if (propStrokeSaved) return;
            propStrokeSaved = true;
            stroke?.Add(new Change { col = PropChange, props = new List<PropPlacement>(Level.Props) });
        }

        string PropModelName(int id)
        {
            int i = TilePalette.PropIndex(id);
            return i >= 0 && i < propModels.Count ? propModels[i] : null;
        }

        /// <summary>The props on a row, for the grid and the 3D view.</summary>
        public List<PropPlacement> PropsAt(int row)
        {
            var list = new List<PropPlacement>();
            if (Level != null)
                foreach (var p in Level.Props)
                    if (p != null && p.row == row && !string.IsNullOrEmpty(p.model)) list.Add(p);
            return list;
        }

        /// <summary>Changes when a row's props change, so the 3D view rebuilds that row.</summary>
        public string PropsKey(int row)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var p in PropsAt(row)) sb.Append(p.model).Append(p.x).Append(',').Append(p.y).Append(',').Append(p.rotY).Append(',').Append(p.scale).Append(';');
            return sb.ToString();
        }

        /// <summary>The level's look (the default Theme if none), then the looks of its theme changes: where props come from.</summary>
        List<TrackTheme> PropSources()
        {
            var sources = new List<TrackTheme>();
            var start = Level != null ? FindLook(Level.Look) : null;
            sources.Add(start != null ? start : DefaultLook());
            if (Level != null)
                foreach (var c in Level.LookChanges()) sources.Add(FindLook(c.look));
            return sources;
        }

        /// <summary>A prop's model, looked up like the game: the level's look first, then its theme changes' looks.</summary>
        public GameObject FindPropModel(string name, out TrackTheme owner)
        {
            foreach (var look in PropSources())
            {
                var m = look != null ? look.FindProp(name) : null;
                if (m == null) continue;
                owner = look;
                return m;
            }
            owner = null;
            return null;
        }

        /// <summary>Every model in the Props lists of the level's look and its theme changes (the default Theme if none).</summary>
        List<string> CollectPropModels()
        {
            var names = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var look in PropSources())
            {
                if (look == null) continue;
                foreach (var m in look.PropModels())
                    if (m != null && seen.Add(m.name)) names.Add(m.name);
            }
            return names;
        }

        /// <summary>Makes the Props tab's buttons again from the looks' Props lists.</summary>
        void RebuildPropButtons()
        {
            if (paletteContent == null) return;
            var models = CollectPropModels();
            bool same = models.Count == propModels.Count && propButtons.Count == models.Count;
            for (int i = 0; same && i < models.Count; i++) same = models[i] == propModels[i];
            if (same) return;
            foreach (var b in propButtons) RemovePaletteButton(b);
            propButtons.Clear();
            propModels.Clear();
            propModels.AddRange(models);
            for (int i = 0; i < propModels.Count; i++)
            {
                var b = MakePaletteButton(paletteContent, new PaletteTile(TilePalette.PropToolFirst - i, propModels[i], TileGroup.Props));
                paletteButtons.Add(new KeyValuePair<TileGroup, GameObject>(TileGroup.Props, b));
                propButtons.Add(b);
            }
            // A prop tool whose model is gone from the list: back to the eraser.
            if (TilePalette.IsPropTool(SelectedId) && PropModelName(SelectedId) == null) Select(TilePalette.EraseObjectTool);
            else Select(SelectedId);
        }

        void CyclePropPlace()
        {
            propPlace = (propPlace + 1) % PropPlaces.Length;
            UpdatePropLabels();
            Status(propPlace == 0 ? "Props go on the cell you click." :
                   $"Props go beside the track ({PropPlaces[propPlace].ToLowerInvariant()}), on the row you click.");
        }

        void CyclePropTurn()
        {
            propTurn = (propTurn + 1) % 4;
            UpdatePropLabels();
        }

        void CyclePropSize()
        {
            propSize = (propSize + 1) % PropSizes.Length;
            UpdatePropLabels();
        }

        void UpdatePropLabels()
        {
            if (propPlaceLabel != null) propPlaceLabel.text = "Place: " + PropPlaces[propPlace];
            if (propTurnLabel != null) propTurnLabel.text = $"Turn: {propTurn * 90}°";
            if (propSizeLabel != null) propSizeLabel.text = $"Size: {PropSizes[propSize]:0.##}x";
        }

        // ---------------- Custom obstacles ----------------

        void OpenCustomObstacle(CustomObstacle def)
        {
            StopMusic();
            if (triggerPanel != null && triggerPanel.IsOpen) triggerPanel.Close();
            if (partPanel != null && partPanel.IsOpen) partPanel.Close();
            customPanel.Open(def);
        }

        /// <summary>The Object tab's custom obstacle buttons, after the + button: made again when the list changed.</summary>
        void RebuildCustomButtons(bool force = false)
        {
            if (paletteContent == null) return;
            var all = CustomObstacles.All();
            bool same = !force && customVersion == CustomObstacles.Version && all.Count == customIds.Count;
            for (int i = 0; same && i < all.Count; i++) same = all[i].id == customIds[i];
            if (same) return;
            foreach (var b in customButtons) RemovePaletteButton(b);
            customButtons.Clear();
            customIds.Clear();
            customVersion = CustomObstacles.Version;
            foreach (var c in all)
            {
                // Its own tab: Object, the Tiles tab's first page (a floor tile) or Collect (a pickup).
                var group = c.layer == CustomLayer.Tile ? TileGroup.Floor : c.layer == CustomLayer.Collect ? TileGroup.Collect : TileGroup.Obstacle;
                var b = MakePaletteButton(paletteContent, new PaletteTile(c.id, c.name, group));
                b.SetActive(currentGroup == group);
                paletteButtons.Add(new KeyValuePair<TileGroup, GameObject>(group, b));
                customButtons.Add(b);
                customIds.Add(c.id);
            }
        }

        /// <summary>
        /// A custom obstacle saved as another kind (Object / Tile / Collect): its painted cells move to that layer. As a tile
        /// it takes the floor's place; a former tile leaves plain floor (1) under it. An Object / Collect place that is
        /// already taken keeps what is there and the obstacle is taken off that cell. Undo does not cover this.
        /// </summary>
        void MoveCustomCells(int id)
        {
            if (Level == null) return;
            var to = TileCatalog.LayerOf(id);
            TileLayer[] layers = { TileLayer.Tile, TileLayer.Object, TileLayer.Collect };
            int moved = 0;
            for (int r = 0; r < Level.RowCount; r++)
                for (int c = 0; c < LevelData.Width; c++)
                    foreach (var from in layers)
                    {
                        if (from == to || Level.Get(from, r, c) != id) continue;
                        Level.Set(from, r, c, from == TileLayer.Tile ? 1 : 0);
                        if (to == TileLayer.Tile || Level.Get(to, r, c) == 0) Level.Set(to, r, c, id);
                        moved++;
                    }
            if (moved == 0) return;
            Level.PruneParts(); // ARGS stay with the cells that still have the ID on its layer
            SetDirty(true);
            Status($"{TilePalette.Name(id)}: {moved} cells moved to the {to} layer.");
        }

        /// <summary>The Custom Obstacles window saved one: it is in the palette, selected, and the grid and 3D view show the change.</summary>
        public void CustomObstacleSaved(int id)
        {
            RebuildCustomButtons(true);
            MoveCustomCells(id);
            var group = TilePalette.PageOf(TilePalette.GroupOf(id));
            if (currentGroup != group) ShowGroup(group);
            Select(id);
            // Cells that use it changed: the level must be saved again to carry the new version.
            if (CustomObstacles.CountIn(Level, id) > 0) SetDirty(true);
            RefreshView();
        }

        public void CustomObstacleDeleted(int id, string name)
        {
            RebuildCustomButtons(true);
            if (SelectedId == id) Select(TilePalette.EraseObjectTool);
            RefreshView();
            Status($"Deleted custom obstacle {name} (ID {id}).");
        }

        /// <summary>Where custom obstacles and props find project models: the level's look and its theme changes' looks.</summary>
        public List<TrackTheme> CustomThemes()
        {
            var list = PropSources();
            list.RemoveAll(t => t == null);
            return list;
        }

        /// <summary>The level's own look (the default Theme if none).</summary>
        public TrackTheme CurrentLook()
        {
            if (!string.IsNullOrEmpty(PreviewLook)) return FindLook(PreviewLook) ?? DefaultLook();
            var look = Level != null ? FindLook(Level.Look) : null;
            return look != null ? look : DefaultLook();
        }

        public void ShowStatus(string message) => Status(message);

        // ---------------- Music and speed ----------------

        /// <summary>Every song the Music setting can pick: "" (the level's number song), the clips, then song files.</summary>
        List<string> MusicChoices()
        {
            var list = new List<string> { "" };
            if (music != null)
                foreach (var c in music)
                    if (c != null && !list.Contains(c.name)) list.Add(c.name);
            foreach (var f in MusicLibrary.FileNames())
                if (!list.Contains(f)) list.Add(f);
            if (Level != null && !string.IsNullOrEmpty(Level.Music) && !list.Contains(Level.Music)) list.Add(Level.Music);
            return list;
        }

        public void CycleMusic(int step)
        {
            if (Level == null) return;
            StopMusic();
            var choices = MusicChoices();
            int i = choices.IndexOf(Level.Music ?? "");
            i = ((i < 0 ? 0 : i) + step + choices.Count) % choices.Count;
            Level.Music = choices[i];
            SetDirty(true);
            LoadSong();
            RefreshSettings();
            Status(choices.Count == 1 ? "No songs yet: run RTX ON > Open Level Editor Scene, or put song files in Assets/StreamingAssets/Music."
                                      : $"Music: {MusicName(Level.Music)}");
        }

        static string MusicName(string name) => string.IsNullOrEmpty(name) ? "Level song" : name;

        /// <summary>The level's song as the Music setting shows it (Level Settings > Music).</summary>
        public string MusicLabel => MusicName(Level != null ? Level.Music : null);

        /// <summary>
        /// Level Settings > Music > Choose File...: a song file from any folder (PC or phone) becomes the level's music.
        /// It is copied into a Music folder (MusicLibrary.Import), so Play, Test and the Main Menu find it by name.
        /// </summary>
        public void ChooseMusicFile(Action<string> onMessage)
        {
            if (Level == null) return;
            StopMusic();
            fileChooser.Browse("Choose the level's music (.mp3 .ogg .wav)", MusicLibrary.FileTypes, path =>
            {
                string message;
                try
                {
                    string name = MusicLibrary.Import(path);
                    Level.Music = name;
                    SetDirty(true);
                    LoadSong();
                    RefreshSettings();
                    message = $"Music: {name}. Save keeps it in the level.";
                }
                catch (Exception e)
                {
                    message = "Could not use that song: " + e.Message;
                }
                Status(message);
                onMessage?.Invoke(message);
            });
        }

        /// <summary>Sets the start speed (rows per second, 1-20, in 0.1 steps). There is no BPM any more: only Speed.</summary>
        void SetSpeed(float speed)
        {
            if (Level == null) return;
            speed = Mathf.Clamp(Mathf.Round(speed * 10f) / 10f, LevelData.MinSpeed, LevelData.MaxSpeed);
            if (Mathf.Approximately(speed, Level.SpeedStart)) { RefreshSettings(); return; }
            Level.SpeedStart = speed;
            SetDirty(true);
            RefreshSettings();
            RefreshView(); // the music visual follows the new speed
        }

        void OnSpeedTyped(string text)
        {
            if (float.TryParse(text.Replace(',', '.'), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float speed) && speed > 0f)
                SetSpeed(speed);
            else RefreshSettings();
        }

        /// <summary>Speed at the start of the level (rows per second), as Level Settings shows it.</summary>
        public float StartSpeed => Level != null ? SpeedAt(0f) : LevelData.DefaultSpeed;

        /// <summary>Level Settings' Start Speed: the same as Speed in the side panel.</summary>
        public void SetStartSpeed(float speed) => SetSpeed(speed);

        void OnSpeedSlider(float value)
        {
            if (!syncingSettings) SetSpeed(value);
        }

        /// <summary>Rows per second for the editor's music timing: the level's speed, else Default Speed.</summary>
        float SpeedAt(float row) => Level.SpeedAt(row, defaultSpeed);

        void RefreshSettings()
        {
            if (Level == null || musicLabel == null) return;
            syncingSettings = true;
            musicLabel.text = MusicName(Level.Music);
            if (lookButtonLabel != null) lookButtonLabel.text = "Theme: " + LookName();
            if (dynamicLabel != null)
            {
                dynamicLabel.text = Level.DynamicLoading ? "Dynamic: ON" : "Dynamic: OFF";
                loadDistanceInput.interactable = Level.DynamicLoading;
                if (!loadDistanceInput.isFocused) loadDistanceInput.text = Level.LoadDistanceOrDefault.ToString();
            }
            if (view3D != null) view3D.rowsAhead = Mathf.Min(Level.DynamicLoading ? Level.LoadDistanceOrDefault : MaxLoadDistance, MaxLoadDistance);
            if (!speedInput.isFocused) speedInput.text = SpeedAt(0f).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            speedSlider.value = SpeedAt(0f);
            syncingSettings = false;

            int gems = Level.CountGems();
            int crowns = Level.CountCrowns();
            int gemTarget = Level.HeaderGems > 0 ? Level.HeaderGems : NewLevelGems; // Gem Number of Level Settings
            infoLabel.text = $"Crowns {crowns}/3{(crowns == 3 ? " OK" : "")}   Gems {gems}/{gemTarget}{(gems >= gemTarget ? " OK" : "")}\nCheckpoints: {Level.Checkpoints.Count}";
        }

        // ---------------- Level Settings window / menu ----------------

        void OpenLevelSettings()
        {
            if (Level == null) return;
            grid.CancelStroke();
            EndStroke();
            if (triggerPanel != null && triggerPanel.IsOpen) triggerPanel.Close();
            levelSettings.Open();
        }

        /// <summary>The Level Settings window changed the level.</summary>
        public void SettingsChanged()
        {
            confirmExit = false;
            SetDirty(true);
            RefreshView();
        }

        /// <summary>
        /// The level's number for its automatic banner ("World N"): its place in the Load list (as in the Main Menu),
        /// else the number in the file name, else 1.
        /// </summary>
        public int LevelNumber
        {
            get
            {
                if (listNumber > 0) return listNumber;
                string name = levelFile != null ? levelFile.name : saveName ?? "";
                var digits = System.Text.RegularExpressions.Regex.Match(name, @"\d+");
                return digits.Success && int.TryParse(digits.Value, out int n) && n > 0 ? n : 1;
            }
        }

        /// <summary>Exit button / Esc: back to the Main Menu. With unsaved changes the first press only warns.</summary>
        void BackToMenu()
        {
            string scene = !string.IsNullOrEmpty(LevelSelect.MenuScene) ? LevelSelect.MenuScene : menuScene;
            if (string.IsNullOrEmpty(scene) || !Application.CanStreamedLevelBeLoaded(scene))
            {
                Status($"No menu: scene \"{scene}\" is not in the build scene list (RTX ON > Build Main Menu Scene adds it).");
                return;
            }
            if (dirty && !confirmExit)
            {
                confirmExit = true;
                Status("You have unsaved changes. Press Exit (or Esc) again to leave without saving, or Save first.");
                return;
            }
            if (dirty)
            {
                dropChanges = true; // left without saving on purpose
                DeleteAutosave();
            }
            StopMusic();
            LevelSelect.SetMenu(scene);
            LoadingScreen.Load(scene);
        }

        // ---------------- Dynamic Loading ----------------

        /// <summary>
        /// Dynamic Loading on: the game builds rows Load Distance ahead of the ball, a few per frame (smooth).
        /// Off: it builds the whole level at the start (long load, nothing built while playing).
        /// </summary>
        void ToggleDynamicLoading()
        {
            if (Level == null) return;
            Level.DynamicLoading = !Level.DynamicLoading;
            SetDirty(true);
            RefreshView();
            Status(Level.DynamicLoading ? $"Dynamic Loading on: rows are built {Level.LoadDistanceOrDefault} ahead of the ball."
                                        : "Dynamic Loading off: the whole level is built when it starts.");
        }

        void OnLoadDistanceTyped(string text)
        {
            if (Level == null) return;
            if (int.TryParse(text, out int rows)) SetLoadDistance(rows);
            else RefreshView();
        }

        void StepLoadDistance(int step)
        {
            if (Level == null || !Level.DynamicLoading) return;
            SetLoadDistance(Level.LoadDistanceOrDefault + step);
        }

        void SetLoadDistance(int rows)
        {
            rows = Mathf.Clamp(rows, MinLoadDistance, MaxLoadDistance);
            if (rows != Level.LoadDistanceOrDefault)
            {
                Level.LoadDistance = rows;
                SetDirty(true);
                Status($"Load Distance {rows}: the game builds {rows} rows ahead of the ball (and the 3D view shows them).");
            }
            RefreshView();
        }

        // ---------------- Play (music in the editor) ----------------

        /// <summary>
        /// Play button (Space or P): the song plays from the bottom row on screen and the grid scrolls with it,
        /// a line marking where the ball is. Press again to stop. Painting still works while it plays.
        /// </summary>
        public void ToggleMusic()
        {
            if (playing) StopMusic();
            else StartMusic();
        }

        void StartMusic()
        {
            if (Level == null) return;
            if (songLoading)
            {
                Status("The song is still loading, press Play again in a moment.");
                return;
            }
            playStartSeconds = Level.TimeAtRow(FirstRow, defaultSpeed);
            playStartTime = Time.unscaledTime;
            if (song != null)
            {
                if (song.loadState == AudioDataLoadState.Unloaded) song.LoadAudioData();
                preview.clip = song;
                preview.Play();
                // Seek after Play: on a clip that is not loaded yet a seek before Play is lost and the song starts silent at 0.
                preview.time = Mathf.Clamp(playStartSeconds, 0f, Mathf.Max(0f, song.length - 0.05f));
                Status($"Playing {song.name} from row {FirstRow}. Space = stop." + (wave.Problem != null ? " " + wave.Problem : ""));
            }
            else Status($"No song for '{MusicName(Level.Music)}': the rows scroll without music. Space = stop.");
            playing = true;
            playButtonLabel.text = "Stop";
            SetIcon(musicPlayIcon, "Pause");
        }

        /// <summary>Changes a button's icon (when the project has that icon).</summary>
        static void SetIcon(Image image, string name)
        {
            var sprite = UiFactory.Icon(name);
            if (image != null && sprite != null) image.sprite = sprite;
        }

        void StopMusic()
        {
            if (!playing) return;
            playing = false;
            preview.Stop();
            playhead.enabled = false;
            if (view3D != null) view3D.SetPlayhead(-1f);
            if (playButtonLabel != null) playButtonLabel.text = "Music Play";
            SetIcon(musicPlayIcon, "Music");
        }

        /// <summary>Follows the song: the playhead row comes from the audio time, so scrolling stays on the beat.</summary>
        void UpdateMusic()
        {
            if (!playing) return;
            float seconds = song != null && preview.isPlaying ? preview.time : playStartSeconds + (Time.unscaledTime - playStartTime);
            if (song != null && !preview.isPlaying && Time.unscaledTime - playStartTime > 0.2f)
            {
                StopMusic();
                Status("The song ended.");
                return;
            }
            float row = Level.RowAtTime(seconds, defaultSpeed);
            if (row > Level.RowCount + 2f)
            {
                StopMusic();
                Status("Reached the end of the level.");
                return;
            }
            ScrollTo(Mathf.FloorToInt(row) - 2);
            if (view3D != null) view3D.SetPlayhead(row);
            playhead.enabled = !Is3D; // the 3D view draws its own line
            playhead.rectTransform.anchoredPosition = new Vector2(playhead.rectTransform.anchoredPosition.x, (row - FirstRow) * CellSize);
        }

        /// <summary>Finds the level's song (a clip in Music, or a song file) for Play and the music visual.</summary>
        void LoadSong()
        {
            string name = Level != null ? Level.Music : null;
            song = null;
            if (songRoutine != null) StopCoroutine(songRoutine);
            songRoutine = null;
            songLoading = false;
            if (string.IsNullOrEmpty(name) && music != null && music.Length > 0) name = "Music1"; // the level's number song
            if (!string.IsNullOrEmpty(name))
            {
                if (music != null)
                    foreach (var c in music)
                        if (c != null && string.Equals(c.name, name, StringComparison.OrdinalIgnoreCase)) song = c;
                string world = song == null && MusicLibrary.FindFile(name) == null ? MusicLibrary.WorldSong(name) : null;
                if (world != null && music != null) // "Massif" = Music1 ...
                    foreach (var c in music)
                        if (c != null && string.Equals(c.name, world, StringComparison.OrdinalIgnoreCase)) song = c;
                if (song == null)
                {
                    string file = MusicLibrary.FindFile(name);
                    if (file != null)
                    {
                        songRoutine = StartCoroutine(LoadSongFile(file));
                        return;
                    }
                }
            }
            wave.SetClip(song, false);
        }

        IEnumerator LoadSongFile(string path)
        {
            songLoading = true;
            using (var req = UnityWebRequestMultimedia.GetAudioClip(MusicLibrary.Url(path), MusicLibrary.TypeOf(path)))
            {
                yield return req.SendWebRequest();
                songLoading = false;
                songRoutine = null;
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Status($"Could not load {path}: {req.error}");
                    yield break;
                }
                song = DownloadHandlerAudioClip.GetContent(req);
                song.name = Path.GetFileNameWithoutExtension(path);
            }
            wave.SetClip(song, true);
        }

        void OnWaveReady() => RefreshView();

        // ---------------- Test in the game ----------------

        /// <summary>
        /// Test button (T): plays the level in the Gameplay scene from the bottom row on screen, music included.
        /// Esc in the game comes back here with nothing lost.
        /// </summary>
        public void TestInGame()
        {
            if (Level == null) return;
            StopMusic();
            grid.CancelStroke();
            EndStroke();
            if (!Level.HasTiles)
            {
                Status("Nothing to play: the level is empty.");
                return;
            }
            if (!Application.CanStreamedLevelBeLoaded(gameplayScene))
            {
                Status($"Scene '{gameplayScene}' is not in the Build Profiles scene list: run RTX ON > Build Gameplay Scene, then Open Level Editor Scene.");
                return;
            }
            // Save & Play (Adrian 2026-10-25): Play saves the level first, so nothing made before the test is lost.
            if (dirty || string.IsNullOrEmpty(savePath) || !File.Exists(savePath))
            {
                Save();
                if (dirty) return; // not saved (a broken file asks for a second press): its message stays on screen
            }
            Level.TrimEmptyEnd();
            TakeFileSettings();
            PlayTest.EditorDirty = dirty;
            PlayTest.EditorFirstRow = FirstRow;
            PlayTest.EditorSelectedId = SelectedId;
            playTestUndo = undo;
            playTestRedo = redo;
            playTestFile = openedFile;
            playTestPath = customPath;
            playTestNumber = listNumber;
            // Start Pos (if set) = that cell; otherwise the bottom row on screen, first safe lane.
            bool fromStartPos = Level.HasTestStart && Level.TestStartRow < Level.RowCount;
            int start = Mathf.Clamp(fromStartPos ? Level.TestStartRow : FirstRow, 0, Mathf.Max(0, Level.RowCount - 2));
            PlayTest.Begin(LevelJson(), start, fromStartPos ? Level.TestStartLane : -1, SceneManager.GetActiveScene().name);
            LoadingScreen.Load(gameplayScene);
        }

        void ReturnFromPlayTest()
        {
            var back = PlayTest.Json != null ? LevelData.ParseJson(PlayTest.Json) : null;
            if (back != null)
            {
                Level = back;
                CustomObstacles.AddFromLevel(Level);
                ThemeLibrary.AddFromLevel(Level);
                if (triggerPanel != null) triggerPanel.Hide();
                if (partPanel != null) partPanel.Hide();
                if (view3D != null) view3D.preview = null;
                if (playTestUndo != null) undo = playTestUndo;
                if (playTestRedo != null) redo = playTestRedo;
                FirstRow = PlayTest.EditorFirstRow;
                Select(PlayTest.EditorSelectedId);
                SetDirty(PlayTest.EditorDirty);
                restoredAutosave = null; // the level came back from the test, not from the autosave
                LoadSong();
                RefreshView();
                Status("Back from the game test.");
            }
            playTestUndo = null;
            playTestRedo = null;
            PlayTest.Clear();
        }

        /// <summary>
        /// The level as level.json. Start Pos is temporary (Adrian 2026-10-20): it goes to Play, not into the saved file,
        /// except in a LevelPreview, where it is where the menu's ball starts (Adrian 2026-10-25).
        /// </summary>
        string LevelJson(bool withTestStart = true)
        {
            // The custom obstacles the level paints go inside it, so it plays without the editor's library.
            var used = CustomObstacles.UsedBy(Level);
            Level.Customs.Clear();
            Level.Customs.AddRange(used);
            // The same for the custom themes its look and theme changes use.
            var themes = ThemeLibrary.UsedBy(Level, looks);
            Level.CustomThemes.Clear();
            foreach (var t in themes) Level.CustomThemes.Add(t.Clone());
            return Level.ToJson(Level.Name, Level.Music, Level.SpeedStart, Level.SpeedEnd, Level.SpeedRampRows, Level.ThemeChanges, Level.Props, withTestStart);
        }

        /// <summary>
        /// The start speed comes from the file as it is now, so anything typed into it while the editor was open is
        /// kept. Level Settings, music, speed, triggers, checkpoints and props (Props tab) belong to the editor.
        /// </summary>
        void TakeFileSettings(LevelData onDisk = null)
        {
            if (onDisk == null) onDisk = LevelFiles.ReadSaved(savePath, out _);
            if (onDisk == null) return;
            string songName = Level.Music;
            float speedStart = Level.SpeedStart; // the Speed box sets it
            var checkpoints = new List<int>(Level.Checkpoints);
            int startRow = Level.TestStartRow, startLane = Level.TestStartLane;
            string lookName = Level.Look; // the Look button sets it
            bool dynamic = Level.DynamicLoading;
            int loadDistance = Level.LoadDistance;
            var themes = new List<ThemeChange>(Level.ThemeChanges);   // the trigger tools edit these
            var cameras = new List<CameraChange>(Level.CameraChanges);
            var filters = new List<FilterChange>(Level.FilterChanges);
            var parts = new List<PartArgs>(Level.Parts);              // the ARGS-Setting window edits these
            var props = new List<PropPlacement>(Level.Props);         // the Props tab edits these
            var cellGroups = new List<CellGroup>(Level.CellGroups);   // the Event Groups window and GROUP edit these
            var groups = new List<EventGroup>(Level.Groups);
            // Level Settings belongs to the editor too.
            string name = Level.Name, team = Level.Team, author = Level.Author, banner = Level.Banner;
            float difficulty = Level.Difficulty, speedEnd = Level.SpeedEnd;
            int rampRows = Level.SpeedRampRows, headerGems = Level.HeaderGems, headerCrowns = Level.HeaderCrowns;
            bool invincible = Level.Invincible, startPortal = Level.StartPortal;
            var themeSettings = Level.ThemeSettings;                  // the Theme window edits these
            var customThemes = new List<CustomTheme>(Level.CustomThemes);
            Level.CopySettingsFrom(onDisk);
            Level.ThemeSettings = themeSettings;
            Level.CustomThemes.Clear();
            Level.CustomThemes.AddRange(customThemes);
            Level.Name = name;
            Level.Team = team;
            Level.Author = author;
            Level.Banner = banner;
            Level.Difficulty = difficulty;
            Level.SpeedEnd = speedEnd;
            Level.SpeedRampRows = rampRows;
            Level.HeaderGems = headerGems;
            Level.HeaderCrowns = headerCrowns;
            Level.Invincible = invincible;
            Level.StartPortal = startPortal;
            Level.ThemeChanges.Clear();
            Level.ThemeChanges.AddRange(themes);
            Level.CameraChanges.Clear();
            Level.CameraChanges.AddRange(cameras);
            Level.FilterChanges.Clear();
            Level.FilterChanges.AddRange(filters);
            Level.Parts.Clear();
            Level.Parts.AddRange(parts);
            Level.PartsChanged();
            Level.Props.Clear();
            Level.Props.AddRange(props);
            // Event Groups belong to the editor too (Adrian 2026-10-25: Save and Play took them back from the file, so they vanished).
            Level.CellGroups.Clear();
            Level.CellGroups.AddRange(cellGroups);
            Level.Groups.Clear();
            Level.Groups.AddRange(groups);
            Level.Look = lookName;
            Level.DynamicLoading = dynamic;
            Level.LoadDistance = loadDistance;
            Level.Music = songName;
            Level.SpeedStart = speedStart;
            Level.TestStartRow = startRow;
            Level.TestStartLane = startLane;
            Level.Checkpoints.Clear();
            Level.Checkpoints.AddRange(checkpoints);
        }

        // ---------------- Save / Load ----------------

        public void Save()
        {
            if (Level == null) return;
            if (!Level.HasTiles)
            {
                Status("Nothing to save: the level is empty.");
                return;
            }
            // Save changes the rows, Level Settings, music, speed, look, triggers, checkpoints, props and Start Pos: the start speed comes from the file as it is now,
            // so anything typed into it while the editor was open is kept. A file that has broken since Load
            // (say, a typo made in VS Code) gets the same two-press warning and backup as one that was broken from the start.
            var onDisk = LevelFiles.ReadSaved(savePath, out bool unreadable);
            if (unreadable && brokenPath == null)
            {
                brokenPath = savePath;
                confirmOverwrite = false;
            }
            // brokenPath is only cleared by Load or a successful Save: a file repaired after a broken Load still
            // holds the real level, while the editor holds a blank one, so it still needs the second press.
            if (brokenPath != null && !confirmOverwrite)
            {
                confirmOverwrite = true;
                string backupName = Path.GetFileName(brokenPath) + ".broken";
                Status(unreadable
                    ? $"{brokenPath} could not be read. Press Save again to replace it; the old file is kept as {backupName}"
                    : $"{brokenPath} can be read now: press Load to open it, or Save again to replace it (the old file is kept as {backupName}).");
                return;
            }

            Level.TrimEmptyEnd(); // empty rows above the last tile are not part of the level
            if (onDisk != null) TakeFileSettings(onDisk);
            // A LevelPreview keeps 10 rows, the default camera and the Day environment (no camera / theme / screen triggers).
            bool cut = EditingPreview && Level.RowCount > LevelPreviews.Rows;
            if (EditingPreview) LevelPreviews.Trim(Level);
            string json = LevelJson(EditingPreview); // a LevelPreview keeps its Start Pos: the menu's ball starts there
            bool firstSave = !EditingPreview && !File.Exists(savePath);
            try
            {
                bool backup = brokenPath != null;
                LevelFiles.Write(savePath, json, backup);
                brokenPath = null;
                confirmOverwrite = false;
                SetDirty(false);
                DeleteAutosave();
                // A new level gets its own LevelPreview: a copy of LevelPreview1 (Adrian 2026-10-25).
                string madePreview = null;
                if (firstSave)
                {
                    try { madePreview = LevelPreviews.CreateFor(savePath, DefaultPreviewJson()); }
                    catch (Exception e) { Debug.LogWarning("RTXOn: the new level's LevelPreview could not be made: " + e.Message); }
                }
                Status($"Saved {Level.RowCount} rows to {savePath}" + (backup ? " (old file kept as .broken)." : ".")
                    + (cut ? $" A LevelPreview keeps only its first {LevelPreviews.Rows} rows." : "")
                    + (madePreview != null ? $" Its LevelPreview starts as LevelPreview1 ({Path.GetFileName(madePreview)}): Load > Load LevelPreview." : "")
                    + (levelFile != openedFile ? " Level File was changed: press Load to open the new one." : ""));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Status("Save failed: " + e.Message);
            }
            RefreshView();
        }

        /// <summary>Load button: the level list (Level 1, 2, 3... with banners, your own levels, and New+).</summary>
        public void Load()
        {
            grid.CancelStroke();
            EndStroke();
            StopMusic();
            if (triggerPanel != null) triggerPanel.Hide();
            if (partPanel != null) partPanel.Hide();
            levelPicker.Open();
        }

        /// <summary>True while there are changes Save has not written.</summary>
        public bool Dirty => dirty;

        /// <summary>The file Save writes now (the level list marks it).</summary>
        public string CurrentPath => savePath;

        /// <summary>Opens a level from the level list: an asset, or a file by its path. Unsaved changes are dropped.</summary>
        public void OpenLevel(TextAsset asset, string path, int number)
        {
            previewOwner = null;
            levelFile = asset;
            customPath = asset == null ? path : null;
            listNumber = number;
            LoadLevel();
        }

        /// <summary>A built game would open an official level (1-5, Massif .. Volcano), which it can't edit (Adrian 2026-10-23).</summary>
        bool OpensOfficialInBuild() =>
            !Application.isEditor && customPath == null && levelFile != null && LevelFiles.IsOfficial(levelFile.name);

        /// <summary>Instead of an official level: your first own level (a saved file, then a Level File), else a new one.</summary>
        void OpenFirstCustomLevel()
        {
            var names = new List<string>();
            if (levelFiles != null)
                foreach (var a in levelFiles)
                    if (a != null) names.Add(a.name);
            levelFile = null;
            foreach (var path in LevelFiles.SavedOnlyFiles(names))
                if (!LevelFiles.IsOfficial(Path.GetFileNameWithoutExtension(path)))
                {
                    OpenLevel(null, path, 0);
                    return;
                }
            if (levelFiles != null)
                foreach (var a in levelFiles)
                    if (LevelFiles.IsJson(a) && !LevelFiles.IsOfficial(a.name))
                    {
                        OpenLevel(a, null, 0);
                        return;
                    }
            NewLevel(names, 0);
        }

        /// <summary>New+: an empty level "Custom N"; Save creates CustomN.json next to the other levels.</summary>
        public void NewLevel(ICollection<string> takenNames, int number)
        {
            string path = LevelFiles.NewLevelPath(takenNames, out int customNumber);
            previewOwner = null;
            levelFile = null;
            customPath = path;
            listNumber = number;
            LoadLevel();
            Level.Name = "Custom " + customNumber;
            RefreshView();
            Status($"New level \"{Level.Name}\": paint some tiles, then Save creates {saveName}.");
        }

        /// <summary>True while a LevelPreview is open (Load > Load LevelPreview), not a level.</summary>
        public bool EditingPreview => previewOwner != null;

        /// <summary>
        /// Load > Load LevelPreview: opens the 10-row LevelPreview of the level saved at <paramref name="levelPath"/>
        /// (Previews/LevelPreviewN.json). A level without one starts from its own first rows (<paramref name="levelJson"/>);
        /// Save creates the file.
        /// </summary>
        public void OpenPreview(string levelKey, string levelPath, string levelJson, int number)
        {
            string path = LevelPreviews.PathFor(levelPath);
            if (path == null)
            {
                Status($"{levelKey} has no file yet: Save it first, then open its LevelPreview.");
                return;
            }
            previewOwner = levelPath;
            levelFile = null;
            customPath = path;
            listNumber = number;
            pendingNew = File.Exists(path) ? null : LevelPreviews.FromLevel(levelJson);
            LoadLevel();
            pendingNew = null;
            string key = LevelPreviews.KeyFor(levelKey);
            Status(File.Exists(path)
                ? $"LevelPreview {key} of {levelKey}: {LevelPreviews.Rows} rows the Main Menu shows in 3D (default camera, Day). Save writes {path}."
                : $"New LevelPreview {key} from the first {LevelPreviews.Rows} rows of {levelKey}: change it, then Save creates {path}.");
        }

        /// <summary>LevelPreview1, where a new level's preview starts: its file, the one shipped with the game, else Level1's first rows.</summary>
        string DefaultPreviewJson()
        {
            try
            {
                string path = LevelPreviews.DefaultPath(LevelFiles.LevelsFolder());
                if (path != null && File.Exists(path)) return File.ReadAllText(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("RTXOn: LevelPreview1 could not be read: " + e.Message);
            }
            if (previewFiles != null)
                foreach (var a in previewFiles)
                    if (a != null && string.Equals(a.name, LevelPreviews.DefaultKey, StringComparison.OrdinalIgnoreCase)) return a.text;
            var level1 = new List<TextAsset>();
#if UNITY_EDITOR
            var project = LevelFiles.FindProjectLevels();
            if (project != null) level1.AddRange(project);
#endif
            if (levelFiles != null) level1.AddRange(levelFiles);
            foreach (var a in level1)
                if (a != null && string.Equals(a.name, "Level1", StringComparison.OrdinalIgnoreCase) && LevelFiles.IsJson(a)) return a.text;
            return null;
        }

        void LoadLevel()
        {
            StopMusic();
            confirmOverwrite = false;
            if (grid != null) grid.CancelStroke();
            openedFile = levelFile;
            savePath = customPath ?? LevelFiles.SavePath(openedFile);
            saveName = Path.GetFileName(savePath);

            string source = savePath, problem;
            var loaded = customPath != null
                ? LevelFiles.OpenPath(customPath, out problem, out brokenPath)
                : LevelFiles.Open(openedFile, out source, out problem, out brokenPath);
            string message;
            // Unsaved changes from last time (autosave): opened instead of the file, unless this level was just opened
            // from them (then opening it again means "drop them").
            string auto = AutosavePath(savePath);
            bool again = restoredAutosave == auto;
            restoredAutosave = null;
            LevelData restored = null;
            if (again) DeleteAutosave(auto);
            else if (File.Exists(auto) && (!File.Exists(savePath) || File.GetLastWriteTimeUtc(auto) > File.GetLastWriteTimeUtc(savePath)))
            {
                try { restored = LevelData.ParseJson(File.ReadAllText(auto)); }
                catch (Exception e) { Debug.LogWarning($"RTXOn: autosave {auto} could not be read: {e.Message}"); }
            }
            if (restored != null)
            {
                loaded = restored;
                restoredAutosave = auto;
                problem = null;
                message = $"Restored your unsaved changes (autosave {File.GetLastWriteTime(auto):HH:mm}, {restored.RowCount} rows). " +
                          $"Save keeps them in {saveName}; to drop them, open this level again from Load.";
            }
            else if (loaded != null)
                message = (problem != null ? problem + " " : "") + $"Opened {source} ({loaded.RowCount} rows).";
            else if (problem != null)
                message = problem + " Started a new level instead.";
            else if (customPath == null && levelFile != null && !LevelFiles.IsJson(levelFile))
                message = $"{levelFile.name} is not a level.json (convert it with RTX ON > Convert Level.txt to level.json). Started a new level; Save creates {saveName}.";
            else
                message = $"No level file yet: started a new level. Save creates {saveName}.";
            if (problem != null) Debug.LogWarning("RTXOn: " + problem);

            bool fromPending = loaded == null && pendingNew != null;
            Level = loaded ?? pendingNew ?? LevelData.CreateEmpty("New Level", NewLevelRows);
            CustomObstacles.AddFromLevel(Level); // the custom obstacles the level carries
            ThemeLibrary.AddFromLevel(Level);    // and the custom themes
            if (themePanel != null) themePanel.Hide();
            if (customThemePanel != null) customThemePanel.Hide();
            if (spriteMaker != null) spriteMaker.Hide();
            if (currentGroup == TileGroup.Obstacle) RebuildCustomButtons();
            if (currentGroup == TileGroup.Props) RebuildPropButtons();
            RefreshObjectNames(); // the level's look names the Object tab's buttons
            if (customPanel != null) customPanel.Hide();
            if (triggerPanel != null) triggerPanel.Hide();
            if (partPanel != null) partPanel.Hide();
            if (cellPanel != null) cellPanel.Hide();
            if (eventsPanel != null) eventsPanel.Hide();
            if (view3D != null) view3D.preview = null;
            if (loaded == null && !fromPending)
            {
                // New levels (Adrian 2026-10-20): speed 7, Massif's song, 10 gems, 5 stars, 120 rows to paint on.
                Level.SpeedStart = LevelData.DefaultSpeed;
                Level.Music = NewLevelMusic;
                Level.HeaderGems = NewLevelGems;
                Level.Difficulty = LevelData.DefaultDifficulty;
            }
            LoadSong();
            undo.Clear();
            redo.Clear();
            stroke = null;
            FirstRow = 0;
            SetDirty(restored != null || fromPending);
            RefreshView();
            Status(message);
        }

        // ---------------- Autosave ----------------

        /// <summary>Where the unsaved changes of the level saved at <paramref name="path"/> are kept.</summary>
        static string AutosavePath(string path)
        {
            // FNV-1a of the full path, so two Level1.json in different folders keep different autosaves.
            uint h = 2166136261;
            foreach (char c in path ?? "") h = (h ^ c) * 16777619;
            string name = Path.GetFileNameWithoutExtension(path ?? "Level");
            return Path.Combine(Application.persistentDataPath, "Autosave", $"{name}_{h:x8}.json");
        }

        void Autosave()
        {
            autosaveAt = -1f;
            if (Level == null || string.IsNullOrEmpty(savePath) || !Level.HasTiles) return;
            try
            {
                string path = AutosavePath(savePath);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, LevelJson(EditingPreview));
            }
            catch (Exception e)
            {
                Debug.LogWarning("RTXOn: autosave failed: " + e.Message);
            }
        }

        void DeleteAutosave(string path = null)
        {
            autosaveAt = -1f;
            path = path ?? (string.IsNullOrEmpty(savePath) ? null : AutosavePath(savePath));
            if (path == restoredAutosave) restoredAutosave = null;
            try { if (path != null && File.Exists(path)) File.Delete(path); }
            catch (Exception e) { Debug.LogWarning("RTXOn: could not delete the autosave: " + e.Message); }
        }

        // ---------------- Scrolling ----------------

        public void ScrollBy(int rows) => ScrollTo(FirstRow + rows);

        public void ScrollTo(int row)
        {
            if (Level == null) return;
            int clamped = Mathf.Clamp(row, 0, MaxFirstRow);
            if (clamped == FirstRow) return;
            // Wheel (2 rows) and W/S (1 row) keep painting the rows scrolled past; bigger jumps start a new line.
            if (grid != null && Mathf.Abs(clamped - FirstRow) > 2) grid.BreakLine();
            FirstRow = clamped;
            RefreshView();
        }

        /// <summary>Shows the last rows of the level with some empty rows above them.</summary>
        public void ScrollToEnd()
        {
            if (Level != null) ScrollTo(Level.RowCount - VisibleRows / 2);
        }

        void RefreshView()
        {
            if (Level == null || grid == null) return;
            FirstRow = Mathf.Clamp(FirstRow, 0, MaxFirstRow);
            grid.Refresh();

            // Changing maxValue can move the slider and fire its callback; ignore that while syncing.
            syncingSlider = true;
            scrollSlider.maxValue = MaxFirstRow;
            scrollSlider.value = FirstRow;
            syncingSlider = false;

            rowsLabel.text = $"Showing rows {FirstRow} to {FirstRow + VisibleRows - 1}\nLevel length: {Level.RowCount} rows";
            RefreshSettings();
            wave.Draw(Level, FirstRow, defaultSpeed);
            if (view3D != null) view3D.Refresh();
            if (cellPanel != null && cellPanel.IsOpen) cellPanel.Refresh();
            placed.Clear(); // both views have seen them
        }

        /// <summary>True for a cell just painted (not loaded, scrolled to or undone): the grid and the 3D view animate it once.</summary>
        public bool WasJustPlaced(int row, int lane) => placed.Contains((long)row * LevelData.Width + lane);

        /// <summary>
        /// Play Mover (3D view, key M): the movers on screen move like in the game, each touch mover with the auto
        /// movers it sets off, then after 3 seconds everything is back where it was painted.
        /// </summary>
        public void PlayMover()
        {
            if (view3D == null) return;
            if (!view3D.Visible) Toggle3D();
            int moves = view3D.PlayMovers();
            Status(moves > 0 ? $"Play Mover: {moves} platform(s) move, back to the start after {EditorTrackView.MoverPreviewSeconds:0} seconds."
                             : "Play Mover: no touch mover (yellow arrow) on screen. Scroll to one and press again.");
        }

        void Update()
        {
            if (Level == null) return;
            if (dirty && autosaveAt >= 0f && Time.unscaledTime >= autosaveAt) Autosave();
            UpdateMusic();
            UpdateFilterPreview();
            if (Typing()) return; // keys go to the speed box
            if (KeyPressed(KeyCode.Escape))
            {
                if (fileChooser != null && fileChooser.IsOpen) fileChooser.Close();
                else if (spriteMaker != null && spriteMaker.IsOpen) spriteMaker.Close();
                else if (customThemePanel != null && customThemePanel.IsOpen) customThemePanel.Close();
                else if (themePanel != null && themePanel.IsOpen) themePanel.Close();
                else if (eventsPanel != null && eventsPanel.IsOpen) eventsPanel.Close();
                else if (customPanel != null && customPanel.IsOpen) customPanel.Close();
                else if (levelPicker != null && levelPicker.IsOpen) levelPicker.Close();
                else if (partPanel != null && partPanel.IsOpen) partPanel.Close();
                else if (cellPanel != null && cellPanel.IsOpen) cellPanel.Close();
                else if (levelSettings != null && levelSettings.IsOpen) levelSettings.Close();
                else if (triggerPanel != null && triggerPanel.IsOpen) triggerPanel.Close();
                else BackToMenu();
                return;
            }
            if (levelSettings != null && levelSettings.IsOpen) return; // the window is in front: no painting keys
            if (levelPicker != null && levelPicker.IsOpen) return;
            if (customPanel != null && customPanel.IsOpen) return;
            if (themePanel != null && themePanel.IsOpen) return;
            if (eventsPanel != null && eventsPanel.IsOpen) return;
            if (customThemePanel != null && customThemePanel.IsOpen) return;
            if (spriteMaker != null && spriteMaker.IsOpen) return;
            if (fileChooser != null && fileChooser.IsOpen) return;
            if (CtrlHeld() && KeyPressed(KeyCode.Z))
            {
                if (ShiftHeld()) Redo(); else Undo();
            }
            if (CtrlHeld() && KeyPressed(KeyCode.Y)) Redo();
            if (KeyPressed(KeyCode.M)) PlayMover();
            if (KeyPressed(KeyCode.E)) Select(TilePalette.EditTool);
            if (KeyPressed(KeyCode.C) && !CtrlHeld()) Select(TilePalette.CopyTool);
            if (KeyPressed(KeyCode.Space) || KeyPressed(KeyCode.P)) ToggleMusic();
            if (KeyPressed(KeyCode.T)) TestInGame();
            if (KeyPressed(KeyCode.V)) Toggle3D();
            if (KeyPressed(KeyCode.UpArrow) || KeyPressed(KeyCode.W)) ScrollBy(1);
            if (KeyPressed(KeyCode.DownArrow) || KeyPressed(KeyCode.S)) ScrollBy(-1);
            if (KeyPressed(KeyCode.PageUp)) ScrollBy(VisibleRows - 1);
            if (KeyPressed(KeyCode.PageDown)) ScrollBy(-(VisibleRows - 1));
            if (KeyPressed(KeyCode.Home)) ScrollTo(0);
            if (KeyPressed(KeyCode.End)) ScrollToEnd();
        }

        static bool Typing()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            var field = selected != null ? selected.GetComponent<InputField>() : null;
            return field != null && field.isFocused;
        }

        /// <summary>Key presses through either input system (Unity 6 uses the new Input System by default).</summary>
        static bool KeyPressed(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return false;
            switch (key)
            {
                case KeyCode.UpArrow: return k.upArrowKey.wasPressedThisFrame;
                case KeyCode.DownArrow: return k.downArrowKey.wasPressedThisFrame;
                case KeyCode.W: return k.wKey.wasPressedThisFrame;
                case KeyCode.S: return k.sKey.wasPressedThisFrame;
                case KeyCode.PageUp: return k.pageUpKey.wasPressedThisFrame;
                case KeyCode.PageDown: return k.pageDownKey.wasPressedThisFrame;
                case KeyCode.Home: return k.homeKey.wasPressedThisFrame;
                case KeyCode.End: return k.endKey.wasPressedThisFrame;
                case KeyCode.P: return k.pKey.wasPressedThisFrame;
                case KeyCode.T: return k.tKey.wasPressedThisFrame;
                case KeyCode.V: return k.vKey.wasPressedThisFrame;
                case KeyCode.Space: return k.spaceKey.wasPressedThisFrame;
                case KeyCode.Escape: return k.escapeKey.wasPressedThisFrame;
                case KeyCode.Z: return k.zKey.wasPressedThisFrame;
                case KeyCode.Y: return k.yKey.wasPressedThisFrame;
                case KeyCode.M: return k.mKey.wasPressedThisFrame;
                case KeyCode.E: return k.eKey.wasPressedThisFrame;
                case KeyCode.C: return k.cKey.wasPressedThisFrame;
                default: return false;
            }
#else
            return Input.GetKeyDown(key);
#endif
        }

        static bool CtrlHeld()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            return k != null && (k.ctrlKey.isPressed || k.leftCommandKey.isPressed || k.rightCommandKey.isPressed);
#else
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
#endif
        }

        static bool ShiftHeld()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            return k != null && k.shiftKey.isPressed;
#else
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#endif
        }

        // ---------------- UI ----------------

        void SetDirty(bool value)
        {
            dirty = value;
            confirmExit = false;
            if (!dirty) autosaveAt = -1f;
            else if (autosaveAt < 0f) autosaveAt = Time.unscaledTime + AutosaveSeconds;
            // The * comes first so it shows even when a long name does not fit.
            if (fileLabel != null) fileLabel.text = dirty ? $"* {saveName}  (not saved)" : saveName;
        }

        void Status(string message)
        {
            if (statusLabel != null) statusLabel.text = message;
        }

        /// <summary>The model the open level's look puts on an Object tile, by a readable name ("10_enemyTreeSmall1" = "TreeSmall1"), or null.</summary>
        string LookModelName(int id)
        {
            if (id <= 0 || CustomObstacle.IsCustomId(id) || TilePalette.GroupOf(id) != TileGroup.Obstacle) return null;
            var look = CurrentLook();
            var model = look != null ? look.ObstacleModel(id) : null;
            if (model == null) return null;
            string n = System.Text.RegularExpressions.Regex.Replace(model.name, @"^\d+_", "");
            if (n.StartsWith("enemy", StringComparison.OrdinalIgnoreCase) && n.Length > 5) n = n.Substring(5);
            return n;
        }

        /// <summary>A tile's name, with the look's model first for Object tiles ("Angklung (Riser)").</summary>
        string DisplayName(int id)
        {
            string model = LookModelName(id);
            return model != null ? $"{model} ({TilePalette.Name(id)})" : TilePalette.Name(id);
        }

        /// <summary>Object tab buttons: the look's model name on top, the kind of obstacle under it.</summary>
        void RefreshObjectNames()
        {
            foreach (var kv in paletteNames)
            {
                if (kv.Value == null) continue;
                string model = LookModelName(kv.Key);
                string kind = TilePalette.Name(kv.Key);
                kv.Value.text = model != null ? model + "\n" + kind : kind;
            }
        }

        void ShowGroup(TileGroup group)
        {
            group = TilePalette.PageOf(group); // Fragile and Jump are on the Tiles page
            currentGroup = group;
            if (group == TileGroup.Obstacle) RefreshObjectNames();
            if (group == TileGroup.Props) RebuildPropButtons();
            RebuildCustomButtons(); // custom obstacles sit in the Object, Tiles or Collect tab
            if (group == TileGroup.Collection) RebuildCollectionButtons();
            bool tiles = TilePalette.IsTilesPage(group);
            if (tiles) lastTilesPage = group;
            foreach (var kv in paletteButtons) kv.Value.SetActive(TilePalette.PageOf(kv.Key) == group);
            foreach (var kv in groupTabs)
            {
                kv.Value.color = kv.Key == group ? AccentColor : ButtonColor;
                kv.Value.gameObject.SetActive(tiles); // the pages show under the Tiles tab only
            }
            foreach (var b in propSettings) b.SetActive(group == TileGroup.Props); // Place / Turn / Size: Props tab only
            if (group == TileGroup.Props && propModels.Count == 0)
                Status("No props yet: add models to the theme's Models list with Prop ticked (or RTX ON > Add Selected Models As Props), then open this tab again.");
            if (group == TileGroup.Collection && collectionButtons.Count == 0)
                Status("Collection is empty: press the star (side panel), click the tiles and objects you want, then press the star again.");
            int main = tiles ? 0 : group == TileGroup.Collect ? 1 : group == TileGroup.Obstacle ? 2 : group == TileGroup.Collection ? 3
                     : group == TileGroup.Triggers ? 4 : -1;
            for (int i = 0; i < layerTabs.Length; i++)
                if (layerTabs[i] != null) layerTabs[i].color = i == main ? AccentColor : ButtonColor;
            // Lay out this tab's tiles now, so the scroll position and the scroll bar fit it straight away.
            LayoutRebuilder.ForceRebuildLayoutImmediate(paletteScroll.content);
            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)paletteScroll.transform);
            paletteScroll.verticalNormalizedPosition = 1f; // back to the top of the list
        }

        static void EnsureCameraAndEventSystem()
        {
            if (FindFirstObjectByType<Camera>() == null)
            {
                var cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = BackgroundColor;
            }

            // Play's music needs a listener: without one Unity plays every sound silently.
            if (FindFirstObjectByType<AudioListener>() == null)
            {
                var cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
                cam.gameObject.AddComponent<AudioListener>();
            }
            AudioListener.pause = false; // a paused game test must not leave the editor silent
            if (AudioListener.volume <= 0f) AudioListener.volume = 1f;

            // Buttons and the grid need an EventSystem with the input module that matches the project's input system.
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
                go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
#else
                go.AddComponent<StandaloneInputModule>();
#endif
            }
        }

        void BuildUi()
        {
            var canvas = UiFactory.MakeCanvas("Level Editor UI", new Vector2(1920f, 1080f));
            canvas.transform.SetParent(transform, false);
            var root = canvas.transform;

            BuildTopBar(root);
            BuildPalette(root);
            BuildGrid(root);
            BuildSidePanel(root);
            levelSettings = LevelSettingsPanel.Build(this, root);
            levelPicker = LevelPickerPanel.Build(this, root);
            customPanel = CustomObstaclePanel.Build(this, root);
            themePanel = ThemeSettingsPanel.Build(this, root);
            eventsPanel = EventGroupsPanel.Build(this, root);
            customThemePanel = CustomThemePanel.Build(this, root);
            spriteMaker = SpriteMakerPanel.Build(this, root);
            fileChooser = FileChooser.Build(root); // in front of every window
            FileChooser.Notify = Status; // Android Import / Export answer later, after the phone's file window
        }

        void BuildTopBar(Transform root)
        {
            var bar = UiFactory.MakePanel(root, "Top Bar", BarColor).rectTransform;
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = Vector2.one;
            bar.pivot = new Vector2(0.5f, 1f);
            bar.sizeDelta = new Vector2(0f, TopBarHeight);
            bar.anchoredPosition = Vector2.zero;

            string[] labels = { "Load", "Save", "Undo", "Redo", "Music Play", "Play", "3D", "Theme", "Events" };
            UnityEngine.Events.UnityAction[] actions = { Load, Save, Undo, Redo, ToggleMusic, TestInGame, Toggle3D, OpenThemeSettings, () => OpenEventGroups() };
            // Adrian's icons (UI/Resources/Icons) left of the names; Undo / Redo have none.
            string[] iconNames = { "Map", "Check", null, null, "Music", "PlayFilled", "Camera", "Theme", "Spin" };
            for (int i = 0; i < labels.Length; i++)
            {
                var color = i == 1 ? AccentColor : i == 4 ? PlayColor : ButtonColor;
                var b = UiFactory.MakeButton(bar, labels[i], color, actions[i]);
                UiFactory.Place((RectTransform)b.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(Margin + i * 142f, 0f), new Vector2(132f, 66f));
                var text = b.GetComponentInChildren<Text>();
                var icon = UiFactory.AddIcon(b, iconNames[i], 30f);
                if (i == 4) musicPlayIcon = icon;
                if (i == 6) view3DIcon = icon;
                if (i == 4)
                {
                    playButtonLabel = text;
                    text.resizeTextForBestFit = true; // "Music Play" fits the button
                    text.resizeTextMinSize = 14;
                    text.resizeTextMaxSize = 28;
                }
                if (i == 6) view3DButtonLabel = text;
                if (i == 7)
                {
                    lookButtonLabel = text;
                    text.resizeTextForBestFit = true; // long theme names get smaller
                    text.resizeTextMinSize = 12;
                    text.resizeTextMaxSize = 26;
                }
            }

            // Exit: back to the Main Menu, at the right end of the bar.
            const float ExitWidth = 132f;
            var exit = UiFactory.MakeButton(bar, "Exit", ExitColor, BackToMenu);
            UiFactory.AddIcon(exit, "Close", 28f);
            UiFactory.Place((RectTransform)exit.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-Margin, 0f), new Vector2(ExitWidth, 66f));

            const float FileLabelX = Margin + 9 * 142f + 20f;
            const float FileLabelWidth = 300f;
            fileLabel = UiFactory.MakeLabel(bar, "", 34, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            UiFactory.Place(fileLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(FileLabelX, 0f), new Vector2(FileLabelWidth, 70f));
            fileLabel.resizeTextForBestFit = true; // long file names shrink instead of being cut off
            fileLabel.resizeTextMinSize = 18;
            fileLabel.resizeTextMaxSize = 34;

            // The status text fills the rest of the bar, from after the file name to the Exit button.
            statusLabel = UiFactory.MakeLabel(bar, "", 22, TextAnchor.MiddleRight, SoftText);
            var status = statusLabel.rectTransform;
            status.anchorMin = new Vector2(0f, 0.5f);
            status.anchorMax = new Vector2(1f, 0.5f);
            status.offsetMin = new Vector2(FileLabelX + FileLabelWidth + 20f, -40f);
            status.offsetMax = new Vector2(-(Margin + ExitWidth + 20f), 40f);
        }

        void BuildPalette(Transform root)
        {
            var panel = UiFactory.MakePanel(root, "Palette Panel", PanelColor).rectTransform;
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 0.5f);
            panel.offsetMin = new Vector2(Margin, Margin);
            panel.offsetMax = new Vector2(Margin + LeftPanelWidth, -(TopBarHeight + Margin));

            // Layer tabs: Tiles (the floor), Collect (pickups), Object (obstacles), Collection (the favourites, with a star),
            // Triggers. (The Props tab is gone, Adrian 2026-10-23: Custom Obstacles replace it. Props already in a level
            // still load, show and save; Erase Object still takes them off.)
            string[] layerNames = { "Tiles", "Collect", "Object", "Collection", "Triggers" };
            UnityEngine.Events.UnityAction[] layerActions =
            {
                () => ShowGroup(lastTilesPage), () => ShowGroup(TileGroup.Collect), () => ShowGroup(TileGroup.Obstacle),
                () => ShowGroup(TileGroup.Collection), () => ShowGroup(TileGroup.Triggers),
            };
            const float TabStep = 96f;
            for (int i = 0; i < layerNames.Length; i++)
            {
                var tab = UiFactory.MakeButton(panel, layerNames[i], ButtonColor, layerActions[i], 19);
                UiFactory.Place((RectTransform)tab.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(Margin + i * TabStep, -Margin), new Vector2(TabStep - 4f, 56f));
                layerTabs[i] = (Image)tab.targetGraphic;
                var label = tab.GetComponentInChildren<Text>();
                label.resizeTextForBestFit = true; // "Collection" and "Triggers" fit the narrower tabs
                label.resizeTextMinSize = 11;
                label.resizeTextMaxSize = 19;
                if (layerNames[i] == "Collection")
                {
                    // A gold star over the top of the tab, so the favourites stand out.
                    var star = UiFactory.MakePanel(tab.transform, "Star", TilePalette.CrownColor);
                    star.sprite = UiFactory.Star;
                    star.raycastTarget = false;
                    UiFactory.Place(star.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -2f), new Vector2(22f, 22f));
                    label.rectTransform.offsetMin = new Vector2(2f, 0f);
                    label.rectTransform.offsetMax = new Vector2(-2f, -10f);
                }
            }
            // The Tiles tab's pages, in a second row.
            TileGroup[] pages = { TileGroup.Floor, TileGroup.Moving, TileGroup.Animated }; // Floor = "Tiles": floor, fragile and jump
            for (int i = 0; i < pages.Length; i++)
            {
                var g = pages[i];
                var tab = UiFactory.MakeButton(panel, TilePalette.GroupName(g), ButtonColor, () => ShowGroup(g), 19);
                UiFactory.Place((RectTransform)tab.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(Margin + i * 96f, -(Margin + 64f)), new Vector2(91f, 50f));
                groupTabs[g] = (Image)tab.targetGraphic;
            }
            // The Props tab's settings for the next props, in the same row.
            UnityEngine.Events.UnityAction[] propActions = { CyclePropPlace, CyclePropTurn, CyclePropSize };
            for (int i = 0; i < propActions.Length; i++)
            {
                var b = UiFactory.MakeButton(panel, "", ButtonColor, propActions[i], 19);
                UiFactory.Place((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(Margin + i * 160f, -(Margin + 64f)), new Vector2(154f, 50f));
                var label = b.GetComponentInChildren<Text>();
                if (i == 0) propPlaceLabel = label;
                else if (i == 1) propTurnLabel = label;
                else propSizeLabel = label;
                propSettings.Add(b.gameObject);
                b.gameObject.SetActive(false);
            }
            UpdatePropLabels();

            // Four columns of 104 fit next to the scroll bar, which shows when a tab has more tiles than fit (Enemies).
            paletteScroll = UiFactory.MakeGridList(panel, new Vector2(PaletteCell, PaletteCell), 8f, 4, AccentColor, out var content);
            paletteContent = content;
            UiFactory.Stretch((RectTransform)paletteScroll.transform, Margin, Margin + EraserHeight + Margin, Margin, Margin + 2 * 64f + 8f);

            foreach (var tile in TilePalette.All)
            {
                if (!TilePalette.InPalette(tile.id)) continue; // edges and the old movers 37-60: only kept for old levels
                var button = MakePaletteButton(content, tile);
                paletteButtons.Add(new KeyValuePair<TileGroup, GameObject>(tile.group, button));
            }

            // The two erasers, along the bottom edge on every tab.
            int count = TilePalette.Erasers.Length;
            float width = (LeftPanelWidth - (count + 1) * Margin) / count;
            for (int i = 0; i < TilePalette.Erasers.Length; i++)
                MakeEraserButton(panel, TilePalette.Erasers[i], new Vector2(Margin + i * (width + Margin), Margin), new Vector2(width, EraserHeight));
        }

        const float EraserHeight = 64f;

        /// <summary>A wide eraser (or Edit / Copy) button with a selection frame, like the palette's tile buttons.</summary>
        void MakeEraserButton(RectTransform panel, PaletteTile tile, Vector2 position, Vector2 size)
        {
            int id = tile.id;
            var cell = UiFactory.MakeRect(panel, tile.name);
            UiFactory.Place(cell, Vector2.zero, Vector2.zero, position, size);
            var frame = UiFactory.MakePanel(cell, "Selected Frame", Color.white);
            UiFactory.Stretch(frame.rectTransform);
            frame.enabled = id == SelectedId || (id == TilePalette.CopyTool && SelectedId == TilePalette.PasteTool);
            AddFrame(paletteFrames, id, frame);
            if (id == TilePalette.CopyTool) AddFrame(paletteFrames, TilePalette.PasteTool, frame); // the Copy brush keeps Copy framed
            bool eraser = id == 0 || id == TilePalette.EraseObjectTool;
            var color = eraser ? Color.Lerp(TilePalette.ColorOf(tile.group), Color.black, 0.35f) // green = Tiles, red = Object
                               : Color.Lerp(TilePalette.ColorOf(id), Color.black, 0.3f);
            string iconName = id == 0 ? "Trash" : id == TilePalette.EraseObjectTool ? "TrashFilled" : id == TilePalette.EditTool ? "InputField" : null;
            bool hasIcon = UiFactory.Icon(iconName) != null;
            var b = UiFactory.MakeButton(cell, eraser && !hasIcon ? "X  " + tile.name : tile.name, color, () => Select(id), 20);
            UiFactory.Stretch((RectTransform)b.transform, FrameWidth, FrameWidth, FrameWidth, FrameWidth);
            UiFactory.AddIcon(b, iconName, 30f);
            var text = b.GetComponentInChildren<Text>();
            text.resizeTextForBestFit = true; // four buttons share the row
            text.resizeTextMinSize = 12;
            text.resizeTextMaxSize = 20;
        }

        GameObject MakePaletteButton(Transform parent, PaletteTile tile)
        {
            int id = tile.id;
            var cell = UiFactory.MakeRect(parent, $"Tile {id}");
            // A bare RectTransform does not tell the grid when it is shown or hidden; a LayoutElement does.
            cell.gameObject.AddComponent<LayoutElement>();

            // Selection frame: a white square behind the button, a little bigger than it, shown for the selected tile.
            var frame = UiFactory.MakePanel(cell, "Selected Frame", Color.white);
            UiFactory.Stretch(frame.rectTransform);
            frame.enabled = id == SelectedId;
            AddFrame(paletteFrames, id, frame);

            var icon = IconFor(id);
            var image = UiFactory.MakePanel(cell, "Button", icon != null ? Color.white : TilePalette.ColorOf(id), true);
            UiFactory.Stretch(image.rectTransform, FrameWidth, FrameWidth, FrameWidth, FrameWidth);
            image.sprite = icon;
            image.preserveAspect = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = UiFactory.NoNavigation;
            button.onClick.AddListener(() => PaletteClick(id));

            var textColor = icon != null ? Color.white : TilePalette.TextColorOf(id);
            var number = UiFactory.MakeLabel(image.transform, TilePalette.Label(id), icon != null ? 20 : 38, icon != null ? TextAnchor.UpperLeft : TextAnchor.MiddleCenter, textColor, FontStyle.Bold);
            UiFactory.Stretch(number.rectTransform, 6f, 40f, 6f, 4f);
            // Tools without a drawn icon: Adrian's picture in the middle (+ = new custom obstacle, the Check Point badge).
            var toolIcon = icon == null ? UiFactory.Icon(id == TilePalette.NewCustomTool ? "Add" : id == TilePalette.CheckpointTool ? "CheckPoint" : null) : null;
            if (toolIcon != null)
            {
                number.enabled = false;
                var picture = UiFactory.MakePanel(image.transform, "Icon", id == TilePalette.CheckpointTool ? Color.white : textColor);
                picture.sprite = toolIcon;
                picture.preserveAspect = true;
                picture.raycastTarget = false;
                UiFactory.Stretch(picture.rectTransform, 10f, 22f, 10f, 6f);
            }
            var name = UiFactory.MakeLabel(image.transform, tile.name, 15, TextAnchor.LowerCenter, textColor);
            UiFactory.Stretch(name.rectTransform, 4f, 4f, 4f, 50f);
            name.resizeTextForBestFit = true; // long names ("Bin mini off right fragile") get smaller
            name.resizeTextMinSize = 10;
            name.resizeTextMaxSize = 15;
            if (tile.group == TileGroup.Obstacle && id > 0) paletteNames[id] = name;

            // A gold star in the top-left corner on favourites (Collection tab).
            if (CanFavorite(id))
            {
                var star = UiFactory.MakePanel(image.transform, "Favorite", TilePalette.CrownColor);
                star.sprite = UiFactory.Star;
                star.raycastTarget = false;
                UiFactory.Place(star.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(3f, -3f), new Vector2(24f, 24f));
                star.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.7f);
                star.enabled = Marked.Contains(id);
                AddFrame(favoriteBadges, id, star);
            }

            // Big ◄ / ► in the top-right corner: which side the tile faces or makes deadly, readable at a glance.
            int side = TilePalette.SideOf(id);
            if (side != 0)
            {
                var arrow = UiFactory.MakeLabel(image.transform, side < 0 ? TilePalette.LeftArrow : TilePalette.RightArrow, 30, TextAnchor.UpperRight, textColor, FontStyle.Bold);
                UiFactory.Stretch(arrow.rectTransform, 4f, 40f, 6f, 2f);
                var outline = arrow.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);
            }
            return cell.gameObject;
        }

        void BuildGrid(Transform root)
        {
            float gridWidth = RowLabelWidth + LevelData.Width * CellSize;
            var gridRoot = UiFactory.MakeRect(root, "Grid");
            // Centred between the two side panels, below the top bar.
            UiFactory.Place(gridRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2((LeftPanelWidth - RightPanelWidth) * 0.5f - (SliderWidth + 16f) * 0.5f, -TopBarHeight * 0.5f),
                new Vector2(gridWidth, VisibleRows * CellSize));
            grid = gridRoot.gameObject.AddComponent<LevelGridView>();
            grid.Build(this, CellSize, RowLabelWidth);

            // 3D view over the cells (button 3D or V), with Tilt and Zoom in its top-right corner.
            // 3D view: wider than the grid (over the music strip and the space left of it), the music drawn inside it.
            const float WideLeft = 235f;
            view3D = gridRoot.gameObject.AddComponent<EditorTrackView>();
            view3D.Build(this, gridRoot, new Vector2(-WideLeft, 0f), new Vector2(WideLeft + gridWidth, VisibleRows * CellSize));
            BuildView3DControls(gridRoot);
            triggerPanel = TriggerPanel.Build(this, root, gridRoot.anchoredPosition + new Vector2(0f, 60f));
            // The Edit tool's window starts left of the cells (over the music strip), so the cells stay clickable; drag its title to move it.
            cellPanel = CellPanel.Build(this, root, gridRoot.anchoredPosition + new Vector2(-430f, 60f));
            partPanel = PartPanel.Build(this, root, gridRoot.anchoredPosition + new Vector2(0f, 40f));
            BuildMarqueeBar(root, gridRoot.anchoredPosition + new Vector2(-gridWidth * 0.5f - 130f, 40f));

            scrollSlider = UiFactory.MakeVerticalSlider(gridRoot, new Color(0.2f, 0.17f, 0.21f), AccentColor);
            UiFactory.Place((RectTransform)scrollSlider.transform, new Vector2(1f, 0f), Vector2.zero, new Vector2(16f, 0f), new Vector2(SliderWidth, VisibleRows * CellSize));
            scrollSlider.onValueChanged.AddListener(v =>
            {
                if (!syncingSlider) ScrollTo(Mathf.RoundToInt(v));
            });

            // Music visual: the song's loudness beside the row numbers, one row of the grid = one row of the level.
            const float WaveWidth = 100f, WaveGap = 16f;
            var waveRect = UiFactory.MakeRect(gridRoot, "Music Visual");
            UiFactory.Place(waveRect, Vector2.zero, Vector2.zero, new Vector2(-(WaveWidth + WaveGap), 0f), new Vector2(WaveWidth, VisibleRows * CellSize));
            wave = waveRect.gameObject.AddComponent<MusicWaveView>();
            wave.Build(VisibleRows);
            wave.Ready = OnWaveReady;

            // Playhead: where the ball is while Play runs.
            playhead = UiFactory.MakePanel(gridRoot, "Playhead", new Color(1f, 0.85f, 0.3f, 0.95f));
            UiFactory.Place(playhead.rectTransform, Vector2.zero, new Vector2(0f, 0.5f), new Vector2(-(WaveWidth + WaveGap), 0f),
                new Vector2(gridWidth + WaveWidth + WaveGap, 5f));
            playhead.enabled = false;
        }

        void BuildView3DControls(RectTransform gridRoot)
        {
            var panel = UiFactory.MakePanel(gridRoot, "3D Controls", new Color(0.08f, 0.06f, 0.09f, 0.75f), true).rectTransform;
            const float W = 250f, H = 160f;
            UiFactory.Place(panel, Vector2.zero, Vector2.zero,
                new Vector2(RowLabelWidth + LevelData.Width * CellSize - W - 6f, VisibleRows * CellSize - H - 6f), new Vector2(W, H));
            Slider3D(panel, "Tilt", 15f, 90f, view3D.tilt, -10f, v => { view3D.tilt = v; view3D.Refresh(); });
            Slider3D(panel, "Zoom", 4f, 30f, view3D.zoom, -55f, v => { view3D.zoom = v; view3D.Refresh(); });
            // Play Mover: the movers on screen move for 3 seconds, then go back.
            var mover = UiFactory.MakeButton(panel, "Play Mover", PlayColor, PlayMover, 22);
            UiFactory.Place((RectTransform)mover.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -100f), new Vector2(W - 20f, 50f));
            view3DControls = panel.gameObject;
            view3DControls.SetActive(false);
        }

        void Slider3D(RectTransform panel, string name, float min, float max, float value, float y, UnityEngine.Events.UnityAction<float> onChange)
        {
            var label = UiFactory.MakeLabel(panel, name, 20, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            UiFactory.Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, y), new Vector2(70f, 36f));
            var slider = UiFactory.MakeHorizontalSlider(panel, new Color(0.2f, 0.17f, 0.21f), AccentColor, Color.white, min, max);
            UiFactory.Place((RectTransform)slider.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(80f, y - 4f), new Vector2(155f, 28f));
            slider.value = value;
            slider.onValueChanged.AddListener(onChange);
        }

        // ---------------- 3D view and looks ----------------

        /// <summary>True while the 3D view shows: the grid paints the cell under the pointer in the picture.</summary>
        public bool Is3D => view3D != null && view3D.Visible;
        public EditorTrackView View3D => view3D;

        /// <summary>3D button (V): the track in 3D with each row's look, still paintable. Press again for the flat grid.</summary>
        public void Toggle3D()
        {
            if (view3D == null) return;
            bool on = !view3D.Visible;
            grid.CancelStroke();
            EndStroke();
            view3D.Show(on);
            view3DControls.SetActive(on);
            grid.Show3D(on);
            wave.gameObject.SetActive(!on); // drawn along the track in 3D
            if (on) playhead.enabled = false;
            view3DButtonLabel.text = on ? "2D" : "3D";
            SetIcon(view3DIcon, on ? "Map" : "Camera");
            RefreshView();
            Status(on ? "3D view: paint on the track, wheel / W S to scroll, Tilt and Zoom top right. V = back to 2D." : "2D grid.");
        }

        // ---------------- Triggers (theme / camera) ----------------

        public ThemeChange ThemeTriggerAt(int row) => Level?.ThemeChanges.Find(c => c != null && c.row == row);
        public CameraChange CameraTriggerAt(int row) => Level?.CameraChanges.Find(c => c != null && c.row == row);
        public FilterChange FilterTriggerAt(int row) => Level?.FilterChanges.Find(f => f != null && f.row == row);

        /// <summary>Theme tool: opens the row's theme trigger, or adds one (the next environment after the current one).</summary>
        void OpenThemeTrigger(int row)
        {
            if (row < 0) return;
            var t = ThemeTriggerAt(row);
            if (t == null)
            {
                var names = EnvironmentNames(row);
                var now = EnvironmentAt(row);
                int i = now != null ? names.IndexOf(now.name) : -1;
                t = new ThemeChange { row = row, preset = names.Count > 0 ? names[(i + 1) % names.Count] : "Night", rows = 30f, look = "", lookRows = 2 };
                Level.ThemeChanges.Add(t);
                Level.ThemeChanges.Sort((a, b) => a.row.CompareTo(b.row));
                SetDirty(true);
                Status($"Theme trigger added at row {row}: {t.preset}. Change it in the window, Delete removes it.");
            }
            triggerPanel.OpenTheme(t);
            RefreshView();
        }

        /// <summary>Camera tool: opens the row's camera trigger, or adds one (a copy of the camera before it, else High).</summary>
        void OpenCameraTrigger(int row)
        {
            if (row < 0) return;
            var c = CameraTriggerAt(row);
            if (c == null)
            {
                CameraChange before = null;
                foreach (var x in Level.CameraChanges) if (x.row < row) before = x;
                c = before != null
                    ? new CameraChange { row = row, position = before.position, rotation = before.rotation, rows = before.rows, curve = before.curve }
                    : CameraChange.High(row);
                Level.CameraChanges.Add(c);
                Level.CameraChanges.Sort((a, b) => a.row.CompareTo(b.row));
                SetDirty(true);
                Status($"Camera trigger added at row {row}. Set it in the window (3D view shows that camera), Delete removes it.");
            }
            triggerPanel.OpenCamera(c);
            RefreshView();
        }

        /// <summary>
        /// Screen FX tool: opens the row's screen filter trigger, or adds one: the effect of the last trigger before it
        /// (turning it off when it is still on, else the same effect again), Glitch when there is none.
        /// </summary>
        void OpenFilterTrigger(int row)
        {
            if (row < 0) return;
            var f = FilterTriggerAt(row);
            if (f == null)
            {
                FilterChange before = null;
                foreach (var x in Level.FilterChanges) if (x.row < row) before = x;
                f = before != null ? before.Clone() : new FilterChange();
                f.row = row;
                if (before != null && ScreenFilters.ValueAt(Level.FilterChanges, before.Effect, row, out _) > 0.001f)
                {
                    f.strength = 0f; // the effect is on here: this trigger turns it off
                    f.rows = 0f;
                }
                else if (f.strength <= 0f) f.strength = 1f;
                Level.FilterChanges.Add(f);
                Level.FilterChanges.Sort((a, b) => a.row.CompareTo(b.row));
                SetDirty(true);
                Status($"Screen FX trigger added at row {row}: {ScreenFilters.NameOf(f.Effect)} {(f.strength > 0f ? "on" : "off")}. 3D view previews it while the window is open.");
            }
            triggerPanel.OpenFilter(f);
            RefreshView();
        }

        /// <summary>
        /// The 3D view shows the screen effects: the open Screen FX trigger, or while Music Play runs, the level's
        /// triggers at the play row. Nothing else in the editor gets them.
        /// </summary>
        void UpdateFilterPreview()
        {
            var open = triggerPanel != null ? triggerPanel.Filter : null;
            bool show = view3D != null && view3D.Visible && (open != null || (view3D.PlayRow >= 0f && Level.FilterChanges.Count > 0));
            if (!show)
            {
                if (ScreenFilters.Active) ScreenFilters.Clear();
                return;
            }
            ScreenFilters.Target = view3D.ViewCamera;
            if (open != null) ScreenFilters.Preview(open);
            else ScreenFilters.ApplyAt(Level.FilterChanges, view3D.PlayRow);
        }

        /// <summary>The trigger window changed something (or opened / closed).</summary>
        public void TriggerChanged(bool edited = false)
        {
            if (edited)
            {
                SetDirty(true);
            }
            if (view3D != null) view3D.preview = triggerPanel != null ? triggerPanel.Camera : null;
            RefreshView();
        }

        // ---------------- Parts (power-ups, animated tiles) ----------------

        /// <summary>The settings a part gets when painted (EDIT ARGS); its defaults until they are changed.</summary>
        public static PartArgs BrushArgs(int id)
        {
            if (!brushArgs.TryGetValue(id, out var a))
            {
                a = PartArgs.Defaults(id);
                brushArgs[id] = a;
            }
            return a;
        }

        /// <summary>EDIT ARGS: opens the ARGS-Setting window on the settings the selected part's next tiles get.</summary>
        void OpenBrushArgs()
        {
            if (!PartArgs.IsPart(SelectedId)) return;
            if (triggerPanel != null && triggerPanel.IsOpen) triggerPanel.Close();
            partPanel.Open(BrushArgs(SelectedId), true);
            Status($"{TilePalette.Name(SelectedId)}: tiles you paint now get these ARGS.");
        }

        /// <summary>The ARGS window changed the settings for new tiles; the level itself did not change.</summary>
        public void BrushChanged() => Status($"{TilePalette.Name(SelectedId)}: new ARGS set. Paint a tile (again) to give it these.");

        /// <summary>Gives a painted part cell the EDIT ARGS settings (painting over a tile with other settings updates it).</summary>
        bool StampArgs(int row, int col, int id)
        {
            var brush = BrushArgs(id);
            var cell = Level.PartAt(row, col, id);
            if (cell != null ? cell.SameValues(brush) : brush.SameValues(PartArgs.Defaults(id))) return false; // defaults need no entry
            cell = Level.EditArgs(row, col, id);
            cell.CopyValuesFrom(brush);
            Level.PartsChanged(); // a speed change moves the music timing
            SetDirty(true);
            return true;
        }

        /// <summary>Middle click: selects the tile on a cell, and for a part takes its ARGS for the next tiles.</summary>
        public void PickCell(int row, int col)
        {
            if (Level == null || row < 0 || row >= Level.RowCount || col < 0 || col >= LevelData.Width) return;
            // The open tab's layer first; an empty one picks the top of the cell (object, pickup, floor).
            var layer = currentGroup == TileGroup.Collect ? TileLayer.Collect : currentGroup == TileGroup.Obstacle ? TileLayer.Object : TileLayer.Tile;
            int id = Level.Get(layer, row, col);
            if (id == 0) id = Level.GetObject(row, col);
            if (id == 0) id = Level.GetCollect(row, col);
            if (id == 0) id = Level.Get(row, col);
            if (PartArgs.IsPart(id)) BrushArgs(id).CopyValuesFrom(Level.ArgsAt(row, col, id));
            if (id != 0 && currentGroup != TileGroup.Collection) ShowGroup(TilePalette.GroupOf(id));
            Select(id);
            if (partPanel != null && partPanel.IsBrush) partPanel.Open(BrushArgs(id), true); // show the picked values
            Status(PartArgs.IsPart(id) ? $"Picked {TilePalette.Name(id)} with its ARGS from row {row}." : $"Picked {(id == 0 ? "Empty" : TilePalette.Name(id))}.");
        }

        /// <summary>The ARGS-Setting window changed a part (or closed). <paramref name="cell"/>: the one cell it edits
        /// (the Edit tool's ARGS), whose animation the 3D view then plays once.</summary>
        public void PartChanged(bool edited = false, PartArgs cell = null)
        {
            if (edited)
            {
                Level.PartsChanged(); // a speed change moves the music timing
                SetDirty(true);
            }
            RefreshView();
            if (edited && cell != null && view3D != null) view3D.PreviewPart(cell.row, cell.lane, cell.id);
        }

        /// <summary>Middle click for one layer (the Edit window's Pick): that layer's tile, with its ARGS, as the brush.</summary>
        public void PickLayer(int row, int col, TileLayer layer)
        {
            if (Level == null || row < 0 || row >= Level.RowCount) return;
            int id = Level.Get(layer, row, col);
            if (id == 0) return;
            if (PartArgs.IsPart(id)) BrushArgs(id).CopyValuesFrom(Level.ArgsAt(row, col, id));
            if (currentGroup != TileGroup.Collection) ShowGroup(TilePalette.GroupOf(id));
            Select(id);
            Status(PartArgs.IsPart(id) ? $"Picked {TilePalette.Name(id)} with its ARGS from row {row}." : $"Picked {TilePalette.Name(id)}.");
        }

        // ---------------- Edit tool ----------------

        void OpenCellEditor(int row, int col)
        {
            if (row >= Level.RowCount)
            {
                Status("That row is past the end of the level: nothing to edit there.");
                return;
            }
            if (triggerPanel != null && triggerPanel.IsOpen) triggerPanel.Close();
            cellPanel.Open(row, col);
            RefreshView(); // the grid frames the cell
            Status($"Editing row {row}, lane {col + 1}. ARGS changes only this cell.");
        }

        /// <summary>The cell the Edit window shows (the grid frames it).</summary>
        public bool IsEditCell(int row, int col) => cellPanel != null && cellPanel.IsOpen && cellPanel.Row == row && cellPanel.Lane == col;

        public void CellPanelClosed() => RefreshView();

        /// <summary>The Edit window's ARGS button: the ARGS-Setting window on that one cell's part.</summary>
        public void EditCellArgs(int row, int col, int id)
        {
            if (Level == null || !Level.HasPart(row, col, id)) return;
            if (triggerPanel != null && triggerPanel.IsOpen) triggerPanel.Close();
            partPanel.Open(Level.EditArgs(row, col, id));
        }

        public void EditCustomObstacle(int id)
        {
            var def = CustomObstacles.Get(id);
            if (def != null) OpenCustomObstacle(def);
        }

        // ---------------- Copy tool ----------------

        /// <summary>A copied cell: its three layers and the ARGS of its parts.</summary>
        class CopiedCell
        {
            public int tile, obstacle, pickup;
            public readonly List<PartArgs> args = new List<PartArgs>();
            public readonly List<CellGroup> groups = new List<CellGroup>(); // Event Groups of its layers
            public int Top => obstacle != 0 ? obstacle : pickup != 0 ? pickup : tile;
            public string Name
            {
                get
                {
                    var names = new List<string>();
                    foreach (int id in new[] { tile, obstacle, pickup }) if (id != 0) names.Add(TilePalette.Name(id));
                    return string.Join(" + ", names);
                }
            }
        }
        static CopiedCell copied; // static: it stays over a play test

        /// <summary>Copy tool (and the Edit window's Copy cell): takes the cell and selects the Copy brush.</summary>
        public void CopyCell(int row, int col)
        {
            if (Level == null || row < 0 || row >= Level.RowCount || col < 0 || col >= LevelData.Width) return;
            var c = new CopiedCell { tile = Level.Get(row, col), obstacle = Level.GetObject(row, col), pickup = Level.GetCollect(row, col) };
            if (c.tile == 0 && c.obstacle == 0 && c.pickup == 0)
            {
                Status("That cell is empty: nothing to copy.");
                return;
            }
            Level.PartIdsAt(row, col, copyIds);
            foreach (int id in copyIds) c.args.Add(Level.ArgsAt(row, col, id).Clone());
            foreach (var g in Level.CellGroups) if (g != null && g.row == row && g.lane == col && !g.IsEmpty) c.groups.Add(g.Clone());
            copied = c;
            Select(TilePalette.PasteTool);
            Status($"Copied {c.Name} from row {row}" + (c.args.Count > 0 ? " with its ARGS" : "") + ". Click cells to paint it.");
        }

        readonly List<int> copyIds = new List<int>(3);

        /// <summary>The Copy brush: the copied cell, all three layers and the ARGS, exactly.</summary>
        bool PasteCell(int row, int col)
        {
            if (copied == null) return false;
            bool changed = SetCell(TileLayer.Tile, row, col, copied.tile);
            changed |= SetCell(TileLayer.Object, row, col, copied.obstacle);
            changed |= SetCell(TileLayer.Collect, row, col, copied.pickup);
            foreach (var a in copied.args)
            {
                if (!Level.HasPart(row, col, a.id)) continue;
                var cell = Level.PartAt(row, col, a.id);
                if (cell != null && cell.SameValues(a)) continue;
                Level.EditArgs(row, col, a.id).CopyValuesFrom(a);
                SetDirty(true);
                changed = true;
            }
            changed |= PasteGroups(copied.groups, row, col);
            if (changed) Level.PartsChanged();
            return changed;
        }

        /// <summary>Puts copied Event Groups on a cell (its layers' own groups are replaced). True when something changed.</summary>
        bool PasteGroups(List<CellGroup> groups, int row, int col)
        {
            bool changed = false;
            for (int layer = 0; layer < 3; layer++)
            {
                var from = groups.Find(g => g.layer == layer);
                var cell = Level.CellGroupAt(row, col, (TileLayer)layer);
                if (from == null && cell == null) continue;
                if (from != null && cell != null && cell.groups == from.groups && cell.fires == from.fires && cell.sleep == from.sleep && cell.wholeRow == from.wholeRow) continue;
                if (cell != null) Level.CellGroups.Remove(cell);
                if (from != null)
                {
                    var g = from.Clone();
                    g.row = row;
                    g.lane = col;
                    Level.CellGroups.Add(g);
                }
                changed = true;
            }
            if (changed) SetDirty(true);
            return changed;
        }

        // ---------------- Favourites (Collection tab) ----------------

        const string FavoritesKey = "RTXOn.Favorites";
        static List<int> favorites;

        static List<int> Favorites
        {
            get
            {
                if (favorites != null) return favorites;
                favorites = new List<int>();
                foreach (var part in PlayerPrefs.GetString(FavoritesKey, "").Split(','))
                    if (int.TryParse(part, out int id) && !favorites.Contains(id)) favorites.Add(id);
                return favorites;
            }
        }

        static bool CanFavorite(int id) =>
            id != TilePalette.NewCustomTool && id != TilePalette.PasteTool && !TilePalette.IsPropTool(id) && !TilePalette.IsBottomTool(id);

        // The star is a mode (Adrian 2026-10-23): press it, click as many tiles / objects as you like (each click
        // marks or unmarks one, gold star on its button), press the star again and the marked ones are the Collection.
        bool collecting;
        List<int> collected; // the Collection while collecting, in order

        List<int> Marked => collecting ? collected : Favorites;

        /// <summary>The side panel's star: starts collecting, or ends it and saves the Collection tab.</summary>
        void ToggleFavorite()
        {
            if (collecting)
            {
                EndCollecting();
                return;
            }
            collecting = true;
            collected = new List<int>(Favorites);
            if (CanFavorite(SelectedId) && !collected.Contains(SelectedId)) collected.Add(SelectedId);
            UpdateFavoriteMarks();
            Status("Collecting: click tiles and objects to mark them (click again to unmark). Press the star again to put them in Collection.");
        }

        void EndCollecting()
        {
            collecting = false;
            int added = 0, removed = 0;
            foreach (int id in collected) if (!Favorites.Contains(id)) added++;
            foreach (int id in Favorites) if (!collected.Contains(id)) removed++;
            favorites = collected;
            collected = null;
            PlayerPrefs.SetString(FavoritesKey, string.Join(",", Favorites));
            PlayerPrefs.Save();
            UpdateFavoriteMarks();
            if (currentGroup == TileGroup.Collection) ShowGroup(TileGroup.Collection);
            Status(added == 0 && removed == 0 ? "Collection unchanged."
                 : $"Collection: {added} added, {removed} removed ({Favorites.Count} in total).");
        }

        /// <summary>A palette button: selects it, and while collecting also marks or unmarks it.</summary>
        void PaletteClick(int id)
        {
            Select(id);
            if (!collecting || !CanFavorite(id)) return;
            bool on = !collected.Contains(id);
            if (on) collected.Add(id);
            else collected.Remove(id);
            UpdateFavoriteMarks();
            Status((on ? $"{TilePalette.Name(id)} marked" : $"{TilePalette.Name(id)} unmarked")
                 + $" ({collected.Count} marked). Press the star to put them in Collection.");
        }

        void UpdateFavoriteStar()
        {
            if (favoriteStar == null) return;
            bool can = CanFavorite(SelectedId);
            favoriteStar.transform.parent.gameObject.SetActive(can || collecting);
            bool favorite = Favorites.Contains(SelectedId);
            favoriteStar.sprite = collecting || favorite ? UiFactory.Star : UiFactory.StarOutline; // outline = not a favourite
            favoriteStar.color = collecting ? Color.white : favorite ? TilePalette.CrownColor : new Color(1f, 1f, 1f, 0.5f);
            if (favoriteButton != null) favoriteButton.color = collecting ? TilePalette.CrownColor : ButtonColor;
        }

        void UpdateFavoriteMarks()
        {
            foreach (var kv in favoriteBadges)
            {
                bool on = Marked.Contains(kv.Key);
                foreach (var img in kv.Value) if (img != null) img.enabled = on;
            }
            UpdateFavoriteStar();
        }

        /// <summary>The Collection tab's buttons: one per favourite that still exists, in the order they were starred.</summary>
        void RebuildCollectionButtons()
        {
            if (paletteContent == null) return;
            foreach (var b in collectionButtons) RemovePaletteButton(b);
            collectionButtons.Clear();
            foreach (int id in Favorites)
            {
                string name;
                if (CustomObstacle.IsCustomId(id))
                {
                    if (!CustomObstacles.TryGet(id, out var c)) continue; // its .rsrt is gone
                    name = c.name;
                }
                else if (TilePalette.TryGet(id, out var t)) name = t.name;
                else continue;
                var b = MakePaletteButton(paletteContent, new PaletteTile(id, name, TileGroup.Collection));
                paletteButtons.Add(new KeyValuePair<TileGroup, GameObject>(TileGroup.Collection, b));
                collectionButtons.Add(b);
            }
        }

        // ---------------- Palette buttons ----------------

        void AddFrame(Dictionary<int, List<Image>> map, int id, Image img)
        {
            if (!map.TryGetValue(id, out var list)) map[id] = list = new List<Image>();
            list.Add(img);
        }

        void SetFrames(int id, bool on)
        {
            if (!paletteFrames.TryGetValue(id, out var list)) return;
            foreach (var f in list) if (f != null) f.enabled = on;
        }

        /// <summary>Takes a palette button out (its frame and star too); Destroy waits for the end of the frame.</summary>
        void RemovePaletteButton(GameObject b)
        {
            if (b == null) return;
            paletteButtons.RemoveAll(kv => kv.Value == b);
            foreach (var list in paletteFrames.Values) list.RemoveAll(i => i == null || i.transform.IsChildOf(b.transform));
            foreach (var list in favoriteBadges.Values) list.RemoveAll(i => i == null || i.transform.IsChildOf(b.transform));
            b.SetActive(false); // out of the palette's layout now
            Destroy(b);
        }

        public void DeleteTrigger(ThemeChange t)
        {
            Level.ThemeChanges.Remove(t);
            SetDirty(true);
            Status($"Theme trigger at row {t.row} deleted.");
        }

        public void DeleteTrigger(FilterChange f)
        {
            Level.FilterChanges.Remove(f);
            SetDirty(true);
            Status($"Screen FX trigger at row {f.row} deleted.");
        }

        public void DeleteTrigger(CameraChange c)
        {
            Level.CameraChanges.Remove(c);
            SetDirty(true);
            Status($"Camera trigger at row {c.row} deleted.");
        }

        /// <summary>Environment preset at a row: the last theme trigger with one, looked up in the row's look.</summary>
        public EnvironmentPreset EnvironmentAt(int row)
        {
            var look = LookAt(row);
            if (look == null || look.environments == null || look.environments.Count == 0) return null;
            if (!string.IsNullOrEmpty(PreviewLook) && PreviewEnvironment != null)
                foreach (var e in look.environments)
                    if (e != null && string.Equals(e.name, PreviewEnvironment, StringComparison.OrdinalIgnoreCase)) return e;
            string name = null;
            if (Level != null)
                foreach (var c in Level.ThemeChanges)
                {
                    if (c == null) continue;
                    if (c.row > row) break;
                    if (!string.IsNullOrEmpty(c.preset)) name = c.preset;
                }
            if (name != null)
                foreach (var e in look.environments)
                    if (e != null && string.Equals(e.name, name, StringComparison.OrdinalIgnoreCase)) return e;
            return look.environments[0];
        }

        /// <summary>Direction of the sun or moon at a row: the Sun angle / Moon angle of the triggers up to it, else the environment's own.</summary>
        public Quaternion LightRotationAt(int row, EnvironmentPreset env)
        {
            bool sunTurned = false, moonTurned = false;
            Quaternion sunAngle = Quaternion.identity, moonAngle = Quaternion.identity;
            if (Level != null)
                foreach (var c in Level.ThemeChanges)
                {
                    if (c == null) continue;
                    if (c.row > row) break;
                    if (c.turnSun) { sunTurned = true; sunAngle = Quaternion.Euler(c.sun.x, c.sun.y, 0f); }
                    if (c.turnMoon) { moonTurned = true; moonAngle = Quaternion.Euler(c.moon.x, c.moon.y, 0f); }
                }
            return EnvironmentDirector.LightRotation(env, sunTurned, sunAngle, moonTurned, moonAngle);
        }

        /// <summary>Names of the environments (Day, Sunset, Night...) of the look at a row.</summary>
        public List<string> EnvironmentNames(int row)
        {
            var names = new List<string>();
            var look = LookAt(row);
            if (look != null && look.environments != null)
                foreach (var e in look.environments)
                    if (e != null && !string.IsNullOrEmpty(e.name) && !names.Contains(e.name)) names.Add(e.name);
            if (names.Count == 0) names.AddRange(new[] { "Day", "Sunset", "Night" });
            return names;
        }

        public List<string> LookNames()
        {
            var names = new List<string>();
            if (looks != null) foreach (var l in looks) if (l != null && !names.Contains(l.name)) names.Add(l.name);
            foreach (var d in ThemeLibrary.All()) if (!names.Contains(d.name)) names.Add(d.name); // Custom and Fanmade themes
            return names;
        }

        /// <summary>The music strip (the 3D view draws the same loudness along the track).</summary>
        public MusicWaveView Wave => wave;

        /// <summary>Seconds into the song at a row (speed, else Default Speed).</summary>
        public float RowTime(float row) => Level != null ? Level.TimeAtRow(row, defaultSpeed) : 0f;

        /// <summary>The look of a row: the last theme change with a look at or before it, else the level's look, else Theme.</summary>
        public TrackTheme LookAt(int row)
        {
            if (!string.IsNullOrEmpty(PreviewLook)) return CurrentLook(); // Custom Theme window: the theme being edited everywhere
            string name = Level != null ? Level.LookKeyAt(row) : null; // the look, with Objects from a theme trigger
            var look = FindLook(name);
            return look != null ? look : DefaultLook();
        }

        /// <summary>Name of the look that starts at this row (a theme change), or null.</summary>
        public string LookChangeAt(int row)
        {
            if (Level == null) return null;
            string found = null;
            foreach (var c in Level.ThemeChanges)
                if (c != null && c.row == row && !string.IsNullOrEmpty(c.look)) found = c.look;
            return found;
        }

        /// <summary>
        /// A look by name, like the game: an official theme (the asset name, or the name without "Theme": Sky =
        /// SkyTheme) or a Custom Theme, with the level's Theme Settings on top.
        /// </summary>
        public TrackTheme FindLook(string name)
        {
            if (string.IsNullOrEmpty(name) || looks == null) return null;
            string settings = Level != null && Level.ThemeSettings != null ? Level.ThemeSettings.Key : "";
            if (lookCacheVersion != ThemeLibrary.Version || lookCacheSettings != settings)
            {
                lookCache.Clear(); // a theme or the Theme Settings changed: the looks are built again
                lookCacheVersion = ThemeLibrary.Version;
                lookCacheSettings = settings;
            }
            if (lookCache.TryGetValue(name, out var cached) && cached != null) return cached;
            var l = ThemeLibrary.ResolveKey(name, looks, Level != null ? Level.ThemeSettings : null, theme);
            if (l != null) lookCache[name] = l;
            return l;
        }

        /// <summary>The scene's Theme (a level with no look) with the level's Theme Settings on top.</summary>
        public TrackTheme DefaultLook() => ThemeLibrary.WithSettings(theme, Level != null ? Level.ThemeSettings : null, looks);

        /// <summary>Every official theme (Level Editor > Looks).</summary>
        public IList<TrackTheme> Looks => looks ?? new TrackTheme[0];

        /// <summary>The scene's Theme: what a level with no look uses.</summary>
        public TrackTheme DefaultTheme => theme;

        /// <summary>Custom Theme window: the name of the theme shown on every row while it is edited (null = the level's own looks).</summary>
        public string PreviewLook { get; private set; }

        public void SetPreviewLook(string name)
        {
            if (PreviewLook == name) return;
            PreviewLook = name;
            RefreshView();
        }

        // ---------------- Themes (Theme Settings, Custom Themes, share files) ----------------

        public FileChooser Files => fileChooser;

        // ---------------- Marquee (copy / cut / delete / paste rows) ----------------

        int marqueeFrom = -1, marqueeTo = -1;
        bool marqueeStroke, marqueePasting, marqueeInsert = true;
        static LevelData.RowsClip rowsClip; // static: it stays over a play test
        GameObject marqueeBar;
        Text marqueeLabel, marqueeModeLabel;

        public bool HasMarquee => marqueeFrom >= 0 && marqueeTo >= 0;
        int MarqueeFirst => Mathf.Min(marqueeFrom, marqueeTo);
        int MarqueeCount => Mathf.Abs(marqueeTo - marqueeFrom) + 1;

        /// <summary>True for a row in the Marquee selection (the grid draws it blue).</summary>
        public bool IsMarqueeRow(int row) => SelectedId == TilePalette.MarqueeTool && HasMarquee && row >= MarqueeFirst && row < MarqueeFirst + MarqueeCount;

        void BuildMarqueeBar(Transform root, Vector2 at)
        {
            var bg = UiFactory.Round(UiFactory.MakePanel(root, "Marquee Bar", PanelColor, true), 1f);
            UiFactory.Place(bg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), at, new Vector2(230f, 470f));
            marqueeBar = bg.gameObject;
            var head = UiFactory.MakeLabel(bg.transform, "MARQUEE", 22, TextAnchor.MiddleCenter, TilePalette.MarqueeColor, FontStyle.Bold);
            UiFactory.Place(head.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(210f, 34f));
            marqueeLabel = UiFactory.MakeLabel(bg.transform, "", 17, TextAnchor.MiddleCenter, SoftText);
            UiFactory.Place(marqueeLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(214f, 62f));
            marqueeLabel.resizeTextForBestFit = true;
            marqueeLabel.resizeTextMinSize = 10;
            marqueeLabel.resizeTextMaxSize = 17;
            string[] names = { "Copy", "Cut", "Delete", "", "Clear" };
            UnityEngine.Events.UnityAction[] actions = { () => MarqueeCopy(false), () => MarqueeCopy(true), MarqueeDelete, ToggleMarqueeMode, ClearMarquee };
            string[] icons = { null, null, "Trash", null, "Close" };
            for (int i = 0; i < names.Length; i++)
            {
                var color = i == 2 ? ExitColor : i == 0 ? AccentColor : ButtonColor;
                var b = UiFactory.MakeButton(bg.transform, names[i], color, actions[i], 22);
                UiFactory.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -118f - i * 68f), new Vector2(200f, 58f));
                if (icons[i] != null) UiFactory.AddIcon(b, icons[i], 26f);
                if (i == 3)
                {
                    marqueeModeLabel = b.GetComponentInChildren<Text>();
                    marqueeModeLabel.resizeTextForBestFit = true;
                    marqueeModeLabel.resizeTextMinSize = 12;
                    marqueeModeLabel.resizeTextMaxSize = 20;
                }
            }
            marqueeBar.SetActive(false);
        }

        void ShowMarqueeBar()
        {
            if (marqueeBar == null) return;
            bool on = SelectedId == TilePalette.MarqueeTool;
            if (marqueeBar.activeSelf != on) marqueeBar.SetActive(on);
            if (!on) return;
            marqueeLabel.text = (HasMarquee ? MarqueeCount == 1 ? $"Row {MarqueeFirst}" : $"Rows {MarqueeFirst} - {MarqueeFirst + MarqueeCount - 1}" : "No rows picked") +
                                (rowsClip != null ? $"\nCopied: {rowsClip.Count} row(s)" + (marqueePasting ? ", click a row" : "") : "");
            marqueeModeLabel.text = marqueeInsert ? "Paste: Insert" : "Paste: Over";
        }

        /// <summary>A click (or drag) with the Marquee: picks rows, or, after Copy / Cut, pastes the rows at the clicked row.</summary>
        void MarqueePaint(int row)
        {
            row = Mathf.Max(0, row);
            if (marqueePasting && rowsClip != null)
            {
                if (marqueeStroke) return; // one paste per click
                marqueeStroke = true;
                stroke?.Add(new Change { row = row, col = RowsChange, rows = Level.SnapshotRows() });
                Level.PasteRows(rowsClip, row, marqueeInsert);
                marqueeFrom = row;
                marqueeTo = row + rowsClip.Count - 1;
                marqueePasting = false;
                SetDirty(true);
                RefreshView();
                ShowMarqueeBar();
                Status($"Pasted {rowsClip.Count} row(s) at row {row}" + (marqueeInsert ? ": the rows after them moved on." : ", over what was there.") +
                       " Press Copy again to paste once more. Undo takes it back.");
                return;
            }
            row = Mathf.Min(row, Mathf.Max(0, Level.RowCount - 1));
            if (!marqueeStroke)
            {
                marqueeStroke = true;
                if (!(ShiftHeld() && HasMarquee)) marqueeFrom = row;
            }
            marqueeTo = row;
            RefreshView();
            ShowMarqueeBar();
        }

        void MarqueeCopy(bool cut)
        {
            if (Level == null || !HasMarquee)
            {
                Status("Marquee: pick rows first (click or drag over them).");
                return;
            }
            int first = MarqueeFirst, count = MarqueeCount;
            rowsClip = Level.CopyRows(first, count);
            if (cut) DeleteMarqueeRows(first, count);
            marqueePasting = true;
            ShowMarqueeBar();
            Status($"{(cut ? "Cut" : "Copied")} {rowsClip.Count} row(s) with their objects, pickups, ARGS and Event Groups. Click the row to paste them at " +
                   (marqueeInsert ? "(they go in there; the rows after it move on)." : "(they replace the rows there)."));
        }

        void MarqueeDelete()
        {
            if (Level == null || !HasMarquee)
            {
                Status("Marquee: pick rows first (click or drag over them).");
                return;
            }
            int first = MarqueeFirst, count = MarqueeCount;
            DeleteMarqueeRows(first, count);
            Status($"Deleted {count} row(s) from row {first}: the rows after them moved back. Undo brings them back.");
        }

        void DeleteMarqueeRows(int first, int count)
        {
            if (Level.RowCount <= 1) return;
            grid.CancelStroke();
            EndStroke();
            undo.Push(new List<Change> { new Change { row = first, col = RowsChange, rows = Level.SnapshotRows() } });
            redo.Clear();
            Level.DeleteRows(first, count);
            marqueeFrom = marqueeTo = -1;
            SetDirty(true);
            RefreshView();
            ShowMarqueeBar();
        }

        void ToggleMarqueeMode()
        {
            marqueeInsert = !marqueeInsert;
            ShowMarqueeBar();
            Status(marqueeInsert ? "Paste: Insert. Pasted rows go in at the clicked row; the rows after it move on."
                                 : "Paste: Over. Pasted rows replace the rows from the clicked row on.");
        }

        void ClearMarquee()
        {
            marqueeFrom = marqueeTo = -1;
            marqueePasting = false;
            RefreshView();
            ShowMarqueeBar();
        }

        /// <summary>The EVENT GROUPS window: the level's groups, or (a row and lane) one layer of that cell.</summary>
        public void OpenEventGroups(int row = -1, int col = -1, TileLayer layer = TileLayer.Object)
        {
            if (Level == null) return;
            grid.CancelStroke();
            EndStroke();
            if (triggerPanel != null && triggerPanel.IsOpen) triggerPanel.Close();
            eventsPanel.Open(row, col, layer);
        }

        /// <summary>Something in the Event Groups window changed: the grid's G / F badges and the Edit window follow.</summary>
        public void EventGroupsChanged()
        {
            SetDirty(true);
            RefreshView();
            if (cellPanel != null && cellPanel.IsOpen) cellPanel.Refresh();
        }

        public void OpenThemeSettings()
        {
            if (Level == null) return;
            grid.CancelStroke();
            EndStroke();
            if (triggerPanel != null && triggerPanel.IsOpen) triggerPanel.Close();
            themePanel.Open();
        }

        /// <summary>Opens the Custom Theme window on <paramref name="def"/> (null = a new theme).</summary>
        public void OpenCustomTheme(CustomTheme def) => customThemePanel.Open(def);

        /// <summary>
        /// Opens the SpriteMaker on the atlas <paramref name="group"/> (see <see cref="SpriteMakerLab.Groups"/>);
        /// Use hands <paramref name="use"/> the PNG as base64. <paramref name="glow"/>: start on the glow map.
        /// <paramref name="live"/> (its 3D button) gets the PNG while it is edited, for the preview only (null = stop).
        /// </summary>
        public void OpenSpriteMaker(string group, string slot, bool glow, Action<string> use, Action<string> live = null) =>
            spriteMaker.Open(group, slot, glow, use, live);

        /// <summary>SpriteMaker 3D ended: the Custom Theme window's own preview (LevelTexturePreview.json) again, else the open level.</summary>
        public void EndSpriteMaker3D()
        {
            if (customThemePanel != null && customThemePanel.IsOpen) customThemePanel.ShowPreviewLevel();
            else SetPreviewLevel(null, 0f);
        }

        /// <summary>Custom Theme > ColorSets: the environment (by name) every row shows while it is edited; null = as the level says.</summary>
        public string PreviewEnvironment { get; private set; }

        public void SetPreviewEnvironment(string name)
        {
            if (PreviewEnvironment == name) return;
            PreviewEnvironment = name;
            RefreshView();
        }

        /// <summary>SpriteMaker 3D: the theme preview draws <paramref name="level"/> from <paramref name="row"/> (null = the level being edited).</summary>
        public void SetPreviewLevel(LevelData level, float row)
        {
            if (view3D == null || !view3D.ThemePreview) return;
            view3D.SetSource(level, row);
        }

        /// <summary>The 3D view draws from a game-like camera while a theme window is open, and goes back after.</summary>
        public void UpdateThemePreview()
        {
            if (view3D == null) return;
            bool on = (themePanel != null && themePanel.IsOpen) || (customThemePanel != null && customThemePanel.IsOpen);
            if (view3D.ThemePreview == on) return;
            view3D.SetThemePreview(on);
            if (!on) RefreshView();
        }

        public Texture ThemePreviewTexture => view3D != null ? view3D.Texture : null;

        /// <summary>The level's look (Theme window picker): an official or custom theme's name, null = Default.</summary>
        public void SetLevelLook(string name)
        {
            if (Level == null) return;
            Level.Look = string.IsNullOrEmpty(name) ? null : name;
            ThemesChanged();
            Status($"Theme: {LookName()}. The game uses it from row 0 (theme triggers can still switch it later).");
        }

        /// <summary>A theme, or the level's Theme Settings, changed: looks are built again everywhere.</summary>
        public void ThemesChanged(bool dirty = true)
        {
            ThemeLibrary.Touch();
            lookCache.Clear();
            if (currentGroup == TileGroup.Props) RebuildPropButtons();
            RefreshObjectNames();
            if (Level != null) Select(SelectedId);
            if (dirty) SetDirty(true);
            RefreshView();
        }

        /// <summary>A custom theme got a new name: the level's look and theme triggers follow it.</summary>
        public void CustomThemeRenamed(string oldName, string newName)
        {
            if (Level == null || string.IsNullOrEmpty(oldName)) return;
            bool changed = false;
            if (string.Equals(Level.Look, oldName, StringComparison.OrdinalIgnoreCase)) { Level.Look = newName; changed = true; }
            foreach (var c in Level.ThemeChanges)
            {
                if (c != null && string.Equals(c.look, oldName, StringComparison.OrdinalIgnoreCase)) { c.look = newName; changed = true; }
                if (c != null && string.Equals(c.objects, oldName, StringComparison.OrdinalIgnoreCase)) { c.objects = newName; changed = true; }
            }
            ThemesChanged(changed);
        }

        /// <summary>How many places of the open level use the theme called <paramref name="name"/> (look, theme triggers).</summary>
        public int ThemeUses(string name)
        {
            if (Level == null || string.IsNullOrEmpty(name)) return 0;
            int n = string.Equals(Level.Look, name, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            foreach (var c in Level.ThemeChanges)
            {
                if (c != null && string.Equals(c.look, name, StringComparison.OrdinalIgnoreCase)) n++;
                if (c != null && string.Equals(c.objects, name, StringComparison.OrdinalIgnoreCase)) n++;
            }
            return n;
        }

        /// <summary>
        /// Import (any share file: .rsrt, .rsrtp, .themert, .levelrsrt): objects and themes go in the libraries; a
        /// full level is written next to the other levels and opened. Returns what happened.
        /// </summary>
        public void ImportAny(Action<string> report)
        {
            fileChooser.Open("Import (.rsrt object, .rsrtp object pack, .themert theme, .levelrsrt full level)", ThemeLibrary.ImportExtensions, path =>
            {
                var r = ThemeLibrary.Import(path);
                string message = r.message;
                if (r.obstacles.Count > 0)
                {
                    if (currentGroup == TileGroup.Obstacle) RebuildCustomButtons();
                    RefreshView();
                }
                if (r.themes.Count > 0) ThemesChanged(false);
                if (r.levelJson != null)
                {
                    if (dirty)
                        message += " The level was not opened: Save the open level first, then Import it again.";
                    else
                    {
                        string file = LevelFiles.NewLevelPath(null, out _);
                        try
                        {
                            LevelFiles.Write(file, r.levelJson);
                            // Its LevelPreview, when the .levelrsrt carries one, goes next to it (Previews/CustomPreviewN.json).
                            if (!string.IsNullOrEmpty(r.previewJson)) LevelFiles.Write(LevelPreviews.PathFor(file), r.previewJson);
                            OpenLevel(null, file, 0);
                            message += $" Saved as {Path.GetFileName(file)}" + (string.IsNullOrEmpty(r.previewJson) ? "" : " with its LevelPreview") + " and opened.";
                        }
                        catch (Exception e)
                        {
                            message += " It could not be written: " + e.Message;
                        }
                    }
                }
                Status(message);
                report?.Invoke(message);
            }, m => { Status(m); report?.Invoke(m); });
        }

        /// <summary>Export Level: the open level as a Full Level (.levelrsrt) with its objects and themes inside.</summary>
        public string ExportLevelPackage()
        {
            if (Level == null) return "";
            // The level and its LevelPreview together (Level1 + LevelPreview1 = one .levelrsrt).
            string levelText, previewText;
            if (EditingPreview)
            {
                levelText = File.Exists(previewOwner) ? File.ReadAllText(previewOwner) : null;
                if (levelText == null) return "The level of this LevelPreview has no file yet: Save the level first.";
                var preview = LevelPreviews.FromLevel(LevelJson(true), true);
                previewText = preview != null ? LevelPreviews.ToJson(preview) : null;
            }
            else
            {
                levelText = LevelJson(false);
                previewText = LevelPreviews.Find(Path.GetFileNameWithoutExtension(savePath), savePath, previewFiles);
            }
            LevelData owner = null;
            try { owner = LevelData.ParseJson(levelText); }
            catch (Exception) { }
            string name = owner == null || string.IsNullOrWhiteSpace(owner.Name) ? "Level" : owner.Name;
            string text = ThemeLibrary.LevelPackageText(name, levelText, previewText);
            return fileChooser.Save("Export Full Level", ThemeLibrary.SafeFileName(name) + LevelPackage.FileExtension, text);
        }

        /// <summary>Export Objects: every custom obstacle the level paints as one Object Pack (.rsrtp).</summary>
        public string ExportObjectPack()
        {
            if (Level == null) return "";
            var used = CustomObstacles.UsedBy(Level);
            if (used.Count == 0) return "This level paints no custom obstacle (Object tab +), so there is nothing to export.";
            string name = (string.IsNullOrWhiteSpace(Level.Name) ? "Level" : Level.Name) + " Objects";
            return fileChooser.Save("Export Object Pack", ThemeLibrary.SafeFileName(name) + ObjectPack.FileExtension, ThemeLibrary.PackText(name, used));
        }

        string LookName()
        {
            var look = FindLook(Level != null ? Level.Look : null);
            if (look != null) return look.name;
            return theme != null ? $"Default ({theme.name})" : "Default";
        }

        void BuildSidePanel(Transform root)
        {
            var panel = UiFactory.MakePanel(root, "Side Panel", PanelColor).rectTransform;
            panel.anchorMin = new Vector2(1f, 0f);
            panel.anchorMax = Vector2.one;
            panel.pivot = new Vector2(1f, 0.5f);
            panel.offsetMin = new Vector2(-(Margin + RightPanelWidth), Margin);
            panel.offsetMax = new Vector2(-Margin, -(TopBarHeight + Margin));

            var title = UiFactory.MakeLabel(panel, "SELECTED", 24, TextAnchor.MiddleCenter, SoftText, FontStyle.Bold);
            UiFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(280f, 36f));

            selectedSwatch = UiFactory.MakePanel(panel, "Selected Tile", TilePalette.ColorOf(1));
            selectedSwatch.preserveAspect = true;
            UiFactory.Place(selectedSwatch.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(150f, 150f));
            selectedNumber = UiFactory.MakeLabel(selectedSwatch.transform, "", 64, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);

            selectedName = UiFactory.MakeLabel(panel, "", 26, TextAnchor.UpperCenter, Color.white, FontStyle.Bold);
            UiFactory.Place(selectedName.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -216f), new Vector2(280f, 66f));

            // EDIT ARGS, left of the swatch: only for power-ups and animated tiles (Parts tab).
            editArgsButton = UiFactory.MakeButton(panel, "EDIT\nARGS", AccentColor, OpenBrushArgs, 18);
            UiFactory.Place((RectTransform)editArgsButton.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(6f, -56f), new Vector2(66f, 150f));
            editArgsButton.gameObject.SetActive(false);
            // EDIT for a custom obstacle, in the same place: opens the Custom Obstacles window on it.
            editCustomButton = UiFactory.MakeButton(panel, "EDIT", TilePalette.CustomColor, () => OpenCustomObstacle(CustomObstacles.Get(SelectedId)), 18);
            UiFactory.Place((RectTransform)editCustomButton.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(6f, -56f), new Vector2(66f, 150f));
            editCustomButton.gameObject.SetActive(false);
            // Favourite: the star right of the swatch puts the selected tile in the Collection tab (or takes it out).
            var fav = UiFactory.MakeButton(panel, "", ButtonColor, ToggleFavorite, 18);
            favoriteButton = fav.GetComponent<Image>();
            UiFactory.Place((RectTransform)fav.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-6f, -56f), new Vector2(66f, 66f));
            favoriteStar = UiFactory.MakePanel(fav.transform, "Star", Color.white);
            favoriteStar.sprite = UiFactory.Star;
            favoriteStar.raycastTarget = false;
            UiFactory.Stretch(favoriteStar.rectTransform, 9f, 9f, 9f, 9f);

            // What the selected tile does, for tiles that do something special (movers, edges, sliders).
            selectedHint = UiFactory.MakeLabel(panel, "", 19, TextAnchor.UpperCenter, SoftText);
            UiFactory.Place(selectedHint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -286f), new Vector2(280f, 110f));

            rowsLabel = UiFactory.MakeLabel(panel, "", 22, TextAnchor.UpperCenter, SoftText);
            UiFactory.Place(rowsLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -404f), new Vector2(280f, 64f));

            string[] labels = { "PgUp", "PgDn", "Start", "End" };
            UnityEngine.Events.UnityAction[] actions =
            {
                () => ScrollBy(VisibleRows - 1),
                () => ScrollBy(-(VisibleRows - 1)),
                () => ScrollTo(0),
                ScrollToEnd,
            };
            for (int i = 0; i < labels.Length; i++)
            {
                var b = UiFactory.MakeButton(panel, labels[i], ButtonColor, actions[i], 20);
                UiFactory.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(-105f + i * 70f, -476f), new Vector2(64f, 50f));
            }

            // Level settings: the button opens the Level Settings window (name, team, author, banner, stars...);
            // music and speed are right here (type a number, or drag the Speed slider).
            var settings = UiFactory.MakeButton(panel, "LEVEL SETTINGS...", AccentColor, OpenLevelSettings, 20);
            UiFactory.Place((RectTransform)settings.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -530f), new Vector2(280f, 38f));
            UiFactory.AddIcon(settings, "Slider", 24f);
            musicLabel = SettingRow(panel, -570f, "<", () => CycleMusic(-1), ">", () => CycleMusic(1));

            speedInput = NumberRow(panel, -626f, "Speed", OnSpeedTyped);
            speedSlider = UiFactory.MakeHorizontalSlider(panel, new Color(0.2f, 0.17f, 0.21f), AccentColor, Color.white, LevelData.MinSpeed, LevelData.MaxSpeed);
            UiFactory.Place((RectTransform)speedSlider.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -682f), new Vector2(260f, 30f));
            speedSlider.onValueChanged.AddListener(OnSpeedSlider);

            // Dynamic Loading: on/off, and the Load Distance (rows built ahead of the ball), typed or - / +.
            var dyn = UiFactory.MakeButton(panel, "Dynamic: ON", ButtonColor, ToggleDynamicLoading, 18);
            UiFactory.Place((RectTransform)dyn.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-87f, -768f), new Vector2(106f, 44f));
            dynamicLabel = dyn.GetComponentInChildren<Text>();
            loadDistanceInput = UiFactory.MakeInputField(panel, 22, new Color(0.08f, 0.06f, 0.09f), InputField.ContentType.IntegerNumber);
            UiFactory.Place((RectTransform)loadDistanceInput.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(2f, -768f), new Vector2(62f, 44f));
            loadDistanceInput.onEndEdit.AddListener(OnLoadDistanceTyped);
            var minus = UiFactory.MakeButton(panel, "-", ButtonColor, () => StepLoadDistance(-10), 28);
            UiFactory.Place((RectTransform)minus.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(62f, -768f), new Vector2(44f, 44f));
            var plus = UiFactory.MakeButton(panel, "+", ButtonColor, () => StepLoadDistance(10), 28);
            UiFactory.Place((RectTransform)plus.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(112f, -768f), new Vector2(44f, 44f));

            infoLabel = UiFactory.MakeLabel(panel, "", 19, TextAnchor.MiddleCenter, TilePalette.CrownColor);
            UiFactory.Place(infoLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -824f), new Vector2(290f, 50f));

            var help = UiFactory.MakeLabel(panel,
                "Click: paint · Right click: erase\nMiddle click: pick a tile and its ARGS\nE: Edit (a cell's ARGS) · C: Copy a cell\nSpace: Music Play · T: Play in game\nWheel, W/S: scroll · Home/End",
                17, TextAnchor.LowerLeft, SoftText);
            UiFactory.Stretch(help.rectTransform, 14f, 6f, 14f, 858f);
        }

        /// <summary>"Name [ box ]" row: a label and a box to type a number in.</summary>
        InputField NumberRow(RectTransform panel, float y, string name, UnityEngine.Events.UnityAction<string> onTyped)
        {
            var label = UiFactory.MakeLabel(panel, name, 22, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            UiFactory.Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-70f, y), new Vector2(120f, 46f));
            var field = UiFactory.MakeInputField(panel, 24, new Color(0.08f, 0.06f, 0.09f), InputField.ContentType.DecimalNumber);
            UiFactory.Place((RectTransform)field.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(65f, y), new Vector2(130f, 44f));
            field.onEndEdit.AddListener(onTyped);
            return field;
        }

        /// <summary>A "&lt; value &gt;" row of the settings: two small buttons with a label between them.</summary>
        Text SettingRow(RectTransform panel, float y, string left, UnityEngine.Events.UnityAction onLeft, string right, UnityEngine.Events.UnityAction onRight)
        {
            var l = UiFactory.MakeButton(panel, left, ButtonColor, onLeft, 30);
            UiFactory.Place((RectTransform)l.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-110f, y), new Vector2(50f, 50f));
            var r = UiFactory.MakeButton(panel, right, ButtonColor, onRight, 30);
            UiFactory.Place((RectTransform)r.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(110f, y), new Vector2(50f, 50f));
            var label = UiFactory.MakeLabel(panel, "", 22, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            UiFactory.Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(166f, 54f));
            label.resizeTextForBestFit = true; // long song names get smaller
            label.resizeTextMinSize = 12;
            label.resizeTextMaxSize = 22;
            label.supportRichText = true;
            return label;
        }
    }
}
