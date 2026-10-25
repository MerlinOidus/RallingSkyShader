using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RTXOn
{
    /// <summary>
    /// What the theme windows (Theme Settings, Custom Theme) share: the look of the Custom Obstacles window, rows
    /// (text, number, slider, toggle, cycle, buttons, colour), a list chooser, a colour picker and picture files.
    /// Each row reads its value through a refresher, so <see cref="Refresh"/> shows the data as it is now.
    /// </summary>
    public abstract class PanelKit : MonoBehaviour
    {
        protected static Color PanelColor => EditorColors.Window;
        protected static Color HeaderColor => EditorColors.Header;
        protected static Color ButtonColor => EditorColors.Button;
        protected static Color AccentColor => EditorColors.Accent;
        protected static Color SaveColor => EditorColors.Go;
        protected static Color DeleteColor => EditorColors.Danger;
        protected static Color FieldColor => EditorColors.Field;
        protected static Color SoftText => EditorColors.Soft;
        protected static Color OnColor => EditorColors.Accent;

        protected const float LabelWidth = 240f;
        protected float RowHeight = 44f, RowStep = 50f;
        protected float pageWidth = 760f;

        protected LevelEditor editor;
        protected Text status;
        protected readonly List<Action> refreshers = new List<Action>();

        // List chooser
        RectTransform chooser, chooserList;
        Text chooserTitle;
        readonly List<GameObject> chooserItems = new List<GameObject>();

        // Colour picker
        RectTransform colorWindow, squareMark, hueMark, glowRow;
        RawImage square, hueBar;
        Texture2D squareTex;
        Color32[] squarePixels;
        float squareHue = -1f, pickH, pickS, pickV;
        Image colorSwatch, currentSwatch;
        readonly Image[] recentSwatches = new Image[RecentCount];
        Slider glowSlider;
        Text colorTitle, glowLabel;
        InputField hexField, hField, sField, bField, rField, gField, blueField;
        Action<Color> colorSet;
        Color colorStart, liveApplied;
        bool colorHdr, syncingColor, liveReady, liveChanged;
        Coroutine liveRoutine;
        const float LiveDelay = 0.05f; // the colour reaches what it is for while it is dragged, at most this often

        // Eyedropper
        RectTransform dropperLayer, dropperLoupe;
        Image dropperSwatch;
        Text dropperHex;
        Texture2D dropperPixel;
        Vector2 dropperAt;
        bool dropperReading;

        public bool IsOpen => gameObject.activeSelf;

        /// <summary>The data changed: show it (and rebuild what depends on it).</summary>
        protected abstract void Changed();

        protected virtual bool Editable => true;

        protected void Status(string message)
        {
            if (status != null) status.text = message;
        }

        protected void Refresh()
        {
            if (!Editable) return;
            foreach (var r in refreshers) r();
        }

        /// <summary>True while the colour picker is open (not hidden by its eyedropper).</summary>
        protected bool ColorPickerOpen => colorWindow != null && colorWindow.gameObject.activeSelf;

        /// <summary>Puts <paramref name="c"/> in the open colour picker, as if picked there (it applies live).</summary>
        protected void PickIntoColor(Color c) => SetPlain(c);

        /// <summary>Closes the chooser or colour picker if one is open. True when it did.</summary>
        protected bool ClosePopups()
        {
            if (EyedropperOpen)
            {
                EndEyedropper(null);
                return true;
            }
            if (colorWindow != null && colorWindow.gameObject.activeSelf)
            {
                CancelColor();
                return true;
            }
            if (chooser != null && chooser.gameObject.activeSelf)
            {
                chooser.gameObject.SetActive(false);
                return true;
            }
            return false;
        }

        // ---------------- Building blocks ----------------

        protected Text MakeStatus(RectTransform root, float width)
        {
            status = UiFactory.MakeLabel(root, "", 20, TextAnchor.MiddleLeft, SoftText);
            UiFactory.Place(status.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 10f), new Vector2(width - 48f, 50f));
            status.resizeTextForBestFit = true;
            status.resizeTextMinSize = 12;
            status.resizeTextMaxSize = 20;
            return status;
        }

        /// <summary>Buttons at the right end of a header, right to left; the last one ("X") is narrow.</summary>
        protected static void HeaderButtons(Transform header, string[] names, UnityAction[] actions, Color[] colors)
        {
            float x = -16f;
            for (int i = names.Length - 1; i >= 0; i--)
            {
                float w = names[i] == "X" ? 64f : 136f;
                var b = UiFactory.MakeButton(header, names[i], colors[i], actions[i], 24);
                UiFactory.Place((RectTransform)b.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(x, 0f), new Vector2(w, 56f));
                x -= w + 10f;
            }
        }

        /// <summary>A part of a page shown only while <paramref name="shown"/> says so (rows placed in it start at its top).</summary>
        protected RectTransform Group(Transform page, string name, float top, Func<bool> shown)
        {
            var area = UiFactory.MakeRect(page, name);
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.offsetMin = Vector2.zero;
            area.offsetMax = new Vector2(0f, top);
            refreshers.Add(() => area.gameObject.SetActive(shown()));
            return area;
        }

        // ---------------- Rows ----------------

        protected RectTransform Row(Transform page, string label, ref float y)
        {
            var rect = UiFactory.MakeRect(page, label);
            UiFactory.Place(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, y), new Vector2(pageWidth, RowHeight));
            y -= RowStep;
            var back = UiFactory.MakePanel(rect, "Back", new Color(1f, 1f, 1f, 0.05f));
            UiFactory.Stretch(back.rectTransform);
            var text = UiFactory.MakeLabel(rect, label, 21, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            UiFactory.Place(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(LabelWidth - 24f, RowHeight));
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 12;
            text.resizeTextMaxSize = 21;
            return rect;
        }

        protected InputField Field(RectTransform row, float x, float width, InputField.ContentType type)
        {
            var f = UiFactory.MakeInputField(row, 21, FieldColor, type);
            UiFactory.Place((RectTransform)f.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(width, RowHeight - 6f));
            return f;
        }

        protected void InfoRow(Transform page, string label, ref float y, Func<string> get)
        {
            var row = Row(page, label, ref y);
            var value = UiFactory.MakeLabel(row, "", 20, TextAnchor.MiddleLeft, AccentColor, FontStyle.Bold);
            UiFactory.Place(value.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(LabelWidth, 0f), new Vector2(pageWidth - LabelWidth - 8f, RowHeight));
            value.resizeTextForBestFit = true;
            value.resizeTextMinSize = 10;
            value.resizeTextMaxSize = 20;
            refreshers.Add(() => value.text = get());
        }

        protected void TextRow(Transform page, string label, ref float y, Func<string> get, Action<string> set, int limit = 40)
        {
            var row = Row(page, label, ref y);
            var f = Field(row, LabelWidth, 440f, InputField.ContentType.Standard);
            f.characterLimit = limit;
            f.onEndEdit.AddListener(t =>
            {
                if (!Editable) return;
                set(t);
                Changed();
            });
            refreshers.Add(() => { if (!f.isFocused) f.text = get(); });
        }

        protected void NumberRow(Transform page, string label, ref float y, Func<float> get, Action<float> set)
        {
            var row = Row(page, label, ref y);
            var f = Field(row, LabelWidth, 160f, InputField.ContentType.DecimalNumber);
            f.onEndEdit.AddListener(t =>
            {
                if (!Editable) return;
                if (TryParse(t, out float v))
                {
                    set(v);
                    Changed();
                }
                else Refresh();
            });
            refreshers.Add(() => SetField(f, get()));
        }

        /// <summary>A slider; the value shows while dragging and is set when the drag ends (looks are built once).</summary>
        /// <param name="live">True = the value is set while the slider moves (realtime), not only when it is let go.</param>
        protected void SliderRow(Transform page, string label, ref float y, float min, float max, Func<float> get, Action<float> set, string format = "0.00", bool live = false, float reset = 0f)
        {
            var row = Row(page, label, ref y);
            var slider = UiFactory.MakeHorizontalSlider(row, FieldColor, AccentColor, Color.white, min, max);
            UiFactory.Place((RectTransform)slider.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(LabelWidth, 0f), new Vector2(360f, 30f));
            var value = UiFactory.MakeLabel(row, "", 20, TextAnchor.MiddleLeft, AccentColor, FontStyle.Bold);
            UiFactory.Place(value.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(LabelWidth + 376f, 0f), new Vector2(110f, RowHeight));
            bool syncing = false;
            slider.onValueChanged.AddListener(v =>
            {
                if (syncing) return;
                value.text = v.ToString(format, CultureInfo.InvariantCulture);
                if (live && Editable)
                {
                    set(v);
                    Changed();
                }
            });
            var trigger = slider.gameObject.AddComponent<EventTrigger>();
            var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            up.callback.AddListener(_ =>
            {
                if (!Editable) return;
                set(slider.value);
                Changed();
            });
            trigger.triggers.Add(up);
            var zero = UiFactory.MakeButton(row, reset.ToString("0.##", CultureInfo.InvariantCulture), ButtonColor, () =>
            {
                if (!Editable) return;
                set(Mathf.Clamp(reset, min, max));
                Changed();
            }, 18);
            UiFactory.Place((RectTransform)zero.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-6f, 0f), new Vector2(60f, RowHeight - 8f));
            refreshers.Add(() =>
            {
                syncing = true;
                slider.value = get();
                syncing = false;
                value.text = get().ToString(format, CultureInfo.InvariantCulture);
            });
        }

        protected void ToggleRow(Transform page, string label, ref float y, Func<string> text, Func<bool> on, Action flip)
        {
            var row = Row(page, label, ref y);
            var b = UiFactory.MakeButton(row, "", FieldColor, () =>
            {
                if (!Editable) return;
                flip();
                Changed();
            }, 20);
            UiFactory.Place((RectTransform)b.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(LabelWidth, 0f), new Vector2(300f, RowHeight - 6f));
            var t = b.GetComponentInChildren<Text>();
            var image = (Image)b.targetGraphic;
            refreshers.Add(() =>
            {
                t.text = text();
                image.color = on() ? OnColor : FieldColor;
                t.color = TilePalette.TextColorOn(image.color);
            });
        }

        protected void CycleRow(Transform page, string label, ref float y, Func<string> text, Action<int> step)
        {
            var row = Row(page, label, ref y);
            var prev = UiFactory.MakeButton(row, "<", ButtonColor, () => { if (!Editable) return; step(-1); Changed(); }, 24);
            UiFactory.Place((RectTransform)prev.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(LabelWidth, 0f), new Vector2(56f, RowHeight - 6f));
            var value = UiFactory.MakeLabel(row, "", 21, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            UiFactory.Place(value.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(LabelWidth + 62f, 0f), new Vector2(240f, RowHeight));
            value.resizeTextForBestFit = true;
            value.resizeTextMinSize = 10;
            value.resizeTextMaxSize = 21;
            var next = UiFactory.MakeButton(row, ">", ButtonColor, () => { if (!Editable) return; step(1); Changed(); }, 24);
            UiFactory.Place((RectTransform)next.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(LabelWidth + 308f, 0f), new Vector2(56f, RowHeight - 6f));
            refreshers.Add(() => value.text = text());
        }

        protected void ButtonsRow(Transform page, string label, ref float y, Func<string> text, params (string name, UnityAction click)[] buttons)
        {
            var row = Row(page, label, ref y);
            float buttonsWidth = 6f;
            foreach (var b in buttons) buttonsWidth += ButtonWidth(b.name) + 6f;
            var value = UiFactory.MakeLabel(row, "", 18, TextAnchor.MiddleLeft, AccentColor, FontStyle.Bold);
            UiFactory.Place(value.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(LabelWidth, 0f),
                new Vector2(Mathf.Max(80f, pageWidth - LabelWidth - Mathf.Max(250f, buttonsWidth)), RowHeight));
            value.resizeTextForBestFit = true;
            value.resizeTextMinSize = 10;
            value.resizeTextMaxSize = 18;
            float x = -6f;
            for (int i = buttons.Length - 1; i >= 0; i--)
            {
                var click = buttons[i].click;
                float w = ButtonWidth(buttons[i].name);
                var b = UiFactory.MakeButton(row, buttons[i].name, ButtonColor, () => { if (Editable) click(); }, 18);
                UiFactory.Place((RectTransform)b.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(x, 0f), new Vector2(w, RowHeight - 8f));
                x -= w + 6f;
            }
            refreshers.Add(() => value.text = text());
        }

        static float ButtonWidth(string name) => name.Length > 8 ? 140f : name.Length > 6 ? 104f : 92f;

        /// <summary>A colour: its swatch and hex; clicking either opens the colour picker. <paramref name="hdr"/>: brightness above 1 allowed (glow).</summary>
        protected void ColorRow(Transform page, string label, ref float y, Func<Color> get, Action<Color> set, bool hdr = false)
        {
            var row = Row(page, label, ref y);
            var swatch = UiFactory.MakeButton(row, "", Color.white, () => { if (Editable) OpenColor(label, get(), hdr, set); }, 16);
            UiFactory.Place((RectTransform)swatch.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(LabelWidth, 0f), new Vector2(120f, RowHeight - 8f));
            var swatchImage = (Image)swatch.targetGraphic;
            var hex = UiFactory.MakeLabel(row, "", 18, TextAnchor.MiddleLeft, AccentColor, FontStyle.Bold);
            UiFactory.Place(hex.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(LabelWidth + 132f, 0f), new Vector2(260f, RowHeight));
            var edit = UiFactory.MakeButton(row, "Edit", ButtonColor, () => { if (Editable) OpenColor(label, get(), hdr, set); }, 18);
            UiFactory.Place((RectTransform)edit.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-6f, 0f), new Vector2(104f, RowHeight - 8f));
            refreshers.Add(() =>
            {
                var c = get();
                float k = Mathf.Max(1f, c.maxColorComponent);
                swatchImage.color = new Color(c.r / k, c.g / k, c.b / k, 1f);
                hex.text = "#" + ColorUtility.ToHtmlStringRGB(swatchImage.color) + (k > 1.001f ? $"  x{k.ToString("0.0", CultureInfo.InvariantCulture)}" : "") +
                           (c.a < 0.999f ? $"  alpha {c.a.ToString("0.00", CultureInfo.InvariantCulture)}" : "");
            });
        }

        protected static void SetField(InputField field, float value)
        {
            if (!field.isFocused) field.text = value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        protected static bool TryParse(string text, out float value) =>
            float.TryParse((text ?? "").Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        // ---------------- List chooser ----------------

        protected void BuildChooser(RectTransform root, Vector2 position, Vector2 size)
        {
            var bg = UiFactory.MakePanel(root, "Chooser", new Color(0.08f, 0.07f, 0.1f, 0.98f), true);
            chooser = bg.rectTransform;
            UiFactory.Place(chooser, new Vector2(0f, 1f), new Vector2(0f, 1f), position, size);
            chooserTitle = UiFactory.MakeLabel(chooser, "", 22, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            UiFactory.Place(chooserTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -8f), new Vector2(size.x - 180f, 50f));
            chooserTitle.resizeTextForBestFit = true;
            chooserTitle.resizeTextMinSize = 12;
            chooserTitle.resizeTextMaxSize = 22;
            var cancel = UiFactory.MakeButton(chooser, "Cancel", ButtonColor, () => chooser.gameObject.SetActive(false), 22);
            UiFactory.Place((RectTransform)cancel.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -8f), new Vector2(140f, 50f));
            var list = UiFactory.MakeGridList(chooser, new Vector2(size.x - 60f, 48f), 6f, 1, AccentColor, out chooserList);
            UiFactory.Stretch((RectTransform)list.transform, 12f, 12f, 12f, 70f);
            bg.gameObject.SetActive(false);
        }

        protected void OpenChooser(string heading, List<string> options, Action<int> pick)
        {
            chooserTitle.text = heading;
            foreach (var go in chooserItems) Destroy(go);
            chooserItems.Clear();
            for (int i = 0; i < options.Count; i++)
            {
                int index = i;
                var b = UiFactory.MakeButton(chooserList, options[i], ButtonColor, () =>
                {
                    chooser.gameObject.SetActive(false);
                    pick(index);
                }, 20);
                chooserItems.Add(b.gameObject);
            }
            chooser.gameObject.SetActive(true);
            chooser.SetAsLastSibling();
        }

        // ---------------- Colour picker ----------------
        // Laid out like Photoshop's: a saturation / brightness square for the hue picked on the bar beside it,
        // new over current, H S B and R G B numbers, Hex, an eyedropper that reads any pixel on the screen
        // and the last colours that were OK'd.

        const int SquareSize = 128;
        const string RecentKey = "RTXOn.RecentColours";
        const int RecentCount = 12;

        protected void BuildColorPicker(RectTransform root)
        {
            var bg = UiFactory.MakePanel(root, "Colour Picker", new Color(0.08f, 0.07f, 0.1f, 0.99f), true);
            colorWindow = bg.rectTransform;
            UiFactory.Place(colorWindow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(780f, 600f));
            // The top strip moves the window (drag it), so what is behind can be seen while picking.
            var strip = UiFactory.MakePanel(colorWindow, "Drag", new Color(1f, 1f, 1f, 0.04f), true);
            UiFactory.Place(strip.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(780f, 60f));
            strip.rectTransform.pivot = new Vector2(0f, 1f);
            strip.rectTransform.anchoredPosition = Vector2.zero;
            var grip = UiFactory.MakePanel(strip.transform, "Grip", new Color(1f, 1f, 1f, 0.35f));
            UiFactory.Place(grip.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(400f, 0f), new Vector2(70f, 6f));
            var gripText = UiFactory.MakeLabel(strip.transform, "drag to move", 14, TextAnchor.MiddleLeft, SoftText);
            UiFactory.Place(gripText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(480f, 0f), new Vector2(140f, 30f));
            var dragTrigger = strip.gameObject.AddComponent<EventTrigger>();
            var dragEntry = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            dragEntry.callback.AddListener(d => MoveColorWindow(((PointerEventData)d).delta));
            dragTrigger.triggers.Add(dragEntry);
            colorTitle = UiFactory.MakeLabel(colorWindow, "", 24, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            UiFactory.Place(colorTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -10f), new Vector2(360f, 50f));
            colorTitle.resizeTextForBestFit = true;
            colorTitle.resizeTextMinSize = 14;
            colorTitle.resizeTextMaxSize = 24;

            // Saturation (left to right) and brightness (bottom to top) for the current hue
            squareTex = new Texture2D(SquareSize, SquareSize, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            square = UiFactory.MakeRect(colorWindow, "Square").gameObject.AddComponent<RawImage>();
            square.texture = squareTex;
            UiFactory.Place(square.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -70f), new Vector2(320f, 320f));
            square.rectTransform.pivot = new Vector2(0f, 1f);
            square.rectTransform.anchoredPosition = new Vector2(20f, -70f);
            OnPointer(square.gameObject, p => { pickS = p.x; pickV = p.y; ShowPicker(); });
            squareMark = MakeMarker(square.rectTransform, new Vector2(18f, 18f));

            // Hue bar: red at the bottom, through yellow, green, cyan, blue and magenta to red at the top
            var hueTex = new Texture2D(1, 256, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < 256; y++) hueTex.SetPixel(0, y, Color.HSVToRGB(y / 255f, 1f, 1f));
            hueTex.Apply();
            hueBar = UiFactory.MakeRect(colorWindow, "Hue").gameObject.AddComponent<RawImage>();
            hueBar.texture = hueTex;
            hueBar.rectTransform.anchorMin = hueBar.rectTransform.anchorMax = new Vector2(0f, 1f);
            hueBar.rectTransform.pivot = new Vector2(0f, 1f);
            hueBar.rectTransform.sizeDelta = new Vector2(36f, 320f);
            hueBar.rectTransform.anchoredPosition = new Vector2(356f, -70f);
            OnPointer(hueBar.gameObject, p => { pickH = Mathf.Clamp01(p.y); RedrawSquare(); ShowPicker(); });
            hueMark = MakeMarker(hueBar.rectTransform, new Vector2(48f, 8f));

            // New over current; clicking current goes back to it
            float x = 416f;
            colorSwatch = UiFactory.MakePanel(colorWindow, "New", Color.white);
            UiFactory.Place(colorSwatch.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -70f), new Vector2(110f, 55f));
            var currentButton = UiFactory.MakeButton(colorWindow, "", Color.white, () => SetPicker(colorStart), 16);
            currentSwatch = currentButton.GetComponent<Image>();
            var currentRt = (RectTransform)currentButton.transform;
            currentRt.anchorMin = currentRt.anchorMax = new Vector2(0f, 1f);
            currentRt.pivot = new Vector2(0f, 1f);
            currentRt.sizeDelta = new Vector2(110f, 55f);
            currentRt.anchoredPosition = new Vector2(x, -125f);
            SmallLabel("new", new Vector2(x + 118f, -70f), 70f);
            SmallLabel("current", new Vector2(x + 118f, -125f), 70f);

            var dropper = UiFactory.MakeButton(colorWindow, "Eyedropper", AccentColor, StartEyedropper, 20);
            var dropperRt = (RectTransform)dropper.transform;
            dropperRt.anchorMin = dropperRt.anchorMax = new Vector2(0f, 1f);
            dropperRt.pivot = new Vector2(0f, 1f);
            dropperRt.sizeDelta = new Vector2(150f, 50f);
            dropperRt.anchoredPosition = new Vector2(610f, -70f);

            // Numbers: H S B on the left, R G B on the right
            hField = NumberField("H:", "°", x, -200f, 360, v => { pickH = v / 360f; RedrawSquare(); ShowPicker(); });
            sField = NumberField("S:", "%", x, -250f, 100, v => { pickS = v / 100f; ShowPicker(); });
            bField = NumberField("B:", "%", x, -300f, 100, v => { pickV = v / 100f; ShowPicker(); });
            rField = NumberField("R:", "", x + 190f, -200f, 255, v => SetPlain(RgbWith(0, v)));
            gField = NumberField("G:", "", x + 190f, -250f, 255, v => SetPlain(RgbWith(1, v)));
            blueField = NumberField("B:", "", x + 190f, -300f, 255, v => SetPlain(RgbWith(2, v)));

            SmallLabel("#", new Vector2(x, -350f), 30f, 24);
            hexField = UiFactory.MakeInputField(colorWindow, 22, FieldColor, InputField.ContentType.Alphanumeric);
            UiFactory.Place((RectTransform)hexField.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x + 30f, -350f), new Vector2(170f, 44f));
            ((RectTransform)hexField.transform).pivot = new Vector2(0f, 1f);
            ((RectTransform)hexField.transform).anchoredPosition = new Vector2(x + 30f, -350f);
            hexField.characterLimit = 6;
            UiFactory.SetPlaceholder(hexField, "FFCC00");
            hexField.onEndEdit.AddListener(ApplyHex);
            hexField.onValueChanged.AddListener(t => { if (t.Length == 6) ApplyHex(t); }); // pasting or typing all 6 shows it at once

            // Glow (HDR colours only)
            glowRow = UiFactory.MakeRect(colorWindow, "Glow");
            glowRow.anchorMin = glowRow.anchorMax = new Vector2(0f, 1f);
            glowRow.pivot = new Vector2(0f, 1f);
            glowRow.sizeDelta = new Vector2(372f, 44f);
            glowRow.anchoredPosition = new Vector2(20f, -404f);
            glowLabel = UiFactory.MakeLabel(glowRow, "Glow x1.0", 18, TextAnchor.MiddleLeft, SoftText, FontStyle.Bold);
            UiFactory.Place(glowLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(110f, 40f));
            glowSlider = UiFactory.MakeHorizontalSlider(glowRow, FieldColor, AccentColor, Color.white, 1f, 8f);
            UiFactory.Place((RectTransform)glowSlider.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(116f, 0f), new Vector2(250f, 28f));
            glowSlider.onValueChanged.AddListener(_ => { if (!syncingColor) ShowPicker(); });

            // Recent colours (the last ones OK'd, kept between sessions)
            SmallLabel("Recent", new Vector2(20f, -462f), 80f);
            for (int i = 0; i < RecentCount; i++)
            {
                int index = i;
                var b = UiFactory.MakeButton(colorWindow, "", Color.white, () => PickRecent(index), 12);
                var rt = (RectTransform)b.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(44f, 36f);
                rt.anchoredPosition = new Vector2(100f + i * 50f, -462f);
                recentSwatches[i] = b.GetComponent<Image>();
            }

            var cancel = UiFactory.MakeButton(colorWindow, "Cancel", ButtonColor, CancelColor, 22);
            UiFactory.Place((RectTransform)cancel.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-180f, 20f), new Vector2(150f, 54f));
            var ok = UiFactory.MakeButton(colorWindow, "OK", SaveColor, () =>
            {
                StopLive();
                colorWindow.gameObject.SetActive(false);
                AddRecent(Color.HSVToRGB(pickH, pickS, pickV));
                colorSet?.Invoke(PickerColor());
                Changed();
            }, 24);
            UiFactory.Place((RectTransform)ok.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-20f, 20f), new Vector2(150f, 54f));
            var hint = UiFactory.MakeLabel(colorWindow, "Changes show at once. Type a Hex like FFCC00, or Eyedropper then click any colour on the screen. Cancel = back to current.", 16, TextAnchor.MiddleLeft, SoftText);
            UiFactory.Place(hint.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(20f, 18f), new Vector2(400f, 62f));
            hint.horizontalOverflow = HorizontalWrapMode.Wrap;
            bg.gameObject.SetActive(false);
        }

        /// <summary>Moves the colour picker by a pointer drag (screen pixels), keeping its top strip on the screen.</summary>
        void MoveColorWindow(Vector2 screenDelta)
        {
            var canvas = colorWindow.GetComponentInParent<Canvas>();
            var rootRect = canvas != null ? (RectTransform)canvas.rootCanvas.transform : colorWindow.parent as RectTransform;
            float scale = canvas != null ? canvas.rootCanvas.scaleFactor : 1f;
            var p = colorWindow.anchoredPosition + screenDelta / Mathf.Max(0.01f, scale);
            if (rootRect != null)
            {
                // (0, 0) = the middle of the window it belongs to, about the middle of the screen: keep the strip on screen.
                var half = rootRect.rect.size * 0.5f;
                var size = colorWindow.rect.size;
                p.x = Mathf.Clamp(p.x, -half.x - size.x * 0.5f + 120f, half.x + size.x * 0.5f - 120f);
                p.y = Mathf.Clamp(p.y, -half.y - size.y * 0.5f + 60f, half.y - size.y * 0.5f);
            }
            colorWindow.anchoredPosition = p;
        }

        /// <summary>Calls <paramref name="moved"/> with the pointer inside <paramref name="target"/> as 0..1 while it is pressed or dragged.</summary>
        static void OnPointer(GameObject target, Action<Vector2> moved)
        {
            var rt = (RectTransform)target.transform;
            void At(BaseEventData d)
            {
                var e = (PointerEventData)d;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out var local)) return;
                var r = rt.rect;
                moved(new Vector2(Mathf.Clamp01((local.x - r.xMin) / r.width), Mathf.Clamp01((local.y - r.yMin) / r.height)));
            }
            var trigger = target.AddComponent<EventTrigger>();
            foreach (var type in new[] { EventTriggerType.PointerDown, EventTriggerType.Drag })
            {
                var entry = new EventTrigger.Entry { eventID = type };
                entry.callback.AddListener(At);
                trigger.triggers.Add(entry);
            }
        }

        /// <summary>A white mark with a dark rim that is placed on the square or the hue bar.</summary>
        static RectTransform MakeMarker(RectTransform parent, Vector2 size)
        {
            var rim = UiFactory.MakePanel(parent, "Mark", new Color(0f, 0f, 0f, 0.85f));
            rim.rectTransform.anchorMin = rim.rectTransform.anchorMax = Vector2.zero;
            rim.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rim.rectTransform.sizeDelta = size + new Vector2(4f, 4f);
            var inner = UiFactory.MakePanel(rim.transform, "White", Color.white);
            UiFactory.Stretch(inner.rectTransform, 2f, 2f, 2f, 2f);
            return rim.rectTransform;
        }

        void SmallLabel(string text, Vector2 position, float width, int size = 18)
        {
            var label = UiFactory.MakeLabel(colorWindow, text, size, TextAnchor.MiddleLeft, SoftText, FontStyle.Bold);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0f, 1f);
            label.rectTransform.pivot = new Vector2(0f, 1f);
            label.rectTransform.sizeDelta = new Vector2(width, size == 18 ? 36f : 44f);
            label.rectTransform.anchoredPosition = position;
        }

        InputField NumberField(string name, string unit, float x, float y, int max, Action<float> set)
        {
            SmallLabel(name, new Vector2(x, y), 34f, 22);
            var field = UiFactory.MakeInputField(colorWindow, 22, FieldColor, InputField.ContentType.IntegerNumber);
            var rt = (RectTransform)field.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(96f, 44f);
            rt.anchoredPosition = new Vector2(x + 36f, y);
            field.characterLimit = 3;
            field.onEndEdit.AddListener(t =>
            {
                if (int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)) set(Mathf.Clamp(v, 0, max));
                else ShowPicker();
            });
            if (unit.Length > 0) SmallLabel(unit, new Vector2(x + 138f, y), 30f, 22);
            return field;
        }

        /// <summary>The picked colour (without glow) with channel <paramref name="channel"/> set to <paramref name="value"/> (0..255).</summary>
        Color RgbWith(int channel, float value)
        {
            var c = Color.HSVToRGB(pickH, pickS, pickV);
            c[channel] = value / 255f;
            return c;
        }

        /// <summary>Picks a plain (0..1) colour and keeps the glow as it is.</summary>
        void SetPlain(Color plain)
        {
            Color.RGBToHSV(plain, out float h, out float s, out float v);
            if (s > 0f && v > 0f) pickH = h; // grey and black keep the hue the square shows
            pickS = s;
            pickV = v;
            RedrawSquare();
            ShowPicker();
        }

        void ApplyHex(string text)
        {
            var hex = text.Trim().TrimStart('#');
            if (hex.Length == 3) hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";
            if (hex.Length == 6 && ColorUtility.TryParseHtmlString("#" + hex, out var c)) SetPlain(c);
            else if (!hexField.isFocused) ShowPicker();
        }

        /// <summary>Opens the colour picker on <paramref name="current"/>; OK calls <paramref name="set"/>, then <see cref="Changed"/>.</summary>
        protected void OpenColor(string label, Color current, bool hdr, Action<Color> set)
        {
            colorSet = set;
            colorHdr = hdr;
            colorTitle.text = label;
            glowRow.gameObject.SetActive(hdr);
            colorStart = current;
            StopLive();
            SetPicker(current);
            liveApplied = PickerColor();
            liveReady = true;
            liveChanged = false;
            ShowRecent();
            colorWindow.gameObject.SetActive(true);
            colorWindow.SetAsLastSibling();
        }

        /// <summary>Cancel (or Esc): the colour it had when the picker opened comes back.</summary>
        void CancelColor()
        {
            StopLive();
            colorWindow.gameObject.SetActive(false);
            if (!liveChanged) return;
            liveChanged = false;
            colorSet?.Invoke(colorStart);
            Changed();
        }

        void StopLive()
        {
            liveReady = false;
            if (liveRoutine != null) StopCoroutine(liveRoutine);
            liveRoutine = null;
        }

        /// <summary>Realtime: the picked colour goes to what the picker is for shortly after each change.</summary>
        void QueueLive()
        {
            if (!liveReady || colorSet == null || liveRoutine != null || !isActiveAndEnabled) return;
            liveRoutine = StartCoroutine(LiveSoon());
        }

        IEnumerator LiveSoon()
        {
            yield return new WaitForSecondsRealtime(LiveDelay);
            liveRoutine = null;
            if (!liveReady) yield break;
            var c = PickerColor();
            if (c == liveApplied) yield break;
            liveApplied = c;
            liveChanged = true;
            colorSet?.Invoke(c);
            Changed();
        }

        void SetPicker(Color c)
        {
            float k = colorHdr ? Mathf.Clamp(c.maxColorComponent, 1f, 8f) : 1f;
            float m = Mathf.Max(1f, c.maxColorComponent);
            var plain = new Color(c.r / m, c.g / m, c.b / m);
            Color.RGBToHSV(plain, out pickH, out pickS, out pickV);
            syncingColor = true;
            glowSlider.value = k;
            syncingColor = false;
            pickerAlpha = c.a;
            currentSwatch.color = plain;
            RedrawSquare();
            ShowPicker();
        }

        float pickerAlpha = 1f;

        Color PickerColor()
        {
            var c = Color.HSVToRGB(pickH, pickS, pickV);
            if (colorHdr) c *= glowSlider.value;
            c.a = pickerAlpha;
            return c;
        }

        void RedrawSquare()
        {
            if (squareTex == null || Mathf.Approximately(squareHue, pickH)) return;
            squareHue = pickH;
            if (squarePixels == null) squarePixels = new Color32[SquareSize * SquareSize];
            for (int y = 0; y < SquareSize; y++)
                for (int x = 0; x < SquareSize; x++)
                    squarePixels[y * SquareSize + x] = Color.HSVToRGB(pickH, x / (SquareSize - 1f), y / (SquareSize - 1f));
            squareTex.SetPixels32(squarePixels);
            squareTex.Apply();
        }

        void ShowPicker()
        {
            var plain = Color.HSVToRGB(pickH, pickS, pickV);
            colorSwatch.color = plain;
            var r = square.rectTransform.rect;
            squareMark.anchoredPosition = new Vector2(pickS * r.width, pickV * r.height);
            var hr = hueBar.rectTransform.rect;
            hueMark.anchoredPosition = new Vector2(hr.width * 0.5f, pickH * hr.height);
            ShowNumber(hField, pickH * 360f);
            ShowNumber(sField, pickS * 100f);
            ShowNumber(bField, pickV * 100f);
            ShowNumber(rField, plain.r * 255f);
            ShowNumber(gField, plain.g * 255f);
            ShowNumber(blueField, plain.b * 255f);
            if (!hexField.isFocused) hexField.SetTextWithoutNotify(ColorUtility.ToHtmlStringRGB(plain));
            glowLabel.text = $"Glow x{glowSlider.value.ToString("0.0", CultureInfo.InvariantCulture)}";
            QueueLive();
        }

        static void ShowNumber(InputField field, float value)
        {
            if (!field.isFocused) field.SetTextWithoutNotify(Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture));
        }

        // Recent colours

        List<Color> LoadRecent()
        {
            var list = new List<Color>();
            foreach (var hex in PlayerPrefs.GetString(RecentKey, "").Split(','))
                if (hex.Length == 6 && ColorUtility.TryParseHtmlString("#" + hex, out var c)) list.Add(c);
            return list;
        }

        void AddRecent(Color plain)
        {
            var hex = ColorUtility.ToHtmlStringRGB(plain);
            var list = LoadRecent();
            list.RemoveAll(c => ColorUtility.ToHtmlStringRGB(c) == hex);
            list.Insert(0, plain);
            if (list.Count > RecentCount) list.RemoveRange(RecentCount, list.Count - RecentCount);
            PlayerPrefs.SetString(RecentKey, string.Join(",", list.ConvertAll(c => ColorUtility.ToHtmlStringRGB(c))));
            PlayerPrefs.Save();
        }

        void ShowRecent()
        {
            var list = LoadRecent();
            for (int i = 0; i < RecentCount; i++)
            {
                recentSwatches[i].gameObject.SetActive(i < list.Count);
                if (i < list.Count) recentSwatches[i].color = list[i];
            }
        }

        void PickRecent(int index)
        {
            var list = LoadRecent();
            if (index < list.Count) SetPlain(list[index]);
        }

        // Eyedropper: hides the picker, then a click (or a finger lifted) anywhere reads that pixel of the screen.

        void StartEyedropper()
        {
            if (dropperLayer == null) BuildEyedropper();
            colorWindow.gameObject.SetActive(false);
            dropperLayer.gameObject.SetActive(true);
            dropperLayer.SetAsLastSibling();
            var own = dropperLayer.GetComponent<Canvas>();
            own.overrideSorting = true; // set again once active: a nested canvas can drop it while inactive
            own.sortingOrder = 32000;
            dropperLoupe.gameObject.SetActive(false);
        }

        /// <summary>Ends the eyedropper; the picker comes back (with the picked colour when <paramref name="picked"/> is set).</summary>
        void EndEyedropper(Color? picked)
        {
            if (dropperLayer == null || !dropperLayer.gameObject.activeSelf) return;
            dropperLayer.gameObject.SetActive(false);
            colorWindow.gameObject.SetActive(true);
            colorWindow.SetAsLastSibling();
            if (picked.HasValue) SetPlain(picked.Value);
        }

        bool EyedropperOpen => dropperLayer != null && dropperLayer.gameObject.activeSelf;

        void BuildEyedropper()
        {
            var canvas = colorWindow.GetComponentInParent<Canvas>().rootCanvas;
            var layer = UiFactory.MakePanel(canvas.transform, "Eyedropper", new Color(0f, 0f, 0f, 0f), true);
            dropperLayer = layer.rectTransform;
            UiFactory.Stretch(dropperLayer);
            // Its own canvas so it sits over every window, whatever canvas they are on
            var own = layer.gameObject.AddComponent<Canvas>();
            own.overrideSorting = true;
            own.sortingOrder = 32000;
            layer.gameObject.AddComponent<GraphicRaycaster>();

            var bar = UiFactory.MakePanel(dropperLayer, "Hint", new Color(0.08f, 0.07f, 0.1f, 0.92f));
            UiFactory.Place(bar.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(900f, 50f));
            var text = UiFactory.MakeLabel(bar.transform, "Eyedropper: click any colour on the screen.  Right click or Esc = cancel.", 20, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            UiFactory.Stretch(text.rectTransform);

            var loupe = UiFactory.MakePanel(dropperLayer, "Loupe", new Color(0.08f, 0.07f, 0.1f, 0.95f));
            dropperLoupe = loupe.rectTransform;
            dropperLoupe.anchorMin = dropperLoupe.anchorMax = Vector2.zero;
            dropperLoupe.pivot = Vector2.zero;
            dropperLoupe.sizeDelta = new Vector2(170f, 70f);
            dropperSwatch = UiFactory.MakePanel(dropperLoupe, "Colour", Color.white);
            UiFactory.Place(dropperSwatch.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(7f, 0f), new Vector2(56f, 56f));
            dropperHex = UiFactory.MakeLabel(dropperLoupe, "", 20, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            UiFactory.Place(dropperHex.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(70f, 0f), new Vector2(96f, 40f));

            var catcher = layer.gameObject.AddComponent<EyedropperCatcher>();
            catcher.moved = p => { dropperAt = p; if (!dropperReading) StartCoroutine(ReadScreen(false)); };
            catcher.picked = p => { dropperAt = p; StartCoroutine(ReadScreen(true)); };
            catcher.cancelled = () => EndEyedropper(null);
            dropperLayer.gameObject.SetActive(false);
        }

        /// <summary>Reads the pixel under <see cref="dropperAt"/> after this frame is drawn; shows it, or picks it when <paramref name="pick"/>.</summary>
        IEnumerator ReadScreen(bool pick)
        {
            dropperReading = true;
            yield return new WaitForEndOfFrame();
            dropperReading = false;
            if (!EyedropperOpen) yield break;
            if (dropperPixel == null) dropperPixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            int x = Mathf.Clamp(Mathf.RoundToInt(dropperAt.x), 0, Screen.width - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt(dropperAt.y), 0, Screen.height - 1);
            dropperPixel.ReadPixels(new Rect(x, y, 1, 1), 0, 0, false);
            dropperPixel.Apply(false);
            var c = dropperPixel.GetPixel(0, 0);
            c.a = 1f;
            if (pick) { EndEyedropper(c); yield break; }
            dropperSwatch.color = c;
            dropperHex.text = "#" + ColorUtility.ToHtmlStringRGB(c);
            dropperLoupe.gameObject.SetActive(true);
            // Beside the pointer (not over it, or it would read its own colour), kept on the screen
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(dropperLayer, dropperAt, null, out var local))
            {
                var r = dropperLayer.rect;
                var size = dropperLoupe.sizeDelta;
                var at = new Vector2(local.x - r.xMin + 28f, local.y - r.yMin + 28f);
                if (at.x + size.x > r.width) at.x -= size.x + 56f;
                if (at.y + size.y > r.height) at.y -= size.y + 56f;
                dropperLoupe.anchoredPosition = at;
            }
        }

        // ---------------- Pictures ----------------

        /// <summary>A PNG / JPG picked by the user, made at most <paramref name="maxSize"/> pixels, as base64 PNG.</summary>
        protected void PickImage(string what, int maxSize, Action<string> set)
        {
            editor.Files.Open(what + " (PNG / JPG)", new[] { "png", "jpg", "jpeg" }, path =>
            {
                byte[] bytes;
                try { bytes = File.ReadAllBytes(path); }
                catch (Exception e) { Status("Could not read the picture: " + e.Message); return; }
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(bytes))
                {
                    Destroy(tex);
                    Status($"{Path.GetFileName(path)} is not a PNG or JPG picture.");
                    return;
                }
                int w = tex.width, h = tex.height;
                var png = Shrink(tex, maxSize).EncodeToPNG();
                Destroy(tex);
                set(Convert.ToBase64String(png));
                Changed();
                Status($"{what}: {Path.GetFileName(path)} ({w} x {h}{(w > maxSize || h > maxSize ? $", made at most {maxSize}" : "")}).");
            }, Status);
        }

        /// <summary><paramref name="tex"/> made at most <paramref name="max"/> pixels wide and high (0 = as it is).</summary>
        protected static Texture2D Shrink(Texture2D tex, int max)
        {
            if (max <= 0 || (tex.width <= max && tex.height <= max)) return tex;
            float k = (float)max / Mathf.Max(tex.width, tex.height);
            int w = Mathf.Max(1, Mathf.RoundToInt(tex.width * k)), h = Mathf.Max(1, Mathf.RoundToInt(tex.height * k));
            var rt = RenderTexture.GetTemporary(w, h, 0);
            Graphics.Blit(tex, rt);
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var small = new Texture2D(w, h, TextureFormat.RGBA32, false);
            small.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            small.Apply();
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(rt);
            tex.Reinitialize(w, h);
            tex.SetPixels32(small.GetPixels32());
            tex.Apply();
            Destroy(small);
            return tex;
        }

        protected static string PictureText(string base64) => string.IsNullOrEmpty(base64) ? "none" : $"PNG ({base64.Length * 3 / 4 / 1024} KB)";
    }

    /// <summary>Catches every pointer over the screen while the colour picker's eyedropper is on.</summary>
    sealed class EyedropperCatcher : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IPointerMoveHandler
    {
        public Action<Vector2> moved, picked;
        public Action cancelled;

        public void OnPointerDown(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) moved?.Invoke(e.position);
            else cancelled?.Invoke();
        }

        public void OnDrag(PointerEventData e) => moved?.Invoke(e.position);

        public void OnPointerMove(PointerEventData e) => moved?.Invoke(e.position);

        public void OnPointerUp(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) picked?.Invoke(e.position);
        }
    }
}
