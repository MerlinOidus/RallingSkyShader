using System;
using UnityEngine;

namespace RTXOn
{
    /// <summary>Moving platform arrows: touch movers go when the ball rolls onto the arrow, auto movers in a chain reaction.</summary>
    public enum MoverKind { None, Touch, Auto }

    /// <summary>Bevelled edges of a floor tile. An edge stops a moving platform from taking the neighbour on that side.</summary>
    [Flags]
    public enum TileEdges { None = 0, Top = 1, Bottom = 2, Left = 4, Right = 8, All = 15 }

    public enum FloorKind { None, Normal, Fragile, Jump, SuperJump, SlideLeft, SlideRight }

    /// <summary>
    /// The three layers of a cell: the floor (Tiles: floor, fragile, jump, movers, sliders, animated tiles, markers),
    /// what the ball picks up (Collect: gems, stars, crowns, power-ups) and the obstacle on it (Object).
    /// </summary>
    public enum TileLayer { Tile, Object, Collect }

    public enum ItemKind
    {
        None,
        Gem, FloatingGem, Star, FloatingStar,
        Crusher, CrusherSafe,
        Riser,
        Tree, Palm,
        RollerLeft, RollerRight,
        FloaterGround, FloaterAir,
        BinaryLeft, BinaryRight,
        Pillar, PillarSafe, LaserPillar,
        Spotlight, Laser,
        Crown,
        /// <summary>RTX ON power-up (160-168): param = <see cref="PartKind"/> - FlyJump.</summary>
        PowerUp,
        /// <summary>RTX ON mover arrow on the Object layer (186-189): param = direction 0 up, 1 down, 2 left, 3 right.
        /// It moves the numbered platform it stands on (or the floor around it, like an auto mover).</summary>
        MoverArrow,
        /// <summary>RTX ON Custom Move Block (158): a deadly block with its own animation (PartArgs, CustomMotion).</summary>
        CustomBlock,
        /// <summary>RTX ON Trigger Object (169): one animation that waits for a trigger tile with the same group (TriggerAnimator).</summary>
        TriggerObject,
    }

    public struct TileSpec
    {
        public FloorKind floor;
        public ItemKind item;
        public int param; // pillar height, binary size (1 = mini, 2 = full), riser / tree variant (0 = main model)
        /// <summary>Binary: lanes beside it (toward its side) that its arm makes deadly, like the original's danger spread.</summary>
        public int reach;
        /// <summary>Binary 77-80 (the original's BinaryOffset): its body sits one lane away from its side.</summary>
        public bool offset;

        public TileSpec(FloorKind floor, ItemKind item = ItemKind.None, int param = 0)
        {
            this.floor = floor;
            this.item = item;
            this.param = param;
            reach = 0;
            offset = false;
        }
    }

    /// <summary>
    /// Maps the Rolling Sky 1.0 tile IDs (see PANDUAN-LEVEL.md) to a floor + item.
    /// Mover tiles (37-48, 57-60) are floor here; <see cref="MoverOf"/> and <see cref="EdgesOf"/> give their
    /// movement and MoverGrid moves them. Top = forward (+z, towards the end of the level).
    /// </summary>
    public static class TileCatalog
    {
        /// <summary>Crown pickup on plain floor (RTX ON's own ID; Rolling Sky 1.0 had no crowns). 3 per level is the norm.</summary>
        public const int CrownId = 150;
        /// <summary>Finish line mesh: the last row made only of 110 (110 also marks moving-platform segments mid-level).</summary>
        public const int FinishId = 110;
        /// <summary>Portal mesh, centred on the run of 113s in the last row.</summary>
        public const int PortalId = 113;
        /// <summary>RTX ON's own IDs: Riser Variants element 0-2 (151-153) and Tree Variants element 0-2 (154-156), on plain floor.</summary>
        /// <summary>Binary sizes (TileSpec.param): 1 = Mini, 2 = Slasher, 3 = Hammer.</summary>
        public const int BinaryMini = 1, BinarySlasher = 2, BinaryHammer = 3;
        /// <summary>RTX ON Hammer binaries: 190 left, 191 right, 192 offset left, 193 offset right (like Slasher 65/66, 77/78).</summary>
        public const int HammerFirst = 190, HammerCount = 4;
        public const int RiserVariantId = 151;
        public const int TreeVariantId = 154;
        public const int VariantCount = 3;

