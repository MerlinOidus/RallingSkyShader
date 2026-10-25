using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// LevelPreview (Adrian 2026-10-25): every level can have a small level of 10 rows that the Main Menu shows in 3D
    /// (default CameraRig, Day environment, only to look at). Level1.json has Previews/LevelPreview1.json next to it,
    /// Custom2.json has Previews/CustomPreview2.json. A level without one shows its own first 10 rows. A new level
    /// starts with a copy of LevelPreview1. The Level Editor edits them with Load > Load LevelPreview, and a Full
    /// Level (.levelrsrt) carries the level and its preview together.
    /// The SpriteMaker's own small level (old name LevelPreview.json) is now LevelTexturePreview.json.
    /// </summary>
    public static class LevelPreviews
    {
        /// <summary>A LevelPreview is at most this many rows.</summary>
        public const int Rows = 10;
        /// <summary>The sub folder of the levels folder the previews are kept in.</summary>
        public const string Folder = "Previews";
        /// <summary>The preview every new level starts from.</summary>
        public const string DefaultKey = "LevelPreview1";

        static readonly Regex Numbered = new Regex(@"^(.*?)(\d+)$");

        /// <summary>The preview's file name (no .json) of a level: Level1 → LevelPreview1, Custom2 → CustomPreview2, Sky → SkyPreview.</summary>
        public static string KeyFor(string levelKey)
        {
            if (string.IsNullOrEmpty(levelKey)) return DefaultKey;
            var m = Numbered.Match(levelKey);
            return m.Success && m.Groups[1].Value.Length > 0 ? m.Groups[1].Value + "Preview" + m.Groups[2].Value : levelKey + "Preview";
        }

        /// <summary>The preview file of the level saved at <paramref name="levelPath"/>: its folder / Previews / LevelPreviewN.json.</summary>
        public static string PathFor(string levelPath)
        {
            if (string.IsNullOrEmpty(levelPath)) return null;
            string folder = Path.GetDirectoryName(levelPath) ?? "";
            return Path.Combine(folder, Folder, KeyFor(Path.GetFileNameWithoutExtension(levelPath)) + ".json").Replace('\\', '/');
        }

        /// <summary>LevelPreview1.json in the levels folder (next to Level1.json in the Unity Editor, the save folder in a build).</summary>
        public static string DefaultPath(string levelsFolder) =>
            string.IsNullOrEmpty(levelsFolder) ? null : Path.Combine(levelsFolder, Folder, DefaultKey + ".json").Replace('\\', '/');

        /// <summary>
        /// The preview text of a level, or null when it has none: its file next to the level (<paramref name="levelPath"/>,
        /// the Level Editor saves there), then the previews shipped with the game (<paramref name="shipped"/>, filled by
        /// Build Main Menu Scene / Build Level Editor Scene).
        /// </summary>
        public static string Find(string levelKey, string levelPath, IList<TextAsset> shipped)
        {
            string path = PathFor(levelPath);
            try
            {
                if (path != null && File.Exists(path)) return File.ReadAllText(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"RTXOn: {path} could not be read: {e.Message}");
            }
            string key = KeyFor(levelKey);
            if (shipped != null)
                foreach (var a in shipped)
                    if (a != null && string.Equals(a.name, key, StringComparison.OrdinalIgnoreCase)) return a.text;
            return null;
        }

        /// <summary>The preview of a level ready to show: its own preview, else its first 10 rows. Null when neither can be read.</summary>
        public static LevelData Load(string levelKey, string levelPath, string levelJson, IList<TextAsset> shipped)
        {
            string text = Find(levelKey, levelPath, shipped);
            if (text != null)
            {
                var own = FromLevel(text, true);
                if (own != null) return own;
                Debug.LogWarning($"RTXOn: the LevelPreview of {levelKey} could not be read: its first rows are shown.");
            }
            return FromLevel(levelJson);
        }

        /// <summary>
        /// The first 10 rows of a level as a preview (no checkpoints, camera, screen or environment triggers). Null when
        /// unreadable. <paramref name="keepStart"/>: the text is a LevelPreview's own, so its Start Pos stays (a level's
        /// Start Pos is only for the editor's Play).
        /// </summary>
        public static LevelData FromLevel(string levelJson, bool keepStart = false)
        {
            if (string.IsNullOrEmpty(levelJson)) return null;
            try
            {
                var level = Trim(LevelData.ParseJson(levelJson));
                if (!keepStart) level.TestStartRow = level.TestStartLane = -1;
                return level;
            }
            catch (Exception) { return null; }
        }

        /// <summary>
        /// Makes <paramref name="level"/> a preview: 10 rows, the default camera (no camera triggers), the Day environment
        /// (no theme triggers), no screen filters, checkpoints or Start Portal. Its Start Pos (any cell of the 10 rows) is
        /// where the menu's ball starts (Adrian 2026-10-25).
        /// </summary>
        public static LevelData Trim(LevelData level)
        {
            if (level == null) return null;
            level.KeepRows(Rows);
            // A look (and objects) the level starts in with a theme trigger on row 0 stays: it becomes the preview's look.
            foreach (var c in level.LookChanges())
                if (c != null && c.row <= 0 && !string.IsNullOrEmpty(c.look) && c.look != "|") level.Look = c.look;
            level.ThemeChanges.Clear();
            level.CameraChanges.Clear();
            level.FilterChanges.Clear();
            level.Checkpoints.Clear();
            level.StartPortal = false;
            return level;
        }

        /// <summary>level.json text of a preview (with its Start Pos).</summary>
        public static string ToJson(LevelData preview) =>
            preview.ToJson(preview.Name, preview.Music, preview.SpeedStart, preview.SpeedEnd, preview.SpeedRampRows, preview.ThemeChanges, preview.Props, true);

        /// <summary>
        /// A new level was saved for the first time (<paramref name="levelPath"/>): it gets its own preview, a copy of
        /// LevelPreview1 (<paramref name="defaultJson"/>), when it has none yet. Returns the file written, or null.
        /// </summary>
        public static string CreateFor(string levelPath, string defaultJson)
        {
            string path = PathFor(levelPath);
            if (path == null || File.Exists(path) || string.IsNullOrEmpty(defaultJson)) return null;
            var preview = FromLevel(defaultJson, true); // LevelPreview1's Start Pos comes along
            if (preview == null) return null;
            LevelFiles.Write(path, ToJson(preview));
            return path;
        }
    }
}
