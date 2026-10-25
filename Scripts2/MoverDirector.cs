using System.Collections.Generic;
using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// Runs the moving platforms of a level (see <see cref="MoverGrid"/> for the rules).
    /// Fires a touch mover when the ball rolls onto its arrow, then slides every platform of the chain
    /// one cell over one row of ball travel, with the original bounce at the end.
    /// TrackBuilder adds this when the level has mover tiles.
    /// </summary>
    [DefaultExecutionOrder(50)] // after the ball has moved this frame
    public class MoverDirector : MonoBehaviour
    {
        class Slide
        {
            public MoverGrid.Move move;
            public Vector3 dir;
            public bool started;
            public readonly List<Transform> parts = new List<Transform>();
        }

        TrackBuilder track;
        MoverGrid grid;
        readonly List<Slide> slides = new List<Slide>();
        // Empty cells a platform will move into. Each gets its floor when the first slide into it starts.
        readonly HashSet<Vector2Int> pendingFloors = new HashSet<Vector2Int>();
        float lastZ;
        float lastSpeed;

        public void Init(TrackBuilder owner, MoverGrid moverGrid)
        {
            track = owner;
            grid = moverGrid;
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (grid == null || gm == null || gm.ball == null) return;
            var ball = gm.ball;

            // After a crash the slides that already started still finish, as if the ball kept rolling.
            float z;
            if (ball.Alive)
            {
                z = ball.Position.z;
                lastSpeed = ball.Speed;
            }
            else z = lastZ + lastSpeed * Time.deltaTime;
            lastZ = z;

            if (ball.Alive && ball.Grounded && gm.State == GameState.Playing)
            {
                var p = ball.Position;
                int row = Mathf.FloorToInt(p.z + 0.5f);
                int lane = Mathf.FloorToInt(p.x + 2.5f);
                if (grid.IsReadyTouch(row, lane))
                    foreach (var move in grid.Trigger(row, lane, z, track.LastBuiltRow))
                        Begin(move);
            }

            for (int i = slides.Count - 1; i >= 0; i--)
                if (Animate(slides[i], z)) slides.RemoveAt(i);
        }

        void Begin(MoverGrid.Move move)
        {
            var slide = new Slide { move = move, dir = new Vector3(move.dir.x, 0f, move.dir.y) };
            foreach (var cell in move.section)
            {
                var holder = track.HolderAt(cell.y, cell.x);
                if (holder == null) continue; // row already removed behind the ball
                slide.parts.Add(WrapChildren(holder));
            }
            // A chained platform starts later than the touch that set it off; its floors and arrow wait for StartSlide.
            foreach (var cell in move.newSolids) pendingFloors.Add(cell);
            slides.Add(slide);
        }

        void StartSlide(Slide slide)
        {
            slide.started = true;
            GameAudio.Play(Sfx.Slide, 0.6f);
            var move = slide.move;
            var trigger = track.HolderAt(move.trigger.y, move.trigger.x);
            if (trigger != null) track.ActivateArrow(trigger);

            // The cells it moves into can be stood on from now on. Two platforms can share a destination;
            // whichever starts sliding first adds the floor.
            foreach (var cell in move.destinations)
                if (pendingFloors.Remove(cell)) track.AddMovedFloor(cell.y, cell.x, cell.y - move.dir.y);
        }

        /// <summary>
        /// Puts the tile's visuals and items under a child that can slide, so the floor collider
        /// itself stays in its cell until the slide is over (like the original).
        /// </summary>
        static Transform WrapChildren(Transform holder)
        {
            var children = new List<Transform>();
            foreach (Transform child in holder) children.Add(child);
            var moving = new GameObject("Moving").transform;
            moving.SetParent(holder, false);
            foreach (var child in children) child.SetParent(moving, false);
            return moving;
        }

        /// <returns>True when the slide is over.</returns>
        bool Animate(Slide slide, float z)
        {
            float v = z - slide.move.offset;
            if (v < 0f) return false; // chained auto movers wait their turn
            if (!slide.started) StartSlide(slide);

            var pos = slide.dir * Impact(Mathf.Clamp01(v));
            foreach (var part in slide.parts)
                if (part != null) part.localPosition = pos;
            if (v <= 1f) return false;

            foreach (var cell in grid.Finish(slide.move))
                track.RemoveFloorCollider(cell.y, cell.x);
            return true;
        }

        /// <summary>
        /// FloatAnim.Impact from the original: speeds up to the target by halfway,
        /// bounces back a quarter of a cell, then settles.
        /// </summary>
        public static float Impact(float p)
        {
            if (p <= 0.5f) return 1f - Mathf.Cos(Mathf.PI * p);
            return 1f - Mathf.Sin(2f * Mathf.PI * (p - 0.5f)) * 0.25f;
        }
    }
}
