using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace RTXOn
{
    public enum Sfx { Jump, Crash, Pickup, Fragile, Crusher, Riser, Slide, Woosh, Laser, Checkpoint, Crown, PowerUp, SpeedUp, SpeedDown, Switch, Invisible, Explosion }

    /// <summary>
    /// Music per level plus one-shot sound effects with a small voice pool.
    /// "RTX ON > Build Gameplay Scene" fills the clips from the AudioClip folder by name.
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        public static GameAudio Instance { get; private set; }

        [Header("Music (Music1 = Level 1, Music2 = Level 2, ...)")]
        public AudioClip[] levelMusic;
        [Tooltip("Your own songs. A level picks one by name (level.json \"music\", or Music in the level editor). " +
                 "Songs can also be audio files (.ogg .mp3 .wav) in the Music folder: " +
                 "StreamingAssets/Music, or the game's save folder /Music on phone and PC.")]
        public List<AudioClip> customMusic = new List<AudioClip>();
        [Range(0f, 1f)] public float musicVolume = 0.8f;

        [Header("Effects")]
        [Range(0f, 1f)] public float sfxVolume = 1f;
        public AudioClip jump;
        public AudioClip crash;
        public AudioClip pickup;
        public AudioClip fragile;
        public AudioClip crusher;
        public AudioClip riser;
        public AudioClip slide;
        public AudioClip woosh;
        public AudioClip laser;
        [Tooltip("Blast right after the laser fires (original 09_Explosion). Empty = no blast sound.")]
        public AudioClip explosion;
        [Tooltip("Passing a checkpoint. Empty = a built-in rising chime.")]
        public AudioClip checkpoint;
        [Tooltip("Taking a crown. Empty = a built-in bright two-note chime.")]
        public AudioClip crown;

        [Header("Power-ups (empty = Power Up sound, or a built-in chime)")]
        [Tooltip("Taking a power-up: Fly Jump, Short Jump, Bounce, and any power-up below that has no sound of its own.")]
        public AudioClip powerUp;
        [Tooltip("Crossing a Speed Up line.")]
        public AudioClip speedUp;
        [Tooltip("Crossing a Speed Down line.")]
        public AudioClip speedDown;
        [Tooltip("Crossing a Switch line (lanes flip left-right).")]
        public AudioClip switchLanes;
        [Tooltip("Taking an Invisible power-up.")]
        public AudioClip invisible;

        const int Voices = 10;
        const float MinRepeat = 0.08f; // same sound can't stack faster than this

        AudioSource music;
        AudioSource[] voices;
        int nextVoice;
        readonly float[] lastPlayed = new float[System.Enum.GetValues(typeof(Sfx)).Length];
        float fadeSpeed;

        void Awake()
        {
            Instance = this;
            music = gameObject.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.loop = false;
            voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                voices[i] = gameObject.AddComponent<AudioSource>();
                voices[i].playOnAwake = false;
            }
            for (int i = 0; i < lastPlayed.Length; i++) lastPlayed[i] = -1f;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>The music's volume: Music Volume here times Settings > Music.</summary>
        float MusicLevel => musicVolume * GameSettings.MusicVolume;

        void Update()
        {
            if (fadeSpeed > 0f && music.isPlaying)
            {
                music.volume = Mathf.MoveTowards(music.volume, 0f, fadeSpeed * Time.unscaledDeltaTime);
                if (music.volume <= 0f) music.Stop();
            }
            else if (fadeSpeed <= 0f) music.volume = MusicLevel; // follows the Settings slider at once
            KeepSeek();
        }

        // ---- Exact start point (Start Pos, checkpoints) ----

        float seekTarget = -1f;  // song second wanted when the song started
        float seekStartedAt;     // Time.unscaledTime when it started

        /// <summary>Song position in seconds, read from the sample counter (exact, unlike AudioSource.time on compressed songs).</summary>
        public float MusicTime => music != null && music.clip != null ? music.timeSamples / (float)music.clip.frequency : 0f;

        /// <summary>The song is playing and already at its start point, so the game can follow its clock.</summary>
        public bool MusicReady => music != null && music.clip != null && music.isPlaying && loading == null && seekTarget < 0f;

        /// <summary>
        /// A seek made while the song's data is still loading is lost and the song plays from 0. Until the song is
        /// where it should be (start point + time since it started), keep putting it there.
        /// </summary>
        void KeepSeek()
        {
            if (seekTarget < 0f) return;
            if (music.clip == null || !music.isPlaying || Time.unscaledTime - seekStartedAt > 5f)
            {
                seekTarget = -1f;
                return;
            }
            if (music.clip.loadState != AudioDataLoadState.Loaded) return;
            float expected = seekTarget + (Time.unscaledTime - seekStartedAt);
            if (Mathf.Abs(MusicTime - expected) > 0.03f) SetPosition(expected);
            else seekTarget = -1f;
        }

        void SetPosition(float seconds)
        {
            var clip = music.clip;
            int sample = Mathf.Clamp(Mathf.RoundToInt(seconds * clip.frequency), 0, Mathf.Max(0, clip.samples - 1));
            music.timeSamples = sample;
        }

        /// <summary>Loads a song's audio data before the tap, so it can start at once at any point.</summary>
        public void PrepareMusic(string clipName, int levelNumber)
        {
            AudioClip clip = null;
            if (!string.IsNullOrEmpty(clipName))
            {
                foreach (var c in customMusic)
                    if (c != null && string.Equals(c.name, clipName, System.StringComparison.OrdinalIgnoreCase)) clip = c;
                if (clip == null && levelMusic != null)
                    foreach (var c in levelMusic)
                        if (c != null && string.Equals(c.name, clipName, System.StringComparison.OrdinalIgnoreCase)) clip = c;
                string file = clip == null ? MusicLibrary.FindFile(clipName) : null;
                string world = clip == null && file == null ? MusicLibrary.WorldSong(clipName) : null;
                if (world != null && levelMusic != null)
                    foreach (var c in levelMusic)
                        if (c != null && string.Equals(c.name, world, System.StringComparison.OrdinalIgnoreCase)) clip = c;
                if (file != null && !loadedFiles.ContainsKey(file)) StartCoroutine(Preload(file));
            }
            else if (levelMusic != null && levelNumber >= 1 && levelNumber <= levelMusic.Length) clip = levelMusic[levelNumber - 1];
            if (clip != null && clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
        }

        IEnumerator Preload(string path)
        {
            using (var req = UnityWebRequestMultimedia.GetAudioClip(MusicLibrary.Url(path), MusicLibrary.TypeOf(path)))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success || loadedFiles.ContainsKey(path)) yield break;
                var clip = DownloadHandlerAudioClip.GetContent(req);
                clip.name = System.IO.Path.GetFileNameWithoutExtension(path);
                loadedFiles[path] = clip;
            }
        }

        /// <summary>Starts the track for a level number (1-based). Missing tracks are skipped silently.</summary>
        public void StartMusic(int levelNumber) => StartMusic(levelNumber, 0f);

        /// <summary>Starts the track for a level number (1-based) from <paramref name="seconds"/>.</summary>
        public void StartMusic(int levelNumber, float seconds)
        {
            int i = levelNumber - 1;
            if (levelMusic == null || i < 0 || i >= levelMusic.Length || levelMusic[i] == null) return;
            PlayClip(levelMusic[i], seconds);
        }

        /// <summary>Starts a track by clip name (e.g. "Music1" from level.json). Falls back to a number in the name.</summary>
        public void StartMusic(string clipName) => StartMusic(clipName, 0f);

        /// <summary>
        /// Starts a song by name from <paramref name="seconds"/>: Music1..5, a Custom Music clip, or an audio file in
        /// a Music folder (loaded in the background; the time spent loading is skipped so the song stays on the beat).
        /// </summary>
        public void StartMusic(string clipName, float seconds)
        {
            if (string.IsNullOrEmpty(clipName)) return;
            foreach (var c in customMusic)
                if (c != null && string.Equals(c.name, clipName, System.StringComparison.OrdinalIgnoreCase))
                {
                    PlayClip(c, seconds);
                    return;
                }
            string file = MusicLibrary.FindFile(clipName);
            if (file != null)
            {
                if (loading != null) StopCoroutine(loading);
                loading = StartCoroutine(LoadAndPlay(file, seconds));
                return;
            }
            StartLevelMusic(clipName, seconds);
        }

        Coroutine loading;
        readonly Dictionary<string, AudioClip> loadedFiles = new Dictionary<string, AudioClip>();

        IEnumerator LoadAndPlay(string path, float seconds)
        {
            float requested = Time.unscaledTime;
            if (!loadedFiles.TryGetValue(path, out var clip) || clip == null)
            {
                using (var req = UnityWebRequestMultimedia.GetAudioClip(MusicLibrary.Url(path), MusicLibrary.TypeOf(path)))
                {
                    yield return req.SendWebRequest();
                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogWarning($"RTXOn: could not load music {path}: {req.error}");
                        loading = null;
                        yield break;
                    }
                    clip = DownloadHandlerAudioClip.GetContent(req);
                    clip.name = System.IO.Path.GetFileNameWithoutExtension(path);
                    loadedFiles[path] = clip;
                }
            }
            loading = null;
            PlayClip(clip, seconds + (Time.unscaledTime - requested));
        }

        void PlayClip(AudioClip clip, float seconds)
        {
            fadeSpeed = 0f;
            music.clip = clip;
            music.volume = MusicLevel;
            if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            seconds = Mathf.Max(0f, seconds);
            if (clip.loadState == AudioDataLoadState.Loaded) SetPosition(seconds); // start right at the point
            music.Play();
            if (seconds > 0f || clip.loadState != AudioDataLoadState.Loaded) SetPosition(seconds);
            // KeepSeek checks the song really is there (and puts it there once its data has loaded).
            seekTarget = seconds;
            seekStartedAt = Time.unscaledTime;
        }

        void StartLevelMusic(string clipName, float seconds)
        {
            if (levelMusic != null)
                for (int i = 0; i < levelMusic.Length; i++)
                    if (levelMusic[i] != null && string.Equals(levelMusic[i].name, clipName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        StartMusic(i + 1, seconds);
                        return;
                    }
            string world = MusicLibrary.WorldSong(clipName); // "Massif" = Music1 ...
            if (world != null) clipName = world;
            var digits = System.Text.RegularExpressions.Regex.Match(clipName ?? "", @"\d+");
            if (digits.Success) StartMusic(int.Parse(digits.Value), seconds);
        }

        /// <summary>Names of every song a level can use: Music1..5, Custom Music clips.</summary>
        public IEnumerable<string> ClipNames()
        {
            if (levelMusic != null) foreach (var c in levelMusic) if (c != null) yield return c.name;
            foreach (var c in customMusic) if (c != null) yield return c.name;
        }

        /// <summary>Jumps the playing track to <paramref name="seconds"/> (continuing from a checkpoint).</summary>
        public void SeekMusic(float seconds)
        {
            if (music == null || music.clip == null) return;
            SetPosition(seconds);
            seekTarget = Mathf.Max(0f, seconds);
            seekStartedAt = Time.unscaledTime;
        }

        public void FadeOutMusic(float seconds)
        {
            fadeSpeed = seconds <= 0f ? float.MaxValue : Mathf.Max(0.0001f, MusicLevel) / seconds;
        }

        public static void Play(Sfx sfx, float volume = 1f)
        {
            if (Instance != null) Instance.PlayInternal(sfx, volume);
        }

        /// <summary>Plays any clip (a custom obstacle's own sound) on the sound effect voices, at the sound effect volume.</summary>
        public static void PlaySfxClip(AudioClip clip, float volume = 1f)
        {
            if (Instance == null || clip == null) return;
            var v = Instance.voices[Instance.nextVoice];
            Instance.nextVoice = (Instance.nextVoice + 1) % Voices;
            v.pitch = 1f;
            v.PlayOneShot(clip, volume * Instance.sfxVolume * GameSettings.SfxVolume);
        }

        void PlayInternal(Sfx sfx, float volume)
        {
            var clip = ClipFor(sfx);
            if (clip == null) return;
            int k = (int)sfx;
            if (Time.time - lastPlayed[k] < MinRepeat) return;
            lastPlayed[k] = Time.time;

            var v = voices[nextVoice];
            nextVoice = (nextVoice + 1) % Voices;
            v.pitch = 1f + Random.Range(-0.04f, 0.04f);
            v.PlayOneShot(clip, volume * sfxVolume * GameSettings.SfxVolume);
        }

        AudioClip chime, crownChime, powerChime, speedUpChime, speedDownChime, switchChime, invisibleChime;

        /// <summary>
        /// A short chime of rising notes, used when no clip is set: C E G C for a checkpoint, high G C for a crown.
        /// </summary>
        static AudioClip MakeChime(string name, float[] notes)
        {
            const int rate = 44100;
            const float step = 0.075f, ring = 0.55f;
            int length = (int)(rate * (step * notes.Length + ring));
            var data = new float[length];
            for (int n = 0; n < notes.Length; n++)
            {
                int start = (int)(rate * step * n);
                for (int i = start; i < length; i++)
                {
                    float t = (i - start) / (float)rate;
                    float env = Mathf.Exp(-t * 6f) * Mathf.Min(1f, t * 400f);
                    data[i] += 0.22f * env * (Mathf.Sin(2f * Mathf.PI * notes[n] * t) + 0.35f * Mathf.Sin(4f * Mathf.PI * notes[n] * t));
                }
            }
            var clip = AudioClip.Create(name, length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        AudioClip ClipFor(Sfx sfx)
        {
            switch (sfx)
            {
                case Sfx.Jump: return jump;
                case Sfx.Crash: return crash;
                case Sfx.Pickup: return pickup;
                case Sfx.Fragile: return fragile;
                case Sfx.Crusher: return crusher;
                case Sfx.Riser: return riser;
                case Sfx.Slide: return slide;
                case Sfx.Woosh: return woosh;
                case Sfx.Laser: return laser;
                case Sfx.Explosion: return explosion;
                case Sfx.Checkpoint:
                    if (checkpoint != null) return checkpoint;
                    return chime != null ? chime : (chime = MakeChime("Checkpoint Chime", new[] { 523.25f, 659.25f, 783.99f, 1046.5f }));
                case Sfx.Crown:
                    if (crown != null) return crown;
                    return crownChime != null ? crownChime : (crownChime = MakeChime("Crown Chime", new[] { 1567.98f, 2093f }));
                case Sfx.PowerUp:
                    if (powerUp != null) return powerUp;
                    return powerChime != null ? powerChime : (powerChime = MakeChime("Power Up Chime", new[] { 783.99f, 1046.5f, 1318.5f, 1567.98f }));
                case Sfx.SpeedUp:
                    if (speedUp != null || powerUp != null) return speedUp != null ? speedUp : powerUp;
                    return speedUpChime != null ? speedUpChime : (speedUpChime = MakeChime("Speed Up Chime", new[] { 659.25f, 880f, 1174.66f, 1567.98f, 2093f }));
                case Sfx.SpeedDown:
                    if (speedDown != null || powerUp != null) return speedDown != null ? speedDown : powerUp;
                    return speedDownChime != null ? speedDownChime : (speedDownChime = MakeChime("Speed Down Chime", new[] { 1567.98f, 1174.66f, 880f, 659.25f }));
                case Sfx.Switch:
                    if (switchLanes != null || powerUp != null) return switchLanes != null ? switchLanes : powerUp;
                    return switchChime != null ? switchChime : (switchChime = MakeChime("Switch Chime", new[] { 1046.5f, 783.99f, 1046.5f, 783.99f }));
                case Sfx.Invisible:
                    if (invisible != null || powerUp != null) return invisible != null ? invisible : powerUp;
                    return invisibleChime != null ? invisibleChime : (invisibleChime = MakeChime("Invisible Chime", new[] { 1318.5f, 1046.5f, 1318.5f }));
            }
            return null;
        }
    }
}
