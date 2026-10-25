using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace RTXOn
{
    /// <summary>
    /// The level editor's Load window: every level as a card with its banner (Level 1, 2, 3... first, then your own
    /// levels; a built game lists only your own: the official levels 1-5 can't be edited there), and NEW + to start an empty one. Clicking a card opens that level; with unsaved changes the first click
    /// only warns.
    /// </summary>
    public class LevelPickerPanel : MonoBehaviour
    {
        static readonly Color Dim = new Color(0f, 0f, 0f, 0.6f);
        static Color PanelColor => EditorColors.Window;
        static Color HeaderColor => EditorColors.Header;
        static Color ButtonColor => EditorColors.Button;
        static Color ItemColor => EditorColors.Item;
        static Color NewColor => EditorColors.Go;
        static Color CurrentColor => EditorColors.Accent;
        static readonly Color WarnColor = new Color(1f, 0.55f, 0.35f);
        static Color SoftText => EditorColors.Soft;
        static readonly Vector2 CardSize = new Vector2(250f, 350f);
        const float WindowWidth = 1500f, WindowHeight = 880f;

        class Entry
        {
            public TextAsset asset; // null for a level that only exists as a file (the build's save folder)
            public string path;     // the file Save writes for it
            public string key;      // file name without .json
            public LevelData level; // null when it could not be read
            public int number;
        }

        LevelEditor editor;
        RectTransform content;
        ScrollRect list;
        Text status;
        Image levelTab, previewTab;
        readonly List<Entry> entries = new List<Entry>();
        int pending = int.MinValue; // the card clicked once with unsaved changes (-1 = NEW +, PreviewOffset + i = a LevelPreview)
        bool previews;              // the Load LevelPreview tab: each level's 10-row LevelPreview instead of the level
        const int PreviewOffset = 100000;

        public bool IsOpen => gameObject.activeSelf;

        public static LevelPickerPanel Build(LevelEditor owner, Transform root)
        {
            // A dark layer over the whole editor: clicks outside the window do nothing while it is open.
            var blocker = UiFactory.MakePanel(root, "Level Picker", Dim, true);
            UiFactory.Stretch(blocker.rectTransform);
            var panel = blocker.gameObject.AddComponent<LevelPickerPanel>();
            panel.editor = owner;
            panel.BuildUi(blocker.transform);
            blocker.gameObject.SetActive(false);
            return panel;
        }

        void BuildUi(Transform blocker)
        {
            var window = UiFactory.Round(UiFactory.MakePanel(blocker, "Window", PanelColor, true), 1f).rectTransform;
            UiFactory.Place(window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(WindowWidth, WindowHeight));

            var header = UiFactory.Round(UiFactory.MakePanel(window, "Header", HeaderColor), 1f).rectTransform;
            UiFactory.Place(header, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(WindowWidth, 86f));
            var title = UiFactory.MakeLabel(header, "LOAD LEVEL", 46, TextAnchor.MiddleLeft, new Color(0.55f, 0.85f, 1f), FontStyle.Bold);
            UiFactory.Stretch(title.rectTransform, 30f, 0f, 120f, 0f);
            // Two tabs (Adrian 2026-10-25): the levels, or each level's LevelPreview (what the Main Menu shows in 3D).
            var levelsButton = UiFactory.MakeButton(header, "Load Level", ButtonColor, () => ShowTab(false), 30);
            UiFactory.Place((RectTransform)levelsButton.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-120f, 0f), new Vector2(330f, 60f));
            levelTab = levelsButton.GetComponent<Image>();
            var previewsButton = UiFactory.MakeButton(header, "Load LevelPreview", ButtonColor, () => ShowTab(true), 30);
            UiFactory.Place((RectTransform)previewsButton.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(240f, 0f), new Vector2(330f, 60f));
            previewTab = previewsButton.GetComponent<Image>();
            var close = UiFactory.MakeButton(header, "X", ButtonColor, Close, 34);
            UiFactory.IconOnly(close, "Close", 34f);
            UiFactory.Place((RectTransform)close.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-18f, 0f), new Vector2(64f, 60f));

            list = UiFactory.MakeGridList(window, CardSize, 14f, 5, CurrentColor, out content);
            UiFactory.Stretch((RectTransform)list.transform, 24f, 70f, 24f, 100f);

            status = UiFactory.MakeLabel(window, "", 22, TextAnchor.MiddleLeft, SoftText);
            UiFactory.Place(status.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(30f, 12f), new Vector2(WindowWidth - 60f, 50f));
        }

        // ---------------- Open / close ----------------

        public void Open()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Collect();
            ShowTab(editor.EditingPreview); // a LevelPreview open now: its tab
        }

        /// <summary>Load Level (false) or Load LevelPreview (true).</summary>
        void ShowTab(bool showPreviews)
        {
            previews = showPreviews;
            pending = int.MinValue;
            levelTab.color = previews ? ButtonColor : NewColor;
            previewTab.color = previews ? NewColor : ButtonColor;
            Fill();
            list.verticalNormalizedPosition = 1f;
            status.color = SoftText;
            if (editor.Dirty)
                status.text = "You have unsaved changes: Save first, or they are lost when another level opens.";
            else if (previews)
                status.text = $"LevelPreview: the {LevelPreviews.Rows} rows the Main Menu shows in 3D (default camera, Day). " +
                              "A level without one starts from its own first rows; Save creates it in " + LevelPreviews.Folder + ".";
            else
                status.text = "Pick a level to edit. NEW + starts an empty level (its LevelPreview starts as LevelPreview1); Save creates its file in " + LevelFiles.LevelsFolder() + ".";
        }

        public void Close()
        {
            if (!IsOpen) return;
            gameObject.SetActive(false);
        }

        // ---------------- Levels ----------------

        /// <summary>The level files in list order: the editor's Level Files (Level1, Level2...), then levels saved only as files.</summary>
        void Collect()
        {
            entries.Clear();
            var assets = new List<TextAsset>();
#if UNITY_EDITOR
            // Every level.json next to Level1.json, so levels made with NEW + show up at once.
            var project = LevelFiles.FindProjectLevels();
            if (project != null) assets.AddRange(project);
#endif
            if (editor.levelFiles != null)
                foreach (var a in editor.levelFiles)
                    if (a != null && !assets.Contains(a)) assets.Add(a);

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var asset in assets)
            {
                if (!LevelFiles.IsJson(asset) || !names.Add(asset.name)) continue;
                if (!Application.isEditor && LevelFiles.IsOfficial(asset.name)) continue; // a built game can't edit levels 1-5
                entries.Add(new Entry
                {
                    asset = asset,
                    path = LevelFiles.SavePath(asset),
                    key = asset.name,
                    level = LevelFiles.Peek(asset),
                });
            }
            foreach (var path in LevelFiles.SavedOnlyFiles(names))
            {
                if (LevelFiles.IsOfficial(Path.GetFileNameWithoutExtension(path))) continue;
                var level = LevelFiles.OpenPath(path, out _, out _);
                entries.Add(new Entry { path = path, key = Path.GetFileNameWithoutExtension(path), level = level });
            }
            for (int i = 0; i < entries.Count; i++) entries[i].number = i + 1;
        }

        void Fill()
        {
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                var child = content.GetChild(i).gameObject;
                child.SetActive(false); // out of the grid now; Destroy only happens at the end of the frame
                Destroy(child);
            }
            if (!previews) MakeNewCard();
            for (int i = 0; i < entries.Count; i++) MakeCard(i);
        }

        void MakeNewCard()
        {
            var item = Card("New Level", NewColor, false, () => Clicked(-1));
            var plus = UiFactory.MakeLabel(item.transform, "+", 150, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            UiFactory.Place(plus.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(220f, 220f));
            plus.raycastTarget = false;
            var add = UiFactory.Icon("Add"); // Adrian's + picture instead of the text
            if (add != null)
            {
                plus.enabled = false;
                var picture = UiFactory.MakePanel(item.transform, "Add", Color.white);
                picture.sprite = add;
                picture.preserveAspect = true;
                picture.raycastTarget = false;
                UiFactory.Place(picture.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(140f, 140f));
            }
            var label = UiFactory.MakeLabel(item.transform, "NEW +", 34, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            UiFactory.Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(220f, 50f));
            label.raycastTarget = false;
            var hint = UiFactory.MakeLabel(item.transform, "Create a new level", 18, TextAnchor.MiddleCenter, SoftText);
            UiFactory.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(220f, 30f));
            hint.raycastTarget = false;
        }

        void MakeCard(int index)
        {
            var e = entries[index];
            string previewPath = previews ? LevelPreviews.PathFor(e.path) : null;
            bool current = SamePath(previews ? previewPath : e.path, editor.CurrentPath);
            var item = Card($"Level {e.number}", ItemColor, current, () => Clicked(previews ? PreviewOffset + index : index));

            var thumb = UiFactory.MakeRect(item.transform, "Banner").gameObject.AddComponent<RawImage>();
            thumb.raycastTarget = false;
            UiFactory.Place(thumb.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(CardSize.x - 28f, 240f));
            var banner = BannerLibrary.Get(e.level?.Banner, e.number, editor.banners);
            if (banner != null) BannerLibrary.Cover(thumb, banner);
            else
            {
                thumb.color = new Color(0.08f, 0.08f, 0.14f);
                var q = UiFactory.MakeLabel(thumb.transform, "?", 90, TextAnchor.MiddleCenter, SoftText, FontStyle.Bold);
                q.raycastTarget = false;
            }

            string name = e.level != null ? e.level.Name : "(can't be read)";
            var label = UiFactory.MakeLabel(item.transform, $"{e.number}. {name}", 26, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            UiFactory.Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(CardSize.x - 20f, 44f));
            label.raycastTarget = false;
            label.resizeTextForBestFit = true; // long names get smaller
            label.resizeTextMinSize = 14;
            label.resizeTextMaxSize = 26;
            string fileName = e.key;
            if (previews)
            {
                fileName = LevelPreviews.KeyFor(e.key);
                if (previewPath == null || !File.Exists(previewPath)) fileName += "  (new)";
            }
            var file = UiFactory.MakeLabel(item.transform, current ? fileName + "  (open now)" : fileName, 17, TextAnchor.MiddleCenter, current ? CurrentColor : SoftText);
            UiFactory.Place(file.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(CardSize.x - 20f, 28f));
            file.raycastTarget = false;
        }

        /// <summary>A card in the grid: a frame (blue for the level open now) around a clickable panel.</summary>
        Image Card(string name, Color color, bool current, UnityEngine.Events.UnityAction onClick)
        {
            var frame = UiFactory.MakePanel(content, name, current ? CurrentColor : Color.clear, true);
            var item = UiFactory.MakePanel(frame.transform, "Item", color, true);
            UiFactory.Stretch(item.rectTransform, 4f, 4f, 4f, 4f);
            var button = item.gameObject.AddComponent<Button>();
            button.targetGraphic = item;
            button.navigation = UiFactory.NoNavigation;
            button.onClick.AddListener(onClick);
            return item;
        }

        /// <summary>A card was clicked (-1 = NEW +, PreviewOffset + i = that level's LevelPreview). With unsaved changes the first click only warns.</summary>
        void Clicked(int index)
        {
            bool preview = index >= PreviewOffset;
            int at = preview ? index - PreviewOffset : index;
            if (editor.Dirty && pending != index)
            {
                pending = index;
                string what = at < 0 ? "start a new level" : preview ? $"open {LevelPreviews.KeyFor(entries[at].key)}" : $"open {entries[at].key}";
                status.color = WarnColor;
                status.text = $"You have unsaved changes. Click again to {what} without saving (or close this and Save first).";
                return;
            }
            pending = int.MinValue;
            Close();
            if (preview)
            {
                var e = entries[at];
                editor.OpenPreview(e.key, e.path, LevelText(e), e.number);
            }
            else if (index < 0)
            {
                var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var e in entries) taken.Add(e.key);
                editor.NewLevel(taken, entries.Count + 1); // it goes at the end of the list
            }
            else
            {
                var e = entries[index];
                editor.OpenLevel(e.asset, e.path, e.number);
            }
        }

        /// <summary>The level's level.json text as it is saved now (its file, else the asset). Null when there is none.</summary>
        static string LevelText(Entry e)
        {
            try
            {
                if (!string.IsNullOrEmpty(e.path) && File.Exists(e.path)) return File.ReadAllText(e.path);
            }
            catch (Exception) { }
            return e.asset != null ? e.asset.text : null;
        }

        static bool SamePath(string a, string b) =>
            !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b) &&
            string.Equals(a.Replace('\\', '/'), b.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);
    }
}
