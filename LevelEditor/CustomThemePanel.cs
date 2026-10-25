using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RTXOn
{
    /// <summary>
    /// The level editor's CUSTOM THEME window (Theme Settings > Custom > + or Edit), like the fan editor's custom
    /// themes: tabs Textures (Classic: uploaded tile atlases, each with its own Glow; RTX ON: Unity materials; both: Enemy,
    /// Enemy Glow, Icon, Background), ColorSets (theme colours, and each environment's sky, sun, ambient and fog) and
    /// Information (name, author, Base Theme). Right: the 3D preview with the theme being edited on every row.
    /// It edits a copy: Save writes the .themert to the Themes library; Export writes one anywhere to share.
    /// </summary>
    public class CustomThemePanel : PanelKit
    {
        static readonly string[] TabNames = { "Textures", "ColorSets", "Information" };
        const float WindowWidth = 1560f, WindowHeight = 940f;
        const float PageTop = -165f;
        const float PreviewWidth = 720f, PreviewHeight = 560f;
        const int IconSize = 256;
        const float PreviewDelay = 0.35f; // seconds after the last change before the preview theme is built again

        CustomTheme def;         // the copy being edited
        string savedJson;        // as it was opened or last saved (null = never saved)
        string savedName;        // its name in the library (null = new)
        bool confirmClose, confirmDelete;
        int colorSet;            // ColorSets: 0 = theme colours, 1.. = environment
        float previewAt = -1f;
        const float MakerPreviewDelay = 0.05f;
        Action<string> makerSet; // the texture row the SpriteMaker's 3D shows its atlas in (preview only)
        string makerPng;
        Text title, help;
        RawImage preview;
        AspectRatioFitter previewFit;
        readonly Image[] tabButtons = new Image[TabNames.Length];
        readonly GameObject[] pages = new GameObject[TabNames.Length];

        protected override bool Editable => def != null;

        public static CustomThemePanel Build(LevelEditor owner, Transform parent)
        {
            var bg = UiFactory.MakePanel(parent, "Custom Theme", PanelColor, true);
            UiFactory.Place(bg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(WindowWidth, WindowHeight));
            var panel = bg.gameObject.AddComponent<CustomThemePanel>();
            panel.editor = owner;
            panel.BuildUi((RectTransform)bg.transform);
            bg.gameObject.SetActive(false);
            return panel;
        }

        // ---------------- Open / close ----------------

        /// <summary>Opens on a copy of <paramref name="existing"/>, or on a new theme made from the level's theme.</summary>
        public void Open(CustomTheme existing)
        {
            if (existing != null)
            {
                def = existing.Clone();
                savedJson = def.ToJson(false);
                savedName = def.name;
            }
            else
            {
                def = NewTheme();
                savedJson = null;
                savedName = null;
            }
            confirmClose = confirmDelete = false;
            colorSet = 0;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            ClosePopups();
            editor.UpdateThemePreview();
            ShowPreviewLevel();
            preview.texture = editor.ThemePreviewTexture;
            if (preview.texture != null) previewFit.aspectRatio = (float)preview.texture.width / Mathf.Max(1, preview.texture.height);
            ShowTab(0);
            Changed();
            Status(existing != null ? $"Editing {def.name}. Save changes it in every level that uses it (after their next Save)."
                                    : $"New theme on {def.baseTheme}. Upload textures, set colours, then Save.");
        }

        /// <summary>A new theme on the level's theme (its official base), with that theme's environments to colour.</summary>
        CustomTheme NewTheme()
        {
            var current = editor.CurrentLook();
            var source = current != null ? ThemeLibrary.Official(current.name, editor.Looks) : null;
            if (source == null)
            {
                var custom = current != null ? ThemeLibrary.Get(current.name) : null;
                source = custom != null ? ThemeLibrary.Official(custom.baseTheme, editor.Looks) : null;
            }
            if (source == null) source = editor.DefaultTheme;
            string name = "Custom Theme";
            for (int i = 2; ThemeLibrary.Has(name) || ThemeLibrary.Official(name, editor.Looks) != null; i++) name = "Custom Theme " + i;
            var t = new CustomTheme { name = name, baseTheme = source != null ? source.name : "", mode = source != null ? source.textureLook : TileLook.Classic };
            if (source != null)
            {
                t.backgroundTint = source.backgroundTint;
                t.midgroundTint = source.midgroundTint;
                t.midFogColor = source.midFogColor;
                if (source.environments != null)
                    foreach (var p in source.environments)
                        if (p != null) t.environments.Add(ThemeEnvironment.From(p, ThemeLibrary.PresetKey(source, p)));
            }
            return t;
        }

        bool Unsaved => def != null && def.ToJson(false) != savedJson;

        public void Close()
        {
            if (!IsOpen) return;
            if (ClosePopups()) return;
            if (Unsaved && !confirmClose)
            {
                confirmClose = true;
                Status("Not saved: press Save, or X again to close without saving.");
                return;
            }
            Hide();
        }

        public void Hide()
        {
            if (!IsOpen) return;
            gameObject.SetActive(false);
            def = null;
            previewAt = -1f;
            makerSet = null;
            makerPng = null;
            ThemeLibrary.Remove(ThemeLibrary.PreviewName);
            if (editor != null)
            {
                editor.SetPreviewEnvironment(null);
                editor.SetPreviewLevel(null, 0f);
                editor.SetPreviewLook(null);
                editor.UpdateThemePreview();
            }
        }

        // ---------------- Buttons ----------------

        void Save()
        {
            def.Normalize();
            if (def.name == ThemeLibrary.PreviewName || ThemeLibrary.Official(def.name, editor.Looks) != null)
            {
                Status($"\"{def.name}\" is the name of an official theme: give the custom theme another name (Information).");
                return;
            }
            var other = ThemeLibrary.Get(def.name);
            bool renamed = savedName != null && !string.Equals(savedName, def.name, StringComparison.OrdinalIgnoreCase);
            if (other != null && (savedName == null || renamed))
            {
                Status($"There is already a theme called {def.name}: pick another name (Information).");
                return;
            }
            try
            {
                ThemeLibrary.SaveToLibrary(def);
                if (renamed) ThemeLibrary.DeleteFromLibrary(savedName);
            }
            catch (Exception e)
            {
                Status("Could not save: " + e.Message);
                return;
            }
            if (renamed) ThemeLibrary.Remove(savedName);
            ThemeLibrary.Set(def.Clone());
            string oldName = savedName;
            savedJson = def.ToJson(false);
            savedName = def.name;
            confirmClose = confirmDelete = false;
            if (renamed) editor.CustomThemeRenamed(oldName, def.name);
            else editor.ThemesChanged(editor.ThemeUses(def.name) > 0); // a level that uses it carries the new copy at its next Save
            Refresh();
            Status($"Saved {def.name} as {ThemeLibrary.LibraryPath(def.name)}. Pick it in Theme Settings > {(def.imported ? "Fanmade" : "Custom")}.");
        }

        void Delete()
        {
            if (savedName == null)
            {
                Hide();
                editor.ShowStatus("New custom theme dropped.");
                return;
            }
            int uses = editor.ThemeUses(savedName);
            if (uses > 0)
            {
                Status($"This level uses {savedName} ({uses} place(s): its theme or theme triggers): pick another theme there first.");
                return;
            }
            if (!confirmDelete)
            {
                confirmDelete = true;
                Status($"Press Delete again to delete {savedName} and its file {ThemeLibrary.SafeFileName(savedName)}{CustomTheme.FileExtension}.");
                return;
            }
            string name = savedName;
            ThemeLibrary.Remove(name);
            try { ThemeLibrary.DeleteFromLibrary(name); }
            catch (Exception e) { Debug.LogWarning("RTXOn: " + e.Message); }
            Hide();
            editor.ThemesChanged(false);
            editor.ShowStatus($"Deleted custom theme {name}.");
        }

        void Export()
        {
            def.Normalize();
            Status(editor.Files.Save("Export Theme", ThemeLibrary.SafeFileName(def.name) + CustomTheme.FileExtension, def.ToJson()));
        }

        // ---------------- Changes and preview ----------------

        protected override void Changed()
        {
            if (def == null) return;
            confirmClose = confirmDelete = false;
            def.Normalize();
            colorSet = Mathf.Clamp(colorSet, 0, def.environments.Count);
            Refresh();
            title.text = $"CUSTOM THEME  ·  {def.name.ToUpperInvariant()}" + (Unsaved ? "  *" : "");
            editor.SetPreviewEnvironment(Env != null ? Env.name : null); // ColorSets: the environment being edited shows in the preview
            previewAt = Time.unscaledTime + PreviewDelay; // build the preview once the clicks stop
        }

        void Update()
        {
            if (def == null || previewAt < 0f || Time.unscaledTime < previewAt) return;
            previewAt = -1f;
            var copy = def.Clone();
            copy.name = ThemeLibrary.PreviewName;
            if (makerSet != null && makerPng != null)
            {
                // The rows' setters write to def: point it at the copy for this one call.
                var own = def;
                def = copy;
                makerSet(makerPng);
                def = own;
            }
            ThemeLibrary.Set(copy);
            editor.SetPreviewLook(ThemeLibrary.PreviewName);
            editor.ThemesChanged(false);
        }

        // ---------------- LevelTexturePreview.json ----------------

        LevelData previewLevel;
        float previewRow;

        /// <summary>The preview draws LevelTexturePreview.json (the SpriteMaker's, or one saved in the levels folder) instead of the open level.</summary>
        public void ShowPreviewLevel()
        {
            if (previewLevel == null) previewLevel = SpriteMakerPanel.LoadPreviewLevel(out _);
            editor.SetPreviewLevel(previewLevel, previewRow); // null (no LevelTexturePreview.json) = the open level
        }

        void MovePreview(float rows)
        {
            if (previewLevel == null)
            {
                editor.ScrollBy(Mathf.RoundToInt(rows * 5f)); // no LevelTexturePreview.json: the open level, as before
                return;
            }
            previewRow = Mathf.Clamp(previewRow + rows, 0f, Mathf.Max(0, previewLevel.RowCount - 4));
            editor.SetPreviewLevel(previewLevel, previewRow);
        }

        void ShowTab(int index)
        {
            for (int i = 0; i < pages.Length; i++)
            {
                pages[i].SetActive(i == index);
                tabButtons[i].color = i == index ? AccentColor : ButtonColor;
            }
            help.text = index switch
            {
                0 => "Mode Classic: the tiles use your atlases (same layout as the theme's Tile Set: General, Fragile, Fragile Active, " +
                     "Mover, Mover Auto), each with its own Glow picture. Mode RTX ON: the tiles use materials made in Unity first " +
                     "(put them in Assets/Resources/RTXOnMaterials). Enemy, Enemy Glow, Icon and Background work in both modes. " +
                     "Empty = the Base Theme's.",
                1 => "Colour set: Theme = ball, background, midground and glow colours; each environment (Day, Sunset, Night...) has " +
                     "its sky, sun, ambient light and fog. Theme triggers switch environments by name. Add copies one from any theme.",
                _ => "Name: how levels pick the theme (rename = the open level follows). Base Theme: models, tile shapes, " +
                     "midground and sounds come from it. Export writes a .themert to share; Import is in Theme Settings.",
            };
        }

        // ---------------- UI ----------------

        void BuildUi(RectTransform root)
        {
            pageWidth = 760f;
            RowHeight = 38f; // 15 rows on the Textures tab
            RowStep = 44f;
            var header = UiFactory.MakePanel(root, "Header", HeaderColor);
            UiFactory.Place(header.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(WindowWidth, 80f));
            title = UiFactory.MakeLabel(header.transform, "CUSTOM THEME", 30, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            UiFactory.Place(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(900f, 70f));
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 16;
            title.resizeTextMaxSize = 30;
            HeaderButtons(header.transform, new[] { "Delete", "Export", "Save", "X" },
                new UnityEngine.Events.UnityAction[] { Delete, Export, Save, Close }, new[] { DeleteColor, ButtonColor, SaveColor, ButtonColor });

            for (int i = 0; i < TabNames.Length; i++)
            {
                int index = i;
                var t = UiFactory.MakeButton(root, TabNames[i], ButtonColor, () => ShowTab(index), 24);
                UiFactory.Place((RectTransform)t.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f + i * 196f, -94f), new Vector2(186f, 54f));
                tabButtons[i] = (Image)t.targetGraphic;
                var page = UiFactory.MakeRect(root, TabNames[i] + " Page");
                UiFactory.Place(page, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, PageTop), new Vector2(pageWidth, WindowHeight + PageTop - 70f));
                pages[i] = page.gameObject;
            }
            BuildTexturesPage(pages[0].transform);
            BuildColorSetsPage(pages[1].transform);
            BuildInformationPage(pages[2].transform);

            var previewFrame = UiFactory.MakePanel(root, "Preview Frame", new Color(0f, 0f, 0f, 0.4f));
            UiFactory.Place(previewFrame.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -100f), new Vector2(PreviewWidth, PreviewHeight));
            preview = UiFactory.MakeRect(previewFrame.transform, "Preview").gameObject.AddComponent<RawImage>();
            preview.raycastTarget = false;
            UiFactory.Stretch(preview.rectTransform);
            previewFit = preview.gameObject.AddComponent<AspectRatioFitter>();
            previewFit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            float by = -100f - PreviewHeight - 12f;
            preview.raycastTarget = true; // drag or scroll it to move along LevelTexturePreview.json
            var previewTrigger = preview.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            var drag = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.Drag };
            drag.callback.AddListener(d => MovePreview(((UnityEngine.EventSystems.PointerEventData)d).delta.y / 30f));
            previewTrigger.triggers.Add(drag);
            var wheel = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.Scroll };
            wheel.callback.AddListener(d => MovePreview(((UnityEngine.EventSystems.PointerEventData)d).scrollDelta.y));
            previewTrigger.triggers.Add(wheel);
            var back = UiFactory.MakeButton(root, "< 4 rows", ButtonColor, () => MovePreview(-4f), 20);
            UiFactory.Place((RectTransform)back.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f - PreviewWidth + 170f, by), new Vector2(170f, 50f));
            var ahead = UiFactory.MakeButton(root, "4 rows >", ButtonColor, () => MovePreview(4f), 20);
            UiFactory.Place((RectTransform)ahead.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f - PreviewWidth + 350f, by), new Vector2(170f, 50f));
            var icon = UiFactory.MakeButton(root, "Icon from preview", ButtonColor, IconFromPreview, 18);
            UiFactory.Place((RectTransform)icon.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, by), new Vector2(210f, 50f));
            help = UiFactory.MakeLabel(root, "", 16, TextAnchor.UpperLeft, SoftText);
            UiFactory.Place(help.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, by - 60f), new Vector2(PreviewWidth, 120f));

            MakeStatus(root, WindowWidth);
            BuildChooser(root, new Vector2(24f, -90f), new Vector2(pageWidth, WindowHeight - 160f));
            BuildColorPicker(root);
        }

        void BuildTexturesPage(Transform page)
        {
            float y = 0f;
            ToggleRow(page, "Mode", ref y, () => def.mode == TileLook.Classic ? "Classic (textures)" : "RTX ON (materials)",
                () => def.mode == TileLook.RTX, () => def.mode = def.mode == TileLook.Classic ? TileLook.RTX : TileLook.Classic);
            float groupTop = y;

            var classic = Group(page, "Classic", groupTop, () => def.mode == TileLook.Classic);
            float cy = 0f;
            // Each atlas with its own glow picture.
            TextureRow(classic, "General", ref cy, () => def.general, v => def.general = v, maker: "TileGeneral");
            TextureRow(classic, "General Glow", ref cy, () => def.tileGlow, v => def.tileGlow = v, maker: "TileGeneral");
            TextureRow(classic, "Fragile", ref cy, () => def.fragile, v => def.fragile = v, maker: "Fragile");
            TextureRow(classic, "Fragile Glow", ref cy, () => def.fragileGlow, v => def.fragileGlow = v, maker: "Fragile");
            TextureRow(classic, "FragileActive", ref cy, () => def.fragileActive, v => def.fragileActive = v, maker: "Active");
            TextureRow(classic, "FragileActive Glow", ref cy, () => def.fragileActiveGlow, v => def.fragileActiveGlow = v, maker: "Active");
            TextureRow(classic, "Mover", ref cy, () => def.mover, v => def.mover = v, maker: "Mover");
            TextureRow(classic, "Mover Glow", ref cy, () => def.moverGlow, v => def.moverGlow = v, maker: "Mover");
            TextureRow(classic, "MoverAuto", ref cy, () => def.moverAuto, v => def.moverAuto = v, maker: "Mover");
            TextureRow(classic, "MoverAuto Glow", ref cy, () => def.moverAutoGlow, v => def.moverAutoGlow = v, maker: "Mover");

            var rtx = Group(page, "RTX ON", groupTop, () => def.mode == TileLook.RTX);
            float ry = 0f;
            MaterialRow(rtx, "Floor", ref ry, () => def.floorMaterial, v => def.floorMaterial = v);
            MaterialRow(rtx, "Floor Side", ref ry, () => def.floorSideMaterial, v => def.floorSideMaterial = v);
            MaterialRow(rtx, "Fragile", ref ry, () => def.fragileMaterial, v => def.fragileMaterial = v);
            MaterialRow(rtx, "Fragile Side", ref ry, () => def.fragileSideMaterial, v => def.fragileSideMaterial = v);
            MaterialRow(rtx, "Mover", ref ry, () => def.moverMaterial, v => def.moverMaterial = v);
            MaterialRow(rtx, "MoverAuto", ref ry, () => def.moverAutoMaterial, v => def.moverAutoMaterial = v);
            MaterialRow(rtx, "Jump", ref ry, () => def.jumpMaterial, v => def.jumpMaterial = v);

            y = groupTop + Mathf.Min(cy, ry);
            TextureRow(page, "Enemy", ref y, () => def.enemy, v => def.enemy = v, maker: "Enemy");
            TextureRow(page, "Enemy Glow", ref y, () => def.enemyGlow, v => def.enemyGlow = v, maker: "Enemy");
            TextureRow(page, "Icon", ref y, () => def.icon, v => def.icon = v, IconSize);
            TextureRow(page, "Background", ref y, () => def.background, v => def.background = v);
        }

        /// <summary>A picture row; <paramref name="maker"/> = the SpriteMaker atlas that can make it (Make...).</summary>
        void TextureRow(Transform page, string label, ref float y, Func<string> get, Action<string> set, int maxSize = CustomTheme.MaxTextureSize, string maker = null)
        {
            if (maker == null)
            {
                ButtonsRow(page, label, ref y, () => PictureText(get()),
                    ("Upload...", () => PickImage(label, maxSize, set)),
                    ("Clear", () => { set(""); Changed(); }));
                return;
            }
            ButtonsRow(page, label, ref y, () => PictureText(get()),
                ("Upload...", () => PickImage(label, maxSize, set)),
                ("Make...", () => editor.OpenSpriteMaker(maker, label, label.EndsWith(" Glow"), png =>
                {
                    if (def == null) return;
                    set(png);
                    Changed();
                    Status($"{label}: made with the SpriteMaker.");
                }, png =>
                {
                    // SpriteMaker 3D: only the preview gets it (the theme itself changes on Use)
                    makerSet = png != null ? set : null;
                    makerPng = png;
                    previewAt = Time.unscaledTime + MakerPreviewDelay;
                })),
                ("Clear", () => { set(""); Changed(); }));
        }

        void MaterialRow(Transform page, string label, ref float y, Func<string> get, Action<string> set)
        {
            ButtonsRow(page, label, ref y, () =>
                {
                    string n = get();
                    if (string.IsNullOrEmpty(n)) return "Base Theme's";
                    return ThemeLibrary.FindMaterial(n, editor.Looks) != null ? n : n + " (not found)";
                },
                ("Select...", () =>
                {
                    var names = new List<string>();
                    foreach (var m in ThemeLibrary.MaterialChoices(editor.Looks)) names.Add(m.name);
                    if (names.Count == 0)
                    {
                        Status($"No materials: make them in Unity and put them in Assets/Resources/{ThemeLibrary.MaterialsFolder}.");
                        return;
                    }
                    OpenChooser($"{label} material (Resources/{ThemeLibrary.MaterialsFolder}, then the themes' own)", names, i =>
                    {
                        set(names[i]);
                        Changed();
                    });
                }),
                ("Clear", () => { set(""); Changed(); }));
        }

        /// <summary>Height (x) and turn (y) of an environment's sun: its own, else the one it was copied from.</summary>
        Vector2 SunAngles(ThemeEnvironment e)
        {
            if (e == null) return new Vector2(42f, -35f);
            if (e.ownSun) return new Vector2(e.sunHeight, Mathf.DeltaAngle(0f, e.sunTurn));
            var source = ThemeLibrary.FindPreset(e.from, editor.Looks);
            return source != null ? new Vector2(source.sunEuler.x, Mathf.DeltaAngle(0f, source.sunEuler.y)) : new Vector2(42f, -35f);
        }

        ThemeEnvironment Env => colorSet > 0 && colorSet <= def.environments.Count ? def.environments[colorSet - 1] : null;

        /// <summary>The environment shown, or a stand-in while the theme colours show (its rows are hidden then).</summary>
        ThemeEnvironment E => Env ?? noEnv;
        readonly ThemeEnvironment noEnv = new ThemeEnvironment();

        void BuildColorSetsPage(Transform page)
        {
            float y = 0f;
            CycleRow(page, "Colour set", ref y, () => Env != null ? "Environment: " + Env.name : "Theme colours",
                step => colorSet = (colorSet + step + def.environments.Count + 1) % (def.environments.Count + 1));
            ButtonsRow(page, "Environments", ref y, () => def.environments.Count == 0 ? "the Base Theme's" : $"{def.environments.Count}",
                ("Add...", AddEnvironment),
                ("Remove", RemoveEnvironment));
            float top = y;

            var theme = Group(page, "Theme colours", top, () => Env == null);
            float ty = 0f;
            ColorRow(theme, "Ball", ref ty, () => def.ballColor.a > 0f ? def.ballColor : Color.white, c => { c.a = 1f; def.ballColor = c; });
            ButtonsRow(theme, "Ball colour", ref ty, () => def.ballColor.a > 0f ? "this colour" : "the ball's own",
                ("Own", () => { var c = def.ballColor; c.a = 0f; def.ballColor = c; Changed(); }));
            ColorRow(theme, "Background tint", ref ty, () => def.backgroundTint, c => def.backgroundTint = c, true);
            ColorRow(theme, "Midground tint", ref ty, () => def.midgroundTint, c => def.midgroundTint = c, true);
            ColorRow(theme, "Midground fog", ref ty, () => def.midFogColor, c => def.midFogColor = c);
            ColorRow(theme, "Tile Glow colour", ref ty, () => def.tileGlowColor, c => def.tileGlowColor = c, true);
            NumberRow(theme, "Tile Glow strength", ref ty, () => def.tileGlowStrength, v => def.tileGlowStrength = v);
            ColorRow(theme, "Enemy Glow colour", ref ty, () => def.enemyGlowColor, c => def.enemyGlowColor = c, true);
            NumberRow(theme, "Enemy Glow strength", ref ty, () => def.enemyGlowStrength, v => def.enemyGlowStrength = v);
            SliderRow(theme, "Enemy specular", ref ty, 0f, 1f, () => def.enemySpecular, v => def.enemySpecular = v);
            SliderRow(theme, "Enemy gloss", ref ty, 0f, 1f, () => def.enemyGloss, v => def.enemyGloss = v);

            var env = Group(page, "Environment colours", top, () => Env != null);
            float ey = 0f;
            TextRow(env, "Name", ref ey, () => E.name, t => { if (Env != null && !string.IsNullOrWhiteSpace(t)) E.name = t.Trim(); }, 24);
            ColorRow(env, "Sky top", ref ey, () => E.skyTop, c => E.skyTop = c);
            ColorRow(env, "Sky horizon", ref ey, () => E.skyHorizon, c => E.skyHorizon = c);
            ColorRow(env, "Sky bottom", ref ey, () => E.skyBottom, c => E.skyBottom = c);
            ColorRow(env, "Sun / moon", ref ey, () => E.sunColor, c => E.sunColor = c);
            SliderRow(env, "Sun / moon turn", ref ey, -180f, 180f, () => SunAngles(E).y, v =>
            {
                if (Env == null) return;
                var a = SunAngles(E);
                E.sunTurn = v;
                E.sunHeight = a.x;
                E.ownSun = true;
            }, "0°", true, -35f);
            SliderRow(env, "Sun / moon height", ref ey, 0f, 90f, () => SunAngles(E).x, v =>
            {
                if (Env == null) return;
                var a = SunAngles(E);
                E.sunHeight = v;
                E.sunTurn = a.y;
                E.ownSun = true;
            }, "0°", true, 42f);
            NumberRow(env, "Sun intensity", ref ey, () => E.sunIntensity, v => E.sunIntensity = Mathf.Max(0f, v));
            ColorRow(env, "Ambient", ref ey, () => E.ambient, c => E.ambient = c);
            ToggleRow(env, "Use Fog", ref ey, () => E.useFog ? "On" : "Off", () => E.useFog, () => E.useFog = !E.useFog);
            ColorRow(env, "Fog colour", ref ey, () => E.fogColor, c => E.fogColor = c);
            NumberRow(env, "Fog density", ref ey, () => E.fogDensity, v => E.fogDensity = Mathf.Clamp(v, 0f, 0.5f));
        }

        void AddEnvironment()
        {
            var keys = new List<string>();
            var presets = new List<EnvironmentPreset>();
            foreach (var t in editor.Looks)
                if (t != null && t.environments != null)
                    foreach (var p in t.environments)
                        if (p != null)
                        {
                            keys.Add(ThemeLibrary.PresetKey(t, p));
                            presets.Add(p);
                        }
            if (keys.Count == 0)
            {
                Status("No theme has environments to copy.");
                return;
            }
            OpenChooser("Copy which environment? (its sky picture, stars and sun come along)", keys, i =>
            {
                var e = ThemeEnvironment.From(presets[i], keys[i]);
                string baseName = e.name;
                for (int n = 2; def.environments.Exists(x => string.Equals(x.name, e.name, StringComparison.OrdinalIgnoreCase)); n++) e.name = $"{baseName} {n}";
                def.environments.Add(e);
                colorSet = def.environments.Count;
                Changed();
                Status($"Added {e.name} (from {keys[i]}). Theme triggers switch to it by this name.");
            });
        }

        void RemoveEnvironment()
        {
            if (Env == null)
            {
                Status("Pick an environment with Colour set first.");
                return;
            }
            string name = Env.name;
            def.environments.RemoveAt(colorSet - 1);
            colorSet = Mathf.Min(colorSet, def.environments.Count);
            Changed();
            Status(def.environments.Count == 0 ? $"Removed {name}: the theme uses the Base Theme's environments." : $"Removed {name}.");
        }

        void BuildInformationPage(Transform page)
        {
            float y = 0f;
            TextRow(page, "Name", ref y, () => def.name, t => { if (!string.IsNullOrWhiteSpace(t)) def.name = t.Trim(); });
            TextRow(page, "Author", ref y, () => def.author, t => def.author = t.Trim());
            ButtonsRow(page, "Base Theme", ref y, () => string.IsNullOrEmpty(def.baseTheme) ? "the first theme" : def.baseTheme,
                ("Select...", () =>
                {
                    var names = new List<string>();
                    foreach (var t in editor.Looks) if (t != null && !names.Contains(t.name)) names.Add(t.name);
                    OpenChooser("Base Theme (models, tile shapes, midground, sounds)", names, i =>
                    {
                        def.baseTheme = names[i];
                        Changed();
                    });
                }));
            ToggleRow(page, "List", ref y, () => def.imported ? "Fanmade tab" : "Custom tab", () => def.imported, () => def.imported = !def.imported);
            InfoRow(page, "File", ref y, () => ThemeLibrary.LibraryPath(def.name));
            InfoRow(page, "Size", ref y, () => $"{def.ToJson(false).Length / 1024} KB (inside every level that uses it)");
            InfoRow(page, "This level", ref y, () =>
            {
                int n = editor.ThemeUses(savedName ?? def.name);
                return n > 0 ? $"uses it ({n} place(s))" : "does not use it (Theme Settings to pick it)";
            });
        }

        /// <summary>The icon from the 3D preview as it is now.</summary>
        void IconFromPreview()
        {
            var source = editor.ThemePreviewTexture as RenderTexture;
            if (source == null) return;
            int w = IconSize * 16 / 9, h = IconSize;
            var small = RenderTexture.GetTemporary(w, h, 0);
            Graphics.Blit(source, small);
            var active = RenderTexture.active;
            RenderTexture.active = small;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(small);
            def.icon = Convert.ToBase64String(tex.EncodeToPNG());
            Destroy(tex);
            Changed();
            Status("Icon taken from the preview (it shows in the theme picker).");
        }
    }
}
