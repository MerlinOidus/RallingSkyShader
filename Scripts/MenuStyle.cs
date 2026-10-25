using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RTXOn
{
    /// <summary>The four colour sets of Adrian's menu design (rolling-sky-ui-unity.md, 2026-10-24).</summary>
    public enum MenuColors { Original, RTXGreen, SunsetSky, TealCoral }

    /// <summary>The colour tokens of a set: what each part of the menu is painted with.</summary>
    public enum MenuSlot { Primary, Primary2, Accent, OnAccent, Panel, CardBody, Page, Ink, Fixed }

    /// <summary>
    /// Look of the Main Menu: colour sets and tokens, rounded sprites made in code (uGUI has no border radius),
    /// fonts, the menu language (Indonesia / English) and the graphics that follow the colour set
    /// (<see cref="Tint"/>, changed with a 0.8 s blend by <see cref="UseColors"/>).
    /// </summary>
    public static class MenuStyle
    {
        const string ColorsKey = "RTXOn.MenuColors", LanguageKey = "RTXOn.Language";

        public static readonly string[] ColorNames = { "ORIGINAL", "RTX GREEN", "SUNSET SKY", "TEAL CORAL" };
        public static readonly string[] LanguageNames = { "INDONESIA", "ENGLISH" };

        // Columns: Original, RTX Green, Sunset Sky, Teal Coral.
        static readonly string[][] Table =
        {
            new[] { "#1D6B0B", "#1C2410", "#2E2657", "#0E6676" }, // Primary: title bar, navbar, section headers
            new[] { "#278A10", "#4F8A00", "#E8505B", "#13A3B4" }, // Primary2: toggle on, plain buttons
            new[] { "#46D81C", "#76B900", "#FFC93C", "#FF6B6B" }, // Accent: active tab, slider, chosen segment
            new[] { "#FFFFFF", "#0B1205", "#3A2400", "#FFFFFF" }, // OnAccent: text / icon on Accent
            new[] { "#0D1A23", "#0C1013", "#1C1838", "#0A2730" }, // Panel: settings sheet
            new[] { "#27C4E2", "#1A2229", "#FFF0CC", "#C4F1F6" }, // CardBody: level card
            new[] { "#FFFFFF", "#0F1418", "#FBF4EA", "#F1FAFB" }, // Page: level list background
            new[] { "#06222C", "#E6F4CF", "#3B2A12", "#07343C" }, // Ink: text on CardBody
        };

        public static readonly Color Soft = Hex("#9FB6C3");
        public static readonly Color GemGold = Hex("#FFE25A");
        /// <summary>The small difficulty stars (lit part).</summary>
        public static readonly Color StarYellow = Hex("#FFD21F");
        public static readonly Color MetaInk = Hex("#0B2A35");
        public static readonly Color ToastColor = new Color(0.039f, 0.094f, 0.125f, 0.92f);

        /// <summary>Optional fonts (Main Menu > Font / Number Font). Empty = Unity's built-in font.</summary>
        public static Font font, numberFont;

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        public static MenuColors Colors
        {
            get => (MenuColors)Mathf.Clamp(PlayerPrefs.GetInt(ColorsKey, (int)MenuColors.RTXGreen), 0, 3);
            set { PlayerPrefs.SetInt(ColorsKey, (int)value); PlayerPrefs.Save(); }
        }

        public static bool English
        {
            get => PlayerPrefs.GetInt(LanguageKey, 0) == 1;
            set { PlayerPrefs.SetInt(LanguageKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>The menu's text in the chosen language.</summary>
        public static string T(string indonesian, string english) => English ? english : indonesian;

        public static Color Get(MenuSlot slot) => Get(Colors, slot);

        public static Color Get(MenuColors set, MenuSlot slot) =>
            slot == MenuSlot.Fixed ? Color.white : Hex(Table[(int)slot][(int)set]);

        /// <summary>A colour made lighter by <paramref name="amount"/> (0..1), like the design's "Panel +14%".</summary>
        public static Color Lighten(Color c, float amount) => Color.Lerp(c, Color.white, amount);

        // ---------------- Graphics that follow the colour set ----------------

        class Tinted
        {
            public Graphic graphic;
            public MenuSlot slot;
            public float alpha, lighten;
        }

        static readonly List<Tinted> tinted = new List<Tinted>();

        /// <summary>Paints <paramref name="g"/> with a token now, and again when the colour set changes.</summary>
        /// <param name="apply">False: only remember the token (the caller blends to <see cref="ColorOf(MenuSlot, float, float)"/> itself).</param>
        public static T Tint<T>(T g, MenuSlot slot, float alpha = 1f, float lighten = 0f, bool apply = true) where T : Graphic
        {
            if (g == null) return g;
            tinted.RemoveAll(t => t.graphic == null || t.graphic == g);
            var entry = new Tinted { graphic = g, slot = slot, alpha = alpha, lighten = lighten };
            tinted.Add(entry);
            if (apply) g.color = ColorOf(entry, Colors);
            return g;
        }

        /// <summary>A token's colour in the current set, lightened and with alpha.</summary>
        public static Color ColorOf(MenuSlot slot, float alpha = 1f, float lighten = 0f) =>
            ColorOf(new Tinted { slot = slot, alpha = alpha, lighten = lighten }, Colors);

        /// <summary>Stops following the colour set (the graphic keeps its colour).</summary>
        public static void Untint(Graphic g) => tinted.RemoveAll(t => t.graphic == null || t.graphic == g);

        static Color ColorOf(Tinted t, MenuColors set)
        {
            var c = Get(set, t.slot);
            if (t.lighten > 0f) c = Lighten(c, t.lighten);
            c.a *= t.alpha;
            return c;
        }

        /// <summary>Switches the colour set: every tinted graphic blends to its new colour in 0.8 s.</summary>
        public static void UseColors(MenuColors set)
        {
            Colors = set;
            tinted.RemoveAll(t => t.graphic == null);
            foreach (var t in tinted)
                MenuTween.Color(t.graphic, t.graphic.color, ColorOf(t, set), 0.8f, MenuEase.InOutCubic);
        }

        public static void ForgetTints() => tinted.Clear();

        // ---------------- Sprites made in code ----------------

        static Sprite rounded, pill, circle, glow, fadeUp;

        /// <summary>White 64x64 square with 16 px round corners, 9-sliced (border 20): panels, buttons, cards, bars.</summary>
        public static Sprite Rounded => rounded != null ? rounded : (rounded = RoundedSprite("Menu Rounded", 64, 16f, 20, 0f));

        /// <summary>A capsule (fully round ends), 9-sliced: toggles, knobs, progress bars, toasts.</summary>
        public static Sprite Pill => pill != null ? pill : (pill = RoundedSprite("Menu Pill", 64, 31.5f, 31, 0f));

        /// <summary>A white circle (slider handle, particles, ripples).</summary>
        public static Sprite Circle => circle != null ? circle : (circle = RoundedSprite("Menu Circle", 128, 63.5f, 0, 0f));

        /// <summary>A soft glow round a rounded box, 9-sliced: behind the navbar indicator and the banner.</summary>
        public static Sprite Glow => glow != null ? glow : (glow = RoundedSprite("Menu Glow", 96, 20f, 44, 24f));

        /// <summary>Black at the bottom fading to clear at the top (the banner's "tap to play" strip).</summary>
        public static Sprite FadeUp
        {
            get
            {
                if (fadeUp != null) return fadeUp;
                var tex = new Texture2D(4, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Menu Fade Up" };
                for (int y = 0; y < 64; y++)
                {
                    float a = Mathf.Pow(1f - y / 63f, 1.6f) * 0.85f;
                    for (int x = 0; x < 4; x++) tex.SetPixel(x, y, new Color(0f, 0f, 0f, a));
                }
                tex.Apply(false, true);
                fadeUp = Sprite.Create(tex, new Rect(0, 0, 4, 64), new Vector2(0.5f, 0.5f), 100f);
                fadeUp.name = tex.name;
                return fadeUp;
            }
        }

        /// <param name="soft">0 = sharp edge; above 0 = a blurred edge that wide (glow).</param>
        static Sprite RoundedSprite(string name, int size, float radius, int border, float soft)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = name };
            var px = new Color32[size * size];
            float inset = soft;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Distance outside a rounded box (negative inside).
                float cx = Mathf.Abs(x + 0.5f - size * 0.5f) - (size * 0.5f - inset - radius);
                float cy = Mathf.Abs(y + 0.5f - size * 0.5f) - (size * 0.5f - inset - radius);
                float outside = new Vector2(Mathf.Max(cx, 0f), Mathf.Max(cy, 0f)).magnitude + Mathf.Min(Mathf.Max(cx, cy), 0f) - radius;
                float a = soft > 0f
                    ? Mathf.Clamp01(1f - outside / soft) * Mathf.Clamp01(1f - outside / soft)
                    : Mathf.Clamp01(0.5f - outside);
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
            sprite.name = name;
            return sprite;
        }

        // ---------------- Building blocks ----------------

        /// <summary>A rounded panel (sliced) painted with a token.</summary>
        public static Image Panel(Transform parent, string name, MenuSlot slot, float alpha = 1f, Sprite sprite = null, bool raycast = false)
        {
            var image = UiFactory.MakePanel(parent, name, Color.white, raycast);
            image.sprite = sprite != null ? sprite : Rounded;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
            Tint(image, slot, alpha);
            return image;
        }

        /// <summary>A text in the menu font (or the number font for big numbers).</summary>
        public static Text Label(Transform parent, string text, float size, TextAnchor align, Color color, bool bold = false, bool number = false)
        {
            var t = UiFactory.MakeLabel(parent, text, Mathf.Max(1, Mathf.RoundToInt(size)), align, color, bold ? FontStyle.Bold : FontStyle.Normal);
            var f = number && numberFont != null ? numberFont : font;
            if (f != null) t.font = f;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        /// <summary>An icon (UI/Resources/Icons) as an Image, or null when the project has none.</summary>
        public static Image Icon(Transform parent, string name, float size, Color color)
        {
            var sprite = UiFactory.Icon(name);
            if (sprite == null) return null;
            var image = UiFactory.MakePanel(parent, name, color);
            image.sprite = sprite;
            image.preserveAspect = true;
            UiFactory.Place(image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            return image;
        }

        /// <summary>
        /// Shows <paramref name="tex"/> inside <paramref name="frame"/> filling it, cropped not stretched (like CSS
        /// object-fit: cover), at any size the frame gets later (<see cref="MenuCover"/>). Returns the picture.
        /// </summary>
        public static RawImage Cover(RectTransform frame, Texture tex)
        {
            if (frame.GetComponent<RectMask2D>() == null && frame.GetComponent<Mask>() == null) frame.gameObject.AddComponent<RectMask2D>();
            var image = UiFactory.MakeRect(frame, "Picture").gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            UiFactory.Stretch(image.rectTransform);
            image.gameObject.AddComponent<MenuCover>().Show(tex);
            return image;
        }

        /// <summary>A rounded frame (Mask on a rounded sprite) with <paramref name="tex"/> covering it.</summary>
        public static RawImage RoundedCover(RectTransform frame, Texture tex, Sprite shape = null)
        {
            var shapeImage = frame.GetComponent<Image>();
            if (shapeImage == null) shapeImage = frame.gameObject.AddComponent<Image>();
            shapeImage.sprite = shape != null ? shape : Rounded;
            shapeImage.type = Image.Type.Sliced;
            shapeImage.color = Color.white;
            if (frame.GetComponent<Mask>() == null) frame.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            return Cover(frame, tex);
        }

        // ---------------- Pictures of words (Adrian's Kirsty titles) ----------------

        /// <summary>
        /// A fixed word as a picture from UI/Resources/MenuText (Kirsty PNGs Adrian exports from Illustrator, white on
        /// clear): <paramref name="key"/>_EN.png in English, else <paramref name="key"/>.png when that picture says the
        /// same in both languages. Null = no picture: show the text.
        /// </summary>
        public static Texture2D WordPicture(string key, string indonesian, string english)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (English)
            {
                var en = Resources.Load<Texture2D>("MenuText/" + key + "_EN");
                if (en != null) return en;
                if (indonesian != english) return null;
            }
            return Resources.Load<Texture2D>("MenuText/" + key);
        }

        /// <summary>
        /// A title: the word picture <paramref name="key"/> when the project has it (as tall as the text would be),
        /// else a text. Either one fills <paramref name="parent"/>'s rect, centred.
        /// </summary>
        public static Graphic Title(Transform parent, string key, string indonesian, string english, float size, TextAnchor align, Color color, bool bold = false)
        {
            var pic = WordPicture(key, indonesian, english);
            if (pic == null) return Label(parent, T(indonesian, english), size, align, color, bold);
            var image = UiFactory.MakeRect(parent, key).gameObject.AddComponent<RawImage>();
            image.texture = pic;
            image.color = color;
            image.raycastTarget = false;
            var rt = image.rectTransform;
            float h = size * 1.1f, w = h * pic.width / Mathf.Max(1f, pic.height);
            float x = align == TextAnchor.MiddleLeft || align == TextAnchor.UpperLeft || align == TextAnchor.LowerLeft ? 0f
                : align == TextAnchor.MiddleRight || align == TextAnchor.UpperRight || align == TextAnchor.LowerRight ? 1f : 0.5f;
            UiFactory.Place(rt, new Vector2(x, 0.5f), new Vector2(x, 0.5f), Vector2.zero, new Vector2(w, h));
            return image;
        }

        /// <summary>A text that shrinks (down to <paramref name="min"/>) to fit its rect on one line.</summary>
        public static Text Fit(Text t, int min)
        {
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = Mathf.Min(min, t.fontSize);
            t.resizeTextMaxSize = t.fontSize;
            return t;
        }
    }

    /// <summary>
    /// Crops a RawImage's picture so it fills the RawImage without stretching (cover), again whenever its size
    /// changes (a picture flying from a level card to the Detail banner).
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class MenuCover : MonoBehaviour
    {
        RawImage image;
        Vector2 lastSize;

        public void Show(Texture tex)
        {
            if (image == null) image = GetComponent<RawImage>();
            image.texture = tex;
            lastSize = Vector2.zero;
            Crop();
        }

        void LateUpdate() => Crop();

        void Crop()
        {
            if (image == null) image = GetComponent<RawImage>();
            var size = image.rectTransform.rect.size;
            if (size == lastSize) return;
            lastSize = size;
            var tex = image.texture;
            if (tex == null || size.x <= 0f || size.y <= 0f || tex.height <= 0)
            {
                image.uvRect = new Rect(0f, 0f, 1f, 1f);
                return;
            }
            float box = size.x / size.y, pic = (float)tex.width / tex.height;
            image.uvRect = pic > box
                ? new Rect((1f - box / pic) * 0.5f, 0f, box / pic, 1f)
                : new Rect(0f, (1f - pic / box) * 0.5f, 1f, pic / box);
        }
    }
}
