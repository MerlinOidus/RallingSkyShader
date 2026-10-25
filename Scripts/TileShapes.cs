using System.Collections.Generic;
using UnityEngine;

namespace RTXOn
{
    /// <summary>The original floor meshes (prefabs 01_X .. 15_O), named by their empty sides and corners.</summary>
    public enum TileShape { X, X1, X2a, X2b, X3, X4, T, T1a, T1b, T2, L, L1, I, D, O }

    /// <summary>
    /// "Tile Type": the original game's floor look (Rolling Sky 1.0 LevelConstructor.ChooseCurrentPrefabName).
    /// Every floor tile is one of 15 meshes, picked by which of its 8 neighbours it is joined to: X = all joined,
    /// T = one side open, L = two sides round a corner, I = two sides across, D = three sides, O = alone; the number
    /// counts open corners. The mesh is then turned by quarter turns so its walls face the open sides.
    /// The meshes are authored in the original's frame (floor = XY plane, down = +Z); <see cref="Rotation"/> turns them
    /// into this game's frame.
    /// </summary>
    public static class TileShapes
    {
        /// <summary>Neighbour (lane, row) offsets, the original's Direction order: Top, TopRight, Right, BottomRight, Bottom, BottomLeft, Left, TopLeft.</summary>
        public static readonly Vector2Int[] Offsets =
        {
            new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(1, 0), new Vector2Int(1, -1),
            new Vector2Int(0, -1), new Vector2Int(-1, -1), new Vector2Int(-1, 0), new Vector2Int(-1, 1),
        };

        /// <summary>The mesh for a tile and how many quarter turns it gets. <paramref name="connected"/>: the 8 neighbours in <see cref="Offsets"/> order.</summary>
        public static TileShape Choose(bool[] connected, out int rotations)
        {
            var s = new bool[8];
            System.Array.Copy(connected, s, 8);
            int emptySides = 0, emptyCorners = 0;
            for (int i = 0; i < 8; i++)
                if (!s[i]) { if ((i & 1) == 1) emptyCorners++; else emptySides++; }
            if (emptyCorners > 0 && emptySides > 0)
            {
                // A corner beside an open side does not count: that side's wall already covers it.
                if (!s[1] && (!s[0] || !s[2])) { s[1] = true; emptyCorners--; }
                if (!s[3] && (!s[2] || !s[4])) { s[3] = true; emptyCorners--; }
                if (!s[5] && (!s[4] || !s[6])) { s[5] = true; emptyCorners--; }
                if (!s[7] && (!s[6] || !s[0])) { s[7] = true; emptyCorners--; }
            }

            rotations = 0;
            switch (emptySides)
            {
                case 0:
                    switch (emptyCorners)
                    {
                        case 0: return TileShape.X;
                        case 1: rotations = ForCorner(s, true); return TileShape.X1;
                        case 2:
                            if (CornersAdjacent(s)) { rotations = ForX2a(s); return TileShape.X2a; }
                            rotations = s[3] ? 0 : 1;
                            return TileShape.X2b;
                        case 3: rotations = ForCorner(s, false); return TileShape.X3;
                        default: return TileShape.X4;
                    }
                case 1:
                    rotations = ForSide(s, true);
                    switch (emptyCorners)
                    {
                        case 0: return TileShape.T;
                        case 1:
                        {
                            int c = ForCorner(s, true);
                            bool b = (rotations == 0 && c == 2) || (rotations == 1 && c == 3) || (rotations == 2 && c == 0) || (rotations == 3 && c == 1);
                            return b ? TileShape.T1b : TileShape.T1a;
                        }
                        default: return TileShape.T2;
                    }
                case 2:
                    if (SidesAdjacent(s))
                    {
                        rotations = ForL(s);
                        return emptyCorners >= 1 ? TileShape.L1 : TileShape.L;
                    }
                    rotations = s[0] || s[4] ? 1 : 0;
                    return TileShape.I;
                case 3:
                    rotations = ForSide(s, false);
                    return TileShape.D;
                default:
                    return TileShape.O;
            }
        }

