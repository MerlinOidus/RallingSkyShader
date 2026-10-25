using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RTXOn
{
    /// <summary>
    /// Press effect for every menu button (Adrian's design): shrinks to 0.86 while held and springs back. Chosen with
    /// the keyboard or a controller it grows a little, so you can see where you are. A close (X) button also turns 90°.
    /// </summary>
    public class MenuPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public float pressed = 0.86f;
        public bool spin;
        bool down, chosen;

        public static MenuPress Add(Component c, float pressed = 0.86f, bool spin = false)
        {
            if (c == null) return null;
            var p = c.GetComponent<MenuPress>();
            if (p == null) p = c.gameObject.AddComponent<MenuPress>();
            p.pressed = pressed;
            p.spin = spin;
            return p;
        }

        float Rest => chosen ? 1.08f : 1f;

        void Start()
        {
            // Shrink and turn round the button's middle: a corner pivot (a button placed from the screen's corner)
            // swung it off the screen. The rect stays where it is.
            if (!(transform is RectTransform rt) || rt.pivot == new Vector2(0.5f, 0.5f)) return;
            var size = rt.rect.size;
            var shift = new Vector2(0.5f, 0.5f) - rt.pivot;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition += Vector2.Scale(shift, size);
        }

        public void OnPointerDown(PointerEventData e)
        {
            down = true;
            MenuTween.Scale(transform, transform.localScale.x, pressed, 0.12f, MenuEase.OutQuad);
            if (spin)
                MenuTween.To(transform, "spin", 0.35f, MenuEase.OutBack,
                    x => transform.localRotation = Quaternion.Euler(0f, 0f, -90f * x), 0f,
                    () => transform.localRotation = Quaternion.identity); // an X turned 90° looks the same
        }

        public void OnPointerUp(PointerEventData e) => Release();
        public void OnPointerExit(PointerEventData e) => Release();

        void Release()
        {
            if (!down) return;
            down = false;
            MenuTween.Scale(transform, transform.localScale.x, Rest, 0.35f, MenuEase.OutBack);
        }

        public void OnSelect(BaseEventData e)
        {
            if (e is PointerEventData) return; // clicked: no "chosen" size
            chosen = true;
            MenuTween.Scale(transform, transform.localScale.x, Rest, 0.3f, MenuEase.OutBack);
        }

        public void OnDeselect(BaseEventData e)
        {
            if (!chosen) return;
            chosen = false;
            MenuTween.Scale(transform, transform.localScale.x, Rest, 0.25f, MenuEase.OutQuad);
        }

        void OnDisable()
        {
            down = chosen = false;
            transform.localScale = Vector3.one;
            transform.localRotation = Quaternion.identity;
        }
    }

    /// <summary>A picture that zooms 1.08 → 1.16 while the pointer is over it or presses it (level card thumbnails).</summary>
    public class MenuHoverZoom : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
    {
        public Transform picture;
        public float rest = 1.08f, zoom = 1.16f;

        public void OnPointerEnter(PointerEventData e) => Zoom(true);
        public void OnPointerDown(PointerEventData e) => Zoom(true);
        public void OnPointerExit(PointerEventData e) => Zoom(false);

        public void Zoom(bool on)
        {
            if (picture == null) return;
            MenuTween.Scale(picture, picture.localScale.x, on ? zoom : rest, 1.2f, MenuEase.OutQuint);
        }
    }

    /// <summary>The ON / OFF switch (216 x 80): a white knob that springs across, ON = Primary2, OFF = Panel +14%.</summary>
    public class MenuToggle : MonoBehaviour
    {
        public const float Width = 216f, Height = 80f;
        const float KnobOn = -54f, KnobOff = 50f;

        Image back;
        RectTransform knob;
        CanvasGroup onLabel, offLabel;
        Action<bool> changed;
        public bool IsOn { get; private set; }
        public Button Button { get; private set; }

        public static MenuToggle Make(Transform parent, bool value, Action<bool> changed)
        {
            var back = MenuStyle.Panel(parent, "Toggle", MenuSlot.Primary2, 1f, MenuStyle.Pill, true);
            var t = back.gameObject.AddComponent<MenuToggle>();
            t.back = back;
            t.changed = changed;
            t.Button = back.gameObject.AddComponent<Button>();
            t.Button.targetGraphic = back;
            t.Button.transition = Selectable.Transition.None;
            t.Button.onClick.AddListener(() => t.Set(!t.IsOn, true, true));

            var on = MenuStyle.Label(back.transform, "ON", 37, TextAnchor.MiddleCenter, Color.white, true);
            UiFactory.Place(on.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(KnobOff, 0f), new Vector2(100f, Height));
            t.onLabel = on.gameObject.AddComponent<CanvasGroup>();
            var off = MenuStyle.Label(back.transform, "OFF", 37, TextAnchor.MiddleCenter, MenuStyle.Hex("#C9D6DD"), true);
            UiFactory.Place(off.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(KnobOn, 0f), new Vector2(100f, Height));
            t.offLabel = off.gameObject.AddComponent<CanvasGroup>();

            var knob = MenuStyle.Panel(back.transform, "Knob", MenuSlot.Fixed, 1f, MenuStyle.Pill);
            t.knob = knob.rectTransform;
            UiFactory.Place(t.knob, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(99f, 67f));
            MenuPress.Add(t, 0.92f);
            t.Set(value, false, false);
            return t;
        }

        public void Set(bool on, bool animate, bool notify)
        {
            IsOn = on;
            var slot = on ? MenuSlot.Primary2 : MenuSlot.Panel;
            float lighten = on ? 0f : 0.14f;
            MenuStyle.Tint(back, slot, 1f, lighten, !animate);
            float x = on ? KnobOn : KnobOff;
            if (animate)
            {
                MenuTween.Move(knob, knob.anchoredPosition, new Vector2(x, 0f), 0.45f, MenuEase.OutBack);
                MenuTween.Color(back, back.color, MenuStyle.ColorOf(slot, 1f, lighten), 0.35f);
                MenuTween.Fade(onLabel, onLabel.alpha, on ? 1f : 0f, 0.3f, MenuEase.Linear);
                MenuTween.Fade(offLabel, offLabel.alpha, on ? 0f : 1f, 0.3f, MenuEase.Linear);
            }
            else
            {
                knob.anchoredPosition = new Vector2(x, 0f);
                onLabel.alpha = on ? 1f : 0f;
                offLabel.alpha = on ? 0f : 1f;
            }
            if (notify) changed?.Invoke(on);
        }
    }

    /// <summary>
    /// A row of choices (Quality, Camera, Language...): an Accent highlight with a glow springs to the chosen one.
    /// Fills the rect it is made in; each choice gets an equal share.
    /// </summary>
    public class MenuSegmented : MonoBehaviour
    {
        RectTransform highlight;
        Text[] labels;
        Action<int> changed;
        public Button[] Buttons { get; private set; }
        public int Index { get; private set; } = -1;

        public static MenuSegmented Make(RectTransform area, string[] options, int value, Action<int> changed)
        {
            var back = MenuStyle.Panel(area, "Segmented", MenuSlot.Panel, 1f);
            MenuStyle.Tint(back, MenuSlot.Panel, 1f, 0.08f);
            UiFactory.Stretch(back.rectTransform);
            var s = back.gameObject.AddComponent<MenuSegmented>();
            s.changed = changed;

            s.highlight = UiFactory.MakeRect(back.transform, "Highlight");
            var glow = MenuStyle.Panel(s.highlight, "Glow", MenuSlot.Accent, 0.45f, MenuStyle.Glow);
            UiFactory.Stretch(glow.rectTransform, -22f, -22f, -22f, -22f);
            var fill = MenuStyle.Panel(s.highlight, "Fill", MenuSlot.Accent, 1f);
            UiFactory.Stretch(fill.rectTransform);

            int n = Mathf.Max(1, options.Length);
            s.labels = new Text[n];
            s.Buttons = new Button[n];
            for (int i = 0; i < options.Length; i++)
            {
                int index = i;
                var cell = UiFactory.MakePanel(back.transform, options[i], Color.clear, true);
                var rt = cell.rectTransform;
                rt.anchorMin = new Vector2((float)i / n, 0f);
                rt.anchorMax = new Vector2((float)(i + 1) / n, 1f);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                var b = cell.gameObject.AddComponent<Button>();
                b.targetGraphic = cell;
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => s.Select(index, true, true));
                s.Buttons[i] = b;
                var label = MenuStyle.Label(cell.transform, options[i], 38, TextAnchor.MiddleCenter, MenuStyle.Soft, true);
                UiFactory.Stretch(label.rectTransform, 10f, 0f, 10f, 0f);
                s.labels[i] = MenuStyle.Fit(label, 18);
                MenuPress.Add(b, 0.92f);
            }
            s.Select(value, false, false);
            return s;
        }

        public void Select(int index, bool animate, bool notify)
        {
            int n = labels.Length;
            index = Mathf.Clamp(index, 0, n - 1);
            Index = index;
            Vector2 min = new Vector2((float)index / n, 0f), max = new Vector2((float)(index + 1) / n, 1f);
            highlight.offsetMin = new Vector2(6f, 6f);
            highlight.offsetMax = new Vector2(-6f, -6f);
            if (animate)
            {
                Vector2 a0 = highlight.anchorMin, a1 = highlight.anchorMax;
                MenuTween.To(highlight, "slide", 0.5f, MenuEase.OutBack, e =>
                {
                    highlight.anchorMin = Vector2.LerpUnclamped(a0, min, e);
                    highlight.anchorMax = Vector2.LerpUnclamped(a1, max, e);
                });
            }
            else
            {
                highlight.anchorMin = min;
                highlight.anchorMax = max;
            }
            for (int i = 0; i < n; i++)
            {
                if (i == index) MenuStyle.Tint(labels[i], MenuSlot.OnAccent);
                else
                {
                    MenuStyle.Untint(labels[i]);
                    labels[i].color = MenuStyle.Soft;
                }
            }
            if (notify) changed?.Invoke(index);
        }
    }

    /// <summary>
    /// &lt; VALUE &gt; for a setting with many choices (Quality on PC, Menu Music, Camera). Clicking the middle (or
    /// Enter / A on it) goes to the next one; the value pops when it changes.
    /// </summary>
    public class MenuStepper : MonoBehaviour
    {
        string[] options;
        Text value;
        Action<int> changed;
        public Button Button { get; private set; }
        public int Index { get; private set; }

        public static MenuStepper Make(RectTransform area, string[] options, int index, Action<int> changed)
        {
            var back = MenuStyle.Panel(area, "Stepper", MenuSlot.Panel, 1f, null, true);
            MenuStyle.Tint(back, MenuSlot.Panel, 1f, 0.08f);
            UiFactory.Stretch(back.rectTransform);
            var s = back.gameObject.AddComponent<MenuStepper>();
            s.options = options;
            s.changed = changed;
            s.Button = back.gameObject.AddComponent<Button>();
            s.Button.targetGraphic = back;
            s.Button.transition = Selectable.Transition.None;
            s.Button.onClick.AddListener(() => s.Step(1));

            s.value = MenuStyle.Label(back.transform, "", 38, TextAnchor.MiddleCenter, Color.white, true);
            UiFactory.Stretch(s.value.rectTransform, 90f, 0f, 90f, 0f);
            MenuStyle.Fit(s.value, 18);
            Arrow(back.rectTransform, s, -1);
            Arrow(back.rectTransform, s, 1);
            s.Set(index, false, false);
            return s;
        }

        static void Arrow(RectTransform back, MenuStepper s, int step)
        {
            var cell = MenuStyle.Panel(back, step < 0 ? "Previous" : "Next", MenuSlot.Accent, 1f, MenuStyle.Rounded, true);
            float x = step < 0 ? 0f : 1f;
            UiFactory.Place(cell.rectTransform, new Vector2(x, 0.5f), new Vector2(x, 0.5f), new Vector2(step < 0 ? 8f : -8f, 0f), new Vector2(72f, 64f));
            var b = cell.gameObject.AddComponent<Button>();
            b.targetGraphic = cell;
            b.transition = Selectable.Transition.None;
            b.navigation = UiFactory.NoNavigation;
            b.onClick.AddListener(() => s.Step(step));
            var icon = MenuStyle.Icon(cell.transform, "MapArrow", 40f, Color.white);
            if (icon != null)
            {
                icon.transform.localRotation = Quaternion.Euler(0f, 0f, step < 0 ? 90f : -90f); // the arrow points up
                MenuStyle.Tint(icon, MenuSlot.OnAccent);
            }
            else MenuStyle.Tint(MenuStyle.Label(cell.transform, step < 0 ? "<" : ">", 44, TextAnchor.MiddleCenter, Color.white, true), MenuSlot.OnAccent);
            MenuPress.Add(b, 0.86f);
        }

        void Step(int step) => Set((Index + step + options.Length) % options.Length, true, true);

        public void Set(int index, bool animate, bool notify)
        {
            Index = Mathf.Clamp(index, 0, options.Length - 1);
            value.text = options[Index];
            if (animate) MenuTween.Scale(value.transform, 1.3f, 1f, 0.35f, MenuEase.OutBack);
            if (notify) changed?.Invoke(Index);
        }
    }

    /// <summary>
    /// The menu slider: a 10 px track (Accent fill, white rest), a round 50 px handle that grows 1.35x while held,
    /// and its value on the right that bounces when let go.
    /// </summary>
    public class MenuSlider : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public Slider Slider { get; private set; }
        public Text Value { get; private set; }
        RectTransform handle;

        public static MenuSlider Make(RectTransform area, float min, float max)
        {
            var root = UiFactory.MakePanel(area, "Slider", Color.clear, true); // the whole height catches the finger
            UiFactory.Stretch(root.rectTransform, 0f, 0f, 140f, 0f);
            var s = root.gameObject.AddComponent<MenuSlider>();

            var track = MenuStyle.Panel(root.transform, "Track", MenuSlot.Fixed, 0.85f, MenuStyle.Pill);
            Bar(track.rectTransform, 0f);
            var fillArea = UiFactory.MakeRect(root.transform, "Fill Area");
            Bar(fillArea, 25f);
            var fill = MenuStyle.Panel(fillArea, "Fill", MenuSlot.Accent, 1f, MenuStyle.Pill);

            var area2 = UiFactory.MakeRect(root.transform, "Handle Area");
            area2.anchorMin = new Vector2(0f, 0.5f);
            area2.anchorMax = new Vector2(1f, 0.5f);
            area2.offsetMin = new Vector2(25f, -25f);
            area2.offsetMax = new Vector2(-25f, 25f);
            var knob = MenuStyle.Panel(area2, "Handle", MenuSlot.Fixed, 1f, MenuStyle.Circle, true);
            knob.type = Image.Type.Simple;
            knob.rectTransform.sizeDelta = new Vector2(50f, 0f);
            var shadow = knob.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
            shadow.effectDistance = new Vector2(0f, -4f);
            s.handle = knob.rectTransform;

            var slider = s.Slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = knob.rectTransform;
            slider.targetGraphic = knob;
            slider.transition = Selectable.Transition.None;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            // The fill's right end is the handle's middle; its left end reaches the track's left end.
            fill.rectTransform.offsetMin = new Vector2(-25f, 0f);
            fill.rectTransform.offsetMax = Vector2.zero;

            s.Value = MenuStyle.Label(area, "", 39, TextAnchor.MiddleRight, MenuStyle.Soft, true);
            UiFactory.Place(s.Value.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(130f, 80f));
            return s;
        }

        static void Bar(RectTransform rt, float inset)
        {
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset, -5f);
            rt.offsetMax = new Vector2(-inset, 5f);
        }

        public void OnPointerDown(PointerEventData e) => MenuTween.Scale(handle, handle.localScale.x, 1.35f, 0.3f, MenuEase.OutBack);

        public void OnPointerUp(PointerEventData e)
        {
            MenuTween.Scale(handle, handle.localScale.x, 1f, 0.3f, MenuEase.OutBack);
            MenuTween.Scale(Value.transform, 1.3f, 1f, 0.35f, MenuEase.OutBack);
        }
    }

    /// <summary>The dark capsule message near the bottom ("Segera hadir"...), gone after 1.6 s.</summary>
    public static class MenuToast
    {
        /// <summary>Where toasts show (the menu's top layer). Toasts are only logged while it is missing.</summary>
        public static RectTransform layer;
        static RectTransform current;

        public static void Show(string text)
        {
            if (layer == null) return;
            if (current != null) UnityEngine.Object.Destroy(current.gameObject);
            var pill = UiFactory.MakePanel(layer, "Toast", MenuStyle.ToastColor);
            pill.sprite = MenuStyle.Pill;
            pill.type = Image.Type.Sliced;
            var rt = current = pill.rectTransform;
            var label = MenuStyle.Label(rt, text, 43, TextAnchor.MiddleCenter, Color.white);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            float maxWidth = Mathf.Max(400f, layer.rect.width - 120f);
            float width = Mathf.Min(maxWidth, label.preferredWidth + 120f);
            UiFactory.Stretch(label.rectTransform, 60f, 0f, 60f, 0f);
            float height = text.Length * 43f * 0.55f > width - 120f ? 170f : 110f;
            UiFactory.Place(rt, new Vector2(0.5f, 0.26f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, height));
            var group = rt.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            MenuTween.Move(rt, new Vector2(0f, -43f), Vector2.zero, 0.5f, MenuEase.OutBack);
            MenuTween.Scale(rt, 0.9f, 1f, 0.5f, MenuEase.OutBack);
            var shown = rt;
            MenuTween.Fade(group, 1f, 0f, 0.3f, MenuEase.Linear, 1.6f, () =>
            {
                if (shown != null) UnityEngine.Object.Destroy(shown.gameObject);
            });
            group.alpha = 0f;
            MenuTween.To(group, "in", 0.25f, MenuEase.Linear, e => group.alpha = e);
        }
    }
}
