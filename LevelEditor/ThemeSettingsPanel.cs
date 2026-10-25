using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RTXOn
{
    /// <summary>
    /// The level editor's THEME SETTINGS window (top bar > Theme), like the fan editor's: the level's theme (click the
    /// background picture: Official / Fanmade / Custom themes, Import and + for a new Custom Theme), the background's
    /// Offset and Gradient, Roller, Block, Mover Edge and Midground taken from another theme, and the Model and Texture
    /// looks (Classic / RTX). Right: a 3D preview
    /// of the level from a game-like camera. Every change goes into the level at once (Theme Settings in level.json).
    /// </summary>
    public class ThemeSettingsPanel : PanelKit
    {
        const float WindowWidth = 1560f, WindowHeight = 900f;
        const float PreviewWidth = 720f, PreviewHeight = 560f;
        static readonly string[] GradientNames = { "Default", "None", "Bottom", "Top", "Both" };
        static readonly string[] PickerTabs = { "Official", "Fanmade", "Custom" };

        RawImage thumb, preview;
        AspectRatioFitter previewFit;
        Text thumbMissing, themeName, themeInfo;
        Button editButton;
        int seenVersion = -1;

        // Theme picker
        RectTransform picker, pickerList;
        readonly Image[] pickerTabButtons = new Image[PickerTabs.Length];
        readonly List<GameObject> pickerItems = new List<GameObject>();
        Button importButton;
        int pickerTab;

        LevelThemeSettings Settings => editor.Level.ThemeSettings;

        /// <summary>Model / Texture row text: "Default (RTX)" for the theme's own (-1), else "Classic" or "RTX".</summary>
        static string LookName(int value, TileLook own) =>
            value < 0 ? "Default (" + own + ")" : ((TileLook)value).ToString();

        /// <summary>Default (-1) > RTX (0) > Classic (1) > Default...</summary>
        static int NextLook(int value, int step) => (value + 1 + step + 3) % 3 - 1;

        public static ThemeSettingsPanel Build(LevelEditor owner, Transform parent)
        {
            var bg = UiFactory.MakePanel(parent, "Theme Settings", PanelColor, true);
            UiFactory.Place(bg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(WindowWidth, WindowHeight));
            var panel = bg.gameObject.AddComponent<ThemeSettingsPanel>();
            panel.editor = owner;
            panel.BuildUi((RectTransform)bg.transform);
            bg.gameObject.SetActive(false);
            return panel;
        }

        protected override bool Editable => editor != null && editor.Level != null;

        // ---------------- Open / close ----------------

        public void Open()
        {
            if (editor.Level.ThemeSettings == null) editor.Level.ThemeSettings = new LevelThemeSettings();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            picker.gameObject.SetActive(false);
            ClosePopups();
            editor.UpdateThemePreview();
            preview.texture = editor.ThemePreviewTexture;
            if (preview.texture != null) previewFit.aspectRatio = (float)preview.texture.width / Mathf.Max(1, preview.texture.height);
            ShowAll();
            Status("Click the background picture to pick the level's theme. Changes go into the level at once (Save writes them).");
        }

        public void Close()
        {
            if (!IsOpen) return;
            if (ClosePopups()) return;
            if (picker.gameObject.activeSelf)
            {
                picker.gameObject.SetActive(false);
                return;
            }
            Hide();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            if (editor != null) editor.UpdateThemePreview();
        }

        void Update()
        {
            // A Custom Theme was saved, deleted or imported: show it.
            if (seenVersion != ThemeLibrary.Version && Editable) ShowAll();
        }

        protected override void Changed()
        {
            Settings.Normalize();
            editor.ThemesChanged();
            ShowAll();
        }

        void ShowAll()
        {
            seenVersion = ThemeLibrary.Version;
            Refresh();
            var look = editor.CurrentLook();
            var def = ThemeLibrary.Get(editor.Level.Look);
            var tex = def != null ? ThemeLibrary.Thumbnail(def, editor.Looks) : look != null ? look.background : null;
            thumb.texture = tex;
            thumb.color = tex != null ? Color.white : FieldColor;
            thumbMissing.gameObject.SetActive(tex == null);
            themeName.text = string.IsNullOrEmpty(editor.Level.Look)
                ? $"Default ({(editor.DefaultTheme != null ? editor.DefaultTheme.name : "none")})"
                : editor.Level.Look + (look == null || (def == null && ThemeLibrary.Official(editor.Level.Look, editor.Looks) == null) ? " (not found)" : "");
            themeInfo.text = def != null
                ? $"{(def.imported ? "Fanmade" : "Custom")} theme on {(string.IsNullOrEmpty(def.baseTheme) ? "the first theme" : def.baseTheme)}, {(def.mode == TileLook.Classic ? "Classic" : "RTX ON")} tiles" +
                  (string.IsNullOrEmpty(def.author) ? "" : $", by {def.author}")
                : "Official theme";
            editButton.gameObject.SetActive(def != null);
            if (picker.gameObject.activeSelf) ShowPicker(pickerTab);
        }

        // ---------------- UI ----------------

        void BuildUi(RectTransform root)
        {
            pageWidth = 760f;
            var header = UiFactory.MakePanel(root, "Header", HeaderColor);
            UiFactory.Place(header.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(WindowWidth, 80f));
            var title = UiFactory.MakeLabel(header.transform, "THEME SETTINGS", 32, TextAnchor.MiddleLeft, new Color(0.55f, 0.85f, 1f), FontStyle.Bold);
            UiFactory.Place(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(600f, 70f));
            HeaderButtons(header.transform, new[] { "Reset", "Done" }, new UnityEngine.Events.UnityAction[] { ResetSettings, Close },
                new[] { ButtonColor, SaveColor });

            // The level's theme: its picture (click = pick another), name and Edit for a custom one.
            var frame = UiFactory.MakePanel(root, "Theme Frame", new Color(0.3f, 0.26f, 0.34f), true);
            UiFactory.Place(frame.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -100f), new Vector2(360f, 210f));
            var frameButton = frame.gameObject.AddComponent<Button>();
            frameButton.targetGraphic = frame;
            frameButton.navigation = UiFactory.NoNavigation;
            frameButton.onClick.AddListener(() => ShowPicker(PickerTabFor(editor.Level.Look)));
            thumb = UiFactory.MakeRect(frame.transform, "Background").gameObject.AddComponent<RawImage>();
            thumb.raycastTarget = false;
            UiFactory.Stretch(thumb.rectTransform, 5f, 5f, 5f, 5f);
            thumbMissing = UiFactory.MakeLabel(frame.transform, "No background", 24, TextAnchor.MiddleCenter, SoftText, FontStyle.Bold);
            var click = UiFactory.MakeLabel(frame.transform, "click to change", 16, TextAnchor.LowerCenter, Color.white, FontStyle.Bold);
            UiFactory.Stretch(click.rectTransform, 4f, 8f, 4f, 4f);
            click.gameObject.AddComponent<Shadow>();

            themeName = UiFactory.MakeLabel(root, "", 28, TextAnchor.UpperLeft, Color.white, FontStyle.Bold);
            UiFactory.Place(themeName.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(404f, -104f), new Vector2(380f, 80f));
            themeName.resizeTextForBestFit = true;
            themeName.resizeTextMinSize = 14;
            themeName.resizeTextMaxSize = 28;
            themeInfo = UiFactory.MakeLabel(root, "", 17, TextAnchor.UpperLeft, SoftText);
            UiFactory.Place(themeInfo.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(404f, -186f), new Vector2(380f, 60f));
            var change = UiFactory.MakeButton(root, "Change...", AccentColor, () => ShowPicker(PickerTabFor(editor.Level.Look)), 22);
            UiFactory.Place((RectTransform)change.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(404f, -254f), new Vector2(180f, 54f));
            editButton = UiFactory.MakeButton(root, "Edit", ButtonColor, () =>
            {
                var def = ThemeLibrary.Get(editor.Level.Look);
                if (def != null) editor.OpenCustomTheme(def);
            }, 22);
            UiFactory.Place((RectTransform)editButton.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(596f, -254f), new Vector2(140f, 54f));

            // Settings rows.
            var page = UiFactory.MakeRect(root, "Settings");
            UiFactory.Place(page, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -336f), new Vector2(pageWidth, 560f));
            float y = 0f;
            ToggleRow(page, "Background", ref y, () => Settings.hideBackground ? "No (sky / skybox)" : "Yes (picture)",
                () => !Settings.hideBackground, () => Settings.hideBackground = !Settings.hideBackground);
            SliderRow(page, "Offset", ref y, -0.5f, 0.5f, () => Settings.backgroundOffset, v => Settings.backgroundOffset = v);
            CycleRow(page, "Gradient", ref y, () => GradientNames[Settings.gradient + 1],
                step => Settings.gradient = (Settings.gradient + 1 + step + GradientNames.Length) % GradientNames.Length - 1);
            ButtonsRow(page, "Roller", ref y, () => FromName(Settings.roller), ("Select...", () => ChooseFrom("Roller", t => t.rollerModel != null, false, n => Settings.roller = n)),
                ("Default", () => { Settings.roller = ""; Changed(); }));
            ButtonsRow(page, "Block", ref y, () => FromName(Settings.block), ("Select...", () => ChooseFrom("Block", t => t.blockModel != null, false, n => Settings.block = n)),
                ("Default", () => { Settings.block = ""; Changed(); }));
            int edges = Enum.GetValues(typeof(MoverEdge)).Length;
            CycleRow(page, "Mover Edge", ref y, () => Settings.moverEdge switch
                {
                    MoverEdge.Always => "With edge",
                    MoverEdge.Never => "Without edge",
                    _ => "Auto",
                },
                step => Settings.moverEdge = (MoverEdge)(((int)Settings.moverEdge + step + edges) % edges));
            ButtonsRow(page, "Midground", ref y, () => FromName(Settings.midground),
                ("Select...", () => ChooseFrom("Midground", t => t.midgroundModels != null && t.midgroundModels.Length > 0, true, n => Settings.midground = n)),
                ("Default", () => { Settings.midground = ""; Changed(); }));
            CycleRow(page, "Model", ref y, () => LookName(Settings.modelLook, editor.theme != null ? editor.theme.modelLook : TileLook.Classic),
                step => Settings.modelLook = NextLook(Settings.modelLook, step));
            CycleRow(page, "Texture", ref y, () => LookName(Settings.textureLook, editor.theme != null ? editor.theme.textureLook : TileLook.RTX),
                step => Settings.textureLook = NextLook(Settings.textureLook, step));
            var hint = UiFactory.MakeLabel(page,
                "Background No = the sky (skybox) all round. Offset moves the background; Gradient fades it into the fog. Roller, Block and Midground come from another theme (Midground None = none); " +
                "Mover Edge = the border round movers. Model: Classic = original tiles, RTX = the theme's RTX Models. Texture: Classic = original atlas, RTX = RTX materials. Default = the theme's own.",
                15, TextAnchor.UpperLeft, SoftText);
            UiFactory.Place(hint.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, y - 6f), new Vector2(pageWidth, 96f));

            // 3D preview: the editor's 3D view from a game-like camera.
            var previewFrame = UiFactory.MakePanel(root, "Preview Frame", new Color(0f, 0f, 0f, 0.4f));
            UiFactory.Place(previewFrame.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -100f), new Vector2(PreviewWidth, PreviewHeight));
            preview = UiFactory.MakeRect(previewFrame.transform, "Preview").gameObject.AddComponent<RawImage>();
            preview.raycastTarget = false;
            UiFactory.Stretch(preview.rectTransform);
            previewFit = preview.gameObject.AddComponent<AspectRatioFitter>();
            previewFit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            float by = -100f - PreviewHeight - 12f;
            var back = UiFactory.MakeButton(root, "< 20 rows", ButtonColor, () => { editor.ScrollBy(-20); }, 20);
            UiFactory.Place((RectTransform)back.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f - PreviewWidth + 170f, by), new Vector2(170f, 50f));
            var ahead = UiFactory.MakeButton(root, "20 rows >", ButtonColor, () => { editor.ScrollBy(20); }, 20);
            UiFactory.Place((RectTransform)ahead.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f - PreviewWidth + 350f, by), new Vector2(170f, 50f));
            var start = UiFactory.MakeButton(root, "Start", ButtonColor, () => { editor.ScrollTo(0); }, 20);
            UiFactory.Place((RectTransform)start.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, by), new Vector2(130f, 50f));
            var previewHint = UiFactory.MakeLabel(root, "3D preview from the game camera at the grid's position (the theme changes of later rows show there too).",
                16, TextAnchor.UpperLeft, SoftText);
            UiFactory.Place(previewHint.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, by - 60f), new Vector2(PreviewWidth, 60f));

            MakeStatus(root, WindowWidth);
            BuildPicker(root);
            BuildChooser(root, new Vector2(24f, -90f), new Vector2(pageWidth, WindowHeight - 160f));
        }

        static string FromName(string name) =>
            string.IsNullOrEmpty(name) ? "Default (the theme's own)" : name == LevelThemeSettings.NoneName ? "None" : name;

        /// <summary>A theme to take a model from: the official themes that have one (Midground also offers None).</summary>
        void ChooseFrom(string what, Func<TrackTheme, bool> has, bool none, Action<string> set)
        {
            var names = new List<string>();
            if (none) names.Add(LevelThemeSettings.NoneName);
            foreach (var t in editor.Looks)
                if (t != null && has(t) && !names.Contains(t.name)) names.Add(t.name);
            if (names.Count == 0)
            {
                Status($"No theme has a {what} model yet (theme > Model overrides).");
                return;
            }
            OpenChooser($"{what} from which theme?", names, i =>
            {
                set(names[i]);
                Changed();
                Status($"{what}: {FromName(names[i])}.");
            });
        }

        void ResetSettings()
        {
            editor.Level.ThemeSettings = new LevelThemeSettings();
            Changed();
            Status("Theme Settings back to Default (the theme stays).");
        }

        // ---------------- Theme picker ----------------

        void BuildPicker(RectTransform root)
        {
            var bg = UiFactory.MakePanel(root, "Theme Picker", new Color(0.08f, 0.07f, 0.1f, 0.99f), true);
            picker = bg.rectTransform;
            UiFactory.Place(picker, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -80f), new Vector2(WindowWidth, WindowHeight - 140f));
            for (int i = 0; i < PickerTabs.Length; i++)
            {
                int index = i;
                var t = UiFactory.MakeButton(picker, PickerTabs[i], ButtonColor, () => ShowPicker(index), 24);
                UiFactory.Place((RectTransform)t.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f + i * 196f, -14f), new Vector2(186f, 54f));
                pickerTabButtons[i] = (Image)t.targetGraphic;
            }
            importButton = UiFactory.MakeButton(picker, "Import", ButtonColor, ImportTheme, 22);
            UiFactory.Place((RectTransform)importButton.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-180f, -14f), new Vector2(150f, 54f));
            var cancel = UiFactory.MakeButton(picker, "Cancel", ButtonColor, () => picker.gameObject.SetActive(false), 22);
            UiFactory.Place((RectTransform)cancel.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -14f), new Vector2(150f, 54f));
            var list = UiFactory.MakeGridList(picker, new Vector2(280f, 210f), 14f, 5, AccentColor, out pickerList);
            UiFactory.Stretch((RectTransform)list.transform, 16f, 16f, 16f, 84f);
            bg.gameObject.SetActive(false);
        }

        int PickerTabFor(string look)
        {
            var def = ThemeLibrary.Get(look);
            if (def == null || ThemeLibrary.Official(look, editor.Looks) != null) return 0;
            return def.imported ? 1 : 2;
        }

        void ShowPicker(int tab)
        {
            pickerTab = tab;
            for (int i = 0; i < pickerTabButtons.Length; i++) pickerTabButtons[i].color = i == tab ? AccentColor : ButtonColor;
            foreach (var go in pickerItems) Destroy(go);
            pickerItems.Clear();
            string current = editor.Level.Look ?? "";
            if (tab == 0)
            {
                var d = editor.DefaultTheme;
                PickerItem($"Default ({(d != null ? d.name : "none")})", d != null ? d.background : null, current.Length == 0, () => Pick(null), null);
                foreach (var t in editor.Looks)
                    if (t != null)
                    {
                        var look = t;
                        PickerItem(t.name, t.background, string.Equals(current, t.name, StringComparison.OrdinalIgnoreCase), () => Pick(look.name), null);
                    }
            }
            else
            {
                if (tab == 2) PickerItem("+  New Custom Theme", null, false, () => editor.OpenCustomTheme(null), null, true);
                int n = 0;
                foreach (var def in ThemeLibrary.All())
                {
                    if (def.imported != (tab == 1)) continue;
                    var d = def;
                    PickerItem(def.name, ThemeLibrary.Thumbnail(def, editor.Looks), string.Equals(current, def.name, StringComparison.OrdinalIgnoreCase),
                        () => Pick(d.name), () => editor.OpenCustomTheme(d));
                    n++;
                }
                if (n == 0 && tab == 1) Status("No Fanmade themes yet: Import a .themert (or a .levelrsrt that carries themes).");
            }
            picker.gameObject.SetActive(true);
            picker.SetAsLastSibling();
        }

        void PickerItem(string name, Texture picture, bool selected, Action pick, Action edit, bool plus = false)
        {
            var cell = UiFactory.MakePanel(pickerList, name, selected ? AccentColor : new Color(0.3f, 0.26f, 0.34f), true);
            var b = cell.gameObject.AddComponent<Button>();
            b.targetGraphic = cell;
            b.navigation = UiFactory.NoNavigation;
            b.onClick.AddListener(() => pick());
            var pic = UiFactory.MakeRect(cell.transform, "Picture").gameObject.AddComponent<RawImage>();
            pic.raycastTarget = false;
            UiFactory.Stretch(pic.rectTransform, 5f, 46f, 5f, 5f);
            pic.texture = picture;
            pic.color = picture != null ? Color.white : FieldColor;
            if (plus)
            {
                var mark = UiFactory.MakeLabel(pic.transform, "+", 90, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
                UiFactory.Stretch(mark.rectTransform);
            }
            var label = UiFactory.MakeLabel(cell.transform, name, 18, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            UiFactory.Place(label.rectTransform, Vector2.zero, Vector2.zero, new Vector2(10f, 2f), new Vector2(edit != null ? 184f : 260f, 42f));
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 10;
            label.resizeTextMaxSize = 18;
            if (edit != null)
            {
                var e = UiFactory.MakeButton(cell.transform, "Edit", ButtonColor, () => edit(), 16);
                UiFactory.Place((RectTransform)e.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-5f, 4f), new Vector2(76f, 36f));
            }
            pickerItems.Add(cell.gameObject);
        }

        void Pick(string name)
        {
            picker.gameObject.SetActive(false);
            editor.SetLevelLook(name);
            ShowAll();
            Status($"Theme: {(string.IsNullOrEmpty(name) ? "Default" : name)}.");
        }

        void ImportTheme()
        {
            editor.ImportAny(message =>
            {
                Status(message);
                if (IsOpen && picker.gameObject.activeSelf) ShowPicker(pickerTab == 0 ? 1 : pickerTab);
            });
        }
    }
}