        public static TileSpec Decode(int id)
        {
            var N = FloorKind.Normal;
            var F = FloorKind.Fragile;
            var SL = FloorKind.SlideLeft;
            var SR = FloorKind.SlideRight;
            var X = FloorKind.None;

            switch (id)
            {
                case 0: return new TileSpec(X);
                case 1: return new TileSpec(N);
                case 2: return new TileSpec(FloorKind.Jump);
                case 32: return new TileSpec(FloorKind.SuperJump);
                // Jump + gem / star: the pickup sits on the pad, taken as the ball takes off (like the original).
                case 54: return new TileSpec(FloorKind.Jump, ItemKind.Gem);
                case 120: return new TileSpec(FloorKind.Jump, ItemKind.Star);
                case CrownId: return new TileSpec(N, ItemKind.Crown);

                case 19: return new TileSpec(SL);
                case 20: return new TileSpec(SR);
                // Numbered platforms (Adrian 2026-10-20): Mover 1-3 (touch, yellow), Auto Mover 1-3 (red).
                case 180: case 181: case 182: case 183: case 184: case 185: return new TileSpec(N);
                case 186: case 187: case 188: case 189: return new TileSpec(N, ItemKind.MoverArrow, id - MoverArrowFirst);
                case PartArgs.CustomMoveBlockId: return new TileSpec(N, ItemKind.CustomBlock);
                case PartArgs.TriggerObjectId: return new TileSpec(N, ItemKind.TriggerObject);

                case 49: return new TileSpec(N, ItemKind.Gem);
                case 50: case 51: return new TileSpec(F, ItemKind.Gem);
                case 52: return new TileSpec(N, ItemKind.FloatingGem); // floating pickups stand on ground in the original
                case 53: return new TileSpec(F, ItemKind.FloatingGem);
                case 55: return new TileSpec(SL, ItemKind.Gem);
                case 56: return new TileSpec(SR, ItemKind.Gem);

                case 117: return new TileSpec(N, ItemKind.Star);
                case 118: case 119: return new TileSpec(F, ItemKind.Star);

                case 9: return new TileSpec(N, ItemKind.Crusher);
                case 10: return new TileSpec(N, ItemKind.CrusherSafe);
                case 11: return new TileSpec(F, ItemKind.Crusher);
                case 12: return new TileSpec(F, ItemKind.CrusherSafe);

                case 13: return new TileSpec(N, ItemKind.Riser);
                case 14: case 15: case 16: return new TileSpec(F, ItemKind.Riser);
                case 17: return new TileSpec(SL, ItemKind.Riser);
                case 18: return new TileSpec(SR, ItemKind.Riser);

                case 21: return new TileSpec(N, ItemKind.Tree);
                case 22: return new TileSpec(F, ItemKind.Tree);
                case 23: return new TileSpec(SL, ItemKind.Tree);
                case 24: return new TileSpec(SR, ItemKind.Tree);
                case 33: return new TileSpec(N, ItemKind.Palm);
                case 34: return new TileSpec(F, ItemKind.Palm);
                case 35: return new TileSpec(SL, ItemKind.Palm);
                case 36: return new TileSpec(SR, ItemKind.Palm);

                case 25: return new TileSpec(N, ItemKind.RollerLeft);
                case 26: return new TileSpec(N, ItemKind.RollerRight);
                case 27: return new TileSpec(F, ItemKind.RollerLeft);
                case 28: return new TileSpec(F, ItemKind.RollerRight);

                case 29: return new TileSpec(N, ItemKind.FloaterGround);
                case 30: return new TileSpec(F, ItemKind.FloaterGround);
                case 31: return new TileSpec(X, ItemKind.FloaterAir);

                case 97: return new TileSpec(N, ItemKind.Spotlight);
                case 98: return new TileSpec(F, ItemKind.Spotlight);
                case 99: return new TileSpec(SL, ItemKind.Spotlight);
                case 100: return new TileSpec(SR, ItemKind.Spotlight);

                case 101: return new TileSpec(N, ItemKind.Laser);
                case 102: return new TileSpec(F, ItemKind.Laser);
                case 103: return new TileSpec(SL, ItemKind.Laser);
                case 104: return new TileSpec(SR, ItemKind.Laser);
                case 105: return new TileSpec(N, ItemKind.LaserPillar, 3); // original: 3 blocks high

                // Segment / bucket markers act as the tile they stand for.
                case 109: case 113: return new TileSpec(X);
                case 110: case 114: return new TileSpec(N);
                case 111: case 115: return new TileSpec(F);
                case 112: case 116: return new TileSpec(FloorKind.Jump);
            }

            if (id >= 3 && id <= 8) return new TileSpec(F);

            if (id >= HammerFirst && id < HammerFirst + HammerCount)
            {
                bool hammerLeft = (id - HammerFirst) % 2 == 0, hammerOffset = id >= HammerFirst + 2;
                return new TileSpec(N, hammerLeft ? ItemKind.BinaryLeft : ItemKind.BinaryRight, BinaryHammer) { reach = hammerOffset ? 1 : 2, offset = hammerOffset };
            }
            // Binary blocks: odd = left, even = right, +2 = on fragile floor. 61-64 / 73-76 Mini, 65-68 / 77-80 Slasher.
            if ((id >= 61 && id <= 68) || (id >= 73 && id <= 80))
            {
                int local = (id >= 73 ? id - 73 : id - 61) % 8; // 0..7
                int size = local < 4 ? 1 : 2;                   // mini, full
                bool left = (id % 2) == 1;
                bool fragile = (local % 4) >= 2;
                // Original (LevelConstructor.TrySpreadDangers): Binary 65-68 reaches 2 lanes, BinaryMini 61-64 and
                // BinaryOffset 77-80 reach 1, BinaryMiniOffset 73-76 only its own tile.
                bool offset = id >= 73;
                int reach = size == 2 ? (offset ? 1 : 2) : (offset ? 0 : 1);
                return new TileSpec(fragile ? F : N, left ? ItemKind.BinaryLeft : ItemKind.BinaryRight, size) { reach = reach, offset = offset && size == 2 };
            }

            if (id >= 85 && id <= 88) return new TileSpec(N, ItemKind.Pillar, id - 84);
            if (id >= 89 && id <= 92) return new TileSpec(N, ItemKind.PillarSafe, id - 88);
            if (id >= RiserVariantId && id < RiserVariantId + VariantCount) return new TileSpec(N, ItemKind.Riser, id - RiserVariantId + 1);
            if (id >= TreeVariantId && id < TreeVariantId + VariantCount) return new TileSpec(N, ItemKind.Tree, id - TreeVariantId + 1);
            // RTX ON parts: power-ups stand on plain floor; the animated tiles are floor (and a gem / crown) that
            // TrackBuilder moves (PartArgs).
            if (id >= PartArgs.PowerUpFirst && id < PartArgs.PowerUpFirst + PartArgs.PowerUpCount)
                return new TileSpec(N, ItemKind.PowerUp, id - PartArgs.PowerUpFirst);
            switch (PartArgs.KindOf(id))
            {
                case PartKind.GemRise:
                case PartKind.GemDrop: return new TileSpec(N, ItemKind.Gem);
                case PartKind.CrownRise:
                case PartKind.CrownDrop: return new TileSpec(N, ItemKind.Crown);
                case PartKind.SlideFromLeft:
                case PartKind.SlideFromRight:
                case PartKind.RiseUp:
                case PartKind.SinkDown:
                case PartKind.Wave:
                case PartKind.DropIn:
                case PartKind.SpinIn:
                case PartKind.Shuffle:
                case PartKind.Extend:
                case PartKind.FallAway: return new TileSpec(N);
            }
            if (id == 129) return new TileSpec(N, ItemKind.FloatingStar);
            if (id == 130 || id == 131) return new TileSpec(F, ItemKind.FloatingStar);
            if (id == 132) return new TileSpec(FloorKind.Jump, ItemKind.FloatingStar);

            // Edge floors (69-72, 81-84, 93-96, 107, 108), movers (37-48, 57-60)
            // and variant markers (121-144) all become plain floor.
            return new TileSpec(N);
        }

