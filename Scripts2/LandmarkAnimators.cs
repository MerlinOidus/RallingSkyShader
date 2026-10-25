using System.Collections.Generic;
using UnityEngine;

namespace RTXOn
{
    /// <summary>Helpers shared by the original finish and portal animations (FloatAnim, FrameAnim of Rolling Sky 1.0).</summary>
    static class OriginalAnim
    {
        /// <summary>FloatAnim.Wave01: 0 → 1 → 0 once per unit.</summary>
        public static float Wave01(float v) => (1f - Mathf.Cos(v * 2f * Mathf.PI)) * 0.5f;

        /// <summary>FloatAnim.Smooth(v, false, true): fast start, eased end.</summary>
        public static float EaseOut(float v) => Mathf.Sin(v * Mathf.PI * 0.5f);

        /// <summary>FloatAnim.Smooth(v, true, false): eased start, fast end.</summary>
        public static float EaseIn(float v) => 1f - Mathf.Cos(v * Mathf.PI * 0.5f);

        /// <summary>FrameAnim.SequenceAuto: the "state*" children of <paramref name="t"/>, ordered by their number.</summary>
        public static Transform[] States(Transform t)
        {
            var list = new List<Transform>();
            for (int i = 0; i < t.childCount; i++)
                if (t.GetChild(i).name.StartsWith("state")) list.Add(t.GetChild(i));
            list.Sort((a, b) => Number(a.name).CompareTo(Number(b.name)));
            return list.ToArray();
        }

        /// <summary>FrameAnim.Sequence.Show: only state <paramref name="index"/> visible.</summary>
        public static void Show(Transform[] states, int index)
        {
            for (int i = 0; i < states.Length; i++)
                if (states[i].gameObject.activeSelf != (i == index)) states[i].gameObject.SetActive(i == index);
        }

        static int Number(string name)
        {
            int n = 0;
            foreach (char c in name)
                if (c >= '0' && c <= '9') n = n * 10 + (c - '0');
            return n;
        }

        public static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }

    /// <summary>
    /// The portal animation of Rolling Sky 1.0 (PortalAnimator) on the original 04_portal. The 289 PortalGeo pieces
    /// form 9 rings of 32 around a centre piece. A model without those pieces spins its static portal instead, as the
    /// original does on low quality.
    /// End portal: standing, always Active; the rings wave and recede like a pulsing tunnel.
    /// Start portal: lying on the floor without its brackets; it switches on (Activating), waves while the level waits
    /// for the first tap, and switches off (Deactivating) once the ball jumps out.
    /// </summary>
    public class PortalRig : MonoBehaviour
    {
        enum State { Waiting, Activating, Active, Deactivating }

        const float WaveAmount = 0.125f; // ActiveAnimAmnt / PassiveAnimAmnt
        const float SwitchTime = 0.75f;  // Activate / Deactivate

        bool startPortal;
        State state;
        float animTime, animTimer;
        Transform[] parts;
        Transform[][] states;
        float[] ringPercent, ringOffset, startHeight;
        Transform staticActive, staticPassive;

        /// <summary>PortalAnimator.ConfigurePortal. False when the model is not an original portal.</summary>
        public bool Setup(bool isStartPortal)
        {
            startPortal = isStartPortal;
            var anim = OriginalAnim.Find(transform, "anim");
            if (anim == null) return false;
            staticActive = OriginalAnim.Find(transform, "portalActiveStatic");
            staticPassive = OriginalAnim.Find(transform, "portalPassiveStatic");
            var animated = OriginalAnim.Find(transform, "portalAnimated");

            // The inner trimming with the dials is hidden on both; the start portal also loses its brackets (Adrian).
            var outer = OriginalAnim.Find(transform, "portalOuter");
            var inner = OriginalAnim.Find(transform, "portalInner");
            if (outer != null) outer.gameObject.SetActive(true);
            if (inner != null) inner.gameObject.SetActive(false);
            foreach (var bracket in new[] { "portalGeo_BracketL", "portalGeo_BracketR" })
            {
                var b = OriginalAnim.Find(transform, bracket);
                if (b != null) b.gameObject.SetActive(!startPortal);
            }
            if (startPortal)
            {
                // Flat on the floor, a bit smaller (original start portal).
                anim.localEulerAngles = Vector3.zero;
                anim.localPosition = new Vector3(0f, -0.49f, 0f);
                anim.localScale = Vector3.one * 0.75f;
            }

            // FindPortalParts: hierarchy order (PortalGeo0, 33, 34...), not the numbers in the names.
            var found = new List<Transform>();
            if (animated != null)
                foreach (var t in animated.GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith("PortalGeo")) found.Add(t);

            int look = startPortal ? 1 : 0; // portal 1 (state2) = switched off, portal 0 (state1) = on
            if (found.Count >= 33)
            {
                if (staticActive != null) staticActive.gameObject.SetActive(false);
                if (staticPassive != null) staticPassive.gameObject.SetActive(false);
                animated.gameObject.SetActive(true);
                parts = found.ToArray();
                int n = parts.Length;
                float rings = n / 32;
                float depthPerRing = 1f / rings;
                states = new Transform[n][];
                ringPercent = new float[n];
                ringOffset = new float[n];
                startHeight = new float[n];
                for (int i = 0; i < n; i++)
                {
                    int ring = i == 0 ? 0 : (i - 1) / 32 + 1;
                    int ringPos = i == 0 ? 0 : (i - 1) % 32;
                    ringPercent[i] = ringPos / 31f;
                    ringOffset[i] = ring / rings;
                    startHeight[i] = ring * depthPerRing - 1f;
                    states[i] = OriginalAnim.States(parts[i]);
                    OriginalAnim.Show(states[i], look);
                    SetY(i, 0f);
                }
            }
            else
            {
                if (animated != null) animated.gameObject.SetActive(false);
                if (staticActive == null || staticPassive == null) return false;
                staticActive.gameObject.SetActive(look == 0);
                staticPassive.gameObject.SetActive(look == 1);
            }

            if (startPortal) Switch(State.Activating, SwitchTime); // TryActivate
            else Switch(State.Active, 1f);
            Animate(0f);
            return true;
        }

