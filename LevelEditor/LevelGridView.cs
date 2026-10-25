using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RTXOn
{
    /// <summary>
    /// The 5-lane grid of the level editor. Shows <see cref="LevelEditor.VisibleRows"/> rows starting at
    /// <see cref="LevelEditor.FirstRow"/> (row 0 = start of the level, at the bottom, like the game).
    /// Press and drag to paint, right mouse button to erase, mouse wheel to scroll.
    /// Only the visible rows have UI objects; scrolling just changes what they show.
    /// A cell shows its three layers: the tile at the back (colour or icon, ID top-left, name bottom-right, an arrow
    /// for sliding and moving tiles), the object in front in the middle, and the pickup (in the middle, or top-right
    /// when there is an object too). An object that reaches other lanes (binary) gets a red bar over them,
    /// but it is still one cell.
    /// </summary>
    public class LevelGridView : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler, IInitializePotentialDragHandler, IScrollHandler
    {
        static readonly Color PastEndColor = new Color(0.07f, 0.06f, 0.08f);
        static readonly Color RowLabelColor = new Color(0.85f, 0.8f, 0.85f);
        static readonly Color RowLabelTens = new Color(0.4f, 0.9f, 1f);
        static readonly Color RowLabelPastEnd = new Color(0.4f, 0.36f, 0.42f);
        static readonly Color DefaultLineColor = new Color(0.25f, 0.8f, 1f);
        static readonly Color ReachColor = new Color(0.95f, 0.18f, 0.2f, 0.9f);
        static readonly Color ChipBorder = new Color(0f, 0f, 0f, 0.55f);

        LevelEditor editor;
        float cellSize;
        RectTransform area;
        Image[,] cells;          // the tile, at the back
        Text[,] ids;
        Text[,] names;
        Image[,] tileArrows;     // slide / move direction of the tile
        Image[,] reachBars;      // red bar: a binary obstacle's arm reaches this cell
        Image[,] objectChips;    // the object, in front
        Text[,] objectNames;     // on the object chip when it has no icon
        Image[,] pickupChips;    // the pickup
        Text[,] pickupNames;
        GameObject[,] startMarks; // "START POS" badge on the editor's Test start cell
        Text[,] propBadges;      // the props on a cell, along its bottom ("<name" / "name>" = beside the track)
        Text[,] groupBadges;     // Event Groups of the cell, bottom left: G5 = in group 5, F7 = fires group 7
        GameObject[] themeBars, cameraBars, filterBars; // coloured lines on rows with a theme / camera / screen FX trigger
        Text[] rowLabels;
        Image[] rowLines;        // behind each row's cells: the gaps between them show the row's look colour
        bool view3D;             // the 3D view covers the cells; clicks are turned into cells through it

        // Power-up lines (ARGS reach): a bar along the lane and a diamond on each landing, pooled.
        readonly List<Image> partLines = new List<Image>();
        readonly List<Image> partDots = new List<Image>();
        readonly List<float> partMarks = new List<float>();
        const int PartLookBack = 200; // rows below the screen whose lines can still reach it

        struct LookColors { public Color line, floor; }
        readonly Dictionary<TrackTheme, LookColors> lookColors = new Dictionary<TrackTheme, LookColors>();

        /// <summary>The cell area (bottom-left pivot), which the 3D view covers.</summary>
        public RectTransform Area => area;

        // Cells just painted grow and settle once (LevelEditor.WasJustPlaced).
        const float PopSeconds = 0.3f;
        readonly List<(RectTransform rt, float start)> pops = new List<(RectTransform, float)>();

        bool painting;
        int paintId;
        int pointerId;
        PointerEventData.InputButton paintButton;
        bool hasLast;          // lastRow/lastCol hold the previous pointer sample of this stroke
        int lastRow, lastCol;  // absolute row and lane; can be outside the visible grid
        int lastFirst;         // FirstRow when that sample was taken

        const float ScrollRepeat = 0.05f; // seconds between two wheel steps
        float nextScroll;

        int Rows => LevelEditor.VisibleRows;
        const int Columns = LevelData.Width;

        public void Build(LevelEditor owner, float cell, float labelWidth)
        {
            editor = owner;
            cellSize = cell;
            var root = (RectTransform)transform;

            // The cell area is the only part that catches the pointer. Its events bubble up to this component.
            area = UiFactory.MakePanel(root, "Cells", new Color(0.03f, 0.02f, 0.04f), true).rectTransform;
            UiFactory.Place(area, Vector2.zero, Vector2.zero, new Vector2(labelWidth, 0f), new Vector2(cell * Columns, cell * Rows));

            cells = new Image[Rows, Columns];
            ids = new Text[Rows, Columns];
            names = new Text[Rows, Columns];
            tileArrows = new Image[Rows, Columns];
            reachBars = new Image[Rows, Columns];
            objectChips = new Image[Rows, Columns];
            objectNames = new Text[Rows, Columns];
            pickupChips = new Image[Rows, Columns];
            pickupNames = new Text[Rows, Columns];
            startMarks = new GameObject[Rows, Columns];
            propBadges = new Text[Rows, Columns];
            groupBadges = new Text[Rows, Columns];
            themeBars = new GameObject[Rows];
            cameraBars = new GameObject[Rows];
            filterBars = new GameObject[Rows];
            rowLabels = new Text[Rows];
            rowLines = new Image[Rows];
            for (int r = 0; r < Rows; r++)
            {
                rowLines[r] = UiFactory.MakePanel(area, $"Row Lines {r}", new Color(0.03f, 0.02f, 0.04f));
                UiFactory.Place(rowLines[r].rectTransform, Vector2.zero, Vector2.zero, new Vector2(0f, r * cell), new Vector2(cell * Columns, cell));
            }

            for (int r = 0; r < Rows; r++)
            {
                var label = UiFactory.MakeLabel(root, "", 24, TextAnchor.MiddleRight, RowLabelColor, FontStyle.Bold);
                UiFactory.Place(label.rectTransform, Vector2.zero, Vector2.zero, new Vector2(0f, r * cell), new Vector2(labelWidth - 12f, cell));
                label.horizontalOverflow = HorizontalWrapMode.Overflow; // "START" never wraps; right-aligned, so it grows to the left
                rowLabels[r] = label;

                for (int c = 0; c < Columns; c++)
                {
                    var img = UiFactory.MakePanel(area, $"Cell {r},{c}", TilePalette.EmptyColor);
                    img.preserveAspect = true;
                    UiFactory.Place(img.rectTransform, Vector2.zero, Vector2.zero, new Vector2(c * cell + 2f, r * cell + 2f), new Vector2(cell - 4f, cell - 4f));
                    cells[r, c] = img;

                    // Children are drawn in order: tile texts at the back, then the reach bar, the object, the pickup.
                    var id = UiFactory.MakeLabel(img.transform, "", 18, TextAnchor.UpperLeft, Color.white, FontStyle.Bold);
                    UiFactory.Stretch(id.rectTransform, 4f, cell * 0.6f, 4f, 2f);
                    ids[r, c] = id;

                    var name = UiFactory.MakeLabel(img.transform, "", 12, TextAnchor.LowerRight, Color.white);
                    UiFactory.Stretch(name.rectTransform, cell * 0.22f, 2f, 4f, cell * 0.7f);
                    name.horizontalOverflow = HorizontalWrapMode.Wrap;
                    name.verticalOverflow = VerticalWrapMode.Overflow;
                    names[r, c] = name;

                    var arrow = UiFactory.MakePanel(img.transform, "Tile Arrow", Color.white);
                    arrow.sprite = UiFactory.Arrow;
                    UiFactory.Place(arrow.rectTransform, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(13f, 13f), new Vector2(20f, 20f));
                    tileArrows[r, c] = arrow;

                    var reach = UiFactory.MakePanel(img.transform, "Reach", ReachColor);
                    UiFactory.Place(reach.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-2f, 0f), new Vector2(cell, 10f));
                    reachBars[r, c] = reach;

                    objectChips[r, c] = MakeChip(img.transform, "Object", out objectNames[r, c]);
                    pickupChips[r, c] = MakeChip(img.transform, "Pickup", out pickupNames[r, c]);

                    var mark = UiFactory.MakePanel(img.transform, "Start Pos", TilePalette.StartColor);
                    UiFactory.Place(mark.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -2f), new Vector2(cell - 12f, 24f));
                    var markText = UiFactory.MakeLabel(mark.transform, "START POS", 15, TextAnchor.MiddleCenter, new Color(0.05f, 0.12f, 0.06f), FontStyle.Bold);
                    UiFactory.Stretch(markText.rectTransform);
                    markText.horizontalOverflow = HorizontalWrapMode.Overflow;
                    mark.gameObject.SetActive(false);
                    startMarks[r, c] = mark.gameObject;

                    var badge = UiFactory.MakePanel(img.transform, "Props", new Color(TilePalette.PropColor.r, TilePalette.PropColor.g, TilePalette.PropColor.b, 0.9f));
                    UiFactory.Place(badge.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(cell - 8f, 20f));
                    var badgeText = UiFactory.MakeLabel(badge.transform, "", 13, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
                    UiFactory.Stretch(badgeText.rectTransform, 2f, 0f, 2f, 0f);
                    badgeText.resizeTextForBestFit = true; // long model names get smaller
                    badgeText.resizeTextMinSize = 7;
                    badgeText.resizeTextMaxSize = 13;
                    badge.gameObject.SetActive(false);
                    propBadges[r, c] = badgeText;

                    var groupBack = UiFactory.MakePanel(img.transform, "Event Groups", new Color(0.12f, 0.06f, 0.2f, 0.88f));
                    UiFactory.Place(groupBack.rectTransform, Vector2.zero, Vector2.zero, new Vector2(2f, 2f), new Vector2(cell * 0.62f, 18f));
                    var groupText = UiFactory.MakeLabel(groupBack.transform, "", 12, TextAnchor.MiddleLeft, EventGroupColor, FontStyle.Bold);
                    UiFactory.Stretch(groupText.rectTransform, 3f, 0f, 2f, 0f);
                    groupText.resizeTextForBestFit = true;
                    groupText.resizeTextMinSize = 7;
                    groupText.resizeTextMaxSize = 12;
                    groupBack.gameObject.SetActive(false);
                    groupBadges[r, c] = groupText;
                }

                // Trigger lines over the row's cells: theme along the bottom, camera along the top.
                var themeBar = UiFactory.MakePanel(area, $"Theme Trigger {r}", TilePalette.ThemeColor);
                UiFactory.Place(themeBar.rectTransform, Vector2.zero, Vector2.zero, new Vector2(0f, r * cell), new Vector2(cell * Columns, 7f));
                themeBar.gameObject.SetActive(false);
                themeBars[r] = themeBar.gameObject;
                var cameraBar = UiFactory.MakePanel(area, $"Camera Trigger {r}", TilePalette.CameraColor);
                UiFactory.Place(cameraBar.rectTransform, Vector2.zero, Vector2.zero, new Vector2(0f, r * cell + cell - 7f), new Vector2(cell * Columns, 7f));
                cameraBar.gameObject.SetActive(false);
                cameraBars[r] = cameraBar.gameObject;
                var filterBar = UiFactory.MakePanel(area, $"Screen FX Trigger {r}", TilePalette.FilterColor);
                UiFactory.Place(filterBar.rectTransform, Vector2.zero, Vector2.zero, new Vector2(0f, r * cell + cell - 13f), new Vector2(cell * Columns, 5f));
                filterBar.gameObject.SetActive(false);
                filterBars[r] = filterBar.gameObject;
            }
        }

        /// <summary>An object or pickup in a cell: its icon, or a coloured square with its name. Size and place are set by Refresh.</summary>
        static Image MakeChip(Transform cell, string name, out Text label)
        {
            var border = UiFactory.MakePanel(cell, name, ChipBorder);
            var chip = UiFactory.MakePanel(border.transform, "Chip", Color.white);
            UiFactory.Stretch(chip.rectTransform, 2f, 2f, 2f, 2f);
            chip.preserveAspect = true;
            label = UiFactory.MakeLabel(chip.transform, "", 13, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            UiFactory.Stretch(label.rectTransform, 1f, 1f, 1f, 1f);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 7;
            label.resizeTextMaxSize = 14;
            border.gameObject.SetActive(false);
            return chip;
        }

        void Pop(RectTransform rt)
        {
            pops.RemoveAll(p => p.rt == rt);
            pops.Add((rt, Time.unscaledTime));
            rt.localScale = Vector3.one * 0.7f;
        }

        void Update()
        {
            for (int i = pops.Count - 1; i >= 0; i--)
            {
                var (rt, start) = pops[i];
                float u = Mathf.Clamp01((Time.unscaledTime - start) / PopSeconds);
                if (rt != null) rt.localScale = Vector3.one * (u >= 1f ? 1f : 0.7f + 0.3f * u + 0.2f * Mathf.Sin(u * Mathf.PI));
                if (u >= 1f || rt == null) pops.RemoveAt(i);
            }
        }

        /// <summary>Redraws the visible rows from the level. Called after every paint, undo, scroll or load.</summary>
        public void Refresh()
        {
            var level = editor.Level;
            for (int r = 0; r < Rows; r++)
            {
                int row = editor.FirstRow + r;
                bool pastEnd = row >= level.RowCount;
                bool checkpoint = editor.IsCheckpointRow(row);
                var colors = ColorsOf(editor.LookAt(row));
                string lookChange = editor.LookChangeAt(row);
                rowLabels[r].text = row == 0 ? "START"
                                  : checkpoint ? $"{row}\n<size=14>CHECKPOINT</size>"
                                  : lookChange != null ? $"{row}\n<size=14>{lookChange.ToUpperInvariant()}</size>"
                                  : row.ToString();
                rowLabels[r].color = pastEnd ? RowLabelPastEnd
                                   : checkpoint ? TilePalette.CheckpointColor
                                   : lookChange != null ? colors.line
                                   : (row % 10 == 0 ? RowLabelTens : RowLabelColor);
                rowLines[r].color = editor.IsMarqueeRow(row) ? TilePalette.MarqueeColor // the Marquee's rows
                                  : pastEnd ? new Color(0.03f, 0.02f, 0.04f) : colors.line * 0.75f + new Color(0f, 0f, 0f, 0.25f);
                var emptyColor = Color.Lerp(TilePalette.EmptyColor, colors.line, 0.1f);
                bool themeTrigger = editor.ThemeTriggerAt(row) != null, cameraTrigger = editor.CameraTriggerAt(row) != null;
                if (themeBars[r].activeSelf != themeTrigger) themeBars[r].SetActive(themeTrigger);
                if (cameraBars[r].activeSelf != cameraTrigger) cameraBars[r].SetActive(cameraTrigger);
                bool filterTrigger = editor.FilterTriggerAt(row) != null;
                if (filterBars[r].activeSelf != filterTrigger) filterBars[r].SetActive(filterTrigger);

                MarkReach(level, row);
                var props = editor.PropsAt(row);
                for (int c = 0; c < Columns; c++)
                {
                    int id = level.Get(row, c);
                    int obstacle = level.GetObject(row, c), pickup = level.GetCollect(row, c);
                    var icon = id != 0 ? editor.IconFor(id) : null;
                    var img = cells[r, c];
                    img.sprite = icon;
                    // Plain floor takes the colour of its row's look; other tiles keep their palette colour.
                    bool plainFloor = id != 0 && icon == null && IsPlainFloor(id);
                    var tileColor = plainFloor ? colors.floor : TilePalette.ColorOf(id);
                    img.color = id == 0 ? (pastEnd ? PastEndColor : emptyColor)
                                        : (icon != null ? Color.white : tileColor);
                    // Tile shuffle (197) comes from a lane you can't see yet: a see-through "?" tile.
                    bool mystery = icon == null && PartArgs.KindOf(id) == PartKind.Shuffle;
                    if (mystery)
                    {
                        var m = TilePalette.ColorOf(id);
                        img.color = new Color(m.r, m.g, m.b, 0.35f);
                    }

                    var textColor = icon != null || mystery ? Color.white : TilePalette.TextColorOn(tileColor);
                    int number = TileCatalog.MoverGroupOf(id, out _);
                    ids[r, c].text = id == 0 ? "" : mystery ? "?" : number > 0 ? number.ToString() : id.ToString(); // numbered platforms show 1, 2, 3
                    ids[r, c].fontSize = number > 0 || mystery ? 30 : 18;
                    // Trigger tiles show their number (T1), custom move tiles their group.
                    if (id == PartArgs.TriggerTileId)
                    {
                        ids[r, c].text = "T" + level.ArgsAt(row, c, id).group;
                        ids[r, c].fontSize = 28;
                    }
                    else if (id == PartArgs.CustomMoveTileId)
                    {
                        int g = level.ArgsAt(row, c, id).group;
                        if (g > 0) ids[r, c].text = $"{id} G{g}";
                    }
                    ids[r, c].color = textColor;
                    names[r, c].text = id == 0 ? "" : TilePalette.Name(id);
                    names[r, c].color = textColor;

                    float angle = 0f;
                    bool arrow = id != 0 && ArrowOf(id, out angle);
                    tileArrows[r, c].enabled = arrow;
                    if (arrow)
                    {
                        tileArrows[r, c].color = textColor;
                        tileArrows[r, c].rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
                    }
                    reachBars[r, c].enabled = reached[c];

                    // The object in front, in the middle; the pickup in the middle too, or in the top-right corner beside an object.
                    ShowChip(objectChips[r, c], objectNames[r, c], obstacle, new Vector2(0.5f, 0.5f), 0.56f);
                    if (PartArgs.HasGroup(PartArgs.KindOf(obstacle)) && objectNames[r, c].text.Length > 0)
                    {
                        int g = level.ArgsAt(row, c, obstacle).group;
                        if (g > 0) objectNames[r, c].text += "\nG" + g;
                        if (obstacle == PartArgs.CustomMoveBlockId && !level.ArgsAt(row, c, obstacle).kill) objectNames[r, c].text += "\nSAFE";
                    }
                    ShowChip(pickupChips[r, c], pickupNames[r, c], pickup,
                             obstacle != 0 ? new Vector2(0.8f, 0.8f) : new Vector2(0.5f, 0.5f), obstacle != 0 ? 0.36f : 0.5f);

                    var edited = img.GetComponent<Outline>();
                    bool editing = editor.IsEditCell(row, c);
                    if (editing && edited == null)
                    {
                        edited = img.gameObject.AddComponent<Outline>();
                        edited.effectColor = Color.white;
                        edited.effectDistance = new Vector2(4f, -4f);
                    }
                    if (edited != null) edited.enabled = editing;

                    bool start = editor.IsStartCell(row, c);
                    if (startMarks[r, c].activeSelf != start) startMarks[r, c].SetActive(start);
                    if (editor.WasJustPlaced(row, c)) Pop(img.rectTransform);
                    ShowProps(propBadges[r, c], props, c);
                    ShowGroups(groupBadges[r, c], level, row, c);
                }
            }
            DrawPartLines(level);
        }

        /// <summary>
        /// The reach of every power-up whose line crosses the screen, like the fan editor: a coloured bar in its lane
        /// from the tile to the end of its Distance (jump landing, end of a bounce or speed change, the switched rows)
        /// and a diamond on each landing. Invisible has none. Switch and Speed Up/Down also get a trigger line across
        /// their row, and Switch's bar runs up the middle of the track.
        /// </summary>
        void DrawPartLines(LevelData level)
        {
            int first = editor.FirstRow;
            float top = Rows * cellSize;
            int lines = 0, dots = 0;
            int end = Mathf.Min(level.RowCount, first + Rows);
            for (int row = Mathf.Max(0, first - PartLookBack); row < end; row++)
                for (int c = 0; c < Columns; c++)
                {
                    if (level.Get(row, c) == PartArgs.TriggerTileId && level.ArgsAt(row, c, PartArgs.TriggerTileId).wholeRow)
                    {
                        float ty = (row - first + 0.5f) * cellSize;
                        if (ty >= 0f && ty <= top)
                        {
                            // Whole-row trigger tile: a line across, like the power-up trigger lines.
                            var across = Pooled(partLines, lines++, "Part Line");
                            across.color = PartArgs.TriggerTileColor;
                            UiFactory.Place(across.rectTransform, Vector2.zero, new Vector2(0f, 0.5f), new Vector2(0f, ty), new Vector2(Columns * cellSize, 6f));
                        }
                    }
                    var kind = PartArgs.KindOf(level.PartIdAt(row, c));
                    if (!PartArgs.IsPowerUp(kind)) continue;
                    bool hasLine = level.ArgsAt(row, c).Line(partMarks, out float from, out float to, out Color color);
                    float rowY = (row - first + 0.5f) * cellSize;
                    if (PartArgs.IsRowTrigger(kind) && rowY >= 0f && rowY <= top)
                    {
                        // Trigger line: across the whole row, it goes off in any lane.
                        var across = Pooled(partLines, lines++, "Part Line");
                        across.color = color;
                        UiFactory.Place(across.rectTransform, Vector2.zero, new Vector2(0f, 0.5f), new Vector2(0f, rowY), new Vector2(Columns * cellSize, 6f));
                    }
                    if (!hasLine) continue;
                    float x = ((PartArgs.CentredLine(kind) ? (Columns - 1) * 0.5f : c) + 0.5f) * cellSize;
                    float y0 = (row - first + 0.5f + from) * cellSize, y1 = (row - first + 0.5f + to) * cellSize;
                    float lo = Mathf.Max(0f, y0), hi = Mathf.Min(top, y1);
                    if (hi > lo)
                    {
                        var line = Pooled(partLines, lines++, "Part Line");
                        line.color = color;
                        UiFactory.Place(line.rectTransform, Vector2.zero, new Vector2(0.5f, 0f), new Vector2(x, lo), new Vector2(8f, hi - lo));
                    }
                    foreach (float m in partMarks)
                    {
                        float y = (row - first + 0.5f + m) * cellSize;
                        if (y < 0f || y > top) continue;
                        var dot = Pooled(partDots, dots++, "Part Landing");
                        dot.color = color;
                        UiFactory.Place(dot.rectTransform, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(26f, 26f));
                        dot.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    }
                }
            for (int i = lines; i < partLines.Count; i++) partLines[i].enabled = false;
            for (int i = dots; i < partDots.Count; i++) partDots[i].enabled = false;
        }

        readonly bool[] reached = new bool[Columns];

        /// <summary>Lanes of a row that a binary obstacle's arm or a custom obstacle's hitbox reaches (its own cell included), like the game's danger spread.</summary>
        void MarkReach(LevelData level, int row)
        {
            for (int c = 0; c < Columns; c++) reached[c] = false;
            for (int c = 0; c < Columns; c++)
            {
                int obstacle = level.GetObject(row, c);
                if (obstacle == 0) continue;
                if (CustomObstacles.TryGet(obstacle, out var custom))
                {
                    // A custom obstacle's hitbox boxes beside it in the same row.
                    if (custom.touch == CustomTouch.Kill)
                        foreach (var b in custom.boxes)
                            if (b.z == 0 && c + b.x >= 0 && c + b.x < Columns) reached[c + b.x] = true;
                    continue;
                }
                var spec = TileCatalog.Decode(obstacle);
                if (spec.item != ItemKind.BinaryLeft && spec.item != ItemKind.BinaryRight || spec.reach <= 0) continue;
                int step = spec.item == ItemKind.BinaryLeft ? -1 : 1;
                for (int i = 0; i <= spec.reach; i++)
                {
                    int lane = c + step * i;
                    if (lane >= 0 && lane < Columns) reached[lane] = true;
                }
            }
        }

        /// <summary>The props of lane <paramref name="c"/>; lane 1 and 5 also list the props beside the track on their side.</summary>
        public static readonly Color EventGroupColor = new Color(1f, 0.72f, 0.25f);
        readonly System.Text.StringBuilder groupText = new System.Text.StringBuilder();

        /// <summary>The cell's Event Groups: G5,6 (its layers are in groups 5 and 6), F7 (it fires group 7).</summary>
        void ShowGroups(Text badge, LevelData level, int row, int c)
        {
            groupText.Length = 0;
            if (level.CellGroups.Count > 0 && row < level.RowCount)
            {
                string groups = "", fires = "";
                foreach (var g in level.CellGroups)
                {
                    if (g == null || g.row != row || g.lane != c || level.Get(g.Layer, row, c) == 0) continue;
                    if (!string.IsNullOrEmpty(g.groups)) groups += (groups.Length > 0 ? "," : "") + g.groups;
                    if (!string.IsNullOrEmpty(g.fires)) fires += (fires.Length > 0 ? "," : "") + g.fires;
                }
                if (groups.Length > 0) groupText.Append('G').Append(groups);
                if (fires.Length > 0) groupText.Append(groupText.Length > 0 ? " " : "").Append('F').Append(fires);
            }
            var back = badge.transform.parent.gameObject;
            bool on = groupText.Length > 0;
            if (back.activeSelf != on) back.SetActive(on);
            if (on) badge.text = groupText.ToString();
        }

        static void ShowProps(Text badge, List<PropPlacement> props, int c)
        {
            string text = null;
            int count = 0;
            foreach (var p in props)
            {
                string name;
                if (c == 0 && p.x <= -1.5f) name = "<" + p.model;
                else if (c == Columns - 1 && p.x >= 1.5f) name = p.model + ">";
                else if (Mathf.Abs(p.x - (c - 2f)) < 0.5f) name = p.model;
                else continue;
                if (count++ == 0) text = name;
            }
            if (count > 1) text += $" +{count - 1}";
            var bg = badge.transform.parent.gameObject;
            if (bg.activeSelf != (count > 0)) bg.SetActive(count > 0);
            badge.text = text ?? "";
        }

        void ShowChip(Image chip, Text label, int id, Vector2 anchor, float size)
        {
            var border = chip.rectTransform.parent.gameObject;
            if (border.activeSelf != (id != 0)) border.SetActive(id != 0);
            if (id == 0) return;
            UiFactory.Place((RectTransform)border.transform, anchor, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(cellSize * size, cellSize * size));
            var icon = editor.IconFor(id) ?? BuiltInIcon(id);
            var color = TilePalette.ColorOf(id);
            // A mover arrow points its way; everything else stands upright.
            float angle = 0f;
            if (TileCatalog.ArrowOf(id, out var dir)) angle = dir.x < 0 ? 90f : dir.x > 0 ? -90f : dir.y < 0 ? 180f : 0f;
            chip.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            chip.sprite = icon;
            // A drawn icon keeps its colours; a built-in shape (gem, star, crown) takes the tile's colour.
            chip.color = icon != null && editor.IconFor(id) != null ? Color.white : color;
            border.GetComponent<Image>().enabled = icon == null;
            label.text = icon != null ? "" : TilePalette.Name(id);
            label.color = TilePalette.TextColorOn(color);
        }

        /// <summary>Shapes for pickups without a drawn icon.</summary>
        static Sprite BuiltInIcon(int id)
        {
            switch (TileCatalog.Decode(id).item)
            {
                case ItemKind.Gem:
                case ItemKind.FloatingGem: return UiFactory.Gem;
                case ItemKind.Star:
                case ItemKind.FloatingStar: return UiFactory.Star;
                case ItemKind.Crown: return UiFactory.Crown;
                case ItemKind.MoverArrow: return UiFactory.Arrow;
            }
            switch (PartArgs.KindOf(id))
            {
                case PartKind.GemRise:
                case PartKind.GemDrop: return UiFactory.Gem;
                case PartKind.CrownRise:
                case PartKind.CrownDrop: return UiFactory.Crown;
            }
            return null;
        }

        /// <summary>Arrow on tiles that slide or move: rotation in degrees from pointing forward (up the grid).</summary>
        static bool ArrowOf(int id, out float angle)
        {
            angle = 0f;
            if (id == 19 || id == 175) { angle = 90f; return true; }  // slides to the left
            if (id == 20 || id == 174) { angle = -90f; return true; } // slides to the right
            if (TileCatalog.MoverOf(id, out var dir) == MoverKind.None) return false;
            angle = dir.x < 0 ? 90f : dir.x > 0 ? -90f : dir.y < 0 ? 180f : 0f;
            return true;
        }

        Image Pooled(List<Image> pool, int index, string name)
        {
            if (index >= pool.Count) pool.Add(UiFactory.MakePanel(area, name, Color.white)); // made after the cells, so drawn over them
            var img = pool[index];
            img.enabled = true;
            return img;
        }

        static bool IsPlainFloor(int id)
        {
            var spec = TileCatalog.Decode(id);
            return spec.floor == FloorKind.Normal && spec.item == ItemKind.None && TileCatalog.MoverOf(id, out _) == MoverKind.None && TileCatalog.MoverGroupOf(id, out _) == 0
                   && !(id >= 109 && id <= 116) && !PartArgs.HasGroup(PartArgs.KindOf(id)); // trigger / custom move tiles keep their colour
        }

        /// <summary>Grid colours of a look: its floor colour, and its floor glow (edge lines) for the row lines.</summary>
        LookColors ColorsOf(TrackTheme look)
        {
            if (look == null) return new LookColors { line = DefaultLineColor, floor = TilePalette.ColorOf(1) };
            if (lookColors.TryGetValue(look, out var c)) return c;
            c.floor = look.floor != null ? BaseColor(look.floor) : TilePalette.ColorOf(1);
            c.line = DefaultLineColor;
            if (look.floor != null && look.floor.HasProperty("_EmissionColor"))
            {
                var e = look.floor.GetColor("_EmissionColor");
                float max = e.maxColorComponent;
                if (max > 0.01f) c.line = new Color(e.r / max, e.g / max, e.b / max, 1f);
            }
            lookColors[look] = c;
            return c;
        }

        static Color BaseColor(Material m)
        {
            var c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
            c.a = 1f;
            return c;
        }

        /// <summary>Strokes start only on the cells or the 3D picture, not on panels that sit over the grid.</summary>
        bool OnPaintSurface(GameObject go)
        {
            if (go == null) return false;
            if (go.transform == area || go.transform.IsChildOf(area)) return true;
            return view3D && editor.View3D != null && editor.View3D.IsPicture(go);
        }

        /// <summary>3D view on: row numbers hide (the picture is not row by row) and clicks go through the 3D camera.</summary>
        public void Show3D(bool on)
        {
            view3D = on;
            CancelStroke();
            foreach (var l in rowLabels) l.enabled = !on;
        }

        // ---------------- Pointer: paint and erase ----------------

        public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = false; // paint from the first pixel

        public void OnPointerDown(PointerEventData e)
        {
            if (painting) return;
            if (!OnPaintSurface(e.pointerPressRaycast.gameObject)) return; // e.g. the 3D controls panel
            if (e.button == PointerEventData.InputButton.Middle)
            {
                // Middle click picks the tile (and a part's ARGS) instead of painting.
                if (CellUnder(e, out int pickRow, out int pickCol)) editor.PickCell(pickRow, pickCol);
                return;
            }
            painting = true;
            pointerId = e.pointerId;
            paintButton = e.button;
            paintId = e.button == PointerEventData.InputButton.Right ? editor.EraserId : editor.SelectedId;
            hasLast = false;
            editor.BeginStroke();
            PaintAt(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (IsStrokePointer(e)) PaintAt(e);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!IsStrokePointer(e)) return;
            painting = false;
            editor.EndStroke();
        }

        /// <summary>
        /// Same finger, same mouse button. With the Input System every mouse button has the same pointerId,
        /// so without the button check letting go of the right button would end a left-button stroke.
        /// </summary>
        bool IsStrokePointer(PointerEventData e) => painting && e.pointerId == pointerId && e.button == paintButton;

        /// <summary>Stops the stroke in progress; the rest of that drag paints nothing. Load and Undo call this.</summary>
        public void CancelStroke()
        {
            painting = false;
            hasLast = false;
        }

        /// <summary>
        /// The view jumped (Home, End, Page Up/Down, slider): the stroke goes on from the cell under the pointer
        /// instead of drawing a line through all the rows in between.
        /// </summary>
        public void BreakLine() => hasLast = false;

        void OnDisable()
        {
            if (!painting) return;
            painting = false;
            editor.EndStroke();
        }

        /// <summary>The cell under the pointer, on the grid or the 3D track.</summary>
        bool CellUnder(PointerEventData e, out int row, out int col)
        {
            row = col = 0;
            if (view3D)
                return editor.View3D.ViewportAt(e.position, e.pressEventCamera, out var viewport) && editor.View3D.CellAt(viewport, out row, out col);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(area, e.position, e.pressEventCamera, out var local)) return false;
            col = Mathf.FloorToInt(local.x / cellSize);
            row = editor.FirstRow + Mathf.FloorToInt(local.y / cellSize);
            return local.x >= 0f && local.y >= 0f && col < Columns && local.y < Rows * cellSize;
        }

        void PaintAt(PointerEventData e)
        {
            if (view3D)
            {
                // The cell under the pointer on the 3D track; off the track = nothing, and the next cell starts a new line.
                if (!editor.View3D.ViewportAt(e.position, e.pressEventCamera, out var viewport) ||
                    !editor.View3D.CellAt(viewport, out int row3, out int col3))
                {
                    hasLast = false;
                    return;
                }
                if (hasLast && row3 == lastRow && col3 == lastCol) return;
                if (hasLast) PaintLine(lastRow, lastCol, row3, col3);
                else PaintCell(row3, col3);
                hasLast = true;
                lastRow = row3;
                lastCol = col3;
                lastFirst = editor.FirstRow;
                return;
            }
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(area, e.position, e.pressEventCamera, out var local)) return;
            // area's pivot is its bottom-left corner, so local runs from (0,0) to (width, height).
            // The cell is worked out even outside the grid, so a drag that leaves or comes back in still paints up to the edge.
            int col = Mathf.FloorToInt(local.x / cellSize);
            int row = editor.FirstRow + Mathf.FloorToInt(local.y / cellSize);
            if (hasLast && row == lastRow && col == lastCol) return;

            if (hasLast) PaintLine(lastRow, lastCol, row, col);
            else PaintCell(row, col);
            hasLast = true;
            lastRow = row;
            lastCol = col;
            lastFirst = editor.FirstRow;
        }

        /// <summary>
        /// Paints one cell if it was on screen at either end of the line; the part outside the grid is skipped.
        /// "Either end" covers the rows scrolled past with the wheel or W/S while holding the button.
        /// </summary>
        void PaintCell(int row, int col)
        {
            int low = hasLast ? Mathf.Min(lastFirst, editor.FirstRow) : editor.FirstRow;
            int high = (hasLast ? Mathf.Max(lastFirst, editor.FirstRow) : editor.FirstRow) + Rows;
            if (col < 0 || col >= Columns || row < 0) return;
            if (!view3D && (row < low || row >= high)) return; // the 3D view shows more rows than the grid
            editor.Paint(row, col, paintId);
        }

        /// <summary>
        /// Paints every cell on the line between two cells (Bresenham). The pointer is only read once per frame,
        /// so a fast drag jumps several cells at a time; this fills the gap.
        /// </summary>
        void PaintLine(int row0, int col0, int row1, int col1)
        {
            int dr = Mathf.Abs(row1 - row0), dc = Mathf.Abs(col1 - col0);
            int sr = row0 < row1 ? 1 : -1, sc = col0 < col1 ? 1 : -1;
            int error = dc - dr;
            int row = row0, col = col0;
            while (row != row1 || col != col1)
            {
                int e2 = 2 * error;
                if (e2 > -dr) { error -= dr; col += sc; }
                if (e2 < dc) { error += dc; row += sr; }
                PaintCell(row, col); // the start cell was painted by the previous call
            }
        }

        // ---------------- Mouse wheel ----------------

        public void OnScroll(PointerEventData e)
        {
            // Only the direction is used: wheel step sizes differ between mice, OSes and input systems.
            // A trackpad sends many small scroll events per swipe, so at most one step every ScrollRepeat seconds.
            float y = e.scrollDelta.y;
            if (y == 0f || Time.unscaledTime < nextScroll) return;
            nextScroll = Time.unscaledTime + ScrollRepeat;
            editor.ScrollBy(y > 0f ? 2 : -2);
        }
    }
}
