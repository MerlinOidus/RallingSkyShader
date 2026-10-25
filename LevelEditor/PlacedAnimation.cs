using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// The level editor's 3D view: a cell that was just painted plays its animation once, on a clock instead of the
    /// ball (an animated tile shows how it moves in the game, any other tile pops in), then rests where it belongs.
    /// Only new paint gets it, not scrolling, loading or Undo (LevelEditor.WasJustPlaced).
    /// </summary>
    public class PlacedAnimation : MonoBehaviour
    {
        const float PopSeconds = 0.35f;
        const float MoveSeconds = 0.9f;

        PartKind kind;
        float height, offsetX, seconds, time;
        bool spinLeft;
        Vector3 move;       // Custom Move: where it goes; Trigger Object: how its bar moves
        float spin;
        Renderer[] blinkers; // Blink In
        Transform target;
        Vector3 home, homeScale;
        Quaternion homeRotation;

        /// <summary>Plays the animation of <paramref name="kind"/> (None = a pop) once on <paramref name="cell"/>.</summary>
        public static void Play(GameObject cell, PartKind kind, PartArgs args, int row, int lane)
        {
            if (cell == null) return;
            var a = cell.AddComponent<PlacedAnimation>();
            a.kind = PartArgs.IsAnimatedFloor(kind) || IsPickupMove(kind) || PartArgs.IsCustomMove(kind) || kind == PartKind.TriggerObject ? kind : PartKind.None;
            a.height = args != null ? Mathf.Max(0.5f, args.height) : 1f;
            a.spinLeft = ((row * 31 + lane * 17) & 1) == 0; // same side as the game's FloorAnimation
            if (kind == PartKind.Shuffle)
            {
                // The same other lane the game slides it in from (TrackBuilder.BuildPart).
                int other = (int)((uint)(row * 73856093 ^ lane * 19349663) % (LevelData.Width - 1));
                if (other >= lane) other++;
                a.offsetX = other - lane;
            }
            a.seconds = a.kind == PartKind.None ? PopSeconds : MoveSeconds;
            a.target = cell.transform;
            if (args != null && PartArgs.IsCustomMove(kind))
            {
                // There, a short wait, and back: at most 30 tiles of it, so it stays on screen.
                a.move = Vector3.ClampMagnitude(args.Move, 30f);
                a.spin = args.spin;
                a.seconds = Mathf.Clamp((args.withBall ? args.Move.magnitude / 8f : args.seconds) / 0.55f, 0.8f, 4f);
                if (kind == PartKind.CustomMoveBlock)
                {
                    var obj = cell.transform.Find("Object");
                    if (obj != null) a.target = obj;
                }
            }
            if (kind == PartKind.TriggerObject)
            {
                // The bar slams down and goes back up (a model is not previewed).
                var bar = cell.transform.Find("Trigger Object/Bar");
                if (bar == null || args == null) { Destroy(a); return; }
                a.target = bar;
                float up = Mathf.Max(0.9f, args.height);
                a.move = new Vector3(0f, 0.3f - up, 0f);
                a.seconds = Mathf.Clamp((args.seconds > 0f ? args.seconds : 0.5f) / 0.55f, 0.6f, 3f);
            }
            if (kind == PartKind.BlinkIn) a.blinkers = cell.GetComponentsInChildren<Renderer>();
            if (IsPickupMove(kind))
            {
                // Only the gem or crown moves: it is the cell's last child (built after the floor).
                var t = cell.transform;
                a.target = t.childCount > 0 ? t.GetChild(t.childCount - 1) : t;
            }
            a.home = a.target.localPosition;
            a.homeScale = a.target.localScale;
            a.homeRotation = a.target.localRotation;
            a.Apply(0f);
        }

        static bool IsPickupMove(PartKind k) =>
            k == PartKind.GemRise || k == PartKind.GemDrop || k == PartKind.CrownRise || k == PartKind.CrownDrop;

        void Update()
        {
            time += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(time / seconds);
            Apply(u);
            if (u >= 1f) Destroy(this);
        }

        void OnDestroy()
        {
            if (blinkers != null) foreach (var r in blinkers) if (r != null) r.enabled = true;
            if (target == null) return;
            target.localPosition = home;
            target.localScale = homeScale;
            target.localRotation = homeRotation;
        }

        static float EaseOut(float u) => 1f - (1f - u) * (1f - u) * (1f - u);

        /// <summary>Goes, waits, comes back: for motions that end away from the cell (sink, fall, rise of a gem).</summary>
        static float Away(float u)
        {
            if (u < 0.55f) return EaseOut(u / 0.55f);
            if (u < 0.75f) return 1f;
            float b = (u - 0.75f) / 0.25f;
            return 1f - b * b * (3f - 2f * b);
        }

        void Apply(float u)
        {
            if (target == null) return;
            float e = EaseOut(u);
            var pos = home;
            var scale = homeScale;
            var rot = homeRotation;
            switch (kind)
            {
                case PartKind.SlideFromLeft: pos.x -= 4f * (1f - e); break;
                case PartKind.SlideFromRight: pos.x += 4f * (1f - e); break;
                case PartKind.Shuffle: pos.x += offsetX * (1f - e); break;
                case PartKind.RiseUp: pos.y -= height * (1f - e); break;
                case PartKind.SinkDown: pos.y -= height * Away(u); break;
                case PartKind.DropIn: pos.y += height * (1f - u * u); break; // falls faster and faster
                case PartKind.SpinIn:
                    rot = Quaternion.Euler(0f, (spinLeft ? -360f : 360f) * (1f - e), 0f) * homeRotation;
                    scale = homeScale * Mathf.Max(0.01f, e);
                    break;
                case PartKind.Extend:
                    // Grows forward from its back edge.
                    scale.z = homeScale.z * Mathf.Max(0.01f, e);
                    pos.z -= 0.5f * (1f - e);
                    break;
                case PartKind.FallAway:
                {
                    float a = Away(u);
                    pos.y -= Mathf.Min(height, 4f) * a * a;
                    rot = Quaternion.Euler((spinLeft ? -50f : 50f) * a, 0f, 0f) * homeRotation;
                    break;
                }
                case PartKind.Wave: pos.y += Mathf.Min(height, 1f) * Mathf.Sin(u * Mathf.PI * 4f) * (1f - u); break;
                case PartKind.GemRise:
                case PartKind.CrownRise: pos.y += height * Away(u); break;
                case PartKind.GemDrop:
                case PartKind.CrownDrop: pos.y += height * (1f - e); break;
                case PartKind.FlipIn:
                case PartKind.PopIn:
                case PartKind.Drawbridge:
                case PartKind.ScrewUp:
                case PartKind.RollIn:
                case PartKind.BlinkIn:
                {
                    var mode = FloorAnimation.Mode.FlipIn + (kind - PartKind.FlipIn);
                    FloorAnimation.Pose(mode, kind == PartKind.PopIn ? u : e, Mathf.Min(height, 4f), spinLeft, home, homeRotation, homeScale, out pos, out rot, out scale);
                    if (blinkers != null)
                    {
                        bool on = u >= 1f || Mathf.Repeat(u * (3f + 10f * u), 1f) < 0.35f + 0.6f * u;
                        foreach (var r in blinkers) if (r != null) r.enabled = on;
                    }
                    break;
                }
                case PartKind.FlyAway:
                {
                    float a = Away(u);
                    pos.y += Mathf.Min(height, 4f) * a * a;
                    rot = Quaternion.Euler(0f, (spinLeft ? -540f : 540f) * a, 40f * a) * homeRotation;
                    scale = homeScale * Mathf.Max(0.01f, 1f - a);
                    break;
                }
                case PartKind.CustomMoveTile:
                case PartKind.CustomMoveBlock:
                case PartKind.TriggerObject:
                {
                    float a = Away(u);
                    pos += move * a;
                    rot = homeRotation * Quaternion.Euler(0f, spin * a, 0f);
                    break;
                }
                default:
                {
                    // Pop: drops in a little and grows past its size, then settles.
                    float s = 1f + 0.18f * Mathf.Sin(u * Mathf.PI) - 0.4f * (1f - e);
                    scale = homeScale * Mathf.Max(0.01f, s);
                    pos.y += 0.35f * (1f - e);
                    break;
                }
            }
            target.localPosition = pos;
            target.localScale = scale;
            target.localRotation = rot;
        }
    }
}