        /// <summary>Start portal: the ball has jumped out, switch off (PortalAnimator.Deactivate).</summary>
        public void Deactivate()
        {
            if (!startPortal || state == State.Waiting || state == State.Deactivating) return;
            Switch(State.Deactivating, SwitchTime);
            if (parts == null) ShowStatic(1);
        }

        void Switch(State s, float time)
        {
            state = s;
            animTime = animTimer = time;
        }

        void Update()
        {
            if (state == State.Waiting)
            {
                // The start portal is gone once the ball is far ahead (it is not part of a row).
                var gm = GameManager.Instance;
                if (startPortal && gm != null && gm.ball != null && gm.ball.Position.z - transform.position.z > 20f) Destroy(gameObject);
                return;
            }
            Animate(Mathf.Min(Time.deltaTime, 1f / 30f));
        }

        /// <summary>PortalAnimator.UpdatePortal.</summary>
        void Animate(float dt)
        {
            bool finished = false;
            float carry = 0f;
            float cycle;
            animTimer -= dt;
            if (state == State.Active)
            {
                if (animTimer <= 0f) animTimer += animTime;
                cycle = 1f - animTimer / animTime;
            }
            else if (animTimer <= 0f)
            {
                animTimer = 0f;
                cycle = 1f;
                finished = true;
            }
            else cycle = 1f - animTimer / animTime;

            switch (state)
            {
                case State.Activating:
                    if (parts != null)
                        for (int i = 0; i < parts.Length; i++) ActivatePart(i, cycle, cycle * 0.5f);
                    else staticPassive.localEulerAngles = new Vector3(0f, 360f * cycle * cycle * cycle, 0f);
                    if (finished)
                    {
                        if (parts == null) ShowStatic(0);
                        Switch(State.Active, 1.5f); // SlowActiveAnimTime
                        carry = 0.75f;
                    }
                    break;
                case State.Active:
                    if (parts != null)
                        for (int i = 0; i < parts.Length; i++) UpdatePart(i, cycle, !startPortal, 1f);
                    else staticActive.localEulerAngles = new Vector3(0f, 360f * cycle, 0f);
                    break;
                case State.Deactivating:
                    float off = 1f - cycle;
                    if (parts != null)
                        for (int i = 0; i < parts.Length; i++) DeactivatePart(i, off, off * 0.5f);
                    else staticPassive.localEulerAngles = new Vector3(0f, 360f * off * off * off, 0f);
                    if (finished) Switch(State.Waiting, 1.5f);
                    break;
            }
            if (finished && carry > 0f) animTimer = animTime - carry;
        }

        /// <summary>PortalPart.UpdatePart: full = the end portal's receding tunnel, else a flat wave.</summary>
        void UpdatePart(int i, float cycle, bool full, float weight)
        {
            float y = full
                ? startHeight[i] + OriginalAnim.Wave01((cycle + ringPercent[i] + ringOffset[i]) * 2f) * OriginalAnim.EaseOut(ringOffset[i]) * WaveAmount
                : OriginalAnim.Wave01(cycle + ringOffset[i]) * WaveAmount;
            SetY(i, y * weight);
        }

        /// <summary>PortalPart.Activate: the rings switch on from the centre out.</summary>
        void ActivatePart(int i, float percent, float cycle)
        {
            float on = Mathf.Clamp01((percent - 0.5f) / 0.5f);
            if (on > ringOffset[i])
            {
                UpdatePart(i, cycle, false, on);
                if (states[i].Length > 1 && states[i][states[i].Length - 1].gameObject.activeSelf) OriginalAnim.Show(states[i], 0);
            }
            else SetY(i, OriginalAnim.Wave01(Mathf.Clamp01(percent / 0.5f) * ringOffset[i]) * 0.1f);
        }

