using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Networking;

namespace RTXOn
{
    /// <summary>A picture of a Custom Obstacle: one frame (PNG / JPG) or the frames of an animated GIF.</summary>
    public sealed class ImageFrames
    {
        public readonly Texture2D[] frames;
        readonly float[] ends; // when each frame ends, in seconds from the start of the loop
        public readonly float length;

        public ImageFrames(List<Texture2D> frames, List<float> delays)
        {
            this.frames = frames.ToArray();
            ends = new float[this.frames.Length];
            float t = 0f;
            for (int i = 0; i < ends.Length; i++)
            {
                t += i < delays.Count ? Mathf.Max(0.02f, delays[i]) : 0.1f;
                ends[i] = t;
            }
            length = t;
        }

        public bool Animated => frames.Length > 1;
        public Texture2D First => frames.Length > 0 ? frames[0] : null;
        public int Width => First != null ? First.width : 1;
        public int Height => First != null ? First.height : 1;

        /// <summary>The frame showing <paramref name="time"/> seconds in (the GIF loops).</summary>
        public Texture2D At(float time)
        {
            if (frames.Length <= 1 || length <= 0f) return First;
            float t = Mathf.Repeat(time, length);
            for (int i = 0; i < ends.Length; i++) if (t < ends[i]) return frames[i];
            return frames[frames.Length - 1];
        }
    }

    /// <summary>
    /// Pictures and sounds kept inside Custom Obstacles (base64 in the .rsrt): decoded once and shared by every cell
    /// with that obstacle. Pictures: PNG, JPG, GIF (animated). Sounds: WAV at once; OGG and MP3 load in the
    /// background (a few frames), so they are started as soon as an obstacle is built.
    /// </summary>
    public static class CustomMedia
    {
        public const int MaxFileBytes = 8 * 1024 * 1024;
        public static readonly string[] ImageExtensions = { "png", "jpg", "jpeg", "gif" };
        public static readonly string[] SoundExtensions = { "wav", "ogg", "mp3" };

        // The same base64 string is looked up by reference first (cheap), then by its contents (a copy of the
        // obstacle made by Save or a level load has equal text in a new string).
        static readonly Dictionary<string, ImageFrames> imagesByRef = new Dictionary<string, ImageFrames>(SameString.Instance);
        static readonly Dictionary<string, ImageFrames> images = new Dictionary<string, ImageFrames>();
        static readonly Dictionary<string, AudioClip> soundsByRef = new Dictionary<string, AudioClip>(SameString.Instance);
        static readonly Dictionary<string, AudioClip> sounds = new Dictionary<string, AudioClip>();
        static readonly HashSet<string> loading = new HashSet<string>();

        sealed class SameString : IEqualityComparer<string>
        {
            public static readonly SameString Instance = new SameString();
            public bool Equals(string a, string b) => ReferenceEquals(a, b);
            public int GetHashCode(string s) => RuntimeHelpers.GetHashCode(s);
        }

        static string Key(string base64) => base64.Length + ":" + base64.GetHashCode();

        public static byte[] Bytes(string base64)
        {
            if (string.IsNullOrEmpty(base64)) return null;
            try { return Convert.FromBase64String(base64); }
            catch (FormatException) { return null; }
        }

        // ---------------- Pictures ----------------

        /// <summary>The picture in <paramref name="base64"/> (PNG, JPG or GIF), or null.</summary>
        public static ImageFrames Image(string base64, string name = "Custom Picture")
        {
            if (string.IsNullOrEmpty(base64)) return null;
            if (imagesByRef.TryGetValue(base64, out var known)) return known;
            string key = Key(base64);
            if (!images.TryGetValue(key, out known))
            {
                known = Decode(Bytes(base64), name);
                images[key] = known;
            }
            imagesByRef[base64] = known;
            return known;
        }

        /// <summary>Reads a picture file's bytes: PNG and JPG with Unity, GIF with <see cref="GifDecoder"/>. Null when unreadable.</summary>
        public static ImageFrames Decode(byte[] bytes, string name)
        {
            if (bytes == null || bytes.Length == 0) return null;
            if (GifDecoder.IsGif(bytes))
                return GifDecoder.Decode(bytes, name, out var frames, out var delays) ? new ImageFrames(frames, delays) : null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Clamp };
            if (!tex.LoadImage(bytes))
            {
                UnityEngine.Object.Destroy(tex);
                return null;
            }
            return new ImageFrames(new List<Texture2D> { tex }, new List<float> { 1f });
        }

        // ---------------- Sounds ----------------

