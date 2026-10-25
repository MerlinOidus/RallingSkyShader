using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RTXOn
{
    /// <summary>
    /// The PC / Xbox look (Adrian's NewUiForPc dan Xbox.pdf, 2026-10-25): a row of square buttons where the chosen one
    /// is bigger and green (<see cref="PcButtonRow"/>), and a bar along the bottom that says what the chosen button
    /// does, with the controller's Select button on the right when a controller is plugged in (<see cref="PcHintBar"/>).
    /// Used by the PC Main Menu, the Pause screen and the Complete screen. Android keeps the phone screens.
    /// </summary>
    public static class PcMenu
    {
        /// <summary>The PC screens are used: a computer or console (not a phone or tablet).</summary>
        public static bool On => !GraphicsQuality.Mobile;

        public static readonly Color ButtonColor = MenuStyle.Hex("#6E9A72");
        public static readonly Color ChosenColor = MenuStyle.Hex("#3DBB44");
        public static readonly Color IconColor = MenuStyle.Hex("#F2EFE8");
        public static readonly Color LightBar = new Color(0.62f, 0.62f, 0.62f, 0.94f);
        public static readonly Color DarkBar = MenuStyle.Hex("#565C56");
        public static readonly Color DarkInk = MenuStyle.Hex("#1E1E1E");

        /// <summary>Select: Enter, Space, or A / X on a controller (Adrian's design shows X).</summary>
        public static bool SelectPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame)) return true;
            var g = Gamepad.current;
            return g != null && (g.buttonSouth.wasPressedThisFrame || g.buttonWest.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space) ||
                   Input.GetKeyDown(KeyCode.JoystickButton0) || Input.GetKeyDown(KeyCode.JoystickButton2);
#endif
        }

        /// <summary>A controller is plugged in: the hint bar shows its buttons.</summary>
        public static bool HasPad
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Gamepad.current != null;
#else
                return Input.GetJoystickNames().Length > 0;
