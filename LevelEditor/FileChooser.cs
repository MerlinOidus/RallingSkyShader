using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace RTXOn
{
    /// <summary>
    /// Picks a file to read and writes exported files for the level editor's windows (Import, Export, textures).
    /// In the Unity Editor it opens the system's file window. A built game has none: Open lists the files in the
    /// Import folder (persistentDataPath/Import) in a small window in front of everything, and Save writes to the
    /// Export folder; on Android both use the phone's own file window instead (<see cref="NativeFiles"/>). <see cref="Browse"/> (Level Settings > Music) can pick from any folder: the system's file window
    /// in the Unity Editor, an in-game folder browser in a built game (PC: drives and your folders; Android: the
    /// phone's storage, Music and Download, after asking for permission to read music).
    /// </summary>
    public class FileChooser : MonoBehaviour
    {
        static readonly Color Dim = new Color(0f, 0f, 0f, 0.6f);
        static Color PanelColor => EditorColors.Window;
        static Color ButtonColor => EditorColors.Button;
        static Color AccentColor => EditorColors.Accent;
        static Color FolderColor => EditorColors.Item;
        static Color PlaceColor => EditorColors.Header;
        static Color FileColor => EditorColors.Go;
        const float Width = 900f, Height = 760f;
        const int MaxItems = 600;
        const string BrowseFolderKey = "RTXOn.BrowseFolder";

        static string lastFolder;
        Text title;
        RectTransform list;
        readonly List<GameObject> items = new List<GameObject>();
        // The folder browser (built game).
        string browseFolder, browseHeading;
        string[] browseExtensions;
        Action<string> browsePicked;
        volatile bool relist; // Android answered the permission question (maybe on its own thread)

        public bool IsOpen => gameObject.activeSelf;

        /// <summary>Shows a message later (the end of an Android Import / Export). The level editor sets it to its status line.</summary>
        public static Action<string> Notify;

        public static string ImportFolder() => Path.Combine(Application.persistentDataPath, "Import");

        public static string ExportFolder() => Path.Combine(Application.persistentDataPath, "Export");

        static string LastFolder => !string.IsNullOrEmpty(lastFolder) ? lastFolder : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        public static FileChooser Build(Transform root)
        {
            var blocker = UiFactory.MakePanel(root, "File Chooser", Dim, true);
            UiFactory.Stretch(blocker.rectTransform);
            var chooser = blocker.gameObject.AddComponent<FileChooser>();
            var window = UiFactory.Round(UiFactory.MakePanel(blocker.transform, "Window", PanelColor, true), 1f).rectTransform;
            UiFactory.Place(window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Width, Height));
            chooser.title = UiFactory.MakeLabel(window, "", 22, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            UiFactory.Place(chooser.title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -8f), new Vector2(Width - 190f, 56f));
            chooser.title.resizeTextForBestFit = true;
            chooser.title.resizeTextMinSize = 12;
            chooser.title.resizeTextMaxSize = 22;
            var cancel = UiFactory.MakeButton(window, "Cancel", ButtonColor, chooser.Close, 22);
            UiFactory.Place((RectTransform)cancel.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -10f), new Vector2(150f, 50f));
            var scroll = UiFactory.MakeGridList(window, new Vector2(Width - 60f, 50f), 6f, 1, AccentColor, out chooser.list);
            UiFactory.Stretch((RectTransform)scroll.transform, 12f, 12f, 12f, 76f);
            blocker.gameObject.SetActive(false);
            return chooser;
        }

        /// <summary>
        /// A file with one of <paramref name="extensions"/> (without dots) for <paramref name="onPath"/>.
        /// <paramref name="onProblem"/> gets a message when there is nothing to pick.
        /// </summary>
        public void Open(string heading, string[] extensions, Action<string> onPath, Action<string> onProblem = null)
        {
#if UNITY_EDITOR
            string path = UnityEditor.EditorUtility.OpenFilePanelWithFilters(heading, LastFolder,
                new[] { string.Join(" / ", extensions).ToUpperInvariant(), string.Join(",", extensions) });
            if (string.IsNullOrEmpty(path)) return;
            lastFolder = Path.GetDirectoryName(path);
            onPath(path);
#else
            if (NativeFiles.Available) // Android: the phone's own file window (the Import folder can't be reached there)
            {
                NativeFiles.Pick(extensions, onPath, onProblem ?? Notify);
                return;
            }
            string folder = ImportFolder();
            var files = new List<string>();
            try
            {
                Directory.CreateDirectory(folder);
                foreach (var f in Directory.GetFiles(folder))
                    foreach (var e in extensions)
                        if (f.EndsWith("." + e, StringComparison.OrdinalIgnoreCase)) { files.Add(f); break; }
            }
            catch (Exception e)
            {
                onProblem?.Invoke("Could not read the Import folder: " + e.Message);
                return;
            }
            if (files.Count == 0)
            {
                onProblem?.Invoke($"No .{string.Join(" / .", extensions)} file in {folder}: copy it there first.");
                return;
            }
            files.Sort(StringComparer.OrdinalIgnoreCase);
            title.text = $"{heading} (from {folder})";
            foreach (var go in items) Destroy(go);
            items.Clear();
            foreach (var f in files)
            {
                string file = f;
                var b = UiFactory.MakeButton(list, Path.GetFileName(f), ButtonColor, () =>
                {
                    Close();
                    onPath(file);
                }, 20);
                items.Add(b.gameObject);
            }
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
#endif
        }

        public void Close() => gameObject.SetActive(false);

        /// <summary>
        /// A file with one of <paramref name="extensions"/> from any folder for <paramref name="onPath"/>: the system's
        /// file window in the Unity Editor, the in-game folder browser in a built game.
        /// </summary>
        public void Browse(string heading, string[] extensions, Action<string> onPath)
        {
#if UNITY_EDITOR
            Open(heading, extensions, onPath);
#else
            if (NativeFiles.Available) // Android: the phone's own file window (no permission needed, any folder or app)
            {
                NativeFiles.Pick(extensions, onPath, Notify);
                return;
            }
            browseHeading = heading;
            browseExtensions = extensions;
            browsePicked = onPath;
#if UNITY_ANDROID
            AskToReadMusic();
#endif
            string start = PlayerPrefs.GetString(BrowseFolderKey, "");
            if (!SafeExists(start)) start = DefaultFolder();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            ShowFolder(start);
#endif
        }

        void Update()
        {
            if (!relist) return;
            relist = false;
            if (browseFolder != null) ShowFolder(browseFolder); // now allowed to read music: list again
        }

        static bool SafeExists(string folder)
        {
            try { return !string.IsNullOrEmpty(folder) && Directory.Exists(folder); }
            catch (Exception) { return false; }
        }

        const string Phone = "/storage/emulated/0";

        static string DefaultFolder()
        {
            if (Application.platform == RuntimePlatform.Android)
                return SafeExists(Phone + "/Music") ? Phone + "/Music" : Phone;
            string music = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
            return SafeExists(music) ? music : LastFolder;
        }

        /// <summary>Places to jump to: the phone's storage, Music, Download and SD cards; or a PC's folders and drives.</summary>
        static List<KeyValuePair<string, string>> Places()
        {
            var places = new List<KeyValuePair<string, string>>();
            void Add(string name, string folder)
            {
                if (SafeExists(folder)) places.Add(new KeyValuePair<string, string>(name, folder));
            }
            if (Application.platform == RuntimePlatform.Android)
            {
                Add("Phone", Phone);
                Add("Music", Phone + "/Music");
                Add("Download", Phone + "/Download");
                try
                {
                    foreach (var d in Directory.GetDirectories("/storage"))
                    {
                        string n = Path.GetFileName(d);
                        if (n != "emulated" && n != "self") Add("SD card " + n, d);
                    }
                }
                catch (Exception) { }
                return places;
            }
            Add("Music", Environment.GetFolderPath(Environment.SpecialFolder.MyMusic));
            Add("Desktop", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
            Add("Documents", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            Add("Downloads", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));
            try
            {
                foreach (var drive in Directory.GetLogicalDrives()) Add("Drive " + drive, drive);
            }
            catch (Exception) { }
            return places;
        }

        void ShowFolder(string folder)
        {
            browseFolder = folder;
            foreach (var go in items)
            {
                go.SetActive(false); // out of the list now; Destroy only happens at the end of the frame
                Destroy(go);
            }
            items.Clear();

            string problem = null;
            var folders = new List<string>();
            var files = new List<string>();
            try
            {
                foreach (var d in Directory.GetDirectories(folder))
                {
                    string n = Path.GetFileName(d);
                    if (!n.StartsWith(".")) folders.Add(d);
                }
                foreach (var f in Directory.GetFiles(folder))
                    foreach (var e in browseExtensions)
                        if (f.EndsWith("." + e, StringComparison.OrdinalIgnoreCase)) { files.Add(f); break; }
            }
            catch (Exception e)
            {
                problem = e.Message;
            }
            folders.Sort(StringComparer.OrdinalIgnoreCase);
            files.Sort(StringComparer.OrdinalIgnoreCase);

            title.text = $"{browseHeading}\n{folder}" + (problem != null ? "  (can't read: " + problem + ")" : "");
            string parent = null;
            try { parent = Path.GetDirectoryName(folder.TrimEnd('/', '\\')); }
            catch (Exception) { }
            if (!string.IsNullOrEmpty(parent) && SafeExists(parent)) Item("[UP]  ..  " + parent, FolderColor, () => ShowFolder(parent));
            foreach (var place in Places())
            {
                string to = place.Value;
                Item("[GO]  " + place.Key, PlaceColor, () => ShowFolder(to));
            }
            foreach (var f in files)
            {
                if (items.Count >= MaxItems) break;
                string file = f;
                Item(Path.GetFileName(f), FileColor, () =>
                {
                    PlayerPrefs.SetString(BrowseFolderKey, browseFolder);
                    PlayerPrefs.Save();
                    Close();
                    browsePicked?.Invoke(file);
                });
            }
            foreach (var d in folders)
            {
                if (items.Count >= MaxItems) break;
                string to = d;
                Item("[DIR]  " + Path.GetFileName(d), FolderColor, () => ShowFolder(to));
            }
            list.anchoredPosition = new Vector2(list.anchoredPosition.x, 0f);
        }

        void Item(string label, Color color, UnityEngine.Events.UnityAction click)
        {
            var b = UiFactory.MakeButton(list, label, color, click, 20);
            var text = b.GetComponentInChildren<Text>();
            text.alignment = TextAnchor.MiddleLeft;
            text.rectTransform.offsetMin = new Vector2(14f, text.rectTransform.offsetMin.y);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 12;
            text.resizeTextMaxSize = 20;
            items.Add(b.gameObject);
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>Android 13+ needs "music and audio" (READ_MEDIA_AUDIO), older phones "storage" to read song files.</summary>
        void AskToReadMusic()
        {
            const string Audio = "android.permission.READ_MEDIA_AUDIO";
            if (Permission.HasUserAuthorizedPermission(Audio) || Permission.HasUserAuthorizedPermission(Permission.ExternalStorageRead)) return;
            var answer = new PermissionCallbacks();
            answer.PermissionGranted += _ => relist = true;
            answer.PermissionDenied += _ => relist = true;
            Permission.RequestUserPermissions(new[] { Audio, Permission.ExternalStorageRead }, answer);
        }
#endif

        /// <summary>Writes <paramref name="text"/> as <paramref name="fileName"/> (the Unity Editor asks where). Returns what happened.</summary>
        public string Save(string heading, string fileName, string text) => Save(heading, fileName, path => File.WriteAllText(path, text));

        /// <summary>Writes <paramref name="bytes"/> (a PNG...) as <paramref name="fileName"/>, like <see cref="Save(string, string, string)"/>.</summary>
        public string SaveBytes(string heading, string fileName, byte[] bytes) => Save(heading, fileName, path => File.WriteAllBytes(path, bytes));

        string Save(string heading, string fileName, Action<string> write)
        {
#if UNITY_EDITOR
            string ext = Path.GetExtension(fileName).TrimStart('.');
            string path = UnityEditor.EditorUtility.SaveFilePanel(heading, LastFolder, fileName, ext);
            if (string.IsNullOrEmpty(path)) return "Export cancelled.";
            lastFolder = Path.GetDirectoryName(path);
#else
            string path = Path.Combine(ExportFolder(), fileName);
#endif
            try
            {
                string folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
                write(path);
#if !UNITY_EDITOR
                if (NativeFiles.Available) // Android: the player chooses where it goes (Download, Drive...)
                {
                    NativeFiles.Save(path, fileName, Notify);
                    return $"Choose where to save {fileName}...";
                }
#endif
                return $"Exported to {path}";
            }
            catch (Exception e)
            {
                return "Export failed: " + e.Message;
            }
        }
    }
}
