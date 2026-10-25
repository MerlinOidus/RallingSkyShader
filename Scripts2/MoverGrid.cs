using System.Collections.Generic;
using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// Rules of the moving platforms (tile IDs 37-48 and 57-60), following Rolling Sky 1.0:
    /// <list type="bullet">
    /// <item>An arrow tile and every floor tile connected to it (up, down, left, right) form one platform.
    /// Holes, jump pads, fragile and sliding floors, other segments and tile edges stop the platform.</item>
    /// <item>The whole platform shifts one cell in the arrow's direction. The cells it leaves become holes
    /// once the slide is over; the cells it moves into are solid as soon as it starts sliding.</item>
    /// <item>Touch movers (37-40) go when the ball rolls onto the arrow.</item>
    /// <item>Auto movers (41-48, 57-60) go when a moving platform ends up next to their platform: a chain
    /// reaction, each link 0.7 rows of ball travel later than the one before.</item>
    /// <item>Numbered platforms (RTX ON, 180-185): Mover 1-3 (touch) and Auto Mover 1-3 cells join only cells of
    /// the same kind and number, so two platforms can touch and still move apart. An arrow on the Object layer
    /// (186-189) gives the direction; the platform's kind decides when it goes. An Object-layer arrow on plain floor
    /// works like an auto mover with the old platform rules.</item>
    /// </list>
    /// Cells are (x = lane 0..4, y = row). This class only keeps the grid; MoverDirector animates it.
    /// </summary>
    public class MoverGrid
    {
        /// <summary>Rows of ball travel between a platform and the auto mover it sets off.</summary>
        public const float ChainDelayRows = 0.7f;

        static readonly Vector2Int[] Sides = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        // The original's search orders. They decide which auto mover of a chain goes first, so they are kept exactly.
        static readonly Vector2Int[] FloodOrder = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };    // ConnectTiles
        static readonly Vector2Int[] NeighbourOrder = { Vector2Int.down, Vector2Int.left, Vector2Int.right, Vector2Int.up }; // StoreAdjacents

        struct Cell
        {
            public bool solid;          // the ball can stand here
            public bool jump, fragile, slider;
            public bool isolated;       // never joins a platform (edged on all sides, or nothing next to it)
            public TileEdges edges;
            public int group;           // numbered platform: kind * 10 + number (0 = not numbered)
            public MoverKind arrow;
            public Vector2Int dir;
            public MoverKind tint;      // platform colour at the start: part of a touch or an auto platform
            public bool activated;      // arrow has fired
            public bool moved;          // a platform moved into this cell (never moves again)
            public bool moveReplaced;   // something moved in, so it stays solid when its own platform leaves
            public bool consumed;       // already part of a moving platform
        }

        /// <summary>One platform starting to move. Its slide runs while the ball travels from Offset to Offset + 1.</summary>
        public class Move
        {
            public Vector2Int trigger;
            public Vector2Int dir;
            public float offset;
            public readonly List<Vector2Int> section = new List<Vector2Int>();
            /// <summary>Every cell the platform moves into.</summary>
            public readonly List<Vector2Int> destinations = new List<Vector2Int>();
            /// <summary>Destinations that were empty; they need a floor collider when the slide starts.</summary>
            public readonly List<Vector2Int> newSolids = new List<Vector2Int>();
        }

        readonly Cell[,] cells;
        readonly int[] segment;
        readonly int rows;

        public bool HasMovers { get; }

        public MoverGrid(LevelData level)
        {
            rows = level.RowCount;
            cells = new Cell[rows, LevelData.Width];
            segment = new int[rows];

            int seg = 0;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < LevelData.Width; c++)
                    if (TileCatalog.IsSegmentStart(level.Get(r, c))) { seg++; break; }
                segment[r] = seg;

                for (int c = 0; c < LevelData.Width; c++)
                {
                    int id = level.Get(r, c);
                    var floor = TileCatalog.Decode(id).floor;
                    ref var cell = ref cells[r, c];
                    cell.solid = floor != FloorKind.None;
                    cell.jump = floor == FloorKind.Jump || floor == FloorKind.SuperJump;
                    cell.fragile = floor == FloorKind.Fragile;
                    cell.slider = floor == FloorKind.SlideLeft || floor == FloorKind.SlideRight;
                    cell.edges = TileCatalog.EdgesOf(id);
                    cell.isolated = cell.edges == TileEdges.All;
                    cell.arrow = TileCatalog.MoverOf(id, out cell.dir);
                    int number = TileCatalog.MoverGroupOf(id, out var groupKind);
                    if (number > 0)
                    {
                        cell.group = (int)groupKind * 10 + number;
                        cell.tint = groupKind;
                    }
                    if (cell.arrow == MoverKind.None && cell.solid && TileCatalog.ArrowOf(level.GetObject(r, c), out var arrowDir))
                    {
                        cell.arrow = number > 0 ? groupKind : MoverKind.Auto;
                        cell.dir = arrowDir;
                    }
                    if (cell.arrow != MoverKind.None) HasMovers = true;
                }
            }
            if (!HasMovers) return;

            // A tile with no connected neighbour at all is isolated (it moves alone), like the original.
            var lonely = new List<Vector2Int>();
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < LevelData.Width; c++)
                {
                    var p = new Vector2Int(c, r);
                    if (!Joinable(p)) continue;
                    bool any = false;
                    foreach (var side in Sides) any |= Connected(p, side);
                    if (!any) lonely.Add(p);
                }
            foreach (var p in lonely) cells[p.y, p.x].isolated = true;

            // Colour every platform by its arrow; a platform with a touch arrow counts as touch.
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < LevelData.Width; c++)
                {
                    var kind = cells[r, c].arrow;
                    if (kind == MoverKind.None) continue;
                    foreach (var p in Platform(new Vector2Int(c, r), int.MaxValue))
                    {
                        ref var cell = ref cells[p.y, p.x];
                        if (kind == MoverKind.Touch || cell.tint == MoverKind.None) cell.tint = kind;
                    }
                }
        }

        public MoverKind TintAt(int row, int lane) => In(lane, row) ? cells[row, lane].tint : MoverKind.None;

        public MoverKind ArrowAt(int row, int lane, out Vector2Int dir)
        {
            dir = Vector2Int.zero;
            if (!In(lane, row)) return MoverKind.None;
            dir = cells[row, lane].dir;
            return cells[row, lane].arrow;
        }

        /// <summary>True for a touch arrow that has not fired yet.</summary>
        public bool IsReadyTouch(int row, int lane)
        {
            if (!In(lane, row)) return false;
            var c = cells[row, lane];
            return c.arrow == MoverKind.Touch && Ready(c);
        }

        /// <summary>
        /// Fires the touch arrow at (row, lane) and every auto mover it sets off.
        /// <paramref name="ballZ"/> is where the ball is now; <paramref name="maxRow"/> the last row that has been built.
        /// </summary>
        public List<Move> Trigger(int row, int lane, float ballZ, int maxRow)
        {
            var moves = new List<Move>();
            if (IsReadyTouch(row, lane)) Fire(new Vector2Int(lane, row), ballZ, maxRow, moves);
            return moves;
        }

        /// <summary>
        /// Call when a slide has finished. Returns the cells that are now holes: every cell the platform left,
        /// except those another platform has moved into meanwhile (a tile is drawn there, so it stays solid).
        /// </summary>
        public List<Vector2Int> Finish(Move move)
        {
            var holes = new List<Vector2Int>();
            foreach (var p in move.section)
            {
                if (cells[p.y, p.x].moveReplaced) continue;
                cells[p.y, p.x].solid = false;
                holes.Add(p);
            }
            return holes;
        }

        void Fire(Vector2Int t, float offset, int maxRow, List<Move> moves)
        {
            ref var trigger = ref cells[t.y, t.x];
            trigger.activated = true;

            var move = new Move { trigger = t, dir = trigger.dir, offset = offset };
            if (trigger.isolated) move.section.Add(t);
            else move.section.AddRange(Platform(t, maxRow));

            var inPlatform = new HashSet<Vector2Int>(move.section);
            foreach (var p in move.section) cells[p.y, p.x].consumed = true;

            var autos = new List<Vector2Int>();
            foreach (var s in move.section)
            {
                var d = s + move.dir;
                if (Valid(d, s))
                {
                    move.destinations.Add(d);
                    ref var dest = ref cells[d.y, d.x];
                    if (!dest.solid)
                    {
                        dest.solid = true;
                        move.newSolids.Add(d);
                    }
                    dest.moved = true;
                    dest.moveReplaced = true;
                }
                CollectAutos(d, s, inPlatform, maxRow, autos);
            }
            trigger.moved = true;
            moves.Add(move);

            // Each auto mover runs once, even when one platform holds two arrows (57-60 pairs).
            foreach (var a in autos)
                if (Ready(cells[a.y, a.x])) Fire(a, offset + ChainDelayRows, maxRow, moves);
        }

        /// <summary>Auto arrows on platforms (or isolated tiles) that touch destination cell d. Rows not built yet are skipped.</summary>
        void CollectAutos(Vector2Int d, Vector2Int source, HashSet<Vector2Int> inPlatform, int maxRow, List<Vector2Int> autos)
        {
            foreach (var side in NeighbourOrder)
            {
                var n = d + side;
                if (n.y > maxRow || !Valid(n, source) || inPlatform.Contains(n) || !cells[n.y, n.x].solid) continue;
                var candidates = cells[n.y, n.x].isolated ? new List<Vector2Int> { n } : Platform(n, maxRow);
                foreach (var p in candidates)
                {
                    var c = cells[p.y, p.x];
                    if (c.arrow == MoverKind.Auto && Ready(c) && !inPlatform.Contains(p) && !autos.Contains(p)) autos.Add(p);
                }
            }
        }

        /// <summary>
        /// Flood fill of the platform that contains <paramref name="start"/>, up to <paramref name="maxRow"/>.
        /// Depth first in FloodOrder, like the original, so the cells come out in the same order.
        /// </summary>
        List<Vector2Int> Platform(Vector2Int start, int maxRow)
        {
            if (!Joinable(start)) return new List<Vector2Int> { start };
            var result = new List<Vector2Int>();
            var seen = new HashSet<Vector2Int>();
            var stack = new Stack<Vector2Int>();
            stack.Push(start);
            while (stack.Count > 0)
            {
                var p = stack.Pop();
                if (!seen.Add(p)) continue; // reached twice before its turn came
                result.Add(p);
                // Pushed in reverse, so the first direction is the next one popped.
                for (int i = FloodOrder.Length - 1; i >= 0; i--)
                {
                    var side = FloodOrder[i];
                    var q = p + side;
                    if (q.y > maxRow || seen.Contains(q) || !Connected(p, side)) continue;
                    stack.Push(q);
                }
            }
            return result;
        }

        static bool Ready(Cell c) => !c.activated && !c.moved && !c.consumed;

        bool In(int lane, int row) => row >= 0 && row < rows && lane >= 0 && lane < LevelData.Width;

        /// <summary>Inside the board and in the same segment as <paramref name="from"/>.</summary>
        bool Valid(Vector2Int p, Vector2Int from) => In(p.x, p.y) && segment[p.y] == segment[from.y];

        /// <summary>Can this cell be part of a platform at all?</summary>
        bool Joinable(Vector2Int p)
        {
            if (!In(p.x, p.y)) return false;
            var c = cells[p.y, p.x];
            return c.solid && !c.isolated && !c.jump && !c.fragile && !c.slider && !c.moved && !c.consumed;
        }

        /// <summary>Do p and its neighbour on <paramref name="side"/> belong to the same platform?</summary>
        bool Connected(Vector2Int p, Vector2Int side)
        {
            var q = p + side;
            if (!Joinable(p) || !Joinable(q) || segment[p.y] != segment[q.y]) return false;
            if (cells[p.y, p.x].group != cells[q.y, q.x].group) return false; // other numbers are other platforms
            var a = cells[p.y, p.x].edges;
            var b = cells[q.y, q.x].edges;
            if (side == Vector2Int.up) return (a & TileEdges.Top) == 0 && (b & TileEdges.Bottom) == 0;
            if (side == Vector2Int.down) return (a & TileEdges.Bottom) == 0 && (b & TileEdges.Top) == 0;
            if (side == Vector2Int.left) return (a & TileEdges.Left) == 0 && (b & TileEdges.Right) == 0;
            return (a & TileEdges.Right) == 0 && (b & TileEdges.Left) == 0;
        }
    }
}
