using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RTXOn.EditorTools
{
    /// <summary>
    /// "RTX ON > Hook Up Tile Types": puts the original floor meshes (01_X .. 15_O, 07_jump, 01_moveArrow,
    /// 02_moveAutoArrow) on every theme and makes its atlas materials from the original textures of the theme's
    /// Tile Set number (General1, Fragile1, FragileActive1, Mover1, MoverAuto1, Enemy1 for set 1, and so on).
    /// Materials go in a "&lt;Theme&gt; Tiles" folder next to the theme; running it again keeps your colour and glow
    /// changes and only swaps the textures (after you change Tile Set).
    /// </summary>
    public static class TileTypeHookup
    {
        static readonly string[] ShapeNames =
            { "01_X", "02_X1", "03_X2a", "04_X2b", "05_X3", "06_X4", "07_T", "08_T1a", "09_T1b", "10_T2", "11_L", "12_L1", "13_I", "14_D", "15_O" };

        // Original MidgroundManager.blockData per world (Tile Set 1 = world 0): prefabs, Height, RiseHeight, minX, maxX, BlankChances.
        static readonly (string[] models, float height, float rise, float minX, float maxX, int blank)[] MidgroundSets =
        {
            (new[] { "midground1a" }, 5f, 15f, 0f, 12f, 4),
            (new[] { "midground2a" }, 8f, 15f, 2f, 7f, 2),
            (new[] { "midground3L", "midground3R" }, 7.5f, 27f, 4f, 9f, 0),
            (new[] { "midground4L", "midground4R" }, 7.75f, 21f, 0f, 10f, 12),
            (new[] { "midground1a" }, 5f, 15f, 0f, 12f, 2),
            (new[] { "midground1a" }, 5f, 15f, 0f, 12f, 4),
            (new[] { "midground1a" }, 5f, 15f, 0f, 12f, 2),
            (new[] { "midground4L", "midground4R" }, 7.75f, 21f, 0f, 10f, 12),
            (new[] { "midground2a" }, 8f, 15f, 2f, 7f, 2),
        };

        // Texture set number of each theme name (General<n>, Fragile<n>, FragileActive<n>, Mover<n>, MoverAuto<n>, Enemy<n>).
        static readonly (string word, int set)[] SetNames =
        {
            ("Massif", 1), ("Sky", 2), ("Forest", 3), ("Morning", 4), ("Volcano", 5), ("Volanco", 5), ("Desert", 7), ("Illusion", 9),
        };

        /// <summary>The Tile Set a theme's name asks for, 1 (Massif) when the name has none of the words.</summary>
        static int SetFromName(string themeName)
        {
            foreach (var (word, set) in SetNames)
                if (themeName.IndexOf(word, System.StringComparison.OrdinalIgnoreCase) >= 0) return set;
            return 1;
        }

        /// <summary>Starting glow of the atlas materials (Emission = the atlas itself times this). Change it on the material.</summary>
        static readonly Color DefaultGlow = new Color(0.35f, 0.35f, 0.35f);

        [MenuItem("RTX ON/Hook Up Tile Types", priority = 11)]
        public static void HookUp()
        {
            var prefabs = new Dictionary<string, GameObject>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var name = Path.GetFileNameWithoutExtension(path);
                if (!prefabs.ContainsKey(name)) prefabs[name] = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            var textures = new Dictionary<string, Texture2D>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var name = Path.GetFileNameWithoutExtension(path);
                if (!textures.ContainsKey(name)) textures[name] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }

            GameObject P(string n) => prefabs.TryGetValue(n, out var g) ? g : null;
            if (P("01_X") == null)
            {
                EditorUtility.DisplayDialog("RTX ON", "Could not find the prefab 01_X. Copy the original MESH/GameObject folder " +
                    "(01_X .. 16_OD, 07_jump, 01_moveArrow, 02_moveAutoArrow) into Assets, then run this again.", "OK");
                return;
            }

            // The RTX look gives the floor meshes new UVs at runtime, which needs Read/Write in a build.
            int readable = 0;
            foreach (var n in ShapeNames) readable += MakeReadable(P(n));

            var report = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:TrackTheme"))
            {
                var themePath = AssetDatabase.GUIDToAssetPath(guid);
                var theme = AssetDatabase.LoadAssetAtPath<TrackTheme>(themePath);
                if (theme == null) continue;
                Undo.RecordObject(theme, "Hook Up Tile Types");

                // A theme still on set 1 gets the set its name says (Adrian 2026-10-21): Massif 1, Sky 2, Forest 3,
                // Morning 4, Volcano 5, Desert 7, Illusion 9. A set you picked yourself (2 or more) is kept.
                if (theme.tileSet <= 1) theme.tileSet = SetFromName(theme.name);
                int set = Mathf.Max(1, theme.tileSet);

                theme.tileX = P(ShapeNames[0]);
                theme.tileX1 = P(ShapeNames[1]);
                theme.tileX2a = P(ShapeNames[2]);
                theme.tileX2b = P(ShapeNames[3]);
                theme.tileX3 = P(ShapeNames[4]);
                theme.tileX4 = P(ShapeNames[5]);
                theme.tileT = P(ShapeNames[6]);
                theme.tileT1a = P(ShapeNames[7]);
                theme.tileT1b = P(ShapeNames[8]);
                theme.tileT2 = P(ShapeNames[9]);
                theme.tileL = P(ShapeNames[10]);
                theme.tileL1 = P(ShapeNames[11]);
                theme.tileI = P(ShapeNames[12]);
                theme.tileD = P(ShapeNames[13]);
                theme.tileO = P(ShapeNames[14]);
                theme.tileOD = P("16_OD"); // closed block for animated tiles
                theme.tileJump = P("07_jump");
                theme.tileMoverArrow = P("01_moveArrow");
                theme.tileMoverAutoArrow = P("02_moveAutoArrow");
                // The original gem and star (MESH/GameObject 01_pickup, 02_star), unless the theme has its own.
                if (theme.gemModel == null) theme.gemModel = P("01_pickup");
                if (theme.starModel == null) theme.starModel = P("02_star");
                // The original laser enemies, unless the theme has its own: tile 105 needs 19_enemyPillarLaser (its own
                // pillar with the beam), not 18_enemyPillar with the 16_enemyLaser turret on top.
                if (theme.pillarModel == null) theme.pillarModel = P("18_enemyPillar");
                if (theme.pillarLaserModel == null) theme.pillarLaserModel = P("19_enemyPillarLaser");
                if (theme.laserModel == null) theme.laserModel = P("16_enemyLaser");
                if (theme.spotlightModel == null) theme.spotlightModel = P("17_enemySpotlight");
                // Their material: the original M_Fragile (the Fragile slot may hold the glass M_FragileRTX).
                if (theme.pickupMaterial == null) theme.pickupMaterial = FindMaterial("M_Fragile");

                string folder = Path.GetDirectoryName(themePath).Replace('\\', '/') + "/" + theme.name + " Tiles";
                if (!AssetDatabase.IsValidFolder(folder))
                    AssetDatabase.CreateFolder(Path.GetDirectoryName(themePath).Replace('\\', '/'), theme.name + " Tiles");

                var missing = new List<string>();
                Texture2D T(string n)
                {
                    if (textures.TryGetValue(n + set, out var t)) return t;
                    missing.Add(n + set);
                    // A set without its own texture uses set 1, so the floor never turns white.
                    return textures.TryGetValue(n + "1", out t) ? t : null;
                }
                theme.tileGeneral = Atlas(folder, "M_TileGeneral", theme.tileGeneral, T("General"));
                theme.tileFragile = Atlas(folder, "M_TileFragile", theme.tileFragile, T("Fragile"));
                theme.tileFragileActive = Atlas(folder, "M_TileFragileActive", theme.tileFragileActive, T("FragileActive"));
                // Glass: a fragile atlas with alpha is drawn see-through (RTX ON > Fragile Solid turns it off again).
                if (FragileTransparency.HasAlpha(theme.tileFragile.GetTexture("_BaseMap"))) FragileTransparency.SetTransparent(theme.tileFragile, true);
                if (FragileTransparency.HasAlpha(theme.tileFragileActive.GetTexture("_BaseMap"))) FragileTransparency.SetTransparent(theme.tileFragileActive, true);
                theme.tileMover = Atlas(folder, "M_TileMover", theme.tileMover, T("Mover"));
                theme.tileMoverAuto = Atlas(folder, "M_TileMoverAuto", theme.tileMoverAuto, T("MoverAuto"));

                // Enemy atlas of the set for the original enemy meshes, unless the theme uses its own (Indo...) atlas.
                var enemy = T("Enemy");
                var current = theme.modelMaterial != null && theme.modelMaterial.HasProperty("_BaseMap") ? theme.modelMaterial.GetTexture("_BaseMap") : null;
                bool ownAtlas = current != null && !current.name.StartsWith("Enemy", System.StringComparison.OrdinalIgnoreCase);
                if (enemy != null && !ownAtlas)
                {
                    theme.modelMaterial = Atlas(folder, "M_TileEnemy", theme.modelMaterial != null && theme.modelMaterial.name == "M_TileEnemy" ? theme.modelMaterial : null, enemy, false);
                    EnemyGlow(folder, set, enemy, theme.modelMaterial);
                }

                // The RTX look paints the shapes with the theme's own slab materials: empty slots get the generated ones.
                var filled = new List<string>();
                Material Rtx(Material current, string name, string label)
                {
                    if (current != null) return current;
                    var m = FindMaterial(name);
                    if (m != null) filled.Add(label);
                    return m;
                }
                theme.floor = Rtx(theme.floor, "M_Floor", "Floor");
                theme.fragile = Rtx(theme.fragile, "M_Fragile", "Fragile");
                theme.jump = Rtx(theme.jump, "M_Jump", "Jump");
                theme.mover = Rtx(theme.mover, "M_Mover", "Mover");
                theme.moverAuto = Rtx(theme.moverAuto, "M_MoverAuto", "Mover Auto");
                theme.moverArrow = Rtx(theme.moverArrow, "M_MoverArrow", "Mover Arrow");

                // Midground blocks of the set, with the original's heights and spacing: into an empty list, or over one this
                // menu filled before (only midground* prefabs) when the Tile Set has changed. Your own models are kept.
                bool autoList = theme.midgroundModels == null || theme.midgroundModels.Length == 0 ||
                                System.Array.TrueForAll(theme.midgroundModels, m => m != null && m.name.StartsWith("midground", System.StringComparison.OrdinalIgnoreCase));
                if (autoList)
                {
                    var mid = MidgroundSets[(set - 1) % MidgroundSets.Length];
                    var found = new List<GameObject>();
                    foreach (var n in mid.models) if (P(n) != null) found.Add(P(n));
                    bool same = theme.midgroundModels != null && theme.midgroundModels.Length == found.Count &&
                                found.TrueForAll(m => System.Array.IndexOf(theme.midgroundModels, m) >= 0);
                    if (found.Count > 0 && !same)
                    {
                        theme.midgroundModels = found.ToArray();
                        theme.midgroundHeight = mid.height;
                        theme.midgroundRise = mid.rise;
                        theme.midgroundMinX = mid.minX;
                        theme.midgroundMaxX = mid.maxX;
                        theme.midgroundBlank = mid.blank;
                        filled.Add("Midground");
                    }
                }

                // Background picture of the set (Background1..8), only into an empty slot.
                if (theme.background == null && textures.TryGetValue("Background" + set, out var background))
                {
                    theme.background = background;
                    filled.Add("Background");
                }

                EditorUtility.SetDirty(theme);
                report.Add($"{theme.name}: set {set}" + (missing.Count > 0 ? " (missing " + string.Join(", ", missing) + ", used set 1)" : "") +
                           (ownAtlas ? ", kept its own model atlas" : "") +
                           (filled.Count > 0 ? ", filled empty " + string.Join(", ", filled) : ""));
            }
            AssetDatabase.SaveAssets();
            Debug.Log("RTX ON: Tile Types hooked up. " + string.Join("; ", report) +
                      (readable > 0 ? $". Turned Read/Write on for {readable} floor meshes" : "") +
                      ". Tile Look on the theme: RTX = the theme's own Floor/Fragile/Mover materials on these shapes, Classic = the original atlases. " +
                      "Change a theme's Tile Set and run this again to switch its textures; tune the Classic glow with Emission on its M_Tile materials.");
        }

        /// <summary>A material in the project with exactly this name (the generated M_Floor, M_Fragile...), or null.</summary>
        static Material FindMaterial(string name)
        {
            foreach (var guid in AssetDatabase.FindAssets(name + " t:Material"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == name) return AssetDatabase.LoadAssetAtPath<Material>(path);
            }
            return null;
        }

        /// <summary>Turns Read/Write on for the meshes of a prefab (mesh assets and model imports). Returns how many changed.</summary>
        static int MakeReadable(GameObject prefab)
        {
            if (prefab == null) return 0;
            int changed = 0;
            foreach (var f in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = f.sharedMesh;
                if (mesh == null || mesh.isReadable) continue;
                var path = AssetDatabase.GetAssetPath(mesh);
                if (string.IsNullOrEmpty(path)) continue;
                if (AssetImporter.GetAtPath(path) is ModelImporter importer)
                {
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                }
                else
                {
                    var so = new SerializedObject(mesh);
                    var prop = so.FindProperty("m_IsReadable");
                    if (prop == null) continue;
                    prop.boolValue = true;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(mesh);
                }
                changed++;
            }
            return changed;
        }

        /// <summary>An atlas material: the given one with its texture swapped, else a new URP Lit one with the atlas as glow.</summary>
        /// <summary>Night glow of the enemies (HDR): T_EnemyGlow times this. Change it on M_TileEnemy.</summary>
        static readonly Color EnemyGlowColor = new Color(2.5f, 2.5f, 2.5f);

        /// <summary>
        /// The enemies' own glow map, apart from their colours (like T_FragileGlow for the fragile tiles):
        /// T_EnemyGlow&lt;set&gt;.png next to M_TileEnemy, made once from the Enemy atlas by keeping only its bright,
        /// coloured parts (the neon rings and lines) and making the rest black. Paint it yourself to choose what glows
        /// (black = off, colour = glow); an existing file is never overwritten. M_TileEnemy gets it as Emission Map;
        /// the environment keeps it dark by day and lights it at sunset and night.
        /// </summary>
        static void EnemyGlow(string folder, int set, Texture2D atlas, Material m)
        {
            if (atlas == null || m == null || !m.HasProperty("_EmissionMap")) return;
            string path = $"{folder}/T_EnemyGlow{set}.png";
            var glow = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (glow == null)
            {
                // Read the atlas through the GPU, so it works whatever its import settings (Read/Write off, compressed).
                var rt = RenderTexture.GetTemporary(atlas.width, atlas.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                var active = RenderTexture.active;
                Graphics.Blit(atlas, rt);
                RenderTexture.active = rt;
                var read = new Texture2D(atlas.width, atlas.height, TextureFormat.RGBA32, false);
                read.ReadPixels(new Rect(0, 0, atlas.width, atlas.height), 0, 0);
                RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(rt);

                var px = read.GetPixels();
                for (int i = 0; i < px.Length; i++)
                {
                    Color.RGBToHSV(px[i], out _, out float sat, out float val);
                    float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.8f, val)) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.55f, sat));
                    px[i] = new Color(px[i].r * k, px[i].g * k, px[i].b * k, 1f);
                }
                read.SetPixels(px);
                read.Apply(false);
                File.WriteAllBytes(path, read.EncodeToPNG());
                Object.DestroyImmediate(read);
                AssetDatabase.ImportAsset(path);
                glow = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            if (glow == null) return;
            var current = m.GetTexture("_EmissionMap");
            if (current != null && current != atlas && current != glow) return; // your own glow map stays
            m.SetTexture("_EmissionMap", glow);
            if (current != glow || m.GetColor("_EmissionColor").maxColorComponent < 0.01f) m.SetColor("_EmissionColor", EnemyGlowColor);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(m);
        }

        static Material Atlas(string folder, string name, Material existing, Texture2D tex, bool glow = true)
        {
            string path = $"{folder}/{name}.mat";
            var m = existing != null && EditorUtility.IsPersistent(existing) ? existing : AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = m == null;
            if (created)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, enableInstancing = true };
                m.SetColor("_BaseColor", Color.white);
                m.SetFloat("_Smoothness", 0.35f);
                m.SetFloat("_Metallic", 0f);
                if (glow)
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", DefaultGlow);
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                }
                AssetDatabase.CreateAsset(m, path);
            }
            if (tex != null)
            {
                m.SetTexture("_BaseMap", tex);
                if (glow) m.SetTexture("_EmissionMap", tex); // the atlas lights itself: lines and arrows glow in their own colours
            }
            if (glow) m.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
