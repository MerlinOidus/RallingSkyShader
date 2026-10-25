using System;
using System.Collections.Generic;
using UnityEngine;

namespace RTXOn
{
    /// <summary>The three curves of Adrian's menu design (2026-10-24) and a few helpers.</summary>
    public enum MenuEase { Linear, OutQuint, OutBack, InOutCubic, OutQuart, OutQuad, InQuint }

    /// <summary>
    /// A tiny tween runner for the Main Menu (no DOTween needed): runs on unscaled time, one tween per target and
    /// channel ("pos", "alpha"...), so starting a new one on the same channel replaces the old one. A tween whose
    /// target was destroyed stops by itself.
    /// </summary>
    public class MenuTween : MonoBehaviour
    {
        class Tw
        {
            public object target;
            public string channel;
            public float start, delay, duration;
            public MenuEase ease;
            public Action<float> apply;
            public Action done;
            public bool loop;    // yoyo forever
        }

        static MenuTween runner;
        readonly List<Tw> tweens = new List<Tw>();
        readonly List<Tw> running = new List<Tw>();

        static MenuTween Runner
        {
            get
            {
                if (runner == null) runner = new GameObject("Menu Tweens").AddComponent<MenuTween>();
                return runner;
            }
        }

        /// <summary>
        /// Runs <paramref name="apply"/> with the eased time 0..1 over <paramref name="duration"/> seconds after
        /// <paramref name="delay"/>. apply(0) runs at once, so the start state shows during the delay.
        /// </summary>
        public static void To(object target, string channel, float duration, MenuEase ease, Action<float> apply, float delay = 0f, Action done = null)
            => Add(target, channel, duration, ease, apply, delay, done, false);

        /// <summary>Back and forth forever (0 → 1 → 0 ...), each way <paramref name="duration"/> seconds.</summary>
        public static void Loop(object target, string channel, float duration, MenuEase ease, Action<float> apply, float delay = 0f)
            => Add(target, channel, duration, ease, apply, delay, null, true);

        /// <summary>Calls <paramref name="action"/> after <paramref name="seconds"/> (unscaled).</summary>
        public static void After(object target, string channel, float seconds, Action action)
            => Add(target, channel, 0.0001f, MenuEase.Linear, null, seconds, action, false);

        public static void Kill(object target, string channel = null)
        {
            if (runner == null) return;
            runner.tweens.RemoveAll(t => ReferenceEquals(t.target, target) && (channel == null || t.channel == channel));
        }

        static void Add(object target, string channel, float duration, MenuEase ease, Action<float> apply, float delay, Action done, bool loop)
        {
            var r = Runner;
            r.tweens.RemoveAll(t => ReferenceEquals(t.target, target) && t.channel == channel);
            var tw = new Tw
            {
                target = target, channel = channel, start = Time.unscaledTime, delay = Mathf.Max(0f, delay),
                duration = Mathf.Max(0.0001f, duration), ease = ease, apply = apply, done = done, loop = loop,
            };
            r.tweens.Add(tw);
            if (apply != null && !Dead(target)) apply(0f);
        }

        static bool Dead(object target) => target is UnityEngine.Object o && o == null;

        void Update()
        {
            float now = Time.unscaledTime;
            running.Clear();
            running.AddRange(tweens);
            foreach (var t in running)
            {
                if (!tweens.Contains(t)) continue; // replaced by a tween started this frame
                if (Dead(t.target)) { tweens.Remove(t); continue; }
                float u = (now - t.start - t.delay) / t.duration;
                if (u < 0f) continue;
                if (t.loop)
                {
                    float phase = u % 2f;
                    float x = phase <= 1f ? phase : 2f - phase;
                    t.apply?.Invoke(Ease(t.ease, x));
                    continue;
                }
                if (u >= 1f)
                {
                    tweens.Remove(t);
                    t.apply?.Invoke(1f);
                    t.done?.Invoke();
                    continue;
                }
                t.apply?.Invoke(Ease(t.ease, u));
            }
        }

        public static float Ease(MenuEase ease, float x)
        {
            x = Mathf.Clamp01(x);
            switch (ease)
            {
                case MenuEase.OutQuint: return 1f - Mathf.Pow(1f - x, 5f);
                case MenuEase.OutQuart: return 1f - Mathf.Pow(1f - x, 4f);
                case MenuEase.OutQuad: return 1f - (1f - x) * (1f - x);
                case MenuEase.InQuint: return x * x * x * x * x;
                case MenuEase.InOutCubic: return x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;
                case MenuEase.OutBack:
                {
                    const float s = 1.70158f;
                    float y = x - 1f;
                    return 1f + (s + 1f) * y * y * y + s * y * y;
                }
                default: return x;
            }
        }

        // ---------------- Shortcuts for RectTransforms and graphics ----------------

        public static void Move(RectTransform rt, Vector2 from, Vector2 to, float duration, MenuEase ease, float delay = 0f, Action done = null)
            => To(rt, "pos", duration, ease, e => rt.anchoredPosition = Vector2.LerpUnclamped(from, to, e), delay, done);

        public static void Scale(Transform t, float from, float to, float duration, MenuEase ease, float delay = 0f)
            => To(t, "scale", duration, ease, e => t.localScale = Vector3.one * Mathf.LerpUnclamped(from, to, e), delay);

        public static void Fade(CanvasGroup g, float from, float to, float duration, MenuEase ease, float delay = 0f, Action done = null)
            => To(g, "alpha", duration, ease, e => g.alpha = Mathf.LerpUnclamped(from, to, e), delay, done);

        public static void Color(UnityEngine.UI.Graphic g, Color from, Color to, float duration, MenuEase ease = MenuEase.Linear, float delay = 0f)
            => To(g, "color", duration, ease, e => g.color = UnityEngine.Color.LerpUnclamped(from, to, e), delay);
    }
}
