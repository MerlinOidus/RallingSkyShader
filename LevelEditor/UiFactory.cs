using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RTXOn
{
    /// <summary>
    /// Small helpers that build uGUI objects in code: canvas, panels, labels, buttons, a vertical slider
    /// and a scrolling grid list. Positions and sizes are in canvas pixels (1920 x 1080 reference).
    /// </summary>
    public static class UiFactory
    {
        static Font font;
        /// <summary>The Main Menu's font when it has one (Main Menu > Menu Font), else Unity's built-in font.</summary>
        public static Font DefaultFont => MenuStyle.font != null ? MenuStyle.font : font != null ? font : (font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        /// <summary>
        /// Round corners like the Main Menu (its rounded sprite, 9-sliced). <paramref name="corner"/> 1 = 16 px corners,
        /// 2 = 8 px. The image's colour stays.
        /// </summary>
        public static Image Round(Image image, float corner = 1f)
        {
            if (image == null) return null;
            image.sprite = MenuStyle.Rounded;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = corner;
            return image;
        }

        public static Canvas MakeCanvas(string name, Vector2 referenceResolution, int sortingOrder = 0)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            // Expand: the canvas is never smaller than 1920 x 1080. Wider screens get more width (the side panels
            // hug the edges), taller ones (4:3, tablets) more height, so the panels never overlap the grid.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static RectTransform MakeRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>
        /// Pins a rect to one point of its parent. anchor and pivot are 0..1 (0,0 = bottom left),
        /// position is the offset from the anchor and size the width and height, in canvas pixels.
        /// </summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
            return rt;
        }

        /// <summary>Stretches a rect over its parent with margins (left, bottom, right, top).</summary>
        public static RectTransform Stretch(RectTransform rt, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <param name="raycast">True if the panel should catch clicks (buttons, the grid), false for pure decoration.</param>
        public static Image MakePanel(Transform parent, string name, Color color, bool raycast = false)
        {
            var image = MakeRect(parent, name).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        /// <summary>A text that fills its parent. Move or resize it with Place/Stretch afterwards.</summary>
        public static Text MakeLabel(Transform parent, string text, int size, TextAnchor align, Color color, FontStyle style = FontStyle.Normal)
        {
            var rt = MakeRect(parent, "Label");
            var t = rt.gameObject.AddComponent<Text>();
            t.font = DefaultFont;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = align;
            t.color = color;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            Stretch(rt);
            return t;
        }

        /// <summary>
        /// No keyboard navigation between UI elements: the arrow keys and W/S only scroll the grid,
        /// instead of also moving the selection from button to button.
        /// </summary>
        public static readonly Navigation NoNavigation = new Navigation { mode = Navigation.Mode.None };

        public static Button MakeButton(Transform parent, string label, Color color, UnityAction onClick, int fontSize = 30)
        {
            var image = Round(MakePanel(parent, label + " Button", color, true), 1.6f); // the menu's rounded look
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = NoNavigation;
            button.onClick.AddListener(onClick);
            MakeLabel(image.transform, label, fontSize, TextAnchor.MiddleCenter, TilePalette.TextColorOn(color), FontStyle.Bold);
            return button;
        }

        /// <summary>A vertical slider (bottom = 0) used as the grid's scroll bar. Works with mouse and touch.</summary>
        public static Slider MakeVerticalSlider(Transform parent, Color track, Color handle)
        {
            var background = MakePanel(parent, "Scroll Slider", track, true);
            var slider = background.gameObject.AddComponent<Slider>();

            var area = Stretch(MakeRect(background.transform, "Handle Area"), 0f, 35f, 0f, 35f); // keeps the handle inside the track
            var knob = MakePanel(area, "Handle", handle, true);
            knob.rectTransform.sizeDelta = new Vector2(0f, 70f); // full width, 70 tall; the slider moves its anchors

            slider.handleRect = knob.rectTransform;
            slider.targetGraphic = knob;
            slider.navigation = NoNavigation;
            slider.direction = Slider.Direction.BottomToTop;
            slider.wholeNumbers = true;
            slider.minValue = 0f;
            return slider;
        }

        /// <summary>A horizontal slider (left = min) with a filled part, for settings such as Speed.</summary>
        public static Slider MakeHorizontalSlider(Transform parent, Color track, Color fill, Color handle, float min, float max)
        {
            var background = MakePanel(parent, "Slider", track, true);
            var slider = background.gameObject.AddComponent<Slider>();

            var fillArea = Stretch(MakeRect(background.transform, "Fill Area"), 0f, 0f, 0f, 0f);
            var fillImage = MakePanel(fillArea, "Fill", fill);
            fillImage.rectTransform.sizeDelta = Vector2.zero;

            var area = Stretch(MakeRect(background.transform, "Handle Area"), 12f, 0f, 12f, 0f); // keeps the handle inside the track
            var knob = MakePanel(area, "Handle", handle, true);
            knob.rectTransform.sizeDelta = new Vector2(24f, 0f); // full height, 24 wide; the slider moves its anchors

            slider.fillRect = fillImage.rectTransform;
            slider.handleRect = knob.rectTransform;
            slider.targetGraphic = knob;
            slider.navigation = NoNavigation;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            return slider;
        }

        /// <summary>A box to type a value in. Read it in onEndEdit (Enter or clicking elsewhere).</summary>
        public static InputField MakeInputField(Transform parent, int fontSize, Color background, InputField.ContentType type)
        {
            var image = MakePanel(parent, "Input", background, true);
            var label = MakeLabel(image.transform, "", fontSize, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            Stretch(label.rectTransform, 6f, 2f, 6f, 2f);
            label.supportRichText = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            var field = image.gameObject.AddComponent<InputField>();
            field.textComponent = label;
            field.targetGraphic = image;
            field.navigation = NoNavigation;
            field.contentType = type;
            field.lineType = InputField.LineType.SingleLine;
            return field;
        }

        /// <summary>
        /// A scrolling list that lays its children out in a grid with a fixed number of columns.
        /// A scroll bar shows on the right only when the list is longer than the panel.
        /// </summary>
        public static ScrollRect MakeGridList(Transform parent, Vector2 cellSize, float spacing, int columns, Color barColor, out RectTransform content)
        {
            const float BarWidth = 16f;
            var root = MakePanel(parent, "List", new Color(0f, 0f, 0f, 0.2f), true); // raycast so the gaps scroll too
            var scroll = root.gameObject.AddComponent<ScrollRect>();

            // The ScrollRect sizes the viewport itself (AutoHideAndExpandViewport below): full width while the bar is
            // hidden, narrower while it shows. The top-left pivot makes it give up that room on the right.
            var viewport = Stretch(MakeRect(root.transform, "Viewport"));
            viewport.pivot = new Vector2(0f, 1f);
            viewport.gameObject.AddComponent<RectMask2D>();

            content = MakeRect(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = cellSize;
            grid.spacing = new Vector2(spacing, spacing);
            grid.padding = new RectOffset(8, 8, 8, 8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var track = MakePanel(root.transform, "Scrollbar", new Color(0f, 0f, 0f, 0.35f), true);
            var bar = track.gameObject.AddComponent<Scrollbar>();
            var barRect = track.rectTransform;
            barRect.anchorMin = new Vector2(1f, 0f);
            barRect.anchorMax = Vector2.one;
            barRect.pivot = new Vector2(1f, 0.5f);
            barRect.offsetMin = new Vector2(-BarWidth, 0f);
            barRect.offsetMax = Vector2.zero;
            var area = Stretch(MakeRect(track.transform, "Sliding Area"));
            var handle = MakePanel(area, "Handle", barColor, true);
            handle.rectTransform.sizeDelta = Vector2.zero; // the scrollbar sets the handle's anchors; zero size = exactly the anchors
            bar.handleRect = handle.rectTransform;
            bar.targetGraphic = handle;
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.navigation = NoNavigation;

            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            scroll.verticalScrollbarSpacing = 4f;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            return scroll;
        }

        // ---------------- Icons (UI/Resources/Icons, Adrian's pictures, 2026-10-23) ----------------

        static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();

        /// <summary>
        /// The picture UI/Resources/Icons/<paramref name="name"/>.png as a sprite (Play, Close, Trash, Gem, XboxA...),
        /// or null when the project has none (then the buttons keep their text).
        /// </summary>
        public static Sprite Icon(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (icons.TryGetValue(name, out var s) && s != null) return s;
            s = Resources.Load<Sprite>("Icons/" + name);
            if (s == null)
            {
                // Imported as a plain texture (before Editor/IconImport made it a sprite): make one from it.
                var tex = Resources.Load<Texture2D>("Icons/" + name);
                if (tex != null)
                {
                    s = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                    s.name = name;
                }
            }
            icons[name] = s;
            return s;
        }

        /// <summary>The red gem picture (Icons/Gem), else the white gem drawn in code. Show the picture untinted (white).</summary>
        public static Sprite GemIcon
        {
            get
            {
                var s = Icon("Gem");
                return s != null ? s : Gem;
            }
        }

        /// <summary>The gold crown picture (Icons/Crown), else the white crown drawn in code.</summary>
        public static Sprite CrownIcon
        {
            get
            {
                var s = Icon("Crown");
                return s != null ? s : Crown;
            }
        }

        /// <summary>True when <paramref name="sprite"/> is a coloured picture (gem, crown), shown white instead of tinted.</summary>
        public static bool IsPicture(Sprite sprite) => sprite != null && (sprite == Icon("Gem") || sprite == Icon("Crown"));

        /// <summary>The outline star (Icons/Star) for "not chosen", else the filled one.</summary>
        public static Sprite StarOutline
        {
            get
            {
                var s = Icon("Star");
                return s != null ? s : Star;
            }
        }

        /// <summary>
        /// Puts icon <paramref name="name"/> on <paramref name="button"/>: left of its text, or in the middle when the
        /// button has no text. The icon takes the text's colour unless <paramref name="tint"/> is given; it is turned by
        /// <paramref name="turn"/> degrees. Null (and the button unchanged) when there is no such icon.
        /// </summary>
        public static Image AddIcon(Button button, string name, float size, Color? tint = null, float turn = 0f)
        {
            var sprite = Icon(name);
            if (sprite == null || button == null) return null;
            var label = button.GetComponentInChildren<Text>();
            var image = MakePanel(button.transform, name + " Icon", tint ?? (label != null ? label.color : Color.white));
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            if (label == null || string.IsNullOrEmpty(label.text) || !label.enabled)
                Place(image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            else
            {
                Place(image.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(size, size));
                label.rectTransform.offsetMin = new Vector2(label.rectTransform.offsetMin.x + size + 18f, label.rectTransform.offsetMin.y);
            }
            image.rectTransform.localEulerAngles = new Vector3(0f, 0f, turn);
            return image;
        }

        /// <summary>Only icon <paramref name="name"/> on the button (an X button becomes the Close picture); unchanged when there is none.</summary>
        public static Image IconOnly(Button button, string name, float size, Color? tint = null)
        {
            if (Icon(name) == null || button == null) return null;
            var label = button.GetComponentInChildren<Text>();
            if (label != null) label.enabled = false;
            return AddIcon(button, name, size, tint);
        }

        // ---------------- Icons drawn in code (stars, gem, crown) ----------------

        static Sprite star, gem, crown, arrow;

        /// <summary>A white five-pointed star (Adrian's Icons/StarFilled when the project has it).</summary>
        public static Sprite Star
        {
            get
            {
                if (star != null) return star;
                var picture = Icon("StarFilled");
                if (picture != null) return star = picture;
                var pts = new Vector2[10];
                for (int i = 0; i < 10; i++)
                {
                    float a = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                    float r = i % 2 == 0 ? 0.5f : 0.21f;
                    pts[i] = new Vector2(0.5f + Mathf.Cos(a) * r, 0.47f + Mathf.Sin(a) * r);
                }
                return star = PolygonSprite("Star", pts);
            }
        }

        /// <summary>A white cut gem (diamond).</summary>
        public static Sprite Gem => gem != null ? gem : (gem = PolygonSprite("Gem", new[]
        {
            new Vector2(0.5f, 0.04f), new Vector2(0.96f, 0.6f), new Vector2(0.78f, 0.86f),
            new Vector2(0.22f, 0.86f), new Vector2(0.04f, 0.6f),
        }));

        /// <summary>A white crown.</summary>
        public static Sprite Crown => crown != null ? crown : (crown = PolygonSprite("Crown", new[]
        {
            new Vector2(0.1f, 0.12f), new Vector2(0.9f, 0.12f), new Vector2(0.98f, 0.82f), new Vector2(0.72f, 0.5f),
            new Vector2(0.5f, 0.92f), new Vector2(0.28f, 0.5f), new Vector2(0.02f, 0.82f),
        }));

        /// <summary>A white arrow pointing up; rotate it for the other directions.</summary>
        public static Sprite Arrow => arrow != null ? arrow : (arrow = PolygonSprite("Arrow", new[]
        {
            new Vector2(0.5f, 0.96f), new Vector2(0.94f, 0.5f), new Vector2(0.66f, 0.5f), new Vector2(0.66f, 0.06f),
            new Vector2(0.34f, 0.06f), new Vector2(0.34f, 0.5f), new Vector2(0.06f, 0.5f),
        }));

        /// <summary>A white shape (points 0..1, any order around it) with soft edges, as a sprite.</summary>
        static Sprite PolygonSprite(string name, Vector2[] pts)
        {
            const int Size = 128;
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    int inside = 0; // 4 samples per pixel for smooth edges
                    for (int s = 0; s < 4; s++)
                        if (Inside(pts, new Vector2((x + 0.25f + 0.5f * (s % 2)) / Size, (y + 0.25f + 0.5f * (s / 2)) / Size))) inside++;
                    px[y * Size + x] = new Color32(255, 255, 255, (byte)(inside * 255 / 4));
                }
            tex.SetPixels32(px);
            tex.Apply(true);
            var sprite = Sprite.Create(tex, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            return sprite;
        }

        static bool Inside(Vector2[] poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            return inside;
        }

        /// <summary>
        /// Five difficulty stars in a row, left to right from the parent's left edge: a dim star behind each lit one, so
        /// half stars show. Set them with <see cref="SetStars"/>.
        /// </summary>
        public static Image[] MakeStars(Transform parent, Vector2 position, float size, float gap, Color on, Color off)
        {
            var lit = new Image[5];
            for (int i = 0; i < 5; i++)
            {
                var back = MakePanel(parent, "Star " + (i + 1), off);
                back.sprite = Star;
                Place(back.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), position + new Vector2(i * (size + gap), 0f), new Vector2(size, size));
                var front = MakePanel(back.transform, "Lit", on);
                Stretch(front.rectTransform); // exactly over the dim star (a new rect would be 100 x 100)
                front.sprite = Star;
                front.type = Image.Type.Filled;
                front.fillMethod = Image.FillMethod.Horizontal;
                front.fillOrigin = (int)Image.OriginHorizontal.Left;
                lit[i] = front;
            }
            return lit;
        }

        /// <summary>Lights <paramref name="value"/> stars (3.5 = three and a half).</summary>
        public static void SetStars(Image[] stars, float value)
        {
            for (int i = 0; i < stars.Length; i++) stars[i].fillAmount = Mathf.Clamp01(value - i);
        }

        /// <summary>A grey text shown in an empty <paramref name="field"/>.</summary>
        public static void SetPlaceholder(InputField field, string text)
        {
            var label = MakeLabel(field.transform, text, field.textComponent.fontSize - 2, field.textComponent.alignment, new Color(1f, 1f, 1f, 0.35f), FontStyle.Italic);
            Stretch(label.rectTransform, 6f, 2f, 6f, 2f);
            field.placeholder = label;
        }
    }
}
