using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// One object with one animation that waits for its trigger: Trigger Object (169) and Custom Obstacles with a Group.
    /// Until a CustomTriggerTiles with the same <see cref="group"/> is touched it stands still, or loops the clip's
    /// first <see cref="frames"/> (60 a second). Then the rest of the clip plays, once or again and again
    /// (<see cref="loop"/>), as made in Blender (<see cref="seconds"/> 0) or stretched to <see cref="seconds"/>.
    /// The hitbox sits on the animated parts (<see cref="AnimatedHitbox"/>), so it moves with the animation.
    /// <para>Without a clip it drives what else the object has: a custom obstacle's Animation tab
    /// (<see cref="CustomObstacleMotion"/>) or a part moved from one place to another (<see cref="mover"/>).</para>
    /// </summary>
    public class TriggerAnimator : TrackBehaviour
    {
        public int group = 1;
        [Tooltip("Frames of the clip played while it waits (1 = it stands still on the first frame).")]
        public int frames = 1;
        [Tooltip("Seconds the animation takes after the trigger. 0 = as made in Blender (no clip: Default Seconds).")]
        public float seconds;
        public bool loop;
        [Tooltip("No trigger (Group 0, or no trigger tile for it): starts when the ball is this many rows away. -1 = waits for the trigger only.")]
        public float startRows = -1f;
        [Tooltip("Without a clip and with Seconds 0.")]
        public float defaultSeconds = 0.5f;

        [Tooltip("The model's clip, timed by this component instead of by the ball.")]
        public ModelAnimator clip;
        [Tooltip("A custom obstacle's Animation tab (approach, spin, bob), started by the trigger.")]
        public CustomObstacleMotion motion;
        [Tooltip("Moved from Move From to Move To (local) when there is no clip: the built-in bar, or a model without a clip.")]
        public Transform mover;
        public Vector3 moveFrom, moveTo;
        [Tooltip("Camera shake when a one-time animation ends (a slam).")]
        public float shake;

        float born, triggeredAt = float.NaN;
        bool ended;

        public bool Triggered => !float.IsNaN(triggeredAt);
        /// <summary>0 while it waits, then 0..1 through the animation (again and again with Loop).</summary>
        public float Progress { get; private set; }

        protected override void Start()
        {
            base.Start();
            born = Time.time;
            if (clip != null) clip.manual = true;
            if (motion != null) motion.trigger = this;
            Update();
        }

        void Update()
        {
            if (!Triggered && startRows >= 0f && Ahead <= startRows) triggeredAt = Time.time; // no trigger: the ball coming starts it
            else if (!Triggered && GroupTriggers.TryFired(group, out float t))
            {
                triggeredAt = t;
                if (!float.IsNegativeInfinity(t) && Ahead < 30f) GameAudio.Play(Sfx.Riser, 0.6f);
            }
            float length = clip != null ? clip.Length : 0f;
            // The clip's idle part (its first Frames) and the part played after the trigger.
            float idleEnd = frames <= 1 ? 0f : Mathf.Min(frames / 60f, length);
            if (length - idleEnd < 1f / 60f) idleEnd = 0f; // a clip shorter than its idle frames plays whole after the trigger
            float rest = Mathf.Max(0f, length - idleEnd);
            float duration = seconds > 0f ? seconds : length > 0f ? rest : defaultSeconds;
            duration = Mathf.Max(0.02f, duration);

            if (!Triggered)
            {
                Progress = 0f;
                if (clip != null) clip.SetTime(idleEnd > 0f ? Mathf.Repeat(Time.time - born, idleEnd) : 0f);
                if (mover != null) mover.localPosition = moveFrom;
                return;
            }

            float elapsed = float.IsNegativeInfinity(triggeredAt) ? (loop ? Time.time : float.MaxValue) : Time.time - triggeredAt;
            float p = elapsed >= float.MaxValue ? 1f : elapsed / duration;
            Progress = loop ? Mathf.Repeat(p, 1f) : Mathf.Clamp01(p);
            if (clip != null) clip.SetTime(idleEnd + rest * Progress);
            if (mover != null)
            {
                float u = loop ? Mathf.PingPong(p, 1f) : Progress;
                mover.localPosition = Vector3.LerpUnclamped(moveFrom, moveTo, u * u); // speeds up into the slam
            }
            if (!ended && !loop && p >= 1f)
            {
                ended = true;
                if (shake > 0f && !float.IsNegativeInfinity(triggeredAt)) CameraRig.Shake(shake, Mathf.Max(0f, Ahead));
            }
        }
    }
}
