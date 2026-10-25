using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RTXOn.EditorTools
{
    /// <summary>
    /// "RTX ON > Open Level Editor Scene": creates Assets/RTXOn/Scenes/LevelEditor.unity the first time
    /// (a camera and a Level Editor object), and just opens it after that (putting the Level Editor object back if it
    /// was deleted). Each time it also fills the editor's looks (every TrackTheme) and
    /// song list (Music1..5 + CustomMusic folder), banners (for Level Settings) and level list (for Load) and puts the scene in the build list, so Play can switch scenes.
    /// The editor builds its UI in code at Play, so script updates never need the scene to be rebuilt.
    /// </summary>
    public static class RTXLevelEditorBuilder
    {
        const string ScenePath = "Assets/RTXOn/Scenes/LevelEditor.unity";

        [MenuItem("RTX ON/Open Level Editor Scene", priority = 40)]
        public static void OpenLevelEditor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("RTX ON", "Exit Play mode first, then run this again.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (File.Exists(ScenePath))
            {
                var opened = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var existing = Object.FindFirstObjectByType<LevelEditor>();
                if (existing != null)
                {
                    Undo.RecordObject(existing, "Level Editor songs");
                    FillMusic(existing);
                    FillLooks(existing);
                    existing.banners = RTXMainMenuBuilder.FindBanners().ToArray();
                    existing.levelFiles = RTXMainMenuBuilder.FindLevels().ToArray();
                    existing.previewFiles = LevelFiles.FindProjectPreviews().ToArray();
                    EditorUtility.SetDirty(existing);
                }
                else
                {
                    // The Level Editor object was deleted: put it (and the camera, if that is gone too) back.
                    if (Camera.main == null && Object.FindFirstObjectByType<Camera>() == null) MakeCamera();
                    MakeEditor();
                    Debug.Log("RTX ON: the Level Editor object was missing from " + ScenePath + " and has been added back.");
                }
                EditorSceneManager.SaveScene(opened);
                AddToBuildSettings(ScenePath);
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MakeCamera();
            MakeEditor();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            Debug.Log("RTX ON: Level Editor scene created at " + ScenePath + ". Press Play to edit the level.");
        }

        static void MakeCamera()
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.06f, 0.09f);
            camGo.AddComponent<AudioListener>(); // Play's music is silent without a listener
        }

        static void MakeEditor()
        {
            var editor = new GameObject("Level Editor").AddComponent<LevelEditor>();
            editor.levelFile = FindLevel1();
            FillMusic(editor);
            FillLooks(editor);
            editor.banners = RTXMainMenuBuilder.FindBanners().ToArray();
            editor.levelFiles = RTXMainMenuBuilder.FindLevels().ToArray();
            editor.previewFiles = LevelFiles.FindProjectPreviews().ToArray();
            if (editor.levelFile == null)
                Debug.LogWarning("RTX ON: Level1.json not found. The editor starts with a new level and Save creates NewLevel.json; set Level Editor > Level File to choose another level.");
        }

        /// <summary>
        /// The looks of the 3D view, the grid colours and the Look button: every TrackTheme in the project, and as
        /// default the Gameplay scene's Track theme (MassifTheme when that scene is not built yet).
        /// </summary>
        static void FillLooks(LevelEditor editor)
        {
            var all = new List<TrackTheme>();
            foreach (var guid in AssetDatabase.FindAssets("t:TrackTheme"))
            {
                var t = AssetDatabase.LoadAssetAtPath<TrackTheme>(AssetDatabase.GUIDToAssetPath(guid));
                if (t != null && !all.Contains(t)) all.Add(t);
            }
            all.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));
            editor.looks = all.ToArray();
            if (editor.theme == null)
                editor.theme = all.Find(t => t.name == "MassifTheme") ?? (all.Count > 0 ? all[0] : null);
        }

        /// <summary>The songs the editor's Music setting offers: Music1..5 and clips in a CustomMusic folder.</summary>
        static void FillMusic(LevelEditor editor)
        {
            var songs = RTXSceneBuilder.LevelMusicClips();
            songs.AddRange(RTXSceneBuilder.CustomMusicClips());
            editor.music = songs.ToArray();
            if (!File.Exists(GameplayScenePath))
                Debug.LogWarning("RTX ON: Play in the Level Editor needs the Gameplay scene: run RTX ON > Build Gameplay Scene.");
            else if (!EditorBuildSettings.scenes.Any(s => s.path == GameplayScenePath))
                Debug.LogWarning("RTX ON: add " + GameplayScenePath + " to the build scene list (Build Gameplay Scene does it) so Play can open it.");
        }

        const string GameplayScenePath = "Assets/RTXOn/Scenes/Gameplay.unity";

        static TextAsset FindLevel1()
        {
            foreach (var guid in AssetDatabase.FindAssets("Level1 t:TextAsset", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.Equals(Path.GetFileName(path), "Level1.json", System.StringComparison.OrdinalIgnoreCase))
                    return AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            }
            return null;
        }

        /// <summary>Adds the scene at the end of Build Settings, so the Gameplay scene stays first.</summary>
        static void AddToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == path)) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
