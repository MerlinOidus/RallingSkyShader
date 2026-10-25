using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// Short row animations: Rise = the row flies up into place (continuing from a crown, play test);
    /// Pop = a small hop that runs down the track ahead of the ball when a crown is taken.
    /// Moves the row's root on Y, then removes itself.
    /// </summary>
    public class RowFx : MonoBehaviour
    {
        public enum Kind { Rise, Pop }

        Kind kind;
        float delay, time;
        const float RiseTime = 0.45f, RiseDepth = 4f;
        const float PopTime = 0.32f, PopHeight = 0.22f;

        public static void Play(GameObject row, Kind kind, float delay)
        {
            if (row == null) return;
            var fx = row.GetComponent<RowFx>();
            if (fx == null) fx = row.AddComponent<RowFx>();
            fx.kind = kind;
            fx.delay = delay;
            fx.time = 0f;
            fx.Apply(0f);
        }

        void Update()
        {
            time += Time.deltaTime;
            float duration = kind == Kind.Rise ? RiseTime : PopTime;
            float p = Mathf.Clamp01((time - delay) / duration);
            Apply(p);
            if (p >= 1f) Destroy(this);
        }

        void Apply(float p)
        {
            var pos = transform.localPosition;
            if (kind == Kind.Rise)
            {
                // Ease out with a little overshoot, so the tiles settle into place.
                float k = 1.7f, q = p - 1f;
                float e = 1f + (k + 1f) * q * q * q + k * q * q;
                pos.y = -RiseDepth * (1f - e);
            }
            else pos.y = PopHeight * Mathf.Sin(p * Mathf.PI);
            transform.localPosition = pos;
        }

        void OnDestroy()
        {
            var pos = transform.localPosition;
            pos.y = 0f;
            transform.localPosition = pos;
        }
    }
}
