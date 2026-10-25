using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace RTXOn.EditorTools
{
    /// <summary>
    /// One-click setup: "RTX ON > Build Gameplay Scene".
    /// Creates textures, materials, theme, post-processing and the Gameplay scene.
    /// Existing materials/theme are kept so your tweaks survive a rebuild;
    /// "RTX ON > Reset Look" deletes the generated assets first.
    /// </summary>
    public static class RTXSceneBuilder
    {
        const string Root = "Assets/RTXOn";
        const string Gen = Root + "/Generated";
        const string ScenePath = Root + "/Scenes/Gameplay.unity";
        const string ThemePath = Root + "/MassifTheme.asset";
        const string LevelPath = Root + "/Levels/Level.txt";
        const int IgnoreRaycastLayer = 2;
        static readonly Color FogColor = new Color(0.74f, 0.86f, 0.98f);
        static readonly Quaternion SunRotation = Quaternion.Euler(42f, -35f, 0f);

        [MenuItem("RTX ON/Build Gameplay Scene", priority = 0)]
        public static void BuildScene()
        {
            if (CanRebuild()) BuildSceneCore(false);
        }

        [MenuItem("RTX ON/Reset Look (regenerate materials)", priority = 20)]
        public static void ResetLook()
        {
            if (!CanRebuild()) return;
            if (!EditorUtility.DisplayDialog("RTX ON",
                    "Delete Assets/RTXOn/Generated and rebuild all materials, textures, sky, post-processing " +
                    "and the Day/Sunset/Night presets?\n\nModel slots, props and the level choice are kept.", "Rebuild", "Cancel"))
                return;
            AssetDatabase.DeleteAsset(Gen);
            BuildSceneCore(true);
        }

        static bool CanRebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("RTX ON", "Exit Play mode first, then run this again.", "OK");
                return false;
            }
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset))
            {
                EditorUtility.DisplayDialog("RTX ON",
                    "This project is not using URP. Create the project from the 'Universal 3D' template, " +
                    "or assign a URP asset in Project Settings > Graphics, then run this again.", "OK");
                return false;
            }
            // The scene is replaced, so give the user the chance to save edits to the open one.
            return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        }

        static void BuildSceneCore(bool resetLook)
        {
            var kept = ReadExistingGame();

            Directory.CreateDirectory(Gen);
            Directory.CreateDirectory(Root + "/Scenes");
            AssetDatabase.Refresh();

            TunePipeline();
            var theme = BuildTheme(kept?.theme, resetLook);
            CollectModelAnimations(theme);
            var profile = BuildVolumeProfile();
            var sky = BuildSkybox();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildEnvironment(sky);
            var sun = BuildSun();
            RenderSettings.sun = sun;
            var volume = BuildVolume(profile);
            var ball = BuildBall(theme);
            if (kept?.character != null) EditorJsonUtility.FromJsonOverwrite(kept.character, ball.gameObject.AddComponent<BallCharacter>());
            var rig = BuildCamera(ball);
            BuildGame(theme, ball, rig, sun, volume, kept);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("RTX ON: Gameplay scene built at " + ScenePath + ". Press Play, tap/click to start, drag or A/D to steer.");
        }

        /// <summary>Settings of the Game object in the open (or saved) Gameplay scene, so a rebuild keeps them.</summary>
        class KeptGame
        {
            public TextAsset levelFile;
            public string layerName;
            public string levelTitle;
            public TrackTheme theme;
            public readonly List<ThemeChange> themeChanges = new List<ThemeChange>();
            /// <summary>The Ball's Ball Character (model, controller, fit), as JSON; null when the ball had none.</summary>
            public string character;
        }

        static KeptGame ReadExistingGame()
        {
            var gm = Object.FindFirstObjectByType<GameManager>();
            var opened = default(Scene);
            if (gm == null && File.Exists(ScenePath))
            {
                opened = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                foreach (var root in opened.GetRootGameObjects())
                {
                    gm = root.GetComponentInChildren<GameManager>(true);
                    if (gm != null) break;
                }
            }

            KeptGame kept = null;
            if (gm != null)
            {
                kept = new KeptGame { levelFile = gm.levelFile, layerName = gm.layerName, levelTitle = gm.levelTitle };
                if (gm.track != null && gm.track.theme != null && EditorUtility.IsPersistent(gm.track.theme))
                    kept.theme = gm.track.theme;
                if (gm.themeChanges != null)
                    foreach (var c in gm.themeChanges)
                        if (c != null) kept.themeChanges.Add(new ThemeChange { row = c.row, preset = c.preset, rows = c.rows });
                foreach (var root in gm.gameObject.scene.GetRootGameObjects())
                {
                    var character = root.GetComponentInChildren<BallCharacter>(true);
                    if (character == null) continue;
                    kept.character = EditorJsonUtility.ToJson(character);
                    break;
                }
            }
            if (opened.IsValid()) EditorSceneManager.CloseScene(opened, true);
            return kept;
        }

        // ---------------- Pipeline ----------------

        static void TunePipeline()
        {
            var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            var so = new SerializedObject(asset);
            SetBool(so, "m_SupportsHDR", true);
            SetBool(so, "m_MainLightShadowsSupported", true);
            SetBool(so, "m_SoftShadowsSupported", true);
            SetFloat(so, "m_ShadowDistance", 45f);
            SetInt(so, "m_ShadowCascadeCount", 2);
            SetBool(so, "m_RequireDepthTexture", true);
            SetBool(so, "m_RequireOpaqueTexture", true); // the glass (RTXOn/Glass) refracts what is behind it
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);

            // SSAO is what gives the tiles contact shadows. The URP templates ship with it; warn if it is missing.
            bool hasSsao = false;
            var list = so.FindProperty("m_RendererDataList");
            if (list != null)
            {
                for (int i = 0; i < list.arraySize; i++)
                {
                    var data = list.GetArrayElementAtIndex(i).objectReferenceValue as ScriptableRendererData;
                    if (data == null) continue;
                    foreach (var f in data.rendererFeatures)
                        if (f != null && f.GetType().Name.Contains("AmbientOcclusion")) hasSsao = true;
                }
            }
            if (!hasSsao)
                Debug.LogWarning("RTX ON: no Screen Space Ambient Occlusion on the URP renderer '" + asset.name +
                                 "'. Select its Renderer asset > Add Renderer Feature > Screen Space Ambient Occlusion for the full look.");
        }

        static void SetBool(SerializedObject so, string name, bool v) { var p = so.FindProperty(name); if (p != null) p.boolValue = v; }
        static void SetFloat(SerializedObject so, string name, float v) { var p = so.FindProperty(name); if (p != null) p.floatValue = v; }
        static void SetInt(SerializedObject so, string name, int v) { var p = so.FindProperty(name); if (p != null) p.intValue = v; }

        // ---------------- Textures ----------------

        enum TexKind { FloorAlbedo, EdgeGlow, FragileAlbedo, FragileGlow, JumpGlow }

        /// <summary>Textures whose drawing changed since they were first made: redrawn in place (materials keep them).</summary>
        static readonly TexKind[] Redraw = { TexKind.JumpGlow };

        static Texture2D GetTexture(string name, TexKind kind)
        {
            string path = $"{Gen}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null && (System.Array.IndexOf(Redraw, kind) < 0 || !existing.isReadable)) return existing;

            const int S = 256;
            var tex = existing != null ? existing : new Texture2D(S, S, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Clamp, anisoLevel = 8 };
            if (tex.width != S || tex.height != S) return existing;
            var px = new Color[S * S];
            var rng = new System.Random(1234 + (int)kind);
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float u = (x + 0.5f) / S, v = (y + 0.5f) / S;
                float d = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v)); // distance to tile edge
                float noise = 0.95f + 0.05f * (float)rng.NextDouble();
                float c = 1f;
                switch (kind)
                {
                    case TexKind.FloorAlbedo:
                        c = noise;
                        if (d < 0.075f) c *= 0.82f;                               // bevel
                        else if (Mathf.Abs(d - 0.1f) < 0.008f) c *= 0.88f;        // inlay groove
                        if (Mathf.Abs(d - 0.3f) < 0.004f) c *= 0.94f;             // inner panel
                        break;
                    case TexKind.FragileAlbedo:
                        int cx = Mathf.FloorToInt(u * 4f), cy = Mathf.FloorToInt(v * 4f);
                        c = ((cx + cy) % 2 == 0 ? 1f : 0.9f) * noise;
                        if (d < 0.075f) c *= 0.82f;
                        float gu = Mathf.Abs(u * 4f - Mathf.Round(u * 4f)), gv = Mathf.Abs(v * 4f - Mathf.Round(v * 4f));
                        if (Mathf.Min(gu, gv) < 0.03f) c *= 0.7f;                 // cracks between cells
                        break;
                    case TexKind.FragileGlow:
                    {
                        // Hazard stripes round the edge (like the original's warning tiles) and glowing cracks inside.
                        float band = (d > 0.03f && d < 0.13f) ? 1f : 0f;
                        float stripe = Mathf.Repeat((u + v) * 7f, 1f) < 0.5f ? 1f : 0f;
                        float fu = Mathf.Abs(u * 4f - Mathf.Round(u * 4f)), fv = Mathf.Abs(v * 4f - Mathf.Round(v * 4f));
                        float crack = d > 0.13f && Mathf.Min(fu, fv) < 0.025f ? 0.6f : 0f;
                        c = Mathf.Max(band * stripe, crack);
                        break;
                    }
                    case TexKind.EdgeGlow:
                        c = Mathf.Min(1f, Mathf.Exp(-Mathf.Pow((d - 0.1f) / 0.022f, 2f)) + 0.4f * Mathf.Exp(-Mathf.Pow((d - 0.1f) / 0.06f, 2f)));
                        break;
                    case TexKind.JumpGlow:
                    {
                        // A box: the glowing edge, a second square inside it and a soft glow in the middle.
                        float outer = Mathf.Exp(-Mathf.Pow((d - 0.1f) / 0.022f, 2f));
                        float inner = Mathf.Exp(-Mathf.Pow((d - 0.24f) / 0.018f, 2f));
                        float core = d > 0.3f ? 0.25f : 0f;
                        c = Mathf.Min(1f, Mathf.Max(outer, Mathf.Max(inner, core)));
                        break;
                    }
                }
                px[y * S + x] = new Color(c, c, c, 1f);
            }
            tex.SetPixels(px);
            tex.Apply(true);
            if (existing != null) EditorUtility.SetDirty(tex);
            else AssetDatabase.CreateAsset(tex, path);
            return tex;
        }

        // ---------------- Materials ----------------

        static Material GetLit(string name, Color baseColor, float smooth, float metal, Texture albedo = null, Texture emissionMap = null, Color? emission = null)
        {
            string path = $"{Gen}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, enableInstancing = true };
            m.SetColor("_BaseColor", baseColor);
            if (albedo != null) m.SetTexture("_BaseMap", albedo);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                if (emissionMap != null) m.SetTexture("_EmissionMap", emissionMap);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            AssetDatabase.CreateAsset(m, path);
            if (emission.HasValue)
            {
                // URP's material import can recompute keywords; make sure emission stays on.
                m.EnableKeyword("_EMISSION");
                EditorUtility.SetDirty(m);
            }
            return m;
        }

        struct FragileVariant
        {
            public FragileKind kind;
            public string name;
            public Color baseColor, glow;
            public FragileVariant(FragileKind kind, string name, Color baseColor, Color glow)
            {
                this.kind = kind; this.name = name; this.baseColor = baseColor; this.glow = glow;
            }
        }

        /// <summary>Generated materials of the fragile IDs 4-8 (Fragile 0 = M_Fragile, orange glow).</summary>
        static readonly FragileVariant[] FragileVariants =
        {
            new FragileVariant(FragileKind.Fragile1, "M_Fragile1", new Color(0.16f, 0.24f, 0.36f), new Color(0.2f, 0.65f, 1f) * 3.5f),        // blue
            new FragileVariant(FragileKind.Fragile2, "M_Fragile2", new Color(0.17f, 0.32f, 0.2f), new Color(0.3f, 1f, 0.35f) * 3.5f),         // green
            new FragileVariant(FragileKind.Checker, "M_FragileChecker", new Color(0.36f, 0.18f, 0.32f), new Color(1f, 0.3f, 0.8f) * 3.5f),   // pink
            new FragileVariant(FragileKind.Row, "M_FragileRow", new Color(0.36f, 0.31f, 0.13f), new Color(1f, 0.85f, 0.15f) * 3.5f),          // yellow
            new FragileVariant(FragileKind.Column, "M_FragileColumn", new Color(0.25f, 0.18f, 0.38f), new Color(0.65f, 0.3f, 1f) * 3.5f),    // violet
        };

        static Material GetFragileSlot(TrackTheme t, FragileKind kind) => kind switch
        {
            FragileKind.Fragile1 => t.fragile1,
            FragileKind.Fragile2 => t.fragile2,
            FragileKind.Checker => t.fragileChecker,
            FragileKind.Row => t.fragileRow,
            FragileKind.Column => t.fragileColumn,
            _ => t.fragile,
        };

        static void SetFragileSlot(TrackTheme t, FragileKind kind, Material m)
        {
            switch (kind)
            {
                case FragileKind.Fragile1: t.fragile1 = m; break;
                case FragileKind.Fragile2: t.fragile2 = m; break;
                case FragileKind.Checker: t.fragileChecker = m; break;
                case FragileKind.Row: t.fragileRow = m; break;
                case FragileKind.Column: t.fragileColumn = m; break;
                default: t.fragile = m; break;
            }
        }

        /// <summary>Gives a generated material (only that one, never your own) its Base Map if it lost it.</summary>
        static void EnsureBaseMap(Material m, string generatedName, Texture map)
        {
            if (m == null || map == null || m.name != generatedName || !AssetDatabase.GetAssetPath(m).StartsWith(Gen)) return;
            if (!m.HasProperty("_BaseMap") || m.GetTexture("_BaseMap") != null) return;
            m.SetTexture("_BaseMap", map);
            EditorUtility.SetDirty(m);
        }

        /// <summary>Gives a generated material (only that one, never your own) its glow map and colour if they are missing.</summary>
        static void EnsureGlow(Material m, string generatedName, Texture map, Color color)
        {
            if (m == null || m.name != generatedName || !AssetDatabase.GetAssetPath(m).StartsWith(Gen)) return;
            if (!m.HasProperty("_EmissionMap")) return;
            bool changed = false;
            if (m.GetTexture("_EmissionMap") == null) { m.SetTexture("_EmissionMap", map); changed = true; }
            if (m.GetColor("_EmissionColor").maxColorComponent < 0.01f) { m.SetColor("_EmissionColor", color); changed = true; }
            if (!m.IsKeywordEnabled("_EMISSION")) { m.EnableKeyword("_EMISSION"); changed = true; }
            if (!changed) return;
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(m);
        }

        static Material GetBeam(string name, Color color)
        {
            string path = $"{Gen}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var shader = Shader.Find("RTXOn/Beam");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            var m = new Material(shader) { name = name };
            m.SetColor("_Color", color);
            m.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <param name="existing">Theme the open scene already uses, if any; otherwise MassifTheme.</param>
        /// <param name="reset">Reassign every material and preset. Otherwise only empty slots are filled, so your own materials stay.</param>
        static TrackTheme BuildTheme(TrackTheme existing, bool reset)
        {
            var floorTex = GetTexture("T_FloorAlbedo", TexKind.FloorAlbedo);
            var fragileTex = GetTexture("T_FragileAlbedo", TexKind.FragileAlbedo);
            var edgeTex = GetTexture("T_EdgeGlow", TexKind.EdgeGlow);
            var fragileGlowTex = GetTexture("T_FragileGlow", TexKind.FragileGlow);
            var jumpTex = GetTexture("T_JumpGlow", TexKind.JumpGlow);

            var theme = existing != null ? existing : AssetDatabase.LoadAssetAtPath<TrackTheme>(ThemePath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<TrackTheme>();
                AssetDatabase.CreateAsset(theme, ThemePath);
            }
            Material Fill(Material current, System.Func<Material> make) => reset || current == null ? make() : current;

            // Massif: icy white stone, cyan neon inlays, warm sun. Fragile floor is dark with orange hazard stripes, so it never looks like normal floor.
            theme.floor = Fill(theme.floor, () => GetLit("M_Floor", new Color(0.86f, 0.93f, 1f), 0.78f, 0.05f, floorTex, edgeTex, new Color(0.25f, 0.8f, 1f) * 3.2f));
            theme.fragile = Fill(theme.fragile, () => GetLit("M_Fragile", new Color(0.32f, 0.2f, 0.17f), 0.55f, 0.05f, fragileTex, fragileGlowTex, new Color(1f, 0.42f, 0.06f) * 3.5f));
            // Each fragile ID its own colour (base colour + glow colour), same maps, so the sections read apart like the original.
            foreach (var v in FragileVariants)
            {
                var variant = v;
                var current = GetFragileSlot(theme, variant.kind);
                SetFragileSlot(theme, variant.kind, Fill(current, () => GetLit(variant.name, variant.baseColor, 0.55f, 0.05f, fragileTex, fragileGlowTex, variant.glow)));
            }
            theme.jump = Fill(theme.jump, () => GetLit("M_Jump", new Color(1f, 0.88f, 0.62f), 0.8f, 0.1f, floorTex, jumpTex, new Color(1f, 0.45f, 0.08f) * 3.5f));
            theme.hazard = Fill(theme.hazard, () => GetLit("M_Hazard", new Color(0.12f, 0.14f, 0.2f), 0.85f, 0.6f, null, edgeTex, new Color(1f, 0.12f, 0.18f) * 4.5f));
            theme.hazardSafe = Fill(theme.hazardSafe, () => GetLit("M_HazardSafe", new Color(0.55f, 0.64f, 0.78f), 0.7f, 0.2f, floorTex));
            theme.gem = Fill(theme.gem, () => GetLit("M_Gem", new Color(0.35f, 0.9f, 1f), 0.95f, 0.5f, null, null, new Color(0.2f, 0.75f, 1f) * 3f));
            theme.star = Fill(theme.star, () => GetLit("M_Star", new Color(1f, 0.82f, 0.25f), 0.95f, 0.8f, null, null, new Color(1f, 0.65f, 0.1f) * 3f));
            theme.trunk = Fill(theme.trunk, () => GetLit("M_Trunk", new Color(0.36f, 0.27f, 0.22f), 0.3f, 0f));
            theme.foliage = Fill(theme.foliage, () => GetLit("M_Foliage", new Color(0.28f, 0.66f, 0.62f), 0.55f, 0f));
            theme.ball = Fill(theme.ball, () => GetLit("M_Ball", new Color(0.96f, 0.97f, 1f), 0.96f, 1f));
            theme.beamRed = Fill(theme.beamRed, () => GetBeam("M_BeamRed", new Color(4f, 0.35f, 0.25f)));
            theme.beamWhite = Fill(theme.beamWhite, () => GetBeam("M_BeamWhite", new Color(1.6f, 1.55f, 1.3f)));
            // Moving platforms: violet = touch mover (Mover Sentuh), green = auto mover, white arrow on the trigger tile.
            theme.mover = Fill(theme.mover, () => GetLit("M_Mover", new Color(0.88f, 0.84f, 1f), 0.78f, 0.05f, floorTex, edgeTex, new Color(0.75f, 0.3f, 1f) * 3.2f));
            theme.moverAuto = Fill(theme.moverAuto, () => GetLit("M_MoverAuto", new Color(0.84f, 1f, 0.88f), 0.78f, 0.05f, floorTex, edgeTex, new Color(0.25f, 1f, 0.45f) * 3.2f));
            theme.moverArrow = Fill(theme.moverArrow, () => GetLit("M_MoverArrow", Color.white, 0.5f, 0f, null, null, new Color(1f, 0.97f, 0.9f) * 4f));

            // Glow maps on the generated materials (the fragile cracks lost theirs in some projects). The environment
            // keeps them dark by day and lights them at night.
            EnsureGlow(theme.floor, "M_Floor", edgeTex, new Color(0.25f, 0.8f, 1f) * 3.2f);
            EnsureGlow(theme.fragile, "M_Fragile", fragileGlowTex, new Color(1f, 0.42f, 0.06f) * 3.5f);
            foreach (var v in FragileVariants) EnsureGlow(GetFragileSlot(theme, v.kind), v.name, fragileGlowTex, v.glow);
            // Base maps too (Base Map + Emission Map, like M_Floor), so a colour change shows on the pattern.
            EnsureBaseMap(theme.floor, "M_Floor", floorTex);
            EnsureBaseMap(theme.fragile, "M_Fragile", fragileTex);
            foreach (var v in FragileVariants) EnsureBaseMap(GetFragileSlot(theme, v.kind), v.name, fragileTex);
            EnsureBaseMap(theme.jump, "M_Jump", floorTex);
            EnsureBaseMap(theme.mover, "M_Mover", floorTex);
            EnsureBaseMap(theme.moverAuto, "M_MoverAuto", floorTex);
            EnsureGlow(theme.mover, "M_Mover", edgeTex, new Color(0.75f, 0.3f, 1f) * 3.2f);
            EnsureGlow(theme.moverAuto, "M_MoverAuto", edgeTex, new Color(0.25f, 1f, 0.45f) * 3.2f);
            EnsureGlow(theme.jump, "M_Jump", jumpTex, new Color(1f, 0.45f, 0.08f) * 3.5f);

            if (reset || theme.environments == null || theme.environments.Count == 0)
                theme.environments = new List<EnvironmentPreset> { EnvironmentPreset.Day(), EnvironmentPreset.Sunset(), EnvironmentPreset.Night() };
            if (theme.autoModelMaterial && theme.modelMaterial == null)
                theme.modelMaterial = BuildModelAtlasMaterial();
            EditorUtility.SetDirty(theme);
            return theme;
        }

        /// <summary>
        /// URP Lit material using the Rolling Sky enemy atlas (a texture with "Enemy" in its name),
        /// so custom hazard models and props that were UV-mapped onto that atlas look right.
        /// Prefers an "Indo…" atlas over the stock EnemyTexture when both are in the project.
        /// </summary>
        static Material BuildModelAtlasMaterial()
        {
            string path = $"{Gen}/M_ModelAtlas.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var candidates = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" }))
            {
                var texPath = AssetDatabase.GUIDToAssetPath(guid);
                if (texPath.StartsWith(Gen + "/")) continue;
                if (Path.GetFileNameWithoutExtension(texPath).IndexOf("Enemy", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                candidates.Add(texPath);
            }
            if (candidates.Count == 0) return null;

            int Rank(string p)
            {
                var n = Path.GetFileNameWithoutExtension(p);
                if (n.IndexOf("Indo", System.StringComparison.OrdinalIgnoreCase) >= 0) return 0;
                return string.Equals(n, "EnemyTexture", System.StringComparison.OrdinalIgnoreCase) ? 2 : 1;
            }
            candidates.Sort((a, b) =>
            {
                int r = Rank(a).CompareTo(Rank(b));
                return r != 0 ? r : string.CompareOrdinal(a, b);
            });
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(candidates[0]);
            if (atlas == null) return null;

            if (candidates.Count > 1)
                Debug.LogWarning("RTX ON: several *Enemy* textures found (" + string.Join(", ", candidates) + "). Using '" + candidates[0] +
                                 "'. To use another one, drag it onto Base Map of " + path + ".");
            else
                Debug.Log("RTX ON: custom models will use the atlas '" + candidates[0] + "' (theme > Model Material).");
            return GetLit("M_ModelAtlas", Color.white, 0.55f, 0.05f, atlas);
        }

        static Material BuildSkybox()
        {
            string path = $"{Gen}/M_SkyGradient.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var shader = Shader.Find("RTXOn/SkyGradient");
            if (shader == null) shader = Shader.Find("Skybox/Procedural");
            var m = new Material(shader) { name = "M_SkyGradient" };
            m.SetColor("_TopColor", new Color(0.2f, 0.45f, 0.88f));
            m.SetColor("_HorizonColor", new Color(0.82f, 0.92f, 1f));
            m.SetColor("_BottomColor", FogColor);
            m.SetVector("_SunDir", -(SunRotation * Vector3.forward));
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // ---------------- Post-processing ----------------

        static VolumeProfile BuildVolumeProfile()
        {
            string path = $"{Gen}/RTX_PostProfile.asset";
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (existing != null) return existing;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.9f);
            bloom.scatter.Override(0.72f);
            bloom.highQualityFiltering.Override(true);
            bloom.clamp.Override(12f);

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);

            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.2f);
            color.contrast.Override(16f);
            color.saturation.Override(12f);

            var wb = profile.Add<WhiteBalance>(true);
            wb.temperature.Override(-6f);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.26f);
            vignette.smoothness.Override(0.45f);

            var ca = profile.Add<ChromaticAberration>(true);
            ca.intensity.Override(0.06f);

            foreach (var c in profile.components)
            {
                c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(c, profile);
            }
            EditorUtility.SetDirty(profile);
            return profile;
        }

        // ---------------- Scene objects ----------------

        static void BuildEnvironment(Material sky)
        {
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.76f, 0.95f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.64f, 0.75f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.34f, 0.42f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.018f;
            RenderSettings.fogColor = FogColor;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        }

        static Light BuildSun()
        {
            var go = new GameObject("Sun");
            go.transform.rotation = SunRotation;
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = new Color(1f, 0.95f, 0.86f);
            l.intensity = 1.6f;
            l.shadows = LightShadows.Soft;
            l.shadowStrength = 0.85f;
            return l;
        }

        static Volume BuildVolume(VolumeProfile profile)
        {
            var go = new GameObject("Global Volume (RTX ON)");
            var v = go.AddComponent<Volume>();
            v.isGlobal = true;
            v.sharedProfile = profile;
            return v;
        }

        static BallController BuildBall(TrackTheme theme)
        {
            var root = new GameObject("Ball");
            var ball = root.AddComponent<BallController>();
            root.transform.position = new Vector3(0f, ball.radius, 0f);

            var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "Visual";
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = Vector3.one * ball.radius * 2f;
            visual.layer = IgnoreRaycastLayer; // hidden from its own reflection probe
            var mr = visual.GetComponent<MeshRenderer>();
            mr.sharedMaterial = theme.ball;
            mr.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            ball.visual = visual.transform;

            // Realtime reflection probe that rides with the ball: the chrome ball reflects the actual track, which is
            // most of the "RTX" feel. Its box holds only the ball (GraphicsQuality keeps it so in old scenes too).
            var probeGo = new GameObject("Reflection Probe (live)");
            probeGo.transform.SetParent(root.transform, false);
            var probe = probeGo.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.EveryFrame;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.resolution = 128;
            probe.size = Vector3.one * 0.6f;
            probe.center = Vector3.zero;
            probe.blendDistance = 0f;
            probe.cullingMask = ~(1 << IgnoreRaycastLayer);
            probe.hdr = true;
            probe.importance = 10;
            return ball;
        }

        static CameraRig BuildCamera(BallController ball)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 220f;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.allowHDR = true;
            go.AddComponent<AudioListener>();
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.renderShadows = true;
            data.stopNaN = true;

            var rig = go.AddComponent<CameraRig>();
            rig.target = ball;
            rig.Snap();
            return rig;
        }

        static void BuildGame(TrackTheme theme, BallController ball, CameraRig rig, Light sun, Volume volume, KeptGame kept)
        {
            var go = new GameObject("Game");
            var gm = go.AddComponent<GameManager>();
            var track = new GameObject("Track").AddComponent<TrackBuilder>();
            track.transform.SetParent(go.transform, false);
            track.theme = theme;

            if (kept != null)
            {
                gm.levelFile = kept.levelFile;
                if (!string.IsNullOrEmpty(kept.layerName)) gm.layerName = kept.layerName;
                if (!string.IsNullOrEmpty(kept.levelTitle)) gm.levelTitle = kept.levelTitle;
            }
            if (gm.levelFile == null) gm.levelFile = FindLevelFile();
            if (gm.levelFile == null)
                Debug.LogError("RTX ON: no Level.txt or Level1.json found. Put one anywhere under Assets and assign it on Game > Level File.");
            int levelNumber = LayerNumber(gm.layerName, 1);
            WorldSpeed(levelNumber, out gm.speed, out _, out _); // Level.txt speed; a level.json sets its own
            gm.looks = FindAllThemes();
            gm.ball = ball;
            gm.track = track;
            gm.cameraRig = rig;

            gm.audioPlayer = go.AddComponent<GameAudio>();
            AssignAudio(gm.audioPlayer);

            gm.environment = go.AddComponent<EnvironmentDirector>();
            gm.environment.sun = sun;
            gm.environment.volume = volume;
            if (kept != null && kept.themeChanges.Count > 0)
            {
                gm.themeChanges = kept.themeChanges;
                return;
            }
            int rows = 518;
            if (gm.levelFile != null && !gm.levelFile.text.TrimStart().StartsWith("{"))
            {
                try { rows = LevelData.Parse(gm.levelFile.text, gm.layerName).RowCount; }
                catch (System.Exception e) { Debug.LogWarning("RTX ON: " + e.Message); }
            }
            gm.themeChanges = DefaultThemes(levelNumber, rows);
        }

        /// <summary>The number in a layer name ("Level 3" → 3), or <paramref name="fallback"/>.</summary>
        static int LayerNumber(string layerName, int fallback)
        {
            var digits = System.Text.RegularExpressions.Regex.Match(layerName ?? "", @"\d+");
            return digits.Success && int.TryParse(digits.Value, out int n) ? n : fallback;
        }

        /// <summary>Level 1 goes Day → Sunset → Night; other levels stay in Day until you add changes.</summary>
        static List<ThemeChange> DefaultThemes(int levelNumber, int rowCount)
        {
            var list = new List<ThemeChange> { new ThemeChange { row = 0, preset = "Day", rows = 0f } };
            if (levelNumber == 1)
            {
                list.Add(new ThemeChange { row = Mathf.RoundToInt(rowCount * 0.42f), preset = "Sunset", rows = 40f });
                list.Add(new ThemeChange { row = Mathf.RoundToInt(rowCount * 0.72f), preset = "Night", rows = 40f });
            }
            return list;
        }

        // ---------------- Levels: Level.txt → level.json ----------------

        /// <summary>Speeds of worlds 1-5 in the original game (LevelDesigner.worldData), in rows per second.</summary>
        static void WorldSpeed(int world, out float start, out float end, out int rampRows)
        {
            // velocity % of 3.75..8, max velocity %, share of the level used to reach it, goal length
            float[,] table =
            {
                { 0.525f, 0.525f, 1f, 500f },
                { 0.625f, 0.625f, 1f, 650f },
                { 0.725f, 0.8f, 1f, 805f },
                { 0.8f, 1f, 1f, 970f },
                { 0.85f, 1f, 0.5f, 1170f },
            };
            int i = Mathf.Clamp(world - 1, 0, 4);
            start = Mathf.Lerp(3.75f, 8f, table[i, 0]);
            end = Mathf.Lerp(3.75f, 8f, table[i, 1]);
            rampRows = Mathf.Approximately(start, end) ? 0 : Mathf.RoundToInt(table[i, 2] * table[i, 3]);
            if (rampRows == 0) end = 0f;
        }

        [MenuItem("RTX ON/Convert Level.txt to level.json", priority = 30)]
        public static void ConvertLevels()
        {
            // Only a Tiled Flare export has "type=" layers; anything else selected (a script, a json) falls back to Level.txt.
            var src = Selection.activeObject as TextAsset;
            if (src == null || LevelData.LayerNames(src.text).Count == 0) src = FindLevelFile(false);
            if (src == null)
            {
                EditorUtility.DisplayDialog("RTX ON", "No Level.txt found. Select a Level.txt (Tiled Flare export) in the Project window first.", "OK");
                return;
            }

            string srcPath = AssetDatabase.GetAssetPath(src);
            string dir = Path.GetDirectoryName(srcPath).Replace('\\', '/');
            var written = new List<string>();
            var kept = new List<string>();
            var layers = LevelData.LayerNames(src.text);
            for (int i = 0; i < layers.Count; i++)
            {
                LevelData data;
                try { data = LevelData.Parse(src.text, layers[i]); }
                catch (System.Exception e) { Debug.LogWarning("RTX ON: skipped layer '" + layers[i] + "': " + e.Message); continue; }

                int n = LayerNumber(layers[i], i + 1);
                string outPath = $"{dir}/Level{n}.json";
                string name = n == 1 ? "Massif" : layers[i];
                string music = "Music" + n;
                WorldSpeed(n, out float start, out float end, out int ramp);
                IList<ThemeChange> themes = DefaultThemes(n, data.RowCount);
                IList<PropPlacement> props = null;

                // Re-converting only replaces the rows: name, music, speed, themes and props you wrote by hand stay.
                if (File.Exists(outPath))
                {
                    try
                    {
                        var old = LevelData.ParseJson(File.ReadAllText(outPath));
                        name = old.Name;
                        data.Look = old.Look;
                        if (!string.IsNullOrEmpty(old.Music)) music = old.Music;
                        start = old.SpeedStart;
                        end = old.SpeedEnd;
                        ramp = old.SpeedRampRows;
                        if (old.ThemeChanges.Count > 0) themes = old.ThemeChanges;
                        props = old.Props;
                        kept.Add(Path.GetFileName(outPath));
                    }
                    catch (System.Exception e)
                    {
                        if (!EditorUtility.DisplayDialog("RTX ON", $"{outPath} could not be read:\n{e.Message}\n\nOverwrite it with a fresh conversion?", "Overwrite", "Skip"))
                            continue;
                    }
                }

                File.WriteAllText(outPath, data.ToJson(name, music, start, end, ramp, themes, props));
                written.Add(outPath);
            }
            AssetDatabase.Refresh();

            if (written.Count == 0)
            {
                EditorUtility.DisplayDialog("RTX ON", "Nothing was converted from " + srcPath + ". See the Console for the reason.", "OK");
                return;
            }
            Debug.Log("RTX ON: wrote " + string.Join(", ", written) +
                      (kept.Count > 0 ? ". Kept name, music, speed, themes and props of " + string.Join(", ", kept) + "." : "."));
            var first = AssetDatabase.LoadAssetAtPath<TextAsset>(written[0]);
            Selection.activeObject = first;

            var gm = Object.FindFirstObjectByType<GameManager>();
            if (gm != null && first != null &&
                EditorUtility.DisplayDialog("RTX ON", $"Use {Path.GetFileName(written[0])} in the open scene?", "Use it", "Not now"))
            {
                Undo.RecordObject(gm, "Use level.json");
                gm.levelFile = first;
                EditorUtility.SetDirty(gm);
                EditorSceneManager.MarkSceneDirty(gm.gameObject.scene);
            }
        }

        // ---------------- Models ----------------

        /// <summary>The theme the open scene's track uses, else MassifTheme, else any theme.</summary>
        static TrackTheme FindTheme()
        {
            var tb = Object.FindFirstObjectByType<TrackBuilder>();
            if (tb != null && tb.theme != null && EditorUtility.IsPersistent(tb.theme)) return tb.theme;
            var theme = AssetDatabase.LoadAssetAtPath<TrackTheme>(ThemePath);
            if (theme != null) return theme;
            foreach (var guid in AssetDatabase.FindAssets("t:TrackTheme"))
            {
                theme = AssetDatabase.LoadAssetAtPath<TrackTheme>(AssetDatabase.GUIDToAssetPath(guid));
                if (theme != null) return theme;
            }
            return null;
        }

        [MenuItem("RTX ON/Add Selected Models As Props", priority = 31)]
        public static void AddSelectedProps()
        {
            var theme = FindTheme();
            if (theme == null)
            {
                EditorUtility.DisplayDialog("RTX ON", "Run RTX ON > Build Gameplay Scene first (it creates the theme).", "OK");
                return;
            }
            Undo.RecordObject(theme, "Add Props");
            var added = new List<string>();
            foreach (var go in Selection.GetFiltered<GameObject>(SelectionMode.Assets))
            {
                var row = theme.AddEntry(go); // its row in Models, Prop ticked
                if (row.prop) continue;
                row.prop = true;
                if (row.animation.clip == null) row.animation.clip = TrackTheme.FirstClip(go);
                added.Add(go.name);
            }
            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            Debug.Log(added.Count > 0
                ? "RTX ON: added props " + string.Join(", ", added) + " to " + AssetDatabase.GetAssetPath(theme) +
                  ". Place them with the Level Editor's Props tab (or in level.json: { \"row\": 40, \"x\": -4, \"model\": \"" + added[0] + "\" })."
                : "RTX ON: no new models selected (select FBX/prefab files in the Project window).");
        }

        /// <summary>Every TrackTheme asset in the project, for GameManager.looks.</summary>
        static List<TrackTheme> FindAllThemes()
        {
            var list = new List<TrackTheme>();
            foreach (var guid in AssetDatabase.FindAssets("t:TrackTheme"))
            {
                var t = AssetDatabase.LoadAssetAtPath<TrackTheme>(AssetDatabase.GUIDToAssetPath(guid));
                if (t != null && !list.Contains(t)) list.Add(t);
            }
            return list;
        }

        [MenuItem("RTX ON/New Theme From Current", priority = 33)]
        public static void NewThemeFromCurrent()
        {
            var source = FindTheme();
            if (source == null)
            {
                EditorUtility.DisplayDialog("RTX ON", "Run RTX ON > Build Gameplay Scene first (it creates the theme).", "OK");
                return;
            }
            string path = EditorUtility.SaveFilePanelInProject("New theme", "IndonesiaTheme", "asset",
                "Name the new theme (e.g. SkyTheme, ForestTheme, DesertTheme, VolcanoTheme, IndonesiaTheme).", Root);
            if (string.IsNullOrEmpty(path)) return;

            // Own copies of the materials next to the theme, so changing its colours never changes the other themes.
            string folder = Path.GetDirectoryName(path).Replace('\\', '/') + "/" + Path.GetFileNameWithoutExtension(path) + " Materials";
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            var copy = Object.Instantiate(source);
            foreach (var f in typeof(TrackTheme).GetFields())
            {
                if (f.FieldType != typeof(Material) || !(f.GetValue(copy) is Material m) || m == null) continue;
                string matPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{m.name}.mat");
                var mc = new Material(m) { name = Path.GetFileNameWithoutExtension(matPath) };
                AssetDatabase.CreateAsset(mc, matPath);
                f.SetValue(copy, mc);
            }
            AssetDatabase.CreateAsset(copy, path);

            var gm = Object.FindFirstObjectByType<GameManager>();
            if (gm != null)
            {
                Undo.RecordObject(gm, "New Theme");
                gm.looks = FindAllThemes();
                EditorSceneManager.MarkSceneDirty(gm.gameObject.scene);
            }
            AssetDatabase.SaveAssets();
            Selection.activeObject = copy;
            Debug.Log($"RTX ON: created {path}. Change its models, materials and Environments, then pick it in a level.json with \"look\": \"{copy.name}\".");
        }

        [MenuItem("RTX ON/Find Model Animations", priority = 32)]
        public static void FindModelAnimations()
        {
            var theme = FindTheme();
            if (theme == null)
            {
                EditorUtility.DisplayDialog("RTX ON", "Run RTX ON > Build Gameplay Scene first (it creates the theme).", "OK");
                return;
            }
            Undo.RecordObject(theme, "Find Model Animations");
            int added = CollectModelAnimations(theme);
            AssetDatabase.SaveAssets();
            Selection.activeObject = theme;
            Debug.Log(added > 0
                ? $"RTX ON: found {added} new animation(s). See Models > Animation in {AssetDatabase.GetAssetPath(theme)} (Loop or Approach)."
                : "RTX ON: no new animations. Check the FBX: Animation tab > Import Animation, and the clip has keyframes.");
        }

        /// <summary>
        /// Adds every animation clip inside the theme's models (model slots and props) to their Models rows (Animation > Clip).
        /// Entries that are already there keep their settings. Returns how many were added.
        /// </summary>
        static int CollectModelAnimations(TrackTheme theme)
        {
            var models = new List<GameObject>();
            foreach (var f in typeof(TrackTheme).GetFields())
                if (f.FieldType == typeof(GameObject) && f.GetValue(theme) is GameObject go && go != null && !models.Contains(go)) models.Add(go);
            foreach (var go in theme.PropModels())
                if (!models.Contains(go)) models.Add(go);

            int added = 0;
            foreach (var go in models)
            {
                string path = AssetDatabase.GetAssetPath(go);
                if (string.IsNullOrEmpty(path)) continue;
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    var clip = asset as AnimationClip;
                    if (clip == null || clip.name.StartsWith("__preview__")) continue;
                    // One clip per model (its Models row); a model that has one keeps it.
                    var row = theme.Entry(go);
                    if (row != null && row.animation.clip != null) break;
                    row = theme.AddEntry(go);
                    row.animation.clip = clip;
                    row.animation.enabled = true;
                    added++;
                    break;
                }
            }
            if (added > 0) EditorUtility.SetDirty(theme);
            return added;
        }

        [MenuItem("RTX ON/Add Selected Models As Props", true)]
        static bool AddSelectedPropsValidate() => Selection.GetFiltered<GameObject>(SelectionMode.Assets).Length > 0;

        [MenuItem("RTX ON/Hook Up Audio (open scene)", priority = 10)]
        public static void HookUpAudio()
        {
            var gm = Object.FindFirstObjectByType<GameManager>();
            if (gm == null)
            {
                EditorUtility.DisplayDialog("RTX ON", "Open the Gameplay scene first (or run Build Gameplay Scene).", "OK");
                return;
            }
            Undo.RecordObject(gm, "Hook Up Audio");
            if (gm.audioPlayer == null) gm.audioPlayer = Undo.AddComponent<GameAudio>(gm.gameObject);
            Undo.RecordObject(gm.audioPlayer, "Hook Up Audio");
            AssignAudio(gm.audioPlayer);
            // Speed of this layer's world in the original game, so its music lines up (World 1 = 5.98 rows/s).
            WorldSpeed(LayerNumber(gm.layerName, 1), out gm.speed, out _, out _);
            EditorUtility.SetDirty(gm);
            EditorUtility.SetDirty(gm.audioPlayer);
            EditorSceneManager.MarkSceneDirty(gm.gameObject.scene);
        }

        /// <summary>Every AudioClip under Assets.</summary>
        static System.Collections.Generic.List<AudioClip> AllClips()
        {
            var clips = new System.Collections.Generic.List<AudioClip>();
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip"))
            {
                var c = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
                if (c != null) clips.Add(c);
            }
            return clips;
        }

        /// <summary>Your own songs: every AudioClip in a folder called CustomMusic (anywhere under Assets).</summary>
        public static System.Collections.Generic.List<AudioClip> CustomMusicClips()
        {
            var list = new System.Collections.Generic.List<AudioClip>();
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Replace('\\', '/').IndexOf("/CustomMusic/", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                var c = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (c != null) list.Add(c);
            }
            list.Sort((x, y) => string.Compare(x.name, y.name, System.StringComparison.OrdinalIgnoreCase));
            return list;
        }

        /// <summary>Music1..5 from the original game (missing ones are skipped).</summary>
        public static System.Collections.Generic.List<AudioClip> LevelMusicClips()
        {
            var clips = AllClips();
            var list = new System.Collections.Generic.List<AudioClip>();
            for (int i = 1; i <= 5; i++)
            {
                var c = clips.Find(x => x.name.StartsWith("Music" + i, System.StringComparison.OrdinalIgnoreCase));
                if (c != null) list.Add(c);
            }
            return list;
        }

        /// <summary>Finds the old Rolling Sky clips anywhere under Assets by their file names.</summary>
        static void AssignAudio(GameAudio a)
        {
            var clips = AllClips();
            AudioClip Find(string prefix) => clips.Find(c => c.name.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase));

            a.jump = Find("02_Jump");
            a.crash = Find("03_Crash");
            a.pickup = Find("04_Pickup");
            a.fragile = Find("05_Fragile");
            a.crusher = Find("06_Boom");
            a.woosh = Find("07_Slasher");
            a.laser = Find("08_Laser");
            a.explosion = Find("09_Explosion") ?? a.explosion;
            a.riser = Find("10_Movement1");
            a.slide = Find("11_Movement2");
            // A clip named Checkpoint... / Crown...; none = keep the clip already set (else a built-in chime).
            a.checkpoint = Find("Checkpoint") ?? a.checkpoint;
            a.crown = Find("Crown") ?? a.crown;
            // Power-up clips by name (PowerUp..., SpeedUp..., SpeedDown..., Switch..., Invisible...); none = keep what is set.
            a.powerUp = Find("PowerUp") ?? Find("Power Up") ?? Find("Power_Up") ?? a.powerUp;
            a.speedUp = Find("SpeedUp") ?? Find("Speed Up") ?? Find("Speed_Up") ?? a.speedUp;
            a.speedDown = Find("SpeedDown") ?? Find("Speed Down") ?? Find("Speed_Down") ?? a.speedDown;
            a.switchLanes = Find("Switch") ?? a.switchLanes;
            a.invisible = Find("Invisible") ?? a.invisible;
            a.customMusic = CustomMusicClips();
            a.levelMusic = new AudioClip[5];
            int music = 0;
            for (int i = 0; i < a.levelMusic.Length; i++)
            {
                a.levelMusic[i] = Find("Music" + (i + 1));
                if (a.levelMusic[i] != null) music++;
            }

            if (clips.Count == 0)
                Debug.LogWarning("RTX ON: no AudioClips found under Assets. Copy the AudioClip folder in, then run RTX ON > Hook Up Audio.");
            else
                Debug.Log($"RTX ON: audio hooked up ({music} music tracks, {a.customMusic.Count} custom songs, jump={a.jump != null}, crash={a.crash != null}, gem={a.pickup != null}, checkpoint sound={(a.checkpoint != null ? a.checkpoint.name : "built-in chime")}, crown sound={(a.crown != null ? a.crown.name : "built-in chime")}).");
        }

        /// <summary>Level.txt from the package wherever its folder was dropped, else Level1.json (if allowed).</summary>
        static TextAsset FindLevelFile(bool allowJson = true)
        {
            var direct = AssetDatabase.LoadAssetAtPath<TextAsset>(LevelPath);
            if (direct != null) return direct;
            string json = null;
            foreach (var guid in AssetDatabase.FindAssets("Level t:TextAsset", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var file = Path.GetFileName(path);
                if (string.Equals(file, "Level.txt", System.StringComparison.OrdinalIgnoreCase))
                    return AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (allowJson && json == null && string.Equals(file, "Level1.json", System.StringComparison.OrdinalIgnoreCase))
                    json = path;
            }
            return json != null ? AssetDatabase.LoadAssetAtPath<TextAsset>(json) : null;
        }

        static void AddToBuildSettings(string path)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == path);
            // First in the list = the scene a built game opens with; the Main Menu keeps that place once it exists.
            int at = scenes.Count > 0 && scenes[0].path.EndsWith("/MainMenu.unity") ? 1 : 0;
            scenes.Insert(at, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