#endif
            }
        }
    }

    /// <summary>
    /// A row of square buttons (PDF pages 1, 3, 4): the chosen one is bigger, bright green with a dark edge; Left / Right
    /// (arrows, A / D, D-pad, left stick) choose, Select presses it, the mouse chooses by pointing and presses by
    /// clicking. The hint bar says what the chosen one does.
    /// </summary>
    public class PcButtonRow : MonoBehaviour
    {
        public class Item
        {
            public string name, icon, hintId, hintEn;
            public Action click;
            public bool enabled = true;

            public Item(string name, string icon, string hintId, string hintEn, Action click)
            {
                this.name = name;
                this.icon = icon;
                this.hintId = hintId;
                this.hintEn = hintEn;
                this.click = click;
            }
        }

        /// <summary>Reads the keys and controller itself (Pause, Complete); the Main Menu passes them on instead.</summary>
        public bool ownInput = true;
        /// <summary>Off: no keys, no clicks (another window is open over it).</summary>
        public bool interactable = true;

        readonly List<Item> items = new List<Item>();
        readonly List<RectTransform> cells = new List<RectTransform>();
        readonly List<Image> backs = new List<Image>();
        readonly List<Outline> edges = new List<Outline>();
        PcHintBar hint;
        float size, chosenSize, gap;
        int focus = -1;

        public int Focus => focus;

        /// <summary>
        /// Makes the row in <paramref name="parent"/> with its middle at <paramref name="centre"/> (anchored at the bottom
        /// middle). <paramref name="chosen"/> is chosen first.
        /// </summary>
        public static PcButtonRow Build(RectTransform parent, Vector2 anchor, Vector2 centre, IList<Item> list, int chosen, PcHintBar hintBar, float buttonSize = 120f)
        {
            var rowRt = UiFactory.MakeRect(parent, "Button Row");
            float chosenSize = buttonSize * 1.16f, gap = 0f;
            float width = (list.Count - 1) * buttonSize + chosenSize + gap * (list.Count - 1);
            UiFactory.Place(rowRt, anchor, new Vector2(0.5f, 0.5f), centre, new Vector2(width, chosenSize));
            var row = rowRt.gameObject.AddComponent<PcButtonRow>();
            row.size = buttonSize;
            row.chosenSize = chosenSize;
            row.gap = gap;
            row.hint = hintBar;
            foreach (var item in list) row.Add(item);
            row.Choose(Mathf.Clamp(chosen, 0, list.Count - 1), false);
            return row;
        }

        void Add(Item item)
        {
            int index = items.Count;
            items.Add(item);
            var back = UiFactory.MakePanel(transform, item.name, PcMenu.ButtonColor, true);
            var rt = back.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            var edge = back.gameObject.AddComponent<Outline>();
            edge.effectColor = PcMenu.DarkInk;
            edge.effectDistance = new Vector2(3f, -3f);
            edge.enabled = false;
            var icon = MenuStyle.Icon(back.transform, item.icon, size * 0.62f, PcMenu.IconColor);
            if (icon == null) MenuStyle.Label(back.transform, item.name.ToUpperInvariant(), size * 0.17f, TextAnchor.MiddleCenter, PcMenu.IconColor, true);
            else
            {
                // It grows with its button (the chosen one is bigger).
                icon.rectTransform.anchorMin = new Vector2(0.19f, 0.19f);
                icon.rectTransform.anchorMax = new Vector2(0.81f, 0.81f);
                icon.rectTransform.offsetMin = icon.rectTransform.offsetMax = Vector2.zero;
            }
            var button = back.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = UiFactory.NoNavigation;
            button.onClick.AddListener(() =>
            {
                // Nothing stays selected, so Enter / Space don't press this button again on top of the row's own Select.
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                if (!interactable) return;
                Choose(index, true);
                Press();
            });
            var pointer = back.gameObject.AddComponent<PcPointer>();
            pointer.enter = () => { if (interactable) Choose(index, true); };
            MenuPress.Add(button, 0.9f);
            cells.Add(rt);
            backs.Add(back);
            edges.Add(edge);
        }

        /// <summary>Changes what button <paramref name="index"/> does in the hint bar (Indonesian / English).</summary>
        public void SetHint(int index, string indonesian, string english)
        {
            if (index < 0 || index >= items.Count) return;
            items[index].hintId = indonesian;
            items[index].hintEn = english;
            if (index == focus && hint != null) hint.Show(items[index].icon, indonesian, english);
        }

        /// <summary>Greys out (or back) one button: it can't be chosen or pressed.</summary>
        public void SetEnabled(int index, bool on)
        {
            if (index < 0 || index >= items.Count) return;
            items[index].enabled = on;
            backs[index].color = ColorOf(index);
            if (!on && focus == index) Step(1);
        }

        Color ColorOf(int i)
        {
            var c = i == focus ? PcMenu.ChosenColor : PcMenu.ButtonColor;
            if (!items[i].enabled) c.a = 0.35f;
            return c;
        }

        /// <summary>Chooses button <paramref name="index"/> (bigger, green) and shows its hint.</summary>
        public void Choose(int index, bool animate)
        {
            if (index < 0 || index >= items.Count || !items[index].enabled && focus >= 0) return;
            focus = index;
            float total = 0f;
            for (int i = 0; i < cells.Count; i++) total += (i == focus ? chosenSize : size) + (i > 0 ? gap : 0f);
            float x = -total * 0.5f;
            for (int i = 0; i < cells.Count; i++)
            {
                float s = i == focus ? chosenSize : size;
                var rt = cells[i];
                var to = new Vector2(x + s * 0.5f, 0f);
                var from = rt.anchoredPosition;
                var fromSize = rt.sizeDelta;
                if (animate)
                    MenuTween.To(rt, "pc-row", 0.18f, MenuEase.OutQuint, e =>
                    {
                        rt.anchoredPosition = Vector2.LerpUnclamped(from, to, e);
                        rt.sizeDelta = Vector2.LerpUnclamped(fromSize, new Vector2(s, s), e);
                    });
                else
                {
                    rt.anchoredPosition = to;
                    rt.sizeDelta = new Vector2(s, s);
                }
                backs[i].color = ColorOf(i);
                edges[i].enabled = i == focus;
                if (i == focus) rt.SetAsLastSibling();
                x += s + gap;
            }
            if (hint != null) hint.Show(items[focus].icon, items[focus].hintId, items[focus].hintEn);
        }

        /// <summary>The next button that can be pressed, to the right (+1) or left (-1).</summary>
        public void Step(int direction)
        {
            if (items.Count == 0) return;
            int i = focus;
            for (int n = 0; n < items.Count; n++)
            {
                i = Mathf.Clamp(i + direction, 0, items.Count - 1);
                if (items[i].enabled) break;
            }
            if (i != focus && items[i].enabled) Choose(i, true);
        }

        /// <summary>Presses the chosen button.</summary>
        public void Press()
        {
            if (focus < 0 || !items[focus].enabled) return;
            Pop(cells[focus]);
            items[focus].click?.Invoke();
        }

        static void Pop(RectTransform rt) =>
            MenuTween.To(rt, "pc-pop", 0.22f, MenuEase.OutBack, e => rt.localScale = Vector3.one * Mathf.LerpUnclamped(0.88f, 1f, e));

        void Update()
        {
            if (!ownInput || !interactable) return;
            if (GameInput.MenuLeft()) Step(-1);
            else if (GameInput.MenuRight()) Step(1);
            else if (PcMenu.SelectPressed()) Press();
        }
    }

    /// <summary>Pointing at a PC button chooses it (the mouse and the controller share one chosen button).</summary>
    public class PcPointer : MonoBehaviour, IPointerEnterHandler
    {
        public Action enter;
        public void OnPointerEnter(PointerEventData e) => enter?.Invoke();
    }

    /// <summary>
    /// The bar along the bottom (PDF pages 1, 3, 4, 5): the chosen button's icon and what it does on the left, the
    /// controller's Select button on the right while a controller is plugged in.
    /// </summary>
    public class PcHintBar : MonoBehaviour
    {
        Image icon;
        Text text;
        RectTransform pad;
        bool padShown;

        public const float Height = 96f;

        /// <summary>The bar at the bottom of <paramref name="parent"/>: <paramref name="dark"/> = the menu's dark grey with light text, else light grey with dark text.</summary>
        public static PcHintBar Build(RectTransform parent, bool dark) =>
            Build(parent, dark ? PcMenu.DarkBar : PcMenu.LightBar, dark ? PcMenu.IconColor : PcMenu.DarkInk, dark ? PcMenu.IconColor : Color.white);

        /// <summary>The bar in its own colours: <paramref name="back"/> behind, <paramref name="ink"/> for the words, <paramref name="iconColor"/> for the icon.</summary>
        public static PcHintBar Build(RectTransform parent, Color back, Color ink, Color iconColor)
        {
            var backImage = UiFactory.MakePanel(parent, "Hint Bar", back, true);
            return Setup(backImage, ink, iconColor);
        }

        static PcHintBar Setup(Image back, Color ink, Color iconColor)
        {
            var rt = back.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = new Vector2(0f, Height);
            var bar = back.gameObject.AddComponent<PcHintBar>();

            bar.icon = UiFactory.MakePanel(rt, "Icon", iconColor);
            bar.icon.preserveAspect = true;
            UiFactory.Place(bar.icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(64f, 64f));
            bar.text = MenuStyle.Label(rt, "", 50f, TextAnchor.MiddleLeft, ink);
            UiFactory.Place(bar.text.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(110f, 0f), new Vector2(1100f, Height));

            // Right: the controller's Select (X, like Adrian's design) when a controller is plugged in.
            bar.pad = UiFactory.MakeRect(rt, "Controller");
            UiFactory.Place(bar.pad, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-40f, 0f), new Vector2(330f, Height));
            var padIcon = MenuStyle.Icon(bar.pad, "XboxX", 66f, Color.white);
            if (padIcon != null)
            {
                padIcon.rectTransform.anchorMin = padIcon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                padIcon.rectTransform.pivot = new Vector2(0f, 0.5f);
                padIcon.rectTransform.anchoredPosition = Vector2.zero;
                padIcon.rectTransform.sizeDelta = new Vector2(66f, 66f);
            }
            var select = MenuStyle.Label(bar.pad, MenuStyle.T("Pilih", "Select"), 50f, TextAnchor.MiddleLeft, ink);
            UiFactory.Place(select.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(84f, 0f), new Vector2(246f, Height));
            bar.padShown = PcMenu.HasPad;
            bar.pad.gameObject.SetActive(bar.padShown);
            return bar;
        }

        /// <summary>Shows <paramref name="iconName"/> and the hint (Indonesian / English by the menu's language).</summary>
        public void Show(string iconName, string indonesian, string english)
        {
            var sprite = UiFactory.Icon(iconName);
            icon.sprite = sprite;
            icon.enabled = sprite != null;
            text.text = MenuStyle.T(indonesian, english);
        }

        void Update()
        {
            bool on = PcMenu.HasPad;
            if (on == padShown) return;
            padShown = on;
            pad.gameObject.SetActive(on);
        }
    }
}
