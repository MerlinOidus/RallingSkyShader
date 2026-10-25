using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RTXOn.EditorTools
{
    /// <summary>
    /// Before every build: writes Assets/StreamingAssets/Music/index.txt, the list of song files there. Android packs
    /// StreamingAssets inside the APK where it can't be listed, so the game (MusicLibrary) reads this list instead.
    /// </summary>
    public class MusicIndexBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report) => WriteIndex();

        [MenuItem("RTX ON/Update Music List (StreamingAssets)", priority = 42)]
        public static void WriteIndex()
        {
            string folder = Path.Combine(Application.dataPath, "StreamingAssets", "Music");
            if (!Directory.Exists(folder)) return;
            var songs = Directory.GetFiles(folder)
                .Where(f => new[] { ".ogg", ".mp3", ".wav" }.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .Select(Path.GetFileName)
                .OrderBy(n => n, System.StringComparer.OrdinalIgnoreCase)
                .ToArray();
            string path = Path.Combine(folder, RTXOn.MusicLibrary.IndexFile);
            string text = string.Join("\n", songs) + "\n";
            if (File.Exists(path) && File.ReadAllText(path) == text) return;
            File.WriteAllText(path, text);
            AssetDatabase.Refresh();
            Debug.Log($"RTX ON: Music list written ({songs.Length} songs) to StreamingAssets/Music/{RTXOn.MusicLibrary.IndexFile}.");
        }
    }
}
