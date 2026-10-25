using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// Spins a binary on the spot like Rolling Sky 1.0 (TileAnimator.SpinSpinner, period 4 rows):
    /// an eased half turn every 2 rows of ball travel, locked to the ball's distance.
    /// </summary>
    public class BinarySpinner : TrackBehaviour
    {
        public Transform spin;
        [Tooltip("Optional: spin turns about this transform's up axis (original meshes, whose \"anim\" child spins). Empty = spin's own local Y.")]
        public Transform pivot;
        [Tooltip("Right binaries turn one way, left ones the other (the original's altAnim).")]
        public bool clockwise = true;
        public float periodRows = 4f;
        [Tooltip("Hammer (original 04_enemyHammer): swings 146 degrees on Z and back once per 2 beats, instead of spinning.")]
        public bool hammer;
        [Tooltip("Original world beat length in rows (World 1 = 6).")]
        public float beatRows = 6f;

        bool? wasBack;

        void Update()
        {
            if (spin == null) return;
            if (hammer)
            {
                // TileAnimator.PoundHammer: offset 6 beats (+1 for left), half the cycle down, half back up.
                float d = Ahead;
                float cycle = DistanceCycle(d, beatRows, clockwise ? 6f : 7f);
                bool back = cycle >= 0.5f;
                if (wasBack.HasValue && wasBack.Value != back) CameraRig.Shake(0.5f, d); // the original shakes on each pound
                wasBack = back;
                float t = cycle % 0.5f * 2f;
                float a = cycle >= 0.5f ? SmoothEnd(1f - t) : SmoothStart(t);
                var swing = Quaternion.Euler(0f, 0f, 146f * a);
                if (pivot != null) spin.rotation = pivot.rotation * swing;
                else spin.localRotation = swing;
                return;
            }
            float turns = SteppedWave01(DistanceCycle(Ahead, periodRows, clockwise ? 1f : 0f), 2);
            if (!clockwise) turns = -turns;
            var turn = Quaternion.Euler(0f, 360f * turns, 0f);
            if (pivot != null) spin.rotation = pivot.rotation * turn; // like the original: anim upright, spun about Y
            else spin.localRotation = turn;
        }

        /// <summary>TileAnimator.GetDistanceCycle: 0..1, one cycle every 2 periods.</summary>
        static float DistanceCycle(float distance, float period, float offset)
        {
            float c = (distance - offset * period) / period / 2f;
            return c < 0f ? -(c % 1f) : 1f - c % 1f;
        }

        /// <summary>FloatAnim.Smooth(v, true, false, 3).</summary>
        static float SmoothStart(float v)
        {
            v = v * v * v;
            return Mathf.Sin((v - 1f) * Mathf.PI * 0.5f) + 1f;
        }

        /// <summary>FloatAnim.Smooth(v, false, true, 3).</summary>
        static float SmoothEnd(float v)
        {
            v = 1f - (1f - v) * (1f - v) * (1f - v);
            return Mathf.Sin(v * Mathf.PI * 0.5f);
        }

        /// <summary>FloatAnim.SteppedWave01: <paramref name="steps"/> eased steps from 0 to 1.</summary>
        static float SteppedWave01(float value, int steps)
        {
            value *= steps;
            float whole = Mathf.Floor(value);
            float rest = value - whole;
            float eased = (1f - Mathf.Cos(rest * Mathf.PI)) * 0.5f; // Wave01(rest / 2)
            return eased / steps + whole / steps;
        }
    }
}