        static int ForCorner(bool[] s, bool empty)
        {
            if (s[1] != empty) return 0;
            if (s[3] != empty) return 1;
            if (s[5] != empty) return 2;
            if (s[7] != empty) return 3;
            return 0;
        }

        static int ForSide(bool[] s, bool empty)
        {
            if (s[0] != empty) return 0;
            if (s[2] != empty) return 1;
            if (s[4] != empty) return 2;
            if (s[6] != empty) return 3;
            return 0;
        }

        static int ForX2a(bool[] s)
        {
            if (!s[1] && !s[3]) return 0;
            if (!s[3] && !s[5]) return 1;
            if (!s[5] && !s[7]) return 2;
            if (!s[7] && !s[1]) return 3;
            return 0;
        }

        static int ForL(bool[] s)
        {
            if (s[0] && s[2]) return 2;
            if (s[2] && s[4]) return 3;
            if (s[4] && s[6]) return 0;
            if (s[6] && s[0]) return 1;
            return 0;
        }

        static bool CornersAdjacent(bool[] s) => (!s[1] && !s[3]) || (!s[3] && !s[5]) || (!s[5] && !s[7]) || (!s[7] && !s[1]);
        static bool SidesAdjacent(bool[] s) => (!s[0] && !s[2]) || (!s[2] && !s[4]) || (!s[4] && !s[6]) || (!s[6] && !s[0]);

        /// <summary>
        /// Are two floor tiles joined, for the look (LevelConstructor.AreConnected)? Not across segments, holes, jump
        /// pads or tile edges; fragile only with fragile of the same section; sliders only with sliders of their row.
        /// <paramref name="side"/> is the index of (row2, lane2) in <see cref="Offsets"/>.
        /// </summary>
        /// <summary>
        /// A floor tile that animates on its own (an animated tile 145-149, 174-179, 194-199, or a Custom Move tile).
        /// Adrian 2026-10-24: it joins no neighbour, so it is a closed piece and its neighbours get their own walls too
        /// (a double wall where they meet is fine): no hollow, see-through side shows while it flips, flies or slides in.
        /// </summary>
        public static bool MovesAlone(LevelData level, int row, int lane)
        {
            if (level == null || row < 0 || row >= level.RowCount || lane < 0 || lane >= LevelData.Width) return false;
            var kind = PartArgs.KindOf(level.Get(row, lane));
            return PartArgs.IsAnimatedFloor(kind) || kind == PartKind.CustomMoveTile;
        }

        public static bool Connected(LevelData level, FragileSections fragiles, int row, int lane, int row2, int lane2, int side)
        {
            if (level == null || row2 < 0 || row2 >= level.RowCount || lane2 < 0 || lane2 >= LevelData.Width) return false;
            if (MovesAlone(level, row, lane) || MovesAlone(level, row2, lane2)) return false;
            if (fragiles != null && fragiles.SegmentOf(row) != fragiles.SegmentOf(row2)) return false;
            int a = level.Get(row, lane), b = level.Get(row2, lane2);
            var fa = TileCatalog.Decode(a).floor;
            var fb = TileCatalog.Decode(b).floor;
            if (fa == FloorKind.None || fb == FloorKind.None) return false;
            if (IsJump(fa) || IsJump(fb)) return false;
            var ea = TileCatalog.EdgesOf(a);
            var eb = TileCatalog.EdgesOf(b);
            if (ea == TileEdges.All || eb == TileEdges.All) return false; // walled in on every side: stands alone
            // Numbered platforms (Mover / Auto Mover 1-3) join only the same kind and number, so they look like separate pieces.
            if (TileCatalog.MoverGroupOf(a, out var ka) * 10 + (int)ka != TileCatalog.MoverGroupOf(b, out var kb) * 10 + (int)kb) return false;

            bool fragA = fa == FloorKind.Fragile, fragB = fb == FloorKind.Fragile;
            if (fragA || fragB) return fragA && fragB && fragiles != null && fragiles.JoinedTo(row, lane, Offsets[side]);

            bool slideA = fa == FloorKind.SlideLeft || fa == FloorKind.SlideRight;
            bool slideB = fb == FloorKind.SlideLeft || fb == FloorKind.SlideRight;
            if (slideA) return slideB && row == row2;
            if (slideB) return false;

            switch (side)
            {
                case 0: return (ea & TileEdges.Top) == 0 && (eb & TileEdges.Bottom) == 0;
                case 2: return (ea & TileEdges.Right) == 0 && (eb & TileEdges.Left) == 0;
                case 4: return (ea & TileEdges.Bottom) == 0 && (eb & TileEdges.Top) == 0;
                case 6: return (ea & TileEdges.Left) == 0 && (eb & TileEdges.Right) == 0;
                default: return true; // edges do not cut corners
            }
        }

