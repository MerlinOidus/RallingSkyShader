namespace RTXOn
{
    /// <summary>
    /// The level chosen in the Main Menu's level list, handed to the Gameplay scene (Play) or the Level Editor (Edit).
    /// Static, so it survives the scene change; the Gameplay scene goes back to <see cref="MenuScene"/> with Esc or
    /// after finishing the level.
    /// </summary>
    public static class LevelSelect
    {
        public static bool Active { get; private set; }
        /// <summary>The chosen level as level.json text.</summary>
        public static string Json { get; private set; }
        /// <summary>Its file name, so a continue from a checkpoint knows it is the same level.</summary>
        public static string Key { get; private set; }
        /// <summary>Its number in the list (1 = first), used for Music1..5 when the level names no music.</summary>
        public static int Number { get; private set; }
        public static string MenuScene { get; private set; }
        /// <summary>The level Edit opens in the Level Editor (taken once by the editor).</summary>
        public static UnityEngine.TextAsset EditLevel;
        /// <summary>Or the file Edit opens, for a level that only exists in the build's save folder.</summary>
        public static string EditPath;
        /// <summary>The row the list had selected, so it opens on the same level when you come back.</summary>
        public static int Selected;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Active = false;
            Json = null;
            EditLevel = null;
            EditPath = null;
            MenuScene = null;
            Selected = 0;
        }

        public static void Begin(string json, string key, int number, string menuScene)
        {
            Active = true;
            Json = json;
            Key = key;
            Number = number;
            MenuScene = menuScene;
        }

        /// <summary>Remembers the menu without a level to play (Edit, or the menu opened again).</summary>
        public static void SetMenu(string menuScene)
        {
            Active = false;
            Json = null;
            MenuScene = menuScene;
        }

        /// <summary>Back to the level list (with the loading screen). False when no menu opened this game.</summary>
        public static bool BackToMenu()
        {
            if (string.IsNullOrEmpty(MenuScene)) return false;
            Active = false;
            Json = null;
            LoadingScreen.Load(MenuScene);
            return true;
        }
    }
}
