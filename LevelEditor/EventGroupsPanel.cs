using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RTXOn
{
    /// <summary>
    /// The level editor's EVENT GROUPS window (top bar > Events, or GROUP on a cell in the Edit tool), after the
    /// RollerCraft editor Adrian showed: left the level's groups (number + name: Run, Prank...), middle the clicked
    /// cell's layer (the groups it is in, the groups it fires, Wait for trigger) and the chosen group, right that group's
    /// events one at a time (Move, Move From, Rotate, Scale, Hide, Show, Shake with Delay, Seconds, X / Y / Z, Curve, Loop).
    /// Every change goes into the level at once.
    /// </summary>
    public class EventGroupsPanel : PanelKit
    {
        const float WindowWidth = 1800f, WindowHeight = 900f;
        const float ListWidth = 320f, ColumnWidth = 700f;
        static readonly string[] TypeNames = { "Move", "Move From", "Rotate", "Scale", "Hide", "Show", "Shake" };
        static readonly string[] TypeHelp =
        {
            "Moves by X / Y / Z tiles (Z = forward, the way the ball rolls).",
            "Starts X / Y / Z tiles away and comes to its place (Y -4 = rises from below).",
            "Turns by X / Y / Z degrees.",
            "Grows or shrinks to X / Y / Z times its size (0 = 1).",
            "Disappears and can't be touched any more (Loop: blinks).",
            "Hidden until the group fires, then appears (Loop: blinks).",
            "Shakes by X / Y / Z tiles (0 = 0.08) for Seconds.",
        };

        RectTransform listContent;
        readonly List<GameObject> listItems = new List<GameObject>();
        Text cellTitle;

        int cellRow = -1, cellLane = -1;
        TileLayer cellLayer = TileLayer.Object;
        int groupId;
        int eventIndex;

        LevelData Level => editor.Level;
        bool HasCell => Level != null && cellRow >= 0 && cellRow < Level.RowCount && cellLane >= 0 && cellLane < LevelData.Width;
        int CellId => HasCell ? Level.Get(cellLayer, cellRow, cellLane) : 0;
        CellGroup Cell => HasCell ? Level.CellGroupAt(cellRow, cellLane, cellLayer) : null;
        CellGroup EditCell => Level.EditCellGroup(cellRow, cellLane, cellLayer);
        EventGroup Chosen => Level != null ? Level.FindGroup(groupId) : null;
        GroupEvent Event
        {
            get
            {
                var g = Chosen;
                if (g == null || g.events.Count == 0) return null;
                eventIndex = Mathf.Clamp(eventIndex, 0, g.events.Count - 1);
                return g.events[eventIndex];
            }
        }

        public static EventGroupsPanel Build(LevelEditor owner, Transform parent)
        {
            var bg = UiFactory.MakePanel(parent, "Event Groups", PanelColor, true);
            UiFactory.Place(bg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(WindowWidth, WindowHeight));
            var panel = bg.gameObject.AddComponent<EventGroupsPanel>();
            panel.editor = owner;
            panel.BuildUi((RectTransform)bg.transform);
            bg.gameObject.SetActive(false);
            return panel;
        }

        protected override bool Editable => editor != null && editor.Level != null;

        // ---------------- Open / close ----------------

        /// <summary>Opens on the level's groups (no cell).</summary>
        public void Open() => Open(-1, -1, TileLayer.Object);

        /// <summary>Opens on one layer of a cell: the groups it is in and fires.</summary>
        public void Open(int row, int lane, TileLayer layer)
        {
            cellRow = row;
            cellLane = lane;
            cellLayer = layer;
            var cell = Cell;
            var first = new List<int>();
            if (cell != null) EventGroups.Parse(cell.groups, first);
            if (first.Count > 0) groupId = first[0];
            else if (Chosen == null) groupId = Level.Groups.Count > 0 ? Level.Groups[0].id : 0;
            eventIndex = 0;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            ClosePopups();
            ShowAll();
            Status(HasCell
                ? "In groups: this layer of the cell plays those groups' events. Fires: touching it fires those groups."
                : "Pick a group on the left, or + Group. Then click a cell with the Edit tool and GROUP to put it in a group.");
        }

        public void Close()
        {
            if (!IsOpen) return;
            if (ClosePopups()) return;
            Hide();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            cellRow = cellLane = -1;
        }

        protected override void Changed()
        {
            if (Level == null) return;
            var g = Chosen;
            if (g != null) g.Normalize();
            var cell = Cell;
            if (cell != null)
            {
                cell.Normalize();
                if (cell.IsEmpty) Level.CellGroups.Remove(cell);
            }
            editor.EventGroupsChanged();
            ShowAll();
        }

        void ShowAll()
        {
            if (Level == null) return;
            if (Chosen == null) groupId = 0;
            cellTitle.text = HasCell
                ? $"ROW {cellRow} · LANE {cellLane + 1} · {EventGroups.LayerName(cellLayer).ToUpperInvariant()}: {(CellId != 0 ? TilePalette.Name(CellId) : "empty")}"
                : "No cell (Edit tool > GROUP picks one)";
            Refresh();
            RebuildList();
        }

        // ---------------- UI ----------------

        void BuildUi(RectTransform root)
        {
            pageWidth = ColumnWidth;
            var header = UiFactory.MakePanel(root, "Header", HeaderColor);
            UiFactory.Place(header.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(WindowWidth, 80f));
            var title = UiFactory.MakeLabel(header.transform, "EVENT GROUPS", 32, TextAnchor.MiddleLeft, new Color(1f, 0.75f, 0.3f), FontStyle.Bold);
            UiFactory.Place(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(600f, 70f));
            HeaderButtons(header.transform, new[] { "Done" }, new UnityEngine.Events.UnityAction[] { Close }, new[] { SaveColor });

            // Left: the groups.
            var listHead = UiFactory.MakeLabel(root, "GROUPS", 20, TextAnchor.MiddleLeft, SoftText, FontStyle.Bold);
            UiFactory.Place(listHead.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -92f), new Vector2(ListWidth, 30f));
            var scroll = UiFactory.MakeGridList(root, new Vector2(ListWidth - 40f, 56f), 6f, 1, AccentColor, out listContent);
            UiFactory.Place((RectTransform)scroll.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -126f), new Vector2(ListWidth, WindowHeight - 126f - 140f));
            var add = UiFactory.MakeButton(root, "+ Group", AccentColor, AddGroup, 24);
            UiFactory.Place((RectTransform)add.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 70f), new Vector2(ListWidth, 56f));

            // Middle: the cell, then the chosen group.
            var middle = UiFactory.MakeRect(root, "Cell And Group");
            UiFactory.Place(middle, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f + ListWidth + 24f, -92f), new Vector2(ColumnWidth, WindowHeight - 170f));
            cellTitle = UiFactory.MakeLabel(middle, "", 21, TextAnchor.MiddleLeft, new Color(1f, 0.75f, 0.3f), FontStyle.Bold);
            UiFactory.Place(cellTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(ColumnWidth, 34f));
            cellTitle.resizeTextForBestFit = true;
            cellTitle.resizeTextMinSize = 12;
            cellTitle.resizeTextMaxSize = 21;

            var cellArea = Group(middle, "Cell", -44f, () => HasCell);
            float y = 0f;
            CycleRow(cellArea, "Layer", ref y, () => EventGroups.LayerName(cellLayer) + " (" + EventGroups.GroupWord(cellLayer) + ")",
                step => cellLayer = (TileLayer)(((int)cellLayer + step + 3) % 3));
            TextRow(cellArea, "In groups", ref y, () => Cell != null ? Cell.groups : "", t => EditCell.groups = t);
            TextRow(cellArea, "Fires groups", ref y, () => Cell != null ? Cell.fires : "", t => EditCell.fires = t);
            ToggleRow(cellArea, "Wait for trigger", ref y, () => Cell == null || Cell.sleep ? "ON: still until its group fires" : "OFF: animates as always",
                () => Cell == null || Cell.sleep, () => EditCell.sleep = !(Cell == null || Cell.sleep));
            ToggleRow(cellArea, "Fires on whole row", ref y, () => Cell != null && Cell.wholeRow ? "ON: crossing the row anywhere" : "OFF: touching it",
                () => Cell != null && Cell.wholeRow, () => EditCell.wholeRow = !(Cell != null && Cell.wholeRow));
            ButtonsRow(cellArea, "Chosen group", ref y, () => Chosen != null ? Chosen.Label : "none",
                ("Join", JoinGroup), ("Leave", LeaveGroup), ("Fires it", FireGroup));

            var groupArea = Group(middle, "Group", -44f - 6f * RowStep - 20f, () => Chosen != null);
            y = 0f;
            var groupHead = UiFactory.MakeLabel(groupArea, "GROUP", 20, TextAnchor.MiddleLeft, SoftText, FontStyle.Bold);
            UiFactory.Place(groupHead.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(ColumnWidth, 30f));
            y = -36f;
            InfoRow(groupArea, "Number", ref y, () => Chosen != null ? Chosen.id.ToString() : "");
            TextRow(groupArea, "Name", ref y, () => Chosen != null ? Chosen.name : "", t => { if (Chosen != null) Chosen.name = t; }, 24);
            InfoRow(groupArea, "Used by", ref y, UsedBy);
            ButtonsRow(groupArea, "Group", ref y, () => "", ("Delete", DeleteGroup));

            // Right: the chosen group's events, one at a time.
            var right = UiFactory.MakeRect(root, "Events");
            UiFactory.Place(right, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f + ListWidth + 24f + ColumnWidth + 24f, -92f), new Vector2(ColumnWidth, WindowHeight - 170f));
            var eventsArea = Group(right, "Event List", 0f, () => Chosen != null);
            y = 0f;
            ButtonsRow(eventsArea, "Events", ref y, () =>
            {
                var g = Chosen;
                return g == null || g.events.Count == 0 ? "none yet" : $"{eventIndex + 1} / {g.events.Count}";
            }, ("<", () => { eventIndex--; Changed(); }), (">", () => { eventIndex++; Changed(); }), ("+ Event", AddEvent), ("Delete", DeleteEvent));
            var eventArea = Group(right, "Event", -RowStep, () => Event != null);
            y = 0f;
            CycleRow(eventArea, "Type", ref y, () => Event != null ? TypeNames[(int)Event.Type] : "", step =>
            {
                var e = Event;
                if (e == null) return;
                var next = (GroupEventType)(((int)e.Type + step + TypeNames.Length) % TypeNames.Length);
                var made = GroupEvent.Make(next);
                made.name = e.name;
                made.delay = e.delay;
                Chosen.events[eventIndex] = made; // a new type starts with values that show what it does
            });
            InfoRow(eventArea, "What it does", ref y, () => Event != null ? TypeHelp[(int)Event.Type] : "");
            TextRow(eventArea, "Event name", ref y, () => Event != null ? Event.name : "", t => { if (Event != null) Event.name = t; }, 24);
            NumberRow(eventArea, "Delay (s)", ref y, () => Event != null ? Event.delay : 0f, v => { if (Event != null) Event.delay = v; });
            NumberRow(eventArea, "Seconds", ref y, () => Event != null ? Event.seconds : 0f, v => { if (Event != null) Event.seconds = v; });
            NumberRow(eventArea, "X", ref y, () => Event != null ? Event.x : 0f, v => { if (Event != null) Event.x = v; });
            NumberRow(eventArea, "Y", ref y, () => Event != null ? Event.y : 0f, v => { if (Event != null) Event.y = v; });
            NumberRow(eventArea, "Z (forward)", ref y, () => Event != null ? Event.z : 0f, v => { if (Event != null) Event.z = v; });
            CycleRow(eventArea, "Curve", ref y, () => Event != null ? Event.Curve.ToString() : "", step =>
            {
                if (Event == null) return;
                int n = Enum.GetValues(typeof(EventCurve)).Length;
                Event.Curve = (EventCurve)(((int)Event.Curve + step + n) % n);
            });
            ToggleRow(eventArea, "Loop", ref y, () => Event != null && Event.loop ? "ON: there and back, again and again" : "OFF: once",
                () => Event != null && Event.loop, () => { if (Event != null) Event.loop = !Event.loop; });

            MakeStatus(root, WindowWidth);
        }

        void RebuildList()
        {
            foreach (var item in listItems) Destroy(item);
            listItems.Clear();
            var cell = Cell;
            var inCell = new List<int>();
            var fired = new List<int>();
            if (cell != null)
            {
                EventGroups.Parse(cell.groups, inCell);
                EventGroups.Parse(cell.fires, fired);
            }
            foreach (var g in Level.Groups)
            {
                if (g == null) continue;
                int id = g.id;
                bool chosen = id == groupId;
                string marks = (inCell.Contains(id) ? "  ●" : "") + (fired.Contains(id) ? "  ▶" : "");
                var b = UiFactory.MakeButton(listContent, $"{g.Label}  ({g.events.Count}){marks}", chosen ? AccentColor : ButtonColor, () =>
                {
                    groupId = id;
                    eventIndex = 0;
                    ShowAll();
                }, 21);
                var t = b.GetComponentInChildren<Text>();
                t.alignment = TextAnchor.MiddleLeft;
                t.rectTransform.offsetMin = new Vector2(14f, t.rectTransform.offsetMin.y);
                t.color = TilePalette.TextColorOn(chosen ? AccentColor : ButtonColor);
                listItems.Add(b.gameObject);
            }
        }

        // ---------------- Actions ----------------

        void AddGroup()
        {
            if (Level == null) return;
            int id = 1;
            while (Level.FindGroup(id) != null && id < EventGroups.MaxGroup) id++;
            if (Level.FindGroup(id) != null)
            {
                Status($"A level can have {EventGroups.MaxGroup} groups.");
                return;
            }
            var g = new EventGroup { id = id };
            g.events.Add(GroupEvent.Make(GroupEventType.Move));
            Level.Groups.Add(g);
            Level.Groups.Sort((a, b) => a.id.CompareTo(b.id));
            groupId = id;
            eventIndex = 0;
            Changed();
            Status($"Group {id} made with one Move event (Z 3 in 2 s). Give it a name, then put cells in it (Join) or make a cell fire it.");
        }

        void DeleteGroup()
        {
            var g = Chosen;
            if (g == null) return;
            Level.Groups.Remove(g);
            // The cells keep the number (it does nothing without events) until it is taken out of them by hand.
            groupId = Level.Groups.Count > 0 ? Level.Groups[0].id : 0;
            Changed();
            Status($"Group {g.id} deleted. Cells still list number {g.id}: it does nothing now.");
        }

        void AddEvent()
        {
            var g = Chosen;
            if (g == null) return;
            g.events.Add(GroupEvent.Make(GroupEventType.Move));
            eventIndex = g.events.Count - 1;
            Changed();
        }

        void DeleteEvent()
        {
            var g = Chosen;
            if (g == null || g.events.Count == 0) return;
            g.events.RemoveAt(Mathf.Clamp(eventIndex, 0, g.events.Count - 1));
            eventIndex = Mathf.Max(0, eventIndex - 1);
            Changed();
        }

        void EditList(Func<CellGroup, string> get, Action<CellGroup, string> set, bool add)
        {
            if (!HasCell || Chosen == null) return;
            if (CellId == 0)
            {
                Status("That layer of the cell is empty: paint something there first.");
                return;
            }
            var cell = EditCell;
            var list = EventGroups.Parse(get(cell));
            if (add && !list.Contains(groupId)) list.Add(groupId);
            if (!add) list.Remove(groupId);
            set(cell, EventGroups.Format(list));
            Changed();
        }

        void JoinGroup() => EditList(c => c.groups, (c, v) => c.groups = v, true);
        void LeaveGroup() => EditList(c => c.groups, (c, v) => c.groups = v, false);

        void FireGroup()
        {
            var cell = Cell;
            bool fires = cell != null && EventGroups.Contains(cell.fires, groupId);
            EditList(c => c.fires, (c, v) => c.fires = v, !fires); // a second press takes it off again
        }

        /// <summary>How many cells are in the chosen group and how many fire it.</summary>
        string UsedBy()
        {
            var g = Chosen;
            if (g == null) return "";
            int members = 0, triggers = 0;
            foreach (var c in Level.CellGroups)
            {
                if (c == null) continue;
                if (EventGroups.Contains(c.groups, g.id)) members++;
                if (EventGroups.Contains(c.fires, g.id)) triggers++;
            }
            int tiles = 0;
            for (int r = 0; r < Level.RowCount; r++)
                for (int l = 0; l < LevelData.Width; l++)
                {
                    int id = Level.Get(r, l);
                    if (id == PartArgs.TriggerTileId && Level.ArgsAt(r, l, id).group == g.id) tiles++;
                }
            return $"{members} cell layer(s) in it, fired by {triggers + tiles}" + (triggers + tiles == 0 ? " (nothing fires it yet)" : "");
        }
    }
}
