using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// Where the level editor reads and writes its level.json.
    /// In the Unity Editor: the Level File asset itself (for example Levels/Level1.json), so the Gameplay scene
    /// plays the change straight away. In a built game (PC, Android): Application.persistentDataPath/Levels,
    /// because the game's own files are read-only there.
    /// </summary>
    public static class LevelFiles
    {
        const string DefaultFolder = "Assets/RTXOn/Levels";
        const string NewLevelName = "NewLevel";
        const string CustomLevelName = "Custom";

        /// <summary>True for a level.json asset (an old Tiled Level.txt is not one; the editor only writes JSON).</summary>
        public static bool IsJson(TextAsset file) => file != null && file.text.TrimStart().StartsWith("{");

        /// <summary>The file Save writes.</summary>
        public static string SavePath(TextAsset levelFile)
        {
#if UNITY_EDITOR
            string assetPath = levelFile != null ? UnityEditor.AssetDatabase.GetAssetPath(levelFile) : "";
            if (IsJson(levelFile) && !string.IsNullOrEmpty(assetPath)) return assetPath;
            // No Level File, or a Level.txt: a new level.json next to it.
            string folder = string.IsNullOrEmpty(assetPath) ? DefaultFolder : Path.GetDirectoryName(assetPath);
            return (folder + "/" + NewLevelName + ".json").Replace('\\', '/');
#else
            string name = IsJson(levelFile) ? levelFile.name : NewLevelName;
            return Path.Combine(Application.persistentDataPath, "Levels", name + ".json");
#endif
        }

        /// <summary>
        /// Opens the level: the saved file first, then the Level File asset.
        /// Returns null when there is nothing to open or nothing could be read; <paramref name="problem"/> then says why
        /// (it stays null when there was simply no file yet). <paramref name="brokenPath"/> is set when the saved file
        /// exists but could not be read, so Save can ask before writing over it.
        /// </summary>
        public static LevelData Open(TextAsset levelFile, out string source, out string problem, out string brokenPath)
        {
            source = problem = brokenPath = null;
            string path = SavePath(levelFile);
            try
            {
                if (File.Exists(path))
                {
                    var level = LevelData.ParseJson(File.ReadAllText(path));
                    source = path;
                    return level;
                }
            }
            catch (Exception e)
            {
                problem = $"Could not read {path}: {e.Message}";
                brokenPath = path;
            }

            if (!IsJson(levelFile)) return null;
            try
            {
                // In the Unity Editor this is the same file; in a build it is the level that ships with the game.
                var level = LevelData.ParseJson(levelFile.text);
                source = levelFile.name;
                return level;
            }
            catch (Exception e)
            {
                problem = (problem != null ? problem + " " : "") + $"Could not read {levelFile.name}: {e.Message}";
                return null;
            }
        }

        /// <summary>
        /// Opens a level.json by its path (a level made with New+, or one in the build's save folder that ships with
        /// no asset). Null with no <paramref name="problem"/> when the file does not exist yet.
        /// </summary>
        public static LevelData OpenPath(string path, out string problem, out string brokenPath)
        {
            problem = brokenPath = null;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            try
            {
                return LevelData.ParseJson(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                problem = $"Could not read {path}: {e.Message}";
                brokenPath = path;
                return null;
            }
        }

        /// <summary>The level as the level list shows it: the saved copy when there is one, else the asset. Null when unreadable.</summary>
        public static LevelData Peek(TextAsset levelFile)
        {
            if (!IsJson(levelFile)) return null;
            try
            {
                string path = SavePath(levelFile);
                return LevelData.ParseJson(File.Exists(path) ? File.ReadAllText(path) : levelFile.text);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The folder New+ levels go in: next to Level1.json in the Unity Editor, the save folder in a build.</summary>
        public static string LevelsFolder()
        {
#if UNITY_EDITOR
            string folder = ProjectLevelsFolder();
            return folder ?? DefaultFolder;
#else
            return Path.Combine(Application.persistentDataPath, "Levels");
#endif
        }

        /// <summary>A free "CustomN.json" path in <see cref="LevelsFolder"/> (N = 1, 2, 3...) that no listed level uses.</summary>
        public static string NewLevelPath(ICollection<string> takenNames, out int number)
        {
            string folder = LevelsFolder();
            for (number = 1; ; number++)
            {
                string name = CustomLevelName + number;
                if (takenNames != null && takenNames.Contains(name)) continue;
                string path = (folder + "/" + name + ".json").Replace('\\', '/');
                if (!File.Exists(path)) return path;
            }
        }

        /// <summary>
        /// Level files in the build's save folder that are not one of <paramref name="assetNames"/> (levels made in the
        /// built game's editor). Empty in the Unity Editor, where they are assets.
        /// </summary>
        public static List<string> SavedOnlyFiles(ICollection<string> assetNames)
        {
            var found = new List<string>();
#if !UNITY_EDITOR
            string folder = Path.Combine(Application.persistentDataPath, "Levels");
            if (!Directory.Exists(folder)) return found;
            foreach (var f in Directory.GetFiles(folder, "*.json"))
                if (assetNames == null || !assetNames.Contains(Path.GetFileNameWithoutExtension(f))) found.Add(f);
            found.Sort(StringComparer.OrdinalIgnoreCase);
#endif
            return found;
        }

#if UNITY_EDITOR
        /// <summary>The folder of Level1.json, or null when the project has none.</summary>
        static string ProjectLevelsFolder()
        {
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("Level1 t:TextAsset", new[] { "Assets" }))
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (string.Equals(Path.GetFileName(path), "Level1.json", StringComparison.OrdinalIgnoreCase))
                    return Path.GetDirectoryName(path).Replace('\\', '/');
            }
            return null;
        }

        /// <summary>The LevelPreviews (Previews/LevelPreview1.json...) next to Level1.json, for the build. Empty when there are none.</summary>
        public static List<TextAsset> FindProjectPreviews()
        {
            var found = new List<TextAsset>();
            string levels = ProjectLevelsFolder();
            if (levels == null) return found;
            string folder = levels + "/" + LevelPreviews.Folder;
            if (!UnityEditor.AssetDatabase.IsValidFolder(folder)) return found;
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:TextAsset", new[] { folder }))
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (asset != null) found.Add(asset);
            }
            found.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            return found;
        }

        /// <summary>
        /// The level.json files next to Level1.json: Level1, Level2... in number order, then the others (Custom1...)
        /// by name. Null when the project has no Level1.json. Run each time, so new levels show up without rebuilding scenes.
        /// </summary>
        public static List<TextAsset> FindProjectLevels()
        {
            string folder = ProjectLevelsFolder();
            if (folder == null) return null;
            var found = new List<TextAsset>();
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:TextAsset", new[] { folder }))
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetDirectoryName(path).Replace('\\', '/') != folder) continue; // not sub folders
                if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (asset != null && asset.text.Contains("\"rows\"")) found.Add(asset);
            }
            found.Sort(CompareLevels);
            return found;
        }
