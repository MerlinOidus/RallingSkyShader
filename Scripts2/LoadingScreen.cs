using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RTXOn
{
    /// <summary>
    /// Loading screen between scenes (level editor Test and back): a progress bar and a random tip.
    /// It loads the scene in the background, stays up until the new scene has built its first rows, then fades out,
    /// so the hitch of building the level never happens while the player can see the ball.
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        /// <summary>Tips shown while loading, one at random.</summary>
        public static readonly string[] Tips =
        {
            "This Actually Rolling Sky RTX ON i was fake",
            "Fragile tiles with the same colour fall together.",
            "A jump pad always throws you 4 rows ahead.",
            "3 crowns and 10 gems make a perfect run.",
            "Crash after a checkpoint? Tap to continue from it.",
            "Level editor: press V for the 3D view.",
            "Level editor: Start Pos sets where Test begins.",
            "Esc in a test goes back to the level editor.",
            "The edges glow by themselves when night falls.",
        };

        const float MinSeconds = 0.8f;   // long enough to read the tip
        const float FadeSeconds = 0.35f;

        static LoadingScreen current;

        CanvasGroup group;
        Image bar;
        Text percent;

        /// <summary>Shows the loading screen and opens <paramref name="scene"/> behind it.</summary>
        public static void Load(string scene)
        {
            if (current != null) return; // already loading
            var go = new GameObject("Loading Screen");
            DontDestroyOnLoad(go);
            current = go.AddComponent<LoadingScreen>();
            current.Build();
            current.StartCoroutine(current.Run(scene));
        }

        void OnDestroy()
        {
            if (current == this) current = null;
        }

        void Build()
        {
            var canvas = UiFactory.MakeCanvas("Loading UI", new Vector2(1920f, 1080f), 1000);
            canvas.transform.SetParent(transform, false);
            group = canvas.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = true; // nothing underneath takes clicks while loading
            var root = canvas.transform;

            var bg = UiFactory.MakePanel(root, "Background", new Color(0.06f, 0.05f, 0.1f), true);
            UiFactory.Stretch(bg.rectTransform);

            var title = UiFactory.MakeLabel(root, "ROLLING SKY  RTX ON", 72, TextAnchor.MiddleCenter, new Color(0.4f, 0.9f, 1f), FontStyle.Bold);
            UiFactory.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 190f), new Vector2(1400f, 110f));

            var loading = UiFactory.MakeLabel(root, "LOADING", 40, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            UiFactory.Place(loading.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-260f, 50f), new Vector2(520f, 60f));
            percent = UiFactory.MakeLabel(root, "0%", 40, TextAnchor.MiddleRight, Color.white, FontStyle.Bold);
            UiFactory.Place(percent.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(260f, 50f), new Vector2(520f, 60f));

            var track = UiFactory.MakePanel(root, "Bar Back", new Color(1f, 1f, 1f, 0.15f));
            UiFactory.Place(track.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(1040f, 22f));
            bar = UiFactory.MakePanel(track.transform, "Bar", new Color(0.4f, 0.9f, 1f));
            var b = bar.rectTransform;
            b.anchorMin = Vector2.zero;
            b.anchorMax = new Vector2(0f, 1f);
            b.pivot = new Vector2(0f, 0.5f);
            b.offsetMin = Vector2.zero;
            b.offsetMax = Vector2.zero;

            var tipTitle = UiFactory.MakeLabel(root, "TIP", 28, TextAnchor.MiddleCenter, new Color(1f, 0.82f, 0.3f), FontStyle.Bold);
            UiFactory.Place(tipTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), new Vector2(400f, 44f));
            var tip = UiFactory.MakeLabel(root, Tips[Random.Range(0, Tips.Length)], 34, TextAnchor.UpperCenter, new Color(0.85f, 0.82f, 0.9f));
            UiFactory.Place(tip.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1400f, 120f));
        }

        void Show(float progress)
        {
            progress = Mathf.Clamp01(progress);
            bar.rectTransform.anchorMax = new Vector2(progress, 1f);
            percent.text = Mathf.RoundToInt(progress * 100f) + "%";
        }

        IEnumerator Run(string scene)
        {
            float start = Time.unscaledTime;
            Show(0f);
            yield return null; // draw the screen once before the loading work starts

            var op = SceneManager.LoadSceneAsync(scene);
            if (op == null)
            {
                Destroy(gameObject);
                yield break;
            }
            op.allowSceneActivation = false;
            // Loading goes to 0.9, then waits for activation; the last 10% is the new scene starting up.
            while (op.progress < 0.9f || Time.unscaledTime - start < MinSeconds)
            {
                Show(Mathf.Min(op.progress / 0.9f * 0.9f, (Time.unscaledTime - start) / MinSeconds * 0.9f));
                yield return null;
            }
            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;

            // Two frames for the scene's Start (track rows, music) to run while the screen still covers it.
            Show(0.95f);
            yield return null;
            yield return null;
            Show(1f);

            for (float t = 0f; t < FadeSeconds; t += Time.unscaledDeltaTime)
            {
                group.alpha = 1f - t / FadeSeconds;
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