        static bool IsJump(FloorKind k) => k == FloorKind.Jump || k == FloorKind.SuperJump;

        /// <summary>The 8 neighbours of a cell, joined or not, ready for <see cref="Choose"/>.</summary>
        public static bool[] Sides(LevelData level, FragileSections fragiles, int row, int lane) =>
            Sides(level, fragiles, row, lane, MoverEdge.Auto);

        /// <summary>
        /// Like <see cref="Sides(LevelData, FragileSections, int, int)"/>, with the theme's Mover Edge for mover tiles:
        /// Always = framed on every side (joined to nothing), Never = no border (joined all round). Texture only.
        /// </summary>
        public static bool[] Sides(LevelData level, FragileSections fragiles, int row, int lane, MoverEdge moverEdge)
        {
            var sides = new bool[8];
            if (moverEdge != MoverEdge.Auto && level != null && TileCatalog.MoverOf(level.Get(row, lane), out _) != MoverKind.None)
            {
                for (int i = 0; i < 8; i++) sides[i] = moverEdge == MoverEdge.Never;
                return sides;
            }
            for (int i = 0; i < 8; i++)
            {
                var o = Offsets[i];
                sides[i] = Connected(level, fragiles, row, lane, row + o.y, lane + o.x, i);
            }
            return sides;
        }

        /// <summary>
        /// Turns an original mesh into this game's frame (its XY floor to our XZ, its +Z down to our -Y), then by
        /// <paramref name="rotations"/> quarter turns about the up axis, like the original's geo rotation.
        /// </summary>
        public static Quaternion Rotation(int rotations) =>
            Quaternion.Euler(0f, rotations * 90f, 0f) * Quaternion.Euler(90f, 0f, 0f);

        /// <summary>
        /// Puts an original tile prefab (floor mesh, jump pad, mover arrow) on a cell. The prefab's own rotation is
        /// only for the editor view and is dropped, like the original did. <paramref name="mat"/> replaces the
        /// mesh's materials (the atlas material of the tile set). <paramref name="states"/>: show "state1" (state01)
        /// and hide "state2", for prefabs that switch after the ball used them. <paramref name="cellUV"/>: the mesh gets
        /// one 0..1 UV square per cell (<see cref="CellUV"/>), for the RTX materials instead of the atlas; its top gets
        /// <paramref name="mat"/> and its sides <paramref name="side"/> (null = <paramref name="mat"/> too).
        /// </summary>
        public static GameObject Place(Transform parent, GameObject model, int rotations, Material mat, string name, bool states, bool cellUV = false, Material side = null)
        {
            if (model == null) return null;
            var holder = new GameObject(name).transform;
            holder.SetParent(parent, false);
            holder.localRotation = Rotation(rotations);
            var v = Object.Instantiate(model, holder, false);
            v.transform.localPosition = Vector3.zero;
            v.transform.localRotation = Quaternion.identity;
            v.transform.localScale = Vector3.one;
            foreach (var c in v.GetComponentsInChildren<Collider>(true))
            {
                c.enabled = false;
                if (Application.isPlaying) Object.Destroy(c); else Object.DestroyImmediate(c);
            }
            if (cellUV)
                foreach (var f in v.GetComponentsInChildren<MeshFilter>(true))
                    f.sharedMesh = CellUV(f.sharedMesh);
            if (mat != null)
                foreach (var r in v.GetComponentsInChildren<Renderer>(true))
                {
                    if (cellUV && r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null && mf.sharedMesh.subMeshCount == 2 &&
                        mf.sharedMesh.name.EndsWith(" (RTX)"))
                    {
                        r.sharedMaterials = new[] { mat, side != null ? side : mat }; // CellUV: top, sides
                        continue;
                    }
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                    r.sharedMaterials = mats;
                }
            foreach (var t in v.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("shadow", System.StringComparison.OrdinalIgnoreCase) || t.name.IndexOf("Shadow", System.StringComparison.Ordinal) > 0)
                    t.gameObject.SetActive(false); // the original's baked shadow quads
            if (states) SwitchState(holder, false);
            return holder.gameObject;
        }

