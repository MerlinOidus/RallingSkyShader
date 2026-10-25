using System;
using System.Collections.Generic;
using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// Every TrackTheme of the project, kept in Assets/RTXOn/Resources so a built game (Android too) has all official
    /// themes, not only the ones in a scene's Looks list (Adrian 2026-10-24: Volcano played in the wrong theme on
    /// Android, and the in-game Level Editor only offered official themes inside Unity). Two assets:
    /// RTXOnThemeRegistry (the themes, loaded by the game and the Level Editor) and RTXOnThemePictures (only names and
    /// backgrounds, for the Main Menu, so it doesn't load every theme's models). Filled before every build and by
    /// RTX ON > Update Theme List (Resources).
    /// </summary>
    public class ThemeRegistry : ScriptableObject
    {
        public const string ThemesResource = "RTXOnThemeRegistry";
        public const string PicturesResource = "RTXOnThemePictures";

        [Serializable]
        public class Picture
        {
            public string name;
            public Texture background;
        }

        public List<TrackTheme> themes = new List<TrackTheme>();
        public List<Picture> pictures = new List<Picture>();

        static ThemeRegistry loadedThemes, loadedPictures;
        static bool triedThemes, triedPictures;

        /// <summary>Adds every theme of the registry (and, inside Unity, of the project) that <paramref name="list"/> is missing.</summary>
        public static void AddTo(List<TrackTheme> list)
        {
            if (list == null) return;
            if (!triedThemes)
            {
                triedThemes = true;
                loadedThemes = Resources.Load<ThemeRegistry>(ThemesResource);
            }
            if (loadedThemes != null)
                foreach (var t in loadedThemes.themes)
                    if (t != null && !list.Contains(t)) list.Add(t);
#if UNITY_EDITOR
            TrackTheme.AddProjectThemes(list); // themes made after the last Update Theme List
#endif
            list.RemoveAll(t => t == null);
            list.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Names and backgrounds of every theme, without loading the themes themselves.</summary>
        public static IEnumerable<Picture> Pictures()
        {
            if (!triedPictures)
            {
                triedPictures = true;
                loadedPictures = Resources.Load<ThemeRegistry>(PicturesResource);
            }
            if (loadedPictures == null) yield break;
            foreach (var p in loadedPictures.pictures)
                if (p != null && !string.IsNullOrEmpty(p.name)) yield return p;
        }
    }
}
