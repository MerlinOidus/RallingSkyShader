using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace RTXOn
{
    /// <summary>
    /// The gameplay HUD built in code, in the Main Menu's look (Adrian's design 2026-10-24, uicoba.pdf pages 7-8):
    /// the Pause button top left, the progress bar with its percent at the top, crowns and gems under the Pause
    /// button, the centre message (ready, 3-2-1, retry), the Complete screen, and the Pause screen (PAUSED picture,
    /// crowns, progress, gems, Restart, and Levels / Resume / Settings / Close) with a small Settings sheet.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        Text title;
        Text message;
        Text gems;
        Text crowns;
        Text percent;
        RectTransform barRoot, barFill;
        RectTransform canvasRoot, top;
        CanvasGroup topGroup;
        CanvasScaler scaler;
        bool builtPortrait;
        // Adrian's icons (UI/Resources/Icons): gem / crown beside the counters, the start button, the checkpoint badge.
        Image gemIcon, crownIcon, startImage, checkpointImage;
        int gemCount, gemTotal, crownCount, crownTotal;
        float progress;

        // Pause
        GameObject pauseButton;
        RectTransform pauseLayer, settingsLayer;
        CanvasGroup pauseContent;
        Button resumeButton, settingsButton;
        Action onPause, onResume, onRestart, onLevels, onHome;
        bool pauseShown;
        // PC / Xbox screens (PcMenu): the button row of the Pause or Complete screen, and what Complete's buttons do.
        PcButtonRow pcRow;
        Action onCompleteLevels, onCompleteRestart, onCompleteBack;
        string levelName = "";
        /// <summary>The PC / Xbox HUD (NewUiForPc page 2): a thin white-edged bar with the percent on it, black counters.</summary>
        bool pcLook;
        Text pauseHead;

        void Awake() => Build();

        void Build()
        {
            if (title != null) return;

            var canvasGo = new GameObject("HUD", typeof(RectTransform));
            canvasRoot = (RectTransform)canvasGo.transform;
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>(); // the Pause button and menu take clicks
            FitScaler();

            var font = MenuStyle.font != null ? MenuStyle.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            title = MakeText(canvasRoot, font, 120, new Vector2(0.5f, 0.62f), FontStyle.Bold);
            message = MakeText(canvasRoot, font, 54, new Vector2(0.5f, 0.52f), FontStyle.Normal);
            startImage = Picture(canvasRoot, "StartButton", new Vector2(0.5f, 0.4f), new Vector2(401f, 138f));
            checkpointImage = Picture(canvasRoot, "CheckPoint", new Vector2(0.5f, 0.66f), new Vector2(360f, 360f));

            // Top: Pause button, progress bar with its percent, crowns and gems (hidden while the Pause screen shows).
            top = UiFactory.MakeRect(canvasRoot, "Top");
            UiFactory.Stretch(top);
            topGroup = top.gameObject.AddComponent<CanvasGroup>();

            pcLook = PcMenu.On && !ScreenRotation.Portrait; // a PC window held upright gets the phone HUD, like the Main Menu
            if (pcLook) barRoot = PcProgressBar(top, 3f, 26f, out barFill, out percent);
            else
            {
                barRoot = MenuStyle.Panel(top, "Progress Bar", MenuSlot.Primary, 1f, MenuStyle.Pill).rectTransform;
                barFill = MenuStyle.Panel(barRoot, "Fill", MenuSlot.Accent, 1f, MenuStyle.Pill).rectTransform;
                barFill.anchorMin = Vector2.zero;
                barFill.anchorMax = new Vector2(0f, 1f);
                barFill.offsetMin = barFill.offsetMax = Vector2.zero;
                percent = MenuStyle.Label(top, "0%", 62f, TextAnchor.MiddleCenter, Color.white, true, true);
                AddShadow(percent, 4f);
            }

            crownIcon = CounterIcon(UiFactory.CrownIcon, CrownGold, out crowns);
            gemIcon = CounterIcon(UiFactory.GemIcon, GemBlue, out gems);

            pauseButton = BuildPauseButton().gameObject;
            pauseButton.SetActive(false);

            LayoutTop();
            SetGems(0, 0);
            SetCrowns(0, 0);
        }

        // ---------------- Layout ----------------

        /// <summary>
        /// Like the Main Menu: portrait = 1080 wide, landscape = 1080 tall, so the HUD keeps its size when the window
        /// or the phone turns.
        /// </summary>
        void FitScaler()
        {
            builtPortrait = ScreenRotation.Portrait;
            scaler.referenceResolution = builtPortrait ? new Vector2(1080f, 1920f) : new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = builtPortrait ? 0f : 1f;
        }

        /// <summary>The canvas size right now (the canvas rect only updates a frame later after a turn).</summary>
        Vector2 CanvasSize()
        {
            float w = Mathf.Max(1f, Screen.width), h = Mathf.Max(1f, Screen.height);
            return builtPortrait ? new Vector2(1080f, h * 1080f / w) : new Vector2(w * 1080f / h, 1080f);
        }

        const float Margin = 40f, PauseSize = 130f;
        /// <summary>The top row's height: lower on a phone, clear of the camera hole (ScreenRotation.TopInset).</summary>
        float TopY => -(Margin + PauseSize * 0.5f) - ScreenRotation.TopInset(CanvasSize().y);

        // The PC HUD (NewUiForPc page 2, 1920 x 1080): Pause and the bar on one line, crown and gem counts under Pause.
        const float PcTopY = -187f, PcPauseX = 159f, PcPauseSize = 110f;

        void LayoutTop()
        {
            if (pcLook)
            {
                UiFactory.Place(barRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, PcTopY), new Vector2(384f, 28f));
                if (pauseButton != null)
                    UiFactory.Place((RectTransform)pauseButton.transform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f),
                        new Vector2(PcPauseX, PcTopY), new Vector2(PcPauseSize, PcPauseSize));
                LayoutCounters();
                return;
            }
            float width = builtPortrait ? 600f : 760f;
            UiFactory.Place(barRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, TopY), new Vector2(width, 24f));
            UiFactory.Place(percent.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, TopY), new Vector2(400f, 90f));
            if (pauseButton != null)
                UiFactory.Place((RectTransform)pauseButton.transform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f),
                    new Vector2(Margin + PauseSize * 0.5f, TopY), new Vector2(PauseSize, PauseSize));
            LayoutCounters();
        }

        /// <summary>Crowns then gems under the Pause button; no crown row when the level has no crowns.</summary>
        void LayoutCounters()
        {
            bool crownRow = crownTotal > 0;
            float y = pcLook ? -266f : TopY - PauseSize * 0.5f - 60f;
            if (crownRow)
            {
                PlaceCounter(crownIcon, crowns, y);
                y -= pcLook ? 66f : 90f;
            }
            crownIcon.enabled = crowns.enabled = crownRow;
            PlaceCounter(gemIcon, gems, y);
        }

        void PlaceCounter(Image icon, Text count, float y)
        {
            if (pcLook)
            {
                UiFactory.Place(icon.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(PcPauseX, y), new Vector2(60f, 60f));
                UiFactory.Place(count.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(PcPauseX + 44f, y), new Vector2(360f, 70f));
                return;
            }
            UiFactory.Place(icon.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(Margin + 36f, y), new Vector2(72f, 72f));
            UiFactory.Place(count.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(Margin + 84f, y), new Vector2(360f, 80f));
        }

        /// <summary>
        /// The PC bar (NewUiForPc pages 2 and 3): a white edge <paramref name="edge"/> thick, light grey fill, and the
        /// percent in dark letters riding at the end of the fill.
        /// </summary>
        static RectTransform PcProgressBar(Transform parent, float edge, float fontSize, out RectTransform fill, out Text label)
        {
            var root = UiFactory.MakeRect(parent, "Progress Bar (PC)");
            var fillImage = UiFactory.MakePanel(root, "Fill", MenuStyle.Hex("#E6E6E6"));
            fill = fillImage.rectTransform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            // The white edge: four thin sides (the inside stays see-through).
            Edge(root, Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, edge));
            Edge(root, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -edge));
            Edge(root, Vector2.zero, new Vector2(0f, 1f), new Vector2(edge, 0f));
            Edge(root, new Vector2(1f, 0f), Vector2.one, new Vector2(-edge, 0f));
            label = MenuStyle.Label(root, "0%", fontSize, TextAnchor.MiddleCenter, PcMenu.DarkInk, false, true);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.08f, 0.5f);
            label.rectTransform.sizeDelta = new Vector2(fontSize * 3f, fontSize * 1.4f);
            return root;
        }

        static void Edge(RectTransform root, Vector2 min, Vector2 max, Vector2 thickness)
        {
            var side = UiFactory.MakePanel(root, "Edge", Color.white).rectTransform;
            side.anchorMin = min;
            side.anchorMax = max;
            // thickness grows inward from that side
            side.offsetMin = new Vector2(Mathf.Min(0f, thickness.x), Mathf.Min(0f, thickness.y));
            side.offsetMax = new Vector2(Mathf.Max(0f, thickness.x), Mathf.Max(0f, thickness.y));
        }

        /// <summary>The percent of a PC bar rides at the end of the fill (kept inside the bar).</summary>
        static void PlacePcPercent(Text label, float p)
        {
            float x = Mathf.Clamp(p, 0.08f, 0.92f);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(x, 0.5f);
            label.rectTransform.anchoredPosition = Vector2.zero;
        }

        /// <summary>A counter: the gem / crown icon and its "3/20" (numbers in the menu's number font).</summary>
        Image CounterIcon(Sprite sprite, Color drawnColor, out Text count)
        {
            var image = UiFactory.MakePanel(top, sprite.name + " Icon", UiFactory.IsPicture(sprite) ? Color.white : drawnColor);
            image.sprite = sprite;
            image.preserveAspect = true;
            if (pcLook)
            {
                // PC (page 2): black numbers, no shadows.
                count = MenuStyle.Label(top, "", 58f, TextAnchor.MiddleLeft, PcMenu.DarkInk, false, true);
                return image;
            }
            image.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0.1f, 0.2f, 0.45f);
            count = MenuStyle.Label(top, "", 56f, TextAnchor.MiddleLeft, Color.white, true, true);
            AddShadow(count, 3f);
            return image;
        }

        RectTransform BuildPauseButton()
        {
            var back = UiFactory.MakePanel(top, "II Button", Color.clear, true);
            var b = back.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.navigation = UiFactory.NoNavigation;
            // PC keeps it on the Pause screen (page 3): pressed again it resumes.
            b.onClick.AddListener(() =>
            {
                if (pauseShown) onResume?.Invoke();
                else onPause?.Invoke();
            });
            var icon = MenuStyle.Icon(back.transform, "Pause", pcLook ? 72f : 100f, Color.white);
            if (icon != null) icon.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0.1f, 0.2f, 0.45f);
            else AddShadow(MenuStyle.Label(back.transform, "II", 80f, TextAnchor.MiddleCenter, Color.white, true), 3f);
            MenuPress.Add(b, 0.86f);
            return back.rectTransform;
        }

        static void AddShadow(Text t, float distance)
        {
            var shadow = t.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0.1f, 0.2f, 0.5f);
            shadow.effectDistance = new Vector2(distance, -distance);
        }

        /// <summary>A picture from the Icons folder (hidden until shown), or null when there is none.</summary>
        static Image Picture(Transform parent, string icon, Vector2 anchor, Vector2 size)
        {
            var sprite = UiFactory.Icon(icon);
            if (sprite == null) return null;
            var image = UiFactory.MakePanel(parent, icon, Color.white);
            image.sprite = sprite;
            image.preserveAspect = true;
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = anchor;
            image.rectTransform.sizeDelta = size;
            image.enabled = false;
            return image;
        }

        static void Show(Image image, bool on)
        {
            if (image != null) image.enabled = on;
        }

        static Text MakeText(Transform parent, Font font, int size, Vector2 anchor, FontStyle style)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.sizeDelta = new Vector2(1000, 200);
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0.1f, 0.2f, 0.45f);
            shadow.effectDistance = new Vector2(3, -3);
            return t;
        }

        // ---------------- Messages ----------------

        public void ShowReady(string levelTitle, bool playTest = false)
        {
            Build();
            HideComplete();
            levelName = levelTitle ?? "";
            title.text = levelTitle;
            message.text = playTest ? "PLAY TEST\n<size=36>Tap to start  ·  Esc = back to editor</size>"
                                    : "TAP TO START\n<size=36>Drag to steer</size>";
            message.supportRichText = true;
            Show(startImage, true);
            SetProgress(0f);
        }

        /// <summary>Ready screen when continuing from a checkpoint.</summary>
        public void ShowCheckpointReady(bool playTest = false)
        {
            title.text = "CHECKPOINT";
            message.supportRichText = true;
            message.text = playTest ? "TAP TO CONTINUE\n<size=36>Esc = back to editor</size>" : "TAP TO CONTINUE\n<size=36>Drag to steer</size>";
            Show(startImage, true);
        }

        public void ShowPlaying()
        {
            HideComplete();
            title.text = "";
            message.text = "";
            Show(startImage, false);
            if (pauseButton != null) pauseButton.SetActive(true);
        }

        public void ShowDead(float progress, bool fromCheckpoint = false, bool playTest = false)
        {
            if (pauseButton != null) pauseButton.SetActive(false);
            Show(startImage, false);
            Show(checkpointImage, false);
            title.text = $"{Mathf.FloorToInt(progress * 100f)}%";
            message.supportRichText = true;
            string back = playTest ? "  ·  Esc = editor" : "";
            message.text = fromCheckpoint ? $"TAP TO CONTINUE\n<size=30>from checkpoint  ·  R = restart{back}</size>"
                                          : $"TAP TO RETRY{(playTest ? "\n<size=30>Esc = back to editor</size>" : "")}";
        }

        /// <summary>3, 2, 1 after Resume ("" clears it); each number pops in.</summary>
        public void ShowCountdown(string text)
        {
            if (title == null) return;
            title.text = text;
            flashUntil = -1f;
            if (!string.IsNullOrEmpty(text)) MenuTween.Scale(title.transform, 1.6f, 1f, 0.4f, MenuEase.OutBack);
            else title.transform.localScale = Vector3.one;
        }

        /// <summary>The Check Point badge for a moment (the word CHECKPOINT when the project has no badge picture).</summary>
        public void FlashCheckpoint()
        {
            if (checkpointImage == null)
            {
                Flash("CHECKPOINT");
                return;
            }
            checkpointImage.enabled = true;
            checkpointImage.rectTransform.localScale = Vector3.one;
            checkpointUntil = Time.time + 1.2f;
        }

        float checkpointUntil = -1f;

        /// <summary>A short line over the track for a moment while playing (checkpoint, controller speed).</summary>
        public void Flash(string text)
        {
            if (title == null) return;
            title.text = text;
            flashText = text;
            flashUntil = Time.time + 1.2f;
        }

        float flashUntil = -1f;
        string flashText;

        void Update()
        {
            // The window or the phone turned: the HUD follows, and an open Pause screen is laid out again.
            if (scaler != null && ScreenRotation.Portrait != builtPortrait)
            {
                FitScaler();
                LayoutTop();
                if (pauseShown) BuildPauseScreen(false);
            }
            if (checkpointUntil > 0f)
            {
                float left = checkpointUntil - Time.time;
                if (left <= 0f)
                {
                    checkpointUntil = -1f;
                    Show(checkpointImage, false);
                }
                else if (left < 0.3f) checkpointImage.rectTransform.localScale = Vector3.one * (left / 0.3f); // shrinks away
            }
            if (flashUntil > 0f && Time.time >= flashUntil)
            {
                flashUntil = -1f;
                if (title != null && title.text == flashText) title.text = "";
            }
            if (problemUntil > 0f && Time.unscaledTime >= problemUntil)
            {
                problemUntil = -1f;
                if (problem != null) problem.gameObject.SetActive(false);
            }
        }

        // ---------------- Build problem ----------------

        RectTransform problem;
        Text problemText;
        float problemUntil = -1f;

        /// <summary>
        /// A part of the track that could not be built (TrackBuilder.BuildProblem): the reason stays at the bottom of the
        /// screen for a while, so it can be screenshot on a phone, where there is no Console.
        /// </summary>
        public void ShowProblem(string text)
        {
            Build();
            if (problem == null)
            {
                problem = UiFactory.MakeRect(canvasRoot, "Build Problem");
                problem.anchorMin = new Vector2(0f, 0f);
                problem.anchorMax = new Vector2(1f, 0f);
                problem.pivot = new Vector2(0.5f, 0f);
                problem.offsetMin = new Vector2(30f, 30f);
                problem.offsetMax = new Vector2(-30f, 290f);
                var bg = problem.gameObject.AddComponent<Image>();
                bg.color = new Color(0.45f, 0.05f, 0.08f, 0.9f);
                bg.raycastTarget = false;
                UiFactory.Round(bg, 1f);
                // The built-in font: every letter of an error message is there.
                problemText = MakeText(problem, Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), 30,
                    new Vector2(0.5f, 0.5f), FontStyle.Normal);
                var rt = problemText.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(24f, 16f);
                rt.offsetMax = new Vector2(-24f, -16f);
                problemText.horizontalOverflow = HorizontalWrapMode.Wrap;
                problemText.verticalOverflow = VerticalWrapMode.Truncate;
                problemText.resizeTextForBestFit = true;
                problemText.resizeTextMinSize = 16;
                problemText.resizeTextMaxSize = 30;
            }
            problemText.text = text;
            problem.gameObject.SetActive(true);
            problem.SetAsLastSibling();
            problemUntil = Time.unscaledTime + 15f;
        }

        // ---------------- Complete ----------------

        /// <summary>
        /// The Complete screen (Adrian 2026-10-23): a crown icon per crown of the level, gold when taken and an empty
        /// outline when not (0 taken = all empty; no crowns in the level = no crown row), and the gem icon with the
        /// count (0 gems = an empty gem). The icons pop in one by one.
        /// </summary>
        public void ShowWon(int crownCount, int crownTotal, int gemCount, int gemTotal, bool playTest = false)
        {
            if (pauseButton != null) pauseButton.SetActive(false);
            Show(startImage, false);
            Show(checkpointImage, false);
            SetProgress(1f);
            message.text = "";
            if (pcLook && onCompleteRestart != null)
            {
                title.text = "";
                topGroup.alpha = 0f;
                BuildPcComplete(crownCount, crownTotal, gemCount, gemTotal);
                return;
            }
            title.text = "COMPLETE";
            BuildComplete(crownCount, crownTotal, gemCount, gemTotal,
                playTest ? "TAP = BACK TO EDITOR  ·  R = PLAY AGAIN" : "TAP TO PLAY AGAIN");
        }

        static readonly Color CrownGold = new Color(1f, 0.82f, 0.3f);
        static readonly Color GemBlue = new Color(0.45f, 0.95f, 1f);
        static readonly Color EmptyIcon = new Color(1f, 1f, 1f, 0.16f);
        GameObject completePanel;

        void BuildComplete(int crownCount, int crownTotal, int gemCount, int gemTotal, string tap)
        {
            if (completePanel != null) Destroy(completePanel);
            var root = new GameObject("Complete", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(canvasRoot, false);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.47f);
            root.sizeDelta = new Vector2(1000f, 520f);
            completePanel = root.gameObject;
            var pops = new List<RectTransform>();

            float y = 150f;
            if (crownTotal > 0)
            {
                const float Size = 150f, Step = 175f;
                float x0 = -(crownTotal - 1) * Step * 0.5f;
                for (int i = 0; i < crownTotal; i++)
                {
                    bool got = i < crownCount;
                    var crown = UiFactory.CrownIcon; // Adrian's gold crown: shown as it is (white), the drawn one tinted gold
                    var color = got ? (UiFactory.IsPicture(crown) ? Color.white : CrownGold) : EmptyIcon;
                    pops.Add(Icon(root, crown, color, new Vector2(x0 + i * Step, y), Size, !got));
                }
                y -= 200f;
            }

            bool anyGem = gemCount > 0;
            var gemSprite = UiFactory.GemIcon;
            var gemColor = anyGem ? (UiFactory.IsPicture(gemSprite) ? Color.white : GemBlue) : EmptyIcon;
            var gem = Icon(root, gemSprite, gemColor, new Vector2(anyGem ? -110f : 0f, y), 120f, !anyGem);
            pops.Add(gem);
            if (anyGem)
            {
                var count = UiFactory.MakeLabel(root, Count(gemCount, gemTotal), 84, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
                UiFactory.Place(count.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(150f, y), new Vector2(360f, 120f));
                count.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0.1f, 0.2f, 0.5f);
                pops.Add(count.rectTransform);
            }

            var tapLabel = UiFactory.MakeLabel(root, tap, 36, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.85f));
            UiFactory.Place(tapLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y - 160f), new Vector2(1000f, 60f));
            StartCoroutine(PopIn(pops));
        }

        /// <summary>An icon; <paramref name="empty"/> = only its outline (not taken).</summary>
        static RectTransform Icon(RectTransform parent, Sprite sprite, Color color, Vector2 pos, float size, bool empty)
        {
            var img = UiFactory.MakePanel(parent, "Icon", color);
            img.sprite = sprite;
            img.preserveAspect = true;
            UiFactory.Place(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
            // Taken: a dark edge. Empty: a faint shape with a light rim, so the slot still reads as a crown / gem.
            var outline = img.gameObject.AddComponent<Outline>();
            outline.useGraphicAlpha = false;
            outline.effectColor = empty ? new Color(1f, 1f, 1f, 0.35f) : new Color(0f, 0f, 0f, 0.5f);
            outline.effectDistance = empty ? new Vector2(2f, -2f) : new Vector2(3f, -3f);
            img.rectTransform.localScale = Vector3.zero;
            return img.rectTransform;
        }

        System.Collections.IEnumerator PopIn(List<RectTransform> items)
        {
            foreach (var item in items) if (item != null) item.localScale = Vector3.zero;
            yield return new WaitForSecondsRealtime(0.35f);
            foreach (var item in items)
            {
                for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.28f)
                {
                    if (item == null) yield break;
                    float k = 1f + 0.25f * Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI); // overshoot, then settle
                    item.localScale = Vector3.one * Mathf.Clamp01(t) * k;
                    yield return null;
                }
                if (item != null) item.localScale = Vector3.one;
            }
        }

        void HideComplete()
        {
            if (completePanel != null) Destroy(completePanel);
            completePanel = null;
            if (topGroup != null && !pauseShown) topGroup.alpha = 1f;
        }

        /// <summary>The Complete screen has buttons (PC): GameManager leaves the clicks and keys to them.</summary>
        public bool CompleteHasButtons => completePanel != null && pcRow != null && pcRow.transform.IsChildOf(completePanel.transform);

        /// <summary>What the PC Complete screen's buttons do: Levels (null = none), Restart, Back.</summary>
        public void SetupComplete(Action levels, Action restart, Action back)
        {
            onCompleteLevels = levels;
            onCompleteRestart = restart;
            onCompleteBack = back;
        }

        /// <summary>
        /// The PC Complete screen (NewUiForPc page 1): the level's name, its crowns, "Level Complete 100%", the gems, the
        /// full progress bar, then Levels / Restart (chosen) / Back and the hint bar.
        /// </summary>
        void BuildPcComplete(int crownCount, int crownTotal, int gemCount, int gemTotal)
        {
            if (completePanel != null) Destroy(completePanel);
            var root = UiFactory.MakeRect(canvasRoot, "Complete (PC)");
            UiFactory.Stretch(root);
            completePanel = root.gameObject;
            var scrim = UiFactory.MakePanel(root, "Scrim", new Color(0.02f, 0.05f, 0.08f, 0.45f), true);
            UiFactory.Stretch(scrim.rectTransform);
            var pops = new List<RectTransform>();

            float y = 330f;
            var name = MenuStyle.Label(root, levelName.ToUpperInvariant(), 104f, TextAnchor.MiddleCenter, Color.white, true);
            UiFactory.Place(name.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(1400f, 130f));
            AddShadow(name, 5f);
            pops.Add(name.rectTransform);
            y -= 125f;

            if (crownTotal > 0)
            {
                const float Size = 105f, Step = 118f;
                float x0 = -(crownTotal - 1) * Step * 0.5f;
                for (int i = 0; i < crownTotal; i++)
                {
                    bool got = i < crownCount;
                    var crown = UiFactory.CrownIcon;
                    var color = got ? (UiFactory.IsPicture(crown) ? Color.white : CrownGold) : (UiFactory.IsPicture(crown) ? CrownGrey : EmptyIcon);
                    pops.Add(Icon((RectTransform)root, crown, color, new Vector2(x0 + i * Step, y), Size, !got));
                }
                y -= 105f;
            }

            var done = MenuStyle.Label(root, MenuStyle.T("Level Selesai 100%", "Level Complete 100%"), 62f, TextAnchor.MiddleCenter, Color.white, false, true);
            UiFactory.Place(done.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(1200f, 90f));
            AddShadow(done, 3f);
            pops.Add(done.rectTransform);
            y -= 85f;

            var gemRow = UiFactory.MakeRect(root, "Gems");
            UiFactory.Place(gemRow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(800f, 80f));
            var layout = gemRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 10f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var gemSprite = UiFactory.GemIcon;
            var gem = UiFactory.MakePanel(gemRow, "Gem", UiFactory.IsPicture(gemSprite) ? Color.white : GemBlue);
            gem.sprite = gemSprite;
            gem.preserveAspect = true;
            var gemSize = gem.gameObject.AddComponent<LayoutElement>();
            gemSize.preferredWidth = gemSize.preferredHeight = 70f;
            var gemText = MenuStyle.Label(gemRow, Count(gemCount, gemTotal), 62f, TextAnchor.MiddleLeft, Color.white, false, true);
            AddShadow(gemText, 3f);
            pops.Add(gemRow);
            y -= 85f;

            var bar = MenuStyle.Panel(root, "Progress Bar", MenuSlot.Primary, 1f, MenuStyle.Pill).rectTransform;
            UiFactory.Place(bar, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(560f, 30f));
            var fill = MenuStyle.Panel(bar, "Fill", MenuSlot.Accent, 1f, MenuStyle.Pill).rectTransform;
            UiFactory.Stretch(fill);
            pops.Add(bar);

            var hint = PcHintBar.Build(root, false);
            var items = new List<PcButtonRow.Item>();
            if (onCompleteLevels != null) items.Add(new PcButtonRow.Item("Levels", "Layer", "Pilih level", "Level Select", () => onCompleteLevels?.Invoke()));
            items.Add(new PcButtonRow.Item("Restart", "Rotate", "Mulai awal lagi", "Play again", () => onCompleteRestart?.Invoke()));
            if (onCompleteBack != null) items.Add(new PcButtonRow.Item("Back", "ArrowLeft", "Kembali", "Back", () => onCompleteBack?.Invoke()));
            pcRow = PcButtonRow.Build(root, new Vector2(0.5f, 0f), new Vector2(0f, PcHintBar.Height + 230f), items,
                items.FindIndex(i => i.name == "Restart"), hint, 120f);
            pops.Add((RectTransform)pcRow.transform);
            StartCoroutine(PopIn(pops));
        }

        // ---------------- Pause ----------------

        /// <summary>
        /// The Pause button's and Pause screen's actions. <paramref name="levels"/> null = no Levels button (not
        /// opened from the level list); <paramref name="home"/> null = no Close button (the level was opened straight
        /// from its scene).
        /// </summary>
        public void SetupPause(Action pause, Action resume, Action restart, Action levels, Action home)
        {
            Build();
            onPause = pause;
            onResume = resume;
            onRestart = restart;
            onLevels = levels;
            onHome = home;
            EnsureEventSystem();
            pauseButton.SetActive(false);
            HidePauseScreen(true);
        }

        public void ShowPause(bool on)
        {
            if (pauseButton == null) return;
            if (pcLook)
            {
                // PC (page 3): the HUD stays, Pause included; PAUSED and a bigger bar take the small bar's place.
                pauseButton.SetActive(true);
                topGroup.blocksRaycasts = true;
                topGroup.alpha = 1f;
                barRoot.gameObject.SetActive(!on);
                if (on) BuildPauseScreen(true);
                else HidePauseScreen(false);
                return;
            }
            pauseButton.SetActive(!on);
            topGroup.blocksRaycasts = !on;
            float from = topGroup.alpha, to = on ? 0f : 1f;
            if (from != to) MenuTween.To(topGroup, "alpha", 0.2f, MenuEase.Linear, e => topGroup.alpha = Mathf.Lerp(from, to, e));
            if (on) BuildPauseScreen(true);
            else HidePauseScreen(false);
        }

        /// <summary>The design's colours for a crown not taken yet: the gold picture darkened to grey.</summary>
        static readonly Color CrownGrey = new Color(0.33f, 0.33f, 0.36f, 1f);

        /// <summary>
        /// The Pause screen (pdf page 7): PAUSED, the level's crowns (gold = taken, grey = not), the progress bar and
        /// percent, the gems, a Restart button, and at the bottom Levels, Resume (big, Accent), Settings and Close.
        /// Sizes follow the screen's height, so it also fits a landscape window.
        /// </summary>
        void BuildPauseScreen(bool animate)
        {
            if (pauseLayer != null) Destroy(pauseLayer.gameObject);
            settingsLayer = null;
            pauseShown = true;
            if (pcLook)
            {
                BuildPcPauseScreen(animate);
                return;
            }
            var size = CanvasSize();
            float inset = ScreenRotation.TopInset(size.y); // clear of the camera hole
            float k = Mathf.Clamp((size.y - inset) / 1750f, 0.58f, 1f);

            pauseLayer = UiFactory.MakeRect(canvasRoot, "Pause");
            UiFactory.Stretch(pauseLayer);
            var layerGroup = pauseLayer.gameObject.AddComponent<CanvasGroup>();
            var scrim = UiFactory.MakePanel(pauseLayer, "Scrim", new Color(0.02f, 0.05f, 0.08f, 0.55f), true); // blocks clicks to the game
            UiFactory.Stretch(scrim.rectTransform);
            var content = UiFactory.MakeRect(pauseLayer, "Content");
            UiFactory.Stretch(content);
            content.offsetMax = new Vector2(0f, -inset);
            pauseContent = content.gameObject.AddComponent<CanvasGroup>();

            var pops = new List<RectTransform>();
            float y = -200f * k;

            var head = UiFactory.MakeRect(content, "Title");
            UiFactory.Place(head, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(1000f, 200f * k));
            var headText = MenuStyle.Title(head, "Paused", "PAUSED", "PAUSED", 150f * k, TextAnchor.MiddleCenter, Color.white, true);
            if (headText is Text t) AddShadow(t, 5f);
            y -= 190f * k;

            if (crownTotal > 0)
            {
                var row = UiFactory.MakeRect(content, "Crowns");
                UiFactory.Place(row, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(800f, 120f * k));
                float step = 135f * k, x0 = -(crownTotal - 1) * step * 0.5f;
                for (int i = 0; i < crownTotal; i++)
                {
                    bool got = i < crownCount;
                    var crown = UiFactory.CrownIcon;
                    var color = got ? (UiFactory.IsPicture(crown) ? Color.white : CrownGold) : (UiFactory.IsPicture(crown) ? CrownGrey : EmptyIcon);
                    var img = UiFactory.MakePanel(row, "Crown", color);
                    img.sprite = crown;
                    img.preserveAspect = true;
                    UiFactory.Place(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x0 + i * step, 0f), new Vector2(115f * k, 115f * k));
                    var outline = img.gameObject.AddComponent<Outline>();
                    outline.effectColor = new Color(0f, 0f, 0f, 0.55f);
                    outline.effectDistance = new Vector2(3f, -3f);
                    pops.Add(img.rectTransform);
                }
                y -= 150f * k;
            }

            // Progress: the HUD's bar, bigger, with the percent on it.
            var bar = MenuStyle.Panel(content, "Progress Bar", MenuSlot.Primary, 1f, MenuStyle.Pill).rectTransform;
            UiFactory.Place(bar, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(640f * k, 28f * k));
            var fill = MenuStyle.Panel(bar, "Fill", MenuSlot.Accent, 1f, MenuStyle.Pill).rectTransform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            var pct = MenuStyle.Label(bar, Mathf.FloorToInt(progress * 100f) + "%", 76f * k, TextAnchor.MiddleCenter, Color.white, true, true);
            UiFactory.Place(pct.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400f, 110f * k));
            AddShadow(pct, 4f);
            pops.Add(bar);
            y -= 120f * k;

            // Gems: icon and count, centred together.
            var gemRow = UiFactory.MakeRect(content, "Gems");
            UiFactory.Place(gemRow, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(800f, 110f * k));
            var layout = gemRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 16f * k;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var gemSprite = UiFactory.GemIcon;
            var gem = UiFactory.MakePanel(gemRow, "Gem", UiFactory.IsPicture(gemSprite) ? Color.white : GemBlue);
            gem.sprite = gemSprite;
            gem.preserveAspect = true;
            var gemSize = gem.gameObject.AddComponent<LayoutElement>();
            gemSize.preferredWidth = gemSize.preferredHeight = 100f * k;
            gem.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0.1f, 0.2f, 0.45f);
            var gemText = MenuStyle.Label(gemRow, Count(gemCount, gemTotal), 84f * k, TextAnchor.MiddleLeft, Color.white, true, true);
            AddShadow(gemText, 4f);
            pops.Add(gemRow);
            y -= 160f * k;

            // Restart: a capsule with Adrian's RESTART picture.
            var restartBack = MenuStyle.Panel(content, "Restart", MenuSlot.Primary2, 1f, MenuStyle.Pill, true);
            UiFactory.Place(restartBack.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(460f * k, 120f * k));
            var restart = restartBack.gameObject.AddComponent<Button>();
            restart.transition = Selectable.Transition.None;
            restart.onClick.AddListener(() => onRestart?.Invoke());
            var restartLabel = UiFactory.MakeRect(restartBack.transform, "Label");
            UiFactory.Stretch(restartLabel);
            MenuStyle.Title(restartLabel, "Restart", "RESTART", "RESTART", 58f * k, TextAnchor.MiddleCenter, Color.white, true);
            MenuPress.Add(restart, 0.9f);
            pops.Add(restartBack.rectTransform);

            // Bottom row: Levels, Resume, Settings, Close (only the ones this level can use).
            float small = 190f * k, big = 225f * k, gap = 18f * k, rowY = 70f * k + big * 0.5f;
            var buttons = new List<(string name, string icon, float size, bool main, Action click, bool spin)>();
            if (onLevels != null) buttons.Add(("Levels", "Layer", small, false, () => onLevels?.Invoke(), false));
            buttons.Add(("Resume", "Play", big, true, () => onResume?.Invoke(), false));
            buttons.Add(("Settings", "Settings", small, false, OpenSettings, false));
            if (onHome != null) buttons.Add(("Close", "Close", small, false, () => onHome?.Invoke(), true));
            float total = -gap;
            foreach (var b in buttons) total += b.size + gap;
            float x = -total * 0.5f;
            var row2 = new List<Button>();
            var rowRects = new List<RectTransform>();
            foreach (var b in buttons)
            {
                var button = PauseScreenButton(content, b.name, b.icon, b.size, b.main, b.click, b.spin);
                var rt = (RectTransform)button.transform;
                UiFactory.Place(rt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(x + b.size * 0.5f, rowY), new Vector2(b.size, b.size));
                x += b.size + gap;
                row2.Add(button);
                rowRects.Add(rt);
                if (b.main) resumeButton = button;
                if (b.name == "Settings") settingsButton = button;
            }

            // Controller / keyboard: left and right along the bottom row, up to Restart, down from Restart to Resume.
            for (int i = 0; i < row2.Count; i++)
                row2[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnLeft = i > 0 ? row2[i - 1] : null,
                    selectOnRight = i < row2.Count - 1 ? row2[i + 1] : null,
                    selectOnUp = restart,
                };
            restart.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = resumeButton };
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);

            if (!animate) return;
            MenuTween.Fade(layerGroup, 0f, 1f, 0.22f, MenuEase.Linear);
            var headPos = head.anchoredPosition;
            MenuTween.Move(head, headPos + new Vector2(0f, 90f), headPos, 0.55f, MenuEase.OutBack);
            for (int i = 0; i < pops.Count; i++) MenuTween.Scale(pops[i], 0.6f, 1f, 0.45f, MenuEase.OutBack, 0.06f + i * 0.04f);
            for (int i = 0; i < rowRects.Count; i++)
            {
                var to = rowRects[i].anchoredPosition;
                MenuTween.Move(rowRects[i], to - new Vector2(0f, 320f * k), to, 0.55f, MenuEase.OutQuint, 0.05f + i * 0.05f);
            }
        }

        /// <summary>
        /// The PC Pause screen (NewUiForPc page 3): the game and the HUD stay in view (Pause, crowns, gems); PAUSED and a
        /// bigger bar with the percent at the top; Levels, Settings, Play (chosen) and Back above a see-through hint bar
        /// that says what the chosen one does. Keys only (GameManager stops the game's own input while paused).
        /// </summary>
        void BuildPcPauseScreen(bool animate)
        {
            pauseLayer = UiFactory.MakeRect(canvasRoot, "Pause (PC)");
            UiFactory.Stretch(pauseLayer);
            var layerGroup = pauseLayer.gameObject.AddComponent<CanvasGroup>();
            var content = UiFactory.MakeRect(pauseLayer, "Content");
            UiFactory.Stretch(content);
            pauseContent = content.gameObject.AddComponent<CanvasGroup>();

            pauseHead = MenuStyle.Label(content, "PAUSED", 60f, TextAnchor.MiddleCenter, PcMenu.DarkInk);
            UiFactory.Place(pauseHead.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -149f), new Vector2(800f, 80f));
            var bar = PcProgressBar(content, 4f, 36f, out var fill, out var pct);
            UiFactory.Place(bar, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -207f), new Vector2(652f, 42f));
            fill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
            pct.text = Mathf.FloorToInt(progress * 100f) + "%";
            PlacePcPercent(pct, progress);

            var hint = PcHintBar.Build(content, new Color(0.25f, 0.25f, 0.25f, 0.7f), PcMenu.DarkInk, Color.white);
            var items = new List<PcButtonRow.Item>();
            if (onLevels != null) items.Add(new PcButtonRow.Item("Levels", "Layer", "Pilih level", "Level Select", () => onLevels?.Invoke()));
            items.Add(new PcButtonRow.Item("Settings", "Settings", "Pengaturan", "Settings", OpenSettings));
            items.Add(new PcButtonRow.Item("Resume", "Play", "Lanjut main", "Tap to Play", () => onResume?.Invoke()));
            if (onHome != null) items.Add(new PcButtonRow.Item("Back", "ArrowLeft", "Kembali", "Back", () => onHome?.Invoke()));
            pcRow = PcButtonRow.Build(content, new Vector2(0.5f, 0f), new Vector2(0f, PcHintBar.Height + 119f), items,
                items.FindIndex(i => i.name == "Resume"), hint, 120f);
            resumeButton = null;
            settingsButton = null;

            if (!animate) return;
            MenuTween.Fade(layerGroup, 0f, 1f, 0.22f, MenuEase.Linear);
            var row = (RectTransform)pcRow.transform;
            var to = row.anchoredPosition;
            MenuTween.Move(row, to - new Vector2(0f, 300f), to, 0.5f, MenuEase.OutQuint);
            var headRt = pauseHead.rectTransform;
            var headTo = headRt.anchoredPosition;
            MenuTween.Move(headRt, headTo + new Vector2(0f, 60f), headTo, 0.45f, MenuEase.OutBack);
        }

        Button PauseScreenButton(Transform parent, string name, string icon, float size, bool main, Action click, bool spin)
        {
            var back = MenuStyle.Panel(parent, name, main ? MenuSlot.Accent : MenuSlot.Primary, main ? 1f : 0.85f, null, true);
            var b = back.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => click?.Invoke());
            var color = main ? MenuStyle.Get(MenuSlot.OnAccent) : Color.white;
            var image = MenuStyle.Icon(back.transform, icon, size * 0.55f, color);
            if (image != null)
            {
                var shadow = image.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.3f);
                shadow.effectDistance = new Vector2(0f, -3f);
            }
            else MenuStyle.Label(back.transform, name.ToUpperInvariant(), size * 0.16f, TextAnchor.MiddleCenter, color, true);
            MenuPress.Add(b, 0.86f, spin);
            return b;
        }

        void HidePauseScreen(bool instant)
        {
            pauseShown = false;
            settingsLayer = null;
            if (pauseLayer == null) return;
            var layer = pauseLayer;
            pauseLayer = null;
            if (instant)
            {
                Destroy(layer.gameObject);
                return;
            }
            var group = layer.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            MenuTween.Fade(group, group.alpha, 0f, 0.18f, MenuEase.Linear, 0f, () =>
            {
                if (layer != null) Destroy(layer.gameObject);
            });
        }

        // ---------------- Settings (from the Pause screen) ----------------

        public bool SettingsOpen => settingsLayer != null;

        /// <summary>
        /// The sheet in the Main Menu's Settings look with what matters mid-level: music and sound volume,
        /// sensitivity, smooth steering and the controller speed. Everything else is in the Main Menu's Settings.
        /// </summary>
        void OpenSettings()
        {
            if (pauseLayer == null || settingsLayer != null) return;
            var size = CanvasSize();
            settingsLayer = UiFactory.MakeRect(pauseLayer, "Settings");
            UiFactory.Stretch(settingsLayer);
            var group = settingsLayer.gameObject.AddComponent<CanvasGroup>();
            pauseContent.interactable = false;
            if (pcRow != null) pcRow.interactable = false;

            var scrim = UiFactory.MakePanel(settingsLayer, "Scrim", new Color(0f, 0f, 0f, 0.45f), true);
            UiFactory.Stretch(scrim.rectTransform);
            var close = scrim.gameObject.AddComponent<Button>();
            close.transition = Selectable.Transition.None;
            close.navigation = UiFactory.NoNavigation;
            close.onClick.AddListener(() => CloseSettings());

            const float Header = 135f, RowHeight = 113f, Pad = 32f;
            const int Rows = 5;
            float width = Mathf.Min(size.x - 76f, 1000f);
            float height = Mathf.Min(size.y - 76f, Header + Rows * RowHeight + 50f);
            var sheet = MenuStyle.Panel(settingsLayer, "Settings Sheet", MenuSlot.Panel, 1f, null, true).rectTransform;
            UiFactory.Place(sheet, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, height));
            sheet.gameObject.AddComponent<Mask>().showMaskGraphic = true; // round corners for the header too

            var header = MenuStyle.Panel(sheet, "Header", MenuSlot.Primary);
            header.sprite = null;
            var hr = header.rectTransform;
            hr.anchorMin = new Vector2(0f, 1f);
            hr.anchorMax = Vector2.one;
            hr.pivot = new Vector2(0.5f, 1f);
            hr.offsetMin = new Vector2(0f, -Header);
            hr.offsetMax = Vector2.zero;
            var gear = MenuStyle.Icon(header.transform, "SettingsFilled", 84f, Color.white);
            if (gear != null) UiFactory.Place(gear.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(40f + 42f, 0f), new Vector2(84f, 84f));
            var titleRect = UiFactory.MakeRect(header.transform, "Title");
            UiFactory.Place(titleRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(150f, 6f), new Vector2(width - 330f, 100f));
            MenuStyle.Title(titleRect, "Settings", "Pengaturan", "Settings", 65f, TextAnchor.MiddleLeft, Color.white);
            var xBack = UiFactory.MakePanel(header.transform, "Close", new Color(0f, 0f, 0f, 0.18f), true);
            UiFactory.Place(xBack.rectTransform, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-Header * 0.5f, 0f), new Vector2(Header, Header));
            var x = xBack.gameObject.AddComponent<Button>();
            x.transition = Selectable.Transition.None;
            x.onClick.AddListener(() => CloseSettings());
            if (MenuStyle.Icon(xBack.transform, "Close", 80f, Color.white) == null)
                MenuStyle.Label(xBack.transform, "X", 70f, TextAnchor.MiddleCenter, Color.white, true);
            MenuPress.Add(x, 0.86f, true);

            float y = -Header - 25f;
            Selectable first = SliderSetting(sheet, ref y, Pad, "Musik", "Music", 0f, 100f, 1f,
                () => GameSettings.MusicVolume * 100f, v => GameSettings.MusicVolume = v / 100f, v => $"{v:0}");
            SliderSetting(sheet, ref y, Pad, "Efek Suara", "Sound FX", 0f, 100f, 1f,
                () => GameSettings.SfxVolume * 100f, v => GameSettings.SfxVolume = v / 100f, v => $"{v:0}");
            SliderSetting(sheet, ref y, Pad, "Sensitivity", "Sensitivity",
                GameSettings.MouseScale * GameSettings.MinSensitivity, GameSettings.MouseScale * GameSettings.MaxSensitivity, 10f,
                () => GameSettings.MouseSensitivity, v => GameSettings.MouseSensitivity = Mathf.RoundToInt(v), v => $"{v:0}");
            var smooth = MenuToggle.Make(SettingLine(sheet, ref y, Pad, "Smooth", "Smooth"), GameSettings.SmoothSteer, v => GameSettings.SmoothSteer = v);
            UiFactory.Place((RectTransform)smooth.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(MenuToggle.Width, MenuToggle.Height));
            smooth.Button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            SliderSetting(sheet, ref y, Pad, "Kecepatan Controller", "Controller Speed",
                GameSettings.MinSensitivity, GameSettings.MaxSensitivity, 0.05f,
                () => GameSettings.StickSensitivity, v => GameSettings.StickSensitivity = v, v => $"x{v:0.00}");

            MenuTween.Fade(group, 0f, 1f, 0.2f, MenuEase.Linear);
            MenuTween.Move(sheet, new Vector2(0f, -size.y * 0.6f), Vector2.zero, 0.5f, MenuEase.OutQuint);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        /// <summary>Closes the Settings sheet; false when it was not open (Back then resumes the game).</summary>
        public bool CloseSettings()
        {
            if (settingsLayer == null) return false;
            var layer = settingsLayer;
            settingsLayer = null;
            if (pauseContent != null) pauseContent.interactable = true;
            if (pcRow != null) pcRow.interactable = true;
            var group = layer.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            MenuTween.Fade(group, 1f, 0f, 0.18f, MenuEase.Linear, 0f, () =>
            {
                if (layer != null) Destroy(layer.gameObject);
            });
            if (EventSystem.current != null && settingsButton != null) EventSystem.current.SetSelectedGameObject(settingsButton.gameObject);
            return true;
        }

        /// <summary>A setting row like the Main Menu's: its name on the left, the rect its control goes in on the right.</summary>
        static RectTransform SettingLine(RectTransform sheet, ref float y, float pad, string indonesian, string english)
        {
            const float Height = 113f;
            var row = UiFactory.MakeRect(sheet, english);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = Vector2.one;
            row.pivot = new Vector2(0.5f, 1f);
            row.offsetMin = new Vector2(pad, y - Height);
            row.offsetMax = new Vector2(-pad, y);
            y -= Height;
            var line = UiFactory.MakePanel(row, "Line", new Color(1f, 1f, 1f, 0.04f));
            line.rectTransform.anchorMin = Vector2.zero;
            line.rectTransform.anchorMax = new Vector2(1f, 0f);
            line.rectTransform.offsetMin = Vector2.zero;
            line.rectTransform.offsetMax = new Vector2(0f, 2f);
            var label = MenuStyle.Label(row, MenuStyle.T(indonesian, english), 50f, TextAnchor.MiddleLeft, Color.white);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = new Vector2(0.46f, 1f);
            label.rectTransform.offsetMin = new Vector2(10f, 0f);
            label.rectTransform.offsetMax = Vector2.zero;
            MenuStyle.Fit(label, 26);
            var area = UiFactory.MakeRect(row, "Control");
            area.anchorMin = new Vector2(0.48f, 0.5f);
            area.anchorMax = new Vector2(1f, 0.5f);
            area.pivot = new Vector2(0.5f, 0.5f);
            area.offsetMin = new Vector2(0f, -40f);
            area.offsetMax = new Vector2(-10f, 40f);
            return area;
        }

        static Selectable SliderSetting(RectTransform sheet, ref float y, float pad, string indonesian, string english, float min, float max, float step,
            Func<float> get, Action<float> set, Func<float, string> format)
        {
            var s = MenuSlider.Make(SettingLine(sheet, ref y, pad, indonesian, english), min, max);
            var slider = s.Slider;
            slider.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            float now = get();
            slider.SetValueWithoutNotify(now);
            s.Value.text = format(now);
            slider.onValueChanged.AddListener(v =>
            {
                v = Mathf.Clamp(Mathf.Round(v / step) * step, min, max);
                set(v);
                s.Value.text = format(v);
            });
            return slider;
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        // ---------------- Counters ----------------

        int lastPercent = -1;

        public void SetProgress(float p)
        {
            progress = p;
            barFill.anchorMax = new Vector2(Mathf.Clamp01(p), 1f);
            if (pcLook) PlacePcPercent(percent, p);
            int pct = Mathf.FloorToInt(p * 100f);
            if (pct == lastPercent) return; // no new string every frame
            lastPercent = pct;
            percent.text = pct + "%";
        }

        /// <summary>"3/10"; just "3" when the level has no gems to count.</summary>
        public void SetGems(int count, int total)
        {
            gemCount = count;
            gemTotal = total;
            gems.text = pcLook ? count.ToString() : Count(count, total); // PC (page 2): just the number
        }

        /// <summary>"1/3"; the crown row is hidden when the level has no crowns.</summary>
        public void SetCrowns(int count, int total)
        {
            crownCount = count;
            crownTotal = total;
            crowns.text = total <= 0 ? "" : pcLook ? count.ToString() : $"{count}/{total}";
            LayoutCounters();
        }

        static string Count(int count, int total) => total > 0 ? $"{count}/{total}" : count.ToString();
    }
}
