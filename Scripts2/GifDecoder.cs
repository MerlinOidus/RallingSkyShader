using System.Collections.Generic;
using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// Reads an animated (or still) GIF into one texture per frame and how long each one shows, for Custom Obstacles
    /// with a GIF picture: Unity's own Texture2D.LoadImage reads PNG and JPG but not GIF. Handles global and local
    /// colour tables, transparency, interlaced frames and the three disposal methods.
    /// </summary>
    public static class GifDecoder
    {
        public const int MaxFrames = 300;
        const long MaxTotalPixels = 40L * 1000 * 1000; // all frames together, about 160 MB of RGBA
        const int MaxCodes = 4096;

        public static bool IsGif(byte[] data) =>
            data != null && data.Length > 6 && data[0] == 'G' && data[1] == 'I' && data[2] == 'F' && data[3] == '8';

        /// <summary>The frames and their times in seconds, or false when the file is not a readable GIF.</summary>
        public static bool Decode(byte[] data, string name, out List<Texture2D> frames, out List<float> delays)
        {
            frames = new List<Texture2D>();
            delays = new List<float>();
            if (!IsGif(data) || data.Length < 13) return false;
            try { Read(data, name, frames, delays); }
            catch (System.IndexOutOfRangeException) { } // cut-off file: keep the frames read so far
            return frames.Count > 0;
        }

        static void Read(byte[] d, string name, List<Texture2D> frames, List<float> delays)
        {
            int p = 6;
            int width = U16(d, p), height = U16(d, p + 2);
            int packed = d[p + 4];
            p += 7;
            if (width <= 0 || height <= 0 || width > 4096 || height > 4096) return;
            Color32[] globalTable = null;
            if ((packed & 0x80) != 0)
            {
                globalTable = ReadTable(d, ref p, 2 << (packed & 7));
            }

            var canvas = new Color32[width * height]; // top row first, like the file
            Color32[] saved = null;
            int disposal = 0, transparent = -1;
            float delay = 0.1f;
            // The previous frame's rectangle and disposal, applied before the next frame is drawn.
            int prevDisposal = 0, prevX = 0, prevY = 0, prevW = 0, prevH = 0;
            long pixelsUsed = 0;

            while (p < d.Length)
            {
                int block = d[p++];
                if (block == 0x3B) break; // trailer
                if (block == 0x21) // extension
                {
                    int label = d[p++];
                    if (label == 0xF9 && d[p] >= 4) // graphic control: disposal, delay, transparent colour
                    {
                        int flags = d[p + 1];
                        disposal = (flags >> 2) & 7;
                        int hundredths = U16(d, p + 2);
                        delay = hundredths <= 1 ? 0.1f : hundredths / 100f; // 0 or 1 = "as fast as possible": browsers use 0.1 s
                        transparent = (flags & 1) != 0 ? d[p + 4] : -1;
                    }
                    SkipSubBlocks(d, ref p);
                    continue;
                }
                if (block != 0x2C) break; // unknown block: stop here

                int x = U16(d, p), y = U16(d, p + 2), w = U16(d, p + 4), h = U16(d, p + 6);
                int imagePacked = d[p + 8];
                p += 9;
                var table = globalTable;
                if ((imagePacked & 0x80) != 0) table = ReadTable(d, ref p, 2 << (imagePacked & 7));
                bool interlaced = (imagePacked & 0x40) != 0;
                int minCodeSize = d[p++];
                var indices = Lzw(Gather(d, ref p), minCodeSize, w * h);

                // Undo the previous frame as its disposal asks.
                if (prevDisposal == 2) Clear(canvas, width, height, prevX, prevY, prevW, prevH);
                else if (prevDisposal == 3 && saved != null) System.Array.Copy(saved, canvas, canvas.Length);
                if (disposal == 3)
                {
                    if (saved == null) saved = new Color32[canvas.Length];
                    System.Array.Copy(canvas, saved, canvas.Length);
                }

                if (table != null) Draw(canvas, width, height, indices, table, transparent, x, y, w, h, interlaced);

                var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    name = $"{name} {frames.Count}",
                    wrapMode = TextureWrapMode.Clamp,
                };
                tex.SetPixels32(FlipRows(canvas, width, height)); // Unity's rows go bottom up
                tex.Apply(false, true);
                frames.Add(tex);
                delays.Add(delay);

                prevDisposal = disposal;
                prevX = x; prevY = y; prevW = w; prevH = h;
                disposal = 0;
                transparent = -1;
                delay = 0.1f;
                pixelsUsed += (long)width * height;
                if (frames.Count >= MaxFrames || pixelsUsed >= MaxTotalPixels) break;
            }
        }

        static int U16(byte[] d, int p) => d[p] | (d[p + 1] << 8);

        static Color32[] ReadTable(byte[] d, ref int p, int count)
        {
            var table = new Color32[count];
            for (int i = 0; i < count; i++, p += 3) table[i] = new Color32(d[p], d[p + 1], d[p + 2], 255);
            return table;
        }

        static void SkipSubBlocks(byte[] d, ref int p)
        {
            while (true)
            {
                int size = d[p++];
                if (size == 0) return;
                p += size;
            }
        }

        /// <summary>The image data sub-blocks joined into one array.</summary>
        static byte[] Gather(byte[] d, ref int p)
        {
            var list = new List<byte>();
            while (p < d.Length)
            {
                int size = d[p++];
                if (size == 0) break;
                int n = Mathf.Min(size, d.Length - p);
                for (int i = 0; i < n; i++) list.Add(d[p + i]);
                p += size;
            }
            return list.ToArray();
        }

        /// <summary>GIF's LZW: the colour index of every pixel of the frame (missing pixels stay 0).</summary>
        static byte[] Lzw(byte[] data, int minCodeSize, int pixelCount)
        {
            var pixels = new byte[pixelCount];
            if (minCodeSize < 1 || minCodeSize > 11) return pixels;
            var prefix = new short[MaxCodes];
            var suffix = new byte[MaxCodes];
            var stack = new byte[MaxCodes + 1];
            int clear = 1 << minCodeSize, end = clear + 1;
            int available = clear + 2, oldCode = -1, codeSize = minCodeSize + 1, codeMask = (1 << codeSize) - 1;
            for (int c = 0; c < clear; c++) suffix[c] = (byte)c;
            int datum = 0, bits = 0, first = 0, top = 0, bi = 0, pi = 0;

            while (pi < pixelCount)
            {
                if (top == 0)
                {
                    if (bits < codeSize)
                    {
                        if (bi >= data.Length) break;
                        datum |= data[bi++] << bits;
                        bits += 8;
                        continue;
                    }
                    int code = datum & codeMask;
                    datum >>= codeSize;
                    bits -= codeSize;
                    if (code == end || code > available) break;
                    if (code == clear)
                    {
                        codeSize = minCodeSize + 1;
                        codeMask = (1 << codeSize) - 1;
                        available = clear + 2;
                        oldCode = -1;
                        continue;
                    }
                    if (oldCode == -1)
                    {
                        stack[top++] = suffix[code];
                        oldCode = code;
                        first = code;
                        continue;
                    }
                    int inCode = code;
                    if (code == available)
                    {
                        stack[top++] = (byte)first;
                        code = oldCode;
                    }
                    while (code > clear)
                    {
                        stack[top++] = suffix[code];
                        code = prefix[code];
                    }
                    first = suffix[code];
                    stack[top++] = (byte)first;
                    if (available < MaxCodes) // a full table waits for the next clear code
                    {
                        prefix[available] = (short)oldCode;
                        suffix[available] = (byte)first;
                        available++;
                        if ((available & codeMask) == 0 && available < MaxCodes)
                        {
                            codeSize++;
                            codeMask += available;
                        }
                    }
                    oldCode = inCode;
                }
                pixels[pi++] = stack[--top];
            }
            return pixels;
        }

        static void Draw(Color32[] canvas, int width, int height, byte[] indices, Color32[] table, int transparent,
                         int x, int y, int w, int h, bool interlaced)
        {
            int[] starts = { 0, 4, 2, 1 }, steps = { 8, 8, 4, 2 };
            int pass = 0, line = 0;
            for (int row = 0; row < h; row++)
            {
                int fileRow = row;
                if (interlaced)
                {
                    // Rows come in four passes: every 8th from 0, every 8th from 4, every 4th from 2, every 2nd from 1.
                    while (pass < 4 && starts[pass] + line * steps[pass] >= h) { pass++; line = 0; }
                    if (pass >= 4) break;
                    fileRow = starts[pass] + line * steps[pass];
                    line++;
                }
                int cy = y + fileRow;
                if (cy < 0 || cy >= height) continue;
                for (int col = 0; col < w; col++)
                {
                    int cx = x + col;
                    if (cx < 0 || cx >= width) continue;
                    int index = indices[row * w + col];
                    if (index == transparent || index >= table.Length) continue;
                    canvas[cy * width + cx] = table[index];
                }
            }
        }

        static void Clear(Color32[] canvas, int width, int height, int x, int y, int w, int h)
        {
            for (int cy = Mathf.Max(0, y); cy < Mathf.Min(height, y + h); cy++)
                for (int cx = Mathf.Max(0, x); cx < Mathf.Min(width, x + w); cx++)
                    canvas[cy * width + cx] = default;
        }

        static Color32[] FlipRows(Color32[] canvas, int width, int height)
        {
            var flipped = new Color32[canvas.Length];
            for (int row = 0; row < height; row++)
                System.Array.Copy(canvas, row * width, flipped, (height - 1 - row) * width, width);
            return flipped;
        }
    }
}
