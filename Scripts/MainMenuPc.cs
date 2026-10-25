using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RTXOn
{
    // The PC level screen (Adrian's NewUiForPc dan Xbox.pdf page 4, 2026-10-25): the level list on the left, the chosen
    // level's 3D LevelPreview on the right, a row of buttons (Layer = other list, Brush = Level Editor, Star = play,
    // Settings, Close = Home) over the preview with Theme / Route / Song above it, and the hint bar along the bottom.
    // Up / Down choose the level, Left / Right the button, Select presses it. Phones (and a PC window held upright) keep
    // the phone list and Detail.
    public partial class MainMenu
    {
        const float PcBar = PcHintBar.Height;  // the title bar on top is as high as the hint bar
        const float PcListRight = 620f;        // the list panel: 20 .. 620 from the left
        const float PcPreviewLeft = 640f;      // the 3D preview: from here to the right edge
        static readonly Vector2 PcCardSize = new Vector2(560f, 330f);
        const float PcCardGap = 20f;
        const int PcLayer = 0, PcBrush = 1, PcStar = 2, PcSettingsButton = 3, PcClose = 4;

        class PcCard
        {
            public Entry entry;
            public CanvasGroup group, frame;
        }

        /// <summary>The PC screens are used: a computer held sideways (a PC window held upright gets the phone screens).</summary>
        bool PcLayout => PcMenu.On && !builtPortrait;

        RectTransform pcRt, pcContent, pcTitle, pcPreviewFrame;
        ScrollRect pcScroll;
        RawImage pcPreview;
        MenuCover pcBackdrop;
        Text pcInfo, pcEmpty;
        PcButtonRow pcRow;
        PcHintBar pcHint;
        readonly List<PcCard> pcCards = new List<PcCard>();
        int pcIndex = -1;
        /// <summary>The level chosen on the PC screen (kept when the menu is built again).</summary>
        Entry pcEntry;

        void BuildPcLevels(RectTransform page)
        {
            pcCards.Clear();
            pcIndex = -1;
            var bg = UiFactory.MakePanel(page, "Page", MenuStyle.Hex("#1B1F1B"), true); // catches clicks meant for Home
            UiFactory.Stretch(bg.rectTransform);

            // Right: the chosen level in 3D (its banner or theme picture while there is no 3D preview). Click = play.
            pcPreviewFrame = UiFactory.Stretch(UiFactory.MakeRect(page, "Preview"), PcPreviewLeft, PcBar, 0f, PcBar);
            var backdrop = MenuStyle.Cover(pcPreviewFrame, HomeBackground);
            backdrop.raycastTarget = false;
            pcBackdrop = backdrop.GetComponent<MenuCover>();
            pcPreview = UiFactory.MakeRect(pcPreviewFrame, "3D LevelPreview").gameObject.AddComponent<RawImage>();
            UiFactory.Stretch(pcPreview.rectTransform);
            pcPreview.enabled = false;
            var playCatcher = UiFactory.MakePanel(pcPreviewFrame, "Click To Play", Color.clear, true);
            UiFactory.Stretch(playCatcher.rectTransform);
            var play = playCatcher.gameObject.AddComponent<Button>();
            play.transition = Selectable.Transition.None;
            play.navigation = UiFactory.NoNavigation;
            play.onClick.AddListener(() =>
            {
                Deselect();
                PcPlay();
            });

            // Left: the list of levels.
            pcScroll = UiFactory.MakeGridList(page, PcCardSize, PcCardGap, 1, PcMenu.ButtonColor, out pcContent);
            pcScroll.GetComponent<Image>().color = MenuStyle.Hex("#1F3322");
            var listRt = (RectTransform)pcScroll.transform;
            listRt.anchorMin = Vector2.zero;
            listRt.anchorMax = new Vector2(0f, 1f);
            listRt.pivot = new Vector2(0f, 0.5f);
            listRt.offsetMin = new Vector2(20f, PcBar + 20f);
            listRt.offsetMax = new Vector2(PcListRight, -PcBar - 20f);
            pcContent.GetComponent<GridLayoutGroup>().padding = new RectOffset(20, 20, 20, 20);
            pcEmpty = MenuStyle.Label(listRt, "", 38f, TextAnchor.MiddleCenter, Color.white);
            pcEmpty.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiFactory.Stretch(pcEmpty.rectTransform, 30f, 30f, 30f, 30f);

            // Top: the list's name, and an arrow to the other list.
            var bar = UiFactory.MakePanel(page, "Title Bar", MenuStyle.Hex("#C8C8C6"), true).rectTransform;
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = Vector2.one;
            bar.pivot = new Vector2(0.5f, 1f);
            bar.offsetMin = new Vector2(0f, -PcBar);
            bar.offsetMax = Vector2.zero;
            pcTitle = UiFactory.MakeRect(bar, "Title");
            UiFactory.Place(pcTitle, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(PcListRight - 140f, PcBar));
            var arrow = UiFactory.MakePanel(bar, "Other List", Color.clear, true);
            UiFactory.Place(arrow.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(PcListRight - 50f, 0f), new Vector2(PcBar, PcBar));
            var arrowButton = arrow.gameObject.AddComponent<Button>();
            arrowButton.transition = Selectable.Transition.None;
            arrowButton.navigation = UiFactory.NoNavigation;
            arrowButton.onClick.AddListener(() =>
            {
                Deselect();
                PcSwitchList();
            });
            if (MenuStyle.Icon(arrow.transform, "ArrowRight", 60f, PcMenu.DarkInk) == null)
                MenuStyle.Label(arrow.transform, ">", 60f, TextAnchor.MiddleCenter, PcMenu.DarkInk, true);
            MenuPress.Add(arrowButton, 0.86f);

            // Over the preview, top left (Adrian 2026-10-25): Theme / Route / Song of the chosen level on 20% black.
            var info = UiFactory.MakePanel(page, "Info", new Color(0f, 0f, 0f, 0.2f));
            info.sprite = MenuStyle.Rounded;
            info.type = Image.Type.Sliced;
            UiFactory.Place(info.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(PcPreviewLeft + 24f, -PcBar - 24f), new Vector2(520f, 160f));
            pcInfo = MenuStyle.Label(info.transform, "", 34f, TextAnchor.MiddleLeft, PcMenu.IconColor);
            pcInfo.horizontalOverflow = HorizontalWrapMode.Wrap;
            var infoShadow = pcInfo.gameObject.AddComponent<Shadow>(); // readable on a bright sky
            infoShadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            infoShadow.effectDistance = new Vector2(2f, -2f);
            UiFactory.Stretch(pcInfo.rectTransform, 26f, 10f, 20f, 10f);

            // Bottom: the hint bar, and the buttons over the middle of the preview.
            pcHint = PcHintBar.Build(page, true);
            var items = new List<PcButtonRow.Item>
            {
                new PcButtonRow.Item("Levels", "Layer", "Lihat Level Custom", "Show Custom Levels", PcSwitchList),
                new PcButtonRow.Item("Edit", "Brush", "Buka Level Editor", "Open the Level Editor", PcEdit),
                new PcButtonRow.Item("Play", "Star", "Mulai main", "Start to Play", PcPlay),
                new PcButtonRow.Item("Settings", "Settings", "Pengaturan", "Settings", () => OpenSettings(false)),
                new PcButtonRow.Item("Home", "Close", "Kembali ke Home", "Back to Home", GoHome),
            };
            pcRow = PcButtonRow.Build(page, new Vector2(0.5f, 0f), new Vector2(PcPreviewLeft / 2f, PcBar + 110f), items, PcStar, pcHint);
            pcRow.ownInput = false; // Update (PcUpdate) passes the keys on, so they wait while Settings is open
        }

        /// <summary>Home → the PC level screen: Home slides away while the screen comes in from the right.</summary>
        void OpenPcLevels(int tab)
        {
            bool fromHome = screen == MenuScreen.Home;
            if (!fromHome && screen == MenuScreen.PcLevels && listTab == tab) return;
            listTab = tab;
            screen = MenuScreen.PcLevels;
            SetNav(NavFor(screen), false);
            HideNavbar(true, false);
            pcRt.gameObject.SetActive(true);
            FillPcLevels();
            ignoreInputUntil = Time.unscaledTime + 0.2f;
            if (!fromHome) return;
            float w = root.rect.width;
            MenuTween.Move(pcRt, new Vector2(w, 0f), Vector2.zero, 0.7f, MenuEase.OutQuint);
            MenuTween.To(homeRt, "leave", 0.7f, MenuEase.OutQuint, e => HomeAway(e, w), 0f, () => homeRt.gameObject.SetActive(false));
        }

        /// <summary>Official ↔ Custom (Layer, the title bar's arrow, Tab / LB / RB).</summary>
        void PcSwitchList()
        {
            if (busy || settingsOpen || screen != MenuScreen.PcLevels) return;
            listTab = listTab == OfficialTab ? CustomTab : OfficialTab;
            FillPcLevels();
        }

        /// <summary>Fills the list with the open tab's levels and chooses one (the one played last, else the first).</summary>
        void FillPcLevels()
        {
            FilterEntries();
            for (int i = pcContent.childCount - 1; i >= 0; i--)
            {
                var child = pcContent.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
            pcCards.Clear();
            pcIndex = -1;
            for (int i = pcTitle.childCount - 1; i >= 0; i--) Destroy(pcTitle.GetChild(i).gameObject);
            bool official = listTab == OfficialTab;
            var title = official
                ? MenuStyle.Title(pcTitle, "LevelOfficial", "Level Official", "Official Levels", 54f, TextAnchor.MiddleLeft, PcMenu.DarkInk)
                : MenuStyle.Title(pcTitle, "CustomLevel", "Level Custom", "Custom Levels", 54f, TextAnchor.MiddleLeft, PcMenu.DarkInk);
            if (title is Text t) MenuStyle.Fit(t, 30);
            pcRow.SetHint(PcLayer, official ? "Lihat Level Custom" : "Lihat Level Official", official ? "Show Custom Levels" : "Show Official Levels");

            for (int i = 0; i < entries.Count; i++) MakePcCard(i);
            pcContent.anchoredPosition = Vector2.zero;
            pcEmpty.text = entries.Count > 0 ? ""
                : official ? MenuStyle.T("Belum ada level official.", "No official levels yet.")
                : MenuStyle.T("Belum ada level buatanmu.\nTekan Brush untuk membuat satu di Level Editor!", "No custom levels yet.\nPress Brush to make one in the Level Editor!");
            pcEmpty.gameObject.SetActive(entries.Count == 0);
            for (int i = 0; i < pcCards.Count; i++)
                MenuTween.Fade(pcCards[i].group, 0f, 1f, 0.5f, MenuEase.OutQuint, 0.1f + Mathf.Min(i, 6) * 0.07f);

            int index = -1;
            var remembered = LevelSelect.Selected >= 0 && LevelSelect.Selected < allEntries.Count ? allEntries[LevelSelect.Selected] : null;
            foreach (var wanted in new[] { scrollTo, pcEntry, remembered })
                if (index < 0 && wanted != null) index = entries.IndexOf(wanted);
            scrollTo = null;
            if (entries.Count == 0) ShowPcLevel(null);
            else SelectPc(Mathf.Max(0, index), false);
        }

        void MakePcCard(int index)
        {
            var e = entries[index];
            var best = LevelProgress.Get(e.key);
            var level = e.level;
            var cell = UiFactory.MakePanel(pcContent, $"Level {e.shown}", Color.clear, true);
            var card = new PcCard { entry = e, group = cell.gameObject.AddComponent<CanvasGroup>() };
            var button = cell.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = UiFactory.NoNavigation;
            button.onClick.AddListener(() =>
            {
                Deselect();
                if (busy || settingsOpen) return;
                if (pcIndex == index) PcPlay(); // a second click on the chosen level plays it
                else SelectPc(index, true);
            });
            MenuPress.Add(button, 0.97f);

            // The white frame round the chosen level.
            var frame = UiFactory.MakePanel(cell.transform, "Chosen", Color.white);
            frame.sprite = MenuStyle.Rounded;
            frame.type = Image.Type.Sliced;
            UiFactory.Stretch(frame.rectTransform, -6f, -6f, -6f, -6f);
            card.frame = frame.gameObject.AddComponent<CanvasGroup>();
            card.frame.alpha = 0f;
            var back = UiFactory.MakePanel(cell.transform, "Card", MenuStyle.Hex("#2A3D2C"));
            back.sprite = MenuStyle.Rounded;
            back.type = Image.Type.Sliced;
            UiFactory.Stretch(back.rectTransform);

            var thumb = UiFactory.MakeRect(cell.transform, "Thumb");
            thumb.anchorMin = Vector2.zero;
            thumb.anchorMax = new Vector2(0.45f, 1f);
            thumb.offsetMin = new Vector2(10f, 10f);
            thumb.offsetMax = new Vector2(0f, -10f);
            MenuStyle.RoundedCover(thumb, e.banner != null ? e.banner : BannerLibrary.NoWorld);
            thumb.GetComponent<Image>().raycastTarget = false;

            var info = UiFactory.MakeRect(cell.transform, "Info");
            info.anchorMin = new Vector2(0.45f, 0f);
            info.anchorMax = Vector2.one;
            info.offsetMin = new Vector2(10f, 10f);
            info.offsetMax = new Vector2(-10f, -10f);

            var nameBar = UiFactory.MakePanel(info, "Name", MenuStyle.Hex("#2E6B35"));
            nameBar.sprite = MenuStyle.Rounded;
            nameBar.type = Image.Type.Sliced;
            TopBar(nameBar.rectTransform, 0f, 64f);
            var name = MenuStyle.Label(nameBar.transform, level.Name, 40f, TextAnchor.MiddleCenter, Color.white);
            UiFactory.Stretch(name.rectTransform, 12f, 0f, 12f, 0f);
            MenuStyle.Fit(name, 20);

            // Difficulty: five yellow stars on a dark pill (half stars show).
            var pill = UiFactory.MakePanel(info, "Difficulty", MenuStyle.Hex("#141A15"));
            pill.sprite = MenuStyle.Pill;
            pill.type = Image.Type.Sliced;
            TopBar(pill.rectTransform, 76f, 50f);
            var starSprite = UiFactory.Icon("StarFilled") ?? UiFactory.Star;
            float difficulty = LevelData.ClampDifficulty(level.Difficulty);
            for (int i = 0; i < 5; i++)
            {
                var star = UiFactory.MakePanel(pill.transform, "Star", new Color(1f, 1f, 1f, 0.25f));
                star.sprite = starSprite;
                star.preserveAspect = true;
                UiFactory.Place(star.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - 2) * 48f, 0f), new Vector2(40f, 40f));
                var lit = UiFactory.MakePanel(star.transform, "Lit", MenuStyle.StarYellow);
                UiFactory.Stretch(lit.rectTransform);
                lit.sprite = starSprite;
                lit.preserveAspect = true;
                lit.type = Image.Type.Filled;
                lit.fillMethod = Image.FillMethod.Horizontal;
                lit.fillAmount = Mathf.Clamp01(difficulty - i);
            }

            var crownRow = UiFactory.MakeRect(info, "Crowns");
            TopBar(crownRow, 136f, 56f);
            Crowns(crownRow, level.CrownsForHeader, best.crowns, 52f, 10f);

            var percent = MenuStyle.Label(info, $"{best.percent}%", 62f, TextAnchor.MiddleCenter, Color.white, false, true);
            TopBar(percent.rectTransform, 196f, 66f);

            var gemRow = UiFactory.MakeRect(info, "Gems");
            TopBar(gemRow, 266f, 44f);
            IconCount(gemRow, UiFactory.GemIcon, 40f, $"{best.gems}/{level.GemsForHeader}", 38f, MenuStyle.GemGold);
            pcCards.Add(card);
        }

        /// <summary>Chooses level <paramref name="index"/> of the list: its frame, the list scrolls to it, the 3D preview and the info follow.</summary>
        void SelectPc(int index, bool animate)
        {
            if (pcCards.Count == 0) return;
            index = Mathf.Clamp(index, 0, pcCards.Count - 1);
            if (index == pcIndex) return;
            pcIndex = index;
            for (int i = 0; i < pcCards.Count; i++)
            {
                float to = i == index ? 1f : 0f;
                if (animate) MenuTween.Fade(pcCards[i].frame, pcCards[i].frame.alpha, to, 0.2f, MenuEase.OutQuad);
                else pcCards[i].frame.alpha = to;
            }
            ShowPcLevel(pcCards[index].entry);

            // Scroll the chosen card into view.
            Canvas.ForceUpdateCanvases();
            float top = 20f + index * (PcCardSize.y + PcCardGap);
            float view = pcScroll.viewport.rect.height;
            float y = pcContent.anchoredPosition.y;
            if (top < y + 20f) y = top - 20f;
            else if (top + PcCardSize.y > y + view - 20f) y = top + PcCardSize.y - view + 20f;
            float max = Mathf.Max(0f, pcContent.rect.height - view);
            float from = pcContent.anchoredPosition.y, target = Mathf.Clamp(y, 0f, max);
            pcScroll.StopMovement();
            if (!animate) pcContent.anchoredPosition = new Vector2(0f, target);
            else MenuTween.To(pcContent, "scroll", 0.3f, MenuEase.OutQuint, e => pcContent.anchoredPosition = new Vector2(0f, Mathf.Lerp(from, target, e)));
        }

        /// <summary>The right side shows <paramref name="e"/> (null = nothing chosen: the list is empty).</summary>
        void ShowPcLevel(Entry e)
        {
            pcEntry = e ?? pcEntry;
            if (e != null) LevelSelect.Selected = allEntries.IndexOf(e);
            pcRow.SetEnabled(PcStar, e != null);
            bool canEdit = CanEdit(e);
            pcRow.SetHint(PcBrush, canEdit ? "Edit level ini" : "Buka Level Editor", canEdit ? "Edit this level" : "Open the Level Editor");
            if (e == null)
            {
                HidePreview();
                pcPreview.enabled = false;
                pcBackdrop.Show(HomeBackground);
                pcInfo.text = "";
                pcInfo.transform.parent.gameObject.SetActive(false);
                pcRow.Choose(listTab == CustomTab ? PcBrush : PcLayer, false);
                return;
            }
            pcInfo.transform.parent.gameObject.SetActive(true);
            var stage = ShowPreview(e, pcPreviewFrame.rect.size.x > 1f ? pcPreviewFrame.rect.size : new Vector2(builtSize.x - PcPreviewLeft, builtSize.y - 2f * PcBar));
            pcPreview.texture = stage != null ? stage.Texture : null;
            pcPreview.enabled = pcPreview.texture != null;
            if (pcPreview.enabled) CoverCrop(pcPreview, pcPreviewFrame.rect.size);
            pcBackdrop.Show(pcPreview.enabled ? DetailBackground(e) : e.banner != null ? e.banner : BannerLibrary.NoWorld);

            var level = e.level;
            string music = string.IsNullOrEmpty(level.Music) ? "Music" + e.number : level.Music;
            string theme = string.IsNullOrEmpty(level.Look) ? "-" : level.Look;
            int bar = theme.IndexOf('|');
            if (bar > 0) theme = theme.Substring(0, bar);
            string route = !string.IsNullOrEmpty(level.Author) ? level.Author : !string.IsNullOrEmpty(level.Team) ? level.Team : "-";
            pcInfo.text = $"{MenuStyle.T("Tema", "Theme")}: {theme}\n{MenuStyle.T("Rute", "Route")}: {route}\n{MenuStyle.T("Lagu", "Song")}: {music}";
        }

        /// <summary>The picture fills <paramref name="size"/> without stretching (cut at the sides or top and bottom).</summary>
        static void CoverCrop(RawImage image, Vector2 size)
        {
            var tex = image.texture;
            if (tex == null || size.x < 1f || size.y < 1f)
            {
                image.uvRect = new Rect(0f, 0f, 1f, 1f);
                return;
            }
            float texAspect = tex.width / (float)Mathf.Max(1, tex.height), aspect = size.x / size.y;
            if (texAspect > aspect)
            {
                float w = aspect / texAspect;
                image.uvRect = new Rect((1f - w) / 2f, 0f, w, 1f);
            }
            else
            {
                float h = texAspect / aspect;
                image.uvRect = new Rect(0f, (1f - h) / 2f, 1f, h);
            }
        }

        /// <summary>Star / Select / a click on the preview: the ball rolls over the preview, then it zooms and the level loads.</summary>
        void PcPlay()
        {
            if (busy || settingsOpen || screen != MenuScreen.PcLevels || pcIndex < 0 || pcIndex >= pcCards.Count) return;
            var e = pcCards[pcIndex].entry;
            busy = true;
            if (pcPreview.enabled && previewStage != null && !previewStage.Playing)
            {
                previewStage.PlayIntro(() => PcZoom(e, previewStage.Texture));
                return;
            }
            PcZoom(e, e.banner != null ? e.banner : BannerLibrary.NoWorld);
        }

        void PcZoom(Entry e, Texture picture)
        {
            Fly(pcPreviewFrame, flyLayer, picture, 0.6f, MenuEase.InOutCubic, true, () =>
            {
                Play(e);
                if (leavingMenu) return; // the loading screen takes over
                for (int i = flyLayer.childCount - 1; i >= 0; i--) Destroy(flyLayer.GetChild(i).gameObject);
                busy = false;
            });
        }

        /// <summary>Brush: edits the chosen custom level, else opens the Level Editor on a new level.</summary>
        void PcEdit()
        {
            if (busy || settingsOpen) return;
            if (CanEdit(pcEntry) && listTab == CustomTab) Edit(pcEntry);
            else OpenEditor();
        }

        /// <summary>Keys and controller on the PC level screen (Update calls it).</summary>
        void PcUpdate()
        {
            if (GameInput.BackPressed(true)) GoHome();
            else if (GameInput.MenuDown()) SelectPc(pcIndex + 1, true);
            else if (GameInput.MenuUp()) SelectPc(pcIndex - 1, true);
            else if (GameInput.MenuLeft()) pcRow.Step(-1);
            else if (GameInput.MenuRight()) pcRow.Step(1);
            else if (PcMenu.SelectPressed() || GameInput.ConfirmPressed()) pcRow.Press();
        }

        /// <summary>A click leaves nothing selected, so Enter / Space don't press the clicked button again.</summary>
        static void Deselect()
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }
    }
}
