using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RTXOn.EditorTools
{
    /// <summary>
    /// Before every build: lists every TrackTheme of the project in Assets/RTXOn/Resources (see <see cref="ThemeRegistry"/>),
    /// so official themes made after Build Gameplay Scene / Open Level Editor Scene are in the built game too.
    /// </summary>
    public class ThemeRegistryBuild : IPreprocessBuildWithReport
    {
        const string Folder = "Assets/RTXOn/Resources";

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report) => UpdateList();

        [MenuItem("RTX ON/Update Theme List (Resources)", priority = 44)]
        public static void UpdateList()
        {
            var themes = new List<TrackTheme>();
            foreach (var guid in AssetDatabase.FindAssets("t:TrackTheme"))
            {
                var t = AssetDatabase.LoadAssetAtPath<TrackTheme>(AssetDatabase.GUIDToAssetPath(guid));
                if (t != null && !themes.Contains(t)) themes.Add(t);
            }
            themes.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));

            var pictures = new List<ThemeRegistry.Picture>();
            foreach (var t in themes) pictures.Add(new ThemeRegistry.Picture { name = t.name, background = t.background });

            Write(ThemeRegistry.ThemesResource, r => { r.themes = themes; r.pictures = new List<ThemeRegistry.Picture>(); });
            Write(ThemeRegistry.PicturesResource, r => { r.themes = new List<TrackTheme>(); r.pictures = pictures; });
            AssetDatabase.SaveAssets();
            Debug.Log($"RTX ON: Theme List up to date in {Folder} ({themes.Count} themes: {string.Join(", ", themes.ConvertAll(t => t.name))}).");
        }

        static void Write(string name, System.Action<ThemeRegistry> fill)
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/RTXOn")) AssetDatabase.CreateFolder("Assets", "RTXOn");
                AssetDatabase.CreateFolder("Assets/RTXOn", "Resources");
            }
            string path = Folder + "/" + name + ".asset";
            var registry = AssetDatabase.LoadAssetAtPath<ThemeRegistry>(path);
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<ThemeRegistry>();
                fill(registry);
                AssetDatabase.CreateAsset(registry, path);
            }
            else
            {
                fill(registry);
                EditorUtility.SetDirty(registry);
            }
        }
    }
}
