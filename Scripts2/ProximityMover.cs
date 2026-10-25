using UnityEngine;

namespace RTXOn
{
    /// <summary>Moves between two local positions as the ball approaches (risers, crushers).</summary>
    public class ProximityMover : TrackBehaviour
    {
        public Vector3 fromLocal;
        public Vector3 toLocal;
        [Tooltip("Starts moving when the ball is this many rows away.")]
        public float triggerRows = 7f;
        [Tooltip("Rows of ball travel the move takes.")]
        public float moveRows = 3f;
        public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

        protected override void Start()
        {
            base.Start();
            transform.localPosition = fromLocal;
        }

        void Update()
        {
            float t = Mathf.Clamp01((triggerRows - Ahead) / Mathf.Max(0.01f, moveRows));
            transform.localPosition = Vector3.LerpUnclamped(fromLocal, toLocal, ease.Evaluate(t));
        }
    }
}
