using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RTXOn
{
    /// <summary>
    /// The level editor's Level Settings window (side panel > LEVEL SETTINGS): what the Main Menu's level list shows
    /// (banner, name, team, author, difficulty stars, gem and crown numbers) and a few level options (End Speed,
    /// Speed Max Point, Invincible, Start Portal, Music: the song list or Choose File... for a song from any folder on
    /// PC or phone). Editor-Preview is a placeholder (Coming Soon).
    /// Every change goes into the level at once and is saved with Save.
    /// </summary>
    public class LevelSettingsPanel : MonoBehaviour
    {
        static readonly Color Dim = new Color(0f, 0f, 0f, 0.6f);
        static Color PanelColor => EditorColors.Window;
        static Color HeaderColor => EditorColors.Header;
        static Color ButtonColor => EditorColors.Button;
        static Color FieldColor => EditorColors.Field;
        static Color SoftText => EditorColors.Soft;
        static readonly Color StarOn = new Color(1f, 0.82f, 0.25f);
        static readonly Color StarOff = new Color(1f, 1f, 1f, 0.15f);
        static Color ToggleOn => EditorColors.Accent;

        LevelEditor editor;
        RawImage banner;
        Text bannerMissing, bannerName, difficultyName, invincibleMark, portalMark, fileStatus, musicName;
        InputField levelName, team, author, startSpeed, endSpeed, maxPoint, gems, crowns;
        Text gemsPlaceholder, crownsPlaceholder;
        Image[] stars;
        bool showing;

        public bool IsOpen => gameObject.activeSelf;

        public static LevelSettingsPanel Build(LevelEditor owner, Transform root)
        {
            // A dark layer over the whole editor: clicks outside the window do nothing while it is open.
            var blocker = UiFactory.MakePanel(root, "Level Settings", Dim, true);
            UiFactory.Stretch(blocker.rectTransform);
            var panel = blocker.gameObject.AddComponent<LevelSettingsPanel>();
            panel.editor = owner;
            panel.BuildUi(blocker.transform);
            blocker.gameObject.SetActive(false);
            return panel;
        }

        void BuildUi(Transform blocker)
        {
            var window = UiFactory.Round(UiFactory.MakePanel(blocker, "Window", PanelColor, true), 1f).rectTransform;
            UiFactory.Place(window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1260f, 900f));

            var header = UiFactory.Round(UiFactory.MakePanel(window, "Header", HeaderColor), 1f).rectTransform;
            UiFactory.Place(header, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1260f, 86f));
            var title = UiFactory.MakeLabel(header, "LEVEL SETTINGS", 46, TextAnchor.MiddleLeft, new Color(0.55f, 0.85f, 1f), FontStyle.Bold);
            UiFactory.Stretch(title.rectTransform, 30f, 0f, 120f, 0f);
            var close = UiFactory.MakeButton(header, "X", ButtonColor, Close, 34);
            UiFactory.IconOnly(close, "Close", 34f);
            UiFactory.Place((RectTransform)close.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-18f, 0f), new Vector2(64f, 60f));

            // Left: the banner and its chooser.
            var frame = UiFactory.MakePanel(window, "Banner Frame", new Color(0.3f, 0.26f, 0.34f));
            UiFactory.Place(frame.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -110f), new Vector2(316f, 500f));
            banner = UiFactory.MakeRect(frame.transform, "Banner").gameObject.AddComponent<RawImage>();
            banner.raycastTarget = false;
            UiFactory.Stretch(banner.rectTransform, 5f, 5f, 5f, 5f);
            bannerMissing = UiFactory.MakeLabel(frame.transform, "No banner", 30, TextAnchor.MiddleCenter, SoftText, FontStyle.Bold);
            var prev = UiFactory.MakeButton(window, "<", ButtonColor, () => CycleBanner(-1), 30);
            UiFactory.Place((RectTransform)prev.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -625f), new Vector2(56f, 56f));
            var next = UiFactory.MakeButton(window, ">", ButtonColor, () => CycleBanner(1), 30);
            UiFactory.Place((RectTransform)next.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(290f, -625f), new Vector2(56f, 56f));
            bannerName = UiFactory.MakeLabel(window, "", 22, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            UiFactory.Place(bannerName.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(90f, -625f), new Vector2(196f, 56f));
            bannerName.resizeTextForBestFit = true;
            bannerName.resizeTextMinSize = 12;
            bannerName.resizeTextMaxSize = 22;
            var bannerHint = UiFactory.MakeLabel(window, "Banner. Auto = \"World N\" picture for level N.\nOwn pictures: a Banners folder (.png .jpg).", 16, TextAnchor.UpperLeft, SoftText);
            UiFactory.Place(bannerHint.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -690f), new Vector2(330f, 70f));

            // Right: the texts and numbers.
            const float LabelX = 390f, FieldX = 640f;
            levelName = TextRow(window, -110f, "Level Name", LabelX, FieldX, 580f, v => { editor.Level.Name = string.IsNullOrWhiteSpace(v) ? "Level" : v.Trim(); });
            team = TextRow(window, -180f, "Team", LabelX, FieldX, 580f, v => editor.Level.Team = v.Trim());
            UiFactory.SetPlaceholder(team, "Input here");
            author = TextRow(window, -250f, "Level Author", LabelX, FieldX, 580f, v => editor.Level.Author = v.Trim());
            UiFactory.SetPlaceholder(author, "Your name");

            RowLabel(window, -320f, "Difficulty", LabelX);
            var starRow = UiFactory.MakeRect(window, "Stars");
            UiFactory.Place(starRow, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(FieldX, -320f), new Vector2(300f, 56f));
            stars = UiFactory.MakeStars(starRow, Vector2.zero, 52f, 6f, StarOn, StarOff);
            for (int i = 0; i < stars.Length; i++)
            {
                int index = i;
                var back = stars[i].transform.parent.GetComponent<Image>();
                back.raycastTarget = true;
                var b = back.gameObject.AddComponent<Button>();
                b.targetGraphic = back;
                b.navigation = UiFactory.NoNavigation;
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => ClickStar(index));
            }
            difficultyName = UiFactory.MakeLabel(window, "", 26, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            UiFactory.Place(difficultyName.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(FieldX + 300f, -320f), new Vector2(280f, 56f));

            // Speed: Start Speed (the same as Speed in the side panel), ramping to End Speed at Speed Max Point.
            startSpeed = NumberRow(window, -390f, "Start Speed", LabelX, FieldX, v => { if (v > 0f) editor.SetStartSpeed(v); });
            endSpeed = NumberRow(window, -390f, "End Speed", FieldX + 190f, FieldX + 440f, v => editor.Level.SpeedEnd = Mathf.Clamp(v, 0f, LevelData.MaxSpeed));
            UiFactory.SetPlaceholder(endSpeed, "Off");
            maxPoint = NumberRow(window, -460f, "Speed Max Point", LabelX, FieldX, v => editor.Level.SpeedRampRows = Mathf.Max(0, Mathf.RoundToInt(v)), true);
            UiFactory.SetPlaceholder(maxPoint, "Level end");
            var speedHint = UiFactory.MakeLabel(window, "Row where the speed reaches End Speed.\nEmpty = at the end of the level.", 16, TextAnchor.MiddleLeft, SoftText);
            UiFactory.Place(speedHint.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(FieldX + 190f, -460f), new Vector2(400f, 56f));
            gems = NumberRow(window, -530f, "Gem Number", LabelX, FieldX, v => editor.Level.HeaderGems = Mathf.Max(0, Mathf.RoundToInt(v)), true);
            gemsPlaceholder = Placeholder(gems);
            crowns = NumberRow(window, -530f, "Crown Number", FieldX + 190f, FieldX + 440f, v => editor.Level.HeaderCrowns = Mathf.Max(0, Mathf.RoundToInt(v)), true);
            crownsPlaceholder = Placeholder(crowns);
            var numbersHint = UiFactory.MakeLabel(window,
                "Difficulty, Gem and Crown Number are only shown in the level list. Empty number = counted from the level.",
                16, TextAnchor.UpperLeft, SoftText);
            UiFactory.Place(numbersHint.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(LabelX, -592f), new Vector2(830f, 28f));

            invincibleMark = CheckBox(window, new Vector2(LabelX, -636f), "Invincible", "hazards don't kill", ToggleInvincible, true);
            portalMark = CheckBox(window, new Vector2(LabelX + 290f, -636f), "Start Portal", "ball jumps out at start", ToggleStartPortal, true);
            CheckBox(window, new Vector2(LabelX + 560f, -636f), "Editor-Preview", "Coming Soon", null, false);

            // Music: the songs in the Music folders (< >, like the side panel), or any song file (Choose File...).
            RowLabel(window, -712f, "Music", LabelX);
            var musicPrev = UiFactory.MakeButton(window, "<", ButtonColor, () => CycleMusic(-1), 30);
            UiFactory.Place((RectTransform)musicPrev.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(FieldX, -712f), new Vector2(56f, 56f));
            var musicBack = UiFactory.Round(UiFactory.MakePanel(window, "Music Name", FieldColor), 2f);
            UiFactory.Place(musicBack.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(FieldX + 62f, -712f), new Vector2(300f, 56f));
            musicName = UiFactory.MakeLabel(musicBack.transform, "", 24, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            UiFactory.Stretch(musicName.rectTransform, 8f, 0f, 8f, 0f);
            musicName.resizeTextForBestFit = true;
            musicName.resizeTextMinSize = 12;
            musicName.resizeTextMaxSize = 24;
            var musicNext = UiFactory.MakeButton(window, ">", ButtonColor, () => CycleMusic(1), 30);
            UiFactory.Place((RectTransform)musicNext.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(FieldX + 368f, -712f), new Vector2(56f, 56f));
            var chooseMusic = UiFactory.MakeButton(window, "Choose File...", new Color(0.1f, 0.45f, 0.6f), ChooseMusic, 20);
            UiFactory.Place((RectTransform)chooseMusic.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(FieldX + 432f, -712f), new Vector2(160f, 56f));

            // Share files: Import reads any of them; Export Level = a Full Level (.levelrsrt: level + its objects + its
            // custom themes); Export Objects = the level's custom obstacles as one Object Pack (.rsrtp).
            string[] fileNames = { "Import...", "Export Level", "Export Objects" };
            UnityAction[] fileActions =
            {
                () => editor.ImportAny(m => { if (IsOpen) { fileStatus.text = m; Show(); } }),
                () => fileStatus.text = editor.ExportLevelPackage(),
                () => fileStatus.text = editor.ExportObjectPack(),
            };
            for (int i = 0; i < fileNames.Length; i++)
            {
                var b = UiFactory.MakeButton(window, fileNames[i], ButtonColor, fileActions[i], 22);
                UiFactory.Place((RectTransform)b.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(30f + i * 210f, 26f), new Vector2(200f, 64f));
            }
            fileStatus = UiFactory.MakeLabel(window, ".levelrsrt = full level (objects + themes inside), .rsrtp = object pack, .themert = theme, .rsrt = one object.",
                16, TextAnchor.MiddleLeft, SoftText);
            UiFactory.Place(fileStatus.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(30f, 96f), new Vector2(1000f, 30f));
            fileStatus.resizeTextForBestFit = true;
            fileStatus.resizeTextMinSize = 11;
            fileStatus.resizeTextMaxSize = 16;

            var done = UiFactory.MakeButton(window, "Done", new Color(0.12f, 0.5f, 0.2f), Close, 30);
            UiFactory.Place((RectTransform)done.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 26f), new Vector2(200f, 64f));
        }

        // ---------------- Open / close ----------------

        public void Open()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Show();
        }

        public void Close()
        {
            if (!IsOpen) return;
            gameObject.SetActive(false);
            editor.SettingsChanged();
        }

        /// <summary>Puts the level's values in the window.</summary>
        void Show()
        {
            var level = editor.Level;
            if (level == null) return;
            showing = true;
            SetText(levelName, level.Name);
            SetText(team, level.Team);
            SetText(author, level.Author);
            float d = LevelData.ClampDifficulty(level.Difficulty);
            UiFactory.SetStars(stars, d);
            difficultyName.text = $"{d.ToString("0.#", CultureInfo.InvariantCulture)}  {LevelData.DifficultyName(d)}";
            SetText(startSpeed, editor.StartSpeed.ToString("0.##", CultureInfo.InvariantCulture));
            SetText(endSpeed, level.SpeedEnd > 0f ? level.SpeedEnd.ToString("0.##", CultureInfo.InvariantCulture) : "");
            SetText(maxPoint, level.SpeedRampRows > 0 ? level.SpeedRampRows.ToString() : "");
            SetText(gems, level.HeaderGems > 0 ? level.HeaderGems.ToString() : "");
            SetText(crowns, level.HeaderCrowns > 0 ? level.HeaderCrowns.ToString() : "");
            gemsPlaceholder.text = $"Auto ({level.CountGems()})";
            crownsPlaceholder.text = $"Auto ({level.CountCrowns()})";
            invincibleMark.text = level.Invincible ? "ON" : "";
            invincibleMark.transform.parent.GetComponent<Image>().color = level.Invincible ? ToggleOn : FieldColor;
            portalMark.text = level.StartPortal ? "ON" : "";
            portalMark.transform.parent.GetComponent<Image>().color = level.StartPortal ? ToggleOn : FieldColor;
            musicName.text = editor.MusicLabel;

            var tex = BannerLibrary.Get(level.Banner, editor.LevelNumber, editor.banners);
            Canvas.ForceUpdateCanvases();
            BannerLibrary.Cover(banner, tex);
            banner.color = tex != null ? Color.white : FieldColor;
            bannerMissing.enabled = tex == null;
            bannerName.text = string.IsNullOrEmpty(level.Banner) ? $"Auto (World {editor.LevelNumber})" : level.Banner;
            showing = false;
        }

        static void SetText(InputField field, string text)
        {
            if (!field.isFocused) field.text = text ?? "";
        }

        void Changed()
        {
            if (showing) return;
            editor.SettingsChanged();
            Show();
        }

        // ---------------- Values ----------------

        void CycleBanner(int step)
        {
            var names = new List<string> { "" };
            names.AddRange(BannerLibrary.Names(editor.banners));
            int index = names.FindIndex(n => string.Equals(n, editor.Level.Banner ?? "", StringComparison.OrdinalIgnoreCase));
            if (index < 0) index = 0;
            editor.Level.Banner = names[(index + step + names.Count) % names.Count];
            Changed();
        }

        /// <summary>Clicking star N gives N stars; clicking it again gives N - 0.5 (a half star).</summary>
        void ClickStar(int index)
        {
            float full = index + 1f;
            float now = LevelData.ClampDifficulty(editor.Level.Difficulty);
            editor.Level.Difficulty = LevelData.ClampDifficulty(Mathf.Approximately(now, full) ? full - 0.5f : full);
            Changed();
        }

        void CycleMusic(int step)
        {
            editor.CycleMusic(step);
            Show();
        }

        /// <summary>A song file from any folder (system file window in the Unity Editor, folder browser in a built game).</summary>
        void ChooseMusic()
        {
            editor.ChooseMusicFile(m =>
            {
                if (!IsOpen) return;
                fileStatus.text = m;
                Show();
            });
        }

        void ToggleInvincible()
        {
            editor.Level.Invincible = !editor.Level.Invincible;
            Changed();
        }

        void ToggleStartPortal()
        {
            editor.Level.StartPortal = !editor.Level.StartPortal;
            Changed();
        }

        // ---------------- Building blocks ----------------

        static void RowLabel(RectTransform parent, float y, string name, float x)
        {
            var label = UiFactory.MakeLabel(parent, name, 26, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            UiFactory.Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y), new Vector2(240f, 56f));
        }

        InputField TextRow(RectTransform parent, float y, string name, float labelX, float fieldX, float width, Action<string> onValue)
        {
            RowLabel(parent, y, name, labelX);
            var f = UiFactory.MakeInputField(parent, 26, FieldColor, InputField.ContentType.Standard);
            UiFactory.Place((RectTransform)f.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(fieldX, y), new Vector2(width, 56f));
            f.characterLimit = 60;
            f.onEndEdit.AddListener(text =>
            {
                if (showing || editor.Level == null) return;
                onValue(text ?? "");
                Changed();
            });
            return f;
        }

        InputField NumberRow(RectTransform parent, float y, string name, float labelX, float fieldX, Action<float> onValue, bool whole = false)
        {
            RowLabel(parent, y, name, labelX);
            var f = UiFactory.MakeInputField(parent, 26, FieldColor, whole ? InputField.ContentType.IntegerNumber : InputField.ContentType.DecimalNumber);
            UiFactory.Place((RectTransform)f.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(fieldX, y), new Vector2(150f, 56f));
            f.onEndEdit.AddListener(text =>
            {
                if (showing || editor.Level == null) return;
                if (string.IsNullOrWhiteSpace(text)) onValue(0f); // empty = off / automatic
                else if (float.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float v)) onValue(v);
                Changed();
            });
            return f;
        }

        static Text Placeholder(InputField field)
        {
            UiFactory.SetPlaceholder(field, "Auto");
            return (Text)field.placeholder;
        }

        /// <summary>A check box with a name and a small note; <paramref name="enabled"/> false = greyed out (Coming Soon).</summary>
        static Text CheckBox(RectTransform parent, Vector2 at, string name, string note, UnityAction onClick, bool enabled)
        {
            var box = UiFactory.Round(UiFactory.MakePanel(parent, name, FieldColor, true), 2f);
            UiFactory.Place(box.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), at, new Vector2(56f, 56f));
            var mark = UiFactory.MakeLabel(box.transform, "", 18, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            if (enabled)
            {
                var b = box.gameObject.AddComponent<Button>();
                b.targetGraphic = box;
                b.navigation = UiFactory.NoNavigation;
                b.onClick.AddListener(onClick);
            }
            else box.color = new Color(0.2f, 0.2f, 0.22f);

            var color = enabled ? Color.white : new Color(1f, 1f, 1f, 0.4f);
            var label = UiFactory.MakeLabel(parent, name, 28, TextAnchor.MiddleLeft, color, FontStyle.Bold);
            UiFactory.Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), at + new Vector2(68f, 8f), new Vector2(220f, 40f));
            var small = UiFactory.MakeLabel(parent, note, 17, TextAnchor.MiddleLeft, enabled ? SoftText : new Color(1f, 0.75f, 0.3f, 0.8f), FontStyle.Italic);
            UiFactory.Place(small.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), at + new Vector2(68f, -32f), new Vector2(220f, 26f));
            return mark;
        }
    }
}
