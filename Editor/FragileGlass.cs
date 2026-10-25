using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RTXOn.EditorTools
{
    /// <summary>
    /// "RTX ON > Make Fragile Glass (M_FragileRTX)": makes three materials with the shader RTXOn/Glass (refraction,
    /// Fresnel reflections, colour depth, glowing section outline) in Assets/RTXOn/Materials, the same glass:
    /// M_FragileRTX for Fragile, Fragile 1/2, Row and Column (a section that falls as one reads as one slab),
    /// M_FragileRTXChecker for Checker (its tiles never join, so each one reads as its own block) and M_FragileRTXSide
    /// for the sides of every fragile tile (Track Theme > Fragile Side). Fills those slots in every theme and turns on
    /// Opaque Texture (full resolution) on the URP assets, which the refraction needs. Existing materials keep their
    /// settings (only Section Outline is switched on). Reset Look puts the old fragile materials back.
    /// </summary>
    public static class FragileGlass
    {
        const string Folder = "Assets/RTXOn/Materials";
        const string MaterialPath = Folder + "/M_FragileRTX.mat";
        const string CheckerPath = Folder + "/M_FragileRTXChecker.mat";
        const string SidePath = Folder + "/M_FragileRTXSide.mat";
        const string ShaderName = "RTXOn/Glass";

        [MenuItem("RTX ON/Make Fragile Glass (M_FragileRTX)", priority = 16)]
        static void Make()
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                EditorUtility.DisplayDialog("RTX ON", "Shader '" + ShaderName + "' not found. Copy Shaders/Glass.shader into the project first.", "OK");
                return;
            }

            var themes = new List<TrackTheme>();
            foreach (var guid in AssetDatabase.FindAssets("t:TrackTheme", new[] { "Assets" }))
            {
                var theme = AssetDatabase.LoadAssetAtPath<TrackTheme>(AssetDatabase.GUIDToAssetPath(guid));
                if (theme != null) themes.Add(theme);
            }

            var glass = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            bool created = glass == null;
            if (created)
            {
                if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/RTXOn", "Materials");
                glass = new Material(shader) { name = "M_FragileRTX" };
                // The cracks of the old fragile material glow on the glass at night too.
                var old = themes.Count > 0 ? themes[0].fragile : null;
                if (old != null && old.shader != shader && old.HasProperty("_EmissionMap") && old.GetTexture("_EmissionMap") != null)
                {
                    glass.SetTexture("_EmissionMap", old.GetTexture("_EmissionMap"));
                    glass.SetTextureScale("_EmissionMap", old.GetTextureScale("_EmissionMap"));
                    if (old.HasProperty("_EmissionColor")) glass.SetColor("_EmissionColor", old.GetColor("_EmissionColor"));
                }
                AssetDatabase.CreateAsset(glass, MaterialPath);
            }
            else if (glass.shader != shader)
            {
                glass.shader = shader;
                EditorUtility.SetDirty(glass);
            }

            // Checker: the same glass. Its tiles never join, so the section outline draws every Checker tile as one block.
            var checker = Ensure(CheckerPath, "M_FragileRTXChecker", glass, shader, out bool checkerCreated);
            // Fragile Side: the sides of every fragile tile (Full and Checker), a material of their own.
            var side = Ensure(SidePath, "M_FragileRTXSide", glass, shader, out bool sideCreated);
            // Section outline on (an older M_FragileRTX had it off) and no checkerboard (gone from the shader).
            foreach (var m in new[] { glass, checker })
            {
                m.SetFloat("_Lines", 1f);
                EditorUtility.SetDirty(m);
            }

            foreach (var theme in themes)
            {
                Undo.RecordObject(theme, "Make Fragile Glass");
                theme.fragile = theme.fragile1 = theme.fragile2 = glass;
                theme.fragileRow = theme.fragileColumn = glass;
                theme.fragileChecker = checker;
                theme.fragileSide = side;
                EditorUtility.SetDirty(theme);
            }

            int pipelines = EnableOpaqueTexture();
            AssetDatabase.SaveAssets();
            Selection.activeObject = glass;
            EditorGUIUtility.PingObject(glass);

            string msg = "In " + Folder + ", for " + themes.Count + " theme(s):\n" +
                         "M_FragileRTX" + (created ? " (new)" : "") + ": Fragile, Fragile 1/2, Row, Column - one glass slab per section\n" +
                         "M_FragileRTXChecker" + (checkerCreated ? " (new)" : "") + ": Checker - every tile its own block\n" +
                         "M_FragileRTXSide" + (sideCreated ? " (new)" : "") + ": the sides of all fragile tiles\n\n" +
                         "Opaque Texture turned on in " + pipelines + " URP asset(s) (the glass needs it to see what is behind).\n\n" +
                         "The glass shows with Tile Look = RTX. Rebuild or press Play to see it.";
            Debug.Log("RTX ON: " + msg.Replace("\n\n", " "));
            EditorUtility.DisplayDialog("RTX ON", msg, "OK");
        }

        /// <summary>The material at <paramref name="path"/>, or a new copy of <paramref name="source"/> there.</summary>
        static Material Ensure(string path, string name, Material source, Shader shader, out bool created)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            created = m == null;
            if (created)
            {
                m = new Material(source) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader)
            {
                m.shader = shader;
                EditorUtility.SetDirty(m);
            }
            return m;
        }

        /// <summary>Opaque Texture on, at full resolution (no blur), on every URP asset of the project.</summary>
        static int EnableOpaqueTexture()
        {
            int count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset", new[] { "Assets" }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null) continue;
                var so = new SerializedObject(asset);
                var opaque = so.FindProperty("m_RequireOpaqueTexture");
                if (opaque != null) opaque.boolValue = true;
                var downsampling = so.FindProperty("m_OpaqueDownsampling");
                if (downsampling != null) downsampling.intValue = 0; // None
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(asset);
                count++;
            }
            return count;
        }
    }
}
