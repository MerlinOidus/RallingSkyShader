using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// Laser turret from the original game: when the ball is <see cref="fireRows"/> rows away it fires a beam
    /// back along its own lane, towards the ball, and the beam stays until the ball has passed.
    /// A ball still in that lane dies; jumping over it is safe. The beam object holds the light and the hazard collider.
    /// </summary>
    public class LaserBeam : TrackBehaviour
    {
        public GameObject beam;
        [Tooltip("Fires when the ball is this many rows away (original: 5).")]
        public float fireRows = 5f;
        [Tooltip("Switches off once the ball is this many rows past the turret.")]
        public float offRows = -1f;

        protected override void Start()
        {
            base.Start();
            if (beam != null) beam.SetActive(false);
        }

        void Update()
        {
            if (beam == null) return;
            float ahead = Ahead;
            bool on = ahead <= fireRows && ahead > offRows;
            if (beam.activeSelf == on) return;
            beam.SetActive(on);
            // The original shakes the camera as a laser fires (TileAnimator.FireLaser, force 0.8).
            if (on && ahead > fireRows - 1f) CameraRig.Shake(0.8f);
        }
    }
}
