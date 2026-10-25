using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace RTXOn
{
    public enum SpriteBlend { Normal, Glow, Multiply, Screen }

    // The JSON shapes match Adrian's Texture Lab (RollingSkyTextureLab) so presets and project files move between the two.

    [Serializable]
    public class SpriteLayerState
    {
        public string n;              // layer name, with its prefix (tr/ mv/ fa/ g/)
        public bool on = true;
        public string c = "#ffffff";
        public float o = 1f;
        public string m = "source-over";
        public string gm = "";        // in the glow map: "" = auto (glow layers), "on", "off"

        public SpriteLayerState Clone() => new SpriteLayerState { n = n, on = on, c = c, o = o, m = m, gm = gm };
    }

    [Serializable]
    public class SpriteGroupPreset
    {
        public string key;
        public bool hasBase;
        public string baseColor = "#000000";
        public List<SpriteLayerState> layers = new List<SpriteLayerState>();
    }

    [Serializable]
    public class SpritePresetFile { public List<SpriteGroupPreset> groups = new List<SpriteGroupPreset>(); }

    [Serializable]
    public class SpritePaletteElement { public int position; public int type; public string[] colors; }

    [Serializable]
    public class SpritePaletteOrder { public bool alwaysOnTop; public SpritePaletteElement[] elements = new SpritePaletteElement[0]; }

    [Serializable]
    public class SpriteProjectFile
    {
        public string group;
        public string topRight = "Star";
        public string paletteOrderJson = "{\"elements\": []}";
        public string glowPaletteJson = "";      // SpriteMaker: the glow map's own Palette Order ("" = the same as paletteOrderJson)
        public string glowCells = "6/16";        // SpriteMaker: palette cells in the glow map (6/16, 6 px, Full, Off)
        public List<SpriteGroupPreset> groups = new List<SpriteGroupPreset>();
    }

    /// <summary>
    /// SpriteMaker: builds Rolling Sky atlases (Enemy, TileGeneral, Mover, Fragile, Active) from white mask layers,
    /// like Adrian's Texture Lab and sqdldev's SpriteMaker. Each layer = its mask x a tint, with opacity and a blend
    /// (Normal, Glow = additive, Multiply, Screen), stacked in order over an optional base colour. The masks are PNG files
    /// renamed .bytes in Resources/SpriteMaker (index.txt lists them), so the game build carries them small and exact on
    /// every platform; MassifPresets.json there is the Massif/Default look. Besides the colour atlas it can make a glow
    /// map: black, with only the glow layers.
    /// </summary>
    public class SpriteMakerLab
    {
        public const string ResourceRoot = "SpriteMaker";
        const string PaletteFolder = "enemy/topright/palettes";

        public class Source { public string folder, prefix; public Func<string, bool> filter; }

        public class GroupDef
        {
            public string key, label;
            public int w = 512, h = 512;
            public bool frames;
            public string[] slots = new string[0]; // the Custom Theme textures it is made for
            public List<Source> sources = new List<Source>();
        }

        public static readonly List<GroupDef> Groups = new List<GroupDef>
        {
            new GroupDef { key = "Enemy", label = "Enemy", slots = new[] { "Enemy" }, sources = {
                new Source { folder = "enemy", prefix = "" },
                new Source { folder = "enemy/topright", prefix = "tr/" } } },
            new GroupDef { key = "TileGeneral", label = "TileGeneral", slots = new[] { "General" }, sources = {
                new Source { folder = "general", prefix = "" } } },
            new GroupDef { key = "Mover", label = "Mover / AutoMover", slots = new[] { "Mover", "MoverAuto" }, sources = {
                new Source { folder = "general", prefix = "", filter = n => !n.StartsWith("jump_pad") },
                new Source { folder = "mover", prefix = "mv/" } } },
            new GroupDef { key = "Fragile", label = "Fragile", slots = new[] { "Fragile" }, sources = {
                new Source { folder = "fragile", prefix = "" },
                new Source { folder = "general", prefix = "g/", filter = n => n.StartsWith("main_palette") } } },
            new GroupDef { key = "Active", label = "Active (FragileActive)", slots = new[] { "FragileActive" }, sources = {
                new Source { folder = "fragile", prefix = "", filter = n => !n.StartsWith("top_left") },
                new Source { folder = "fragileactive", prefix = "fa/" },
                new Source { folder = "general", prefix = "g/", filter = n => n.StartsWith("main_palette") } } },
            new GroupDef { key = "EnemyBottomRight", label = "Enemy bottom right (frames)", w = 256, h = 128, frames = true, sources = {
                new Source { folder = "enemy/bottomright", prefix = "" } } },
        };

        /// <summary>Palette Order: type -> mask files in enemy/topright/palettes (":rot" = turned 90 degrees).</summary>
        static readonly Dictionary<int, string[]> PalTypes = new Dictionary<int, string[]>
        {
            {0, new[]{"0"}}, {1, new[]{"1-1","1-2","1-3","1-4"}}, {2, new[]{"2-1","2-2","2-3","2-4","2-5","2-6"}},
            {3, new[]{"3-1","3-2","3-3","3-4","3-5"}}, {4, new[]{"3-1","3-2","3-3","3-4","3-5","3-6"}},
            {5, new[]{"2-1","2-2","2-3","2-4","2-5","2-6","3-1-1"}}, {6, new[]{"2-1","2-2","2-3","2-4","2-5","2-6","3-1-1"}},
            {7, new[]{"4-1","4-2","4-3","4-4","4-5","4-6","4-7","4-8"}}, {8, new[]{"5-1","5-2","5-3"}},
            {9, new[]{"0-1"}}, {10, new[]{"0-1:rot"}}, {11, new[]{"0-1-1","0-1-2"}}, {12, new[]{"0-1-1-1","0-1-2-1"}},
            {13, Enumerable.Range(1, 16).Select(i => "6-" + i).ToArray()},
        };

        /// <summary>A mask cut to the part that has pixels (bb), bottom-up like Unity textures.</summary>
        public class Mask { public Color32[] px; public int w, h; public RectInt bb; }

        public class Layer
        {
            public string name, path;
            public SpriteLayerState st;
            public Mask mask;
            public bool InGlowMap => st.gm == "on" || (st.gm != "off" && (ParseBlend(st.m) == SpriteBlend.Glow || st.n.IndexOf("glow", StringComparison.OrdinalIgnoreCase) >= 0));
        }

        public class GroupState
        {
            public GroupDef def;
            public List<Layer> layers = new List<Layer>();
            public bool hasBase;
            public Color baseColor = Color.black;
            public int frame;
        }

        static List<string> index;
        static readonly Dictionary<string, Mask> masks = new Dictionary<string, Mask>();

        SpritePresetFile presets;
        readonly Dictionary<string, GroupState> states = new Dictionary<string, GroupState>();
        public string topRight = "Star";
        public string paletteJson = "{\"elements\": []}";

        /// <summary>The glow map's own Palette Order; null = the same as <see cref="palette"/>.</summary>
        public SpritePaletteOrder glowPalette;
        public string glowPaletteJson = "";

        /// <summary>
        /// Palette cells in the glow map (Enemy, top right Palette), each one kept in its middle, the rest see-through:
        /// "6/16" = 6 of every 16 pixels (a 16 px cell keeps 6, 32 keeps 12, 64 keeps 24), "6 px" = at most 6 pixels,
        /// "Full" = the whole cell, "Off" = no palettes in the glow map.
        /// </summary>
        public string glowCells = "6/16";
        public static readonly string[] GlowCellModes = { "6/16", "6 px", "Full", "Off" };
        public SpritePaletteOrder palette = new SpritePaletteOrder();

        public SpriteMakerLab()
        {
            var ta = Resources.Load<TextAsset>(ResourceRoot + "/MassifPresets");
            presets = ta != null ? JsonUtility.FromJson<SpritePresetFile>(ta.text) : null;
            if (presets == null) presets = new SpritePresetFile();
        }

        /// <summary>"folder/name" of every mask in Resources/SpriteMaker (from its index.txt).</summary>
        public static List<string> Index
        {
            get
            {
                if (index != null) return index;
                index = new List<string>();
                var ta = Resources.Load<TextAsset>(ResourceRoot + "/index");
                if (ta == null) return index;
                foreach (var line in ta.text.Split('\n'))
                {
                    var l = line.Trim();
                    if (l.Length > 0) index.Add(l);
                }
                return index;
            }
        }

        public static bool HasMasks => Index.Count > 0;

        // ---------------- Groups ----------------

        public GroupState State(GroupDef d)
        {
            if (!states.TryGetValue(d.key, out var s)) states[d.key] = s = BuildState(d, true);
            return s;
        }

        public static GroupDef FindGroup(string key) => Groups.FirstOrDefault(g => g.key == key);

        string PresetKey(GroupDef d) => d.key == "Enemy" && topRight == "Palette" ? "Enemy:Palette" : d.key;

        public bool PaletteMode(GroupDef d) => d.key == "Enemy" && topRight == "Palette";

        /// <summary>The group back to Massif/Default.</summary>
        public void Reset(GroupDef d) => states[d.key] = BuildState(d, true);

        GroupState BuildState(GroupDef d, bool usePreset)
        {
            var s = new GroupState { def = d };
            var all = new List<Layer>();
            foreach (var src in d.sources)
                foreach (var path in Index)
                {
                    int slash = path.LastIndexOf('/');
                    if (slash < 0 || path.Substring(0, slash) != src.folder) continue;
                    string n = path.Substring(slash + 1);
                    if (src.filter != null && !src.filter(n)) continue;
                    var full = src.prefix + n;
                    all.Add(new Layer { name = full, path = path, st = DefaultStyle(full) });
                }
            var p = usePreset ? presets.groups.FirstOrDefault(x => x.key == PresetKey(d)) : null;
            if (p != null)
            {
                var byName = new Dictionary<string, Layer>();
                foreach (var l in all) byName[l.name] = l;
                var used = new HashSet<string>();
                foreach (var ls in p.layers)
                {
                    if (ls == null || !byName.TryGetValue(ls.n, out var L) || used.Contains(ls.n)) continue;
                    L.st = ls.Clone();
                    if (L.st.gm == null) L.st.gm = "";
                    s.layers.Add(L);
                    used.Add(ls.n);
                }
                foreach (var L in all)
                    if (!used.Contains(L.name))
                    {
                        L.st.on = false;
                        s.layers.Add(L);
                    }
                s.hasBase = p.hasBase;
                ColorUtility.TryParseHtmlString(p.baseColor, out s.baseColor);
            }
            else s.layers = all.OrderBy(l => RoleOrder(Role(l.name))).ToList();
            return s;
        }

        public static string Role(string n)
        {
            n = n.ToLowerInvariant();
            if (n.Contains("glow")) return "glow";
            if (Regex.IsMatch(n, "shadow|darken")) return "shadow";
            if (Regex.IsMatch(n, "lighten|overlay")) return "light";
            if (Regex.IsMatch(n, "background|midground")) return "background";
            if (Regex.IsMatch(n, "border|outline")) return "border";
            if (n.Contains("dull")) return "soft";
            return "colour";
        }

        static int RoleOrder(string r) => Array.IndexOf(new[] { "background", "shadow", "soft", "colour", "light", "border", "glow" }, r);

        static SpriteLayerState DefaultStyle(string n)
        {
            switch (Role(n))
            {
                case "background": return new SpriteLayerState { n = n, c = "#afa69d" };
                case "shadow": return new SpriteLayerState { n = n, c = "#000000", o = 0.55f };
                case "light": return new SpriteLayerState { n = n, c = "#ffffff", o = 0.35f };
                case "glow": return new SpriteLayerState { n = n, c = "#30efee", o = 0.9f, m = "lighter" };
                case "soft": return new SpriteLayerState { n = n, c = "#877f77" };
                default: return new SpriteLayerState { n = n, c = "#30efee" };
            }
        }

        // ---------------- Masks ----------------

        public static Mask GetMask(string path)
        {
            if (masks.TryGetValue(path, out var m)) return m;
            var file = Resources.Load<TextAsset>(ResourceRoot + "/" + path);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            bool ok = file != null && tex.LoadImage(file.bytes);
            if (file != null) Resources.UnloadAsset(file);
            if (!ok)
            {
                UnityEngine.Object.Destroy(tex);
                masks[path] = null;
                return null;
            }
            int w = tex.width, h = tex.height;
            var px = tex.GetPixels32();
            UnityEngine.Object.Destroy(tex);
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (px[y * w + x].a > 0)
                    {
                        if (x < x0) x0 = x;
                        if (x > x1) x1 = x;
                        if (y < y0) y0 = y;
                        if (y > y1) y1 = y;
                    }
            m = new Mask { w = w, h = h };
            if (x1 < 0)
            {
                m.bb = new RectInt(0, 0, 0, 0);
                m.px = new Color32[0];
            }
            else
            {
                m.bb = new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
                m.px = new Color32[m.bb.width * m.bb.height];
                for (int y = 0; y < m.bb.height; y++)
                    Array.Copy(px, (y0 + y) * w + x0, m.px, y * m.bb.width, m.bb.width);
            }
            masks[path] = m;
            return m;
        }

        /// <summary>The mask pixel at (x, y) of the full picture (bottom-up): its alpha, 0 outside.</summary>
        public static int AlphaAt(Mask m, int x, int y)
        {
            if (m == null || !m.bb.Contains(new Vector2Int(x, y))) return 0;
            return m.px[(y - m.bb.y) * m.bb.width + (x - m.bb.x)].a;
        }

        // ---------------- Compositing ----------------

        public static SpriteBlend ParseBlend(string m)
        {
            switch (m)
            {
                case "lighter": return SpriteBlend.Glow;
                case "multiply": return SpriteBlend.Multiply;
                case "screen": return SpriteBlend.Screen;
                default: return SpriteBlend.Normal;
            }
        }

        public static string BlendName(SpriteBlend b)
        {
            switch (b)
            {
                case SpriteBlend.Glow: return "lighter";
                case SpriteBlend.Multiply: return "multiply";
                case SpriteBlend.Screen: return "screen";
                default: return "source-over";
            }
        }

        public static Color Hex(string h) => ColorUtility.TryParseHtmlString(h, out var c) ? c : Color.white;

        public static string ToHex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c).ToLowerInvariant();

        static void DrawMask(Color[] dst, int W, int H, Mask m, int ox, int oy, Color tint, float opacity, SpriteBlend blend, bool white = false, bool rotate = false)
        {
            if (m == null || m.px.Length == 0 || opacity <= 0f) return;
            int bw = m.bb.width, bh = m.bb.height;
            for (int y = 0; y < bh; y++)
                for (int x = 0; x < bw; x++)
                {
                    // Turned 90 degrees: (x, y) of the turned picture reads (y, h-1-x) of the mask.
                    int fx = m.bb.x + x, fy = m.bb.y + y, dx, dy;
                    if (rotate)
                    {
                        dx = ox + (m.h - 1 - fy);
                        dy = oy + fx;
                    }
                    else
                    {
                        dx = ox + fx;
                        dy = oy + fy;
                    }
                    var s = m.px[y * bw + x];
                    if (s.a == 0 || dx < 0 || dy < 0 || dx >= W || dy >= H) continue;
                    float a = s.a / 255f * opacity;
                    float r = (white ? 1f : s.r / 255f) * tint.r, g = (white ? 1f : s.g / 255f) * tint.g, b = (white ? 1f : s.b / 255f) * tint.b;
                    int i = dy * W + dx;
                    var d = dst[i];
                    switch (blend)
                    {
                        case SpriteBlend.Glow:
                            d.r = Mathf.Min(1f, d.r + r * a);
                            d.g = Mathf.Min(1f, d.g + g * a);
                            d.b = Mathf.Min(1f, d.b + b * a);
                            d.a = Mathf.Min(1f, d.a + a);
                            dst[i] = d;
                            continue;
                        case SpriteBlend.Multiply:
                            r = Mathf.Lerp(r, r * d.r, d.a);
                            g = Mathf.Lerp(g, g * d.g, d.a);
                            b = Mathf.Lerp(b, b * d.b, d.a);
                            break;
                        case SpriteBlend.Screen:
                            r = Mathf.Lerp(r, 1f - (1f - r) * (1f - d.r), d.a);
                            g = Mathf.Lerp(g, 1f - (1f - g) * (1f - d.g), d.a);
                            b = Mathf.Lerp(b, 1f - (1f - b) * (1f - d.b), d.a);
                            break;
                    }
                    float oa = a + d.a * (1f - a);
                    if (oa > 0f)
                    {
                        d.r = (r * a + d.r * d.a * (1f - a)) / oa;
                        d.g = (g * a + d.g * d.a * (1f - a)) / oa;
                        d.b = (b * a + d.b * d.a * (1f - a)) / oa;
                    }
                    d.a = oa;
                    dst[i] = d;
                }
        }

        /// <summary>
        /// The atlas of <paramref name="s"/>, bottom-up. <paramref name="glowMap"/>: black, with only the layers that are
        /// in the glow map (for the Custom Theme's Glow textures: black = no glow).
        /// </summary>
        public Color[] Compose(GroupState s, bool glowMap)
        {
            int W = s.def.w, H = s.def.h;
            var buf = new Color[W * H];
            if (glowMap) for (int i = 0; i < buf.Length; i++) buf[i] = Color.black;
            else if (s.hasBase) for (int i = 0; i < buf.Length; i++) buf[i] = new Color(s.baseColor.r, s.baseColor.g, s.baseColor.b, 1f);
            if (s.layers.Count == 0) return buf;
            bool pal = PaletteMode(s.def) && !glowMap;
            IEnumerable<Layer> list = s.def.frames
                ? new[] { s.layers[Mathf.Clamp(s.frame, 0, s.layers.Count - 1)] }
                : s.layers.Where(l => l.st.on && (!glowMap || l.InGlowMap));
            foreach (var L in list)
            {
                L.mask = L.mask ?? GetMask(L.path);
                DrawMask(buf, W, H, L.mask, 0, 0, Hex(L.st.c), L.st.o, ParseBlend(L.st.m));
                if (pal && !palette.alwaysOnTop && L.name == "tr/basic_palette") DrawPalettes(buf, W, H);
            }
            if (pal && palette.alwaysOnTop) DrawPalettes(buf, W, H);
            if (glowMap && PaletteMode(s.def) && glowCells != "Off") DrawGlowPalettes(buf, W, H);
            return buf;
        }

        void DrawPalettes(Color[] buf, int W, int H)
        {
            if (palette?.elements == null) return;
            foreach (var el in palette.elements)
            {
                if (el == null || el.colors == null || el.position < 1 || el.position > 12 || !PalTypes.TryGetValue(el.type, out var files)) continue;
                int col = (el.position - 1) % 4, row = (el.position - 1) / 4;
                int ox = 256 + col * 64, oy = H - (row + 1) * 64; // top right quarter, rows from the top
                for (int k = 0; k < files.Length && k < el.colors.Length; k++)
                {
                    var parts = files[k].Split(':');
                    DrawMask(buf, W, H, GetMask(PaletteFolder + "/" + parts[0]), ox, oy, Hex(el.colors[k]), 1f, SpriteBlend.Normal, true, parts.Length > 1);
                }
            }
        }

        /// <summary>
        /// Glow map: each palette's 64 x 64 place made see-through, then its cells in their colours, cut to their middle
        /// (<see cref="glowCells"/>), so only the middle of every cell glows.
        /// </summary>
        void DrawGlowPalettes(Color[] buf, int W, int H)
        {
            var order = glowPalette ?? palette;
            if (order?.elements == null) return;
            foreach (var el in order.elements)
            {
                if (el == null || el.colors == null || el.position < 1 || el.position > 12 || !PalTypes.TryGetValue(el.type, out var files)) continue;
                int col = (el.position - 1) % 4, row = (el.position - 1) / 4;
                int ox = 256 + col * 64, oy = H - (row + 1) * 64;
                for (int y = oy; y < oy + 64; y++)
                    for (int x = ox; x < ox + 64; x++)
                        if (x >= 0 && y >= 0 && x < W && y < H) buf[y * W + x] = Color.clear;
                // Which cell shows at each pixel at full size: a cell covered by a later one (2-x under 3-1-1) stays hidden.
                int n = Mathf.Min(files.Length, el.colors.Length);
                var cells = new RectInt[n];
                for (int i = 0; i < owner.Length; i++) owner[i] = -1;
                for (int k = 0; k < n; k++)
                {
                    var parts = files[k].Split(':');
                    var m = GetMask(PaletteFolder + "/" + parts[0]);
                    if (m == null || m.bb.width == 0) { cells[k] = new RectInt(0, 0, 0, 0); continue; }
                    bool rotate = parts.Length > 1; // turned like DrawMask turns it
                    cells[k] = rotate ? new RectInt(m.h - m.bb.yMax, m.bb.x, m.bb.height, m.bb.width) : m.bb;
                    for (int y = cells[k].yMin; y < cells[k].yMax; y++)
                        for (int x = cells[k].xMin; x < cells[k].xMax; x++)
                            if (x >= 0 && y >= 0 && x < 64 && y < 64) owner[y * 64 + x] = k;
                }
                for (int k = 0; k < n; k++)
                {
                    int x0 = cells[k].x, w = cells[k].width, y0 = cells[k].y, h = cells[k].height;
                    if (w == 0) continue;
                    Middle(ref x0, ref w);
                    Middle(ref y0, ref h);
                    var c = Hex(el.colors[k]);
                    c.a = 1f;
                    for (int y = y0; y < y0 + h; y++)
                        for (int x = x0; x < x0 + w; x++)
                        {
                            if (x < 0 || y < 0 || x >= 64 || y >= 64 || owner[y * 64 + x] != k) continue;
                            int dx = ox + x, dy = oy + y;
                            if (dx >= 0 && dy >= 0 && dx < W && dy < H) buf[dy * W + dx] = c;
                        }
                }
            }
        }

        readonly int[] owner = new int[64 * 64];

        /// <summary>A span cut to its middle as <see cref="glowCells"/> says.</summary>
        void Middle(ref int start, ref int length)
        {
            int keep = glowCells == "6 px" ? Mathf.Min(length, 6)
                     : glowCells == "Full" ? length
                     : Mathf.Max(1, Mathf.RoundToInt(length * 6f / 16f));
            start += (length - keep) / 2;
            length = keep;
        }

        /// <summary>The glow map's own Palette Order (empty text = the same as the Palette Order); null = fine, else what is wrong.</summary>
        public string ApplyGlowPaletteJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                glowPalette = null;
                glowPaletteJson = "";
                return null;
            }
            try
            {
                var p = JsonUtility.FromJson<SpritePaletteOrder>(json);
                if (p == null || p.elements == null) return "Glow palettes: not valid JSON (check the quotes and commas).";
                glowPalette = p;
                glowPaletteJson = json;
                return null;
            }
            catch
            {
                return "Glow palettes: not valid JSON (check the quotes and commas).";
            }
        }

        /// <summary>The atlas as a PNG file.</summary>
        public byte[] Png(GroupState s, bool glowMap)
        {
            var tex = new Texture2D(s.def.w, s.def.h, TextureFormat.RGBA32, false);
            tex.SetPixels(Compose(s, glowMap));
            tex.Apply();
            var png = tex.EncodeToPNG();
            UnityEngine.Object.Destroy(tex);
            return png;
        }

        /// <summary>Applies the Palette Order JSON; null = fine, else what is wrong.</summary>
        public string ApplyPaletteJson(string json)
        {
            try
            {
                var p = JsonUtility.FromJson<SpritePaletteOrder>(json);
                if (p == null || p.elements == null) return "Palette Order: not valid JSON (check the quotes and commas).";
                palette = p;
                paletteJson = json;
                return null;
            }
            catch
            {
                return "Palette Order: not valid JSON (check the quotes and commas).";
            }
        }

        public static string ExamplePaletteJson()
        {
            var c = "[\"#ff0000\",\"#00ff00\",\"#0000ff\",\"#ffff00\",\"#ff00ff\",\"#00ffff\",\"#ffddff\",\"#ddffff\"]";
            return "{\"alwaysOnTop\": true, \"elements\": [" +
                   string.Join(",", Enumerable.Range(0, 12).Select(i => $"{{\"position\": {i + 1}, \"type\": {i}, \"colors\": {c}}}")) + "]}";
        }

        // ---------------- Project files (same as the Texture Lab's) ----------------

        public string ProjectJson(GroupDef current)
        {
            var pf = new SpriteProjectFile { group = current.key, topRight = topRight, paletteOrderJson = paletteJson, glowPaletteJson = glowPaletteJson, glowCells = glowCells };
            foreach (var kv in states)
                pf.groups.Add(new SpriteGroupPreset
                {
                    key = kv.Key == "Enemy" && topRight == "Palette" ? "Enemy:Palette" : kv.Key,
                    hasBase = kv.Value.hasBase,
                    baseColor = "#" + ColorUtility.ToHtmlStringRGB(kv.Value.baseColor),
                    layers = kv.Value.layers.Select(l => l.st.Clone()).ToList()
                });
            return JsonUtility.ToJson(pf, true);
        }

        /// <summary>Opens a project file's text: the group it was on, or null when it is not a project.</summary>
        public GroupDef LoadProject(string json)
        {
            SpriteProjectFile pf;
            try { pf = JsonUtility.FromJson<SpriteProjectFile>(json); }
            catch { return null; }
            if (pf == null || pf.groups == null || pf.groups.Count == 0) return null;
            topRight = pf.topRight == "Palette" ? "Palette" : "Star";
            if (!string.IsNullOrEmpty(pf.paletteOrderJson)) ApplyPaletteJson(pf.paletteOrderJson);
            ApplyGlowPaletteJson(pf.glowPaletteJson);
            if (Array.IndexOf(GlowCellModes, pf.glowCells) >= 0) glowCells = pf.glowCells;
            var saved = presets;
            presets = new SpritePresetFile { groups = pf.groups };
            states.Clear();
            foreach (var g in Groups)
                if (pf.groups.Any(x => x != null && (x.key == g.key || (g.key == "Enemy" && x.key == "Enemy:Palette"))))
                    states[g.key] = BuildState(g, true);
            presets = saved;
            return FindGroup(pf.group) ?? Groups[0];
        }

        /// <summary>Top right Star or Palette (Enemy): its own Massif layers.</summary>
        public void SetTopRight(string value)
        {
            topRight = value == "Palette" ? "Palette" : "Star";
            Reset(FindGroup("Enemy"));
        }
    }
}
