using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RTXOn.EditorTools
{
    /// <summary>
    /// "RTX ON > Hook Up Screen Filters": adds the <see cref="ScreenFilterFeature"/> to every URP renderer asset in
    /// the project (PC_Renderer, Mobile_Renderer...), so screen filter triggers show in the game. Also runs by itself
    /// whenever the scripts load; a renderer that already has it is left alone. Same as selecting the renderer asset
    /// and pressing Add Renderer Feature > Screen Filter Feature.
    /// </summary>
    [InitializeOnLoad]
    public static class ScreenFilterSetup
    {
        static ScreenFilterSetup() => EditorApplication.delayCall += () => HookUp(false);

        [MenuItem("RTX ON/Hook Up Screen Filters", priority = 15)]
        static void Menu() => HookUp(true);

        static void HookUp(bool ask)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var shader = Shader.Find(ScreenFilters.ShaderName);
            int found = 0, added = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets" }))
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid));
                if (data == null) continue;
                found++;
                var existing = data.rendererFeatures.Find(f => f is ScreenFilterFeature) as ScreenFilterFeature;
                if (existing != null)
                {
                    if (existing.shader == null && shader != null)
                    {
                        existing.shader = shader;
                        EditorUtility.SetDirty(existing);
                    }
                    continue;
                }

                // Like the renderer's own Add Renderer Feature button: a sub-asset, listed with its file ID.
                var feature = ScriptableObject.CreateInstance<ScreenFilterFeature>();
                feature.name = "RTX Screen Filters";
                feature.shader = shader;
                AssetDatabase.AddObjectToAsset(feature, data);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
                var so = new SerializedObject(data);
                var list = so.FindProperty("m_RendererFeatures");
                var map = so.FindProperty("m_RendererFeatureMap");
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
                if (map != null)
                {
                    map.arraySize++;
                    map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                }
                so.ApplyModifiedProperties();
                data.SetDirty();
                EditorUtility.SetDirty(data);
                added++;
            }
            if (added > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"RTX ON: Screen Filters added to {added} URP renderer(s).");
            }
            if (!ask) return;
            string message = found == 0 ? "No URP renderer asset found under Assets. Is the project on URP?"
                : added > 0 ? $"Screen Filters added to {added} URP renderer(s)."
                : "Every URP renderer already has the Screen Filters.";
            if (shader == null) message += "\n\nShader RTXOn/ScreenFilters is missing: copy Shaders/Resources/RTXOnScreenFilters.shader into the project.";
            EditorUtility.DisplayDialog("RTX ON", message, "OK");
        }
    }
}
