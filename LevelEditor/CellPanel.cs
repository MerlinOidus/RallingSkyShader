using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RTXOn
{
    /// <summary>
    /// The level editor's Edit tool: the cell you click, layer by layer (Tile, Object, Collect), with every ARGS value.
    /// ARGS opens the ARGS-Setting window on that cell (only that cell changes; a Custom Obstacle: its Group), Pick
    /// takes the tile (and its ARGS) as the brush. Copy takes the whole cell for the Copy brush. Drag the title to move it.
    /// </summary>
    public class CellPanel : MonoBehaviour
    {
        static Color PanelColor => EditorColors.Window;
        static Color ButtonColor => EditorColors.Button;
        static Color SoftText => EditorColors.Soft;
        static Color StripColor => EditorColors.Strip;
        const float Width = 470f, SectionHeight = 196f;

        LevelEditor editor;
        RectTransform root;
        Text title, propsLine;

        class Section
        {
            public TileLayer layer;
            public Image swatch;
            public Text number, name, details;
            public Button args, pick, group;
            public Text argsLabel;
            public int id;
        }
        readonly List<Section> sections = new List<Section>();

        public int Row { get; private set; } = -1;
        public int Lane { get; private set; } = -1;
        public bool IsOpen => gameObject.activeSelf;

        public static CellPanel Build(LevelEditor owner, Transform parent, Vector2 centre)
        {
            var bg = UiFactory.Round(UiFactory.MakePanel(parent, "Cell Panel", PanelColor, true), 1f);
            UiFactory.Place(bg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), centre, new Vector2(Width, 120f + 3 * SectionHeight + 110f));
            var panel = bg.gameObject.AddComponent<CellPanel>();
            panel.editor = owner;
            panel.root = bg.rectTransform;
            panel.BuildUi();
            bg.gameObject.SetActive(false);
            return panel;
        }

        void BuildUi()
        {
            // Title strip: drag it to move the window.
            var strip = UiFactory.MakePanel(root, "Drag Strip", StripColor, true);
            UiFactory.Place(strip.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(Width, 70f));
            var drag = strip.gameObject.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            entry.callback.AddListener(d => Move(((PointerEventData)d).delta));
            drag.triggers.Add(entry);
            title = UiFactory.MakeLabel(strip.transform, "", 24, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            UiFactory.Place(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(360f, 60f));
            title.raycastTarget = false;
            var close = UiFactory.MakeButton(root, "X", ButtonColor, Close, 26);
            UiFactory.IconOnly(close, "Close", 26f);
            UiFactory.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-10f, -10f), new Vector2(50f, 50f));

            TileLayer[] layers = { TileLayer.Tile, TileLayer.Object, TileLayer.Collect };
            for (int i = 0; i < layers.Length; i++) sections.Add(MakeSection(layers[i], -80f - i * SectionHeight));

            propsLine = UiFactory.MakeLabel(root, "", 18, TextAnchor.MiddleLeft, SoftText);
            UiFactory.Place(propsLine.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(18f, 70f), new Vector2(Width - 36f, 34f));

            var copy = UiFactory.MakeButton(root, "Copy cell", new Color(0.2f, 0.42f, 0.62f), () => editor.CopyCell(Row, Lane), 22);
            UiFactory.Place((RectTransform)copy.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(18f, 14f), new Vector2(180f, 48f));
            var done = UiFactory.MakeButton(root, "Done", new Color(0.12f, 0.5f, 0.2f), Close, 22);
            UiFactory.Place((RectTransform)done.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-18f, 14f), new Vector2(150f, 48f));
        }

        Section MakeSection(TileLayer layer, float y)
        {
            var s = new Section { layer = layer };
            var back = UiFactory.MakePanel(root, layer + " Section", new Color(1f, 1f, 1f, 0.05f));
            UiFactory.Place(back.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, y), new Vector2(Width - 24f, SectionHeight - 10f));
            var head = UiFactory.MakeLabel(back.transform, LayerName(layer).ToUpperInvariant(), 16, TextAnchor.UpperLeft, SoftText, FontStyle.Bold);
            UiFactory.Place(head.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -6f), new Vector2(120f, 22f));
            s.swatch = UiFactory.MakePanel(back.transform, "Swatch", Color.white);
            s.swatch.preserveAspect = true;
            UiFactory.Place(s.swatch.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -32f), new Vector2(92f, 92f));
            s.number = UiFactory.MakeLabel(s.swatch.transform, "", 30, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            UiFactory.Stretch(s.number.rectTransform);
            s.name = UiFactory.MakeLabel(back.transform, "", 22, TextAnchor.UpperLeft, Color.white, FontStyle.Bold);
            UiFactory.Place(s.name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(114f, -8f), new Vector2(200f, 30f));
            s.name.resizeTextForBestFit = true;
            s.name.resizeTextMinSize = 12;
            s.name.resizeTextMaxSize = 22;
            s.details = UiFactory.MakeLabel(back.transform, "", 15, TextAnchor.UpperLeft, SoftText);
            UiFactory.Place(s.details.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(114f, -40f), new Vector2(210f, 140f));
            s.details.resizeTextForBestFit = true;
            s.details.resizeTextMinSize = 10;
            s.details.resizeTextMaxSize = 15;
            s.args = UiFactory.MakeButton(back.transform, "ARGS", new Color(0.55f, 0.28f, 0.62f), () => OpenArgs(s), 20);
            UiFactory.Place((RectTransform)s.args.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-8f, -10f), new Vector2(100f, 56f));
            s.argsLabel = s.args.GetComponentInChildren<Text>();
            s.pick = UiFactory.MakeButton(back.transform, "Pick", ButtonColor, () => editor.PickLayer(Row, Lane, s.layer), 20);
            UiFactory.Place((RectTransform)s.pick.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-8f, -72f), new Vector2(100f, 56f));
            // Event Groups of this layer (in groups / fires groups).
            s.group = UiFactory.MakeButton(back.transform, "GROUP", new Color(0.62f, 0.4f, 0.12f), () => editor.OpenEventGroups(Row, Lane, s.layer), 20);
            UiFactory.Place((RectTransform)s.group.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-8f, -134f), new Vector2(100f, 46f));
            return s;
        }

        static string LayerName(TileLayer layer) => layer == TileLayer.Tile ? "Tile" : layer == TileLayer.Object ? "Object" : "Collect";

        // ---------------- Open / close ----------------

        public void Open(int row, int lane)
        {
            Row = row;
            Lane = lane;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Refresh();
        }

        public void Close()
        {
            if (!IsOpen) return;
            gameObject.SetActive(false);
            Row = Lane = -1;
            editor.CellPanelClosed();
        }

        /// <summary>Closes without telling the editor (the level it showed was replaced).</summary>
        public void Hide()
        {
            gameObject.SetActive(false);
            Row = Lane = -1;
        }

        void Move(Vector2 screenDelta)
        {
            var canvas = GetComponentInParent<Canvas>();
            float scale = canvas != null ? canvas.rootCanvas.scaleFactor : 1f;
            var p = root.anchoredPosition + screenDelta / Mathf.Max(0.01f, scale);
            p.x = Mathf.Clamp(p.x, -900f, 900f);
            p.y = Mathf.Clamp(p.y, -420f, 420f);
            root.anchoredPosition = p;
        }

        void OpenArgs(Section s)
        {
            if (s.id == 0) return;
            if (PartArgs.IsPart(s.id)) editor.EditCellArgs(Row, Lane, s.id); // a Custom Obstacle too: its Group (EDIT on the side panel opens its window)
        }

        /// <summary>Shows the cell as it is now (call after any change to it).</summary>
        public void Refresh()
        {
            var level = editor.Level;
            if (!IsOpen || level == null) return;
            title.text = $"CELL  ·  ROW {Row}  ·  LANE {Lane + 1}";
            foreach (var s in sections)
            {
                int id = Row < level.RowCount ? level.Get(s.layer, Row, Lane) : 0;
                s.id = id;
                var icon = id != 0 ? editor.IconFor(id) : null;
                s.swatch.sprite = icon;
                s.swatch.color = id == 0 ? TilePalette.EmptyColor : icon != null ? Color.white : TilePalette.ColorOf(id);
                s.number.text = id == 0 || icon != null ? "" : TilePalette.Label(id);
                s.number.color = TilePalette.TextColorOf(id);
                s.name.text = id == 0 ? "Empty" : TilePalette.Name(id);
                s.details.text = id == 0 ? "" : Details(level, id, s.layer);
                s.args.gameObject.SetActive(PartArgs.IsPart(id)); // Custom Obstacles too (their Group)
                s.argsLabel.text = "ARGS";
                s.pick.gameObject.SetActive(id != 0);
                s.group.gameObject.SetActive(id != 0);
            }
            int props = 0;
            foreach (var p in level.Props) if (p != null && p.row == Row && Mathf.Abs(p.x - (Lane - 2f)) < 0.5f) props++;
            propsLine.text = props > 0 ? $"Props on this cell: {props}" : "";
        }

        /// <summary>ID, what it is and every ARGS value it plays with.</summary>
        string Details(LevelData level, int id, TileLayer layer)
        {
            var sb = new StringBuilder();
            sb.Append("ID ").Append(id);
            var g = level.CellGroupAt(Row, Lane, layer);
            if (g != null && !g.IsEmpty)
            {
                if (!string.IsNullOrEmpty(g.groups)) sb.Append("\nIn groups: ").Append(GroupNames(level, g.groups)).Append(g.sleep ? " (waits)" : "");
                if (!string.IsNullOrEmpty(g.fires)) sb.Append("\nFires: ").Append(GroupNames(level, g.fires)).Append(g.wholeRow ? " (whole row)" : "");
            }
            if (!PartArgs.IsPart(id)) return sb.ToString();
            var a = level.ArgsAt(Row, Lane, id);
            bool saved = level.PartAt(Row, Lane, id) != null;
            sb.Append(saved ? "  ·  own ARGS" : "  ·  default ARGS");
            var kind = a.Kind;
            void Add(PartArgs.Field f, string label, string value)
            {
                if (PartArgs.Uses(kind, f)) sb.Append('\n').Append(label).Append(": ").Append(value);
            }
            Add(PartArgs.Field.Group, "Group", a.group > 0 ? a.group.ToString() : "none");
            Add(PartArgs.Field.WholeRow, "Whole row", a.wholeRow ? "on" : "off");
            if (PartArgs.Uses(kind, PartArgs.Field.MoveX)) sb.Append($"\nMove X {F(a.moveX)}  Y {F(a.moveY)}  Z {F(a.moveZ)}");
            Add(PartArgs.Field.Spin, "Spin", F(a.spin) + "°");
            Add(PartArgs.Field.WithBall, "With ball", a.withBall ? "on" : "off");
            Add(PartArgs.Field.Speed, "Speed", a.speed > 0f ? F(a.speed) + " tiles/s" : "0 (Seconds)");
            Add(PartArgs.Field.Keep, "Keep", a.keep ? "on" : $"off (gone {F(a.vanish)} s after the ball)");
            Add(PartArgs.Field.Kill, "Kill", a.kill ? "on" : "off (safe, pickups ride)");
            Add(PartArgs.Field.Distance, "Distance", F(a.distance));
            Add(PartArgs.Field.Multiple, "Speed Multiple", F(a.multiple));
            Add(PartArgs.Field.MusicOffset, "Music offset", a.musicOffset ? "on" : "off");
            Add(PartArgs.Field.Height, "Height", F(a.height));
            Add(PartArgs.Field.Pattern, "Hops", a.pattern);
            Add(PartArgs.Field.Frames, "Idle frames", a.frames <= 1 ? "1 (still)" : a.frames.ToString());
            Add(PartArgs.Field.Seconds, "Seconds", PartArgs.IsTriggerObject(kind) && a.seconds <= 0f ? "0 (as made)" : F(a.seconds));
            Add(PartArgs.Field.Loop, "Loop", a.loop ? "on" : "off");
            Add(PartArgs.Field.Text, "Text", a.text);
            Add(PartArgs.Field.StartRows, "Start rows", F(a.startRows));
            return sb.ToString();
        }

        static string GroupNames(LevelData level, string list)
        {
            var names = new List<string>();
            foreach (int g in EventGroups.Parse(list)) names.Add(level.GroupLabel(g));
            return string.Join(", ", names);
        }

        static string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
