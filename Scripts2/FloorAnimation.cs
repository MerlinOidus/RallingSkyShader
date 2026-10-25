using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// Animated floor tiles 194, 196, 198, 199 (PartArgs), worked out from the ball's distance like every track piece,
    /// so a retry from a checkpoint looks right at once.
    /// <list type="bullet">
    /// <item>Wave: bobs up and down by <see cref="height"/> with its neighbours, flat from <see cref="startRows"/> before the ball.</item>
    /// <item>Spin In: spins and grows into place over <see cref="moveRows"/>, starting <see cref="startRows"/> before the ball.</item>
    /// <item>Extend: grows forward from its back edge, like a bridge building itself.</item>
    /// <item>Fall Away: <see cref="startRows"/> behind the ball it tumbles <see cref="height"/> down (only for show).</item>
    /// <item>Flip In (145), Pop In (146), Drawbridge (147), Screw Up (148), Roll In (149), Blink In (179): in place before the
    /// ball; Fly Away (178): behind the ball it spins up into the sky (only for show).</item>
    /// </list>
    /// Every animation is back home before the ball gets there.
    /// </summary>
    public class FloorAnimation : TrackBehaviour
    {
        public enum Mode { Wave, SpinIn, Extend, FallAway, FlipIn, PopIn, Drawbridge, ScrewUp, RollIn, FlyAway, BlinkIn }

        public Mode mode;
        public float startRows = 4f;
        public float height = 0.5f;
        public float moveRows = 3f;
        public bool spinLeft;

        Vector3 home, homeScale;
        Quaternion homeRotation;
        float phase;
        Renderer[] blinkers;

        protected override void Start()
        {
            base.Start();
            home = transform.localPosition;
            homeRotation = transform.localRotation;
            homeScale = transform.localScale;
            phase = transform.position.z * 0.55f + transform.position.x * 0.9f; // neighbours move one after another
            if (mode == Mode.BlinkIn) blinkers = GetComponentsInChildren<Renderer>();
            Update();
        }

        void Update()
        {
            float ahead = Ahead;
            switch (mode)
            {
                case Mode.Wave:
                {
                    float calm = Smooth((Mathf.Abs(ahead) - startRows) / 4f); // 0 near the ball, full 4 rows further
                    transform.localPosition = home + Vector3.up * (Mathf.Sin(Time.time * 2.6f - phase) * height * calm);
                    break;
                }
                case Mode.SpinIn:
                {
                    float e = Smooth((startRows - ahead) / Mathf.Max(0.01f, moveRows));
                    transform.localRotation = homeRotation * Quaternion.Euler(0f, (1f - e) * (spinLeft ? -360f : 360f), 0f);
                    transform.localScale = homeScale * Mathf.Max(0.01f, e);
                    break;
                }
                case Mode.Extend:
                {
                    float e = Mathf.Max(0.01f, Smooth((startRows - ahead) / Mathf.Max(0.01f, moveRows)));
                    transform.localScale = new Vector3(homeScale.x, homeScale.y, homeScale.z * e);
                    transform.localPosition = home + Vector3.back * (0.5f * (1f - e)); // the back edge stays put
                    break;
                }
                case Mode.FallAway:
                {
                    float t = (-startRows - ahead) / Mathf.Max(0.01f, moveRows);
                    if (t <= 0f)
                    {
                        transform.localPosition = home;
                        transform.localRotation = homeRotation;
                        break;
                    }
                    t = Mathf.Min(t, 1f);
                    float side = spinLeft ? -1f : 1f;
                    transform.localPosition = home + Vector3.down * (height * t * t);
                    transform.localRotation = homeRotation * Quaternion.Euler(t * 50f, 0f, t * 35f * side);
                    break;
                }
                case Mode.FlyAway:
                {
                    float t = (-startRows - ahead) / Mathf.Max(0.01f, moveRows);
                    if (t <= 0f)
                    {
                        transform.localPosition = home;
                        transform.localRotation = homeRotation;
                        transform.localScale = homeScale;
                        break;
                    }
                    t = Mathf.Min(t, 1f);
                    transform.localPosition = home + Vector3.up * (height * t * t);
                    transform.localRotation = homeRotation * Quaternion.Euler(0f, t * 540f * (spinLeft ? -1f : 1f), t * 40f);
                    transform.localScale = homeScale * Mathf.Max(0.01f, 1f - t);
                    break;
                }
                default:
                {
                    // The tiles that come into place: e goes 0 -> 1 over moveRows, from Start rows before the ball.
                    float e = Smooth((startRows - ahead) / Mathf.Max(0.01f, moveRows));
                    Pose(mode, e, height, spinLeft, home, homeRotation, homeScale, out var pos, out var rot, out var scale);
                    transform.localPosition = pos;
                    transform.localRotation = rot;
                    transform.localScale = scale;
                    if (mode == Mode.BlinkIn && blinkers != null)
                    {
                        // Flickers faster and faster, on for good once it is in place. Its collider never goes.
                        bool on = e >= 1f || (e > 0f && Mathf.Repeat((startRows - ahead) * (2f + 8f * e), 1f) < 0.35f + 0.6f * e);
                        foreach (var r in blinkers) if (r != null && r.enabled != on) r.enabled = on;
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// Where a tile that comes into place (Flip In, Pop In, Drawbridge, Screw Up, Roll In, Blink In) is at
        /// <paramref name="e"/> (0 = start, 1 = home). Shared with the level editor's preview (PlacedAnimation).
        /// </summary>
        public static void Pose(Mode mode, float e, float height, bool left, Vector3 home, Quaternion homeRotation, Vector3 homeScale,
            out Vector3 pos, out Quaternion rot, out Vector3 scale)
        {
            pos = home;
            rot = homeRotation;
            scale = homeScale;
            float side = left ? -1f : 1f;
            switch (mode)
            {
                case Mode.FlipIn:
                    rot = homeRotation * Quaternion.Euler(0f, 0f, (1f - e) * 180f * side);
                    pos = home + Vector3.up * (Mathf.Sin(e * Mathf.PI) * 0.6f);
                    break;
                case Mode.PopIn:
                {
                    // Back-out: a little too big, then settles.
                    const float c1 = 1.70158f, c3 = c1 + 1f;
                    float s = e <= 0f ? 0f : 1f + c3 * Mathf.Pow(e - 1f, 3f) + c1 * Mathf.Pow(e - 1f, 2f);
                    scale = homeScale * Mathf.Max(0.01f, s);
                    break;
                }
                case Mode.Drawbridge:
                {
                    // Stands on its back edge, the front up, and swings down flat.
                    float angle = (1f - e) * 90f;
                    var r = Quaternion.Euler(-angle, 0f, 0f);
                    rot = homeRotation * r;
                    pos = home + Vector3.back * 0.5f + r * (Vector3.forward * 0.5f);
                    break;
                }
                case Mode.ScrewUp:
                    pos = home + Vector3.down * (Mathf.Max(0.5f, height) * (1f - e));
                    rot = homeRotation * Quaternion.Euler(0f, (1f - e) * 720f * side, 0f);
                    break;
                case Mode.RollIn:
                {
                    // Two lanes away, turning half a turn per lane as it rolls into its own lane.
                    float lanes = 2f * (1f - e);
                    pos = home + new Vector3(-side * lanes, Mathf.Abs(Mathf.Sin(lanes * Mathf.PI)) * 0.2f, 0f);
                    rot = homeRotation * Quaternion.Euler(0f, 0f, side * lanes * 180f);
                    break;
                }
            }
        }

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
