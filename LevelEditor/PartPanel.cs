using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RTXOn
{
    /// <summary>
    /// The level editor's ARGS-Setting window (side panel button EDIT ARGS): the settings new tiles of the selected
    /// part get when painted (<see cref="PartArgs"/>), e.g. a Speed Up's Speed Multiple, Distance and "Incorporate
    /// music offset calculation". Only the fields the part uses are shown. Tiles already in the grid keep theirs until
    /// they are painted again. Reset puts the defaults back.
    /// </summary>
    public class PartPanel : MonoBehaviour
    {
        static Color PanelColor => EditorColors.Window;
        static Color ButtonColor => EditorColors.Button;
        static Color FieldColor => EditorColors.Field;
        static Color SoftText => EditorColors.Soft;
        static Color OnColor => EditorColors.Accent;
        const float RowStep = 66f;

        LevelEditor editor;
        RectTransform rootRect;
        PartArgs args;
        bool brush; // editing the settings for new tiles, not one cell
        Text title, help;
        Image colorBar;

        class Row
        {
            public PartArgs.Field field;
            public RectTransform rect;
            public InputField input;
            public Text toggle;
            public Func<bool> on;   // toggles
            public Slider slider;   // Frames
            public Text sliderValue;
        }
        readonly List<Row> rows = new List<Row>();

        public bool IsOpen => gameObject.activeSelf;
        /// <summary>The cell being edited, or null.</summary>
        public PartArgs Args => IsOpen ? args : null;

        public static PartPanel Build(LevelEditor owner, Transform parent, Vector2 centre)
        {
            var bg = UiFactory.Round(UiFactory.MakePanel(parent, "ARGS Panel", PanelColor, true), 1f); // catches clicks, nothing paints under it
            UiFactory.Place(bg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), centre, new Vector2(600f, 640f));
            var panel = bg.gameObject.AddComponent<PartPanel>();
            panel.editor = owner;
            panel.rootRect = (RectTransform)bg.transform;
            panel.BuildUi(panel.rootRect);
            bg.gameObject.SetActive(false);
            return panel;
        }

        void BuildUi(RectTransform root)
        {
            colorBar = UiFactory.MakePanel(root, "Colour", Color.white);
            UiFactory.Place(colorBar.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(600f, 8f));
            title = UiFactory.MakeLabel(root, "", 26, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            UiFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -14f), new Vector2(490f, 50f));
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 14;
            title.resizeTextMaxSize = 26;
            var close = UiFactory.MakeButton(root, "X", ButtonColor, Close, 26);
            UiFactory.IconOnly(close, "Close", 26f);
            UiFactory.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -14f), new Vector2(50f, 46f));

            // The order the rows show in (only the ones the part uses).
            Number(root, PartArgs.Field.Group, "Group (EnemyGroupID)", v => args.group = Mathf.Clamp(Mathf.RoundToInt(v), 0, PartArgs.MaxGroup));
            Toggle(root, PartArgs.Field.StartUp, "Start as GateUp", () => args.startUp = !args.startUp, () => args.startUp);
            Toggle(root, PartArgs.Field.WholeRow, "Whole row (any lane, in the air too)", () => args.wholeRow = !args.wholeRow, () => args.wholeRow);
            Number(root, PartArgs.Field.MoveX, "Move X (tiles, + = right)", v => args.moveX = Mathf.Clamp(v, -500f, 500f));
            Number(root, PartArgs.Field.MoveY, "Move Y (tiles, + = up)", v => args.moveY = Mathf.Clamp(v, -100f, 100f));
            Number(root, PartArgs.Field.MoveZ, "Move Z (tiles, + = forward)", v => args.moveZ = Mathf.Clamp(v, -500f, 500f));
            Number(root, PartArgs.Field.Spin, "Spin (degrees)", v => args.spin = Mathf.Clamp(v, -3600f, 3600f));
            Toggle(root, PartArgs.Field.WithBall, "With ball (as fast as the ball)", () => args.withBall = !args.withBall, () => args.withBall);
            Number(root, PartArgs.Field.Speed, "Speed (tiles/s, 0 = Seconds)", v => args.speed = Mathf.Clamp(v, 0f, 100f));
            Number(root, PartArgs.Field.Distance, "Distance (rows)", v => args.distance = Mathf.Clamp(v, 0.5f, 500f));
            Number(root, PartArgs.Field.Multiple, "Speed Multiple", v => args.multiple = Mathf.Clamp(v, 0.1f, 5f));
            Toggle(root, PartArgs.Field.MusicOffset, "Incorporate music offset calculation", () => args.musicOffset = !args.musicOffset, () => args.musicOffset);
            Number(root, PartArgs.Field.Height, "Height", v => args.height = Mathf.Clamp(v, 0.1f, 20f));
            TextField(root, PartArgs.Field.Pattern, "Hops (rows, e.g. 3,4,3)", t => args.pattern = t);
            FramesSlider(root, PartArgs.Field.Frames, "Idle frames (1 = still)");
            Number(root, PartArgs.Field.Seconds, "Seconds", v => args.seconds = PartArgs.HasShortSeconds(args.Kind)
                ? Mathf.Clamp(v, PartArgs.MinShortSeconds, PartArgs.MaxShortSeconds)
                : PartArgs.IsTriggerObject(args.Kind) && v <= 0f ? 0f : Mathf.Clamp(v, 0.05f, 60f)); // Trigger Object: 0 = as made
            Toggle(root, PartArgs.Field.Loop, "Loop", () => args.loop = !args.loop, () => args.loop);
            WordsField(root, PartArgs.Field.Text, "Text (bubble above the ball)", t => args.text = t);
            Number(root, PartArgs.Field.StartRows, "Start rows", v => args.startRows = Mathf.Clamp(v, 0f, 100f));
            Toggle(root, PartArgs.Field.Keep, "Keep (off = gone after the ball)", () => args.keep = !args.keep, () => args.keep);
            Number(root, PartArgs.Field.Vanish, "Vanish after ball (1-3 s)", v => args.vanish = Mathf.Clamp(v, 1f, 3f));
            Toggle(root, PartArgs.Field.Kill, "Kill (off = safe, pickups ride)", () => args.kill = !args.kill, () => args.kill);

            help = UiFactory.MakeLabel(root, "", 17, TextAnchor.UpperLeft, SoftText);
            UiFactory.Place(help.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(20f, 80f), new Vector2(560f, 160f));

            var reset = UiFactory.MakeButton(root, "Reset", ButtonColor, ResetArgs, 24);
            UiFactory.Place((RectTransform)reset.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(20f, 16f), new Vector2(150f, 48f));
            var done = UiFactory.MakeButton(root, "Done", new Color(0.12f, 0.5f, 0.2f), Close, 24);
            UiFactory.Place((RectTransform)done.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-20f, 16f), new Vector2(150f, 48f));
        }

        // ---------------- Open / close ----------------

        /// <summary>Opens on a cell's settings, or (<paramref name="forBrush"/>) on the settings new tiles get.</summary>
        public void Open(PartArgs cell, bool forBrush = false)
        {
            args = cell;
            brush = forBrush;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Show();
        }

        public void Close()
        {
            if (!IsOpen) return;
            gameObject.SetActive(false);
            args = null;
            editor.PartChanged();
        }

        /// <summary>Closes without telling the editor (the level it edited was replaced).</summary>
        public void Hide()
        {
            gameObject.SetActive(false);
            args = null;
        }

        void ResetArgs()
        {
            args.CopyValuesFrom(PartArgs.Defaults(args.id, args.row, args.lane));
            Changed();
        }

        /// <summary>True while it edits the settings for new tiles.</summary>
        public bool IsBrush => IsOpen && brush;

        void Changed()
        {
            Show();
            if (brush) editor.BrushChanged();
            else editor.PartChanged(true, args);
        }

        /// <summary>Puts the cell's values in the fields and shows only the fields its part uses.</summary>
        void Show()
        {
            if (args == null) return;
            var kind = args.Kind;
            string where = brush ? "NEW TILES" : $"ROW {args.row}";
            title.text = $"ARGS-SETTING  ·  {TilePalette.Name(args.id).ToUpperInvariant()}  ·  {where}";
            colorBar.color = TilePalette.ColorOf(args.id);
            help.text = PartArgs.Help(kind) + (brush ? "\nTiles you paint now get these settings; tiles already placed keep theirs until painted again." : "");
            float y = -80f;
            int shown = 0;
            foreach (var r in rows) if (PartArgs.Uses(kind, r.field)) shown++;
            // Tall enough for every row it shows, the help and the buttons.
            // Many rows (Custom Move Block has 13): closer together, so the window still fits a 1080 screen.
            bool compact = shown > 10;
            float step = compact ? 55f : RowStep;
            rootRect.sizeDelta = new Vector2(600f, Mathf.Max(640f, 100f + shown * step + (compact ? 240f : 260f)));
            foreach (var r in rows)
            {
                bool used = PartArgs.Uses(kind, r.field);
                r.rect.gameObject.SetActive(used);
                if (!used) continue;
                r.rect.anchoredPosition = new Vector2(0f, y);
                r.rect.sizeDelta = new Vector2(600f, compact ? 50f : 56f);
                y -= step;
                switch (r.field)
                {
                    case PartArgs.Field.Distance: Set(r.input, args.distance); break;
                    case PartArgs.Field.Multiple: Set(r.input, args.multiple); break;
                    case PartArgs.Field.Height: Set(r.input, args.height); break;
                    case PartArgs.Field.Seconds: Set(r.input, args.seconds); break;
                    case PartArgs.Field.StartRows: Set(r.input, args.startRows); break;
                    case PartArgs.Field.Group: Set(r.input, args.group); break;
                    case PartArgs.Field.MoveX: Set(r.input, args.moveX); break;
                    case PartArgs.Field.MoveY: Set(r.input, args.moveY); break;
                    case PartArgs.Field.MoveZ: Set(r.input, args.moveZ); break;
                    case PartArgs.Field.Spin: Set(r.input, args.spin); break;
                    case PartArgs.Field.Speed: Set(r.input, args.speed); break;
                    case PartArgs.Field.Vanish: Set(r.input, args.vanish); break;
                    case PartArgs.Field.Frames:
                        showing = true;
                        r.slider.value = args.frames;
                        showing = false;
                        r.sliderValue.text = args.frames.ToString();
                        break;
                    case PartArgs.Field.Pattern:
                        if (!r.input.isFocused) r.input.text = args.pattern;
                        break;
                    case PartArgs.Field.Text:
                        if (!r.input.isFocused) r.input.text = args.text;
                        break;
                    default:
                        if (r.toggle == null) break;
                        bool on = r.on();
                        r.toggle.text = on ? "ON" : "OFF";
                        r.toggle.transform.parent.GetComponent<Image>().color = on ? OnColor : FieldColor;
                        break;
                }
            }
        }

        static void Set(InputField field, float value)
        {
            if (!field.isFocused) field.text = value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        // ---------------- Building blocks ----------------

        Row MakeRow(RectTransform parent, PartArgs.Field field, string name)
        {
            var rect = UiFactory.MakeRect(parent, name);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(600f, 56f);
            var back = UiFactory.MakePanel(rect, "Back", new Color(1f, 1f, 1f, 0.05f));
            UiFactory.Stretch(back.rectTransform, 14f, 0f, 14f, 0f);
            var label = UiFactory.MakeLabel(rect, name, 22, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            UiFactory.Place(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(28f, 0f), new Vector2(330f, 56f));
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 14;
            label.resizeTextMaxSize = 22;
            var row = new Row { field = field, rect = rect };
            rows.Add(row);
            return row;
        }

        void Number(RectTransform parent, PartArgs.Field field, string name, Action<float> onValue)
        {
            var row = MakeRow(parent, field, name);
            row.input = UiFactory.MakeInputField(row.rect, 24, FieldColor, InputField.ContentType.DecimalNumber);
            UiFactory.Place((RectTransform)row.input.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(380f, 0f), new Vector2(190f, 48f));
            row.input.onEndEdit.AddListener(text =>
            {
                if (args == null) return;
                if (TryParse(text, out float v)) { onValue(v); Changed(); }
                else Show();
            });
        }

        void TextField(RectTransform parent, PartArgs.Field field, string name, Action<string> onValue)
        {
            var row = MakeRow(parent, field, name);
            row.input = UiFactory.MakeInputField(row.rect, 24, FieldColor, InputField.ContentType.Standard);
            UiFactory.Place((RectTransform)row.input.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(380f, 0f), new Vector2(190f, 48f));
            row.input.onEndEdit.AddListener(text =>
            {
                if (args == null) return;
                var old = args.pattern;
                onValue(text.Trim());
                if (args.Hops().Count == 0) args.pattern = old; // nothing usable typed: keep the old hops
                Changed();
            });
        }

        /// <summary>A free text field (Dialogue's words): empty keeps the old text.</summary>
        void WordsField(RectTransform parent, PartArgs.Field field, string name, Action<string> onValue)
        {
            var row = MakeRow(parent, field, name);
            row.input = UiFactory.MakeInputField(row.rect, 24, FieldColor, InputField.ContentType.Standard);
            row.input.characterLimit = PartArgs.MaxDialogueLength;
            UiFactory.Place((RectTransform)row.input.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(380f, 0f), new Vector2(190f, 48f));
            row.input.onEndEdit.AddListener(text =>
            {
                if (args == null) return;
                if (!string.IsNullOrWhiteSpace(text)) onValue(text.Trim());
                Changed();
            });
        }

        bool showing; // Show is moving the slider: not a change

        /// <summary>Idle frames: a slider 1-60 with the number beside it.</summary>
        void FramesSlider(RectTransform parent, PartArgs.Field field, string name)
        {
            var row = MakeRow(parent, field, name);
            row.slider = UiFactory.MakeHorizontalSlider(row.rect, FieldColor, OnColor, Color.white, 1f, 60f);
            row.slider.wholeNumbers = true;
            UiFactory.Place((RectTransform)row.slider.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(370f, 0f), new Vector2(150f, 30f));
            row.sliderValue = UiFactory.MakeLabel(row.rect, "", 22, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            UiFactory.Place(row.sliderValue.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(528f, 0f), new Vector2(56f, 48f));
            row.slider.onValueChanged.AddListener(v =>
            {
                if (args == null || showing) return;
                args.frames = Mathf.Clamp(Mathf.RoundToInt(v), 1, 60);
                row.sliderValue.text = args.frames.ToString();
                if (brush) editor.BrushChanged();
                else editor.PartChanged(true); // no preview per step of the drag
            });
        }

        void Toggle(RectTransform parent, PartArgs.Field field, string name, UnityAction flip, Func<bool> on)
        {
            var row = MakeRow(parent, field, name);
            row.on = on;
            var box = UiFactory.MakeButton(row.rect, "", FieldColor, () =>
            {
                if (args == null) return;
                flip();
                Changed();
            }, 22);
            UiFactory.Place((RectTransform)box.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(440f, 0f), new Vector2(90f, 48f));
            row.toggle = box.GetComponentInChildren<Text>();
        }

        static bool TryParse(string text, out float value) =>
            float.TryParse((text ?? "").Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
