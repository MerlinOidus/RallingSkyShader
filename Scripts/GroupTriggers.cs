using System.Collections.Generic;
using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// Trigger groups (EnemyGroupID) of the level being played: a CustomTriggerTiles (159), or any cell whose Event Group
    /// "Fires" it (<see cref="CellGroup"/>), fires its Group, and every
    /// Trigger Object (169), Custom Obstacle (200-999) and Custom Move (157, 158) with that Group starts. Custom moves whose Group has no trigger tile start
    /// together, when the ball comes near the first row of the group. Cleared for every run (TrackBuilder.Init).
    /// </summary>
    public static class GroupTriggers
    {
        static readonly Dictionary<int, float> fired = new Dictionary<int, float>();
        static readonly HashSet<int> withTrigger = new HashSet<int>();
        static readonly Dictionary<int, int> firstRow = new Dictionary<int, int>();

        /// <summary>Reads the level's groups. Trigger tiles behind <paramref name="ballZ"/> (a run continued from a
        /// checkpoint or Start Pos) count as fired long ago, so their objects are already in their end state.</summary>
        public static void Reset(LevelData level, float ballZ)
        {
            fired.Clear();
            withTrigger.Clear();
            firstRow.Clear();
            if (level == null) return;
            var ids = new List<int>(3);
            for (int r = 0; r < level.RowCount; r++)
                for (int c = 0; c < LevelData.Width; c++)
                {
                    level.PartIdsAt(r, c, ids);
                    foreach (int id in ids)
                    {
                        var kind = PartArgs.KindOf(id);
                        if (!PartArgs.HasGroup(kind)) continue;
                        var a = level.ArgsAt(r, c, id);
                        if (a.group <= 0) continue;
                        if (kind == PartKind.TriggerTile)
                        {
                            withTrigger.Add(a.group);
                            if (r < ballZ - 0.5f && !fired.ContainsKey(a.group)) fired[a.group] = float.NegativeInfinity;
                        }
                        else if (PartArgs.IsCustomMove(kind) && (!firstRow.TryGetValue(a.group, out int first) || r < first))
                            firstRow[a.group] = r;
                    }
                }
            // Event Groups: any tile, object or collect can fire groups when the ball touches it.
            var groups = new List<int>(4);
            foreach (var cell in level.CellGroups)
            {
                if (cell == null) continue;
                EventGroups.Parse(cell.fires, groups);
                foreach (int g in groups)
                {
                    withTrigger.Add(g);
                    if (cell.row < ballZ - 0.5f && !fired.ContainsKey(g)) fired[g] = float.NegativeInfinity;
                }
            }
        }

        /// <summary>The first time one of <paramref name="groups"/> fired (false when none has).</summary>
        public static bool TryFiredAny(List<int> groups, out float time)
        {
            time = float.PositiveInfinity;
            bool any = false;
            if (groups == null) return false;
            foreach (int g in groups)
                if (fired.TryGetValue(g, out float t))
                {
                    any = true;
                    if (t < time) time = t;
                }
            return any;
        }

        /// <summary>A trigger was touched: its group starts (only the first time).</summary>
        public static void Fire(int group)
        {
            if (group <= 0 || fired.ContainsKey(group)) return;
            fired[group] = Time.time;
        }

        /// <summary>True when some trigger tile in the level fires <paramref name="group"/>.</summary>
        public static bool HasTrigger(int group) => group > 0 && withTrigger.Contains(group);

        /// <summary>When <paramref name="group"/> was fired (Time.time; negative infinity = before this run started).</summary>
        public static bool TryFired(int group, out float time) => fired.TryGetValue(group, out time);

        /// <summary>The first row of a group of custom moves (they start together from there), else <paramref name="fallback"/>.</summary>
        public static float FirstRow(int group, float fallback) => group > 0 && firstRow.TryGetValue(group, out int r) ? r : fallback;
    }
}
