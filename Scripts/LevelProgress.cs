using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// The player's best run on each level played from the Main Menu (PlayerPrefs, per level file name): best percent,
    /// most gems and most crowns. The level cards and the Detail screen show it. Play tests don't count.
    /// </summary>
    public static class LevelProgress
    {
        const string Prefix = "RTXOn.Best.";

        public struct Best
        {
            public int percent, gems, crowns;
            public bool played;
        }

        public static Best Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return default;
            string k = Prefix + key;
            return new Best
            {
                percent = PlayerPrefs.GetInt(k + ".percent", 0),
                gems = PlayerPrefs.GetInt(k + ".gems", 0),
                crowns = PlayerPrefs.GetInt(k + ".crowns", 0),
                played = PlayerPrefs.HasKey(k + ".percent"),
            };
        }

        /// <summary>Keeps each number if it beats the saved one. <paramref name="progress"/> is 0..1.</summary>
        public static void Record(string key, float progress, int gems, int crowns)
        {
            if (string.IsNullOrEmpty(key)) return;
            string k = Prefix + key;
            var best = Get(key);
            int percent = Mathf.Clamp(Mathf.FloorToInt(progress * 100f), 0, 100);
            PlayerPrefs.SetInt(k + ".percent", Mathf.Max(best.percent, percent));
            PlayerPrefs.SetInt(k + ".gems", Mathf.Max(best.gems, gems));
            PlayerPrefs.SetInt(k + ".crowns", Mathf.Max(best.crowns, crowns));
            PlayerPrefs.Save();
        }

        public static void Forget(string key)
        {
            string k = Prefix + key;
            PlayerPrefs.DeleteKey(k + ".percent");
            PlayerPrefs.DeleteKey(k + ".gems");
            PlayerPrefs.DeleteKey(k + ".crowns");
        }
    }
}
