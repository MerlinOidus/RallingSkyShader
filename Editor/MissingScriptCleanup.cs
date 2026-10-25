using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RTXOn.EditorTools
{
    /// <summary>
    /// "RTX ON > Remove Missing Scripts From Prefabs": the original prefabs (04_portal and others from MESH/GameObject)
    /// still list the original game's scripts, which this project doesn't have. Unity warns "The referenced script on
    /// this Behaviour is missing" every time one is placed. This takes those empty script slots off every prefab under
    /// Assets (nothing else on the prefab changes), so the warning goes away.
    /// </summary>
    public static class MissingScriptCleanup
    {
        [MenuItem("RTX ON/Remove Missing Scripts From Prefabs", priority = 40)]
        public static void Clean()
        {
            var cleaned = new List<string>();
            int removed = 0;
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (!path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase)) continue; // models can't hold scripts
                    if (EditorUtility.DisplayCancelableProgressBar("RTX ON", path, (float)i / guids.Length)) break;

                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (asset == null || CountMissing(asset) == 0) continue;

                    var root = PrefabUtility.LoadPrefabContents(path);
                    int count = 0;
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                        count += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                    if (count > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        removed += count;
                        cleaned.Add(System.IO.Path.GetFileNameWithoutExtension(path));
                    }
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            AssetDatabase.SaveAssets();
            Debug.Log(cleaned.Count == 0
                ? "RTX ON: no prefab has a missing script."
                : $"RTX ON: removed {removed} missing script(s) from {cleaned.Count} prefab(s): {string.Join(", ", cleaned)}.");
        }

        static int CountMissing(GameObject go)
        {
            int n = 0;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                n += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            return n;
        }
    }
}
