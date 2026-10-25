using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RTXOn.EditorTools
{
    /// <summary>
    /// "RTX ON > Fragile See-Through" / "Fragile Solid": switches the fragile materials of every theme (Fragile,
    /// Fragile 1/2, Checker, Row, Column and the Classic Tile Fragile / Fragile Active) to URP Surface Type
    /// Transparent (alpha blend) or back to Opaque. See-through uses the texture's alpha times Base Color alpha, so a
    /// texture without alpha stays solid until Base Color alpha is lowered. Same as picking Surface Type on the material.
    /// </summary>
    public static class FragileTransparency
    {
        [MenuItem("RTX ON/Fragile See-Through", priority = 13)]
        static void SeeThrough() => Apply(true);

        [MenuItem("RTX ON/Fragile Solid", priority = 14)]
        static void Solid() => Apply(false);

        static void Apply(bool transparent)
        {
            var done = new HashSet<Material>();
            foreach (var guid in AssetDatabase.FindAssets("t:TrackTheme", new[] { "Assets" }))
            {
                var theme = AssetDatabase.LoadAssetAtPath<TrackTheme>(AssetDatabase.GUIDToAssetPath(guid));
                if (theme == null) continue;
                foreach (var m in new[] { theme.fragile, theme.fragile1, theme.fragile2, theme.fragileChecker, theme.fragileRow,
                                          theme.fragileColumn, theme.tileFragile, theme.tileFragileActive })
                {
                    if (m == null || !done.Add(m)) continue;
                    SetTransparent(m, transparent);
                }
            }
            AssetDatabase.SaveAssets();
            string what = transparent ? "see-through (Transparent)" : "solid (Opaque)";
            Debug.Log($"RTX ON: {done.Count} fragile material(s) made {what}.");
            EditorUtility.DisplayDialog("RTX ON", $"{done.Count} fragile material(s) made {what}." +
                (transparent ? "\n\nThe see-through part comes from the texture's alpha. A texture without alpha stays solid: lower Base Color alpha on the material." : ""), "OK");
        }

        /// <summary>URP Lit / Simple Lit / Unlit: Surface Type Transparent with alpha blending, or Opaque.</summary>
        public static void SetTransparent(Material m, bool transparent)
        {
            if (m == null || !m.HasProperty("_Surface")) return;
            Undo.RecordObject(m, "Fragile Transparency");
            m.SetFloat("_Surface", transparent ? 1f : 0f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f); // Alpha
            m.SetFloat("_SrcBlend", (float)(transparent ? BlendMode.SrcAlpha : BlendMode.One));
            m.SetFloat("_DstBlend", (float)(transparent ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
            if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", (float)(transparent ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
            m.SetFloat("_ZWrite", transparent ? 0f : 1f);
            m.SetOverrideTag("RenderType", transparent ? "Transparent" : "Opaque");
            if (transparent) m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            else m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.DisableKeyword("_ALPHAMODULATE_ON");
            m.SetShaderPassEnabled("DepthOnly", !transparent);
            m.renderQueue = transparent ? (int)RenderQueue.Transparent : -1;
            EditorUtility.SetDirty(m);
        }

        /// <summary>True when the texture has an alpha channel (a see-through atlas).</summary>
        public static bool HasAlpha(Texture tex)
        {
            if (tex == null) return false;
            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex)) as TextureImporter;
            return importer != null ? importer.DoesSourceTextureHaveAlpha() && importer.alphaSource != TextureImporterAlphaSource.None
                                    : UnityEngine.Experimental.Rendering.GraphicsFormatUtility.HasAlphaChannel(tex.graphicsFormat);
        }
    }
}
