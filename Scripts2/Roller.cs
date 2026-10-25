using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// Log that rolls from side to side across the track, again and again, like Rolling Sky 1.0
    /// (the same swing as <see cref="SliderFloor"/>, rolling without slipping). The original 03_enemyRoller is a
    /// wheel facing the ball that turns round its own axis (<see cref="drum"/>).
    /// </summary>
    public class Roller : TrackBehaviour
    {
        public bool left;
        public Transform spinner;
        [Tooltip("Original 03_enemyRoller's \"altAnim\": the wheel turns round its axis (local Z, along the track), " +
                 "rolling without slipping as it paces. Used instead of Spinner.")]
        public Transform drum;
        const float Radius = 0.4f;
        const float WheelRadius = 0.49f; // original roller mesh
        int lane;
        float laneX;

        protected override void Start()
        {
            base.Start();
            // The roller hangs under its tile, which sits at x = lane - 2 in the row.
            laneX = transform.parent != null ? transform.parent.localPosition.x : 0f;
            lane = Mathf.RoundToInt(laneX + 2f);
            Update();
        }

        void Update()
        {
            float x = SliderFloor.Pace(Ahead, lane, left, 0f);
            var p = transform.localPosition;
            p.x = x - laneX;
            transform.localPosition = p;
            if (drum != null)
                drum.localRotation = Quaternion.Euler(0f, 0f, -x / WheelRadius * Mathf.Rad2Deg);
            else if (spinner != null)
                spinner.localRotation = Quaternion.Euler(0f, 0f, -x / Radius * Mathf.Rad2Deg);
        }
    }
}
