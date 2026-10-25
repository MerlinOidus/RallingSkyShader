using UnityEngine;

namespace RTXOn
{
    /// <summary>Jump pad: how far and high it throws the ball. It pops up a little when the ball takes off from it.</summary>
    public class JumpPad : MonoBehaviour
    {
        public float rows = 4f;
        public float height = 1.6f;
        [Tooltip("How high the pad pops up when the ball jumps off it.")]
        public float popHeight = 0.16f;
        [Tooltip("Seconds the pop takes, up and back down.")]
        public float popTime = 0.32f;

        float popAt = -1f;
        float lastOffset;

        /// <summary>The ball jumped off this pad. An original 07_jump pad also switches to its lit look (state02).</summary>
        public void Pop()
        {
            popAt = Time.time;
            TileShapes.SwitchState(transform, true);
        }

        void LateUpdate()
        {
            if (popAt < 0f) return;
            float p = (Time.time - popAt) / popTime;
            // Quick rise, softer settle, a tiny dip at the end like a spring.
            float offset = p >= 1f ? 0f : popHeight * Mathf.Sin(p * Mathf.PI) * (1f - 0.35f * p);
            // Moves by the change only, so a moving platform can still move the pad at the same time.
            transform.localPosition += Vector3.up * (offset - lastOffset);
            lastOffset = offset;
            if (p >= 1f) popAt = -1f;
        }
    }
}
