using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace RTXOn
{
    /// <summary>
    /// Music visual of the level editor: the song's loudness as a strip beside the row numbers, lined up with the
    /// rows (row → seconds through the level's speed, and Speed Up/Down that count the music).
    /// Drawn into a small texture; the colours are fields, so the look can be changed later.
    /// </summary>
    public class MusicWaveView : MonoBehaviour
    {
        public Color waveColor = new Color(0.35f, 0.9f, 1f, 1f);
        public Color pastEndColor = new Color(0.25f, 0.45f, 0.55f, 1f);
        public Color beatColor = new Color(1f, 1f, 1f, 0.3f);
        public Color backgroundColor = new Color(0.05f, 0.04f, 0.06f, 1f);

        const int Width = 64, PixelsPerRow = 24, EnvelopeRate = 100;

        RawImage image;
        Texture2D texture;
        Color32[] pixels;
        float[] envelope; // loudness 0..1, EnvelopeRate values per second
        AudioClip source;
        Coroutine building;
        int rows;

        /// <summary>Why there is no wave for the current song (null = fine or still reading it).</summary>
        public string Problem { get; private set; }
        /// <summary>Called when a song has been read, so the owner can redraw.</summary>
        public System.Action Ready;

        public void Build(int visibleRows)
        {
            rows = visibleRows;
            image = gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            texture = new Texture2D(Width, rows * PixelsPerRow, TextureFormat.RGBA32, false)
            {
                name = "Music Wave",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            pixels = new Color32[texture.width * texture.height];
            image.texture = texture;
        }

        void OnDestroy()
        {
            if (texture != null) Destroy(texture);
        }

        /// <summary>
        /// The song to show. Reading its samples takes a few frames.
        /// <paramref name="fromFile"/>: loaded from a song file at runtime (always readable), not a project clip.
        /// </summary>
        public void SetClip(AudioClip clip, bool fromFile)
        {
            if (clip == source) return;
            source = clip;
            envelope = null;
            Problem = null;
            if (building != null) StopCoroutine(building);
            building = clip != null ? StartCoroutine(ReadSong(clip, fromFile)) : null;
        }

        IEnumerator ReadSong(AudioClip clip, bool fromFile)
        {
            var pcm = clip;
#if UNITY_EDITOR
            // A compressed clip can't hand out its samples: in the Unity Editor, read the song file itself instead.
            if (!fromFile && clip.loadType != AudioClipLoadType.DecompressOnLoad)
            {
                string path = UnityEditor.AssetDatabase.GetAssetPath(clip);
                if (!string.IsNullOrEmpty(path))
                {
                    string full = System.IO.Path.GetFullPath(path);
                    using (var req = UnityWebRequestMultimedia.GetAudioClip(new System.Uri(full).AbsoluteUri, MusicLibrary.TypeOf(full)))
                    {
                        yield return req.SendWebRequest();
                        if (req.result == UnityWebRequest.Result.Success) pcm = DownloadHandlerAudioClip.GetContent(req);
                    }
                }
            }
#endif
            if (!fromFile && pcm == clip && clip.loadType != AudioClipLoadType.DecompressOnLoad)
            {
                Problem = $"No music visual for {clip.name}: set its Load Type to Decompress On Load.";
                building = null;
                yield break;
            }
            if (pcm.loadState != AudioDataLoadState.Loaded)
            {
                pcm.LoadAudioData();
                while (pcm.loadState == AudioDataLoadState.Loading) yield return null;
            }

            int channels = Mathf.Max(1, pcm.channels);
            int frames = pcm.samples;
            int perValue = Mathf.Max(1, pcm.frequency / EnvelopeRate);
            var env = new float[frames / perValue + 1];
            const int ChunkFrames = 22050;
            var buffer = new float[ChunkFrames * channels];
            int chunk = 0;
            for (int start = 0; start < frames; start += ChunkFrames)
            {
                int count = Mathf.Min(ChunkFrames, frames - start);
                if (count < ChunkFrames) buffer = new float[count * channels];
                if (!pcm.GetData(buffer, start))
                {
                    Problem = $"No music visual for {clip.name}: its samples can't be read.";
                    building = null;
                    yield break;
                }
                for (int i = 0; i < count; i++)
                {
                    float peak = 0f;
                    for (int c = 0; c < channels; c++) peak = Mathf.Max(peak, Mathf.Abs(buffer[i * channels + c]));
                    int e = (start + i) / perValue;
                    if (peak > env[e]) env[e] = peak;
                }
                if (++chunk % 16 == 0) yield return null; // spread over frames, no hitch
            }

            float loudest = 0.0001f;
            foreach (float v in env) loudest = Mathf.Max(loudest, v);
            for (int i = 0; i < env.Length; i++) env[i] = Mathf.Sqrt(env[i] / loudest); // quiet parts stay visible
            envelope = env;
            building = null;
            Ready?.Invoke();
        }

        /// <summary>Redraws the strip for the rows on screen.</summary>
        public void Draw(LevelData level, int firstRow, float fallbackSpeed)
        {
            if (texture == null) return;
            Color32 background = backgroundColor, wave = waveColor, past = pastEndColor;
            var beat = new Color32((byte)(beatColor.r * 255), (byte)(beatColor.g * 255), (byte)(beatColor.b * 255), 255);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = background;

            if (envelope != null && level != null)
            {
                float beatSeconds = 0f; // no BPM any more, so no beat lines
                float t = level.TimeAtRow(firstRow, fallbackSpeed);
                for (int r = 0; r < rows; r++)
                {
                    int row = firstRow + r;
                    float step = 1f / Mathf.Max(0.01f, level.MusicSpeedAt(row + 0.5f, fallbackSpeed)) / PixelsPerRow;
                    var color = row < level.RowCount ? wave : past;
                    for (int k = 0; k < PixelsPerRow; k++, t += step)
                    {
                        int y = r * PixelsPerRow + k; // row 0 at the bottom, like the grid
                        int half = Mathf.RoundToInt(Loudness(t, t + step) * (Width / 2 - 1));
                        for (int x = Width / 2 - half; x <= Width / 2 + half; x++) pixels[y * Width + x] = color;
                        if (beatSeconds > 0f && Mathf.Floor((t + step) / beatSeconds) > Mathf.Floor(t / beatSeconds))
                            for (int x = 0; x < Width; x++)
                                pixels[y * Width + x] = Color32.Lerp(pixels[y * Width + x], beat, beatColor.a);
                    }
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false);
        }

        /// <summary>True once the song's loudness has been read (the 3D view draws it too).</summary>
        public bool HasSong => envelope != null;

        /// <summary>Loudest point (0..1) between two times of the song, in seconds.</summary>
        public float LoudnessBetween(float from, float to) => envelope != null ? Loudness(from, to) : 0f;

        float Loudness(float from, float to)
        {
            int a = Mathf.FloorToInt(from * EnvelopeRate), b = Mathf.Max(a, Mathf.FloorToInt(to * EnvelopeRate));
            float peak = 0f;
            for (int i = Mathf.Max(0, a); i <= b && i < envelope.Length; i++) peak = Mathf.Max(peak, envelope[i]);
            return peak;
        }
    }
}
