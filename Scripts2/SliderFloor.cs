using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// Slider floor (IDs 19, 20 and the slider variants): swings from side to side across the whole track, again and again,
    /// carrying the other sliders of its row (TrackBuilder parents them to the row's first slider),
    /// like Rolling Sky 1.0 (TileAnimator.PacePacer). Its items (tree, laser, gem...) are children, so they swing with it.
    /// </summary>
    public class SliderFloor : TrackBehaviour
    {
        /// <summary>True for the "left" tiles (19, 17, 23...): they swing the other way round.</summary>
        public bool left;
        int lane;

        protected override void Start()
        {
            base.Start();
            lane = Mathf.RoundToInt(transform.localPosition.x + 2f);
            Update();
        }

        void Update()
        {
            var p = transform.localPosition;
            p.x = Pace(Ahead, lane, left, 0.0125f);
            transform.localPosition = p;
        }

        /// <summary>
        /// Track x (lane c is at c - 2) of a slider or roller that belongs to <paramref name="lane"/>, when the ball is
        /// <paramref name="ahead"/> rows away. One full swing (lane 0 to lane 4 and back) takes 20 rows of ball travel;
        /// the phase depends on the lane, so the piece is near its own lane when the ball gets there.
        /// </summary>
        public static float Pace(float ahead, int lane, bool left, float animOffset)
        {
            int n = left ? 4 - lane : lane;
            float offset = (Mathf.Clamp(n, 0, 4) - 2) * 0.1f;
            float v = ahead / 20f - offset + animOffset;
            if (left) v = 1f - v;
            return -2f * Mathf.Sin(v * Mathf.PI * 2f);
        }
    }
}
