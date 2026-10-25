using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// Moves a custom obstacle (its model and hitbox together) by its Animation tab: the approach from the From pose
    /// to its place, the spin and the bob. Timed by the ball's distance like the built-in obstacles. In the editor's
    /// preview the distance comes from <see cref="previewAhead"/> instead.
    /// </summary>
    public class CustomObstacleMotion : TrackBehaviour
    {
        public CustomObstacle def;
        [Tooltip("Editor preview: use Preview Ahead instead of the ball.")]
        public bool preview;
        public float previewAhead = 999f;
        [Tooltip("Set by a TriggerAnimator (the cell's Group): it stands still until the trigger, then the approach plays over its Seconds.")]
        public TriggerAnimator trigger;

        Vector3 restPosition, restScale;
        Quaternion restRotation;
        bool started;
        float wokeAt = float.NaN, wokeAhead;

        protected override void Start()
        {
            base.Start();
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
            restScale = transform.localScale;
            started = true;
            Update();
        }

        void Update()
        {
            if (!started || def == null) return;
            float ahead = preview ? previewAhead : Ahead;
            // No bob or spin before the trigger, or (no trigger) until the ball is Wake Rows away (Adrian 2026-10-24).
            bool waiting = trigger != null ? !trigger.Triggered : !preview && float.IsNaN(wokeAt) && ahead > WakeRows;
            if (!waiting && float.IsNaN(wokeAt))
            {
                wokeAt = Time.time;
                wokeAhead = preview ? 0f : ahead;
            }
            float s = trigger == null ? def.ApproachAt(ahead) : def.approach ? CustomObstacle.Ease(def.ease, trigger.Progress) : 1f;
            float bob = waiting ? 0f : ahead - wokeAhead; // starts from its rest height
            var p = restPosition + def.fromOffset * (1f - s);
            if (def.bobHeight != 0f) p.y += Mathf.Sin(bob / Mathf.Max(0.5f, def.bobRows) * Mathf.PI * 2f) * def.bobHeight;
            transform.localPosition = p;
            var r = restRotation * Quaternion.Euler(def.fromRotation * (1f - s));
            if (def.spin != Vector3.zero && !waiting) r *= Quaternion.Euler(def.spin * (Time.time - wokeAt));
            transform.localRotation = r;
            transform.localScale = restScale * Mathf.LerpUnclamped(def.fromScale, 1f, s);
        }
    }
}
