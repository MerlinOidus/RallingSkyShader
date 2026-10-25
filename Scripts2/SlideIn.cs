using UnityEngine;

namespace RTXOn
{
    /// <summary>Slides sideways into its lane as the ball approaches (slider floors, binary blocks).</summary>
    public class SlideIn : TrackBehaviour
    {
        public float offsetX = 2f;
        public float settleRows = 2.5f;
        public float slideRows = 6f;
        float homeX;

        protected override void Start()
        {
            base.Start();
            homeX = transform.localPosition.x;
            Update();
        }

        void Update()
        {
            float t = Mathf.Clamp01((Ahead - settleRows) / slideRows);
            t = t * t * (3f - 2f * t);
            var p = transform.localPosition;
            p.x = homeX + offsetX * t;
            transform.localPosition = p;
        }
    }
}
