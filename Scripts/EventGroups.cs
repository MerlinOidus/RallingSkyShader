using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// Event Groups (Adrian 2026-10-24, from the RollerCraft editor): any tile, object or collect of a cell can belong to
    /// groups (TileGroupID / EnemyGroupID, several: "5,6") and can fire groups when the ball touches it ("Fires").
    /// Group 0 / none = it plays as always. A group has a name (Run, Prank...) and events (move, turn, hide...) that
    /// play when the group fires. Saved in level.json "cellGroups" and "groups".
    /// </summary>
    [Serializable]
    public class CellGroup
    {
        public int row;
        public int lane;
        [Tooltip("0 = Tile, 1 = Object, 2 = Collect.")]
        public int layer;
        [Tooltip("The groups it belongs to (\"5\" or \"5,6,7\"); their events move it.")]
        public string groups = "";
        [Tooltip("The groups it fires when the ball touches it (\"7\"), like a CustomTriggerTiles.")]
        public string fires = "";
        [Tooltip("Wait for trigger: its own animation (crusher, riser, clip...) stands still until one of its groups fires.")]
        public bool sleep = true;
        [Tooltip("Fires: the ball crossing its row in any lane (or in the air) fires, not only touching it.")]
        public bool wholeRow;

        public TileLayer Layer
        {
            get => (TileLayer)Mathf.Clamp(layer, 0, 2);
            set => layer = (int)value;
        }

        public bool IsEmpty => EventGroups.Parse(groups).Count == 0 && EventGroups.Parse(fires).Count == 0;

        public CellGroup Clone() => (CellGroup)MemberwiseClone();

        public void Normalize()
        {
            layer = Mathf.Clamp(layer, 0, 2);
            groups = EventGroups.Format(EventGroups.Parse(groups));
            fires = EventGroups.Format(EventGroups.Parse(fires));
        }
    }

    public enum GroupEventType
    {
        /// <summary>Moves by X / Y / Z tiles (Z = forward).</summary>
        Move,
        /// <summary>Starts X / Y / Z tiles away and comes to its place (rise from below, drop from above).</summary>
        MoveFrom,
        /// <summary>Turns by X / Y / Z degrees.</summary>
        Rotate,
        /// <summary>Grows or shrinks to X / Y / Z times its size (0 = 1).</summary>
        Scale,
        /// <summary>Disappears (and stops being touchable).</summary>
        Hide,
        /// <summary>Hidden until the group fires, then appears.</summary>
        Show,
        /// <summary>Shakes by X / Y / Z tiles (0 = 0.08) for Seconds.</summary>
        Shake,
    }

    public enum EventCurve { Smooth, Fast, Slow, Linear, Bounce }

    /// <summary>One event of a group: what happens, Delay seconds after the group fires, over Seconds.</summary>
    [Serializable]
    public class GroupEvent
    {
        public string name = "";
        public string type = nameof(GroupEventType.Move);
        [Tooltip("Seconds after the group fires before it starts.")]
        public float delay;
        [Tooltip("Seconds it takes.")]
        public float seconds = 1f;
        public float x, y, z;
        [Tooltip("There and back, again and again.")]
        public bool loop;
        [Tooltip("Smooth, Fast (quick start, soft end), Slow (soft start, quick end: a fall), Linear, Bounce.")]
        public string curve = nameof(EventCurve.Smooth);

        public GroupEventType Type
        {
            get => Enum.TryParse(type, true, out GroupEventType t) ? t : GroupEventType.Move;
            set => type = value.ToString();
        }

        public EventCurve Curve
        {
            get => Enum.TryParse(curve, true, out EventCurve c) ? c : EventCurve.Smooth;
            set => curve = value.ToString();
        }

        public Vector3 Amount => new Vector3(x, y, z);

        public GroupEvent Clone() => (GroupEvent)MemberwiseClone();

        public void Normalize()
        {
            Type = Type;
            Curve = Curve;
            name = (name ?? "").Trim();
            delay = Mathf.Clamp(delay, 0f, 60f);
            seconds = Mathf.Clamp(seconds, 0f, 60f);
            x = Mathf.Clamp(x, -3600f, 3600f);
            y = Mathf.Clamp(y, -3600f, 3600f);
            z = Mathf.Clamp(z, -3600f, 3600f);
        }

        /// <summary>A new event of <paramref name="t"/> with values that show what it does.</summary>
        public static GroupEvent Make(GroupEventType t)
        {
            var e = new GroupEvent { Type = t };
            switch (t)
            {
                case GroupEventType.Move: e.z = 3f; e.seconds = 2f; e.Curve = EventCurve.Fast; break; // Adrian's gem that runs away
                case GroupEventType.MoveFrom: e.y = -4f; e.seconds = 0.6f; e.Curve = EventCurve.Fast; break;
                case GroupEventType.Rotate: e.y = 360f; e.seconds = 1f; break;
                case GroupEventType.Scale: e.x = e.y = e.z = 2f; e.seconds = 0.5f; break;
                case GroupEventType.Hide: e.seconds = 0f; break;
                case GroupEventType.Show: e.seconds = 0f; break;
                case GroupEventType.Shake: e.x = 0.08f; e.seconds = 1f; break;
            }
            return e;
        }

        /// <summary>The event's curve at <paramref name="u"/> (0..1).</summary>
        public float Ease(float u)
        {
            u = Mathf.Clamp01(u);
            switch (Curve)
            {
                case EventCurve.Fast: return 1f - (1f - u) * (1f - u) * (1f - u);
                case EventCurve.Slow: return u * u * u;
                case EventCurve.Linear: return u;
                case EventCurve.Bounce:
                    if (u < 1f / 2.75f) return 7.5625f * u * u;
                    if (u < 2f / 2.75f) { u -= 1.5f / 2.75f; return 7.5625f * u * u + 0.75f; }
                    if (u < 2.5f / 2.75f) { u -= 2.25f / 2.75f; return 7.5625f * u * u + 0.9375f; }
                    u -= 2.625f / 2.75f;
                    return 7.5625f * u * u + 0.984375f;
                default: return u * u * (3f - 2f * u);
            }
        }
    }

    /// <summary>A group: its number, name and events.</summary>
    [Serializable]
    public class EventGroup
    {
        public int id;
        public string name = "";
        public List<GroupEvent> events = new List<GroupEvent>();

        public string Label => string.IsNullOrEmpty(name) ? $"Group {id}" : $"{id} {name}";

        public EventGroup Clone()
        {
            var g = (EventGroup)MemberwiseClone();
            g.events = new List<GroupEvent>();
            if (events != null) foreach (var e in events) if (e != null) g.events.Add(e.Clone());
            return g;
        }

        public void Normalize()
        {
            id = Mathf.Clamp(id, 1, EventGroups.MaxGroup);
            name = (name ?? "").Trim();
            if (name.Length > 24) name = name.Substring(0, 24);
            if (events == null) events = new List<GroupEvent>();
            events.RemoveAll(e => e == null);
            foreach (var e in events) e.Normalize();
        }
    }

    public static class EventGroups
    {
        public const int MaxGroup = 99;
        static readonly List<int> scratch = new List<int>(4);

        /// <summary>"5, 6,7" → 5, 6, 7 (1..99, no repeats).</summary>
        public static List<int> Parse(string text, List<int> into = null)
        {
            var list = into ?? new List<int>(4);
            list.Clear();
            if (string.IsNullOrEmpty(text)) return list;
            foreach (var part in text.Split(new[] { ',', ' ', ';', '/' }, StringSplitOptions.RemoveEmptyEntries))
                if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out int g) && g > 0 && g <= MaxGroup && !list.Contains(g))
                    list.Add(g);
            return list;
        }

        public static string Format(List<int> groups) => groups == null || groups.Count == 0 ? "" : string.Join(",", groups);

        public static bool Contains(string text, int group) => Parse(text, scratch).Contains(group);

        public static string LayerName(TileLayer layer) => layer == TileLayer.Tile ? "Tile" : layer == TileLayer.Object ? "Object" : "Collect";

        /// <summary>The ID word of a layer, like RollerCraft: TileGroupID, EnemyGroupID, CollectGroupID.</summary>
        public static string GroupWord(TileLayer layer) => layer == TileLayer.Tile ? "TileGroupID" : layer == TileLayer.Object ? "EnemyGroupID" : "CollectGroupID";
    }
}