        public const int MoverGroupFirst = 180;     // 180-182 Mover 1-3 (touch)
        public const int AutoGroupFirst = 183;      // 183-185 Auto Mover 1-3
        public const int MoverArrowFirst = 186;     // 186 up, 187 down, 188 left, 189 right (Object layer)

        /// <summary>
        /// Numbered platform tile: its kind (Mover = touch, Auto Mover) and number 1-3. Cells with the same kind and
        /// number that touch are one platform; other numbers next to it are separate platforms. 0 = not numbered.
        /// </summary>
        public static int MoverGroupOf(int id, out MoverKind kind)
        {
            kind = MoverKind.None;
            if (id >= MoverGroupFirst && id < MoverGroupFirst + 3) { kind = MoverKind.Touch; return id - MoverGroupFirst + 1; }
            if (id >= AutoGroupFirst && id < AutoGroupFirst + 3) { kind = MoverKind.Auto; return id - AutoGroupFirst + 1; }
            return 0;
        }

        /// <summary>Direction of a mover arrow from the Object layer (186-189).</summary>
        public static bool ArrowOf(int id, out Vector2Int dir)
        {
            dir = Vector2Int.zero;
            if (id < MoverArrowFirst || id >= MoverArrowFirst + 4) return false;
            dir = DirectionOf(id - MoverArrowFirst);
            return true;
        }

