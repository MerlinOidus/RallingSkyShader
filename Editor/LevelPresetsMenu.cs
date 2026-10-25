using System.IO;
using UnityEditor;
using UnityEngine;

namespace RTXOn.EditorTools
{
    /// <summary>
    /// "RTX ON > Apply Level 1-5 Settings": writes the name, music, speed, gem number and difficulty Adrian chose
    /// (2026-10-20) into Level1.json .. Level5.json in the project. Only those header fields change: rows, objects,
    /// pickups, themes, cameras, checkpoints and parts stay as you painted them.
    /// </summary>
    public static class LevelPresetsMenu
    {
        struct Preset
        {
            public string name, music;
            public float speed, stars;
            public int gems;
            public Preset(string name, string music, float speed, int gems, float stars)
            {
                this.name = name; this.music = music; this.speed = speed; this.gems = gems; this.stars = stars;
            }
        }

        static readonly Preset[] Presets =
        {
            new Preset("Massif", "Massif", 7.5f, 20, 1.3f),
            new Preset("Sky", "Sky", 8f, 20, 2f),
            new Preset("Forest", "Forest", 8.5f, 20, 3f),
            new Preset("Dessert", "Dessert", 9f, 20, 3.3f),
            new Preset("Volcano", "Volcano", 10f, 20, 4.2f),
        };

        [MenuItem("RTX ON/Apply Level 1-5 Settings", priority = 34)]
        public static void Apply()
        {
            int done = 0;
            var log = new System.Text.StringBuilder();
            for (int i = 0; i < Presets.Length; i++)
            {
                string fileName = $"Level{i + 1}";
                string path = FindLevel(fileName);
                if (path == null)
                {
                    log.AppendLine($"{fileName}.json: not found in the project, skipped.");
                    continue;
                }
                LevelData level = null;
                try { level = LevelData.ParseJson(File.ReadAllText(path)); }
                catch (System.Exception e) { Debug.LogWarning($"RTX ON: {path}: {e.Message}"); }
                if (level == null)
                {
                    log.AppendLine($"{fileName}.json: could not be read, skipped.");
                    continue;
                }
                var p = Presets[i];
                level.Name = p.name;
                level.Music = p.music;
                level.SpeedStart = p.speed;
                level.SpeedEnd = 0f;                          // constant speed
                level.SpeedRampRows = 0;
                level.HeaderGems = p.gems;
                level.Difficulty = p.stars;
                File.WriteAllText(path, level.ToJson(level.Name, level.Music, p.speed, 0f, 0, level.ThemeChanges, level.Props));
                log.AppendLine($"{fileName}.json: {p.name}, music {p.music}, speed {p.speed}, {p.gems} gems, {p.stars} stars.");
                done++;
            }
            AssetDatabase.Refresh();
            Debug.Log($"RTX ON: level settings applied to {done} level(s).\n{log}");
            EditorUtility.DisplayDialog("RTX ON", $"Updated {done} of {Presets.Length} levels.\n\n{log}", "OK");
        }

        /// <summary>LevelN.json anywhere under Assets (the one the game uses first: not inside a backup folder).</summary>
        static string FindLevel(string fileName)
        {
            string found = null;
            foreach (var guid in AssetDatabase.FindAssets(fileName + " t:TextAsset", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.Equals(Path.GetFileName(path), fileName + ".json", System.StringComparison.OrdinalIgnoreCase)) continue;
                if (found == null || path.Contains("/Levels/")) found = path;
            }
            return found;
        }
    }
}