#endif

        /// <summary>Level1, Level2... in number order, then the other names alphabetically.</summary>
        public static int CompareLevels(TextAsset a, TextAsset b)
        {
            int na = ListNumber(a.name), nb = ListNumber(b.name);
            if (na != nb) return na.CompareTo(nb);
            return string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>How many official levels the game has: Level1 .. Level5 (Massif, Sky, Forest, Desert, Volcano).</summary>
        public const int OfficialCount = 5;

        /// <summary>
        /// Level1 .. Level5: the official levels (Main Menu > Official Level). A built game can't edit them (Adrian
        /// 2026-10-23); the Unity Editor still can (Level Editor > Load).
        /// </summary>
        public static bool IsOfficial(string key)
        {
            int n = ListNumber(key ?? "");
            return n >= 1 && n <= OfficialCount;
        }

        /// <summary>"Level3" → 3; names without "Level N" go after the numbered ones.</summary>
        static int ListNumber(string name)
        {
            var m = System.Text.RegularExpressions.Regex.Match(name, @"^level\s*0*(\d+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return m.Success ? int.Parse(m.Groups[1].Value) : int.MaxValue;
        }

        /// <summary>
        /// The saved level as it is on disk now, or null. Save reads this just before writing, so name, music, speed,
        /// themes and props typed into the file while the editor was open are kept.
        /// <paramref name="unreadable"/> is true when the file exists but could not be read (a typo, or locked).
        /// </summary>
        public static LevelData ReadSaved(string path, out bool unreadable)
        {
            unreadable = false;
            if (!File.Exists(path)) return null;
            try
            {
                return LevelData.ParseJson(File.ReadAllText(path));
            }
            catch (Exception)
            {
                unreadable = true;
                return null;
            }
        }

        /// <summary>
        /// Writes the file at <paramref name="path"/> (from <see cref="SavePath"/>, worked out when the level was opened). The text goes to a .tmp file first and then replaces the old file,
        /// so a save that is cut off (app killed, disk full) never leaves a half-written level.
        /// </summary>
        /// <param name="backup">Copy the old file to "&lt;path&gt;.broken" first (used when it could not be read).</param>
        public static void Write(string path, string text, bool backup = false)
        {
            string folder = Path.GetDirectoryName(path);
            bool newFolder = !string.IsNullOrEmpty(folder) && !Directory.Exists(folder);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            if (backup && File.Exists(path)) File.Copy(path, path + ".broken", true);

            string tmp = path + ".tmp"; // Unity ignores .tmp files, so it never imports this one
            File.WriteAllText(tmp, text);
            if (!File.Exists(path))
            {
                File.Move(tmp, path);
            }
            else
            {
                try
                {
                    File.Replace(tmp, path, null);
                }
                catch (Exception)
                {
                    // File.Replace is not available everywhere; copying is not atomic, but the .tmp copy was complete.
                    File.Copy(tmp, path, true);
                    File.Delete(tmp);
                }
            }
#if UNITY_EDITOR
            // Re-import so the TextAsset (and the Gameplay scene's Level File) shows the new rows right away.
            // A folder made just now (Previews) is not known to Unity yet: a refresh imports it with the file.
            if (newFolder) UnityEditor.AssetDatabase.Refresh();
            else UnityEditor.AssetDatabase.ImportAsset(path);
#endif
        }
    }
}
