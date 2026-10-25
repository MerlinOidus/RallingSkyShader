using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RTXOn
{
    // The Main Menu's screens (Adrian's design 2026-10-24): Home, the level list (Official / Custom), the level Detail
    // and the navbar. Sizes are the design's pixels on the 1080-wide portrait canvas; the landscape canvas is 1080 high.
    public partial class MainMenu
    {
        enum MenuScreen { Home, Levels, Detail, PcLevels }

        const int NavHome = 0, NavCustom = 1, NavSkin = 2, NavOfficial = 3, NavSettings = 4;
        static readonly string[] NavIcons = { "Home", "Layer", "Brush", "Star", "Settings" };
        static readonly string[] NavFilled = { "HomeFilled", null, null, "StarFilled", "SettingsFilled" };
        static readonly string[] NavFallback = { "H", "C", "S", "O", "*" };

        MenuScreen screen;
        bool builtPortrait;
        Vector2 builtSize;
        Canvas menuCanvas;
        RectTransform root, homeRt, levelsRt, detailRt, flyLayer;
        /// <summary>A screen change that must finish first (a picture flying), so taps don't stack up.</summary>
        bool busy;
        float ignoreInputUntil;

        /// <summary>The canvas size in its own units: 1080 wide when portrait, 1080 high when landscape.</summary>
        static Vector2 CanvasSize(bool portrait)
        {
            float w = Mathf.Max(1, Screen.width), h = Mathf.Max(1, Screen.height);
            return portrait ? new Vector2(1080f, 1080f * h / w) : new Vector2(1080f * w / h, 1080f);
        }

        void BuildUi(bool portrait)
        {
            builtPortrait = portrait;
            builtSize = CanvasSize(portrait);
            topInset = ScreenRotation.TopInset(builtSize.y); // phones: the top row clear of the camera hole
            menuCanvas = UiFactory.MakeCanvas("Main Menu UI", portrait ? new Vector2(1080f, 1920f) : new Vector2(1920f, 1080f));
            var scaler = menuCanvas.GetComponent<CanvasScaler>();
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = portrait ? 0f : 1f; // portrait follows the width, landscape the height
            root = (RectTransform)menuCanvas.transform;

            var screens = Layer("Screens");
            homeRt = Page(screens, "Home");
            BuildHome(homeRt);
            levelsRt = Page(screens, "Levels");
            BuildLevels(levelsRt);
            detailRt = Page(screens, "Detail");
            BuildDetail(detailRt);
            pcRt = null;
            if (PcLayout)
            {
                // PC held sideways: the level list and the chosen level's 3D preview on one screen (MainMenuPc.cs).
                pcRt = Page(screens, "PC Levels");
                BuildPcLevels(pcRt);
            }
            BuildNavbar(Layer("Navbar"));
            settingsLayer = Layer("Settings");
            BuildSettings(settingsLayer);
            settingsLayer.gameObject.SetActive(false);
            flyLayer = Layer("Flying Pictures");
            MenuToast.layer = Layer("Toasts");
        }

        /// <summary>How far the top row of every page moves down on a phone held upright (ScreenRotation.TopInset).</summary>
        float topInset;

        RectTransform Layer(string name) => UiFactory.Stretch(UiFactory.MakeRect(root, name));

        static RectTransform Page(Transform parent, string name)
        {
            var rt = UiFactory.Stretch(UiFactory.MakeRect(parent, name));
            rt.gameObject.AddComponent<CanvasGroup>();
            return rt;
        }

        /// <summary>The phone was turned, the language changed...: the whole menu is built again, on the same screen.</summary>
        void Rebuild()
        {
            bool keepSettings = settingsOpen;
            StopListening();
            MenuStyle.ForgetTints();
            if (menuCanvas != null)
            {
                menuCanvas.gameObject.SetActive(false);
                Destroy(menuCanvas.gameObject);
            }
            settingsOpen = false;
            busy = false;
            cards.Clear();
            particles.Clear();
            refreshers.Clear();
            settingRows.Clear();
            firstSetting = null;
            BuildUi(ScreenRotation.Portrait);
            if (screen == MenuScreen.Detail) scrollTo = detailEntry;
            if (screen == MenuScreen.PcLevels) scrollTo = pcEntry;
            ShowScreen(screen, true);
            if (keepSettings) OpenSettings(true);
        }

        /// <summary>Shows a screen at once (opening the menu, or after building it again).</summary>
        void ShowScreen(MenuScreen s, bool instant)
        {
            // The PC screen takes the place of the level list and Detail (and gives it back to a window held upright).
            if (PcLayout && (s == MenuScreen.Levels || s == MenuScreen.Detail))
            {
                if (s == MenuScreen.Detail && detailEntry != null) scrollTo = detailEntry;
                s = MenuScreen.PcLevels;
            }
            else if (!PcLayout && s == MenuScreen.PcLevels) s = MenuScreen.Levels;
            screen = s;
            if (s == MenuScreen.Detail && detailEntry == null) screen = s = MenuScreen.Levels;
            homeRt.gameObject.SetActive(s == MenuScreen.Home);
            levelsRt.gameObject.SetActive(s == MenuScreen.Levels || s == MenuScreen.Detail);
            detailRt.gameObject.SetActive(s == MenuScreen.Detail);
            if (pcRt != null) pcRt.gameObject.SetActive(s == MenuScreen.PcLevels);
            homeRt.anchoredPosition = levelsRt.anchoredPosition = Vector2.zero;
            if (pcRt != null) pcRt.anchoredPosition = Vector2.zero;
            homeRt.localScale = Vector3.one;
            homeDim.color = Color.clear;
            if (s == MenuScreen.Levels || s == MenuScreen.Detail) FillLevels();
            if (s == MenuScreen.PcLevels) FillPcLevels();
            else if (s != MenuScreen.Detail) HidePreview();
            if (s == MenuScreen.Detail)
            {
                FillDetail(detailEntry);
                detailRt.GetComponent<CanvasGroup>().alpha = 1f;
                PlayDetailEnter();
            }
            if (s == MenuScreen.Home) PlayHomeIntro();
            HideNavbar(s == MenuScreen.Detail || s == MenuScreen.PcLevels, instant);
            SetNav(NavFor(s), false);
            ignoreInputUntil = Time.unscaledTime + 0.2f;
        }

        int NavFor(MenuScreen s) => s == MenuScreen.Home ? NavHome : listTab == CustomTab ? NavCustom : NavOfficial;

        // ---------------- Home ----------------

        class Particle
        {
            public RectTransform rt;
            public Image image;
            public float x, speed, phase, alpha;
        }

        readonly List<Particle> particles = new List<Particle>();
        RectTransform logoHolder, homeTap;
        CanvasGroup logoGroup, subtitleGroup, tapGroup;
        RectTransform subtitleRt, logoFloat;
        Image homeDim;
        Vector2 logoHome;

        void BuildHome(RectTransform page)
        {
            bool portrait = builtPortrait;
            // The whole screen is the "tap to play" button.
            var catcher = UiFactory.MakePanel(page, "Tap Anywhere", Color.clear, true);
            UiFactory.Stretch(catcher.rectTransform);
            var tap = catcher.gameObject.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;
            tap.navigation = UiFactory.NoNavigation;
            tap.onClick.AddListener(() => OpenLevels(OfficialTab));

            var bgFrame = UiFactory.Stretch(UiFactory.MakeRect(page, "Background"));
            var bg = MenuStyle.Cover(bgFrame, HomeBackground);
            // Drift: the background breathes in and out (13 s each way).
            MenuTween.Loop(bg.transform, "drift", 13f, MenuEase.InOutCubic, e => bg.transform.localScale = Vector3.one * Mathf.Lerp(1.06f, 1.14f, e));

            // 18 specks of light rising.
            var specks = UiFactory.Stretch(UiFactory.MakeRect(page, "Particles"));
            for (int i = 0; i < 18; i++)
            {
                var dot = UiFactory.MakePanel(specks, "Speck", Color.white);
                dot.sprite = MenuStyle.Circle;
                float size = 6f + (i * 7 % 5) * 3f;
                UiFactory.Place(dot.rectTransform, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
                particles.Add(new Particle
                {
                    rt = dot.rectTransform,
                    image = dot,
                    x = (i * 0.618f) % 1f,
                    speed = 0.03f + (i * 13 % 7) * 0.006f,
                    phase = (i * 0.37f) % 1f,
                    alpha = 0.25f + (i % 4) * 0.12f,
                });
            }

            // The logo, "REMASTERED" under its title, and "tap to play".
            float size2 = portrait ? 1080f : 620f, k = size2 / 1080f;
            logoHolder = UiFactory.MakeRect(page, "Logo");
            logoHome = new Vector2(0f, (portrait ? -173f : -10f) - topInset);
            UiFactory.Place(logoHolder, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), logoHome, new Vector2(size2, size2));
            logoGroup = logoHolder.gameObject.AddComponent<CanvasGroup>();
            logoGroup.blocksRaycasts = false;
            logoFloat = UiFactory.Stretch(UiFactory.MakeRect(logoHolder, "Float"));
            var tex = logo != null ? logo : Resources.Load<Texture2D>("RTXOnLogo");
            if (tex != null)
            {
                var image = UiFactory.MakeRect(logoFloat, "Logo Image").gameObject.AddComponent<RawImage>();
                image.texture = tex;
                image.raycastTarget = false;
                float h = size2, w = h * tex.width / Mathf.Max(1f, tex.height);
                UiFactory.Place(image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, h));
            }
            else MenuStyle.Label(logoFloat, "ROLLING\nSKY", 150f * k, TextAnchor.MiddleCenter, Color.white, true);
            MenuTween.Loop(logoFloat, "float", 2.5f, MenuEase.InOutCubic, e => logoFloat.anchoredPosition = new Vector2(0f, Mathf.Lerp(-13f, 13f, e)));

            subtitleRt = UiFactory.MakeRect(logoFloat, "Subtitle");
            UiFactory.Place(subtitleRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -308f * k), new Vector2(size2, 120f * k));
            subtitleGroup = subtitleRt.gameObject.AddComponent<CanvasGroup>();
            if (!string.IsNullOrEmpty(subtitle))
                MenuStyle.Title(subtitleRt, "Remastered", subtitle, subtitle, 93f * k, TextAnchor.MiddleCenter, MenuStyle.Hex("#111111"));

            homeTap = UiFactory.MakeRect(page, "Tap To Play");
            UiFactory.Place(homeTap, new Vector2(0.5f, portrait ? 0.27f : 0.3f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 90f));
            tapGroup = homeTap.gameObject.AddComponent<CanvasGroup>();
            tapGroup.blocksRaycasts = false;
            var tapText = MenuStyle.Title(homeTap, "Start", "KETUK UNTUK MAIN", "TAP TO PLAY", 41f, TextAnchor.MiddleCenter, Color.white, true);
            var shadow = tapText.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
            shadow.effectDistance = new Vector2(0f, -3f);
            MenuTween.Loop(homeTap, "pulse", 1.1f, MenuEase.InOutCubic, e =>
            {
                tapGroup.alpha = Mathf.Lerp(1f, 0.35f, e);
                homeTap.localScale = Vector3.one * Mathf.Lerp(1f, 0.97f, e);
            });

            if (!Application.isMobilePlatform)
            {
                // PC: a small X in the corner closes the game (phones use their own Home button).
                var quit = SquareButton(page, "Quit", "Close", 96f, 54f, MenuSlot.Primary, 0.75f, Quit, true);
                UiFactory.Place(quit, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-32f, -32f), new Vector2(96f, 96f));
            }

            homeDim = UiFactory.MakePanel(page, "Dim", Color.clear);
            UiFactory.Stretch(homeDim.rectTransform);
        }

        void PlayHomeIntro()
        {
            MenuTween.To(logoHolder, "intro", 1.1f, MenuEase.OutBack, e =>
            {
                logoHolder.anchoredPosition = logoHome + new Vector2(0f, -151f * (1f - e));
                logoHolder.localScale = Vector3.one * Mathf.LerpUnclamped(0.75f, 1f, e);
                logoHolder.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(-4f, 0f, e));
            });
            MenuTween.Fade(logoGroup, 0f, 1f, 0.6f, MenuEase.OutQuint);
            MenuTween.To(subtitleRt, "intro", 0.9f, MenuEase.OutQuint, e =>
            {
                subtitleGroup.alpha = e;
                subtitleRt.localScale = new Vector3(Mathf.Lerp(1.25f, 1f, e), 1f, 1f); // letters closing up
            }, 0.5f);
        }

        void AnimateParticles()
        {
            if (homeRt == null || !homeRt.gameObject.activeInHierarchy) return;
            float t = Time.unscaledTime;
            var size = homeRt.rect.size;
            foreach (var p in particles)
            {
                float y = (t * p.speed + p.phase) % 1f;
                p.rt.anchoredPosition = new Vector2(p.x * size.x + Mathf.Sin(t * 0.7f + p.phase * 6f) * 18f, -40f + y * (size.y + 80f));
                p.image.color = new Color(1f, 1f, 1f, Mathf.Sin(y * Mathf.PI) * p.alpha);
            }
        }

        /// <summary>Home → level list: Home slides away (30% left, smaller, darker) while the list comes in from the right.</summary>
        void OpenLevels(int tab)
        {
            if (busy || settingsOpen) return;
            if (PcLayout)
            {
                OpenPcLevels(tab);
                return;
            }
            if (screen == MenuScreen.Levels && listTab == tab) return;
            bool fromHome = screen == MenuScreen.Home;
            listTab = tab;
            screen = MenuScreen.Levels;
            SetNav(NavFor(screen), true);
            levelsRt.gameObject.SetActive(true);
            FillLevels();
            ignoreInputUntil = Time.unscaledTime + 0.2f;
            if (!fromHome) return;
            float w = root.rect.width;
            MenuTween.Move(levelsRt, new Vector2(w, 0f), Vector2.zero, 0.7f, MenuEase.OutQuint);
            MenuTween.To(homeRt, "leave", 0.7f, MenuEase.OutQuint, e => HomeAway(e, w), 0f, () => homeRt.gameObject.SetActive(false));
        }

        void HomeAway(float e, float w)
        {
            homeRt.anchoredPosition = new Vector2(-0.3f * w * e, 0f);
            homeRt.localScale = Vector3.one * Mathf.Lerp(1f, 0.94f, e);
            homeDim.color = new Color(0f, 0f, 0f, 0.6f * e);
        }

        void GoHome()
        {
            if (busy || screen == MenuScreen.Home) return;
            if (screen == MenuScreen.Detail || screen == MenuScreen.PcLevels)
            {
                detailRt.gameObject.SetActive(false);
                HideNavbar(false, false);
            }
            var page = screen == MenuScreen.PcLevels ? pcRt : levelsRt;
            screen = MenuScreen.Home;
            SetNav(NavHome, true);
            homeRt.gameObject.SetActive(true);
            float w = root.rect.width;
            MenuTween.To(homeRt, "leave", 0.7f, MenuEase.OutQuint, e => HomeAway(1f - e, w));
            MenuTween.Move(page, Vector2.zero, new Vector2(w, 0f), 0.7f, MenuEase.OutQuint, 0f, () =>
            {
                page.anchoredPosition = Vector2.zero;
                if (screen != MenuScreen.Home) return;
                page.gameObject.SetActive(false);
                if (page == pcRt)
                {
                    pcPreview.enabled = false;
                    HidePreview(); // nothing to draw while Home shows
                }
            });
            ignoreInputUntil = Time.unscaledTime + 0.2f;
        }

        // ---------------- Level list ----------------

        class Card
        {
            public Entry entry;
            public RectTransform root, inner, thumb, fill;
            public CanvasGroup group, thumbGroup, chosen;
            public float percent;
        }

        readonly List<Card> cards = new List<Card>();
        ScrollRect levelScroll;
        RectTransform levelContent, levelTitle;
        GameObject editorButton;
        Text emptyLabel;
        GameObject emptyEditor;
        int columns = 1, cursor = -1;
        float cardHeight;
        Entry scrollTo;
        const float CardGap = 43f;

        void BuildLevels(RectTransform page)
        {
            var bg = UiFactory.MakePanel(page, "Page", Color.white, true); // catches taps so Home underneath gets none
            UiFactory.Stretch(bg.rectTransform);
            MenuStyle.Tint(bg, MenuSlot.Page);

            var size = builtSize;
            columns = builtPortrait ? 1 : 2;
            cardHeight = builtPortrait ? 756f : 600f;
            float cellWidth = (size.x - 64f - CardGap * (columns - 1)) / columns;
            levelScroll = UiFactory.MakeGridList(page, new Vector2(cellWidth, cardHeight), CardGap, columns, Color.clear, out levelContent);
            levelScroll.GetComponent<Image>().color = Color.clear;
            Destroy(levelScroll.verticalScrollbar.gameObject); // swiped like a phone list: no scroll bar
            levelScroll.verticalScrollbar = null;
            levelScroll.viewport.offsetMin = levelScroll.viewport.offsetMax = Vector2.zero;
            levelScroll.movementType = ScrollRect.MovementType.Elastic;
            UiFactory.Stretch((RectTransform)levelScroll.transform, 0f, 0f, 0f, 194f + topInset);
            var grid = levelContent.GetComponent<GridLayoutGroup>();
            grid.padding = new RectOffset(32, 32, 22, 346); // room under the last card for the navbar

            // Title bar: the list's name, X back to Home, and (Custom) + for the Level Editor.
            var bar = MenuStyle.Panel(page, "Title Bar", MenuSlot.Primary).rectTransform;
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = Vector2.one;
            bar.pivot = new Vector2(0.5f, 1f);
            bar.offsetMin = new Vector2(32f, -43f - 119f - topInset);
            bar.offsetMax = new Vector2(-32f, -43f - topInset);
            levelTitle = UiFactory.Stretch(UiFactory.MakeRect(bar, "Title"), 140f, 0f, 140f, 0f);
            var close = SquareButton(bar, "Close", "Close", 119f, 65f, MenuSlot.Primary, 0f, GoHome, true);
            UiFactory.Place(close, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(119f, 119f));
            var add = SquareButton(bar, "Level Editor", "Add", 119f, 65f, MenuSlot.Primary, 0f, OpenEditor, false);
            UiFactory.Place(add, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(119f, 119f));
            editorButton = add.gameObject;

            emptyLabel = MenuStyle.Label(page, "", 44f, TextAnchor.MiddleCenter, Color.white, true);
            emptyLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiFactory.Place(emptyLabel.rectTransform, new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Mathf.Min(900f, size.x - 120f), 300f));
            MenuStyle.Tint(emptyLabel, MenuSlot.Ink);
            var make = MenuStyle.Panel(page, "New Level", MenuSlot.Primary2, 1f, null, true);
            UiFactory.Place(make.rectTransform, new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.5f), new Vector2(0f, -260f), new Vector2(680f, 140f));
            var makeButton = make.gameObject.AddComponent<Button>();
            makeButton.transition = Selectable.Transition.None;
            makeButton.onClick.AddListener(OpenEditor);
            MenuStyle.Title(make.transform, "NewEditor", "LEVEL EDITOR", "LEVEL EDITOR", 56f, TextAnchor.MiddleCenter, Color.white, true);
            MenuPress.Add(makeButton);
            emptyEditor = make.gameObject;
        }

        /// <summary>Fills the open list (Official or Custom) with cards, which then come in one after another.</summary>
        void FillLevels()
        {
            FilterEntries();
            for (int i = levelContent.childCount - 1; i >= 0; i--)
            {
                var child = levelContent.GetChild(i).gameObject;
                child.SetActive(false); // out of the list now; Destroy only happens at the end of the frame
                Destroy(child);
            }
            cards.Clear();
            cursor = -1;
            for (int i = levelTitle.childCount - 1; i >= 0; i--) Destroy(levelTitle.GetChild(i).gameObject);
            bool official = listTab == OfficialTab;
            var title = official
                ? MenuStyle.Title(levelTitle, "LevelOfficial", "Level Official", "Official Levels", 76f, TextAnchor.MiddleCenter, Color.white)
                : MenuStyle.Title(levelTitle, "CustomLevel", "Level Custom", "Custom Levels", 76f, TextAnchor.MiddleCenter, Color.white);
            if (title is Text t) MenuStyle.Fit(t, 36);
            editorButton.SetActive(!official);

            for (int i = 0; i < entries.Count; i++) MakeCard(i);
            levelContent.anchoredPosition = Vector2.zero;
            emptyLabel.text = entries.Count > 0 ? ""
                : official ? MenuStyle.T("Belum ada level official.\nIsi Main Menu > Levels (Level1 .. Level5).", "No official levels yet.\nSet Main Menu > Levels (Level1 .. Level5).")
                : MenuStyle.T("Belum ada level buatanmu.\nBuat satu di Level Editor!", "No custom levels yet.\nMake one in the Level Editor!");
            emptyLabel.gameObject.SetActive(entries.Count == 0);
            emptyEditor.SetActive(entries.Count == 0 && !official);
            PlayCardsEnter();

            if (scrollTo != null)
            {
                int index = entries.IndexOf(scrollTo);
                scrollTo = null;
                if (index >= 0)
                {
                    Canvas.ForceUpdateCanvases();
                    float y = 22f + (index / columns) * (cardHeight + CardGap) - 40f;
                    float max = Mathf.Max(0f, levelContent.rect.height - levelScroll.viewport.rect.height);
                    levelContent.anchoredPosition = new Vector2(0f, Mathf.Clamp(y, 0f, max));
                }
            }
        }

        void MakeCard(int index)
        {
            var e = entries[index];
            var best = LevelProgress.Get(e.key);
            var level = e.level;
            var cell = UiFactory.MakePanel(levelContent, $"Level {e.shown}", Color.clear, true);
            var card = new Card { entry = e, root = cell.rectTransform };
            card.group = cell.gameObject.AddComponent<CanvasGroup>();
            var button = cell.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = UiFactory.NoNavigation;
            button.onClick.AddListener(() => OpenDetail(card, true));
            MenuPress.Add(button, 0.97f);

            card.inner = UiFactory.Stretch(UiFactory.MakeRect(cell.transform, "Card"));
            // A glow round the card chosen with the keyboard or a controller.
            var glow = MenuStyle.Panel(card.inner, "Chosen", MenuSlot.Accent, 0.85f, MenuStyle.Glow);
            UiFactory.Stretch(glow.rectTransform, -34f, -34f, -34f, -34f);
            card.chosen = glow.gameObject.AddComponent<CanvasGroup>();
            card.chosen.alpha = 0f;

            card.thumb = UiFactory.MakeRect(card.inner, "Thumb");
            card.thumb.anchorMin = Vector2.zero;
            card.thumb.anchorMax = new Vector2(0.47f, 1f);
            card.thumb.offsetMin = card.thumb.offsetMax = Vector2.zero;
            var picture = MenuStyle.RoundedCover(card.thumb, e.banner != null ? e.banner : BannerLibrary.NoWorld);
            card.thumb.GetComponent<Image>().raycastTarget = false;
            picture.transform.localScale = Vector3.one * 1.08f;
            cell.gameObject.AddComponent<MenuHoverZoom>().picture = picture.transform;
            card.thumbGroup = card.thumb.gameObject.AddComponent<CanvasGroup>();

            var info = UiFactory.MakeRect(card.inner, "Info");
            info.anchorMin = new Vector2(0.47f, 0f);
            info.anchorMax = Vector2.one;
            info.offsetMin = new Vector2(11f, 0f);
            info.offsetMax = Vector2.zero;

            var nameBar = MenuStyle.Panel(info, "Name", MenuSlot.Primary).rectTransform;
            TopBar(nameBar, 0f, 119f);
            var name = MenuStyle.Label(nameBar, level.Name, 65f, TextAnchor.MiddleCenter, Color.white);
            UiFactory.Stretch(name.rectTransform, 20f, 0f, 20f, 0f);
            MenuStyle.Fit(name, 28);

            var body = MenuStyle.Panel(info, "Body", MenuSlot.CardBody).rectTransform;
            UiFactory.Stretch(body, 0f, 0f, 0f, 130f);
            var stack = UiFactory.MakeRect(body, "Stack");
            stack.anchorMin = new Vector2(0f, 0.5f);
            stack.anchorMax = new Vector2(1f, 0.5f);
            stack.offsetMin = new Vector2(26f, -201.5f);
            stack.offsetMax = new Vector2(-26f, 201.5f);

            // Difficulty: five stars on a dark bar (half stars show).
            var starBar = MenuStyle.Panel(stack, "Difficulty", MenuSlot.Primary).rectTransform;
            TopBar(starBar, 0f, 76f);
            var starSprite = UiFactory.Icon("StarFilled") ?? UiFactory.Star;
            float difficulty = LevelData.ClampDifficulty(level.Difficulty);
            for (int i = 0; i < 5; i++)
            {
                var back = UiFactory.MakePanel(starBar, "Star", new Color(1f, 1f, 1f, 0.25f));
                back.sprite = starSprite;
                back.preserveAspect = true;
                UiFactory.Place(back.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f + i * 62f, 0f), new Vector2(54f, 54f));
                var lit = UiFactory.MakePanel(back.transform, "Lit", MenuStyle.StarYellow); // small stars are yellow
                UiFactory.Stretch(lit.rectTransform);
                lit.sprite = starSprite;
                lit.preserveAspect = true;
                lit.type = Image.Type.Filled;
                lit.fillMethod = Image.FillMethod.Horizontal;
                lit.fillAmount = Mathf.Clamp01(difficulty - i);
            }

            var crownRow = UiFactory.MakeRect(stack, "Crowns");
            TopBar(crownRow, 93f, 76f);
            Crowns(crownRow, level.CrownsForHeader, best.crowns, 76f, 15f);

            var gemRow = UiFactory.MakeRect(stack, "Gems");
            TopBar(gemRow, 186f, 60f);
            var gems = IconCount(gemRow, UiFactory.GemIcon, 56f, $"{best.gems}/{level.GemsForHeader}", 54f, Color.white);
            MenuStyle.Tint(gems, MenuSlot.Ink);

            var percent = MenuStyle.Label(stack, $"{best.percent}%", 103f, TextAnchor.MiddleCenter, Color.white, false, true);
            TopBar(percent.rectTransform, 260f, 115f);
            MenuStyle.Tint(percent, MenuSlot.Ink);

            var track = MenuStyle.Panel(stack, "Progress", MenuSlot.Ink, 0.15f, MenuStyle.Pill).rectTransform;
            track.anchorMin = new Vector2(0.07f, 1f);
            track.anchorMax = new Vector2(0.93f, 1f);
            track.pivot = new Vector2(0.5f, 1f);
            track.offsetMin = new Vector2(0f, -390f);
            track.offsetMax = new Vector2(0f, -377f);
            var fill = MenuStyle.Panel(track, "Fill", MenuSlot.Accent, 1f, MenuStyle.Pill).rectTransform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            card.fill = fill;
            card.percent = best.percent / 100f;
            cards.Add(card);
        }

        /// <summary>Puts a rect along the top of its parent: <paramref name="top"/> down from the top, <paramref name="height"/> high.</summary>
        static void TopBar(RectTransform rt, float top, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0f, -top - height);
            rt.offsetMax = new Vector2(0f, -top);
        }

        /// <summary>A centred row of crowns: the ones earned in colour, the others grey.</summary>
        static List<Image> Crowns(RectTransform row, int total, int earned, float size, float gap)
        {
            var list = new List<Image>();
            float width = total * size + Mathf.Max(0, total - 1) * gap;
            for (int i = 0; i < total; i++)
            {
                var crown = UiFactory.MakePanel(row, "Crown", i < earned ? Color.white : new Color(0.45f, 0.45f, 0.45f, 0.55f));
                crown.sprite = UiFactory.CrownIcon;
                crown.preserveAspect = true;
                UiFactory.Place(crown.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(-width / 2f + size / 2f + i * (size + gap), 0f), new Vector2(size, size));
                list.Add(crown);
            }
            return list;
        }

        /// <summary>An icon and a number side by side, centred in <paramref name="row"/> ("◆ 20/20").</summary>
        static Text IconCount(RectTransform row, Sprite icon, float iconSize, string text, float fontSize, Color color)
        {
            var holder = UiFactory.MakeRect(row, "Count");
            UiFactory.Place(holder, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, iconSize));
            var layout = holder.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 12f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var fit = holder.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var image = UiFactory.MakePanel(holder, icon != null ? icon.name : "Icon", UiFactory.IsPicture(icon) ? Color.white : color);
            image.sprite = icon;
            image.preserveAspect = true;
            var element = image.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = element.preferredHeight = iconSize;
            var label = MenuStyle.Label(holder, text, fontSize, TextAnchor.MiddleLeft, color, false, true);
            return label;
        }

        /// <summary>The cards come in one after another (fade, rise, grow), then their progress bars fill.</summary>
        void PlayCardsEnter()
        {
            for (int i = 0; i < cards.Count; i++)
            {
                var c = cards[i];
                float delay = 0.25f + Mathf.Min(i, 8) * 0.11f;
                MenuTween.Fade(c.group, 0f, 1f, 0.75f, MenuEase.OutQuint, delay);
                var inner = c.inner;
                MenuTween.To(inner, "enter", 0.75f, MenuEase.OutQuint, e =>
                {
                    inner.anchoredPosition = new Vector2(0f, -86f * (1f - e));
                    inner.localScale = Vector3.one * Mathf.Lerp(0.97f, 1f, e);
                }, delay);
                var fill = c.fill;
                float to = c.percent;
                fill.gameObject.SetActive(to > 0f);
                MenuTween.To(fill, "fill", 1.3f, MenuEase.OutQuint, e => fill.anchorMax = new Vector2(to * e, 1f), 0.7f + Mathf.Min(i, 8) * 0.11f);
            }
        }

        /// <summary>Keyboard / controller: moves the glow to another card and scrolls it into view.</summary>
        void MoveCursor(int step)
        {
            if (cards.Count == 0) return;
            int next = cursor < 0 ? 0 : Mathf.Clamp(cursor + step, 0, cards.Count - 1);
            for (int i = 0; i < cards.Count; i++)
                MenuTween.Fade(cards[i].chosen, cards[i].chosen.alpha, i == next ? 1f : 0f, 0.25f, MenuEase.OutQuad);
            cursor = next;
            Canvas.ForceUpdateCanvases();
            float top = 22f + (cursor / columns) * (cardHeight + CardGap);
            float view = levelScroll.viewport.rect.height;
            float y = levelContent.anchoredPosition.y;
            if (top < y + 40f) y = top - 40f;
            else if (top + cardHeight > y + view - 300f) y = top + cardHeight - view + 300f; // above the navbar
            float max = Mathf.Max(0f, levelContent.rect.height - view);
            float from = levelContent.anchoredPosition.y, to = Mathf.Clamp(y, 0f, max);
            levelScroll.StopMovement();
            MenuTween.To(levelContent, "scroll", 0.35f, MenuEase.OutQuint, e => levelContent.anchoredPosition = new Vector2(0f, Mathf.Lerp(from, to, e)));
        }

        // ---------------- Detail ----------------

        Entry detailEntry;
        Text detailName, detailPercent, detailGems, detailMeta;
        RectTransform detailCrowns, bannerFrame;
        MenuCover bannerPicture, detailBackground;
        RawImage bannerPreview; // the level's 3D LevelPreview over the banner (PC)
        Vector2 bannerPixels;
        CanvasGroup bannerGroup, bannerGlow;
        GameObject detailEdit;
        readonly List<RectTransform> detailSlides = new List<RectTransform>();
        readonly Dictionary<RectTransform, Vector2> detailHomes = new Dictionary<RectTransform, Vector2>();

        void BuildDetail(RectTransform page)
        {
            bool portrait = builtPortrait;
            detailSlides.Clear();
            detailHomes.Clear();
            var bgFrame = UiFactory.Stretch(UiFactory.MakeRect(page, "Background"));
            var catcher = bgFrame.gameObject.AddComponent<Image>();
            catcher.color = Color.black; // under the picture; also catches taps meant for the list below
            var bg = MenuStyle.Cover(bgFrame, HomeBackground);
            bg.color = MenuStyle.Hex("#D9D9D9");
            detailBackground = bg.GetComponent<MenuCover>(); // the level's theme background (FillDetail)

            var close = SquareButton(page, "Close", "Close", 120f, 97f, MenuSlot.Primary, 0f, CloseDetail, true);
            UiFactory.Place(close, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -32f - topInset), new Vector2(120f, 120f));

            // Where things go: portrait = one column (the design); landscape = banner left, the rest on the right.
            float colX = portrait ? 0.5f : 0.7f;
            float colWidth = portrait ? builtSize.x - 300f : builtSize.x * 0.5f;

            detailName = MenuStyle.Label(page, "", 80f, TextAnchor.MiddleCenter, Color.white);
            Slide(UiFactory.Place(detailName.rectTransform, new Vector2(colX, 1f), new Vector2(0.5f, 1f), new Vector2(0f, (portrait ? -59f : -90f) - topInset), new Vector2(colWidth, 110f)));
            MenuStyle.Fit(detailName, 36);
            var nameShadow = detailName.gameObject.AddComponent<Shadow>();
            nameShadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
            nameShadow.effectDistance = new Vector2(0f, -4f);

            detailCrowns = UiFactory.MakeRect(page, "Crowns");
            UiFactory.Place(detailCrowns, new Vector2(colX, 1f), new Vector2(0.5f, 1f), new Vector2(0f, (portrait ? -173f : -215f) - topInset), new Vector2(600f, 97f));

            // The banner: tap it to play. A blinking Accent glow round it, "tap to play" along its bottom.
            Vector2 bannerSize = portrait ? new Vector2(626f, 979f) : new Vector2(560f, 875f);
            var glowHolder = UiFactory.MakeRect(page, "Banner Glow");
            var glow = MenuStyle.Panel(glowHolder, "Glow", MenuSlot.Accent, 1f, MenuStyle.Glow);
            UiFactory.Stretch(glow.rectTransform, -44f, -44f, -44f, -44f);
            bannerGlow = glowHolder.gameObject.AddComponent<CanvasGroup>();
            bannerGlow.blocksRaycasts = false;
            bannerFrame = UiFactory.MakeRect(page, "Banner");
            if (portrait)
                UiFactory.Place(bannerFrame, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -302f - topInset), bannerSize);
            else
                UiFactory.Place(bannerFrame, new Vector2(0.3f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, bannerSize);
            glowHolder.anchorMin = bannerFrame.anchorMin;
            glowHolder.anchorMax = bannerFrame.anchorMax;
            glowHolder.pivot = bannerFrame.pivot;
            glowHolder.anchoredPosition = bannerFrame.anchoredPosition;
            glowHolder.sizeDelta = bannerFrame.sizeDelta;
            bannerPicture = MenuStyle.RoundedCover(bannerFrame, null).GetComponent<MenuCover>();
            // The 3D LevelPreview goes over the banner picture, under the "tap to play" strip.
            bannerPixels = bannerSize;
            bannerPreview = UiFactory.MakeRect(bannerFrame, "3D LevelPreview").gameObject.AddComponent<RawImage>();
            UiFactory.Stretch(bannerPreview.rectTransform);
            bannerPreview.raycastTarget = false;
            bannerPreview.enabled = false;
            bannerGroup = bannerFrame.gameObject.AddComponent<CanvasGroup>();
            var playButton = bannerFrame.gameObject.AddComponent<Button>();
            playButton.transition = Selectable.Transition.None;
            playButton.navigation = UiFactory.NoNavigation;
            playButton.onClick.AddListener(PlayFromBanner);
            MenuPress.Add(playButton, 0.97f);
            var strip = UiFactory.MakePanel(bannerFrame, "Hint", Color.white);
            strip.sprite = MenuStyle.FadeUp;
            strip.rectTransform.anchorMin = Vector2.zero;
            strip.rectTransform.anchorMax = new Vector2(1f, 0f);
            strip.rectTransform.pivot = new Vector2(0.5f, 0f);
            strip.rectTransform.offsetMin = Vector2.zero;
            strip.rectTransform.offsetMax = new Vector2(0f, 260f);
            var hint = UiFactory.MakeRect(strip.transform, "Tap To Play");
            UiFactory.Place(hint, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(bannerSize.x - 40f, 60f));
            MenuStyle.Title(hint, "Start", "KETUK UNTUK MAIN", "TAP TO PLAY", 35f, TextAnchor.MiddleCenter, Color.white, true);

            detailPercent = MenuStyle.Label(page, "", 140f, TextAnchor.MiddleCenter, Color.white, false, true);
            Slide(UiFactory.Place(detailPercent.rectTransform, new Vector2(colX, 1f), new Vector2(0.5f, 1f), new Vector2(0f, (portrait ? -1318f : -370f) - topInset), new Vector2(700f, 160f)));
            var percentShadow = detailPercent.gameObject.AddComponent<Shadow>();
            percentShadow.effectColor = new Color(0f, 0f, 0f, 0.3f);
            percentShadow.effectDistance = new Vector2(0f, -5f);

            var gemRow = UiFactory.MakeRect(page, "Gems");
            Slide(UiFactory.Place(gemRow, new Vector2(colX, 1f), new Vector2(0.5f, 1f), new Vector2(0f, (portrait ? -1478f : -540f) - topInset), new Vector2(600f, 90f)));
            detailGems = IconCount(gemRow, UiFactory.GemIcon, 76f, "", 76f, MenuStyle.GemGold);

            var gear = SquareButton(page, "Settings", "Settings", 120f, 108f, MenuSlot.Primary, 0f, () => OpenSettings(false), false);
            UiFactory.Place(gear, Vector2.zero, Vector2.zero, portrait ? new Vector2(65f, 76f) : new Vector2(40f, 40f), new Vector2(120f, 120f));
            var edit = SquareButton(page, "Edit", "Brush", 120f, 100f, MenuSlot.Primary, 0f, () => Edit(detailEntry), false);
            UiFactory.Place(edit, Vector2.zero, Vector2.zero, portrait ? new Vector2(65f + 160f, 76f) : new Vector2(200f, 40f), new Vector2(120f, 120f));
            detailEdit = edit.gameObject;

            detailMeta = MenuStyle.Label(page, "", 37f, TextAnchor.LowerRight, MenuStyle.MetaInk);
            detailMeta.horizontalOverflow = HorizontalWrapMode.Wrap;
            Slide(UiFactory.Place(detailMeta.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), portrait ? new Vector2(-48f, 64f) : new Vector2(-48f, 48f), new Vector2(620f, 240f)));
        }

        /// <summary>An element that fades and rises into place when the Detail opens.</summary>
        void Slide(RectTransform rt)
        {
            rt.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            detailSlides.Add(rt);
            detailHomes[rt] = rt.anchoredPosition;
        }

        void FillDetail(Entry e)
        {
            var level = e.level;
            var best = LevelProgress.Get(e.key);
            detailName.text = level.Name;
            for (int i = detailCrowns.childCount - 1; i >= 0; i--) Destroy(detailCrowns.GetChild(i).gameObject);
            Crowns(detailCrowns, level.CrownsForHeader, best.crowns, 97f, 15f);
            bannerPicture.Show(e.banner != null ? e.banner : BannerLibrary.NoWorld);
            // The phone Detail keeps its banner in front (Adrian 2026-10-25); the 3D LevelPreview is the PC screen's,
            // and here only with Main Menu > Preview 3D On Android.
            var stage = preview3DOnAndroid ? ShowPreview(e, bannerPixels) : null;
            bannerPreview.texture = stage != null ? stage.Texture : null;
            bannerPreview.enabled = stage != null && stage.Texture != null;
            detailBackground.Show(DetailBackground(e));
            detailPercent.text = best.percent + "%";
            detailGems.text = $"{best.gems}/{level.GemsForHeader}";
            string music = string.IsNullOrEmpty(level.Music) ? "Music" + e.number : level.Music;
            string theme = string.IsNullOrEmpty(level.Look) ? "-" : level.Look;
            string route = !string.IsNullOrEmpty(level.Author) ? level.Author : !string.IsNullOrEmpty(level.Team) ? level.Team : "-";
            detailMeta.text = $"{MenuStyle.T("Tema", "Theme")}: {theme}\n{MenuStyle.T("Rute", "Route")}: {route}\n" +
                              $"{MenuStyle.T("Musik", "Music")}: {music}\n{LevelData.DifficultyName(LevelData.ClampDifficulty(level.Difficulty))} · {level.RowCount} {MenuStyle.T("baris", "rows")}";
            detailEdit.SetActive(CanEdit(e));
        }

        void PlayDetailEnter()
        {
            var e = detailEntry;
            var best = LevelProgress.Get(e.key);
            for (int i = 0; i < detailCrowns.childCount; i++)
            {
                var crown = detailCrowns.GetChild(i);
                MenuTween.To(crown, "pop", 0.7f, MenuEase.OutBack, x =>
                {
                    crown.localScale = Vector3.one * x;
                    crown.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(-30f, 0f, x));
                }, 0.55f + i * 0.14f);
            }
            var percent = detailPercent;
            int toPercent = best.percent;
            MenuTween.To(percent, "count", 1.4f, MenuEase.OutQuart, x => percent.text = Mathf.RoundToInt(toPercent * x) + "%", 0.55f);
            var gems = detailGems;
            int toGems = best.gems, total = e.level.GemsForHeader;
            MenuTween.To(gems, "count", 1.2f, MenuEase.OutQuart, x => gems.text = $"{Mathf.RoundToInt(toGems * x)}/{total}", 0.55f);
            for (int i = 0; i < detailSlides.Count; i++)
            {
                var rt = detailSlides[i];
                var home = detailHomes[rt];
                var group = rt.GetComponent<CanvasGroup>();
                MenuTween.To(rt, "enter", 0.6f, MenuEase.OutQuint, x =>
                {
                    rt.anchoredPosition = home + new Vector2(0f, -43f * (1f - x));
                    group.alpha = x;
                }, 0.25f + i * 0.09f);
            }
            bannerGlow.alpha = 0f;
            MenuTween.Loop(bannerGlow, "blink", 1.2f, MenuEase.InOutCubic, x => bannerGlow.alpha = 0.85f * x, 1.6f);
        }

        /// <summary>A card was tapped: its picture flies up into the Detail banner while the Detail fades in.</summary>
        void OpenDetail(Card card, bool fly)
        {
            if (busy || settingsOpen || screen != MenuScreen.Levels) return;
            detailEntry = card.entry;
            LevelSelect.Selected = allEntries.IndexOf(card.entry);
            screen = MenuScreen.Detail;
            FillDetail(card.entry);
            detailRt.gameObject.SetActive(true);
            MenuTween.Fade(detailRt.GetComponent<CanvasGroup>(), 0f, 1f, 0.35f, MenuEase.OutQuint);
            HideNavbar(true, false);
            PlayDetailEnter();
            ignoreInputUntil = Time.unscaledTime + 0.3f;
            if (!fly) return;
            Canvas.ForceUpdateCanvases();
            busy = true;
            bannerGroup.alpha = 0f;
            card.thumbGroup.alpha = 0f;
            var tex = card.entry.banner != null ? card.entry.banner : BannerLibrary.NoWorld;
            Fly(card.thumb, bannerFrame, tex, 0.65f, MenuEase.OutQuint, false, () =>
            {
                bannerGroup.alpha = 1f;
                card.thumbGroup.alpha = 1f;
                busy = false;
            });
        }

        void CloseDetail()
        {
            if (busy || screen != MenuScreen.Detail) return;
            screen = MenuScreen.Levels;
            HideNavbar(false, false);
            SetNav(NavFor(screen), false);
            MenuTween.Kill(bannerGlow, "blink");
            MenuTween.Fade(detailRt.GetComponent<CanvasGroup>(), 1f, 0f, 0.4f, MenuEase.OutQuint, 0f, () =>
            {
                if (screen == MenuScreen.Detail) return;
                detailRt.gameObject.SetActive(false);
                bannerPreview.enabled = false;
                HidePreview(); // nothing to draw while the Detail is closed
            });
            ignoreInputUntil = Time.unscaledTime + 0.3f;
            var card = cards.Find(c => c.entry == detailEntry);
            if (card == null || !card.root.gameObject.activeInHierarchy) return;
            // The picture flies back to its card.
            busy = true;
            card.thumbGroup.alpha = 0f;
            bannerGroup.alpha = 0f;
            var tex = card.entry.banner != null ? card.entry.banner : BannerLibrary.NoWorld;
            Fly(bannerFrame, card.thumb, tex, 0.6f, MenuEase.OutQuint, false, () =>
            {
                card.thumbGroup.alpha = 1f;
                bannerGroup.alpha = 1f;
                busy = false;
            });
        }

        /// <summary>The banner was tapped: it zooms to fill the screen, then the level loads.</summary>
        void PlayFromBanner()
        {
            if (busy || settingsOpen || screen != MenuScreen.Detail || detailEntry == null) return;
            busy = true;
            bannerGroup.alpha = 0f;
            MenuTween.Kill(bannerGlow, "blink");
            bannerGlow.alpha = 0f;
            var e = detailEntry;
            // With the 3D LevelPreview: first the ball rolls over it (its tiles and objects play), then the picture zooms.
            if (bannerPreview.enabled && previewStage != null && !previewStage.Playing)
            {
                bannerGroup.alpha = 1f;
                previewStage.PlayIntro(() => ZoomToPlay(e, previewStage.Texture));
                return;
            }
            ZoomToPlay(e, e.banner != null ? e.banner : BannerLibrary.NoWorld);
        }

        /// <summary>The banner (or the 3D LevelPreview) zooms to fill the screen, then the Loading screen takes over.</summary>
        void ZoomToPlay(Entry e, Texture picture)
        {
            bannerGroup.alpha = 0f;
            Fly(bannerFrame, flyLayer, picture, 0.75f, MenuEase.InOutCubic, true, () =>
            {
                Play(e);
                if (leavingMenu) return; // the loading screen takes over
                for (int i = flyLayer.childCount - 1; i >= 0; i--) Destroy(flyLayer.GetChild(i).gameObject);
                bannerGroup.alpha = 1f;
                busy = false;
            });
        }

        /// <summary>
        /// A copy of a picture flies from one rect to another (shared element), cropped to fill and with round corners
        /// all the way. <paramref name="keep"/>: it stays at the end (the level is loading behind it).
        /// </summary>
        void Fly(RectTransform from, RectTransform to, Texture tex, float duration, MenuEase ease, bool keep, Action done)
        {
            var frame = UiFactory.MakeRect(flyLayer, "Flying Picture");
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(0.5f, 0.5f);
            MenuStyle.RoundedCover(frame, tex);
            frame.GetComponent<Image>().raycastTarget = false;
            Rect a = RectIn(from, flyLayer), b = RectIn(to, flyLayer);
            MenuTween.To(frame, "fly", duration, ease, e =>
            {
                frame.anchoredPosition = Vector2.LerpUnclamped(a.center, b.center, e);
                frame.sizeDelta = Vector2.LerpUnclamped(a.size, b.size, e);
            }, 0f, () =>
            {
                if (!keep && frame != null) Destroy(frame.gameObject);
                done?.Invoke();
            });
        }

        static Rect RectIn(RectTransform r, RectTransform space)
        {
            var c = new Vector3[4];
            r.GetWorldCorners(c);
            Vector2 bl = space.InverseTransformPoint(c[0]), tr = space.InverseTransformPoint(c[2]);
            return new Rect(bl, tr - bl);
        }

        // ---------------- Navbar ----------------

        RectTransform navbar, navIndicator;
        CanvasGroup navbarGroup;
        readonly Image[] navIcons = new Image[5];
        readonly RectTransform[] navCells = new RectTransform[5];
        int navIndex = -1;
        float navY;
        const float NavCell = 158f;

        void BuildNavbar(RectTransform layer)
        {
            var bar = MenuStyle.Panel(layer, "Navbar", MenuSlot.Primary, 1f, null, true);
            navbar = bar.rectTransform;
            navY = builtPortrait ? 134f : 30f;
            UiFactory.Place(navbar, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, navY), new Vector2(NavCell * 5f, NavCell));
            if (!builtPortrait) navbar.localScale = Vector3.one * 0.8f;
            navbarGroup = navbar.gameObject.AddComponent<CanvasGroup>();
            var shadow = bar.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
            shadow.effectDistance = new Vector2(0f, -8f);

            navIndicator = UiFactory.MakeRect(navbar, "Indicator");
            UiFactory.Place(navIndicator, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(NavCell / 2f, 0f), new Vector2(NavCell, NavCell));
            var glow = MenuStyle.Panel(navIndicator, "Glow", MenuSlot.Accent, 0.55f, MenuStyle.Glow);
            UiFactory.Stretch(glow.rectTransform, -30f, -30f, -30f, -30f);
            var fill = MenuStyle.Panel(navIndicator, "Fill", MenuSlot.Accent);
            UiFactory.Stretch(fill.rectTransform);

            for (int i = 1; i < 5; i++)
            {
                var line = UiFactory.MakePanel(navbar, "Line", new Color(0f, 0f, 0f, 0.25f));
                UiFactory.Place(line.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(i * NavCell, 0f), new Vector2(3f, NavCell * 0.62f));
            }
            for (int i = 0; i < 5; i++)
            {
                int index = i;
                var cell = UiFactory.MakePanel(navbar, NavIcons[i], Color.clear, true);
                navCells[i] = UiFactory.Place(cell.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(NavCell / 2f + i * NavCell, 0f), new Vector2(NavCell, NavCell));
                var b = cell.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.navigation = UiFactory.NoNavigation;
                b.onClick.AddListener(() => NavPressed(index));
                MenuPress.Add(b);
                navIcons[i] = MenuStyle.Icon(cell.transform, NavIcons[i], 80f, Color.white);
                if (navIcons[i] == null)
                    MenuStyle.Label(cell.transform, NavFallback[i], 60f, TextAnchor.MiddleCenter, Color.white, true);
            }
            navIndex = -1;
        }

        void NavPressed(int i)
        {
            if (busy) return;
            switch (i)
            {
                case NavHome: GoHome(); break;
                case NavCustom: OpenLevels(CustomTab); break;
                case NavOfficial: OpenLevels(OfficialTab); break;
                case NavSettings: OpenSettings(false); break;
                case NavSkin:
                    Pop(navIcons[i] != null ? navIcons[i].transform : navCells[i]);
                    MenuToast.Show(MenuStyle.T("Skin segera hadir!", "Skins are coming soon!"));
                    break;
            }
        }

        /// <summary>RB / LB, Tab: Home → Custom → Official → Home...</summary>
        void StepNavbar(int step)
        {
            int[] order = { NavHome, NavCustom, NavOfficial };
            int at = Mathf.Max(0, Array.IndexOf(order, NavFor(screen)));
            NavPressed(order[(at + step + order.Length) % order.Length]);
        }

        /// <summary>
        /// Lights navbar button <paramref name="index"/>: filled icon in OnAccent with a pop, and the indicator jumps
        /// there like jelly (stretched along the way, squashed, then springs back).
        /// </summary>
        void SetNav(int index, bool animate)
        {
            if (navbar == null) return;
            int old = navIndex;
            navIndex = index;
            for (int i = 0; i < 5; i++)
            {
                var icon = navIcons[i];
                if (icon == null) continue;
                bool on = i == index;
                var sprite = on && NavFilled[i] != null ? UiFactory.Icon(NavFilled[i]) : null;
                icon.sprite = sprite != null ? sprite : UiFactory.Icon(NavIcons[i]);
                if (on) MenuStyle.Tint(icon, MenuSlot.OnAccent);
                else
                {
                    MenuStyle.Untint(icon);
                    icon.color = Color.white;
                }
            }
            if (index == old) return;
            if (navIcons[index] != null && animate) Pop(navIcons[index].transform);
            float x1 = navCells[index].anchoredPosition.x;
            if (!animate || old < 0)
            {
                navIndicator.anchoredPosition = new Vector2(x1, 0f);
                navIndicator.localScale = Vector3.one;
                return;
            }
            float x0 = navIndicator.anchoredPosition.x;
            float stretch = 1f + Mathf.Abs(index - old) * 0.45f;
            MenuTween.To(navIndicator, "pos", 0.56f, MenuEase.OutBack, e => navIndicator.anchoredPosition = new Vector2(Mathf.LerpUnclamped(x0, x1, e), 0f));
            MenuTween.To(navIndicator, "jelly", 0.56f, MenuEase.Linear, e =>
            {
                float s = e * 0.56f;
                Vector2 scale;
                if (s < 0.22f)
                {
                    float q = MenuTween.Ease(MenuEase.OutQuad, s / 0.22f);
                    scale = new Vector2(Mathf.Lerp(1f, stretch, q), Mathf.Lerp(1f, 0.82f, q));
                }
                else
                {
                    float q = MenuTween.Ease(MenuEase.OutBack, (s - 0.22f) / 0.34f);
                    scale = new Vector2(Mathf.LerpUnclamped(stretch, 1f, q), Mathf.LerpUnclamped(0.82f, 1f, q));
                }
                navIndicator.localScale = new Vector3(scale.x, scale.y, 1f);
            });
        }

        static void Pop(Transform t)
        {
            MenuTween.To(t, "pop", 0.5f, MenuEase.OutBack, e =>
            {
                t.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, e);
                t.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(-12f, 0f, e));
            });
        }

        /// <summary>The navbar drops away (432 down, fading) on the Detail screen and comes back after.</summary>
        void HideNavbar(bool hide, bool instant)
        {
            if (navbar == null) return;
            navbarGroup.blocksRaycasts = !hide;
            Vector2 shown = new Vector2(0f, navY), hidden = new Vector2(0f, navY - 432f);
            if (instant)
            {
                navbar.anchoredPosition = hide ? hidden : shown;
                navbarGroup.alpha = hide ? 0f : 1f;
                return;
            }
            MenuTween.Move(navbar, navbar.anchoredPosition, hide ? hidden : shown, 0.6f, MenuEase.OutQuint);
            MenuTween.Fade(navbarGroup, navbarGroup.alpha, hide ? 0f : 1f, 0.6f, MenuEase.OutQuint);
        }

        // ---------------- Building blocks ----------------

        /// <summary>
        /// A square icon button: a <paramref name="slot"/> background at <paramref name="alpha"/> (0 = none) and icon
        /// <paramref name="icon"/> in white. <paramref name="spin"/>: an X that turns when pressed.
        /// </summary>
        static RectTransform SquareButton(Transform parent, string name, string icon, float size, float iconSize, MenuSlot slot, float alpha,
            UnityEngine.Events.UnityAction click, bool spin)
        {
            var back = MenuStyle.Panel(parent, name, slot, alpha, null, true);
            if (alpha <= 0f)
            {
                MenuStyle.Untint(back);
                back.color = Color.clear;
            }
            var b = back.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.navigation = UiFactory.NoNavigation;
            b.onClick.AddListener(click);
            var image = MenuStyle.Icon(back.transform, icon, iconSize, Color.white);
            if (image != null)
            {
                var shadow = image.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.3f);
                shadow.effectDistance = new Vector2(0f, -3f);
            }
            else MenuStyle.Label(back.transform, name, iconSize * 0.35f, TextAnchor.MiddleCenter, Color.white, true);
            MenuPress.Add(b, 0.86f, spin);
            return back.rectTransform;
        }
    }
}