        /// <summary>PortalPart.Deactivate: the reverse, from the outside in.</summary>
        void DeactivatePart(int i, float percent, float cycle)
        {
            float on = Mathf.Clamp01((percent - 0.5f) / 0.5f);
            if (on <= ringOffset[i])
            {
                UpdatePart(i, cycle, false, on);
                if (states[i].Length > 0 && states[i][0].gameObject.activeSelf) OriginalAnim.Show(states[i], states[i].Length - 1);
            }
            else SetY(i, OriginalAnim.Wave01(Mathf.Clamp01(percent / 0.5f) * ringOffset[i]) * 0.1f);
        }

        void ShowStatic(int look)
        {
            staticActive.gameObject.SetActive(look == 0);
            staticPassive.gameObject.SetActive(look == 1);
            staticActive.localEulerAngles = staticPassive.localEulerAngles = Vector3.zero;
        }

        void SetY(int i, float y)
        {
            var p = parts[i].localPosition;
            p.y = y;
            parts[i].localPosition = p;
        }
    }
    /// <summary>
    /// The finish wall of Rolling Sky 1.0 (FinishManager.GoalWall) on the original 03_finish: its 42 blocks wait dark,
    /// light up when the ball reaches the wall, then fly forward and spread away from the ball, rising one after another
    /// (the farther from the ball, the later), and are gone after 4 seconds. Only the look; the win is GameManager's.
    /// </summary>
    public class FinishWall : MonoBehaviour
    {
        const float ShatterTime = 4f;   // shatterTimeFinishMax
        const float Force = 5f;         // shatterForce
        const float RiseDelayMax = 1.5f;
        const float RiseSmoothTime = 0.5f;
        const float RisePerFrame = 0.1f; // the original rises 0.1 per frame at 60 fps

        Transform anim;
        Transform[] blocks;
        Transform[][] states;
        Vector3[] startLocal;
        Vector3[] move;
        float[] riseDelay;
        bool shattering, done;
        float timer;

        /// <summary>GoalWall.ShowAt. False when the model has no "anim" with "block" children.</summary>
        public bool Setup()
        {
            anim = null;
            for (int i = 0; i < transform.childCount && anim == null; i++)
                if (transform.GetChild(i).name == "anim") anim = transform.GetChild(i);
            if (anim == null) anim = OriginalAnim.Find(transform, "anim");
            if (anim == null) return false;

            var list = new List<Transform>();
            for (int i = 0; i < anim.childCount; i++)
                if (anim.GetChild(i).name.StartsWith("block")) list.Add(anim.GetChild(i));
            if (list.Count == 0) return false;

            blocks = list.ToArray();
            int n = blocks.Length;
            states = new Transform[n][];
            startLocal = new Vector3[n];
            move = new Vector3[n];
            riseDelay = new float[n];
            for (int i = 0; i < n; i++)
            {
                states[i] = OriginalAnim.States(blocks[i]);
                startLocal[i] = blocks[i].localPosition;
            }
            ResetWall();
            return true;
        }

        void ResetWall()
        {
            shattering = done = false;
            anim.gameObject.SetActive(true);
            for (int i = 0; i < blocks.Length; i++)
            {
                blocks[i].localPosition = startLocal[i];
                OriginalAnim.Show(states[i], 1); // WallTile: waiting look
            }
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.ball == null) return;
            var ball = gm.ball.Position;
            float wallZ = transform.position.z;

            if (!shattering && !done)
            {
                if (ball.z >= wallZ && gm.ball.Alive) Shatter(ball.x);
                return;
            }
            if (ball.z < wallZ - 1f) // continued from a checkpoint before the wall: build it again
            {
                ResetWall();
                return;
            }
            if (done) return;

            float dt = Time.deltaTime;
            timer += dt;
            for (int i = 0; i < blocks.Length; i++)
            {
                // WallTile.UpdateShatter (full animation)
                var p = blocks[i].position + move[i] * Force * dt;
                if (timer > riseDelay[i])
                {
                    float t = (timer - riseDelay[i]) / RiseSmoothTime;
                    float rise = t >= 1f ? 1f : OriginalAnim.EaseIn(t);
                    p.y += RisePerFrame * rise * dt * 60f;
                }
                blocks[i].position = p;
            }
            if (timer >= ShatterTime)
            {
                done = true;
                anim.gameObject.SetActive(false);
            }
        }

        /// <summary>WallTile.SetHitPoint for every block, then Highlight.</summary>
        void Shatter(float ballX)
        {
            shattering = true;
            timer = 0f;
            for (int i = 0; i < blocks.Length; i++)
            {
                // Distance across the 5-wide wall, as a percent: blocks near the ball go first.
                float proximity = (blocks[i].position.x - ballX) / 5f;
                bool onTopRow = i % 2 == 0; // MathUtils.ToGrid(number + 1, 2): odd row
                float delay = OriginalAnim.EaseIn(Mathf.Min(Mathf.Abs(proximity), 1f));
                if (onTopRow) delay -= 0.1f;
                riseDelay[i] = delay * RiseDelayMax;
                move[i] = new Vector3(proximity * 0.1f, 0f, 1f); // original (x, forward, up) → ours (x, up, forward)
                OriginalAnim.Show(states[i], 0);
            }
        }
    }
}