        /// <summary>
        /// The sound in <paramref name="base64"/> (file <paramref name="fileName"/>: .wav, .ogg or .mp3), or null when it
        /// cannot be read, or (OGG / MP3) while it is still loading: asking again later gets it.
        /// </summary>
        public static AudioClip Sound(string base64, string fileName)
        {
            if (string.IsNullOrEmpty(base64)) return null;
            if (soundsByRef.TryGetValue(base64, out var known)) return known;
            string key = Key(base64);
            if (sounds.TryGetValue(key, out known))
            {
                soundsByRef[base64] = known;
                return known;
            }
            if (loading.Contains(key)) return null;
            var bytes = Bytes(base64);
            string name = string.IsNullOrEmpty(fileName) ? "Custom Sound" : Path.GetFileNameWithoutExtension(fileName);
            string ext = (Path.GetExtension(fileName ?? "") ?? "").TrimStart('.').ToLowerInvariant();
            if (bytes == null) return null;
            if (ext == "wav" || IsWav(bytes))
            {
                known = Wav(bytes, name);
                sounds[key] = known;
                soundsByRef[base64] = known;
                return known;
            }
            var type = ext == "mp3" ? AudioType.MPEG : ext == "ogg" ? AudioType.OGGVORBIS : AudioType.UNKNOWN;
            if (type == AudioType.UNKNOWN) return null;
            loading.Add(key);
            Loader.Run(LoadFile(bytes, ext, type, key, name));
            return null;
        }

        static IEnumerator LoadFile(byte[] bytes, string ext, AudioType type, string key, string name)
        {
            // Unity reads OGG / MP3 only through a file or URL: write it to the cache folder, then load it.
            string path = Path.Combine(Application.temporaryCachePath, "rsrt-sound-" + (uint)key.GetHashCode() + "." + ext);
            AudioClip clip = null;
            bool written = true;
            try { File.WriteAllBytes(path, bytes); }
            catch (Exception e) { Debug.LogWarning("Custom obstacle sound could not be written: " + e.Message); written = false; }
            if (written)
            {
                using (var request = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, type))
                {
                    yield return request.SendWebRequest();
                    if (request.result == UnityWebRequest.Result.Success) clip = DownloadHandlerAudioClip.GetContent(request);
                    else Debug.LogWarning($"Custom obstacle sound {name}.{ext} could not be read: {request.error}");
                }
            }
            if (clip != null) clip.name = name;
            sounds[key] = clip;
            loading.Remove(key);
        }

        static bool IsWav(byte[] b) => b.Length > 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'A' && b[10] == 'V' && b[11] == 'E';

        /// <summary>A WAV file (PCM 8 / 16 / 24 / 32 bit or 32 bit float) as a clip, or null.</summary>
        public static AudioClip Wav(byte[] b, string name)
        {
            if (!IsWav(b)) return null;
            int channels = 0, rate = 0, bitsPer = 0, format = 0, dataStart = -1, dataLength = 0;
            int p = 12;
            while (p + 8 <= b.Length)
            {
                string id = System.Text.Encoding.ASCII.GetString(b, p, 4);
                int size = BitConverter.ToInt32(b, p + 4);
                int body = p + 8;
                if (size < 0) break;
                if (id == "fmt " && body + 16 <= b.Length)
                {
                    format = BitConverter.ToUInt16(b, body);
                    channels = BitConverter.ToUInt16(b, body + 2);
                    rate = BitConverter.ToInt32(b, body + 4);
                    bitsPer = BitConverter.ToUInt16(b, body + 14);
                    if (format == 0xFFFE && size >= 26 && body + 26 <= b.Length) format = BitConverter.ToUInt16(b, body + 24); // extensible
                }
                else if (id == "data")
                {
                    dataStart = body;
                    dataLength = Mathf.Min(size, b.Length - body);
                    break;
                }
                p = body + size + (size & 1);
            }
            if (dataStart < 0 || channels <= 0 || rate <= 0) return null;
            int bytesPer = bitsPer / 8;
            if (bytesPer < 1 || bytesPer > 4 || (format != 1 && format != 3)) return null;
            int count = dataLength / bytesPer / channels * channels;
            if (count <= 0) return null;
            var samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                int q = dataStart + i * bytesPer;
                samples[i] = format == 3 && bytesPer == 4 ? BitConverter.ToSingle(b, q)
                    : bytesPer == 1 ? (b[q] - 128) / 128f
                    : bytesPer == 2 ? BitConverter.ToInt16(b, q) / 32768f
                    : bytesPer == 3 ? ((b[q] << 8 | b[q + 1] << 16 | b[q + 2] << 24) >> 8) / 8388608f
                    : BitConverter.ToInt32(b, q) / 2147483648f;
            }
            var clip = AudioClip.Create(name, count / channels, channels, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>Runs the background sound loads (survives scene changes, so a level load can finish them).</summary>
        sealed class Loader : MonoBehaviour
        {
            static Loader instance;

            public static void Run(IEnumerator routine)
            {
                if (instance == null)
                {
                    var go = new GameObject("Custom Media Loader") { hideFlags = HideFlags.HideAndDontSave };
                    if (Application.isPlaying) DontDestroyOnLoad(go);
                    instance = go.AddComponent<Loader>();
                }
                instance.StartCoroutine(routine);
            }
        }
    }
}
