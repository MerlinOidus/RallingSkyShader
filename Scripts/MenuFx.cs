using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RTXOn
{
    /// <summary>
    /// Main Menu button feel: grows a little under the mouse or when chosen with the keyboard / controller, dips when
    /// pressed. The mouse also moves the controller's choice, so only one button is ever raised.
    /// </summary>
    public class MenuButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        public float hoverScale = 1.06f;
        bool hover, selected, down;
        float scale = 1f;

        public static MenuButtonFx Add(Component c, float hoverScale = 1.06f)
        {
            var fx = c.gameObject.GetComponent<MenuButtonFx>();
            if (fx == null) fx = c.gameObject.AddComponent<MenuButtonFx>(); // not ??: Unity's null for a missing component
            fx.hoverScale = hoverScale;
            return fx;
        }

        public void OnPointerEnter(PointerEventData e)
        {
            hover = true;
            var s = GetComponent<Selectable>();
            // Only controls the keyboard / controller can move between (not the Level List's buttons, which Enter / A
            // would otherwise press as well as playing the level).
            if (s != null && s.interactable && s.navigation.mode != Navigation.Mode.None && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(gameObject);
        }

        public void OnPointerExit(PointerEventData e) { hover = false; down = false; }
        public void OnSelect(BaseEventData e) => selected = true;
        public void OnDeselect(BaseEventData e) => selected = false;
        public void OnPointerDown(PointerEventData e) => down = true;
        public void OnPointerUp(PointerEventData e) => down = false;

        void OnDisable()
        {
            hover = selected = down = false;
            scale = 1f;
            transform.localScale = Vector3.one;
        }

        void Update()
        {
            float want = down ? 0.96f : hover || selected ? hoverScale : 1f;
            scale = Mathf.Lerp(scale, want, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }

    /// <summary>Slides a menu element in from an offset while it fades in, after a delay (pages opening, the title's buttons).</summary>
    public class MenuSlideIn : MonoBehaviour
    {
        public Vector2 from = new Vector2(140f, 0f);
        public float seconds = 0.5f;

        RectTransform rt;
        CanvasGroup group;
        Vector2 home;
        float start, delay;

        public static MenuSlideIn Add(Component c, Vector2 from, float seconds = 0.5f)
        {
            var s = c.gameObject.GetComponent<MenuSlideIn>();
            if (s == null) s = c.gameObject.AddComponent<MenuSlideIn>();
            s.from = from;
            s.seconds = seconds;
            s.rt = (RectTransform)c.transform;
            s.home = s.rt.anchoredPosition;
            s.group = c.gameObject.GetComponent<CanvasGroup>();
            if (s.group == null) s.group = c.gameObject.AddComponent<CanvasGroup>();
            s.enabled = false;
            return s;
        }

        /// <summary>The element was moved (the Main Menu turned portrait / landscape): it slides in to here now.</summary>
        public void SetHome(Vector2 position)
        {
            home = position;
            if (!enabled) rt.anchoredPosition = home;
        }

        public void Play(float delayed)
        {
            delay = delayed;
            start = Time.unscaledTime;
            enabled = true;
            Apply(0f);
        }

        void Update()
        {
            float u = Mathf.Clamp01((Time.unscaledTime - start - delay) / Mathf.Max(0.01f, seconds));
            Apply(u);
            if (u >= 1f) enabled = false;
        }

        void Apply(float u)
        {
            // Ease out with a small overshoot, so it lands with a little bounce.
            const float c1 = 1.4f, c3 = c1 + 1f;
            float e = u <= 0f ? 0f : 1f + c3 * Mathf.Pow(u - 1f, 3f) + c1 * Mathf.Pow(u - 1f, 2f);
            rt.anchoredPosition = home + from * (1f - e);
            group.alpha = Mathf.Clamp01(u * 2f);
        }
    }

    /// <summary>
    /// The Main Menu's moving background: a slowly shifting gradient with tiles that drift up and turn, in the
    /// colours of the logo (sky blue, purple, the yellow tile, the red ball).
    /// </summary>
    public class MenuBackdrop : MonoBehaviour
    {
        public int tiles = 34;
        public Color top = new Color(0.07f, 0.07f, 0.2f);
        public Color bottom = new Color(0.2f, 0.09f, 0.32f);

        static readonly Color[] TileColors =
        {
            new Color(0.35f, 0.75f, 1f), new Color(0.6f, 0.45f, 1f), new Color(1f, 0.78f, 0.2f), new Color(0.95f, 0.3f, 0.35f),
        };

        struct Tile { public RectTransform rt; public Image image; public float speed, spin, size, x, phase, alpha; }

        Tile[] items;
        RawImage gradient;
        Texture2D texture;
        RectTransform area;

        void Start()
        {
            area = (RectTransform)transform;
            gradient = UiFactory.MakeRect(transform, "Gradient").gameObject.AddComponent<RawImage>();
            gradient.raycastTarget = false;
            UiFactory.Stretch(gradient.rectTransform);
            texture = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Menu Gradient" };
            Paint(0f);
            gradient.texture = texture;

            items = new Tile[tiles];
            for (int i = 0; i < tiles; i++)
            {
                var image = UiFactory.MakePanel(transform, "Tile", Color.white);
                float r = Hash(i, 1);
                items[i] = new Tile
                {
                    rt = image.rectTransform,
                    image = image,
                    size = Mathf.Lerp(26f, 120f, r * r),
                    speed = Mathf.Lerp(18f, 70f, Hash(i, 2)),
                    spin = Mathf.Lerp(-25f, 25f, Hash(i, 3)),
                    x = Hash(i, 4),
                    phase = Hash(i, 5),
                    alpha = Mathf.Lerp(0.05f, 0.2f, Hash(i, 6)),
                };
                image.color = TileColors[i % TileColors.Length];
                image.rectTransform.anchorMin = image.rectTransform.anchorMax = Vector2.zero;
                image.rectTransform.sizeDelta = Vector2.one * items[i].size;
            }
        }

        void OnDestroy()
        {
            if (texture != null) Destroy(texture);
        }

        static float Hash(int i, int salt)
        {
            uint h = (uint)(i * 73856093) ^ (uint)(salt * 19349663);
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            return (h & 0xffff) / 65535f;
        }

        /// <summary>The gradient, its colours slowly breathing.</summary>
        void Paint(float t)
        {
            float w = 0.5f + 0.5f * Mathf.Sin(t * 0.25f);
            var a = Color.Lerp(top, new Color(0.05f, 0.12f, 0.26f), w);
            var b = Color.Lerp(bottom, new Color(0.1f, 0.06f, 0.24f), w);
            for (int y = 0; y < 64; y++) texture.SetPixel(0, y, Color.Lerp(b, a, y / 63f));
            texture.Apply(false);
        }

        void Update()
        {
            float t = Time.unscaledTime;
            if (texture != null && Time.frameCount % 4 == 0) Paint(t);
            if (items == null) return;
            var size = area.rect.size;
            float h = size.y + 240f;
            for (int i = 0; i < items.Length; i++)
            {
                var it = items[i];
                float y = Mathf.Repeat(it.phase * h + t * it.speed, h) - 120f;
                float x = it.x * size.x + Mathf.Sin(t * 0.4f + it.phase * 6.28f) * 30f;
                it.rt.anchoredPosition = new Vector2(x, y);
                it.rt.localRotation = Quaternion.Euler(0f, 0f, 45f + t * it.spin);
                var c = it.image.color;
                // Fades in at the bottom and out at the top.
                float edge = Mathf.Clamp01(Mathf.Min(y + 120f, h - 120f - y) / 220f);
                c.a = it.alpha * edge;
                it.image.color = c;
            }
        }
    }
}
