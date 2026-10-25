using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RTXOn.EditorTools
{
    /// <summary>
    /// "RTX ON > Add Selected Models As Custom Models": puts the FBX / prefabs selected in the Project window in the
    /// Custom Model Library (Assets/RTXOn/Resources/RTXOnCustomModels), so the Level Editor's Custom Obstacles window
    /// can pick them (Model > Select...) and built games have them too.
    /// </summary>
    public static class CustomModelsMenu
    {
        const string Folder = "Assets/RTXOn/Resources";

        [MenuItem("RTX ON/Add Selected Models As Custom Models", priority = 32)]
        static void AddSelected()
        {
            var lib = FindOrCreate();
            Undo.RecordObject(lib, "Add Custom Models");
            var added = new List<string>();
            foreach (var go in Selection.GetFiltered<GameObject>(SelectionMode.Assets))
            {
                if (lib.models.Contains(go)) continue;
                lib.models.Add(go);
                added.Add(go.name);
            }
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Debug.Log(added.Count > 0
                ? $"RTX ON: added {string.Join(", ", added)} to {AssetDatabase.GetAssetPath(lib)}. Pick them in the Level Editor: Object tab > + > Model > Select..."
                : "RTX ON: no new models selected (select FBX/prefab files in the Project window).");
            Selection.activeObject = lib;
        }

        [MenuItem("RTX ON/Add Selected Models As Custom Models", true)]
        static bool AddSelectedValidate() => Selection.GetFiltered<GameObject>(SelectionMode.Assets).Length > 0;

        /// <summary>The library asset (any CustomModelLibrary named RTXOnCustomModels in a Resources folder), made if missing.</summary>
        public static CustomModelLibrary FindOrCreate()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:CustomModelLibrary"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) != CustomModelLibrary.ResourceName || !path.Contains("/Resources/")) continue;
                var found = AssetDatabase.LoadAssetAtPath<CustomModelLibrary>(path);
                if (found != null) return found;
            }
            if (!AssetDatabase.IsValidFolder("Assets/RTXOn")) AssetDatabase.CreateFolder("Assets", "RTXOn");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/RTXOn", "Resources");
            var lib = ScriptableObject.CreateInstance<CustomModelLibrary>();
            AssetDatabase.CreateAsset(lib, $"{Folder}/{CustomModelLibrary.ResourceName}.asset");
            return lib;
        }
    }
}
