using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RTXOn.EditorTools
{
    /// <summary>
    /// "RTX ON > Build Main Menu Scene": creates Assets/RTXOn/Scenes/MainMenu.unity the first time (a camera and a
    /// Main Menu object) and opens it after that. Each time it fills the level list (Level1.json, Level2.json... in
    /// number order, then other level.json files of that folder) and the banners ("World N" textures and textures in a
    /// Banners folder), and puts the scene FIRST in the build list, so a built game opens on the Level List.
    /// </summary>
    public static class RTXMainMenuBuilder
    {
        const string ScenePath = "Assets/RTXOn/Scenes/MainMenu.unity";

        [MenuItem("RTX ON/Build Main Menu Scene", priority = 41)]
        public static void BuildMainMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("RTX ON", "Exit Play mode first, then run this again.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            UnityEngine.SceneManagement.Scene scene;
            if (File.Exists(ScenePath))
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                AssetDatabase.Refresh();
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            if (Object.FindFirstObjectByType<Camera>() == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.1f, 0.1f, 0.19f);
                camGo.AddComponent<AudioListener>();
            }
            var menu = Object.FindFirstObjectByType<MainMenu>();
            if (menu == null) menu = new GameObject("Main Menu").AddComponent<MainMenu>();
            Undo.RecordObject(menu, "Main Menu levels");
            menu.levels = FindLevels().ToArray();
            menu.levelPreviews = LevelFiles.FindProjectPreviews().ToArray(); // the 3D LevelPreviews shipped with the game
            menu.banners = FindBanners().ToArray();
            menu.worldMusic = FindWorldMusic();
            if (menu.homeBackground == null) menu.homeBackground = FindTexture("Background1");
            menu.themeBackgrounds = FindThemeBackgrounds();
            EditorUtility.SetDirty(menu);

            EditorSceneManager.SaveScene(scene, ScenePath);
            PutFirstInBuild(ScenePath);
            Debug.Log($"RTX ON: Main Menu built at {ScenePath} with {menu.levels.Length} levels, {menu.levelPreviews.Length} LevelPreviews and {menu.banners.Length} banners. " +
                      "It is first in the build scene list. Press Play to see the Level List.");
            if (!File.Exists("Assets/RTXOn/Scenes/Gameplay.unity"))
                Debug.LogWarning("RTX ON: Play needs the Gameplay scene: run RTX ON > Build Gameplay Scene.");
        }

        /// <summary>Every official theme (TrackTheme asset) with its background, for the level Detail.</summary>
        static MainMenu.ThemePicture[] FindThemeBackgrounds()
        {
            var list = new List<MainMenu.ThemePicture>();
            foreach (var guid in AssetDatabase.FindAssets("t:TrackTheme"))
            {
                var t = AssetDatabase.LoadAssetAtPath<TrackTheme>(AssetDatabase.GUIDToAssetPath(guid));
                if (t != null) list.Add(new MainMenu.ThemePicture { theme = t.name, background = t.background });
            }
            return list.ToArray();
        }

        /// <summary>The project's texture called <paramref name="name"/> (Home's background: Background1), or null.</summary>
        static Texture2D FindTexture(string name)
        {
            foreach (var guid in AssetDatabase.FindAssets(name + " t:Texture2D"))
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
                if (tex != null && tex.name == name) return tex;
            }
            return null;
        }

        /// <summary>Music1..5 (Massif, Sky, Forest, Desert, Volcano) for the menu's music, in that order (missing = empty).</summary>
        static AudioClip[] FindWorldMusic()
        {
            var songs = new AudioClip[5];
            foreach (var guid in AssetDatabase.FindAssets("Music t:AudioClip"))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
                if (clip == null) continue;
                var m = Regex.Match(clip.name, @"^Music(\d)(?!\d)", RegexOptions.IgnoreCase);
                if (!m.Success) continue;
                int i = int.Parse(m.Groups[1].Value) - 1;
                if (i >= 0 && i < songs.Length && songs[i] == null) songs[i] = clip;
            }
            return songs;
        }

        /// <summary>The level.json files next to Level1.json: Level1, Level2... in number order, then the others by name.</summary>
        public static List<TextAsset> FindLevels()
        {
            var found = LevelFiles.FindProjectLevels();
            if (found == null)
            {
                Debug.LogWarning("RTX ON: Level1.json not found: the Main Menu's level list is empty. Fill Main Menu > Levels by hand.");
                return new List<TextAsset>();
            }
            return found;
        }

        /// <summary>
        /// Banner pictures: textures named "World N..." (World 1 - Valea, World 2 - Aperta...) and every texture in a
        /// folder called Banners.
        /// </summary>
        public static List<Texture2D> FindBanners()
        {
            var world = new Regex(@"^\s*world\s*[-_ ]?\s*\d+", RegexOptions.IgnoreCase);
            var found = new List<Texture2D>();
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);
                bool inBanners = path.Replace('\\', '/').IndexOf("/Banners/", System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (!inBanners && !world.IsMatch(name)) continue;
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null && !found.Contains(tex)) found.Add(tex);
            }
            found.Sort((a, b) => EditorUtility.NaturalCompare(a.name, b.name));
            return found;
        }

        static void PutFirstInBuild(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == path);
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
