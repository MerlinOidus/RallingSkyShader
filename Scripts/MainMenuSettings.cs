using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RTXOn
{
    // Settings: a sheet that rises from the bottom over any screen (Adrian's design 2026-10-24), with every game
    // setting (GameSettings, GraphicsQuality), the controller mapping, the menu colour set and the language.
    public partial class MainMenu
    {
        RectTransform settingsLayer, settingsSheet, settingsContent, settingsGear;
        CanvasGroup settingsScrim;
        ScrollRect settingsScroll;
        bool settingsOpen;
        float settingsY, rowWidth;
        Selectable firstSetting;
        GameObject lastSelected;
        readonly List<Action> refreshers = new List<Action>();
        readonly List<RectTransform> settingRows = new List<RectTransform>();

        PadAction? listening;
        Text listenLabel;
        float listenStartedAt, restoreNavigationAt;

        const float SheetInset = 38f, HeaderHeight = 135f, RowPad = 32f;

        void BuildSettings(RectTransform layer)
        {
            var scrim = UiFactory.MakePanel(layer, "Scrim", new Color(0f, 0f, 0f, 0.55f), true);
            UiFactory.Stretch(scrim.rectTransform);
            var close = scrim.gameObject.AddComponent<Button>();
            close.transition = Selectable.Transition.None;
            close.navigation = UiFactory.NoNavigation;
            close.onClick.AddListener(CloseSettings);
            settingsScrim = scrim.gameObject.AddComponent<CanvasGroup>();

            // The sheet: 38 in from every edge (landscape: at most 1300 wide, centred).
            var sheet = MenuStyle.Panel(layer, "Settings Sheet", MenuSlot.Panel, 1f, null, true);
            settingsSheet = sheet.rectTransform;
            float width = builtPortrait ? builtSize.x - SheetInset * 2f : Mathf.Min(builtSize.x - SheetInset * 2f, 1300f);
            settingsSheet.anchorMin = new Vector2(0.5f, 0f);
            settingsSheet.anchorMax = new Vector2(0.5f, 1f);
            settingsSheet.pivot = new Vector2(0.5f, 0.5f);
            settingsSheet.offsetMin = new Vector2(-width / 2f, SheetInset);
            settingsSheet.offsetMax = new Vector2(width / 2f, -SheetInset - topInset); // clear of the camera hole
            sheet.gameObject.AddComponent<Mask>().showMaskGraphic = true; // round corners for the header too
            rowWidth = width - RowPad * 2f;

            var header = UiFactory.MakePanel(settingsSheet, "Header", Color.white);
            MenuStyle.Tint(header, MenuSlot.Primary);
            TopBar(header.rectTransform, 0f, HeaderHeight);
            var gearHolder = UiFactory.MakeRect(header.transform, "Gear");
            UiFactory.Place(gearHolder, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(40f + 46f, 0f), new Vector2(92f, 92f));
            settingsGear = gearHolder;
            if (MenuStyle.Icon(gearHolder, "SettingsFilled", 92f, Color.white) == null)
                MenuStyle.Label(gearHolder, "*", 80f, TextAnchor.MiddleCenter, Color.white, true);
            var titleRect = UiFactory.MakeRect(header.transform, "Title");
            UiFactory.Place(titleRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(150f, 6f), new Vector2(width - 330f, 100f));
            MenuStyle.Title(titleRect, "Settings", "Pengaturan", "Settings", 65f, TextAnchor.MiddleLeft, Color.white);
            var version = MenuStyle.Label(header.transform, MenuStyle.T("Versi ", "Version ") + Application.version, 28f, TextAnchor.LowerRight, MenuStyle.Soft);
            UiFactory.Place(version.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-HeaderHeight - 18f, 14f), new Vector2(400f, 40f));
            var x = SquareButton(header.transform, "Close", "Close", HeaderHeight, 80f, MenuSlot.Primary, 1f, CloseSettings, true);
            var xImage = x.GetComponent<Image>();
            MenuStyle.Untint(xImage);
            xImage.sprite = null;
            xImage.color = new Color(0f, 0f, 0f, 0.18f);
            UiFactory.Place(x, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(HeaderHeight, HeaderHeight));

            settingsScroll = UiFactory.MakeGridList(settingsSheet, Vector2.one, 0f, 1, Color.clear, out settingsContent);
            // Rows are placed by hand (different heights): no grid, no scroll bar (swiped like a phone list).
            DestroyImmediate(settingsContent.GetComponent<ContentSizeFitter>());
            DestroyImmediate(settingsContent.GetComponent<GridLayoutGroup>());
            Destroy(settingsScroll.verticalScrollbar.gameObject);
            settingsScroll.verticalScrollbar = null;
            settingsScroll.viewport.offsetMin = settingsScroll.viewport.offsetMax = Vector2.zero;
            settingsScroll.movementType = ScrollRect.MovementType.Elastic;
            settingsScroll.GetComponent<Image>().color = Color.clear;
            UiFactory.Stretch((RectTransform)settingsScroll.transform, 0f, 0f, 0f, HeaderHeight);

            settingsY = -20f;
            bool mobile = GraphicsQuality.Mobile;
            Section("Audio", "Audio", "Audio");
            SliderRow("Musik", "Music", 0f, 100f, 1f, () => GameSettings.MusicVolume * 100f, v => GameSettings.MusicVolume = v / 100f, v => $"{v:0}");
            SliderRow("Efek Suara", "Sound FX", 0f, 100f, 1f, () => GameSettings.SfxVolume * 100f, v => GameSettings.SfxVolume = v / 100f, v => $"{v:0}");
            ChoiceRow("Musik Menu", "Menu Music", MenuSongs, () => GameSettings.MenuMusic, i => GameSettings.MenuMusic = i);

            Section("Display & Grafik", "Display & Graphics", "SunFilled");
            ChoiceRow("Kualitas", "Quality", GraphicsQuality.Names, () => (int)GraphicsQuality.Level, i => GraphicsQuality.Level = (GraphicsLevel)i);
            Note(mobile
                ? MenuStyle.T("Android: Low .. Ultra tanpa bayangan; Ultra+Shadow = bayangan dekat bola saja (~10 blok). Lebih rendah = lebih lancar di HP lemah.", "Android: Low .. Ultra without shadows; Ultra+Shadow = shadows near the ball only (~10 blocks). Lower = smoother on weak phones.")
                : MenuStyle.T("RTX = semua efek nyala. Turunkan kalau game patah-patah.", "RTX = everything on. Lower it if the game stutters."));
            ChoiceRow("Bloom", "Bloom", GraphicsQuality.BloomNames, () => (int)GraphicsQuality.BloomChoice, i => GraphicsQuality.BloomChoice = (BloomSetting)i);
            SliderRow("Kuat Bloom", "Bloom Strength", 0f, 200f, 5f, () => GraphicsQuality.BloomStrength * 100f, v => GraphicsQuality.BloomStrength = v / 100f, v => $"{v:0}%");
            if (!mobile)
            {
                ChoiceRow("Upscaler", "Upscaler", GraphicsQuality.UpscalerNames, () => (int)GraphicsQuality.Upscaler, i => GraphicsQuality.Upscaler = (UpscalerChoice)i);
                ChoiceRow("Mode Upscale", "Upscale Mode", GraphicsQuality.UpscaleModeNames, () => GraphicsQuality.UpscaleMode, i => GraphicsQuality.UpscaleMode = i);
            }
            ChoiceRow("Kamera", "Camera", new[] { "NORMAL", "CLASSIC", "HIGH", "FAR" }, () => (int)GameSettings.Camera, i => GameSettings.Camera = (CameraView)i, "Camera");

            Section("Sensitivity", "Sensitivity", "Slider");
            SliderRow("Sensitivity", "Sensitivity", GameSettings.MouseScale * GameSettings.MinSensitivity, GameSettings.MouseScale * GameSettings.MaxSensitivity, 10f,
                () => GameSettings.MouseSensitivity, v => GameSettings.MouseSensitivity = Mathf.RoundToInt(v), v => $"{v:0}");
            ToggleRow("Smooth", "Smooth", () => GameSettings.SmoothSteer, v => GameSettings.SmoothSteer = v);
            if (!mobile)
            {
                ToggleRow("Gerak Otomatis", "Auto Move", () => GameSettings.AutoMove, v => GameSettings.AutoMove = v);
                Note(MenuStyle.T("Nyala: bola ikut mouse tanpa tahan tombol.", "On: the ball follows the mouse without holding the button."));
            }

            Section("Controller", "Controller", "XboxLeftStick");
            ChoiceRow("Setir Pakai", "Steer With", new[] { "STICK", "LT RT", "LB RB" }, () => (int)GameSettings.SteerSource, i => GameSettings.SteerSource = (SteerSource)i);
            SliderRow("Kecepatan", "Controller Speed", GameSettings.MinSensitivity, GameSettings.MaxSensitivity, 0.05f,
                () => GameSettings.StickSensitivity, v => GameSettings.StickSensitivity = v, v => $"x{v:0.00}");
            SliderRow("Accelerator", "Accelerator", GameSettings.MinAccelerator, GameSettings.MaxAccelerator, 0.1f,
                () => GameSettings.Accelerator, v => GameSettings.Accelerator = v, v => $"x{v:0.0}");
            Note(MenuStyle.T("Tahan Accelerator (A / Cross) atau Spasi: bola belok ke samping lebih cepat.", "Hold Accelerator (A / Cross) or Space: the ball steers sideways faster."));

            Section("Tombol Controller", "Controller Buttons", "XboxA");
            BindRow(PadAction.LaneLeft, "Lajur Kiri", "Lane Left");
            BindRow(PadAction.LaneRight, "Lajur Kanan", "Lane Right");
            BindRow(PadAction.Accelerator, "Accelerator", "Accelerator");
            BindRow(PadAction.SteerSpeed, "Speed / Ulang", "Speed / Restart");
            BindRow(PadAction.Pause, "Pause", "Pause");
            BindRow(PadAction.Back, "Kembali", "Back");
            BindRow(PadAction.Hitboxes, "Hitbox", "Hitboxes");
            ButtonRow(MenuStyle.T("RESET TOMBOL CONTROLLER", "RESET CONTROLLER BUTTONS"), () => { GameSettings.ResetBindings(); RefreshSettings(); });

            if (!mobile)
            {
                Section("Keyboard", "Keyboard", "InputField");
                Note("A / D  ·  ← / →  " + MenuStyle.T("belok", "steer") + "  ·  Space accelerator  ·  P pause  ·  R restart  ·  F1 hitboxes  ·  Esc " + MenuStyle.T("kembali", "back"));
            }

            Section("Warna Menu", "Menu Color", "Brush");
            ColorRow();
            Section("Bahasa", "Language", null);
            ChoiceRow("Bahasa", "Language", MenuStyle.LanguageNames, () => MenuStyle.English ? 1 : 0, i =>
            {
                if ((i == 1) == MenuStyle.English) return;
                MenuStyle.English = i == 1;
                MenuTween.After(this, "language", 0.4f, Rebuild); // let the highlight slide, then every text changes
            });

            settingsY -= 20f;
            ButtonRow(MenuStyle.T("RESET SEMUA", "RESET ALL"), () => { GameSettings.ResetAll(); RefreshSettings(); }, MenuSlot.Primary);
            settingsContent.sizeDelta = new Vector2(0f, -settingsY + 60f);
        }

        // ---------------- Opening and closing ----------------

        void OpenSettings(bool instant)
        {
            if (settingsOpen || busy) return;
            settingsOpen = true;
            settingsLayer.gameObject.SetActive(true);
            SetNav(NavSettings, !instant);
            RefreshSettings();
            settingsContent.anchoredPosition = Vector2.zero;
            ignoreInputUntil = Time.unscaledTime + 0.2f;
            float h = root.rect.height;
            if (instant)
            {
                settingsSheet.anchoredPosition = Vector2.zero;
                settingsSheet.localScale = Vector3.one;
                settingsScrim.alpha = 1f;
            }
            else
            {
                var sheet = settingsSheet;
                MenuTween.To(sheet, "open", 0.7f, MenuEase.OutQuint, e =>
                {
                    sheet.anchoredPosition = new Vector2(0f, -h * (1f - e));
                    sheet.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, e);
                });
                MenuTween.Fade(settingsScrim, 0f, 1f, 0.45f, MenuEase.OutQuint);
                var gear = settingsGear;
                MenuTween.To(gear, "spin", 1f, MenuEase.OutQuint, e =>
                {
                    gear.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-180f, 0f, e));
                    gear.localScale = Vector3.one * Mathf.Lerp(0.5f, 1f, e);
                });
                // The rows come in one after another from the left.
                for (int i = 0; i < settingRows.Count; i++)
                {
                    var row = settingRows[i];
                    var group = row.GetComponent<CanvasGroup>();
                    MenuTween.To(row, "enter", 0.6f, MenuEase.OutQuint, e =>
                    {
                        row.anchoredPosition = new Vector2(-65f * (1f - e), row.anchoredPosition.y);
                        group.alpha = e;
                    }, 0.18f + Mathf.Min(i, 24) * 0.035f);
                }
            }
            Choose(firstSetting);
        }

        void CloseSettings()
        {
            if (!settingsOpen) return;
            StopListening();
            settingsOpen = false;
            SetNav(NavFor(screen), true);
            ignoreInputUntil = Time.unscaledTime + 0.2f;
            Choose(null);
            float h = root.rect.height;
            var sheet = settingsSheet;
            MenuTween.To(sheet, "open", 0.5f, MenuEase.InQuint, e =>
            {
                sheet.anchoredPosition = new Vector2(0f, -h * e);
                sheet.localScale = Vector3.one * Mathf.Lerp(1f, 0.96f, e);
            }, 0f, () =>
            {
                if (!settingsOpen) settingsLayer.gameObject.SetActive(false);
            });
            MenuTween.Fade(settingsScrim, settingsScrim.alpha, 0f, 0.5f, MenuEase.InQuint);
        }

        static void Choose(Selectable s)
        {
            if (EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(s != null ? s.gameObject : null);
        }

        void RefreshSettings()
        {
            foreach (var r in refreshers) r();
        }

        // ---------------- Rows ----------------

        RectTransform Row(float height)
        {
            var row = UiFactory.MakeRect(settingsContent, "Row");
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.offsetMin = new Vector2(RowPad, settingsY - height);
            row.offsetMax = new Vector2(-RowPad, settingsY);
            settingsY -= height;
            row.gameObject.AddComponent<CanvasGroup>();
            settingRows.Add(row);
            return row;
        }

        /// <summary>A section heading: a Primary bar with an icon (from the Icons folder) and its name.</summary>
        void Section(string indonesian, string english, string icon)
        {
            var row = Row(103f + 26f);
            var bar = MenuStyle.Panel(row, "Section", MenuSlot.Primary).rectTransform;
            UiFactory.Stretch(bar, 0f, 0f, 0f, 26f);
            float x = 30f;
            var image = MenuStyle.Icon(bar, icon, 60f, Color.white);
            if (image != null)
            {
                UiFactory.Place(image.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(60f, 60f));
                x = 104f;
            }
            var label = MenuStyle.Label(bar, MenuStyle.T(indonesian, english), 54f, TextAnchor.MiddleLeft, Color.white);
            UiFactory.Stretch(label.rectTransform, x, 0f, 20f, 0f);
            MenuStyle.Fit(label, 30);
        }

        /// <summary>A setting row: its name on the left, and the rect its control goes in (the right 52%).</summary>
        RectTransform Line(string indonesian, string english, string icon = null, float height = 113f)
        {
            var row = Row(height);
            var line = UiFactory.MakePanel(row, "Line", new Color(1f, 1f, 1f, 0.04f));
            line.rectTransform.anchorMin = Vector2.zero;
            line.rectTransform.anchorMax = new Vector2(1f, 0f);
            line.rectTransform.offsetMin = Vector2.zero;
            line.rectTransform.offsetMax = new Vector2(0f, 2f);
            float x = 10f;
            var image = MenuStyle.Icon(row, icon, 50f, MenuStyle.Soft);
            if (image != null)
            {
                UiFactory.Place(image.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(50f, 50f));
                x = 74f;
            }
            var label = MenuStyle.Label(row, MenuStyle.T(indonesian, english), 50f, TextAnchor.MiddleLeft, Color.white);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = new Vector2(0.46f, 1f);
            label.rectTransform.offsetMin = new Vector2(x, 0f);
            label.rectTransform.offsetMax = Vector2.zero;
            MenuStyle.Fit(label, 26);
            var area = UiFactory.MakeRect(row, "Control");
            area.anchorMin = new Vector2(0.48f, 0.5f);
            area.anchorMax = new Vector2(1f, 0.5f);
            area.pivot = new Vector2(0.5f, 0.5f);
            area.offsetMin = new Vector2(0f, -40f);
            area.offsetMax = new Vector2(-10f, 40f);
            return area;
        }

        void Note(string text)
        {
            const float Size = 32f;
            float chars = Mathf.Max(1f, rowWidth / (Size * 0.5f));
            int lines = Mathf.Max(1, Mathf.CeilToInt(text.Length / chars));
            var row = Row(lines * Size * 1.3f + 22f);
            var label = MenuStyle.Label(row, text, Size, TextAnchor.UpperLeft, MenuStyle.Soft);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiFactory.Stretch(label.rectTransform, 10f, 0f, 10f, 8f);
        }

        void SliderRow(string indonesian, string english, float min, float max, float step, Func<float> get, Action<float> set, Func<float, string> format)
        {
            var area = Line(indonesian, english);
            var s = MenuSlider.Make(area, min, max);
            var slider = s.Slider;
            slider.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            slider.onValueChanged.AddListener(v =>
            {
                v = Mathf.Clamp(Mathf.Round(v / step) * step, min, max);
                set(v);
                s.Value.text = format(v);
            });
            refreshers.Add(() =>
            {
                float v = get();
                slider.SetValueWithoutNotify(v);
                s.Value.text = format(v);
            });
            if (firstSetting == null) firstSetting = slider;
        }

        void ToggleRow(string indonesian, string english, Func<bool> get, Action<bool> set)
        {
            var area = Line(indonesian, english);
            var toggle = MenuToggle.Make(area, get(), set);
            UiFactory.Place((RectTransform)toggle.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(MenuToggle.Width, MenuToggle.Height));
            toggle.Button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            refreshers.Add(() =>
            {
                if (toggle.IsOn != get()) toggle.Set(get(), toggle.isActiveAndEnabled, false);
            });
            if (firstSetting == null) firstSetting = toggle.Button;
        }

        /// <summary>Up to 3 choices side by side (a sliding highlight); more = &lt; value &gt; (a stepper).</summary>
        void ChoiceRow(string indonesian, string english, string[] options, Func<int> get, Action<int> set, string icon = null)
        {
            var area = Line(indonesian, english, icon);
            if (options.Length <= 3)
            {
                var seg = MenuSegmented.Make(area, options, get(), i => { set(i); RefreshSettings(); });
                foreach (var b in seg.Buttons) b.navigation = new Navigation { mode = Navigation.Mode.Automatic };
                refreshers.Add(() =>
                {
                    if (seg.Index != get()) seg.Select(get(), seg.isActiveAndEnabled, false);
                });
                if (firstSetting == null) firstSetting = seg.Buttons[0];
            }
            else
            {
                var stepper = MenuStepper.Make(area, options, get(), i => { set(i); RefreshSettings(); });
                stepper.Button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
                refreshers.Add(() =>
                {
                    if (stepper.Index != get()) stepper.Set(get(), false, false);
                });
                if (firstSetting == null) firstSetting = stepper.Button;
            }
        }

        void ButtonRow(string name, UnityEngine.Events.UnityAction click, MenuSlot slot = MenuSlot.Primary2)
        {
            var row = Row(130f);
            var back = MenuStyle.Panel(row, name, slot, 1f, null, true);
            UiFactory.Stretch(back.rectTransform, 0f, 14f, 0f, 14f);
            var b = back.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            b.onClick.AddListener(click);
            var label = MenuStyle.Label(back.transform, name, 44f, TextAnchor.MiddleCenter, Color.white, true);
            MenuStyle.Fit(label, 24);
            MenuPress.Add(b, 0.95f);
        }

        /// <summary>The four colour sets as swatches (Primary with an Accent dot); the chosen one has a white ring.</summary>
        void ColorRow()
        {
            var area = Line("Warna", "Colors", null, 150f);
            var ring = MenuStyle.Panel(area, "Ring", MenuSlot.Fixed);
            ring.rectTransform.anchorMin = ring.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            ring.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            ring.rectTransform.sizeDelta = new Vector2(112f, 112f);
            var swatches = new RectTransform[4];
            for (int i = 0; i < 4; i++)
            {
                var set = (MenuColors)i;
                var swatch = UiFactory.MakePanel(area, MenuStyle.ColorNames[i], MenuStyle.Get(set, MenuSlot.Primary), true);
                swatch.sprite = MenuStyle.Rounded;
                swatch.type = Image.Type.Sliced;
                var rt = swatches[i] = swatch.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2((i + 0.5f) / 4f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(96f, 96f);
                var dot = UiFactory.MakePanel(swatch.transform, "Accent", MenuStyle.Get(set, MenuSlot.Accent));
                dot.sprite = MenuStyle.Circle;
                UiFactory.Place(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 40f));
                var b = swatch.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.navigation = new Navigation { mode = Navigation.Mode.Automatic };
                b.onClick.AddListener(() =>
                {
                    if (MenuStyle.Colors == set) return;
                    MenuStyle.UseColors(set);
                    RefreshSettings();
                    MenuToast.Show(MenuStyle.ColorNames[(int)set]);
                });
                MenuPress.Add(b);
            }
            refreshers.Add(() =>
            {
                var target = swatches[(int)MenuStyle.Colors];
                Vector2 a0 = ring.rectTransform.anchorMin, a1 = target.anchorMin;
                if (a0 == a1) return;
                MenuTween.To(ring.rectTransform, "slide", 0.5f, MenuEase.OutBack, e =>
                    ring.rectTransform.anchorMin = ring.rectTransform.anchorMax = Vector2.LerpUnclamped(a0, a1, e));
            });
            ring.rectTransform.anchorMin = ring.rectTransform.anchorMax = swatches[(int)MenuStyle.Colors].anchorMin;
        }

        void BindRow(PadAction action, string indonesian, string english)
        {
            var area = Line(indonesian, english);
            var back = MenuStyle.Panel(area, "Binding", MenuSlot.Panel, 1f, null, true);
            MenuStyle.Tint(back, MenuSlot.Panel, 1f, 0.08f);
            UiFactory.Stretch(back.rectTransform);
            Button b = back.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            b.onClick.AddListener(() => StartListening(action, b));
            MenuPress.Add(b, 0.95f);
            var label = MenuStyle.Label(back.transform, "", 38f, TextAnchor.MiddleLeft, Color.white, true);
            UiFactory.Stretch(label.rectTransform, 90f, 0f, 16f, 0f);
            MenuStyle.Fit(label, 20);
            // The controller button's picture (Xbox A, LB, D-pad...) on the left of its name.
            var pad = UiFactory.MakePanel(back.transform, "Button Icon", Color.white);
            pad.preserveAspect = true;
            UiFactory.Place(pad.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(56f, 56f));
            refreshers.Add(() =>
            {
                if (listening == action) return;
                label.text = GameInput.ButtonName(action);
                label.color = Color.white;
                var sprite = UiFactory.Icon(PadIcon(GameSettings.Binding(action)));
                pad.sprite = sprite;
                pad.enabled = sprite != null;
            });
            if (firstSetting == null) firstSetting = b;
        }

        /// <summary>The Xbox picture for a controller binding (Icons/XboxA ...), or null.</summary>
        static string PadIcon(string binding)
        {
            switch (binding)
            {
                case "buttonSouth": return "XboxA";
                case "buttonEast": return "XboxB";
                case "buttonWest": return "XboxX";
                case "buttonNorth": return "XboxY";
                case "start": return "XboxMenu";
                case "select": return "XboxView";
                case "dpad/up": return "XboxDpadUp";
                case "dpad/down": return "XboxDpadDown";
                case "dpad/left": return "XboxDpadLeft";
                case "dpad/right": return "XboxDpadRight";
                case "leftShoulder": return "XboxLB";
                case "rightShoulder": return "XboxRB";
                case "leftTrigger": return "XboxLT";
                case "rightTrigger": return "XboxRT";
                case "leftStickPress": return "XboxLSPress";
                case "rightStickPress": return "XboxRSPress";
            }
            return binding != null && binding.StartsWith("dpad") ? "XboxDpad" : null;
        }

        // ---------------- Controller mapping ----------------

        void StartListening(PadAction action, Button button)
        {
            if (Time.unscaledTime < ignoreInputUntil || listening != null) return;
            listening = action;
            listenLabel = button.GetComponentInChildren<Text>();
            listenLabel.text = HasController() ? MenuStyle.T("TEKAN TOMBOL...", "PRESS A BUTTON...") : MenuStyle.T("SAMBUNGKAN CONTROLLER", "CONNECT A CONTROLLER");
            listenLabel.color = MenuStyle.GemGold;
            listenStartedAt = Time.unscaledTime;
            // The press that maps must not also press the menu's buttons.
            if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = false;
        }

        static bool HasController()
        {
#if ENABLE_INPUT_SYSTEM
            return Gamepad.current != null;
#else
            return Input.GetJoystickNames().Length > 0;
#endif
        }

        void StopListening()
        {
            if (listening == null) return;
            listening = null;
            restoreNavigationAt = Time.unscaledTime + 0.3f;
            ignoreInputUntil = Time.unscaledTime + 0.3f;
            RefreshSettings();
        }

        /// <summary>While a controller button is being mapped: true (the menu ignores other input).</summary>
        bool UpdateListening()
        {
            if (listening == null)
            {
                if (restoreNavigationAt > 0f && Time.unscaledTime >= restoreNavigationAt)
                {
                    restoreNavigationAt = 0f;
                    if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = true;
                }
                return false;
            }
            if (Time.unscaledTime - listenStartedAt > 0.15f && GameInput.ListenForButton(out var binding))
            {
                GameSettings.SetBinding(listening.Value, binding);
                StopListening();
            }
            else if (EscapePressed() || Time.unscaledTime - listenStartedAt > 8f) StopListening(); // Esc or nothing pressed: unchanged
            return true;
        }

        static bool EscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            return k != null && k.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        /// <summary>Scrolls the settings so the control just chosen with the keyboard or controller is in view.</summary>
        void KeepSelectedVisible()
        {
            var es = EventSystem.current;
            var go = es != null ? es.currentSelectedGameObject : null;
            if (go == lastSelected) return;
            lastSelected = go;
            if (go == null || settingsContent == null || !go.transform.IsChildOf(settingsContent)) return;
            var corners = new Vector3[4];
            var view = new Vector3[4];
            ((RectTransform)go.transform).GetWorldCorners(corners);
            settingsScroll.viewport.GetWorldCorners(view);
            float scale = Mathf.Max(0.0001f, settingsContent.lossyScale.y);
            float margin = 60f * scale, shift = 0f;
            if (corners[0].y < view[0].y + margin) shift = view[0].y + margin - corners[0].y;
            else if (corners[1].y > view[1].y - margin) shift = view[1].y - margin - corners[1].y;
            if (shift == 0f) return;
            var p = settingsContent.anchoredPosition;
            float max = Mathf.Max(0f, settingsContent.rect.height - settingsScroll.viewport.rect.height);
            p.y = Mathf.Clamp(p.y + shift / scale, 0f, max);
            settingsContent.anchoredPosition = p;
        }
    }
}
