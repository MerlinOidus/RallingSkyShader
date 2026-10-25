using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// Floater like Rolling Sky 1.0 (TileAnimator.FloatFloater): from 10 to 3 rows ahead it rises out of the floor to
    /// its height while turning over half a turn; from 2 to 1 rows its four spikes ("part1".."part4", folded at 70.45°)
    /// snap open with a bounce and the body wobbles; then it switches to "state2". The spikes and turn need the
    /// original 07_enemyFloater mesh; any other model only rises. Amplitude above 0 makes it bob once it is up.
    /// </summary>
    public class Floater : TrackBehaviour
    {
        public float baseHeight = 0.9f;
        [Tooltip("Rises out of the floor like the original. Off = waits at its height from the start.")]
        public bool rise = true;
        public float amplitude = 0f;
        public float rowsPerCycle = 4f;

        const float RiseFrom = 10f, RiseTo = 3f, OpenFrom = 2f, OpenTo = 1f;
        const float SpikesFolded = 70.45f, Wobble = 7.5f;

        float lowHeight;
        Transform anim;
        Quaternion animRest;
        Transform[] parts = new Transform[0];
        Transform state1, state2;
        bool open;

        protected override void Start()
        {
            base.Start();
            lowHeight = transform.localPosition.y; // built sitting on the floor
            anim = EnemyRig.Find(transform, "anim");
            if (anim != null)
            {
                animRest = anim.localRotation;
                var list = new System.Collections.Generic.List<Transform>();
                foreach (var t in anim.GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith("part")) list.Add(t);
                parts = list.ToArray();
                state1 = EnemyRig.Find(transform, "state1");
                state2 = EnemyRig.Find(transform, "state2");
                if (state1 != null && state2 != null)
                {
                    state1.gameObject.SetActive(true);
                    state2.gameObject.SetActive(false);
                }
                foreach (var t in GetComponentsInChildren<Transform>(true))
                    if (t.name == "shadow" || t.name == "shadowAlt") t.gameObject.SetActive(false);
            }
            Update();
        }

        void Update()
        {
            float d = Ahead;
            float s = 1f;
            if (rise)
            {
                float v = EnemyRig.P01(d, RiseFrom, RiseTo, out bool below, out _);
                s = below ? 0f : Smooth2(v);
            }
            var p = transform.localPosition;
            p.y = Mathf.LerpUnclamped(lowHeight, baseHeight, s);
            if (amplitude != 0f && s >= 1f) p.y += Mathf.Sin(d / rowsPerCycle * Mathf.PI * 2f) * amplitude;
            transform.localPosition = p;

            if (anim == null) return;
            // The rest pose is the risen one (X 90 in the original); rising turns it over from upside down.
            float turn = rise ? -180f * (1f - s) : 0f;
            float spikes = SpikesFolded, wobble = 0f;
            float o = EnemyRig.P01(d, OpenFrom, OpenTo, out bool waiting, out bool opened);
            if (opened) spikes = 0f;
            else if (!waiting)
            {
                spikes = SpikesFolded * EnemyRig.Impact(1f - o, 0.25f, 0.75f); // FloatAnim.ImpactLate
                wobble = Wobble * Mathf.Sin(o * Mathf.PI);                       // FloatAnim.Pulse01
            }
            anim.localRotation = Quaternion.Euler(turn + wobble, 0f, 0f) * animRest;
            foreach (var part in parts) part.localEulerAngles = new Vector3(spikes, 0f, 0f);
            if (opened != open && state1 != null && state2 != null)
            {
                open = opened;
                state1.gameObject.SetActive(!open);
                state2.gameObject.SetActive(open);
            }
        }

        /// <summary>FloatAnim.Smooth(v, true, true, 2): power ease in and out, then a sine ease.</summary>
        static float Smooth2(float v)
        {
            v = v < 0.5f ? v * v * 2f : 1f - (1f - v) * (1f - v) * 2f;
            return (Mathf.Sin((v - 0.5f) * Mathf.PI) + 1f) * 0.5f;
        }
    }
}
