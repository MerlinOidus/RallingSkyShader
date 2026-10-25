using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// The original crusher (Rolling Sky 1.0 TileAnimator.RiseCrusher on 02_enemyCrusher): on a 24-row cycle locked
    /// to the ball's distance, it lifts up to 2 tiles high while its rotor spins fast and its blades fold out, then
    /// slams back onto the tile (sound + camera shake). Adrian 2026-10-24: only ONCE, the cycle the ball meets (it
    /// starts about 22 rows ahead); before and after that it rests on its tile with the rotor idling.
    /// Only the look: the danger is the whole tile, the original way (deadly unless jumping; safe crushers never).
    /// Works on the original mesh ("anim", "altAnim", "part1-4", "state1/2") or on any block given as <see cref="anim"/>.
    /// </summary>
    public class CrusherRig : TrackBehaviour
    {
        public bool safe;
        public Transform anim;
        Transform altAnim;
        Transform[] parts = new Transform[0];
        bool? wasDown;

        public void Setup(bool isSafe, Transform block = null)
        {
            safe = isSafe;
            anim = block != null ? block : EnemyRig.Find(transform, "anim");
            if (block == null)
            {
                altAnim = EnemyRig.Find(transform, "altAnim");
                var list = new System.Collections.Generic.List<Transform>();
                foreach (var t in GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith("part")) list.Add(t);
                parts = list.ToArray();
                // Tile.CreateAnimator: a normal crusher switches to its second look (state2), a safe one keeps state1.
                var state1 = EnemyRig.Find(transform, "state1");
                var state2 = EnemyRig.Find(transform, "state2");
                if (state1 != null) state1.gameObject.SetActive(safe);
                if (state2 != null) state2.gameObject.SetActive(!safe);
                // The flat shadow card would take the enemy material; the scene's real shadows do its job.
                foreach (var t in GetComponentsInChildren<Transform>(true))
                    if (t.name == "shadow" || t.name == "shadowAlt") t.gameObject.SetActive(false);
            }
            Update();
        }

        void Update()
        {
            if (anim == null) return;
            float d = Ahead;
            float cycle = OnceCycle(d, 12f, safe ? -0.6f : -0.1725f);
            float n = EnemyRig.PulseWeighted(cycle, 0.85f, 0.5f) * 2f - 1f; // -1..1
            bool down = n < 0f;
            float up = Mathf.Clamp01(n);
            if (wasDown.HasValue && wasDown.Value != down && up <= 0f)
            {
                // Just slammed onto the tile.
                if (Mathf.Abs(d) < 15f) GameAudio.Play(Sfx.Crusher, Mathf.Clamp01(1f - Mathf.Abs(d) / 15f));
                CameraRig.Shake(0.5f, d);
            }
            wasDown = down;

            EnemyRig.SetY(anim, up * 2f);
            // Rotor: fast while it rises and falls, a slow idle turn otherwise (1000 degrees a second at most).
            float spin = n >= -0.25f && n <= 1f ? Mathf.Sin(Mathf.Clamp01((n + 0.25f) / 1.25f) * Mathf.PI) : 0.05f;
            if (altAnim != null) altAnim.Rotate(Vector3.up * spin * 1000f * Time.deltaTime);
            // Blades slide out as it lifts.
            foreach (var p in parts)
            {
                var lp = p.localPosition;
                lp.z = 0.2f * up;
                p.localPosition = lp;
            }
        }

        /// <summary>
        /// The one cycle of <see cref="DistanceCycle"/> that ends just after the ball passes; 0 (resting) outside it.
        /// </summary>
        static float OnceCycle(float distance, float period, float offset)
        {
            float first = offset * period; // the cycle ends here (the slam), and began 2 periods further ahead
            if (distance < first || distance > first + 2f * period) return 0f;
            return DistanceCycle(distance, period, offset);
        }

        /// <summary>TileAnimator.GetDistanceCycle: 0..1, one cycle every 2 periods.</summary>
        static float DistanceCycle(float distance, float period, float offset)
        {
            float c = (distance - offset * period) / period / 2f;
            return c < 0f ? -(c % 1f) : 1f - c % 1f;
        }
    }
}
