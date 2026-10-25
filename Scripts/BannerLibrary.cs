using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// Banner pictures of the level list. A level's "banner" names a texture (Main Menu > Banners, e.g.
    /// "World 1 - Valea") or a .png/.jpg file in a Banners folder: Assets/StreamingAssets/Banners (ships with the game)
    /// or the save folder's Banners (Application.persistentDataPath/Banners, where a player can drop new pictures).
    /// No banner = the texture whose name starts with "World N" (or "Level N", "Banner N"), N = the level's number.
    /// </summary>
    public static class BannerLibrary
    {
        static readonly string[] Extensions = { ".png", ".jpg", ".jpeg" };
        static readonly Dictionary<string, Texture2D> loaded = new Dictionary<string, Texture2D>();

        public static IEnumerable<string> Folders()
        {
            yield return Path.Combine(Application.streamingAssetsPath, "Banners");
            yield return Path.Combine(Application.persistentDataPath, "Banners");
        }

        /// <summary>
        /// The banner of a level: <paramref name="name"/> if it is found, else the "World N" texture for
        /// <paramref name="number"/>, else null.
        /// </summary>
        public static Texture2D Get(string name, int number, IList<Texture2D> textures)
        {
            var t = Find(name, textures);
            if (t == null) t = Auto(number, textures);
            return t != null ? t : NoWorld;
        }

        /// <summary>The "no world" picture (Icons/NoWorld) for a level without a banner, or null.</summary>
        public static Texture2D NoWorld
        {
            get
            {
                var s = UiFactory.Icon("NoWorld");
                return s != null ? s.texture : null;
            }
        }

        /// <summary>A texture or banner file by name (with or without extension), or null.</summary>
        public static Texture2D Find(string name, IList<Texture2D> textures)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (textures != null)
                foreach (var t in textures)
                    if (t != null && string.Equals(t.name, name, System.StringComparison.OrdinalIgnoreCase)) return t;
            string bare = Path.GetFileNameWithoutExtension(name);
            if (loaded.TryGetValue(bare.ToLowerInvariant(), out var cached) && cached != null) return cached;
            string path = FindFile(name);
            if (path == null) return null;
            try
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true) { name = bare, wrapMode = TextureWrapMode.Clamp };
                if (!tex.LoadImage(File.ReadAllBytes(path)))
                {
                    Object.Destroy(tex);
                    return null;
                }
                loaded[bare.ToLowerInvariant()] = tex;
                return tex;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"RTXOn: could not read banner {path}: {e.Message}");
                return null;
            }
        }

        /// <summary>The "World N - ..." (or "Level N", "Banner N") texture for level number <paramref name="number"/>.</summary>
        public static Texture2D Auto(int number, IList<Texture2D> textures)
        {
            if (number <= 0 || textures == null) return null;
            var pattern = new Regex(@"^\s*(world|level|banner)\s*[-_ ]?\s*0*" + number + @"(?!\d)", RegexOptions.IgnoreCase);
            foreach (var t in textures)
                if (t != null && pattern.IsMatch(t.name)) return t;
            return null;
        }

        /// <summary>Every banner name to choose from: the textures, then the files of the Banners folders.</summary>
        public static List<string> Names(IList<Texture2D> textures)
        {
            var names = new List<string>();
            if (textures != null)
                foreach (var t in textures)
                    if (t != null && !names.Contains(t.name)) names.Add(t.name);
            foreach (var folder in Folders())
            {
                if (!Directory.Exists(folder)) continue;
                var files = new List<string>(Directory.GetFiles(folder));
                files.Sort(System.StringComparer.OrdinalIgnoreCase);
                foreach (var f in files)
                    if (IsImage(f) && !names.Contains(Path.GetFileNameWithoutExtension(f))) names.Add(Path.GetFileNameWithoutExtension(f));
            }
            return names;
        }

        static string FindFile(string name)
        {
            foreach (var folder in Folders())
            {
                if (!Directory.Exists(folder)) continue;
                string direct = Path.Combine(folder, name);
                if (IsImage(direct) && File.Exists(direct)) return direct;
                foreach (var ext in Extensions)
                {
                    string p = Path.Combine(folder, name + ext);
                    if (File.Exists(p)) return p;
                }
            }
            return null;
        }

        static bool IsImage(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return System.Array.IndexOf(Extensions, ext) >= 0;
        }

        /// <summary>
        /// Shows <paramref name="tex"/> in <paramref name="image"/> filling its rect (cropped, not stretched), like a cover photo.
        /// </summary>
        public static void Cover(UnityEngine.UI.RawImage image, Texture tex)
        {
            image.texture = tex;
            if (tex == null)
            {
                image.uvRect = new Rect(0f, 0f, 1f, 1f);
                return;
            }
            var size = image.rectTransform.rect.size;
            if (size.x <= 0f || size.y <= 0f) size = image.rectTransform.sizeDelta;
            float box = size.y > 0f ? size.x / size.y : 1f;
            float pic = tex.height > 0 ? (float)tex.width / tex.height : 1f;
            if (pic > box)
            {
                float w = box / pic;
                image.uvRect = new Rect((1f - w) * 0.5f, 0f, w, 1f);
            }
            else
            {
                float h = pic / box;
                image.uvRect = new Rect(0f, (1f - h) * 0.5f, 1f, h); // tall picture: keep the middle
            }
        }
    }
}
