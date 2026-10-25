using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RTXOn.EditorTools
{
    /// <summary>
    /// Before every build: copies the theme library (Levels/Themes/*.themert) to Assets/RTXOn/Resources/RTXOnThemes as
    /// .bytes, so the built game (Android too) has every Custom Theme, not only the ones a level carries inside it.
    /// </summary>
    public class ThemeBundleBuild : IPreprocessBuildWithReport
    {
        const string Target = "Assets/RTXOn/Resources/" + ThemeLibrary.BundledFolder;

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report) => Copy();

        [MenuItem("RTX ON/Update Bundled Themes (Resources)", priority = 43)]
        public static void Copy()
        {
            string source = ThemeLibrary.LibraryFolder();
            if (!Directory.Exists(source)) return;
            Directory.CreateDirectory(Target);
            int written = 0, removed = 0;
            foreach (var path in Directory.GetFiles(source, "*" + CustomTheme.FileExtension))
            {
                string to = Path.Combine(Target, Path.GetFileNameWithoutExtension(path) + ".bytes");
                byte[] bytes = File.ReadAllBytes(path);
                if (File.Exists(to) && SameBytes(File.ReadAllBytes(to), bytes)) continue;
                File.WriteAllBytes(to, bytes);
                written++;
            }
            // A theme deleted from the library leaves the build too.
            foreach (var path in Directory.GetFiles(Target, "*.bytes"))
            {
                string from = Path.Combine(source, Path.GetFileNameWithoutExtension(path) + CustomTheme.FileExtension);
                if (File.Exists(from)) continue;
                AssetDatabase.DeleteAsset(path.Replace('\\', '/'));
                removed++;
            }
            if (written > 0 || removed > 0) AssetDatabase.Refresh();
            Debug.Log($"RTX ON: Bundled Themes up to date in {Target} ({written} written, {removed} removed).");
        }

        static bool SameBytes(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
