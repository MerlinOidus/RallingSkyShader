using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace RTXOn
{
    /// <summary>
    /// Plays an animation clip made in Blender on a theme model (hazard slot or prop), without an Animator Controller.
    /// The time is set every frame (not left to run on its own), so Approach clips line up with the ball exactly
    /// like the built-in obstacles, and a paused game pauses the clip too.
    /// Works with Generic/Humanoid clips (Rig tab: Generic) and with Legacy clips.
    /// </summary>
    public class ModelAnimator : TrackBehaviour
    {
        public ModelAnimation settings;
        [Tooltip("Custom Obstacle window preview: use Preview Ahead instead of the ball.")]
        public bool preview;
        public float previewAhead = 999f;
        [Tooltip("Timed by another component (TriggerAnimator through SetTime) instead of by the ball or the clock.")]
        public bool manual;
        [Tooltip("Loop clips: play all the time (Roller, Spotlight, Binaries). Off = stand on the first frame until the " +
                 "ball is Wake Rows (25) away, then loop from there.")]
        public bool alwaysLoop;
        float manualTime;
        float wokeAt = float.NaN;

        /// <summary>The clip's length in seconds (0 without a clip).</summary>
        public float Length => settings != null && settings.clip != null ? settings.clip.length : 0f;

        /// <summary>Shows the clip at <paramref name="time"/> seconds (manual mode).</summary>
        public void SetTime(float time)
        {
            manual = true;
            manualTime = time;
        }

        PlayableGraph graph;
        AnimationClipPlayable playable;
        Animation legacy;
        AnimationState legacyState;

        protected override void Start()
        {
            base.Start();
            var clip = settings != null ? settings.clip : null;
            if (clip == null) { enabled = false; return; }

            if (clip.legacy)
            {
                legacy = GetComponent<Animation>();
                if (legacy == null) legacy = gameObject.AddComponent<Animation>();
                legacy.playAutomatically = false;
                legacy.AddClip(clip, clip.name);
                legacyState = legacy[clip.name];
                legacyState.enabled = true;
                legacyState.weight = 1f;
                legacyState.speed = 0f;
            }
            else
            {
                var animator = GetComponent<Animator>();
                if (animator == null) animator = gameObject.AddComponent<Animator>();
                animator.applyRootMotion = false;
                playable = AnimationPlayableUtilities.PlayClip(animator, clip, out graph);
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            }
            Update();
        }

        void Update()
        {
            var clip = settings.clip;
            float length = Mathf.Max(clip.length, 0.0001f);
            float t;
            if (manual)
                t = Mathf.Clamp(manualTime, 0f, length);
            else if (settings.mode == ModelAnimMode.Loop)
            {
                // Level Editor views and previews (no game running) keep looping, to show the clip.
                // An always-looping model in an Event Group that waits for its trigger sleeps too (Ahead reads far away).
                if (preview || GameManager.Instance == null || alwaysLoop && Ahead < 900f)
                    t = Mathf.Repeat(Time.time * settings.speed, length);
                else
                {
                    if (float.IsNaN(wokeAt) && Ahead <= WakeRows) wokeAt = Time.time;
                    t = float.IsNaN(wokeAt) ? 0f : Mathf.Repeat((Time.time - wokeAt) * settings.speed, length);
                }
            }
            else
                t = Mathf.Clamp01((settings.startRows - (preview ? previewAhead : Ahead)) / Mathf.Max(settings.playRows, 0.01f)) * length;

            if (legacy != null)
            {
                legacyState.time = t;
                legacy.Sample();
            }
            else if (graph.IsValid())
            {
                playable.SetTime(t);
                graph.Evaluate();
            }
        }

        void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
