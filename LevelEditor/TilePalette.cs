using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// The tabs of the level editor palette. Floor (with Fragile and Jump on the same page, called Tiles), Moving and
    /// Animated are the Tiles tab's pages (the Tiles layer); Collect and Object are the other two layers; Triggers are row tools; Props are the theme's
    /// decoration models (level.json "props", no tile ID: their buttons are made from the look's Props list).
    /// Collection shows the favourites (the star), from every tab.
    /// </summary>
    public enum TileGroup { Floor, Fragile, Jump, Moving, Animated, Collect, Obstacle, Triggers, Props, Collection }

    /// <summary>One tile button in the level editor palette.</summary>
    public readonly struct PaletteTile
    {
        public readonly int id;
        public readonly string name;
        public readonly TileGroup group;

        public PaletteTile(int id, string name, TileGroup group)
        {
            this.id = id;
            this.name = name;
            this.group = group;
        }
    }

    /// <summary>
    /// Every tile the level editor offers, with a name and a colour per group.
    /// The IDs are the Rolling Sky 1.0 tile IDs (PANDUAN-LEVEL.md), so a level drawn in the editor
    /// plays the same as one made in Tiled. Names and colours match the web level editor.
    /// </summary>
    public static class TilePalette
    {
        const TileGroup F = TileGroup.Floor;
        const TileGroup R = TileGroup.Fragile;
        const TileGroup J = TileGroup.Jump;
        const TileGroup M = TileGroup.Moving;
        const TileGroup A = TileGroup.Animated;
        const TileGroup C = TileGroup.Collect;
        const TileGroup O = TileGroup.Obstacle;
        const TileGroup T = TileGroup.Triggers;

        // Objects and pickups are painted on top of a tile, so each has one entry (on plain floor); the floor under it
        // (fragile, slider, jump...) is the tile. Old levels with combined IDs (14 = riser on fragile) are split on load.
        public static readonly PaletteTile[] All =
        {
            new PaletteTile(1, "Floor", F),
            new PaletteTile(69, "Edge top", F), new PaletteTile(70, "Edge bottom", F),
            new PaletteTile(71, "Edge left", F), new PaletteTile(72, "Edge right", F),
            new PaletteTile(81, "Edge TL", F), new PaletteTile(82, "Edge BL", F),
            new PaletteTile(83, "Edge TR", F), new PaletteTile(84, "Edge BR", F),
            new PaletteTile(93, "Edge TLR", F), new PaletteTile(94, "Edge BLR", F),
            new PaletteTile(95, "Edge TBL", F), new PaletteTile(96, "Edge TBR", F),
            new PaletteTile(107, "Edge TB", F), new PaletteTile(108, "Edge LR", F),

            new PaletteTile(3, "Fragile 0", R), new PaletteTile(4, "Fragile 1", R), new PaletteTile(5, "Fragile 2", R),
            new PaletteTile(6, "Fragile checker", R), new PaletteTile(7, "Fragile row", R), new PaletteTile(8, "Fragile column", R),

            new PaletteTile(2, "Jump", J), new PaletteTile(32, "Super jump", J),

            new PaletteTile(19, "Slide left", M), new PaletteTile(20, "Slide right", M),
            // Numbered platforms + separate arrows (Object layer), like the fan editor.
            new PaletteTile(180, "Mover 1", M), new PaletteTile(181, "Mover 2", M), new PaletteTile(182, "Mover 3", M),
            new PaletteTile(183, "Auto Mover 1", M), new PaletteTile(184, "Auto Mover 2", M), new PaletteTile(185, "Auto Mover 3", M),
            new PaletteTile(186, "Arrow up", M), new PaletteTile(187, "Arrow down", M),
            new PaletteTile(188, "Arrow left", M), new PaletteTile(189, "Arrow right", M),
            new PaletteTile(37, "Touch mover up", M), new PaletteTile(38, "Touch mover down", M),
            new PaletteTile(39, "Touch mover left", M), new PaletteTile(40, "Touch mover right", M),
            new PaletteTile(41, "Auto mover up", M), new PaletteTile(42, "Auto mover down", M),
            new PaletteTile(43, "Auto mover left", M), new PaletteTile(44, "Auto mover right", M),
            new PaletteTile(45, "Auto solo up", M), new PaletteTile(46, "Auto solo down", M),
            new PaletteTile(47, "Auto solo left", M), new PaletteTile(48, "Auto solo right", M),
            new PaletteTile(59, "Auto left, left end", M), new PaletteTile(57, "Auto left, right end", M),
            new PaletteTile(58, "Auto right, left end", M), new PaletteTile(60, "Auto right, right end", M),

            new PaletteTile(174, "Tile slide from left", A), new PaletteTile(175, "Tile slide from right", A),
            new PaletteTile(176, "Tile rise up", A), new PaletteTile(177, "Tile sink down", A),
            new PaletteTile(194, "Tile wave", A), new PaletteTile(195, "Tile drop in", A),
            new PaletteTile(196, "Tile spin in", A), new PaletteTile(197, "Tile shuffle", A),
            new PaletteTile(198, "Tile extend", A), new PaletteTile(199, "Tile fall away", A),
            new PaletteTile(145, "Tile flip in", A), new PaletteTile(146, "Tile pop in", A),
            new PaletteTile(147, "Tile drawbridge", A), new PaletteTile(148, "Tile screw up", A),
            new PaletteTile(149, "Tile roll in", A), new PaletteTile(178, "Tile fly away", A),
            new PaletteTile(179, "Tile blink in", A),
            new PaletteTile(PartArgs.CustomMoveTileId, "Custom move tile", A),
            new PaletteTile(PartArgs.TriggerTileId, "CustomTriggerTiles", A),

            new PaletteTile(49, "Gem", C), new PaletteTile(52, "Gem floating", C),
            new PaletteTile(117, "Star", C), new PaletteTile(129, "Star floating", C),
            new PaletteTile(TileCatalog.CrownId, "Crown", C),
            new PaletteTile(170, "Gem Rise", C), new PaletteTile(171, "Gem Drop", C),
            new PaletteTile(172, "Crown Rise", C), new PaletteTile(173, "Crown Drop", C),
            new PaletteTile(160, "Fly Jump", C), new PaletteTile(161, "Short Jump", C), new PaletteTile(162, "Bounce", C),
            new PaletteTile(163, "Switch", C), new PaletteTile(164, "Invisible", C),
            new PaletteTile(165, "Speed Up", C), new PaletteTile(166, "Speed Down", C),
            new PaletteTile(167, "Hide Objects", C), new PaletteTile(168, "Dialogue", C),
            new PaletteTile(CheckpointTool, "Checkpoint", C),
            new PaletteTile(TileCatalog.FinishId, "Finish line", C), new PaletteTile(TileCatalog.PortalId, "Portal", C),

            new PaletteTile(9, "Crusher", O), new PaletteTile(10, "Crusher (safe)", O),
            new PaletteTile(13, "Riser", O),
            new PaletteTile(TileCatalog.RiserVariantId, "Riser variant 1", O),
            new PaletteTile(TileCatalog.RiserVariantId + 1, "Riser variant 2", O),
            new PaletteTile(TileCatalog.RiserVariantId + 2, "Riser variant 3", O),
            new PaletteTile(21, "Tree", O),
            new PaletteTile(TileCatalog.TreeVariantId, "Tree variant 1", O),
            new PaletteTile(TileCatalog.TreeVariantId + 1, "Tree variant 2", O),
            new PaletteTile(TileCatalog.TreeVariantId + 2, "Tree variant 3", O),
            new PaletteTile(33, "Palm", O),
            new PaletteTile(25, "Roller", O), new PaletteTile(26, "Roller", O),
            new PaletteTile(29, "Floater (ground)", O), new PaletteTile(31, "Floater (air)", O),
            // Binaries (Adrian 2026-10-21): each kind its own tiles, the arrow on the button = the side its arm reaches.
            new PaletteTile(61, "Mini", O), new PaletteTile(62, "Mini", O),
            new PaletteTile(65, "Slasher", O), new PaletteTile(66, "Slasher", O),
            new PaletteTile(TileCatalog.HammerFirst, "Hammer", O), new PaletteTile(TileCatalog.HammerFirst + 1, "Hammer", O),
            new PaletteTile(73, "Mini offset", O), new PaletteTile(74, "Mini offset", O),
            new PaletteTile(77, "Slasher offset", O), new PaletteTile(78, "Slasher offset", O),
            new PaletteTile(TileCatalog.HammerFirst + 2, "Hammer offset", O), new PaletteTile(TileCatalog.HammerFirst + 3, "Hammer offset", O),
            new PaletteTile(85, "Pillar 1", O), new PaletteTile(86, "Pillar 2", O), new PaletteTile(87, "Pillar 3", O), new PaletteTile(88, "Pillar 4", O),
            new PaletteTile(89, "Pillar safe 1", O), new PaletteTile(90, "Pillar safe 2", O),
            new PaletteTile(91, "Pillar safe 3", O), new PaletteTile(92, "Pillar safe 4", O),
            new PaletteTile(105, "Pillar laser", O),
            new PaletteTile(97, "Spotlight", O),
            new PaletteTile(101, "Laser", O),
            new PaletteTile(PartArgs.CustomMoveBlockId, "Custom move block", O),
            new PaletteTile(PartArgs.TriggerObjectId, "Trigger Object", O),
            new PaletteTile(NewCustomTool, "New custom", O), // the custom obstacles follow it (LevelEditor adds them)

            new PaletteTile(ThemeTool, "Theme trigger", T),
            new PaletteTile(CameraTool, "Camera trigger", T),
            new PaletteTile(FilterTool, "Screen FX", T),
            new PaletteTile(StartTool, "Start Pos", T),
            new PaletteTile(RowInsertTool, "Row +", T),
            new PaletteTile(RowDeleteTool, "Row -", T),
            new PaletteTile(MarqueeTool, "Marquee", T),
        };

        /// <summary>
        /// The two erasers, always at the bottom of the palette: Erase Tile empties the cell (floor and all on it);
        /// Erase Object takes everything but the floor (obstacle, pickup, props).
        /// </summary>
        public static readonly PaletteTile[] Erasers =
        {
            new PaletteTile(0, "Erase Tile", F),
            new PaletteTile(EraseObjectTool, "Erase Object", O),
            new PaletteTile(EditTool, "Edit", T),
            new PaletteTile(CopyTool, "Copy", T),
        };

        /// <summary>Not tiles: Edit shows the cell you click with all its ARGS (and edits them for that cell only);
        /// Copy takes the cell you click (tile, object, pickup and their ARGS) and then paints copies of it (PasteTool).</summary>
        public const int EditTool = -12;
        public const int CopyTool = -13;
        public const int PasteTool = -14;
        public static readonly Color EditColor = new Color32(0x4a, 0x9a, 0xff, 0xff);
        public static readonly Color CopyColor = new Color32(0x2f, 0xc4, 0xb0, 0xff);
        public static bool IsBottomTool(int id) => id == 0 || id == EraseObjectTool || id == EditTool || id == CopyTool;

        /// <summary>Not tiles: Row + puts an empty row in at the row you click (it and the rows after it move on);
        /// Row - deletes the row you click (the rows after it move back).</summary>
        public const int RowInsertTool = -9;
        public const int RowDeleteTool = -10;
        public static bool IsRowTool(int id) => id == RowInsertTool || id == RowDeleteTool;
        static readonly Color RowToolColor = new Color32(0x9a, 0xa4, 0xb8, 0xff);

        /// <summary>Not a tile: Marquee picks rows (click, drag, Shift+click) to copy, cut or delete; after Copy a click pastes them.</summary>
        public const int MarqueeTool = -15;
        public static readonly Color MarqueeColor = new Color32(0x3c, 0xd6, 0xff, 0xff);

        /// <summary>Not a tile: the + button at the end of the Object tab, opens a new Custom Obstacle.</summary>
        public const int NewCustomTool = -8;
        public static readonly Color CustomColor = new Color32(0xc2, 0x3a, 0x6e, 0xff);

        /// <summary>Not a tile: takes the props off the cells you paint (Props tab; lane 1 / 5 also clear that side).</summary>
        public const int ErasePropTool = -7;
        /// <summary>Not tiles: Props tab buttons are -100, -101, ... = the look's Props list element 0, 1, ...</summary>
        public const int PropToolFirst = -100;
        public static bool IsPropTool(int id) => id <= PropToolFirst;
        public static int PropIndex(int id) => PropToolFirst - id;
        public static readonly Color PropColor = new Color32(0xc9, 0x8b, 0x4a, 0xff);

        /// <summary>Not tiles: Erase collect clears the Collect layer; Erase Object (the palette's) everything but the floor.</summary>
        public const int EraseCollectTool = -5;
        public const int EraseObjectTool = -6;

        /// <summary>The layer a palette entry paints on (tools count as the layer of their tab).</summary>
        public static TileLayer LayerOf(int id) =>
            id == EraseCollectTool ? TileLayer.Collect : id == EraseObjectTool ? TileLayer.Object : TileCatalog.LayerOf(id);

        /// <summary>
        /// Not offered any more, but old levels keep them and the grid still names them:
        /// edge floors (69-72, 81-84, 93-96, 107, 108), because Tile Types give every floor its shape from its
        /// neighbours (they play as plain floor); and the old movers 37-48 / 57-60, because Mover 1-3 (yellow),
        /// Auto Mover 1-3 (red) and the black arrows 186-189 do it all (the old ones still move as before).
        /// </summary>
        static readonly System.Collections.Generic.HashSet<int> NotInPalette = new System.Collections.Generic.HashSet<int>
        {
            69, 70, 71, 72, 81, 82, 83, 84, 93, 94, 95, 96, 107, 108,
            37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 57, 58, 59, 60,
        };
        public static bool InPalette(int id) => !NotInPalette.Contains(id);

        /// <summary>The page a group's buttons are on: Fragile and Jump share the Floor page ("Tiles").</summary>
        public static TileGroup PageOf(TileGroup group) => group == TileGroup.Fragile || group == TileGroup.Jump ? TileGroup.Floor : group;

        /// <summary>The Tiles tab's pages; Collect, Object and Triggers are tabs of their own.</summary>
        public static bool IsTilesPage(TileGroup group) => group <= TileGroup.Animated;

        /// <summary>The tab an ID is offered on (for middle click picking).</summary>
        public static TileGroup GroupOf(int id) => TryGet(id, out var t) ? t.group
            : TileCatalog.LayerOf(id) == TileLayer.Object ? TileGroup.Obstacle
            : TileCatalog.LayerOf(id) == TileLayer.Collect ? TileGroup.Collect : TileGroup.Floor;

        /// <summary>Not a tile: the Checkpoint tool puts a checkpoint on the row it paints, or takes it off.</summary>
        public const int CheckpointTool = -1;
        public static readonly Color CheckpointColor = new Color32(0x6c, 0xe5, 0xff, 0xff);
        /// <summary>Not a tile: the Start Pos tool sets the cell where Test starts (editor only).</summary>
        public const int StartTool = -2;
        public static readonly Color StartColor = new Color32(0x4c, 0xe0, 0x7a, 0xff);
        /// <summary>Not tiles: a click on a row adds (or opens) a theme / camera trigger there.</summary>
        public const int ThemeTool = -3;
        public const int CameraTool = -4;
        public static readonly Color ThemeColor = new Color32(0xd9, 0x4c, 0xff, 0xff);
        public static readonly Color CameraColor = new Color32(0x40, 0xf2, 0xcc, 0xff);
        /// <summary>Not a tile: a click on a row adds (or opens) a screen filter trigger there (Glitch, Grayscale...).</summary>
        public const int FilterTool = -11;
        public static readonly Color FilterColor = new Color32(0xff, 0x9f, 0x1c, 0xff);
        public static readonly Color CrownColor = new Color32(0xff, 0xd2, 0x4a, 0xff);
        static readonly Color MarkerColor = new Color32(0x8a, 0x8d, 0x90, 0xff); // 109-116, grey like the web editor

        public static readonly Color EmptyColor = new Color(0.12f, 0.1f, 0.13f);
        static readonly Color UnknownColor = new Color(0.54f, 0.56f, 0.56f);

        public static bool TryGet(int id, out PaletteTile tile)
        {
            foreach (var t in All)
            {
                if (t.id != id) continue;
                tile = t;
                return true;
            }
            foreach (var t in Erasers)
            {
                if (t.id != id) continue;
                tile = t;
                return true;
            }
            tile = default;
            return false;
        }

        /// <summary>Name for any ID; IDs that are not in the palette (bucket markers) show as "Tile 121". Left / right ones end in an arrow.</summary>
        public static string Name(int id)
        {
            if (CustomObstacle.IsCustomId(id)) return CustomObstacles.TryGet(id, out var c) ? c.name : $"Custom {id} (missing)";
            string name = TryGet(id, out var t) ? t.name : "Tile " + id;
            int side = SideOf(id);
            return side < 0 ? name + " " + LeftArrow : side > 0 ? name + " " + RightArrow : name;
        }

        public const string LeftArrow = "\u25C4", RightArrow = "\u25BA"; // ◄ ►

        /// <summary>-1 for tiles that go or reach left (binary arm, roller, slide), 1 for right, 0 for the others.</summary>
        public static int SideOf(int id)
        {
            if (id <= 0 || CustomObstacle.IsCustomId(id) || TileCatalog.MoverGroupOf(id, out _) > 0 || TileCatalog.ArrowOf(id, out _)) return 0;
            if (id == 174) return 1;  // Tile slide from left: comes from the left, moving right
            if (id == 175) return -1;
            var spec = TileCatalog.Decode(id);
            switch (spec.item)
            {
                case ItemKind.BinaryLeft:
                case ItemKind.RollerLeft: return -1;
                case ItemKind.BinaryRight:
                case ItemKind.RollerRight: return 1;
            }
            if (TileCatalog.LayerOf(id) != TileLayer.Tile) return 0;
            return spec.floor == FloorKind.SlideLeft ? -1 : spec.floor == FloorKind.SlideRight ? 1 : 0;
        }

        /// <summary>Text on a tile button without an icon: the ID, X for the eraser, CP for the Checkpoint tool.</summary>
        public static string Label(int id) => id == 0 || id == EraseCollectTool || id == EraseObjectTool || id == ErasePropTool ? "X" : IsPropTool(id) ? "P" : id == NewCustomTool ? "+" : id == CheckpointTool ? "CP" : id == StartTool ? "S"
            : id == EditTool ? "ED" : id == CopyTool ? "CO" : id == PasteTool ? "V"
            : id == ThemeTool ? "TH" : id == CameraTool ? "CAM" : id == FilterTool ? "FX" : id == RowInsertTool ? "R+" : id == RowDeleteTool ? "R-" : id == MarqueeTool ? "MQ" : PartArgs.KindOf(id) == PartKind.Shuffle ? "?"
            : NumberOrArrow(id) ?? id.ToString();

        /// <summary>"1".."3" on numbered platforms (like the fan editor), ^ v &lt; &gt; on arrows; null for other IDs.</summary>
        public static string NumberOrArrow(int id)
        {
            int number = TileCatalog.MoverGroupOf(id, out _);
            if (number > 0) return number.ToString();
            return TileCatalog.ArrowOf(id, out _) ? ArrowGlyphs[id - TileCatalog.MoverArrowFirst] : null;
        }

        static readonly string[] ArrowGlyphs = { "^", "v", "<", ">" };

        public static string GroupName(TileGroup group) => group switch
        {
            TileGroup.Floor => "Tiles",
            TileGroup.Fragile => "Fragile",
            TileGroup.Jump => "Jump",
            TileGroup.Animated => "Animated",
            TileGroup.Collect => "Collect",
            TileGroup.Obstacle => "Object",
            TileGroup.Triggers => "Triggers",
            TileGroup.Props => "Props",
            TileGroup.Collection => "Collection",
            _ => "Moving",
        };

        public static Color ColorOf(TileGroup group) => group switch
        {
            TileGroup.Floor => new Color32(0x5b, 0xbf, 0x5f, 0xff),
            TileGroup.Fragile => new Color32(0x93, 0xcb, 0xe6, 0xff),
            TileGroup.Jump => new Color32(0xf0, 0xc6, 0x2a, 0xff),
            TileGroup.Collect => new Color32(0xee, 0x7a, 0x1e, 0xff),
            TileGroup.Obstacle => new Color32(0xd4, 0x48, 0x3f, 0xff),
            TileGroup.Animated => AnimatedFloorColor,
            TileGroup.Props => PropColor,
            TileGroup.Collection => CrownColor,
            _ => new Color32(0x3b, 0x7f, 0xe0, 0xff),
        };

        static readonly Color TouchMoverColor = new Color32(0x9b, 0x59, 0xd0, 0xff); // violet, like the platform in game
        static readonly Color AutoMoverColor = new Color32(0x22, 0xa8, 0x8c, 0xff);  // teal-green
        static readonly Color NumberedMoverColor = new Color32(0xf2, 0xc2, 0x30, 0xff); // Mover 1-3: yellow
        static readonly Color NumberedAutoColor = new Color32(0xe0, 0x48, 0x48, 0xff);  // Auto Mover 1-3: red
        static readonly Color ArrowColor = new Color32(0x2b, 0x2f, 0x45, 0xff); // dark, so it shows on yellow and red platforms
        static readonly Color AnimatedFloorColor = new Color32(0x6e, 0xc9, 0xb0, 0xff); // floor that moves (174-177, 194-199)

        // Fragile 0, 1, 2, checker, row, column: each ID its own colour, like the game's M_Fragile... materials.
        static readonly Color[] FragileColors =
        {
            new Color32(0xf2, 0x9a, 0x5c, 0xff), // 3 orange
            new Color32(0x6f, 0xa8, 0xff, 0xff), // 4 blue
            new Color32(0x7f, 0xdc, 0x8f, 0xff), // 5 green
            new Color32(0xef, 0x9f, 0xd6, 0xff), // 6 pink
            new Color32(0xe8, 0xdc, 0x7a, 0xff), // 7 yellow
            new Color32(0xb9, 0xa0, 0xf0, 0xff), // 8 violet
        };

        public static Color ColorOf(int id)
        {
            if (id == 0 || id == EraseCollectTool || id == EraseObjectTool || id == ErasePropTool) return EmptyColor;
            if (IsPropTool(id)) return PropColor;
            if (id == NewCustomTool) return ColorOf(TileGroup.Obstacle);
            if (CustomObstacle.IsCustomId(id)) return CustomColor;
            if (id == CheckpointTool) return CheckpointColor;
            if (id == StartTool) return StartColor;
            if (id == ThemeTool) return ThemeColor;
            if (id == CameraTool) return CameraColor;
            if (id == FilterTool) return FilterColor;
            if (IsRowTool(id)) return RowToolColor;
            if (id == MarqueeTool) return MarqueeColor;
            if (id == EditTool) return EditColor;
            if (id == CopyTool || id == PasteTool) return CopyColor;
            if (id == TileCatalog.CrownId) return CrownColor;
            if (id >= 109 && id <= 116) return MarkerColor;
            switch (TileCatalog.MoverGroupOf(id, out var groupKind) > 0 ? groupKind : MoverKind.None)
            {
                case MoverKind.Touch: return NumberedMoverColor;
                case MoverKind.Auto: return NumberedAutoColor;
            }
            if (TileCatalog.ArrowOf(id, out _)) return ArrowColor;
            switch (PartArgs.KindOf(id))
            {
                case PartKind.None: break;
                case PartKind.GemRise:
                case PartKind.GemDrop: return ColorOf(TileGroup.Collect);
                case PartKind.CrownRise:
                case PartKind.CrownDrop: return CrownColor;
                default:
                    var kind = PartArgs.KindOf(id);
                    return PartArgs.IsAnimatedFloor(kind) ? AnimatedFloorColor : PartArgs.ColorOf(kind);
            }
            switch (TileCatalog.MoverOf(id, out _))
            {
                case MoverKind.Touch: return TouchMoverColor;
                case MoverKind.Auto: return AutoMoverColor;
            }
            if (id >= 3 && id <= 8) return FragileColors[id - 3];
            return TryGet(id, out var t) ? ColorOf(t.group) : UnknownColor;
        }

        /// <summary>Dark text on light colours, white text on dark ones, so labels stay readable.</summary>
        public static Color TextColorOf(int id) => TextColorOn(ColorOf(id));

        public static Color TextColorOn(Color background)
        {
            var c = background.linear;
            float luminance = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
            return luminance > 0.2f ? new Color(0.1f, 0.1f, 0.13f) : Color.white;
        }

        /// <summary>One line of help for tiles that do something special, shown under the selected tile.</summary>
        public static string Hint(int id)
        {
            if (id == 0) return "Erase Tile: empties the cell (floor, and the object and pickup on it). Right click in the Tiles tab does the same.";
            if (id == RowInsertTool) return "Click a row: an empty row goes in there; that row and every row after it (with triggers, checkpoints, props, ARGS) move one row on.";
            if (id == RowDeleteTool) return "Click a row: it is deleted with everything on it; the rows after it move one row back. Undo brings it back.";
            if (id == MarqueeTool) return "Marquee: click or drag over rows (Shift+click adds up to that row), then Copy, Cut or Delete. After Copy or Cut, click any row to paste them there.";
            if (id == EraseCollectTool) return "Takes the pickup off the cells you paint; the floor and object stay. Right click in this tab does the same.";
            if (id == EraseObjectTool) return "Erase Object: takes everything but the floor off the cells you paint (obstacle, pickup, props). Right click in the other tabs does the same.";
            if (id == ErasePropTool) return "Takes the props off the cells you paint. Lane 1 and 5 also clear the props beside the track on that side. Right click in this tab does the same.";
            if (id == NewCustomTool) return "Makes a new Custom Obstacle: your own model, hitbox, event and animation.";
            if (id == EditTool) return "Click a cell: its tile, object and pickup with all their ARGS. ARGS there changes only that cell; Pick takes it as the brush.";
            if (id == CopyTool) return "Click a cell: it is copied (tile, object, pickup and their ARGS) and selected. Then every click paints the same cell.";
            if (id == PasteTool) return "Paints the copied cell: tile, object, pickup and their ARGS, exactly as copied. Copy again to take another cell.";
            if (CustomObstacle.IsCustomId(id))
                return CustomObstacles.TryGet(id, out _) ? "Custom Obstacle. EDIT (left of the swatch) changes it; every cell with this ID changes too. " +
                                                           "Edit tool (E) > ARGS on a cell: Group = it waits for the CustomTriggerTiles with that number."
                                                         : "Custom Obstacle that is not loaded: its .rsrt is missing. Import it, or erase these cells.";
            if (IsPropTool(id)) return "Decoration, no collider. Click cells to place it. Place / Turn / Size (top of this tab) set the next ones: Place Left / Right / Both = beside the track.";
            if (id == ThemeTool) return "Click a row: a theme trigger there (environment Day / Sunset / Night, look, fade rows). Click it again to edit or delete.";
            if (id == CameraTool) return "Click a row: a camera trigger there (position, angle, offset rows, curve, presets). Click it again to edit or delete.";
            if (id == FilterTool) return "Click a row: a screen effect there (Glitch, Grayscale, Negative, Chromatic, Scan Lines, Hue Shift) with strength, fade and stay rows. Click it again to edit or delete. Effects add up.";
            if (id == StartTool) return "Test only: click any cell and Test (T) starts the ball there. Click it again to take it off (Test then starts at the bottom row on screen). The game itself always starts at row 0.";
            if (id == CheckpointTool) return "Click a row to put a checkpoint on it, click again to take it off. After dying, the game continues from the last one.";
            if (id == TileCatalog.CrownId) return "Crown on the floor; the ball takes it by touching it. A level has 3.";
            if (id == TileCatalog.FinishId) return "Finish line mesh on the last row made only of 110. 110 elsewhere = start of a moving-platform segment.";
            if (id == TileCatalog.PortalId) return "Portal mesh at the end of the level (centred on the 113s of the row).";
            if (PartArgs.IsPart(id)) return PartArgs.Help(PartArgs.KindOf(id)) + " EDIT ARGS (left of the swatch) sets what the tiles you paint get.";
            if (TileCatalog.MoverGroupOf(id, out var group) > 0)
                return (group == MoverKind.Touch ? "Mover" : "Auto Mover") + " platform " + (id - (group == MoverKind.Touch ? TileCatalog.MoverGroupFirst : TileCatalog.AutoGroupFirst) + 1) +
                       ": touching cells with the same number are one platform; put an Arrow on it. " +
                       (group == MoverKind.Touch ? "It slides when the ball rolls onto the arrow." : "It slides when a moving platform ends up next to it.");
            if (TileCatalog.ArrowOf(id, out _))
                return "Arrow (Object layer): the platform under it slides one tile this way. Works on Mover and Auto Mover numbers; on plain floor it acts as an auto mover.";
            switch (TileCatalog.MoverOf(id, out _))
            {
                case MoverKind.Touch:
                    return "When the ball rolls onto the arrow, the platform it is on slides one tile that way.";
                case MoverKind.Auto:
                    return id >= 45 && id <= 48
                        ? "Moves alone, one tile, when a moving platform ends up next to it."
                        : "Slides its platform one tile when a moving platform ends up next to it.";
            }
            if (TileCatalog.EdgesOf(id) != TileEdges.None)
                return "Edges stop a moving platform from taking the tile next to them.";
            if (id == 19 || id == 20) return "Slides into its lane as the ball comes.";
            if (id >= TileCatalog.RiserVariantId && id < TileCatalog.RiserVariantId + TileCatalog.VariantCount)
                return "Riser with the theme's Riser Variants element " + (id - TileCatalog.RiserVariantId) + " (empty = Riser Model).";
            if (id >= TileCatalog.TreeVariantId && id < TileCatalog.TreeVariantId + TileCatalog.VariantCount)
                return "Tree with the theme's Tree Variants element " + (id - TileCatalog.TreeVariantId) + " (empty = Tree Model).";
            if (TileCatalog.Decode(id).item == ItemKind.BinaryLeft || TileCatalog.Decode(id).item == ItemKind.BinaryRight)
            {
                var b = TileCatalog.Decode(id);
                string kind = b.param == TileCatalog.BinaryHammer ? "Hammer: pounds down and back up (the theme's Binary Hammer Model)"
                            : b.param == TileCatalog.BinarySlasher ? "Slasher: spins its long arm (the theme's Binary Slasher Model)"
                            : "Mini: a small spinner (the theme's Binary Mini Model)";
                return kind + ". The arrow is the side its arm makes deadly" + (b.offset ? "; offset = its body stands one lane the other way." : ".") + " Jumping over it is safe.";
            }
            if (id >= 85 && id <= 88) return "Pillar of " + (id - 84) + " block(s): slams up out of the floor as the ball comes, sinks after.";
            if (id >= 89 && id <= 92) return "Safe pillar " + (id - 88) + ": rises with a gap at floor level; the ball rolls under it.";
            if (id > 0 && TileCatalog.LayerOf(id) != TileLayer.Tile) return "Stands on the cell's floor (fragile, slider, jump...); an empty cell gets plain floor.";
            return "";
        }
    }
}
