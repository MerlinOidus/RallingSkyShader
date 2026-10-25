using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RTXOn
{
    /// <summary>
    /// The level editor's trigger window, opened by clicking a row with the Theme or Camera trigger tool.
    /// <list type="bullet">
    /// <item>Theme trigger: environment (Day, Sunset, Night... of the row's look) with its blend rows, and the look
    /// (TrackTheme) with its fade rows ("Keep" = no change), and Sun angle / Moon angle: turn the sun or the moon
    /// (each its own way) from this row on, also with Environment "Keep".</item>
    /// <item>Camera trigger: position and angles of the game camera (relative to the ball), Offset rows of the move,
    /// its Curve, and the presets Default, High and Far. In the 3D view the picture shows that camera while open.</item>
    /// <item>Screen FX trigger: the effect (Glitch, Grayscale, Negative, Chromatic, Scan Lines, Hue Shift), its
    /// Strength (0 = off), Fade rows, Stay rows (0 = until the next trigger of that effect, above 0 = a burst) and
    /// Speed. The 3D view shows the effect while the window is open.</item>
    /// </list>
    /// Every change is written into the level at once; Delete removes the trigger.
    /// </summary>
    public class TriggerPanel : MonoBehaviour
    {
        static Color PanelColor => EditorColors.Window;
        static Color ButtonColor => EditorColors.Button;
        static Color FieldColor => EditorColors.Field;
        static Color DeleteColor => EditorColors.Danger;

        LevelEditor editor;
        Text title;
        GameObject themePage, cameraPage, filterPage;
        ThemeChange theme;
        new CameraChange camera; // "new": Unity's old Component.camera has the same name
        FilterChange filter;

        // Theme page
        Text environmentLabel, lookLabel, objectsLabel;
        InputField blendRows, lookRows;
        Text sunMode, moonMode;
        readonly InputField[] sunAngle = new InputField[2];
        readonly InputField[] moonAngle = new InputField[2];
        // Camera page
        readonly InputField[] position = new InputField[3];
        readonly InputField[] rotation = new InputField[3];
        InputField offsetRows, curve;
        // Screen FX page
        Text effectLabel;
        InputField strength, fadeRows, stayRows, speed;

        public bool IsOpen => gameObject.activeSelf;
        public CameraChange Camera => IsOpen ? camera : null;
        public FilterChange Filter => IsOpen ? filter : null;

        public static TriggerPanel Build(LevelEditor owner, Transform parent, Vector2 centre)
        {
            var bg = UiFactory.Round(UiFactory.MakePanel(parent, "Trigger Panel", PanelColor, true), 1f); // catches clicks, nothing paints under it
            UiFactory.Place(bg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), centre, new Vector2(560f, 700f));
            var panel = bg.gameObject.AddComponent<TriggerPanel>();
            panel.editor = owner;
            panel.BuildUi((RectTransform)bg.transform);
            bg.gameObject.SetActive(false);
            return panel;
        }

        void BuildUi(RectTransform root)
        {
            title = UiFactory.MakeLabel(root, "", 28, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            UiFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -10f), new Vector2(440f, 50f));
            var close = UiFactory.MakeButton(root, "X", ButtonColor, Close, 26);
            UiFactory.IconOnly(close, "Close", 26f);
            UiFactory.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -12f), new Vector2(50f, 46f));

            themePage = UiFactory.MakeRect(root, "Theme").gameObject;
            UiFactory.Stretch((RectTransform)themePage.transform, 0f, 70f, 0f, 70f);
            var t = (RectTransform)themePage.transform;
            environmentLabel = Chooser(t, -10f, "Environment", () => CycleEnvironment(-1), () => CycleEnvironment(1));
            blendRows = Field(t, -70f, "Blend rows", v => { theme.rows = Mathf.Max(0f, v); Changed(); });
            lookLabel = Chooser(t, -140f, "Theme set", () => CycleLook(-1), () => CycleLook(1));
            objectsLabel = Chooser(t, -200f, "Objects", () => CycleObjects(-1), () => CycleObjects(1));
            lookRows = Field(t, -260f, "Fade rows", v => { theme.lookRows = Mathf.Max(0, Mathf.RoundToInt(v)); Changed(); });
            sunMode = AngleRow(t, -322f, "Sun angle", sunAngle, () => { theme.turnSun = !theme.turnSun; Changed(); },
                (i, v) => { var a = theme.sun; a[i] = v; theme.sun = a; theme.turnSun = true; Changed(); });
            moonMode = AngleRow(t, -382f, "Moon angle", moonAngle, () => { theme.turnMoon = !theme.turnMoon; Changed(); },
                (i, v) => { var a = theme.moon; a[i] = v; theme.moon = a; theme.turnMoon = true; Changed(); });
            var hint = UiFactory.MakeLabel(t, "Keep = no change. Theme set = tiles, colours, background (Official or Custom).\nObjects = enemy models from another theme (Own = the set's own).\nFade rows 2 = smooth, 0 = instant.\nAngle X = height (0-90), Y = direction (0 = ahead, 180 = behind).\nAuto = the environment's own sun / moon.",
                17, TextAnchor.UpperLeft, new Color(0.8f, 0.76f, 0.82f));
            UiFactory.Place(hint.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -436f), new Vector2(520f, 110f));

            cameraPage = UiFactory.MakeRect(root, "Camera").gameObject;
            UiFactory.Stretch((RectTransform)cameraPage.transform, 0f, 70f, 0f, 70f);
            var c = (RectTransform)cameraPage.transform;
            Vector3Row(c, -10f, "Position", position, (i, v) => { var p = camera.position; p[i] = v; camera.position = p; Changed(); });
            Vector3Row(c, -70f, "Rotation", rotation, (i, v) => { var r = camera.rotation; r[i] = v; camera.rotation = r; Changed(); });
            offsetRows = Field(c, -130f, "Offset rows", v => { camera.rows = Mathf.Max(0f, v); Changed(); });
            curve = Field(c, -190f, "Curve (-1..1)", v => { camera.curve = Mathf.Clamp(v, -1f, 1f); Changed(); });
            string[] presets = { "Default", "High", "Far" };
            for (int i = 0; i < presets.Length; i++)
            {
                int index = i;
                var b = UiFactory.MakeButton(c, presets[i], ButtonColor, () => ApplyPreset(index), 22);
                UiFactory.Place((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f + i * 130f, -250f), new Vector2(120f, 44f));
            }

            filterPage = UiFactory.MakeRect(root, "Screen FX").gameObject;
            UiFactory.Stretch((RectTransform)filterPage.transform, 0f, 70f, 0f, 70f);
            var f = (RectTransform)filterPage.transform;
            effectLabel = Chooser(f, -10f, "Effect", () => CycleEffect(-1), () => CycleEffect(1));
            strength = Field(f, -70f, "Strength (0-1)", v => { filter.strength = Mathf.Clamp01(v); Changed(); });
            fadeRows = Field(f, -130f, "Fade rows", v => { filter.fade = Mathf.Max(0f, v); Changed(); });
            stayRows = Field(f, -190f, "Stay rows", v => { filter.rows = Mathf.Max(0f, v); Changed(); });
            speed = Field(f, -250f, "Speed", v => { filter.speed = Mathf.Max(0f, v); Changed(); });
            string[] quick = { "On", "Off", "Burst" };
            for (int i = 0; i < quick.Length; i++)
            {
                int index = i;
                var b = UiFactory.MakeButton(f, quick[i], ButtonColor, () => ApplyQuick(index), 22);
                UiFactory.Place((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f + i * 130f, -310f), new Vector2(120f, 44f));
            }
            var fxHint = UiFactory.MakeLabel(f, "Strength 0 = turns the effect off. Stay rows 0 = stays on,\nabove 0 = fades out again after those rows (a burst).\nSpeed: Glitch jumps, Scan Lines scroll, Hue Shift turns (0 = fixed).\nEffects add up: Grayscale on one row, Scan Lines on the next.",
                17, TextAnchor.UpperLeft, new Color(0.8f, 0.76f, 0.82f));
            UiFactory.Place(fxHint.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -366f), new Vector2(520f, 90f));

            var delete = UiFactory.MakeButton(root, "Delete", DeleteColor, Delete, 24);
            UiFactory.Place((RectTransform)delete.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(20f, 16f), new Vector2(150f, 48f));
            var done = UiFactory.MakeButton(root, "Done", new Color(0.12f, 0.5f, 0.2f), Close, 24);
            UiFactory.Place((RectTransform)done.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-20f, 16f), new Vector2(150f, 48f));
        }

        // ---------------- Open / close ----------------

        public void OpenTheme(ThemeChange change)
        {
            theme = change;
            camera = null;
            filter = null;
            themePage.SetActive(true);
            cameraPage.SetActive(false);
            filterPage.SetActive(false);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Show();
        }

        public void OpenCamera(CameraChange change)
        {
            camera = change;
            theme = null;
            filter = null;
            themePage.SetActive(false);
            cameraPage.SetActive(true);
            filterPage.SetActive(false);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Show();
            editor.TriggerChanged(); // the 3D view shows this camera
        }

        public void OpenFilter(FilterChange change)
        {
            filter = change;
            theme = null;
            camera = null;
            themePage.SetActive(false);
            cameraPage.SetActive(false);
            filterPage.SetActive(true);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Show();
            editor.TriggerChanged(); // the 3D view previews the effect
        }

        public void Close()
        {
            if (!IsOpen) return;
            gameObject.SetActive(false);
            theme = null;
            camera = null;
            filter = null;
            editor.TriggerChanged();
        }

        /// <summary>Closes without telling the editor (the level it edited was replaced).</summary>
        public void Hide()
        {
            gameObject.SetActive(false);
            theme = null;
            camera = null;
            filter = null;
        }

        void Delete()
        {
            if (theme != null) editor.DeleteTrigger(theme);
            if (camera != null) editor.DeleteTrigger(camera);
            if (filter != null) editor.DeleteTrigger(filter);
            Close();
        }

        void Changed()
        {
            Show();
            editor.TriggerChanged(true);
        }

        /// <summary>Puts the trigger's values in the fields.</summary>
        void Show()
        {
            if (theme != null)
            {
                title.text = $"THEME TRIGGER  ·  ROW {theme.row}";
                environmentLabel.text = string.IsNullOrEmpty(theme.preset) ? "Keep" : theme.preset;
                lookLabel.text = string.IsNullOrEmpty(theme.look) ? "Keep" : theme.look;
                objectsLabel.text = string.IsNullOrEmpty(theme.objects) ? "Keep" : theme.objects == ThemeChange.OwnObjects ? "Own (the set's)" : theme.objects;
                Set(blendRows, theme.rows);
                Set(lookRows, theme.lookRows);
                lookRows.interactable = !string.IsNullOrEmpty(theme.look) || !string.IsNullOrEmpty(theme.objects);
                blendRows.interactable = !string.IsNullOrEmpty(theme.preset) || theme.turnSun || theme.turnMoon;
                sunMode.text = theme.turnSun ? "Set" : "Auto";
                moonMode.text = theme.turnMoon ? "Set" : "Auto";
                for (int i = 0; i < 2; i++)
                {
                    Set(sunAngle[i], theme.sun[i]);
                    Set(moonAngle[i], theme.moon[i]);
                }
            }
            if (camera != null)
            {
                title.text = $"CAMERA TRIGGER  ·  ROW {camera.row}";
                for (int i = 0; i < 3; i++)
                {
                    Set(position[i], camera.position[i]);
                    Set(rotation[i], camera.rotation[i]);
                }
                Set(offsetRows, camera.rows);
                Set(curve, camera.curve);
            }
            if (filter != null)
            {
                title.text = $"SCREEN FX  ·  ROW {filter.row}";
                effectLabel.text = ScreenFilters.NameOf(filter.Effect);
                Set(strength, filter.strength);
                Set(fadeRows, filter.fade);
                Set(stayRows, filter.rows);
                Set(speed, filter.speed);
                var e = filter.Effect;
                speed.interactable = e == ScreenEffect.Glitch || e == ScreenEffect.ScanLines || e == ScreenEffect.HueShift;
            }
        }

        static void Set(InputField field, float value)
        {
            if (!field.isFocused) field.text = value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        // ---------------- Theme ----------------

        void CycleEnvironment(int step)
        {
            var names = new List<string> { "" };
            names.AddRange(editor.EnvironmentNames(theme.row));
            theme.preset = Cycle(names, theme.preset, step);
            Changed();
        }

        void CycleLook(int step)
        {
            var names = new List<string> { "" };
            names.AddRange(editor.LookNames());
            theme.look = Cycle(names, theme.look, step);
            Changed();
        }

        void CycleObjects(int step)
        {
            var names = new List<string> { "", ThemeChange.OwnObjects };
            names.AddRange(editor.LookNames());
            theme.objects = Cycle(names, theme.objects, step);
            Changed();
        }

        static string Cycle(List<string> names, string current, int step)
        {
            int index = names.FindIndex(n => string.Equals(n, current ?? "", StringComparison.OrdinalIgnoreCase));
            if (index < 0) index = 0;
            return names[(index + step + names.Count) % names.Count];
        }

        // ---------------- Camera ----------------

        void ApplyPreset(int index)
        {
            var p = index == 1 ? CameraChange.High(camera.row) : index == 2 ? CameraChange.Far(camera.row) : CameraChange.Default(camera.row);
            camera.position = p.position;
            camera.rotation = p.rotation;
            Changed();
        }

        // ---------------- Screen FX ----------------

        void CycleEffect(int step)
        {
            filter.Effect = (ScreenEffect)(((int)filter.Effect + step + ScreenFilters.Count) % ScreenFilters.Count);
            Changed();
        }

        /// <summary>On: full and stays. Off: back to nothing. Burst: full for 6 rows, then fades out.</summary>
        void ApplyQuick(int index)
        {
            filter.strength = index == 1 ? 0f : 1f;
            filter.rows = index == 2 ? 6f : 0f;
            if (index == 2 && filter.fade > 1f) filter.fade = 1f;
            Changed();
        }

        // ---------------- Building blocks ----------------

        Text Chooser(RectTransform parent, float y, string name, UnityAction left, UnityAction right)
        {
            Label(parent, y, name);
            var l = UiFactory.MakeButton(parent, "<", ButtonColor, left, 28);
            UiFactory.Place((RectTransform)l.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(230f, y), new Vector2(50f, 50f));
            var r = UiFactory.MakeButton(parent, ">", ButtonColor, right, 28);
            UiFactory.Place((RectTransform)r.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(490f, y), new Vector2(50f, 50f));
            var value = UiFactory.MakeLabel(parent, "", 24, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            UiFactory.Place(value.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(285f, y), new Vector2(200f, 50f));
            value.resizeTextForBestFit = true;
            value.resizeTextMinSize = 12;
            value.resizeTextMaxSize = 24;
            return value;
        }

        InputField Field(RectTransform parent, float y, string name, Action<float> onValue)
        {
            Label(parent, y, name);
            var f = UiFactory.MakeInputField(parent, 24, FieldColor, InputField.ContentType.DecimalNumber);
            UiFactory.Place((RectTransform)f.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(230f, y), new Vector2(140f, 48f));
            f.onEndEdit.AddListener(text => { if (TryParse(text, out float v)) onValue(v); else Show(); });
            return f;
        }

        void Vector3Row(RectTransform parent, float y, string name, InputField[] fields, Action<int, float> onValue)
        {
            Label(parent, y, name);
            string[] axes = { "X", "Y", "Z" };
            for (int i = 0; i < 3; i++)
            {
                int axis = i;
                var a = UiFactory.MakeLabel(parent, axes[i], 20, TextAnchor.MiddleRight, new Color(0.8f, 0.76f, 0.82f), FontStyle.Bold);
                UiFactory.Place(a.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(160f + i * 106f, y), new Vector2(26f, 48f));
                var f = UiFactory.MakeInputField(parent, 22, FieldColor, InputField.ContentType.DecimalNumber);
                UiFactory.Place((RectTransform)f.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(190f + i * 106f, y), new Vector2(72f, 48f));
                f.onEndEdit.AddListener(text => { if (TryParse(text, out float v)) onValue(axis, v); else Show(); });
                fields[i] = f;
            }
        }

        /// <summary>"Name [Auto/Set] X [ ] Y [ ]": a sun or moon angle. Typing a number switches it to Set.</summary>
        Text AngleRow(RectTransform parent, float y, string name, InputField[] fields, UnityAction toggle, Action<int, float> onValue)
        {
            Label(parent, y, name);
            var mode = UiFactory.MakeButton(parent, "Auto", ButtonColor, toggle, 22);
            UiFactory.Place((RectTransform)mode.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(200f, y), new Vector2(90f, 48f));
            string[] axes = { "X", "Y" };
            for (int i = 0; i < 2; i++)
            {
                int axis = i;
                var a = UiFactory.MakeLabel(parent, axes[i], 20, TextAnchor.MiddleRight, new Color(0.8f, 0.76f, 0.82f), FontStyle.Bold);
                UiFactory.Place(a.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(300f + i * 122f, y), new Vector2(26f, 48f));
                var f = UiFactory.MakeInputField(parent, 22, FieldColor, InputField.ContentType.DecimalNumber);
                UiFactory.Place((RectTransform)f.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(330f + i * 122f, y), new Vector2(84f, 48f));
                f.onEndEdit.AddListener(text => { if (TryParse(text, out float v)) onValue(axis, v); else Show(); });
                fields[i] = f;
            }
            return mode.GetComponentInChildren<Text>();
        }

        static void Label(RectTransform parent, float y, string name)
        {
            var label = UiFactory.MakeLabel(parent, name, 22, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            UiFactory.Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, y), new Vector2(200f, 48f));
        }

        static bool TryParse(string text, out float value) =>
            float.TryParse((text ?? "").Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
