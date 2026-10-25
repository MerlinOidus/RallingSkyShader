using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// Every Custom Theme the editor and the game know, by name: the library (.themert files in the Themes folder next
    /// to the levels) plus the ones the open level carries. Turns them into TrackThemes (built on their Base Theme), and
    /// applies a level's Theme Settings (Roller, Block, Midground from another theme, Mover Edge, background Offset and
    /// Gradient) to a look. Also reads and writes the share files (.rsrt, .rsrtp, .themert, .levelrsrt).
    /// </summary>
    public static class ThemeLibrary
    {
        /// <summary>Folder (inside Resources) whose materials RTX ON custom themes can use, so builds keep them.</summary>
        public const string MaterialsFolder = "RTXOnMaterials";

        /// <summary>
        /// Folder (inside Resources) with the build's copy of the theme library: each Levels/Themes .themert saved as
        /// "&lt;name&gt;.bytes" before every build (RTX ON > Update Bundled Themes), so phones have the themes too.
        /// </summary>
        public const string BundledFolder = "RTXOnThemes";
        static bool bundledLoaded;

        /// <summary>Name of the copy the Custom Theme window shows in the 3D preview while editing (never listed or saved).</summary>
        public const string PreviewName = "~Custom Theme Preview~";

        static readonly Dictionary<string, CustomTheme> defs = new Dictionary<string, CustomTheme>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<CustomTheme, TrackTheme> built = new Dictionary<CustomTheme, TrackTheme>();
        static readonly Dictionary<string, TrackTheme> variants = new Dictionary<string, TrackTheme>();
        static readonly Dictionary<CustomTheme, Texture2D> icons = new Dictionary<CustomTheme, Texture2D>();
        static readonly Dictionary<CustomTheme, List<UnityEngine.Object>> made = new Dictionary<CustomTheme, List<UnityEngine.Object>>(); // materials and textures of built themes
        static List<UnityEngine.Object> making; // the list Build fills now
        static List<Material> resourceMaterials;

        /// <summary>Goes up on every change (themes or a level's Theme Settings), so views know to build looks again.</summary>
        public static int Version { get; private set; } = 1;

        /// <summary>Something that changes looks changed (Theme Settings): built variants are made again.</summary>
        public static void Touch()
        {
            Version++;
            foreach (var v in variants.Values) if (v != null) UnityEngine.Object.Destroy(v);
            variants.Clear();
        }

        public static CustomTheme Get(string name) => !string.IsNullOrEmpty(name) && defs.TryGetValue(name, out var d) ? d : null;

        public static bool Has(string name) => Get(name) != null;

        /// <summary>All of them, by name.</summary>
        public static List<CustomTheme> All()
        {
            var list = new List<CustomTheme>(defs.Values);
            list.RemoveAll(d => d.name == PreviewName);
            list.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            return list;
        }

        /// <summary>Adds or replaces the theme with this name.</summary>
        public static void Set(CustomTheme def)
        {
            if (def == null) return;
            def.Normalize();
            if (defs.TryGetValue(def.name, out var old))
            {
                if (old.ToJson(false) == def.ToJson(false)) return;
                Forget(old);
            }
            defs[def.name] = def;
            Touch();
        }

        public static void Remove(string name)
        {
            var old = Get(name);
            if (old == null) return;
            Forget(old);
            defs.Remove(name);
            Touch();
        }

        static void Forget(CustomTheme def)
        {
            if (built.TryGetValue(def, out var t) && t != null) UnityEngine.Object.Destroy(t);
            built.Remove(def);
            if (made.TryGetValue(def, out var list))
                foreach (var o in list) if (o != null) UnityEngine.Object.Destroy(o);
            made.Remove(def);
            if (icons.TryGetValue(def, out var icon) && icon != null) UnityEngine.Object.Destroy(icon);
            icons.Remove(def);
        }

        // ---------------- Levels ----------------

        /// <summary>The themes a level carries ("customThemes"): they win over library ones with the same name while it is open.</summary>
        public static void AddFromLevel(LevelData level)
        {
            if (level == null) return;
            foreach (var t in level.CustomThemes) Set(t.Clone());
        }

        /// <summary>The custom themes a level uses (its look and the looks of its theme changes), for its "customThemes".</summary>
        public static List<CustomTheme> UsedBy(LevelData level, IList<TrackTheme> officials)
        {
            var list = new List<CustomTheme>();
            if (level == null) return list;
            var names = new List<string> { level.Look };
            foreach (var c in level.ThemeChanges)
                if (c != null)
                {
                    names.Add(c.look);
                    if (c.objects != ThemeChange.OwnObjects) names.Add(c.objects);
                }
            foreach (var n in names)
            {
                if (string.IsNullOrEmpty(n) || Official(n, officials) != null) continue;
                var d = Get(n) ?? level.FindCustomTheme(n);
                if (d != null && !list.Contains(d)) list.Add(d);
            }
            return list;
        }

        // ---------------- Library (.themert files) ----------------

        public static string LibraryFolder() => Path.Combine(LevelFiles.LevelsFolder(), "Themes").Replace('\\', '/');

        public static string LibraryPath(string name) => $"{LibraryFolder()}/{SafeFileName(name)}{CustomTheme.FileExtension}";

        /// <summary>Reads every .themert in the library. Returns how many were read.</summary>
        public static int LoadLibrary()
        {
            string folder = LibraryFolder();
            if (!Directory.Exists(folder)) return 0;
            int n = 0;
            foreach (var path in Directory.GetFiles(folder, "*" + CustomTheme.FileExtension))
            {
                CustomTheme def;
                try { def = CustomTheme.FromJson(File.ReadAllText(path)); }
                catch (Exception) { continue; }
                if (def == null) continue;
                Set(def);
                n++;
            }
            return n;
        }

        /// <summary>
        /// Once per run: the themes shipped with the game (Resources/RTXOnThemes), for the names not known yet. In the
        /// Unity Editor the project's Levels/Themes files are read instead (they are the source of those copies).
        /// </summary>
        public static void LoadBundled()
        {
            if (bundledLoaded) return;
            bundledLoaded = true;
#if UNITY_EDITOR
            LoadLibrary();
#else
            foreach (var asset in Resources.LoadAll<TextAsset>(BundledFolder))
            {
                CustomTheme def;
                try { def = CustomTheme.FromJson(asset.text); }
                catch (Exception) { continue; }
                if (def != null && !Has(def.name)) Set(def);
            }
#endif
        }

        public static void SaveToLibrary(CustomTheme def) => LevelFiles.Write(LibraryPath(def.name), def.ToJson());

        public static void DeleteFromLibrary(string name)
        {
            string path = LibraryPath(name);
#if UNITY_EDITOR
            if (path.StartsWith("Assets/") && UnityEditor.AssetDatabase.DeleteAsset(path)) return;
#endif
            if (File.Exists(path)) File.Delete(path);
        }

        public static string SafeFileName(string name)
        {
            name ??= "";
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return string.IsNullOrWhiteSpace(name) ? "Theme" : name.Trim();
        }

        // ---------------- Looks ----------------

        /// <summary>An official theme by name, like the game: the asset name, or the name without "Theme" (Sky = SkyTheme).</summary>
        public static TrackTheme Official(string name, IList<TrackTheme> officials)
        {
            if (string.IsNullOrEmpty(name) || officials == null) return null;
            foreach (var t in officials)
                if (t != null && (string.Equals(t.name, name, StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(t.name, name + "Theme", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(t.name, name + " Theme", StringComparison.OrdinalIgnoreCase)))
                    return t;
            return null;
        }

        /// <summary>
        /// The look called <paramref name="name"/>: an official theme, else a custom one, with the level's Theme
        /// Settings on top. Null when there is none with that name.
        /// </summary>
        public static TrackTheme Resolve(string name, IList<TrackTheme> officials, LevelThemeSettings settings)
        {
            var look = Official(name, officials);
            if (look == null)
            {
                var def = Get(name);
                if (def != null) look = Build(def, officials);
            }
            return look != null ? WithSettings(look, settings, officials) : null;
        }

        /// <summary>
        /// A look key (<see cref="LevelData.LookKey"/>): "Look" or "Look|Objects". An empty look part =
        /// <paramref name="defaultLook"/>. The objects come from the Objects theme as it is (no Theme Settings).
        /// </summary>
        public static TrackTheme ResolveKey(string key, IList<TrackTheme> officials, LevelThemeSettings settings, TrackTheme defaultLook)
        {
            if (key == null) return null;
            int bar = key.IndexOf('|');
            if (bar < 0) return Resolve(key, officials, settings);
            string lookName = key.Substring(0, bar), objectsName = key.Substring(bar + 1);
            var look = lookName.Length == 0 ? WithSettings(defaultLook, settings, officials) : Resolve(lookName, officials, settings);
            if (look == null || objectsName.Length == 0) return look;
            var objects = Resolve(objectsName, officials, null);
            return WithObjects(look, objects);
        }

        /// <summary>What "objects" means for Theme trigger > Objects: the enemy models, their materials and lights.</summary>
        static readonly string[] ObjectFields =
        {
            "riserModel", "crusherModel", "floaterModel", "binaryMiniModel", "binarySlasherModel", "binaryHammerModel",
            "pillarModel", "laserModel", "pillarLaserModel", "spotlightModel", "rollerModel", "treeModel", "palmModel",
            "riserVariants", "treeVariants", "modelMaterial", "autoModelMaterial", "fitModels", "modelsFaceBall",
            "hazard", "hazardSafe", "trunk", "foliage", "beamRed", "beamWhite", "spotlightColor", "laserLightColor",
        };

        /// <summary><paramref name="look"/> (tiles, colours, background, environments) with <paramref name="objects"/>'s objects.</summary>
        public static TrackTheme WithObjects(TrackTheme look, TrackTheme objects)
        {
            if (look == null || objects == null || look == objects) return look;
            string key = look.GetInstanceID() + "+objects+" + objects.GetInstanceID();
            if (variants.TryGetValue(key, out var cached) && cached != null) return cached;
            var t = UnityEngine.Object.Instantiate(look);
            t.name = look.name + " + " + objects.name;
            foreach (var name in ObjectFields)
            {
                var f = typeof(TrackTheme).GetField(name);
                if (f != null) f.SetValue(t, f.GetValue(objects));
            }
            // Models rows (Adjust, Animation, Prop): the objects theme's first, then the look's other ones.
            var rows = new List<ThemeModel>(objects.models);
            foreach (var row in t.models)
                if (row != null && row.model != null && !rows.Exists(r => r != null && r.model == row.model)) rows.Add(row);
            t.models = rows;
            variants[key] = t;
            return t;
        }

        /// <summary>The TrackTheme of a custom theme: its Base Theme with the custom textures, materials, colours and environments.</summary>
        public static TrackTheme Build(CustomTheme def, IList<TrackTheme> officials)
        {
            if (def == null) return null;
            if (built.TryGetValue(def, out var cached) && cached != null) return cached;
            var source = Official(def.baseTheme, officials) ?? FirstOfficial(officials);
            if (source == null) return null;

            var t = UnityEngine.Object.Instantiate(source);
            t.name = def.name;
            t.textureLook = def.mode;
            making = new List<UnityEngine.Object>();
            made[def] = making;

            var tileGlowColor = def.tileGlowColor * def.tileGlowStrength;
            if (def.mode == TileLook.Classic)
            {
                // Each atlas has its own glow picture (General Glow, Fragile Glow...).
                t.tileGeneral = Retexture(source.tileGeneral, def.general, Decode(def.tileGlow, def.name + " General Glow"), tileGlowColor, def.name + " General");
                t.tileFragile = Retexture(source.tileFragile, def.fragile, Decode(def.fragileGlow, def.name + " Fragile Glow"), tileGlowColor, def.name + " Fragile");
                t.tileFragileActive = Retexture(source.tileFragileActive, def.fragileActive, Decode(def.fragileActiveGlow, def.name + " FragileActive Glow"), tileGlowColor, def.name + " FragileActive");
                t.tileMover = Retexture(source.tileMover, def.mover, Decode(def.moverGlow, def.name + " Mover Glow"), tileGlowColor, def.name + " Mover");
                t.tileMoverAuto = Retexture(source.tileMoverAuto, def.moverAuto, Decode(def.moverAutoGlow, def.name + " MoverAuto Glow"), tileGlowColor, def.name + " MoverAuto");
            }
            else
            {
                t.floor = FindMaterial(def.floorMaterial, officials) ?? source.floor;
                t.floorSide = FindMaterial(def.floorSideMaterial, officials) ?? source.floorSide;
                var fragile = FindMaterial(def.fragileMaterial, officials);
                if (fragile != null)
                    t.fragile = t.fragile1 = t.fragile2 = t.fragileChecker = t.fragileRow = t.fragileColumn = fragile;
                t.fragileSide = FindMaterial(def.fragileSideMaterial, officials) ?? source.fragileSide;
                t.mover = FindMaterial(def.moverMaterial, officials) ?? source.mover;
                t.moverAuto = FindMaterial(def.moverAutoMaterial, officials) ?? source.moverAuto;
                t.jump = FindMaterial(def.jumpMaterial, officials) ?? source.jump;
            }

            // Objects: the same in both modes.
            var enemyGlow = Decode(def.enemyGlow, def.name + " Enemy Glow");
            if (source.modelMaterial != null)
            {
                var m = Retexture(source.modelMaterial, def.enemy, enemyGlow, def.enemyGlowColor * def.enemyGlowStrength, def.name + " Enemy", true);
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", def.enemyGloss);
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", def.enemySpecular);
                t.modelMaterial = m;
            }

            var background = Decode(def.background, def.name + " Background");
            if (background != null)
            {
                background.wrapMode = TextureWrapMode.Clamp;
                t.background = background;
            }
            t.backgroundTint = def.backgroundTint;
            t.midgroundTint = def.midgroundTint;
            t.midFogColor = def.midFogColor;
            if (def.ballColor.a > 0f) t.ballColor = def.ballColor;

            if (def.environments.Count > 0)
            {
                var list = new List<EnvironmentPreset>();
                foreach (var e in def.environments)
                    list.Add(e.ToPreset(FindPreset(e.from, officials) ?? FindPresetByName(source, e.name)));
                t.environments = list;
            }

            making = null;
            built[def] = t;
            return t;
        }

        /// <summary>
        /// A look with the level's Theme Settings on top (the same look when they are all Default and it needs no RTX
        /// enemy models). Model Look RTX swaps in the RTX Pillar / Laser / Block slots.
        /// </summary>
        public static TrackTheme WithSettings(TrackTheme look, LevelThemeSettings s, IList<TrackTheme> officials)
        {
            if (look == null) return look;
            bool plain = s == null || s.IsDefault;
            var modelLook = s != null && s.modelLook >= 0 ? (TileLook)s.modelLook : look.modelLook;
            bool rtxModels = modelLook == TileLook.RTX && look.HasRtxEnemyModels;
            if (plain && !rtxModels) return look;
            string key = look.GetInstanceID() + "|" + (s != null ? s.Key : "") + "|" + (int)modelLook;
            if (variants.TryGetValue(key, out var cached) && cached != null) return cached;

            var t = UnityEngine.Object.Instantiate(look);
            t.name = look.name;
            t.modelLook = modelLook;
            if (s != null && s.textureLook >= 0) t.textureLook = (TileLook)s.textureLook;
            if (rtxModels) t.UseRtxModels(s != null && !string.IsNullOrEmpty(s.block)); // a chosen Block theme wins
            if (s == null)
            {
                variants[key] = t;
                return t;
            }
            var roller = Official(s.roller, officials);
            if (roller != null && roller.rollerModel != null) TakeModel(t, roller, roller.rollerModel, m => t.rollerModel = m);
            var block = Official(s.block, officials);
            if (block != null && block.blockModel != null) TakeModel(t, block, block.blockModel, m => t.blockModel = m);
            if (string.Equals(s.midground, LevelThemeSettings.NoneName, StringComparison.OrdinalIgnoreCase))
                t.midgroundModels = new GameObject[0];
            else
            {
                var mid = Official(s.midground, officials);
                if (mid != null)
                {
                    // The blocks' shapes and placement; their texture and mid fog stay the look's.
                    t.midgroundModels = (GameObject[])mid.midgroundModels.Clone();
                    t.midgroundHeight = mid.midgroundHeight;
                    t.midgroundRise = mid.midgroundRise;
                    t.midgroundMinX = mid.midgroundMinX;
                    t.midgroundMaxX = mid.midgroundMaxX;
                    t.midgroundBlank = mid.midgroundBlank;
                }
            }
            if (s.moverEdge != MoverEdge.Auto) t.moverEdge = s.moverEdge;
            t.backgroundY = Mathf.Clamp(look.backgroundY + s.backgroundOffset, -0.5f, 0.5f);
            if (s.gradient >= 0) t.backgroundGradient = (BackgroundGradient)s.gradient;
            if (s.hideBackground) t.background = null; // the sky shows (BackgroundView draws nothing without a picture)
            variants[key] = t;
            return t;
        }

        /// <summary>Puts another theme's model in a slot, with its Models row (Adjust, Animation).</summary>
        static void TakeModel(TrackTheme into, TrackTheme from, GameObject model, Action<GameObject> set)
        {
            set(model);
            var row = from.Entry(model);
            if (row != null && into.Entry(model) == null) into.models.Add(row);
        }

        static TrackTheme FirstOfficial(IList<TrackTheme> officials)
        {
            if (officials == null) return null;
            foreach (var t in officials) if (t != null) return t;
            return null;
        }

        /// <summary>"Theme / Environment" (as ThemeEnvironment.from writes it).</summary>
        public static string PresetKey(TrackTheme theme, EnvironmentPreset p) => theme.name + " / " + p.name;

        public static EnvironmentPreset FindPreset(string key, IList<TrackTheme> officials)
        {
            if (string.IsNullOrEmpty(key) || officials == null) return null;
            foreach (var t in officials)
            {
                if (t == null || t.environments == null) continue;
                foreach (var p in t.environments)
                    if (p != null && string.Equals(PresetKey(t, p), key, StringComparison.OrdinalIgnoreCase)) return p;
            }
            return null;
        }

        static EnvironmentPreset FindPresetByName(TrackTheme theme, string name)
        {
            if (theme == null || theme.environments == null) return null;
            foreach (var p in theme.environments)
                if (p != null && string.Equals(p.name, name, StringComparison.OrdinalIgnoreCase)) return p;
            return theme.environments.Count > 0 ? theme.environments[0] : null;
        }

        /// <summary>
        /// A copy of <paramref name="source"/> with <paramref name="base64"/> as its texture (null = the source's
        /// texture) and <paramref name="glow"/> as its emission picture. No change at all = the source itself.
        /// </summary>
        static Material Retexture(Material source, string base64, Texture2D glow, Color glowColor, string name, bool always = false)
        {
            if (source == null) return null;
            var tex = Decode(base64, name);
            if (tex == null && glow == null && !always) return source;
            var m = new Material(source) { name = name + " (custom)" };
            making?.Add(m);
            if (tex != null)
            {
                var old = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
                // The tile atlases glow with their own picture (Hook Up Tile Types): the new picture glows the same way.
                if (glow == null && m.HasProperty("_EmissionMap") && old != null && m.GetTexture("_EmissionMap") == old)
                    m.SetTexture("_EmissionMap", tex);
            }
            if (glow != null && m.HasProperty("_EmissionMap"))
            {
                m.SetTexture("_EmissionMap", glow);
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", glowColor);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            return m;
        }

        static Texture2D Decode(string base64, string name)
        {
            var tex = CustomObstacles.DecodePng(base64, name);
            if (tex != null) making?.Add(tex);
            return tex;
        }

        // ---------------- Pictures ----------------

        /// <summary>The picture of a look in the theme picker: its background (a custom theme's Icon first).</summary>
        public static Texture Thumbnail(TrackTheme look) => look != null ? look.background : null;

        public static Texture Thumbnail(CustomTheme def, IList<TrackTheme> officials)
        {
            if (def == null) return null;
            if (icons.TryGetValue(def, out var t) && t != null) return t;
            t = CustomObstacles.DecodePng(!string.IsNullOrEmpty(def.icon) ? def.icon : def.background, def.name + " Icon");
            if (t != null)
            {
                icons[def] = t;
                return t;
            }
            return Thumbnail(Official(def.baseTheme, officials));
        }

        // ---------------- Materials (RTX ON custom themes) ----------------

        /// <summary>Materials an RTX ON custom theme can use: Resources/RTXOnMaterials, then every official theme's.</summary>
        public static List<Material> MaterialChoices(IList<TrackTheme> officials)
        {
            var list = new List<Material>();
            resourceMaterials ??= new List<Material>(Resources.LoadAll<Material>(MaterialsFolder));
            foreach (var m in resourceMaterials) if (m != null && !list.Exists(x => x.name == m.name)) list.Add(m);
            if (officials != null)
                foreach (var t in officials)
                {
                    if (t == null) continue;
                    foreach (var m in new[] { t.floor, t.floorSide, t.fragile, t.fragile1, t.fragile2, t.fragileChecker, t.fragileRow,
                                 t.fragileColumn, t.fragileSide, t.jump, t.mover, t.moverAuto, t.moverArrow })
                        if (m != null && !list.Exists(x => x.name == m.name)) list.Add(m);
                }
            return list;
        }

        public static Material FindMaterial(string name, IList<TrackTheme> officials)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (var m in MaterialChoices(officials))
                if (string.Equals(m.name, name, StringComparison.OrdinalIgnoreCase)) return m;
            return null;
        }

        // ---------------- Share files ----------------

        [Serializable]
        class FormatProbe
        {
            public string format;
            public string[] rows;
        }

        public static string[] ImportExtensions =>
            new[] { CustomObstacle.FileExtension.TrimStart('.'), ObjectPack.FileExtension.TrimStart('.'),
                    CustomTheme.FileExtension.TrimStart('.'), LevelPackage.FileExtension.TrimStart('.'), "json" };

        /// <summary>What an imported file held. <see cref="levelJson"/> is set for a full level (to open it).</summary>
        public class ImportResult
        {
            public string message;
            public string levelJson;
            public string previewJson; // its LevelPreview (a .levelrsrt can carry one), else null
            public string levelName;
            public readonly List<CustomObstacle> obstacles = new List<CustomObstacle>();
            public readonly List<CustomTheme> themes = new List<CustomTheme>();
        }

        /// <summary>
        /// Reads any share file (.rsrt object, .rsrtp object pack, .themert theme, .levelrsrt full level, or a plain
        /// level.json) and puts its objects and themes in the libraries. The extension does not matter: the
        /// "format" inside decides.
        /// </summary>
        public static ImportResult Import(string path)
        {
            var result = new ImportResult();
            string text;
            try { text = File.ReadAllText(path); }
            catch (Exception e)
            {
                result.message = $"{Path.GetFileName(path)} could not be read: {e.Message}";
                return result;
            }
            FormatProbe probe = null;
            try { probe = JsonUtility.FromJson<FormatProbe>(text); }
            catch (Exception) { }
            string format = probe != null ? probe.format ?? "" : "";
            string file = Path.GetFileName(path);

            if (format == CustomTheme.FormatName)
            {
                var def = CustomTheme.FromJson(text);
                if (def == null) { result.message = $"{file}: broken theme file."; return result; }
                def.imported = true;
                AddTheme(def, result);
                result.message = $"Imported theme {def.name}.";
            }
            else if (format == ObjectPack.FormatName)
            {
                ObjectPack pack = null;
                try { pack = JsonUtility.FromJson<ObjectPack>(text); }
                catch (Exception) { }
                if (pack == null || pack.objects == null) { result.message = $"{file}: broken object pack."; return result; }
                foreach (var o in pack.objects) AddObstacle(o, result);
                result.message = $"Imported object pack {pack.name}: {result.obstacles.Count} object(s).";
            }
            else if (format == LevelPackage.FormatName || (probe != null && probe.rows != null && probe.rows.Length > 0))
            {
                string levelText = text;
                string name = Path.GetFileNameWithoutExtension(path);
                if (format == LevelPackage.FormatName)
                {
                    LevelPackage package = null;
                    try { package = JsonUtility.FromJson<LevelPackage>(text); }
                    catch (Exception) { }
                    if (package == null || string.IsNullOrEmpty(package.level)) { result.message = $"{file}: broken level file."; return result; }
                    levelText = package.level;
                    name = package.name;
                    if (!string.IsNullOrEmpty(package.preview))
                    {
                        // The preview's own custom obstacles and themes go in the libraries too.
                        try
                        {
                            var preview = LevelData.ParseJson(package.preview);
                            foreach (var o in preview.Customs) AddObstacle(o, result, false);
                            foreach (var t in preview.CustomThemes)
                            {
                                t.imported = true;
                                AddTheme(t, result, false);
                            }
                            result.previewJson = package.preview;
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"RTXOn: the LevelPreview inside {file} could not be read: {e.Message}");
                        }
                    }
                }
                LevelData level;
                try { level = LevelData.ParseJson(levelText); }
                catch (Exception e) { result.message = $"{file}: {e.Message}"; return result; }
                foreach (var o in level.Customs) AddObstacle(o, result, false);
                foreach (var t in level.CustomThemes)
                {
                    t.imported = true;
                    AddTheme(t, result, false);
                }
                result.levelJson = levelText;
                result.levelName = string.IsNullOrEmpty(level.Name) ? name : level.Name;
                result.message = $"Imported level {result.levelName}" + (result.previewJson != null ? " + its LevelPreview" : "") +
                                 $" ({result.obstacles.Count} object(s), {result.themes.Count} theme(s)).";
            }
            else
            {
                var def = CustomObstacle.FromJson(text);
                if (def == null) { result.message = $"{file} is not a .rsrt, .rsrtp, .themert or .levelrsrt file."; return result; }
                AddObstacle(def, result);
                result.message = $"Imported object {def.name} (ID {def.id}).";
            }
            return result;
        }

        /// <summary>
        /// A theme into the library. A different theme with the same name: renamed "Name (2)" when it stands alone; a
        /// level's own theme keeps its name (the level uses it by name) and only goes in when the name is free.
        /// </summary>
        static void AddTheme(CustomTheme def, ImportResult result, bool rename = true)
        {
            var old = Get(def.name);
            if (old != null && old.ToJson(false) != def.ToJson(false))
            {
                if (!rename) return;
                string baseName = def.name;
                for (int i = 2; Has(def.name); i++) def.name = $"{baseName} ({i})";
            }
            Set(def);
            SaveToLibrary(def);
            result.themes.Add(def);
        }

        /// <summary>An object into the library, on the next free ID when its own is taken by a different one (not for a level's own).</summary>
        static void AddObstacle(CustomObstacle def, ImportResult result, bool moveId = true)
        {
            if (def == null) return;
            def.Normalize();
            var old = CustomObstacles.Get(def.id);
            if (old != null && !old.SameAs(def))
            {
                if (!moveId) return;
                int id = CustomObstacles.NextFreeId();
                if (id < 0) return;
                def.id = id;
            }
            CustomObstacles.Set(def);
            CustomObstacles.SaveToLibrary(def);
            result.obstacles.Add(def);
        }

        public static string PackText(string name, IEnumerable<CustomObstacle> objects)
        {
            var pack = new ObjectPack { name = name };
            pack.objects.AddRange(objects);
            return JsonUtility.ToJson(pack, true);
        }

        public static string LevelPackageText(string name, string levelJson, string previewJson = null) =>
            JsonUtility.ToJson(new LevelPackage { name = name, level = levelJson, preview = previewJson ?? "" }, false);
    }
}
