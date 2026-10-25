using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// Your own songs as audio files (.ogg, .mp3, .wav), found by name in the Music folders:
    /// Assets/StreamingAssets/Music (ships with the game) and the save folder's Music
    /// (Application.persistentDataPath/Music, where a phone or PC player can drop new songs).
    /// </summary>
    public static class MusicLibrary
    {
        static readonly string[] Extensions = { ".ogg", ".mp3", ".wav" };
        /// <summary>The song file types, without dots (for a file chooser).</summary>
        public static readonly string[] FileTypes = { "mp3", "ogg", "wav" };
        /// <summary>The list of song files in StreamingAssets/Music, written at build time (Android packs that folder in the APK).</summary>
        public const string IndexFile = "index.txt";

        /// <summary>Android: StreamingAssets is inside the APK (a jar: URL), so it can't be listed or checked with File.</summary>
        static bool Packed => Application.streamingAssetsPath.Contains("://");
        static string StreamingFolder => Path.Combine(Application.streamingAssetsPath, "Music");
        static List<string> packedFiles;

        /// <summary>The song files of the packed StreamingAssets/Music (from its index.txt), read once.</summary>
        static List<string> PackedFiles()
        {
            if (packedFiles != null) return packedFiles;
            packedFiles = new List<string>();
            using (var req = UnityEngine.Networking.UnityWebRequest.Get(StreamingFolder + "/" + IndexFile))
            {
                var op = req.SendWebRequest();
                while (!op.isDone) System.Threading.Thread.Sleep(1); // a small file inside the APK
                if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success) return packedFiles;
                foreach (var line in req.downloadHandler.text.Split('\n'))
                {
                    string f = line.Trim();
                    if (f.Length > 0 && IsAudio(f)) packedFiles.Add(f);
                }
            }
            return packedFiles;
        }

        /// <summary>The URL UnityWebRequest loads a song from (a file path, or Android's jar: path).</summary>
        public static string Url(string path)
        {
            if (!path.Contains("://")) return new System.Uri(path).AbsoluteUri;
            return path.Replace("%", "%25").Replace(" ", "%20").Replace("[", "%5B").Replace("]", "%5D").Replace("#", "%23");
        }

        /// <summary>Names a Main Menu world song can have, by index (Massif, Sky, Forest, Desert, Volcano).</summary>
        static readonly string[][] WorldNames =
        {
            new[] { "Massif", "Music1" }, new[] { "Sky", "Music2" }, new[] { "Forest", "Music3" },
            new[] { "Desert", "Dessert", "Music4" }, new[] { "Volcano", "Volanco", "Music5" },
        };

        /// <summary>The song file of world <paramref name="index"/> (0 Massif .. 4 Volcano) in a Music folder, or null.</summary>
        public static string FindWorldFile(int index)
        {
            if (index < 0 || index >= WorldNames.Length) return null;
            foreach (var name in WorldNames[index])
            {
                string f = FindFile(name);
                if (f != null) return f;
            }
            return null;
        }

        public static IEnumerable<string> Folders()
        {
            yield return Path.Combine(Application.streamingAssetsPath, "Music");
            yield return Path.Combine(Application.persistentDataPath, "Music");
        }

        /// <summary>Song names (file names without extension) in every Music folder.</summary>
        public static List<string> FileNames()
        {
            var names = new List<string>();
            if (Packed)
                foreach (var f in PackedFiles())
                {
                    string n = Path.GetFileNameWithoutExtension(f);
                    if (!names.Contains(n)) names.Add(n);
                }
            foreach (var folder in Folders())
            {
                if (!Directory.Exists(folder)) continue;
                foreach (var f in Directory.GetFiles(folder))
                {
                    if (!IsAudio(f)) continue;
                    string n = Path.GetFileNameWithoutExtension(f);
                    if (!names.Contains(n)) names.Add(n);
                }
            }
            names.Sort(System.StringComparer.OrdinalIgnoreCase);
            return names;
        }

        /// <summary>Full path of the song file called <paramref name="name"/> (with or without extension), or null.</summary>
        /// <summary>
        /// The world song a world's name stands for: "Massif" = Music1, "Sky" = Music2, "Forest" = Music3,
        /// "Desert" / "Dessert" = Music4, "Volcano" = Music5. Null for other names. Used when no song has that exact name.
        /// </summary>
        public static string WorldSong(string name)
        {
            switch ((name ?? "").Trim().ToLowerInvariant())
            {
                case "massif": return "Music1";
                case "sky": return "Music2";
                case "forest": return "Music3";
                case "desert": case "dessert": return "Music4";
                case "volcano": case "volanco": return "Music5";
            }
            return null;
        }

        public static string FindFile(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (Packed)
                foreach (var f in PackedFiles())
                    if (string.Equals(f, name, System.StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(Path.GetFileNameWithoutExtension(f), name, System.StringComparison.OrdinalIgnoreCase))
                        return StreamingFolder + "/" + f;
            foreach (var folder in Folders())
            {
                if (!Directory.Exists(folder)) continue;
                string direct = Path.Combine(folder, name);
                if (IsAudio(direct) && File.Exists(direct)) return direct;
                foreach (var ext in Extensions)
                {
                    string p = Path.Combine(folder, name + ext);
                    if (File.Exists(p)) return p;
                }
            }
            return null;
        }

        /// <summary>
        /// Puts a song file from anywhere (a PC folder, a phone's Music or Download folder) in a Music folder and returns
        /// its song name for level.json "music". The Unity Editor copies it to Assets/StreamingAssets/Music (so it ships
        /// with the game), a built game to its save folder's Music. A file already in a Music folder is used as it is;
        /// a different song with the same name gets "Name (2)".
        /// </summary>
        public static string Import(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) throw new IOException("the file is not there any more");
            if (!IsAudio(path)) throw new IOException("not an .mp3, .ogg or .wav file");
            string full = Path.GetFullPath(path);
            string baseName = Path.GetFileNameWithoutExtension(path);
            foreach (var folder in Folders())
                if (Directory.Exists(folder) && string.Equals(Path.GetDirectoryName(full), Path.GetFullPath(folder), System.StringComparison.OrdinalIgnoreCase))
                    return baseName;

            string target = Application.isEditor ? StreamingFolder : Path.Combine(Application.persistentDataPath, "Music");
            Directory.CreateDirectory(target);
            long size = new FileInfo(path).Length;
            for (int n = 1; ; n++)
            {
                string name = n == 1 ? baseName : $"{baseName} ({n})";
                string existing = FindFile(name);
                if (existing == null)
                {
                    File.Copy(path, Path.Combine(target, name + Path.GetExtension(path).ToLowerInvariant()));
                    return name;
                }
                // The same song chosen again: no second copy.
                if (!existing.Contains("://") && File.Exists(existing) && new FileInfo(existing).Length == size) return name;
            }
        }

        public static AudioType TypeOf(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".mp3": return AudioType.MPEG;
                case ".wav": return AudioType.WAV;
                default: return AudioType.OGGVORBIS;
            }
        }

        static bool IsAudio(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return System.Array.IndexOf(Extensions, ext) >= 0;
        }
    }
}
