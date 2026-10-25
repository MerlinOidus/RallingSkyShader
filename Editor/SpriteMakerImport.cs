using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RTXOn.EditorTools
{
    /// <summary>
    /// RTX ON > SpriteMaker > Update Mask List: the SpriteMaker reads its masks as PNG files renamed .bytes in a
    /// Resources/SpriteMaker folder (small in the build, exact pixels on every platform). This renames any new .png
    /// there to .bytes and writes index.txt, the list of masks the SpriteMaker knows.
    /// </summary>
    public static class SpriteMakerImport
    {
        const string Folder = "/Resources/" + SpriteMakerLab.ResourceRoot + "/";

        static bool InFolder(string path) => path.Replace('\\', '/').Contains(Folder);

        [MenuItem("RTX ON/SpriteMaker/Update Mask List", priority = 40)]
        static void UpdateMaskList()
        {
            // New masks dropped in as .png become .bytes (TextAsset), with their .meta.
            int renamed = 0;
            foreach (var p in AssetDatabase.GetAllAssetPaths().Where(p => InFolder(p) && p.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)).ToList())
            {
                string to = p.Substring(0, p.Length - ".png".Length) + ".bytes";
                if (File.Exists(to)) File.Delete(to);
                File.Move(p, to);
                if (File.Exists(p + ".meta")) File.Delete(p + ".meta");
                renamed++;
            }
            if (renamed > 0) AssetDatabase.Refresh();

            var roots = new Dictionary<string, List<string>>();
            foreach (var p in AssetDatabase.GetAllAssetPaths())
            {
                var path = p.Replace('\\', '/');
                if (!InFolder(path) || !path.EndsWith(".bytes", System.StringComparison.OrdinalIgnoreCase)) continue;
                int at = path.IndexOf(Folder, System.StringComparison.Ordinal);
                string root = path.Substring(0, at + Folder.Length);
                string rel = path.Substring(root.Length);
                rel = rel.Substring(0, rel.Length - ".bytes".Length);
                if (!roots.TryGetValue(root, out var list)) roots[root] = list = new List<string>();
                list.Add(rel);
            }
            if (roots.Count == 0)
            {
                Debug.LogWarning($"RTXOn SpriteMaker: no masks in a {Folder} folder (copy the SpriteMaker folder into Assets).");
                return;
            }
            foreach (var kv in roots)
            {
                kv.Value.Sort(System.StringComparer.Ordinal);
                string file = kv.Key + "index.txt";
                File.WriteAllText(file, string.Join("\n", kv.Value) + "\n");
                AssetDatabase.ImportAsset(file);
                Debug.Log($"RTXOn SpriteMaker: {file} lists {kv.Value.Count} mask(s){(renamed > 0 ? $" ({renamed} .png renamed to .bytes)" : "")}.");
            }
        }
    }
}
