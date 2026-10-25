using UnityEditor;
using UnityEngine;

namespace RTXOn.EditorTools
{
    /// <summary>
    /// Adrian's UI icons (any Resources/Icons folder): imported as sprites at their own size (a plain texture would be
    /// stretched to a power of two, squashing the Start Button and the No World picture), without mipmaps, so the
    /// game loads them with Resources.Load&lt;Sprite&gt;("Icons/Name") (UiFactory.Icon).
    /// Also the menu's word pictures (Resources/MenuText: PAUSED, LEVEL OFFICIAL... white on clear), up to 2048 wide.
    /// </summary>
    public class IconImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            bool words = path.Contains("/Resources/MenuText/");
            if (!path.Contains("/Resources/Icons/") && !words) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = words ? 2048 : 512;
        }

        [MenuItem("RTX ON/Reimport UI Icons", priority = 43)]
        static void Reimport()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string p = path.Replace('\\', '/');
                if (p.Contains("/Resources/Icons/") || p.Contains("/Resources/MenuText/")) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            Debug.Log("RTXOn: UI icons and menu word pictures imported as sprites.");
        }
    }
}
