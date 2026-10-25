using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// Switch power-up: turns a built row over left-right (its X scale goes from 1 to -1 through 0), so lane 1 swaps
    /// with lane 5 and lane 2 with lane 4, obstacles and all. Removes itself when done.
    /// </summary>
    public class RowFlip : MonoBehaviour
    {
        public float target = -1f;
        public float delay;
        public float duration = 0.25f;

        float start;
        float time;

        void Start() => start = transform.localScale.x;

        void Update()
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01((time - delay) / Mathf.Max(0.01f, duration));
            t = t * t * (3f - 2f * t);
            var s = transform.localScale;
            s.x = Mathf.Lerp(start, target, t);
            transform.localScale = s;
            if (t >= 1f) Destroy(this);
        }
    }
}