        /// <summary>
        /// Adrian 2026-10-24: an animated tile is 16_OD (top and front) with 15_O turned upside down under it (bottom and
        /// back), one closed block. Puts <paramref name="model"/> (15_O) flipped under <paramref name="tile"/> (what
        /// <see cref="Place"/> returned for 16_OD): turned 180 degrees about the tile's width, its top becomes the bottom.
        /// </summary>
        public static void AddUnderside(GameObject tile, GameObject model, Material mat, bool cellUV = false, Material side = null)
        {
            if (tile == null || model == null) return;
            var under = Place(tile.transform, model, 0, mat, "Underside", false, cellUV, side);
            if (under == null) return;
            var t = under.transform;
            t.localRotation = Quaternion.identity; // the tile's holder already turns the original's frame into ours
            t.localPosition = Vector3.zero;
            // The mesh's depth in the original frame (down = +z), to flip it within the same block.
            float zMin = float.MaxValue, zMax = float.MinValue;
            foreach (var r in under.GetComponentsInChildren<Renderer>(true))
            {
                var b = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                    float z = tile.transform.InverseTransformPoint(corner).z;
                    zMin = Mathf.Min(zMin, z);
                    zMax = Mathf.Max(zMax, z);
                }
            }
            if (zMin > zMax) return;
            t.localRotation = Quaternion.Euler(180f, 0f, 0f); // (x, y, z) -> (x, -y, -z)
            t.localPosition = new Vector3(0f, 0f, zMin + zMax);
        }

        static MaterialPropertyBlock joinBlock;
        static readonly int JoinedId = Shader.PropertyToID("_Joined");

        /// <summary>
        /// Tells the glass shader (RTXOn/Glass) which edges of this fragile cell join a tile of the same section, so its
        /// tile lines run only around the section's outline: a Fragile section reads as one slab, Checker tiles (which
        /// never join) as single blocks. _Joined = (U 0, U 1, V 0, V 1 edge of the cell UV), 1 = joined.
        /// </summary>
        public static void MarkFragileJoins(GameObject tile, int rotations, FragileSections fragiles, int row, int lane)
        {
            if (tile == null || fragiles == null || row < 0 || lane < 0) return;
            var rot = Rotation(rotations);
            float Joined(Vector3 meshDir)
            {
                var d = rot * meshDir; // the cell UV edge's direction on the track: x = lane, z = row
                return fragiles.JoinedTo(row, lane, new Vector2Int(Mathf.RoundToInt(d.x), Mathf.RoundToInt(d.z))) ? 1f : 0f;
            }
            var joined = new Vector4(Joined(Vector3.left), Joined(Vector3.right), Joined(Vector3.down), Joined(Vector3.up));
            if (joinBlock == null) joinBlock = new MaterialPropertyBlock();
            foreach (var r in tile.GetComponentsInChildren<Renderer>(true))
            {
                r.GetPropertyBlock(joinBlock);
                joinBlock.SetVector(JoinedId, joined);
                r.SetPropertyBlock(joinBlock);
            }
        }

