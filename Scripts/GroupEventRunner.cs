using System.Collections.Generic;
using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// Event Groups on one layer of a cell (a tile with what stands on it, an object, or a collect): when one of its
    /// groups fires, that group's events play on it (Move, Move From, Rotate, Scale, Hide, Show, Shake), added
    /// together when it is in several groups. Wait for trigger (<see cref="sleep"/>): its own animation (crusher, riser,
    /// Blender clip...) stands still until one of its groups fires, then wakes up (<see cref="SleepAhead"/>).
    /// A holder with no groups only keeps the tile's sleep away from the object or collect on it.
    /// </summary>
    public class GroupEventRunner : MonoBehaviour
    {
        public TileLayer layer;
        public bool sleep = true;
        [Tooltip("The groups it belongs to.")]
        public List<int> groups = new List<int>();

        /// <summary>Rows the ball seems to be away while it sleeps (as if the ball were far: the idle pose).</summary>
        const float SleepFar = 999f;
        /// <summary>When it wakes up, its animation runs from this many rows away to where the ball really is, in WakeSeconds.</summary>
        const float WakeFrom = TrackBehaviour.WakeRows + 5f;
        const float WakeSeconds = 0.35f;

        readonly List<EventGroup> defs = new List<EventGroup>();
        Vector3 home, homeScale;
        Quaternion homeRotation;
        bool visible = true, hasEvents;
        float seed;
        readonly List<Renderer> hiddenRenderers = new List<Renderer>();
        readonly List<Collider> hiddenColliders = new List<Collider>();

        /// <summary>Its groups and their events from the level (TrackBuilder).</summary>
        public void Setup(LevelData level, List<int> groupIds, bool waitForTrigger, TileLayer cellLayer)
        {
            layer = cellLayer;
            groups.Clear();
            if (groupIds != null) groups.AddRange(groupIds);
            sleep = waitForTrigger && groups.Count > 0;
            defs.Clear();
            hasEvents = false;
            if (level != null)
                foreach (int g in groups)
                {
                    var def = level.FindGroup(g);
                    if (def == null || def.events == null || def.events.Count == 0) continue;
                    defs.Add(def);
                    hasEvents = true;
                }
            enabled = hasEvents;
        }

        void Start()
        {
            home = transform.localPosition;
            homeRotation = transform.localRotation;
            homeScale = transform.localScale;
            seed = (transform.position.x * 7.3f + transform.position.z * 3.1f) % 10f;
            if (hasEvents) Update();
        }

        /// <summary>
        /// What a <see cref="TrackBehaviour"/> under it sees as its distance to the ball: far away while it sleeps, then,
        /// when one of its groups fires, quickly the real distance (so it wakes up and plays from its idle pose).
        /// </summary>
        public float SleepAhead(float real)
        {
            if (!sleep || groups.Count == 0) return real;
            if (!GroupTriggers.TryFiredAny(groups, out float firedAt)) return SleepFar;
            if (float.IsNegativeInfinity(firedAt)) return real;
            float k = Mathf.Clamp01((Time.time - firedAt) / WakeSeconds);
            if (k >= 1f) return real;
            float from = Mathf.Max(real, WakeFrom);
            return Mathf.Lerp(from, real, k * k * (3f - 2f * k));
        }

        void Update()
        {
            if (GameManager.Instance == null) return; // the editor's 3D view shows it at home
            var pos = home;
            var euler = Vector3.zero;
            var scale = Vector3.one;
            bool show = true;
            float now = Time.time;
            foreach (var def in defs)
            {
                bool fired = GroupTriggers.TryFired(def.id, out float firedAt);
                foreach (var e in def.events)
                {
                    if (e == null) continue;
                    float length = Mathf.Max(0.02f, e.seconds);
                    bool started;
                    float u;
                    if (!fired) { started = false; u = 0f; }
                    else if (float.IsNegativeInfinity(firedAt)) { started = true; u = e.loop ? now / length : 1f; } // fired before this run: its end
                    else
                    {
                        float t = now - firedAt - e.delay;
                        started = t >= 0f;
                        u = e.seconds > 0f ? t / length : started ? 1f : 0f;
                    }
                    float k = !started ? 0f : e.loop ? e.Ease(Mathf.PingPong(u, 1f)) : e.Ease(u);
                    var amount = e.Amount;
                    switch (e.Type)
                    {
                        case GroupEventType.Move: pos += amount * k; break;
                        case GroupEventType.MoveFrom: pos += amount * (1f - k); break;
                        case GroupEventType.Rotate: euler += amount * k; break;
                        case GroupEventType.Scale:
                            var target = new Vector3(amount.x == 0f ? 1f : amount.x, amount.y == 0f ? 1f : amount.y, amount.z == 0f ? 1f : amount.z);
                            scale = Vector3.Scale(scale, Vector3.LerpUnclamped(Vector3.one, target, k));
                            break;
                        case GroupEventType.Hide:
                            if (started) show &= e.loop && e.seconds > 0f && Mathf.Repeat(u, 1f) < 0.5f; // Loop: blinks
                            break;
                        case GroupEventType.Show:
                            if (!started) show = false;
                            else if (e.loop && e.seconds > 0f) show &= Mathf.Repeat(u, 1f) < 0.5f;
                            break;
                        case GroupEventType.Shake:
                            if (started && (e.loop || u < 1f))
                            {
                                if (amount == Vector3.zero) amount = new Vector3(0.08f, 0f, 0f);
                                float f = now * 40f + seed;
                                float fade = e.loop ? 1f : 1f - Mathf.Clamp01(u);
                                pos += new Vector3(Mathf.Sin(f) * amount.x, Mathf.Sin(f * 1.31f) * amount.y, Mathf.Sin(f * 0.73f) * amount.z) * fade;
                            }
                            break;
                    }
                }
            }
            transform.localPosition = pos;
            transform.localRotation = homeRotation * Quaternion.Euler(euler);
            transform.localScale = Vector3.Scale(homeScale, scale);
            if (show != visible) SetVisible(show);
        }

        /// <summary>Hidden: its renderers and colliders off (only those that were on, so a taken gem stays taken).</summary>
        void SetVisible(bool on)
        {
            visible = on;
            if (!on)
            {
                hiddenRenderers.Clear();
                hiddenColliders.Clear();
                foreach (var r in GetComponentsInChildren<Renderer>())
                    if (r.enabled) { r.enabled = false; hiddenRenderers.Add(r); }
                foreach (var c in GetComponentsInChildren<Collider>())
                    if (c.enabled) { c.enabled = false; hiddenColliders.Add(c); }
                return;
            }
            foreach (var r in hiddenRenderers) if (r != null) r.enabled = true;
            foreach (var c in hiddenColliders) if (c != null) c.enabled = true;
            hiddenRenderers.Clear();
            hiddenColliders.Clear();
        }
    }

    /// <summary>
    /// Event Groups "Fires": when the ball touches this tile, object or collect (rolls onto it, or, Whole Row, crosses
    /// its row anywhere), its groups fire (<see cref="GroupTriggers.Fire"/>), like a CustomTriggerTiles.
    /// </summary>
    public class GroupFire : MonoBehaviour
    {
        public List<int> groups = new List<int>();
        public bool wholeRow;
        public TileLayer layer;

        float lastBallZ = float.NaN;
        bool done;

        void Update()
        {
            if (done) return;
            var gm = GameManager.Instance;
            if (gm == null || gm.ball == null || !gm.ball.Alive || gm.State != GameState.Playing)
            {
                lastBallZ = float.NaN;
                return;
            }
            var ball = gm.ball.Position;
            var at = transform.position;
            float line = at.z - 0.35f; // the near edge of the cell, a little in
            bool crossed = !float.IsNaN(lastBallZ) && lastBallZ < line && ball.z >= line;
            lastBallZ = ball.z;
            if (!crossed) return;
            // Touching: in its lane and not jumping over it (a tile is touched by rolling on it).
            float reach = layer == TileLayer.Tile ? 1f : 1.8f;
            if (!wholeRow && (Mathf.Abs(ball.x - at.x) > 0.6f || ball.y - at.y > reach)) return;
            done = true;
            foreach (int g in groups) GroupTriggers.Fire(g);
            if (layer != TileLayer.Collect) GameAudio.Play(Sfx.Switch, 0.5f);
        }
    }
}
