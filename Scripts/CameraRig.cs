using System.Collections.Generic;
using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// Rolling Sky style chase camera: high behind the ball, looking down the track. Settings > Camera picks it:
    /// Normal (this chase camera), or the original game's Classic / High / Far camera (the camera trigger presets).
    /// A level's camera triggers take over from their row on.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        public BallController target;
        public Vector3 offset = new Vector3(0f, 5.2f, -5.8f);
        public float lookAhead = 6f;
        [Range(0f, 1f)] public float followX = 0.45f;
        [Range(0f, 1f)] public float followJumpY = 0.35f;
        public float smoothTime = 0.12f;
        [Tooltip("Seconds the camera takes to settle after the ball moves left or right (eases in and out, never jerks). " +
                 "Bigger = softer and slower, 0 = stuck to the ball.")]
        [Range(0f, 0.6f)] public float sideSmoothTime = 0.25f;
        [Tooltip("Camera shake strength (crusher slams, hammers, lasers firing, pyramids landing). 1 = like the original, 0 = off.")]
        [Range(0f, 2f)] public float shakeStrength = 1f;
        [Tooltip("Field of view (up and down) on every screen, portrait or landscape (Adrian: standard 60). " +
                 "0 = keep the camera's own.")]
        [Range(0f, 100f)] public float fieldOfView = 60f;

        Camera cam;

        Vector3 velocity;
        float trackedY;
        float smoothX, sideVelocity; // the camera's left-right, eased the same way in every camera mode
        readonly List<CameraChange> changes = new List<CameraChange>();

        /// <summary>
        /// Camera triggers of the level (level.json "cameras"). Each one moves the camera to its position and angles
        /// from its row on, blending over its Offset rows; before the first one the normal chase camera is used.
        /// Worked out from the ball's row every frame, so starting from a checkpoint is right at once.
        /// </summary>
        public void SetChanges(IEnumerable<CameraChange> list)
        {
            changes.Clear();
            if (list != null) foreach (var c in list) if (c != null) changes.Add(c);
            changes.Sort((a, b) => a.row.CompareTo(b.row));
        }

        void Start()
        {
            cam = GetComponent<Camera>();
            if (cam == null) cam = GetComponentInChildren<Camera>();
            Snap();
        }

        /// <summary>The same field of view on every screen (no fisheye widening on tall phones).</summary>
        void FitScreen()
        {
            if (cam == null || fieldOfView <= 0f) return;
            if (!Mathf.Approximately(cam.fieldOfView, fieldOfView)) cam.fieldOfView = fieldOfView;
        }

        public void Snap()
        {
            if (target == null) return;
            trackedY = 0f;
            smoothX = target.Position.x * followX;
            sideVelocity = 0f;
            shakeTimer = 0f;
            shakeForce = 0f;
            appliedShake = 0f;
            transform.position = Desired();
            transform.rotation = LookRotation();
            ApplyView(true);
            ApplyTriggers(true);
        }

        void LateUpdate()
        {
            if (target == null) return;
            transform.position -= Vector3.up * appliedShake; // last frame's shake never feeds the follow
            // Follow jumps a little, never follow a fall so the drop reads clearly.
            float y = target.Position.y - target.radius;
            trackedY = Mathf.Lerp(trackedY, Mathf.Max(0f, y) * followJumpY, 1f - Mathf.Exp(-6f * Time.deltaTime));
            // Left-right: a critically damped spring (SmoothDamp), so a lane change starts and stops softly.
            float wantX = target.Position.x * followX;
            smoothX = sideSmoothTime > 0f && Time.deltaTime > 0f
                ? Mathf.SmoothDamp(smoothX, wantX, ref sideVelocity, sideSmoothTime)
                : wantX;

            if (!ApplyView(false))
            {
                var desired = Desired();
                var p = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
                p.x = desired.x; // already eased (smoothX): a second lag would make it swing
                p.z = desired.z; // never lag behind along the track
                transform.position = p;
                transform.rotation = LookRotation();
            }
            ApplyTriggers(false);

            appliedShake = ShakeOffset() * shakeStrength;
            transform.position += Vector3.up * appliedShake;
            FitScreen();
        }

        // ---- Camera shake (Rolling Sky 1.0 CameraControl.ShakeCamera / TileAnimator.ShakeCamera) ----

        static float shakeForce, shakeStart, shakeTimer;
        float appliedShake;

        /// <summary>
        /// Shakes the camera up and down like the original: <paramref name="force"/> 0..1 (crusher 0.5, laser 0.8),
        /// weaker the further <paramref name="rowsAway"/> the piece is (nothing from 20 rows). A stronger shake
        /// replaces a weaker one; it lasts <paramref name="force"/> seconds.
        /// </summary>
        public static void Shake(float force, float rowsAway = 0f)
        {
            float f = (1f - Mathf.Abs(rowsAway) / 20f) * force;
            if (f <= 0f) return;
            f = Mathf.Sin((Mathf.Min(f, 1f) - 1f) * Mathf.PI * 0.5f) + 1f; // FloatAnim.Smooth(f, true, false)
            if (f <= shakeForce) return;
            shakeForce = f;
            shakeStart = f;
            shakeTimer = f;
        }

        /// <summary>CameraControl.CalculateShakeAmmount: 7 waves of 0.5 x force, over force seconds.</summary>
        static float ShakeOffset()
        {
            if (shakeTimer <= 0f) return 0f;
            shakeTimer -= Time.smoothDeltaTime;
            if (shakeTimer <= 0f)
            {
                shakeTimer = 0f;
                shakeForce = 0f;
                return 0f;
            }
            shakeForce = shakeTimer / shakeStart;
            return 0.5f * Mathf.Sin(shakeForce * 7f * Mathf.PI * 2f) * shakeStart;
        }

        /// <summary>
        /// Settings > Camera other than Normal: places the camera like that camera trigger preset (false for Normal).
        /// It is what a level's first camera trigger blends from.
        /// </summary>
        bool ApplyView(bool snap)
        {
            CameraChange v;
            switch (GameSettings.Camera)
            {
                case CameraView.Classic: v = CameraChange.Default(0); break;
                case CameraView.High: v = CameraChange.High(0); break;
                case CameraView.Far: v = CameraChange.Far(0); break;
                default: return false;
            }
            var b = target.Position;
            var pos = new Vector3(smoothX, trackedY, b.z) + v.position;
            transform.position = pos;
            transform.rotation = Quaternion.Euler(v.rotation);
            velocity = Vector3.zero;
            return true;
        }

        /// <summary>Camera triggers: blends from the camera before the active trigger to the trigger's own.</summary>
        void ApplyTriggers(bool snap)
        {
            if (changes.Count == 0) return;
            float z = target.Position.z;
            int index = -1;
            for (int i = 0; i < changes.Count; i++) if (changes[i].row <= z) index = i;
            if (index < 0) return; // normal chase camera until the first trigger

            var c = changes[index];
            var b = target.Position;
            var basePos = new Vector3(smoothX, trackedY, b.z);
            var toPos = basePos + c.position;
            var toRot = Quaternion.Euler(c.rotation);

            Vector3 fromPos;
            Quaternion fromRot;
            if (index > 0)
            {
                var p = changes[index - 1];
                fromPos = basePos + p.position;
                fromRot = Quaternion.Euler(p.rotation);
            }
            else
            {
                fromPos = transform.position; // the chase camera, already placed this frame
                fromRot = transform.rotation;
            }

            float t = c.rows > 0f ? (z - c.row) / c.rows : 1f;
            float e = c.Ease(t);
            var pos = Vector3.Lerp(fromPos, toPos, e);
            transform.position = pos;
            transform.rotation = Quaternion.Slerp(fromRot, toRot, e);
        }

        Vector3 Desired()
        {
            var b = target.Position;
            return new Vector3(smoothX, trackedY, b.z) + offset;
        }

        Quaternion LookRotation()
        {
            var b = target.Position;
            // Looks a little towards the ball's side too, eased like the position, so the view turns softly.
            var focus = new Vector3(smoothX * 0.6f, trackedY, b.z + lookAhead);
            return Quaternion.LookRotation(focus - transform.position, Vector3.up);
        }
    }
}
