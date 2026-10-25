using System.Collections.Generic;
using UnityEngine;

namespace RTXOn
{
    /// <summary>The fragile floor variants of the original (tile IDs 3-8 and the fragile enemy and pickup tiles).</summary>
    public enum FragileKind { None, Fragile0, Fragile1, Fragile2, Checker, Row, Column }

    /// <summary>
    /// Groups touching fragile tiles into sections, like Rolling Sky 1.0 (LevelConstructor.AreConnected):
    /// <list type="bullet">
    /// <item>Checker tiles never join: each is its own section.</item>
    /// <item>Row tiles join only within their row, Column tiles only within their lane.</item>
    /// <item>Fragile 0, 1 and 2 join neighbours of the same number, so a designer splits groups by using another number.</item>
    /// </list>
    /// Sections stay inside one segment (rows starting at IDs 109-112). When the ball touches a tile of a section,
    /// the rest of the section falls at once and the section's tiles in the touched row when the ball leaves that
    /// row (see FragileTile).
    /// </summary>
    public class FragileSections
    {
        static readonly Vector2Int[] Sides = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        readonly FragileKind[,] kinds;
        readonly int[] segment;
        readonly int[,] section; // -1 = not fragile
        readonly int rows;

        public int Count { get; }

        public static FragileKind KindOf(int id)
        {
            if (TileCatalog.Decode(id).floor != FloorKind.Fragile) return FragileKind.None;
            switch (id)
            {
                case 4: return FragileKind.Fragile1;
                case 5: return FragileKind.Fragile2;
                case 6: case 12: case 15: case 51: case 119: case 131: return FragileKind.Checker;
                case 7: case 16: return FragileKind.Row;
                case 8: return FragileKind.Column;
                default: return FragileKind.Fragile0;
            }
        }

        public FragileSections(LevelData level)
        {
            rows = level.RowCount;
            kinds = new FragileKind[rows, LevelData.Width];
            segment = new int[rows];
            section = new int[rows, LevelData.Width];

            int seg = 0;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < LevelData.Width; c++)
                    if (TileCatalog.IsSegmentStart(level.Get(r, c))) { seg++; break; }
                segment[r] = seg;
                for (int c = 0; c < LevelData.Width; c++)
                {
                    kinds[r, c] = KindOf(level.Get(r, c));
                    section[r, c] = -1;
                }
            }

            // Flood fill every fragile tile that has no section yet.
            int next = 0;
            var stack = new Stack<Vector2Int>();
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < LevelData.Width; c++)
                {
                    if (kinds[r, c] == FragileKind.None || section[r, c] >= 0) continue;
                    int id = next++;
                    section[r, c] = id;
                    stack.Push(new Vector2Int(c, r));
                    while (stack.Count > 0)
                    {
                        var p = stack.Pop();
                        foreach (var side in Sides)
                        {
                            var q = p + side;
                            if (!Connected(p, q) || section[q.y, q.x] >= 0) continue;
                            section[q.y, q.x] = id;
                            stack.Push(q);
                        }
                    }
                }
            Count = next;
        }

        /// <summary>Segment number of a row (rows starting at IDs 109-112 begin a new one).</summary>
        public int SegmentOf(int row) => row >= 0 && row < rows ? segment[row] : -1;

        /// <summary>Section number of a cell, or -1 when it is not fragile.</summary>
        public int SectionAt(int row, int lane) => In(lane, row) ? section[row, lane] : -1;

        /// <summary>Is the neighbour of (row, lane) on that side part of the same section? Used to close the gap between them.</summary>
        public bool JoinedTo(int row, int lane, Vector2Int side)
        {
            var p = new Vector2Int(lane, row);
            return Connected(p, p + side);
        }

        bool In(int lane, int row) => row >= 0 && row < rows && lane >= 0 && lane < LevelData.Width;

        bool Connected(Vector2Int p, Vector2Int q)
        {
            if (!In(p.x, p.y) || !In(q.x, q.y) || segment[p.y] != segment[q.y]) return false;
            var a = kinds[p.y, p.x];
            var b = kinds[q.y, q.x];
            if (a == FragileKind.None || b == FragileKind.None) return false;
            if (a == FragileKind.Checker || b == FragileKind.Checker) return false;
            if (a == FragileKind.Row || b == FragileKind.Row) return p.y == q.y;
            if (a == FragileKind.Column || b == FragileKind.Column) return p.x == q.x;
            return a == b;
        }
    }
}
