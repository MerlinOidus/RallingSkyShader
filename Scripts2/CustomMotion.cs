using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// Custom Move Tile (157) / Custom Move Block (158): moves by <see cref="move"/> (tiles) and turns by
    /// <see cref="spin"/> (degrees) from where it was built. With Ball: as fast as the ball rolls (Z 20 rides 20 rows next
    /// to the ball); else in <see cref="seconds"/>. It starts <see cref="startRows"/> before the ball, or, when its
    /// <see cref="group"/> has a trigger tile, when that tile is touched. Loop: there and back, again and again.
    /// <see cref="speed"/> above 0 (tiles a second) takes the place of Seconds. Keep off: <see cref="vanish"/> seconds after
    /// the ball has passed it, it shrinks away and is gone (floor, block and what rides on it).
    /// </summary>
    public class CustomMotion : TrackBehaviour
    {
        public Vector3 move;
        public float spin;
        public bool withBall;
        public float seconds = 1f;
        public bool loop;
        public float startRows;
        public int group;
        [Tooltip("Tiles a second when With Ball is off; 0 = it takes Seconds.")]
        public float speed;
        public bool keep = true;
        [Tooltip("Keep off: seconds after the ball passed it before it disappears.")]
        public float vanish = 1.5f;

        const float ShrinkSeconds = 0.3f;
        float passedAt = float.NaN;
        Vector3 homeScale;

        Vector3 home;
        Quaternion homeRotation;
        float refZ;
        float startTime = float.NaN, startBallZ = float.NaN;

        protected override void Start()
        {
            base.Start();
            home = transform.localPosition;
            homeRotation = transform.localRotation;
            homeScale = transform.localScale;
            // Custom moves with the same group start together, from the group's first row (Ahead would move with us).
            refZ = GroupTriggers.FirstRow(group, Mathf.Round(transform.position.z));
            Update();
        }

        float Length => move.magnitude > 0.01f ? move.magnitude : Mathf.Max(1f, Mathf.Abs(spin) / 90f);
        float Duration => Mathf.Max(0.05f, speed > 0f ? Length / speed : seconds);

        /// <summary>Keep off: gone a while after the ball passed it.</summary>
        void Vanish(float ballZ)
        {
            if (keep) return;
            if (float.IsNaN(passedAt))
            {
                if (transform.position.z < ballZ - 0.6f) passedAt = Time.time;
                return;
            }
            float t = (Time.time - passedAt - vanish) / ShrinkSeconds;
            if (t <= 0f) return;
            if (t >= 1f)
            {
                gameObject.SetActive(false);
                return;
            }
            transform.localScale = homeScale * (1f - t * t);
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.ball == null)
            {
                Apply(0f);
                return;
            }
            float ballZ = gm.ball.Position.z;
            Vanish(ballZ);
            if (float.IsNaN(startTime))
            {
                if (GroupTriggers.HasTrigger(group))
                {
                    if (!GroupTriggers.TryFired(group, out float firedAt)) { Apply(0f); return; }
                    if (float.IsNegativeInfinity(firedAt))
                    {
                        // Fired before this run started (continued from a checkpoint): already at its end.
                        startTime = float.NegativeInfinity;
                        startBallZ = float.NegativeInfinity;
                    }
                    else
                    {
                        startTime = firedAt;
                        startBallZ = ballZ;
                    }
                }
                else if (refZ - ballZ <= startRows)
                {
                    startTime = Time.time;
                    startBallZ = refZ - startRows; // with the ball: counted from the start point, so a retry looks the same
                }
                else
                {
                    Apply(0f);
                    return;
                }
            }
            float u;
            if (float.IsNegativeInfinity(startTime)) u = loop ? Time.time / Duration : 1f;
            else if (withBall) u = (ballZ - startBallZ) / Length;
            else u = (Time.time - startTime) / Duration;
            if (u < 0f) u = 0f;
            float e = loop ? Mathf.PingPong(u, 1f) : Mathf.Min(u, 1f);
            if (!withBall) e = e * e * (3f - 2f * e);
            Apply(e);
        }

        void Apply(float e)
        {
            transform.localPosition = home + move * e;
            transform.localRotation = homeRotation * Quaternion.Euler(0f, spin * e, 0f);
        }
    }
}