        /// <summary>
        /// Mover arrow of a tile, from LevelConstructor.DecideType in the original game.
        /// <paramref name="dir"/>: x = lanes (+1 = right), y = rows (+1 = forward).
        /// </summary>
        public static MoverKind MoverOf(int id, out Vector2Int dir)
        {
            dir = Vector2Int.zero;
            if (id >= 37 && id <= 48)
            {
                dir = DirectionOf((id - 37) % 4);
                return id <= 40 ? MoverKind.Touch : MoverKind.Auto;
            }
            switch (id)
            {
                case 57: case 59: dir = Vector2Int.left; return MoverKind.Auto;
                case 58: case 60: dir = Vector2Int.right; return MoverKind.Auto;
            }
            return MoverKind.None;
        }

        // Order of the original IDs: up (forward), down (back), left, right.
        static Vector2Int DirectionOf(int i) => i switch
        {
            0 => Vector2Int.up,
            1 => Vector2Int.down,
            2 => Vector2Int.left,
            _ => Vector2Int.right,
        };

        /// <summary>Edges of a tile (edged floors and the edged auto movers). Top = the forward side.</summary>
        public static TileEdges EdgesOf(int id)
        {
            const TileEdges T = TileEdges.Top, B = TileEdges.Bottom, L = TileEdges.Left, R = TileEdges.Right;
            switch (id)
            {
                case 69: return T;
                case 70: return B;
                case 71: return L;
                case 72: return R;
                case 81: return T | L;
                case 83: return T | R;
                case 82: return B | L;
                case 84: return B | R;
                case 93: return T | L | R;
                case 94: return B | L | R;
                case 95: case 58: case 59: return T | B | L;
                case 96: case 57: case 60: return T | B | R;
                case 107: return T | B;
                case 108: return L | R;
                case 45: case 46: case 47: case 48: return TileEdges.All;
            }
            return TileEdges.None;
        }

        /// <summary>The layer an ID is painted on. Finish line and portal (110, 113) are floor tiles.</summary>
        public static TileLayer LayerOf(int id)
        {
            if (id <= 0) return TileLayer.Tile;
            if (CustomObstacle.IsCustomId(id)) return CustomObstacles.LayerOf(id); // its own choice: Object, Tile or Collect
            if (id >= PartArgs.PowerUpFirst && id < PartArgs.PowerUpFirst + PartArgs.PowerUpCount) return TileLayer.Collect;
            switch (PartArgs.KindOf(id))
            {
                case PartKind.GemRise:
                case PartKind.GemDrop:
                case PartKind.CrownRise:
                case PartKind.CrownDrop: return TileLayer.Collect;
            }
            var spec = Decode(id);
            if (spec.item == ItemKind.None) return TileLayer.Tile;
            return IsPickup(spec.item) ? TileLayer.Collect : TileLayer.Object;
        }

        public static bool IsPickup(ItemKind item) =>
            item == ItemKind.Gem || item == ItemKind.FloatingGem || item == ItemKind.Star || item == ItemKind.FloatingStar ||
            item == ItemKind.Crown || item == ItemKind.PowerUp;

