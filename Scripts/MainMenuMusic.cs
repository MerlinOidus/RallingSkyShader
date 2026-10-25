using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace RTXOn
{
    // The Main Menu's own music: a world song on loop (Settings > Menu Music, Massif by default), at the Settings
    // Music volume. It fades in when the menu opens and out when a level or the editor loads.
    public partial class MainMenu
    {
        [Header("Music")]
        [Tooltip("World songs for Settings > Menu Music: Massif, Sky, Forest, Desert, Volcano (Music1..5). Empty = the " +
                 "song file of that world in StreamingAssets/Music (Massif, Sky, Forest, Dessert, Volanco...).")]
        public AudioClip[] worldMusic = new AudioClip[5];
        [Range(0f, 1f)] public float menuMusicVolume = 0.8f;

        /// <summary>Settings > Menu Music choices, in GameSettings.MenuMusic order (the last one = no music).</summary>
        static readonly string[] MenuSongs = { "MASSIF", "SKY", "FOREST", "DESERT", "VOLCANO", "OFF" };

        AudioSource menuSource;
        float menuLevel;
        bool leavingMenu;

        void StartMenuMusic()
        {
            if (FindFirstObjectByType<AudioListener>() == null)
            {
                var cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
                if (cam != null) cam.gameObject.AddComponent<AudioListener>();
            }
            menuSource = gameObject.AddComponent<AudioSource>();
            menuSource.playOnAwake = false;
            menuSource.loop = true;
            menuSource.volume = 0f;
            UpdateMenuMusic();
        }

        AudioClip MenuClip(int index)
        {
            if (index < 0 || index >= 5) return null; // OFF
            if (worldMusic != null && index < worldMusic.Length && worldMusic[index] != null) return worldMusic[index];
            if (fileSongs[index] != null) return fileSongs[index];
            if (fileTried[index]) return null; // looked once already (this runs every frame)
            fileTried[index] = true;
#if UNITY_EDITOR
            // Main Menu scene built before the music field existed: find Music1..5 in the project.
            string name = "Music" + (index + 1);
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets(name + " t:AudioClip"))
            {
                var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                if (clip != null && clip.name.StartsWith(name, System.StringComparison.OrdinalIgnoreCase))
                {
                    if (worldMusic == null || worldMusic.Length < 5) System.Array.Resize(ref worldMusic, 5);
                    worldMusic[index] = clip;
                    return clip;
                }
            }
#endif
            // No clip: the world's song file in a Music folder (StreamingAssets/Music: Massif, Sky, Forest, Dessert,
            // Volanco...), loaded once in the background; the menu plays it as soon as it is there.
            string file = MusicLibrary.FindWorldFile(index);
            if (file != null) StartCoroutine(LoadSong(index, file));
            return null;
        }

        readonly AudioClip[] fileSongs = new AudioClip[5];
        readonly bool[] fileTried = new bool[5];

        IEnumerator LoadSong(int index, string path)
        {
            using (var req = UnityWebRequestMultimedia.GetAudioClip(MusicLibrary.Url(path), MusicLibrary.TypeOf(path)))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"RTXOn: menu music {path} could not be loaded: {req.error}");
                    yield break;
                }
                var clip = DownloadHandlerAudioClip.GetContent(req);
                clip.name = System.IO.Path.GetFileNameWithoutExtension(path);
                fileSongs[index] = clip;
            }
        }

        void UpdateMenuMusic()
        {
            if (menuSource == null) return;
            var clip = MenuClip(GameSettings.MenuMusic);
            if (clip != menuSource.clip)
            {
                menuSource.Stop();
                menuSource.clip = clip;
                menuLevel = 0f; // the new song fades in
                if (clip != null) menuSource.Play();
            }
            float target = leavingMenu ? 0f : 1f;
            menuLevel = Mathf.MoveTowards(menuLevel, target, Time.unscaledDeltaTime / (leavingMenu ? 0.4f : 1.2f));
            menuSource.volume = menuLevel * menuMusicVolume * GameSettings.MusicVolume;
        }
    }
}
