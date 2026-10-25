using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// The level editor's colours, taken from the Main Menu's colour set (Settings > Menu Color), so the editor looks
    /// like the new menu (Adrian 2026-10-24: "lebih fresh"). <see cref="Load"/> runs when the editor opens; the values
    /// are kept in fields, so reading them costs nothing (the panels read some every frame).
    /// </summary>
    public static class EditorColors
    {
        static bool loaded;
        static Color background, bar, panel, window, header, button, item, accent, go, field, strip;

        public static readonly Color Danger = MenuStyle.Hex("#D9434E");
        public static readonly Color Gold = new Color(1f, 0.82f, 0.25f);

        /// <summary>Reads the colour set again (the player may have changed it in the menu).</summary>
        public static void Load()
        {
            loaded = true;
            var p = MenuStyle.Get(MenuSlot.Panel);
            background = Darken(p, 0.35f);
            bar = MenuStyle.Get(MenuSlot.Primary);
            panel = MenuStyle.Lighten(p, 0.04f);
            window = WithAlpha(MenuStyle.Lighten(p, 0.03f), 0.98f);
            header = MenuStyle.Get(MenuSlot.Primary);
            button = MenuStyle.Lighten(p, 0.17f);
            item = MenuStyle.Lighten(p, 0.1f);
            accent = MenuStyle.Get(MenuSlot.Primary2); // the set's own colour, still dark enough for white text
            go = MenuStyle.Hex("#2C9A3F");
            field = Darken(p, 0.4f);
            strip = MenuStyle.Lighten(p, 0.08f);
        }

        static Color Get(ref Color c)
        {
            if (!loaded) Load();
            return c;
        }

        /// <summary>Behind the grid.</summary>
        public static Color Background => Get(ref background);
        /// <summary>The top bar and window headers: the set's Primary.</summary>
        public static Color Bar => Get(ref bar);
        /// <summary>Side panels.</summary>
        public static Color Panel => Get(ref panel);
        /// <summary>Windows in front of the editor.</summary>
        public static Color Window => Get(ref window);
        public static Color Header => Get(ref header);
        /// <summary>Plain buttons.</summary>
        public static Color Button => Get(ref button);
        /// <summary>List rows, cards, Off boxes.</summary>
        public static Color Item => Get(ref item);
        /// <summary>Chosen tab, current item, On: the set's Primary2 (white text stays readable on it).</summary>
        public static Color Accent => Get(ref accent);
        /// <summary>Play, Save, New: green in every set.</summary>
        public static Color Go => Get(ref go);
        /// <summary>Text boxes.</summary>
        public static Color Field => Get(ref field);
        public static Color Strip => Get(ref strip);
        public static Color Soft => MenuStyle.Soft;

        static Color Darken(Color c, float amount) => Color.Lerp(c, Color.black, amount);

        static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }
    }
}