        /// <summary>
        /// Splits an original combined ID (floor and item in one, e.g. 14 = riser on fragile, 54 = gem on a jump pad)
        /// into its floor tile and its item: <paramref name="tile"/> keeps the floor (3-8 keep the fragile variant),
        /// <paramref name="item"/> is the item on plain floor (13 riser, 49 gem...), on <paramref name="layer"/>.
        /// False when the ID has no item (it is a floor tile already).
        /// </summary>
        public static bool Split(int id, out int tile, out int item, out TileLayer layer)
        {
            tile = id;
            item = 0;
            layer = TileLayer.Tile;
            if (id <= 0) return false;
            layer = LayerOf(id);
            if (layer == TileLayer.Tile) return false;
            if (CustomObstacle.IsCustomId(id))
            {
                tile = 1; // a custom obstacle typed into rows: plain floor with the obstacle on it
                item = id;
                return true;
            }
            var spec = Decode(id);
            tile = FloorTile(spec.floor, id);
            if (id >= PartArgs.PowerUpFirst && id < PartArgs.PowerUpFirst + PartArgs.PowerUpCount || PartArgs.IsPart(id))
            {
                item = id; // power-ups and gem / crown rise and drop: the ID itself (its ARGS are kept by ID)
                return true;
            }
            item = ItemTile(spec, id);
            return item != 0;
        }

        /// <summary>The plain floor tile of a floor kind (fragile: the variant of <paramref name="id"/>).</summary>
        public static int FloorTile(FloorKind floor, int id)
        {
            switch (floor)
            {
                case FloorKind.Normal: return 1;
                case FloorKind.Jump: return 2;
                case FloorKind.SuperJump: return 32;
                case FloorKind.SlideLeft: return 19;
                case FloorKind.SlideRight: return 20;
                case FloorKind.Fragile: return 3 + Mathf.Max(0, (int)FragileSections.KindOf(id) - (int)FragileKind.Fragile0);
                default: return 0;
            }
        }

        /// <summary>The ID of an item on plain floor (what the Collect and Object palettes paint).</summary>
        static int ItemTile(TileSpec spec, int id)
        {
            switch (spec.item)
            {
                case ItemKind.Gem: return 49;
                case ItemKind.FloatingGem: return 52;
                case ItemKind.Star: return 117;
                case ItemKind.FloatingStar: return 129;
                case ItemKind.Crown: return CrownId;
                case ItemKind.Crusher: return 9;
                case ItemKind.CrusherSafe: return 10;
                case ItemKind.Riser: return spec.param > 0 ? RiserVariantId + spec.param - 1 : 13;
                case ItemKind.Tree: return spec.param > 0 ? TreeVariantId + spec.param - 1 : 21;
                case ItemKind.Palm: return 33;
                case ItemKind.RollerLeft: return 25;
                case ItemKind.RollerRight: return 26;
                case ItemKind.FloaterGround: return 29;
                case ItemKind.FloaterAir: return 31;
                case ItemKind.BinaryLeft:
                case ItemKind.BinaryRight:
                {
                    if (id >= HammerFirst && id < HammerFirst + HammerCount) return id;
                    // 61-68 / 73-80: +2 = the same binary on fragile floor.
                    int local = (id >= 73 ? id - 73 : id - 61) % 8;
                    return (local % 4) >= 2 ? id - 2 : id;
                }
                case ItemKind.Pillar: return 84 + spec.param;
                case ItemKind.PillarSafe: return 88 + spec.param;
                case ItemKind.LaserPillar: return 105;
                case ItemKind.Spotlight: return 97;
                case ItemKind.Laser: return 101;
                case ItemKind.PowerUp: return PartArgs.PowerUpFirst + spec.param;
                case ItemKind.MoverArrow: return MoverArrowFirst + spec.param;
                case ItemKind.CustomBlock: return PartArgs.CustomMoveBlockId;
                case ItemKind.TriggerObject: return PartArgs.TriggerObjectId;
            }
            return 0;
        }

        /// <summary>Segment start markers (109-112). A moving platform never crosses into another segment.</summary>
        public static bool IsSegmentStart(int id) => id >= 109 && id <= 112;
    }
}
