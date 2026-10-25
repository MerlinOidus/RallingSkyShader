using System;
using System.Collections.Generic;
using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// The RTX ON "parts": power-ups (160-168), animated tiles (170-177, 194-199, 145-149, 178, 179), custom moving
    /// tiles / blocks (157, 158), trigger tiles (159), trigger objects (169) and Custom Obstacles (200-999, Group only). Each has its own settings (ARGS).
    /// </summary>
    public enum PartKind
    {
        None,
        // Power-ups: the ball takes them by touching them.
        FlyJump, ShortJump, Bounce, Switch, Invisible, SpeedUp, SpeedDown, HideObjects, Dialogue,
        // Animated tiles: they move as the ball comes.
        GemRise, GemDrop, CrownRise, CrownDrop, SlideFromLeft, SlideFromRight, RiseUp, SinkDown,
        // More animated floor tiles (194-199).
        Wave, DropIn, SpinIn, Shuffle, Extend, FallAway,
        // Even more animated floor tiles (145-149, 178, 179).
        FlipIn, PopIn, Drawbridge, ScrewUp, RollIn, FlyAway, BlinkIn,
        // Own animation (Move X / Y / Z, Spin, Seconds or with the ball, Loop) started by the ball or by a trigger group.
        CustomMoveTile, CustomMoveBlock,
        // CustomTriggerTiles: the ball touches it and every part with the same Group (EnemyGroupID) starts.
        TriggerTile,
        // Trigger Object: one object with one animation; it stands still (or plays its idle frames) until its group is
        // triggered, then the animation plays and its hitbox goes with it.
        TriggerObject,
        // A Custom Obstacle (200-999) on a cell: its ARGS (Group etc.) make it wait for a trigger like a Trigger Object.
        CustomObject,
    }

    /// <summary>
    /// The settings of one part on one cell (the level editor's ARGS-Setting window), saved in level.json "parts".
    /// A part without an entry uses <see cref="Defaults"/>. Which fields mean something depends on the kind
    /// (<see cref="Uses"/>); the others are ignored.
    /// </summary>
    [Serializable]
    public class PartArgs
    {
        public int row;
        public int lane;
        [Tooltip("The tile ID these settings were made for; another tile painted on the cell gets its own defaults.")]
        public int id;
        [Tooltip("Rows: jump length, how long Bounce goes on, rows Switch mirrors, rows a speed change lasts.")]
        public float distance;
        [Tooltip("Speed Up / Speed Down: the level's speed is multiplied by this (2 = twice as fast, 0.5 = half).")]
        public float multiple = 1f;
        [Tooltip("Speed Up / Speed Down: the music timing counts the speed change, so the ball stays on the music " +
                 "(Start Pos and checkpoints after it start at the right time). Off: the ball gets ahead of (or behind) the music.")]
        public bool musicOffset = true;
        [Tooltip("Jump height; Bounce: height of a 4-row hop; flying gem / crown: top height; Rise Up / Sink Down: depth.")]
        public float height;
        [Tooltip("Bounce: the hops in rows, repeated (\"3,4,3\").")]
        public string pattern = "";
        [Tooltip("Invisible: seconds the ball is hidden. Hide Objects / Dialogue: seconds it lasts (1-4).")]
        public float seconds;
        [Tooltip("Dialogue: the words in the bubble above the ball.")]
        public string text = "";
        [Tooltip("Switch: rows after the switch tile where the mirrored part starts. Animated tiles: rows before the ball " +
                 "arrives when the animation starts.")]
        public float startRows;
        [Tooltip("EnemyGroupID (1-99). Trigger tile: the number it fires. Custom move / Trigger Object / Custom Obstacle: the trigger number it waits for; " +
                 "custom moves with the same Group (and no trigger tile for it) start together. 0 = none.")]
        public int group;
        [Tooltip("Custom move: how far it goes, in tiles (X = right, Y = up, Z = forward, the way the ball rolls).")]
        public float moveX, moveY, moveZ;
        [Tooltip("Custom move: degrees it turns around its up axis on the way.")]
        public float spin;
        [Tooltip("Custom move: on = it moves as fast as the ball rolls (Z 20 = 20 rows next to the ball); off = it takes Seconds.")]
        public bool withBall;
        [Tooltip("Custom move: goes there and back again and again. Trigger Object / Custom Obstacle: after the trigger its animation plays again and again.")]
        public bool loop;
        [Tooltip("Trigger Object / Custom Obstacle: the clip's first Frames (1-60, 60 a second) play while it waits; 1 = it stands still. " +
                 "After the trigger the rest of the clip plays.")]
        public int frames = DefaultFrames;
        [Tooltip("Old Gate setting, no longer used (kept so old level files load).")]
        public bool startUp = true;
        [Tooltip("Trigger tile: fires when the ball crosses its row in any lane (or in the air), not only when it rolls onto it.")]
        public bool wholeRow;
        [Tooltip("Custom move: on = it stays after the ball has passed; off = it disappears Vanish seconds after the ball passed it.")]
        public bool keep = true;
        [Tooltip("Custom move with Keep off: seconds after the ball passed it before it disappears (1-3).")]
        public float vanish = 1.5f;
        [Tooltip("Custom move with With ball off: tiles a second (0 = it takes Seconds instead).")]
        public float speed;
        [Tooltip("Custom Move Block: on = deadly; off = safe (the ball goes through it) and the gem, crown or power-up on its cell rides on top of it.")]
        public bool kill = true;

        public const int PowerUpFirst = 160;
        public const int PowerUpCount = 9;
        public const int AnimFirst = 170;
        public const int AnimCount = 8;
        /// <summary>The second set of animated floor tiles: 194 Wave ... 199 Fall Away.</summary>
        public const int Anim2First = 194;
        public const int Anim2Count = 6;

        public const int CustomMoveTileId = 157, CustomMoveBlockId = 158, TriggerTileId = 159, TriggerObjectId = 169;
        public const int DefaultFrames = 30, MaxGroup = 99;

        /// <summary>Parts whose IDs are not in a run: the third set of animated tiles and the custom / trigger parts.</summary>
        static readonly int[] ExtraIds = { 145, 146, 147, 148, 149, 178, 179, CustomMoveTileId, CustomMoveBlockId, TriggerTileId, TriggerObjectId };
        static readonly PartKind[] ExtraKinds =
        {
            PartKind.FlipIn, PartKind.PopIn, PartKind.Drawbridge, PartKind.ScrewUp, PartKind.RollIn, PartKind.FlyAway, PartKind.BlinkIn,
            PartKind.CustomMoveTile, PartKind.CustomMoveBlock, PartKind.TriggerTile, PartKind.TriggerObject,
        };

        public static PartKind KindOf(int id)
        {
            if (CustomObstacle.IsCustomId(id)) return PartKind.CustomObject;
            if (id >= PowerUpFirst && id < PowerUpFirst + PowerUpCount) return PartKind.FlyJump + (id - PowerUpFirst);
            if (id >= AnimFirst && id < AnimFirst + AnimCount) return PartKind.GemRise + (id - AnimFirst);
            if (id >= Anim2First && id < Anim2First + Anim2Count) return PartKind.Wave + (id - Anim2First);
            for (int i = 0; i < ExtraIds.Length; i++) if (ExtraIds[i] == id) return ExtraKinds[i];
            return PartKind.None;
        }

        public static int IdOf(PartKind kind)
        {
            if (kind >= PartKind.FlyJump && kind <= PartKind.Dialogue) return PowerUpFirst + (kind - PartKind.FlyJump);
            if (kind >= PartKind.GemRise && kind <= PartKind.SinkDown) return AnimFirst + (kind - PartKind.GemRise);
            if (kind >= PartKind.Wave && kind <= PartKind.FallAway) return Anim2First + (kind - PartKind.Wave);
            for (int i = 0; i < ExtraKinds.Length; i++) if (ExtraKinds[i] == kind) return ExtraIds[i];
            return 0;
        }

        public static bool IsPart(int id) => KindOf(id) != PartKind.None;
        public static bool IsPowerUp(PartKind kind) => kind >= PartKind.FlyJump && kind <= PartKind.Dialogue;
        /// <summary>Floor tiles that move by themselves (174-177, 194-199, 145-149, 178, 179), as opposed to flying gems / crowns and power-ups.</summary>
        public static bool IsAnimatedFloor(PartKind kind) =>
            (kind >= PartKind.SlideFromLeft && kind <= PartKind.SinkDown) || (kind >= PartKind.Wave && kind <= PartKind.FallAway) ||
            (kind >= PartKind.FlipIn && kind <= PartKind.BlinkIn);
        public static bool IsCustomMove(PartKind kind) => kind == PartKind.CustomMoveTile || kind == PartKind.CustomMoveBlock;
        /// <summary>Parts with a Group (EnemyGroupID): trigger tiles, custom moves and gates.</summary>
        public static bool HasGroup(PartKind kind) => IsCustomMove(kind) || kind == PartKind.TriggerTile || IsTriggerObject(kind);
        /// <summary>Trigger Object (169) and Custom Obstacles: one animation that waits for its Group's trigger.</summary>
        public static bool IsTriggerObject(PartKind kind) => kind == PartKind.TriggerObject || kind == PartKind.CustomObject;
        public static bool IsSpeed(PartKind kind) => kind == PartKind.SpeedUp || kind == PartKind.SpeedDown;

        /// <summary>
        /// Switch, Speed Up, Speed Down, Hide Objects and Dialogue are trigger lines: they go off when the ball crosses
        /// their row, in any lane, rolling or in the air, so they can't be missed. The other power-ups are taken by touching them.
        /// </summary>
        public static bool IsRowTrigger(PartKind kind) =>
            kind == PartKind.Switch || IsSpeed(kind) || kind == PartKind.HideObjects || kind == PartKind.Dialogue;

        /// <summary>Hide Objects and Dialogue last 1 to 4 seconds.</summary>
        public const float MinShortSeconds = 1f, MaxShortSeconds = 4f;
        public static bool HasShortSeconds(PartKind kind) => kind == PartKind.HideObjects || kind == PartKind.Dialogue;
        public const string DefaultDialogue = "WHO HE?";
        public const int MaxDialogueLength = 60;

        /// <summary>Switch draws its line in the middle of the track, whatever lane its tile is in.</summary>
        public static bool CentredLine(PartKind kind) => kind == PartKind.Switch;

        public PartKind Kind => KindOf(id);

        /// <summary>The settings a newly painted part starts with.</summary>
        public static PartArgs Defaults(int id, int row = 0, int lane = 0)
        {
            var a = new PartArgs { id = id, row = row, lane = lane };
            switch (KindOf(id))
            {
                case PartKind.FlyJump: a.distance = 6f; a.height = 2.4f; break;
                case PartKind.ShortJump: a.distance = 2f; a.height = 0.9f; break;
                case PartKind.Bounce: a.distance = 20f; a.height = 1.6f; a.pattern = "3,4,3"; break;
                case PartKind.Switch: a.distance = 15f; a.startRows = 2f; break;
                case PartKind.Invisible: a.seconds = 3f; break;
                case PartKind.SpeedUp: a.distance = 30f; a.multiple = 2f; break;
                case PartKind.SpeedDown: a.distance = 30f; a.multiple = 0.5f; break;
                case PartKind.HideObjects: a.distance = 20f; a.seconds = 2f; break;
                case PartKind.Dialogue: a.seconds = 2f; a.text = DefaultDialogue; break;
                case PartKind.GemRise:
                case PartKind.CrownRise: a.startRows = 8f; a.height = 2.2f; break;
                case PartKind.GemDrop:
                case PartKind.CrownDrop: a.startRows = 8f; a.height = 2.6f; break;
                case PartKind.SlideFromLeft:
                case PartKind.SlideFromRight: a.startRows = 8f; break;
                case PartKind.RiseUp: a.startRows = 10f; a.height = 3f; break;
                case PartKind.SinkDown: a.startRows = 4f; a.height = 4f; break;
                case PartKind.Wave: a.startRows = 3f; a.height = 0.5f; break;
                case PartKind.DropIn: a.startRows = 10f; a.height = 6f; break;
                case PartKind.SpinIn: a.startRows = 8f; break;
                case PartKind.Shuffle: a.startRows = 8f; break;
                case PartKind.Extend: a.startRows = 6f; break;
                case PartKind.FallAway: a.startRows = 1.5f; a.height = 8f; break;
                case PartKind.FlipIn:
                case PartKind.Drawbridge:
                case PartKind.RollIn:
                case PartKind.BlinkIn: a.startRows = 8f; break;
                case PartKind.PopIn: a.startRows = 6f; break;
                case PartKind.ScrewUp: a.startRows = 8f; a.height = 3f; break;
                case PartKind.FlyAway: a.startRows = 1.5f; a.height = 8f; break;
                // Adrian's example: Z 20 next to the ball (it rides along with the ball from the moment it reaches it).
                case PartKind.CustomMoveTile: a.moveZ = 20f; a.withBall = true; a.seconds = 3f; a.startRows = 0f; break;
                case PartKind.CustomMoveBlock: a.moveX = 2f; a.seconds = 1.5f; a.loop = true; a.startRows = 10f; break;
                case PartKind.TriggerTile: a.group = 1; break;
                // Seconds 0 = the clip at the speed it was made; Frames 1 = it stands still until the trigger.
                case PartKind.TriggerObject: a.group = 1; a.seconds = 0f; a.height = 1.2f; a.frames = 1; break;
                case PartKind.CustomObject: a.group = 0; a.seconds = 0f; a.frames = 1; a.startRows = TrackBehaviour.WakeRows; break;
            }
            return a;
        }

        public PartArgs Clone() => (PartArgs)MemberwiseClone();

        /// <summary>Takes the settings (not the cell or tile ID) of another part: the editor's brush stamping a cell.</summary>
        public void CopyValuesFrom(PartArgs other)
        {
            distance = other.distance;
            multiple = other.multiple;
            musicOffset = other.musicOffset;
            height = other.height;
            pattern = other.pattern;
            seconds = other.seconds;
            startRows = other.startRows;
            text = other.text;
            group = other.group;
            moveX = other.moveX;
            moveY = other.moveY;
            moveZ = other.moveZ;
            spin = other.spin;
            withBall = other.withBall;
            loop = other.loop;
            frames = other.frames;
            startUp = other.startUp;
            wholeRow = other.wholeRow;
            keep = other.keep;
            vanish = other.vanish;
            speed = other.speed;
            kill = other.kill;
        }

        public bool SameValues(PartArgs other) =>
            distance == other.distance && multiple == other.multiple && musicOffset == other.musicOffset && height == other.height &&
            (pattern ?? "") == (other.pattern ?? "") && seconds == other.seconds && startRows == other.startRows &&
            (text ?? "") == (other.text ?? "") && group == other.group && moveX == other.moveX && moveY == other.moveY &&
            moveZ == other.moveZ && spin == other.spin && withBall == other.withBall && loop == other.loop && frames == other.frames &&
            startUp == other.startUp && wholeRow == other.wholeRow && keep == other.keep && vanish == other.vanish &&
            speed == other.speed && kill == other.kill;

        /// <summary>Puts sensible values in fields a hand-written entry left empty or out of range.</summary>
        public void Normalize()
        {
            var d = Defaults(id, row, lane);
            if (Uses(Field.Distance) && distance <= 0f) distance = d.distance;
            if (Uses(Field.Height) && height <= 0f) height = d.height;
            if (Uses(Field.Multiple) && multiple <= 0f) multiple = d.multiple;
            if (Uses(Field.Seconds) && seconds <= 0f && !IsTriggerObject(Kind)) seconds = d.seconds;
            seconds = Mathf.Max(0f, seconds);
            if (Uses(Field.StartRows) && (startRows < 0f || Kind == PartKind.CustomObject && startRows <= 0f)) startRows = d.startRows;
            if (Uses(Field.Pattern) && Hops().Count == 0) pattern = d.pattern;
            distance = Mathf.Clamp(distance, 0f, 500f);
            multiple = Mathf.Clamp(multiple, 0.1f, 5f);
            if (HasShortSeconds(Kind)) seconds = Mathf.Clamp(seconds, MinShortSeconds, MaxShortSeconds);
            text = (text ?? "").Trim();
            if (text.Length > MaxDialogueLength) text = text.Substring(0, MaxDialogueLength);
            if (Uses(Field.Text) && text.Length == 0) text = d.text;
            group = Mathf.Clamp(group, 0, MaxGroup);
            if (frames < 1) frames = DefaultFrames;
            frames = Mathf.Clamp(frames, 1, 60);
            moveX = Mathf.Clamp(moveX, -500f, 500f);
            moveY = Mathf.Clamp(moveY, -100f, 100f);
            moveZ = Mathf.Clamp(moveZ, -500f, 500f);
            spin = Mathf.Clamp(spin, -3600f, 3600f);
            vanish = Mathf.Clamp(vanish <= 0f ? 1.5f : vanish, 1f, 3f);
            speed = Mathf.Clamp(speed, 0f, 100f);
        }

        /// <summary>Custom move: the offset it ends at, in tiles.</summary>
        public Vector3 Move => new Vector3(moveX, moveY, moveZ);

        /// <summary>The settings fields; <see cref="Uses"/> says which a kind has.</summary>
        public enum Field
        {
            Distance, Height, Pattern, Multiple, MusicOffset, Seconds, StartRows, Text,
            Group, MoveX, MoveY, MoveZ, Spin, WithBall, Loop, Frames, StartUp, WholeRow, Keep, Vanish, Speed, Kill,
        }

        public bool Uses(Field f) => Uses(Kind, f);

        public static bool Uses(PartKind kind, Field f)
        {
            switch (kind)
            {
                case PartKind.FlyJump:
                case PartKind.ShortJump: return f == Field.Distance || f == Field.Height;
                case PartKind.Bounce: return f == Field.Distance || f == Field.Height || f == Field.Pattern;
                case PartKind.Switch: return f == Field.Distance || f == Field.StartRows;
                case PartKind.Invisible: return f == Field.Seconds;
                case PartKind.HideObjects: return f == Field.Distance || f == Field.Seconds;
                case PartKind.Dialogue: return f == Field.Seconds || f == Field.Text;
                case PartKind.SpeedUp:
                case PartKind.SpeedDown: return f == Field.Distance || f == Field.Multiple || f == Field.MusicOffset;
                case PartKind.SlideFromLeft:
                case PartKind.SlideFromRight:
                case PartKind.SpinIn:
                case PartKind.Shuffle:
                case PartKind.Extend:
                case PartKind.FlipIn:
                case PartKind.PopIn:
                case PartKind.Drawbridge:
                case PartKind.RollIn:
                case PartKind.BlinkIn: return f == Field.StartRows;
                case PartKind.ScrewUp:
                case PartKind.FlyAway: return f == Field.StartRows || f == Field.Height;
                case PartKind.CustomMoveTile:
                case PartKind.CustomMoveBlock:
                    return f == Field.MoveX || f == Field.MoveY || f == Field.MoveZ || f == Field.Spin || f == Field.WithBall ||
                           f == Field.Speed || f == Field.Seconds || f == Field.Loop || f == Field.StartRows || f == Field.Group ||
                           f == Field.Keep || f == Field.Vanish || f == Field.Kill && kind == PartKind.CustomMoveBlock;
                case PartKind.TriggerTile: return f == Field.Group || f == Field.WholeRow;
                case PartKind.TriggerObject:
                    return f == Field.Group || f == Field.Frames || f == Field.Loop || f == Field.Seconds || f == Field.Height;
                case PartKind.CustomObject: return f == Field.Group || f == Field.Frames || f == Field.Loop || f == Field.Seconds || f == Field.StartRows;
                case PartKind.None: return false;
                default: return f == Field.StartRows || f == Field.Height; // flying pickups, rising / sinking tiles
            }
        }

        /// <summary>Bounce hops in rows ("3,4,3" → 3, 4, 3); empty when the pattern has no positive number.</summary>
        public List<float> Hops()
        {
            var list = new List<float>();
            foreach (var part in (pattern ?? "").Split(new[] { ',', ' ', '-', ';' }, StringSplitOptions.RemoveEmptyEntries))
                if (float.TryParse(part, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) && v > 0.1f)
                    list.Add(Mathf.Min(v, 50f));
            return list;
        }

        /// <summary>Landing points of a bounce, in rows after the tile (the last one at or past Distance).</summary>
        public List<float> BounceLandings()
        {
            var list = new List<float>();
            var hops = Hops();
            if (hops.Count == 0) return list;
            float at = 0f;
            for (int i = 0; at < distance - 0.01f && list.Count < 200; i++)
            {
                at += hops[i % hops.Count];
                list.Add(at);
            }
            return list;
        }

        // ---------------- Editor lines ----------------

        public static readonly Color FlyJumpColor = new Color(0.25f, 0.62f, 1f);
        public static readonly Color ShortJumpColor = new Color(0.45f, 0.92f, 1f);
        public static readonly Color BounceColor = new Color(1f, 0.55f, 0.1f);
        public static readonly Color SwitchColor = new Color(0.78f, 0.36f, 1f);
        public static readonly Color InvisibleColor = new Color(0.85f, 0.88f, 0.95f);
        public static readonly Color SpeedUpColor = new Color(0.5f, 0.92f, 0.12f);
        public static readonly Color SpeedDownColor = new Color(1f, 0.32f, 0.3f);
        public static readonly Color HideObjectsColor = new Color(0.45f, 0.45f, 0.55f);
        public static readonly Color DialogueColor = new Color(1f, 0.85f, 0.25f);
        public static readonly Color CustomMoveColor = new Color(0.93f, 0.42f, 0.78f);
        public static readonly Color TriggerTileColor = new Color(0.3f, 0.95f, 0.55f);
        public static readonly Color TriggerObjectColor = new Color(1f, 0.38f, 0.22f);

        /// <summary>The colour of a part in the editor and of a power-up's glow in the game.</summary>
        public static Color ColorOf(PartKind kind) => kind switch
        {
            PartKind.FlyJump => FlyJumpColor,
            PartKind.ShortJump => ShortJumpColor,
            PartKind.Bounce => BounceColor,
            PartKind.Switch => SwitchColor,
            PartKind.Invisible => InvisibleColor,
            PartKind.SpeedUp => SpeedUpColor,
            PartKind.SpeedDown => SpeedDownColor,
            PartKind.HideObjects => HideObjectsColor,
            PartKind.Dialogue => DialogueColor,
            PartKind.CustomMoveTile => CustomMoveColor,
            PartKind.CustomMoveBlock => CustomMoveColor,
            PartKind.TriggerTile => TriggerTileColor,
            PartKind.TriggerObject => TriggerObjectColor,
            PartKind.CustomObject => TriggerObjectColor,
            _ => new Color(0.95f, 0.75f, 0.35f),
        };

        /// <summary>
        /// The line the editor draws for a power-up, in rows after its tile: from <paramref name="from"/> to
        /// <paramref name="to"/>, with dots at <paramref name="marks"/> (landings). False for Invisible and animated tiles.
        /// Row triggers (<see cref="IsRowTrigger"/>) also get a line across the whole row.
        /// </summary>
        public bool Line(List<float> marks, out float from, out float to, out Color color)
        {
            marks.Clear();
            from = 0f;
            to = 0f;
            color = ColorOf(Kind);
            switch (Kind)
            {
                case PartKind.FlyJump:
                case PartKind.ShortJump:
                case PartKind.SpeedUp:
                case PartKind.SpeedDown:
                case PartKind.HideObjects:
                    to = distance;
                    marks.Add(distance);
                    return distance > 0f;
                case PartKind.Bounce:
                    marks.AddRange(BounceLandings());
                    to = marks.Count > 0 ? marks[marks.Count - 1] : 0f;
                    return to > 0f;
                case PartKind.Switch:
                    // From the trigger line up the middle; the diamonds mark where the mirrored rows start and end.
                    to = startRows + distance;
                    marks.Add(startRows);
                    marks.Add(to);
                    return distance > 0f;
            }
            return false;
        }

        /// <summary>One line of help for the ARGS window.</summary>
        public static string Help(PartKind kind) => kind switch
        {
            PartKind.FlyJump => "Fly Jump: a long flying jump that lands Distance rows after the tile.",
            PartKind.ShortJump => "Short Jump: a low hop that lands Distance rows after the tile.",
            PartKind.Bounce => "Bounce: hops on and on (Hops, repeated) for Distance rows. Every landing needs floor, or the ball falls. Landings touch the tiles like normal rolling.",
            PartKind.Switch => "Switch (trigger line): when the ball crosses this row, in any lane, the track mirrors left-right (1,1,1,0,0 becomes 0,0,1,1,1) from Start rows after it, for Distance rows.",
            PartKind.Invisible => "Invisible: the ball disappears for Seconds, then comes back. It still plays as normal.",
            PartKind.SpeedUp => "Speed Up (trigger line): when the ball crosses this row, in any lane, the speed is multiplied by Speed Multiple for Distance rows.",
            PartKind.SpeedDown => "Speed Down (trigger line): when the ball crosses this row, in any lane, the speed is multiplied by Speed Multiple for Distance rows.",
            PartKind.HideObjects => "Hide Objects (trigger line): when the ball crosses this row, in any lane, the obstacles of the next Distance rows disappear for Seconds (1-4). They are only hidden: touching one still kills.",
            PartKind.Dialogue => "Dialogue (trigger line): when the ball crosses this row, in any lane, a bubble pops up above the ball with Text for Seconds (1-4).",
            PartKind.GemRise => "Gem Rise: the gem flies up to Height as the ball comes (Start rows before). Jump to get it.",
            PartKind.GemDrop => "Gem Drop: the gem floats at Height and comes down to the floor as the ball comes.",
            PartKind.CrownRise => "Crown Rise: the crown flies up to Height as the ball comes. Jump to get it.",
            PartKind.CrownDrop => "Crown Drop: the crown floats at Height and comes down as the ball comes.",
            PartKind.SlideFromLeft => "Tile Slide from Left: a normal floor tile that slides into its lane from the left as the ball comes.",
            PartKind.SlideFromRight => "Tile Slide from Right: a normal floor tile that slides into its lane from the right as the ball comes.",
            PartKind.RiseUp => "Rise Up: the floor tile comes up from Height below the track as the ball comes, so the ball can pass.",
            PartKind.SinkDown => "Sink Down: the floor tile sinks Height down as the ball comes. Be on it too late and the ball falls.",
            PartKind.Wave => "Wave: the floor tile bobs up and down by Height in a wave with its neighbours, and lies flat from Start rows before the ball.",
            PartKind.DropIn => "Drop In: the floor tile falls from Height above into its place as the ball comes (Start rows before).",
            PartKind.SpinIn => "Spin In: the floor tile spins and grows into its place as the ball comes (Start rows before).",
            PartKind.Shuffle => "Shuffle: the floor tile starts in another lane (always the same one) and slides into its own as the ball comes.",
            PartKind.Extend => "Extend: the floor tile grows forward from its back edge, like a bridge building itself, as the ball comes.",
            PartKind.FallAway => "Fall Away: after the ball has passed (Start rows behind it), the floor tile tumbles Height down. Only for show behind the ball.",
            PartKind.FlipIn => "Flip In: the floor tile turns over sideways, upside down to right way up, as the ball comes.",
            PartKind.PopIn => "Pop In: the floor tile pops up out of nothing, a little too big, then settles, as the ball comes.",
            PartKind.Drawbridge => "Drawbridge: the floor tile stands up on its back edge and swings down flat as the ball comes.",
            PartKind.ScrewUp => "Screw Up: the floor tile turns round and round as it rises from Height below the track into place.",
            PartKind.RollIn => "Roll In: the floor tile rolls over sideways from two lanes away into its own lane as the ball comes.",
            PartKind.FlyAway => "Fly Away: after the ball has passed (Start rows behind it), the floor tile spins up Height into the sky and shrinks away. Only for show.",
            PartKind.BlinkIn => "Blink In: the floor tile flickers on and off and stays on from Start rows before the ball. It is always solid.",
            PartKind.CustomMoveTile => "Custom Move Tile: a floor tile with its own animation (it carries what stands on it). Move X / Y / Z in tiles " +
                                       "(Z = forward), Spin in degrees. With ball on: as fast as the ball rolls (Z 20 rides 20 rows with the ball); off: takes Seconds. " +
                                       "Speed (tiles a second) is used instead of Seconds when it is above 0. Keep off: it disappears Vanish seconds (1-3) after the ball passed it. " +
                                       "Starts Start rows before the ball, or when a CustomTriggerTiles with the same Group is touched. Tiles with the same Group move as one.",
            PartKind.CustomMoveBlock => "Custom Move Block: a block with its own animation, settings like Custom Move Tile. Loop = there and back, again and again. " +
                                        "Kill off = safe: the ball goes through it, and the gem, crown or power-up on its cell rides on top of it.",
            PartKind.TriggerTile => "CustomTriggerTiles: when the ball rolls onto it (or crosses its row, Whole row on), every Trigger Object, Custom Obstacle and Custom Move with the same Group starts.",
            PartKind.TriggerObject => "Trigger Object: one object with one animation (the theme's Trigger Object Model and its Blender clip). It waits, still " +
                                      "(Frames 1) or playing the clip's first Frames, until a CustomTriggerTiles with the same Group is touched; then the rest of the " +
                                      "clip plays (Seconds 0 = as made, else stretched to Seconds) and its hitbox moves with it. Loop: plays again and again. " +
                                      "No model: a glowing bar at Height that slams down.",
            PartKind.CustomObject => "Custom Obstacle: its Blender clip waits (Idle frames 1 = still) and then plays once (Loop off) or again and again (Loop on). " +
                                     "It starts Start rows before the ball, or with a Group: when a CustomTriggerTiles with the same Group is touched. " +
                                     "Seconds 0 = as made in Blender.",
            _ => "",
        };
    }
}
