using UnityEngine;
using UnityEngine.UI;

namespace RTXOn
{
    /// <summary>
    /// Dialogue power-up: a speech bubble that pops up above the ball with a line of text ("WHO HE?") for a few
    /// seconds and follows it on screen. A Custom Obstacle's Dialogue pops up above the obstacle and follows it.
    /// A new one replaces the one still showing.
    /// </summary>
    public class DialoguePopup : MonoBehaviour
    {
        static readonly Color BubbleColor = new Color(1f, 1f, 1f, 0.95f);
        static readonly Color TextColor = new Color(0.1f, 0.1f, 0.13f);
        const float PopTime = 0.18f, FadeTime = 0.25f;
        const float AboveBall = 0.9f; // world units above the ball's centre

        static DialoguePopup current;

        BallController ball;
        Transform target; // a Custom Obstacle's bubble follows it instead of the ball
        float above = AboveBall;
        RectTransform canvasRect, bubble;
        CanvasGroup group;
        Canvas canvas;
        float shownAt, seconds;

        public static void Show(BallController ball, string text, float seconds)
        {
            if (ball == null) return;
            Show(ball, null, AboveBall, text, seconds);
        }

        /// <summary>A bubble <paramref name="height"/> tiles above <paramref name="follow"/> (a Custom Obstacle), following it.</summary>
        public static void Show(Transform follow, float height, string text, float seconds)
        {
            if (follow == null) return;
            Show(null, follow, height, text, seconds);
        }

        static void Show(BallController ball, Transform follow, float height, string text, float seconds)
        {
            if (current != null) Destroy(current.gameObject);
            text = string.IsNullOrWhiteSpace(text) ? PartArgs.DefaultDialogue : text.Trim();

            var canvas = UiFactory.MakeCanvas("Dialogue Popup", new Vector2(1920f, 1080f), 50);
            PortraitCanvas.Add(canvas); // gameplay is portrait on phones
            var popup = canvas.gameObject.AddComponent<DialoguePopup>();
            popup.canvas = canvas;
            popup.canvasRect = (RectTransform)canvas.transform;
            popup.ball = ball;
            popup.target = follow;
            popup.above = height;
            popup.seconds = Mathf.Clamp(seconds, PartArgs.MinShortSeconds, PartArgs.MaxShortSeconds);
            popup.shownAt = Time.time;
            popup.Build(text);
            current = popup;
        }

        void Build(string text)
        {
            var image = UiFactory.MakePanel(transform, "Bubble", BubbleColor);
            bubble = image.rectTransform;
            bubble.anchorMin = bubble.anchorMax = Vector2.zero;
            bubble.pivot = new Vector2(0.5f, 0f);
            // Width from the text: about 26 px per letter at size 46, between one short word and a long line.
            float width = Mathf.Clamp(text.Length * 26f + 70f, 180f, 760f);
            float height = text.Length * 26f + 70f > 760f ? 150f : 96f;
            bubble.sizeDelta = new Vector2(width, height);
            group = bubble.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            var outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.6f);
            outline.effectDistance = new Vector2(4f, -4f);

            // The little tail under the bubble, pointing at the ball.
            var tail = UiFactory.MakePanel(bubble, "Tail", BubbleColor);
            UiFactory.Place(tail.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30f, 30f));
            tail.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            var label = UiFactory.MakeLabel(bubble, text, 46, TextAnchor.MiddleCenter, TextColor, FontStyle.Bold);
            UiFactory.Stretch(label.rectTransform, 22f, 10f, 22f, 10f);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 20;
            label.resizeTextMaxSize = 46;
            Follow();
        }

        void LateUpdate()
        {
            float t = Time.time - shownAt;
            if (t >= seconds || ball == null && target == null)
            {
                Destroy(gameObject);
                return;
            }
            // Pops in (a little too big, then settles), fades out at the end.
            float pop = Mathf.Clamp01(t / PopTime);
            float scale = pop < 1f ? Mathf.Lerp(0.2f, 1.12f, pop) : Mathf.Lerp(1.12f, 1f, Mathf.Clamp01((t - PopTime) / PopTime));
            bubble.localScale = Vector3.one * scale;
            group.alpha = Mathf.Clamp01((seconds - t) / FadeTime);
            Follow();
        }

        void Follow()
        {
            var cam = Camera.main;
            if (cam == null || ball == null && target == null) return;
            var at = target != null ? target.position : ball.Position;
            var screen = cam.WorldToScreenPoint(at + Vector3.up * above);
            bool visible = screen.z > 0f;
            bubble.gameObject.SetActive(visible);
            if (!visible) return;
            // Screen pixels to canvas units (the canvas scales with the screen).
            float k = canvas.scaleFactor > 0f ? 1f / canvas.scaleFactor : 1f;
            bubble.anchoredPosition = new Vector2(screen.x * k, screen.y * k + 24f);
        }

        void OnDestroy()
        {
            if (current == this) current = null;
        }
    }

    /// <summary>A Custom Obstacle's Dialogue: its bubble pops up once when the ball is Rows away, above the obstacle and moving with it.</summary>
    public class ObstacleDialogue : TrackBehaviour
    {
        public string text = "";
        public float rows = 8f, seconds = 2f, height = 1.6f;
        bool shown;

        void Update()
        {
            if (shown || string.IsNullOrEmpty(text)) return;
            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Playing) return;
            float ahead = Ahead;
            if (ahead > rows || ahead < -0.5f) return;
            shown = true;
            DialoguePopup.Show(transform, height, text, seconds);
        }
    }
}