        static readonly Dictionary<Mesh, Mesh> cellUVMeshes = new Dictionary<Mesh, Mesh>();
        static bool warnedUnreadable;

        /// <summary>
        /// A copy of an original floor mesh with the UVs of the built-in RTX slab: the top gets the whole 0..1 square
        /// (so the floor texture's bevel, inlay groove and neon line sit on every cell), the walls a 0..1 strip from
        /// top to bottom. The atlas UVs are dropped. Two sub-meshes: 0 = the top, 1 = the walls (and anything else), so
        /// the sides can have their own material (Track Theme > Floor Side). Made once per mesh. A mesh Unity cannot read (Read/Write off, in
        /// a build) stays as it is; Hook Up Tile Types turns Read/Write on.
        /// </summary>
        public static Mesh CellUV(Mesh source)
        {
            if (source == null) return null;
            if (cellUVMeshes.TryGetValue(source, out var cached) && cached != null) return cached;
            if (!source.isReadable && !Application.isEditor)
            {
                if (!warnedUnreadable) Debug.LogWarning($"RTX ON: mesh {source.name} has Read/Write off, so the RTX tile look cannot remap it. Run RTX ON > Hook Up Tile Types.");
                warnedUnreadable = true;
                return source;
            }

            var m = Object.Instantiate(source);
            m.name = source.name + " (RTX)";
            var verts = m.vertices;
            var normals = m.normals;
            if (normals == null || normals.Length != verts.Length)
            {
                m.RecalculateNormals();
                normals = m.normals;
            }
            // Original frame: floor = XY, the top at z = 0, the walls hang down to +z.
            float depth = Mathf.Max(0.01f, source.bounds.max.z);
            var uv = new Vector2[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                var p = verts[i];
                var n = normals[i];
                float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
                if (az >= ax && az >= ay) uv[i] = new Vector2(p.x + 0.5f, p.y + 0.5f);
                else uv[i] = new Vector2((ax > ay ? p.y : p.x) + 0.5f, 1f - Mathf.Clamp01(p.z / depth));
            }
            m.uv = uv;

            // Split the triangles: top (facing up, i.e. the original's -Z) and everything else.
            var top = new List<int>();
            var sides = new List<int>();
            for (int s = 0; s < source.subMeshCount; s++)
            {
                var tris = source.GetTriangles(s);
                for (int i = 0; i + 2 < tris.Length; i += 3)
                {
                    var n = normals[tris[i]] + normals[tris[i + 1]] + normals[tris[i + 2]];
                    bool up = n.z < 0f && Mathf.Abs(n.z) >= Mathf.Abs(n.x) && Mathf.Abs(n.z) >= Mathf.Abs(n.y);
                    var list = up ? top : sides;
                    list.Add(tris[i]);
                    list.Add(tris[i + 1]);
                    list.Add(tris[i + 2]);
                }
            }
            m.subMeshCount = 2;
            m.SetTriangles(top, 0);
            m.SetTriangles(sides, 1);
            m.RecalculateTangents();
            cellUVMeshes[source] = m;
            return m;
        }

        /// <summary>
        /// The original's FrameAnim.SequenceAuto: children "state1".."state2" (or "state01".."state02") are shown
        /// one at a time. Shows the first (<paramref name="after"/> false) or the second. False when there are none.
        /// </summary>
        public static bool SwitchState(Transform root, bool after)
        {
            Transform first = null, second = null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("state", System.StringComparison.OrdinalIgnoreCase)) continue;
                if (t.name.EndsWith("1")) first = t;
                else if (t.name.EndsWith("2")) second = t;
            }
            if (first == null || second == null) return false;
            first.gameObject.SetActive(!after);
            second.gameObject.SetActive(after);
            return true;
        }
    }
}
