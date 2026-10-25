namespace RTXOn
{
    /// <summary>
    /// Level editor play test: the editor hands the level it is editing (as level.json text) and a start row to the
    /// Gameplay scene, which plays it with its music from that row; Esc goes back to the editor with the edits kept.
    /// Static, so it survives the scene change.
    /// </summary>
    public static class PlayTest
    {
        public static bool Active { get; private set; }
        /// <summary>The level being edited, as level.json text.</summary>
        public static string Json { get; private set; }
        public static int StartRow { get; private set; }
        /// <summary>Lane the ball starts in (the editor's Start Pos), or -1 = the first safe lane of the start row.</summary>
        public static int StartLane { get; private set; } = -1;
        public static string EditorScene { get; private set; }

        // The editor's state, given back when it opens again.
        public static bool EditorDirty;
        public static int EditorFirstRow;
        public static int EditorSelectedId;
        public static bool Returning { get; private set; }

        /// <summary>Entering Play mode without a domain reload must not start in an old play test.</summary>
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Active = false;
            Returning = false;
            Json = null;
        }

        public static void Begin(string json, int startRow, int startLane, string editorScene)
        {
            Active = true;
            Returning = false;
            Json = json;
            StartRow = startRow;
            StartLane = startLane;
            EditorScene = editorScene;
        }

        /// <summary>Back to the editor: it takes the level from <see cref="Json"/> instead of reloading the file.</summary>
        public static void End()
        {
            Active = false;
            Returning = true;
        }

        /// <summary>The editor has taken its level back.</summary>
        public static void Clear()
        {
            Returning = false;
            Json = null;
        }
    }
}
