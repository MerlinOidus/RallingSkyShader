using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace RTXOn
{
    /// <summary>
    /// Main Menu, rebuilt from Adrian's design (rolling-sky-ui-unity.md + uicoba.pdf, 2026-10-24):
    /// Home (logo over Background1, "tap to play"), Level lists (Official / Custom: cards with best %, gems, crowns),
    /// Detail (the level's banner: tap it to play), Settings (a sheet from the bottom) and a navbar with a jelly
    /// indicator. Colours come from four colour sets (Settings > Menu Color), text in Indonesian or English.
    /// The phone held upright gets a 1080 x 1920 canvas, held sideways a 1920 x 1080 one with its own layout; turning
    /// the phone builds the menu again on the same screen.
    /// The levels come from <see cref="levels"/> in order (1, 2, 3...), then levels saved in the game's save folder.
    /// The UI is built in code at Play, like the level editor.
    /// </summary>
    public partial class MainMenu : MonoBehaviour
    {
        [Header("Home")]
        [Tooltip("The logo on Home. Empty = UI/Resources/RTXOnLogo.png.")]
        public Texture2D logo;
        [Tooltip("Under the logo.")]
        public string subtitle = "REMASTERED";
        [Tooltip("Behind Home and the level Detail. Build Main Menu Scene sets Background1. Empty = the first banner.")]
        public Texture2D homeBackground;

        [Tooltip("Each official theme's background picture, behind the level Detail of a level with that theme. " +
                 "Build Main Menu Scene fills it (the Unity Editor also finds new themes by itself).")]
        public ThemePicture[] themeBackgrounds;

        [System.Serializable]
        public class ThemePicture
        {
            public string theme;
            public Texture background;
        }

        [Header("Fonts")]
        [Tooltip("Menu text (Selawik is a free look-alike of Segoe UI). Empty = Unity's built-in font.")]
        public Font menuFont;
        [Tooltip("Big numbers (100%, 20/20). Empty = Menu Font.")]
        public Font numberFont;

        [Header("Levels")]
        [Tooltip("The levels in list order: number 1 first. Build Main Menu Scene fills it with Level1.json, Level2.json...")]
        public TextAsset[] levels;
        [Tooltip("Also list levels saved in the save folder (persistentDataPath/Levels) that are not in Levels.")]
        public bool includeSavedLevels = true;

        [Header("LevelPreview (3D)")]
        [Tooltip("The LevelPreviews shipped with the game (Previews/LevelPreview1.json...). Build Main Menu Scene fills it; " +
                 "the Unity Editor also finds them by itself, and a built game reads the ones its Level Editor saved.")]
        public TextAsset[] levelPreviews;
        [Tooltip("The look of a level preview without a look of its own. Empty = Massif.")]
        public TrackTheme previewTheme;
        [Tooltip("PC: the level Detail shows the level's LevelPreview in 3D instead of its banner.")]
        public bool preview3DOnPc = true;
        [Tooltip("The phone level Detail (Android, or a PC window held upright) shows the 3D LevelPreview over its banner. Off = the banner (Adrian).")]
        public bool preview3DOnAndroid;

        [Header("Banners")]
        [Tooltip("Pictures a level can use as banner (level.json \"banner\"). A level without one uses the texture " +
                 "named \"World N...\" for its number N (World 1 - Valea, World 2 - Aperta...). Build Main Menu Scene " +
                 "fills it with every \"World N\" texture and the textures in a Banners folder.")]
        public Texture2D[] banners;

        [Header("Scenes")]
        public string gameplayScene = "Gameplay";
        public string editorScene = "LevelEditor";

        class Entry
        {
            public LevelData level;
            public string json;
            public string key;
            public TextAsset asset; // null for a level that only exists in the save folder
            public string path;     // that level's file
            public int number;      // in the whole list: the World N banner and MusicN song
            public int shown;       // in its list (1 = first)
            public bool official;   // Level1 .. Level5
            public Texture2D banner;
        }

        const int OfficialTab = 0, CustomTab = 1;

        /// <summary>Every level: official ones first, then the custom ones.</summary>
        readonly List<Entry> allEntries = new List<Entry>();
        /// <summary>The levels the open list shows (Official or Custom).</summary>
        readonly List<Entry> entries = new List<Entry>();
        int listTab;

        /// <summary>Play or Edit was pressed: the menu opens on the level list when it comes back.</summary>
        static bool backToLevels;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetMenuStatics() => backToLevels = false;

        /// <summary>The next time the menu opens it shows Home, not the level list (Close on the Pause screen).</summary>
        public static void OpenOnHome() => backToLevels = false;

        /// <summary>The next time the menu opens it shows the level list (Levels on the Pause screen).</summary>
        public static void OpenOnLevels() => backToLevels = true;

        void Awake()
        {
            ScreenRotation.Free(); // the menu turns with the phone (portrait or landscape)
            EnsureCameraAndEventSystem();
            LevelSelect.SetMenu(SceneManager.GetActiveScene().name);
            MenuStyle.font = menuFont;
            MenuStyle.numberFont = numberFont;
            LoadEntries();
            // Back from a level or the editor: the list of the level chosen last time (Official or Custom).
            int remembered = LevelSelect.Selected;
            var last = remembered >= 0 && remembered < allEntries.Count ? allEntries[remembered] : null;
            listTab = last != null && !last.official ? CustomTab : OfficialTab;
            screen = backToLevels ? MenuScreen.Levels : MenuScreen.Home;
            scrollTo = backToLevels ? last : null;
            backToLevels = false;
            BuildUi(ScreenRotation.Portrait);
            ShowScreen(screen, true);
            StartMenuMusic();
            if (allEntries.Count == 0)
                Status("No levels: set Main Menu > Levels (RTX ON > Build Main Menu Scene fills it).");
            else if (!Application.CanStreamedLevelBeLoaded(gameplayScene))
                Status($"Scene \"{gameplayScene}\" is not in the build scene list: run RTX ON > Build Gameplay Scene.");
        }

        void OnDestroy()
        {
            MenuStyle.ForgetTints();
            if (previewStage != null) Destroy(previewStage.gameObject);
        }

        // ---------------- LevelPreview (3D) ----------------

        LevelPreviewStage previewStage;

        bool Preview3D => GraphicsQuality.Mobile ? preview3DOnAndroid : preview3DOnPc;

        /// <summary>The 3D LevelPreview of <paramref name="e"/> on the stage (made the first time), or null when 3D previews are off.</summary>
        LevelPreviewStage ShowPreview(Entry e, Vector2 size)
        {
            if (!Preview3D || e == null) return null;
            if (previewStage == null)
            {
                float scale = GraphicsQuality.Mobile ? 1f : 1.5f;
                previewStage = LevelPreviewStage.Create(size * scale, previewTheme);
            }
            string levelPath = e.path ?? (e.asset != null ? LevelFiles.SavePath(e.asset) : null);
            previewStage.Show(LevelPreviews.Load(e.key, levelPath, e.json, levelPreviews));
            return previewStage;
        }

        void HidePreview()
        {
            if (previewStage != null) previewStage.Show(null);
        }

        // ---------------- Levels ----------------

        void LoadEntries()
        {
            allEntries.Clear();
            entries.Clear();
            var used = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            var list = new List<TextAsset>();
            if (levels != null) list.AddRange(levels);
#if UNITY_EDITOR
            // Levels made since Build Main Menu Scene (the level editor's New+) are listed too.
            var project = LevelFiles.FindProjectLevels();
            if (project != null)
                foreach (var asset in project)
                    if (!list.Contains(asset)) list.Add(asset);
#endif
            foreach (var asset in list)
            {
                if (asset == null) continue;
                used.Add(asset.name);
                // In a built game the level editor saves its changes in the save folder: play that copy
                // (not for the official levels 1-5, which a built game can't edit).
                string text = asset.text;
#if !UNITY_EDITOR
                string saved = Path.Combine(Application.persistentDataPath, "Levels", asset.name + ".json");
                if (!LevelFiles.IsOfficial(asset.name) && File.Exists(saved)) text = File.ReadAllText(saved);
#endif
                Add(text, asset.name, asset);
            }

#if !UNITY_EDITOR
            // Levels made in the built game's level editor (in the Unity Editor they are assets, listed in Levels).
            string folder = Path.Combine(Application.persistentDataPath, "Levels");
            if (includeSavedLevels && Directory.Exists(folder))
            {
                var files = new List<string>(Directory.GetFiles(folder, "*.json"));
                files.Sort(System.StringComparer.OrdinalIgnoreCase);
                foreach (var f in files)
                {
                    string key = Path.GetFileNameWithoutExtension(f);
                    if (used.Contains(key)) continue;
                    Add(File.ReadAllText(f), key, null, f);
                }
            }
#endif
        }

        void Add(string text, string key, TextAsset asset, string path = null)
        {
            if (string.IsNullOrEmpty(text) || !text.TrimStart().StartsWith("{")) return; // an old Level.txt is not a level.json
            try
            {
                var level = LevelData.ParseJson(text);
                int number = allEntries.Count + 1;
                allEntries.Add(new Entry
                {
                    level = level,
                    json = text,
                    key = key,
                    asset = asset,
                    path = path,
                    number = number,
                    official = asset != null && LevelFiles.IsOfficial(key),
                    banner = BannerLibrary.Get(level.Banner, number, banners),
                });
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"RTXOn: level {key} could not be read and is left out of the list: {e.Message}");
            }
        }

        /// <summary>The open list's levels: Official (1-5) or Custom (the others), numbered from 1.</summary>
        void FilterEntries()
        {
            entries.Clear();
            bool official = listTab == OfficialTab;
            foreach (var e in allEntries)
                if (e.official == official)
                {
                    entries.Add(e);
                    e.shown = entries.Count;
                }
        }

        /// <summary>Edit opens it: a custom level with a file (official levels 1-5 can't be edited).</summary>
        static bool CanEdit(Entry e) => e != null && !e.official && (e.asset != null || e.path != null);

        // ---------------- Theme backgrounds (level Detail) ----------------

        readonly Dictionary<string, Texture> lookPictures = new Dictionary<string, Texture>(System.StringComparer.OrdinalIgnoreCase);
        List<ThemePicture> themePictures;

        /// <summary>
        /// The picture behind the Detail of <paramref name="e"/> (Adrian: every theme has its own): the background of
        /// the level's theme (an official theme, or a custom one's own picture, else its base theme's). Home's
        /// picture when the level has no theme or hides its background.
        /// </summary>
        Texture DetailBackground(Entry e)
        {
            var level = e?.level;
            if (level == null || level.ThemeSettings != null && level.ThemeSettings.hideBackground) return HomeBackground;
            string look = level.Look ?? "";
            int bar = look.IndexOf('|'); // "Look|Objects": the picture belongs to the look
            if (bar >= 0) look = look.Substring(0, bar);
            if (look.Length == 0) return HomeBackground;
            string cacheKey = (e.key ?? "") + "/" + look; // a level can carry its own custom theme of that name
            if (!lookPictures.TryGetValue(cacheKey, out var tex))
            {
                ThemeLibrary.LoadBundled(); // the custom themes shipped with the game (Levels/Themes)
                tex = lookPictures[cacheKey] = LookPicture(look, level, 0);
            }
            return tex != null ? tex : HomeBackground;
        }

        Texture LookPicture(string look, LevelData level, int depth)
        {
            if (string.IsNullOrEmpty(look) || depth > 4) return null;
            var official = OfficialPicture(look, out bool known);
            if (known) return official;
            var def = level.FindCustomTheme(look) ?? ThemeLibrary.Get(look);
            if (def == null) return null;
            if (!string.IsNullOrEmpty(def.background))
            {
                var own = CustomObstacles.DecodePng(def.background, def.name + " Background");
                if (own != null)
                {
                    own.wrapMode = TextureWrapMode.Clamp;
                    return own;
                }
            }
            return LookPicture(def.baseTheme, level, depth + 1);
        }

        /// <summary>An official theme's background, named like the game does (Sky = SkyTheme).</summary>
        Texture OfficialPicture(string name, out bool known)
        {
            if (themePictures == null)
            {
                themePictures = new List<ThemePicture>();
                if (themeBackgrounds != null)
                    foreach (var p in themeBackgrounds)
                        if (p != null && !string.IsNullOrEmpty(p.theme)) themePictures.Add(p);
                // Themes made after the scene was built (a small list: names and pictures, not the whole themes).
                foreach (var p in ThemeRegistry.Pictures())
                    if (!themePictures.Exists(q => q.theme == p.name))
                        themePictures.Add(new ThemePicture { theme = p.name, background = p.background });
#if UNITY_EDITOR
                foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:TrackTheme"))
                {
                    var t = UnityEditor.AssetDatabase.LoadAssetAtPath<TrackTheme>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                    if (t != null && !themePictures.Exists(p => p.theme == t.name))
                        themePictures.Add(new ThemePicture { theme = t.name, background = t.background });
                }
#endif
            }
            foreach (var p in themePictures)
                if (string.Equals(p.theme, name, System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.theme, name + "Theme", System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.theme, name + " Theme", System.StringComparison.OrdinalIgnoreCase))
                {
                    known = true;
                    return p.background;
                }
            known = false;
            return null;
        }

        /// <summary>The picture behind Home and Detail.</summary>
        Texture2D HomeBackground
        {
            get
            {
                if (homeBackground != null) return homeBackground;
#if UNITY_EDITOR
                // Main Menu scene built before this field existed: find Background1 in the project.
                foreach (var guid in UnityEditor.AssetDatabase.FindAssets("Background1 t:Texture2D"))
                {
                    var tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                    if (tex != null && tex.name == "Background1") return homeBackground = tex;
                }
#endif
                if (banners != null)
                    foreach (var b in banners)
                        if (b != null) return b;
                return null;
            }
        }

        // ---------------- Leaving the menu ----------------

        void Play(Entry e)
        {
            if (e == null) return;
            if (!Application.CanStreamedLevelBeLoaded(gameplayScene))
            {
                Status($"Scene \"{gameplayScene}\" is not in the build scene list: run RTX ON > Build Gameplay Scene.");
                return;
            }
            LevelSelect.Selected = allEntries.IndexOf(e);
            LevelSelect.Begin(e.json, e.key, e.number, SceneManager.GetActiveScene().name);
            backToLevels = true;
            leavingMenu = true;
            LoadingScreen.Load(gameplayScene);
        }

        void Edit(Entry e)
        {
            if (!CanEdit(e)) return;
            if (!Application.CanStreamedLevelBeLoaded(editorScene))
            {
                Status($"Scene \"{editorScene}\" is not in the build scene list: run RTX ON > Open Level Editor Scene.");
                return;
            }
            LevelSelect.Selected = allEntries.IndexOf(e);
            LevelSelect.SetMenu(SceneManager.GetActiveScene().name);
            LevelSelect.EditLevel = e.asset;
            LevelSelect.EditPath = e.asset == null ? e.path : null;
            backToLevels = true;
            leavingMenu = true;
            LoadingScreen.Load(editorScene);
        }

        /// <summary>The Level Editor on its own level (no level from the list).</summary>
        void OpenEditor()
        {
            if (!Application.CanStreamedLevelBeLoaded(editorScene))
            {
                Status($"Scene \"{editorScene}\" is not in the build scene list: run RTX ON > Open Level Editor Scene.");
                return;
            }
            LevelSelect.SetMenu(SceneManager.GetActiveScene().name);
            LevelSelect.EditLevel = null;
            LevelSelect.EditPath = null;
            backToLevels = true;
            leavingMenu = true;
            LoadingScreen.Load(editorScene);
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void Status(string message)
        {
            MenuToast.Show(message);
            Debug.LogWarning("RTXOn: " + message);
        }

        // ---------------- Every frame ----------------

        void Update()
        {
            // The phone was turned (or the Game view resized): the menu is built again for the new shape.
            if (ScreenRotation.Portrait != builtPortrait || Mathf.Abs(CanvasSize(builtPortrait).x - builtSize.x) > 4f) Rebuild();
            UpdateMenuMusic();
            AnimateParticles();
            if (UpdateListening()) return; // Settings > Controller is waiting for a button
            if (Time.unscaledTime < ignoreInputUntil || busy) return;

            if (settingsOpen)
            {
                if (GameInput.BackPressed(true)) CloseSettings();
                else KeepSelectedVisible();
                return;
            }
            int step = TabStep();
            if (step != 0 && screen == MenuScreen.PcLevels) PcSwitchList();
            else if (step != 0 && screen != MenuScreen.Detail) StepNavbar(step);
            switch (screen)
            {
                case MenuScreen.Home:
                    if (GameInput.ConfirmPressed()) OpenLevels(OfficialTab);
                    // Android has no X on Home: its Back button leaves the game, like other phone games.
                    else if (Application.platform == RuntimePlatform.Android && GameInput.BackPressed(true)) Quit();
                    break;
                case MenuScreen.Levels:
                    if (GameInput.BackPressed(true)) { GoHome(); return; }
                    if (cards.Count == 0) return;
                    if (GameInput.MenuDown()) MoveCursor(columns);
                    if (GameInput.MenuUp()) MoveCursor(-columns);
                    if (columns > 1 && GameInput.MenuRight()) MoveCursor(1);
                    if (columns > 1 && GameInput.MenuLeft()) MoveCursor(-1);
                    if (GameInput.ConfirmPressed() && cursor >= 0) OpenDetail(cards[cursor], false);
                    break;
                case MenuScreen.Detail:
                    if (GameInput.BackPressed(true)) CloseDetail();
                    else if (GameInput.ConfirmPressed()) PlayFromBanner();
                    break;
                case MenuScreen.PcLevels:
                    PcUpdate();
                    break;
            }
        }

        /// <summary>The navbar's next screen: RB / R1 or Tab = +1 (right), LB / L1 or Shift+Tab = -1 (left), else 0.</summary>
        static int TabStep()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k != null && k.tabKey.wasPressedThisFrame) return k.shiftKey.isPressed ? -1 : 1;
            var g = Gamepad.current;
            if (g == null) return 0;
            if (g.rightShoulder.wasPressedThisFrame) return 1;
            return g.leftShoulder.wasPressedThisFrame ? -1 : 0;
#else
            if (Input.GetKeyDown(KeyCode.Tab)) return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? -1 : 1;
            if (Input.GetKeyDown(KeyCode.JoystickButton5)) return 1;
            return Input.GetKeyDown(KeyCode.JoystickButton4) ? -1 : 0;
#endif
        }

        static void EnsureCameraAndEventSystem()
        {
            if (FindFirstObjectByType<Camera>() == null)
            {
                var cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.gameObject.AddComponent<AudioListener>();
            }
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
                go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
#else
                go.AddComponent<StandaloneInputModule>();
#endif
            }
        }
    }
}
