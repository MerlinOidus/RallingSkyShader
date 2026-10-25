using System;
using System.Collections.Generic;
using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// Mover Edge (Theme Settings): the border painted around mover platforms. Auto = by neighbours like the
    /// original, Always = every mover tile framed on its own, Never = no border at all. Only the texture changes.
    /// </summary>
    public enum MoverEdge { Auto, Always, Never }

    /// <summary>
    /// Gradient (Theme Settings): how the background picture runs into the fog. Bottom = its lower part fades into
    /// the fog colour, Top = its upper part, Both = top and bottom (a transition look), None = the picture as it is.
    /// </summary>
    public enum BackgroundGradient { None, Bottom, Top, Both }

    /// <summary>One environment (Day, Sunset, Night...) of a custom theme: a copy of an existing one, with its colours.</summary>
    [Serializable]
    public class ThemeEnvironment
    {
        [Tooltip("The name theme changes use (Day, Sunset, Night...).")]
        public string name = "Day";
        [Tooltip("Where it was copied from (\"Theme / Environment\"): sky picture (HDRI), stars and sun disc come from there.")]
        public string from = "";
        public Color skyTop = new Color(0.2f, 0.45f, 0.88f);
        public Color skyHorizon = new Color(0.82f, 0.92f, 1f);
        public Color skyBottom = new Color(0.74f, 0.86f, 0.98f);
        public Color sunColor = new Color(1f, 0.95f, 0.86f);
        public float sunIntensity = 1.6f;
        public Color ambient = new Color(0.62f, 0.76f, 0.95f);
        public bool useFog = true;
        public Color fogColor = new Color(0.74f, 0.86f, 0.98f);
        public float fogDensity = 0.018f;
        [Tooltip("True = sunHeight / sunTurn below set the sun (and moon); false = the copied environment's own angle.")]
        public bool ownSun;
        [Tooltip("Sun / moon height above the horizon, degrees (0 = on the horizon, 90 = straight above).")]
        public float sunHeight = 42f;
        [Tooltip("Sun / moon turn round the track, degrees (0 = from behind the ball).")]
        public float sunTurn = -35f;

        /// <summary>Takes the colours of <paramref name="p"/>.</summary>
        public static ThemeEnvironment From(EnvironmentPreset p, string source)
        {
            return new ThemeEnvironment
            {
                name = string.IsNullOrEmpty(p.name) ? "Day" : p.name,
                from = source ?? "",
                skyTop = p.skyTop,
                skyHorizon = p.skyHorizon,
                skyBottom = p.skyBottom,
                sunColor = p.sunColor,
                sunIntensity = p.sunIntensity,
                ambient = p.ambientSky,
                useFog = p.fogDensity > 0f,
                fogColor = p.fogColor,
                fogDensity = p.fogDensity > 0f ? p.fogDensity : 0.018f,
                ownSun = true,
                sunHeight = p.sunEuler.x,
                sunTurn = Mathf.DeltaAngle(0f, p.sunEuler.y),
            };
        }

        /// <summary>The preset it was copied from with these colours on top.</summary>
        public EnvironmentPreset ToPreset(EnvironmentPreset source)
        {
            var p = source != null ? source.Clone() : EnvironmentPreset.Day();
            p.name = string.IsNullOrEmpty(name) ? "Day" : name;
            p.skyTop = skyTop;
            p.skyHorizon = skyHorizon;
            p.skyBottom = skyBottom;
            p.sunColor = sunColor;
            p.sunIntensity = sunIntensity;
            // The ambient light keeps the source's balance between sky, horizon and ground.
            var shift = new Color(ambient.r - p.ambientSky.r, ambient.g - p.ambientSky.g, ambient.b - p.ambientSky.b, 0f);
            p.ambientSky = ambient;
            p.ambientEquator = Clamp(p.ambientEquator + shift);
            p.ambientGround = Clamp(p.ambientGround + shift);
            p.fogColor = fogColor;
            p.fogDensity = useFog ? Mathf.Max(0f, fogDensity) : 0f;
            if (ownSun) p.sunEuler = new Vector3(Mathf.Clamp(sunHeight, -10f, 90f), sunTurn, 0f);
            return p;
        }

        static Color Clamp(Color c) => new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), 1f);
    }

    /// <summary>
    /// A Custom Theme made in the Level Editor (Theme Settings > Custom > +), like the fan editor's custom themes. It
    /// starts from an official theme (Base Theme: its models, tile shapes, sounds) and changes the look:
    /// Classic = the tiles painted with uploaded atlases (General, Fragile, Fragile Active, Mover, Mover Auto) and a
    /// Tile Glow picture; RTX ON = the tiles painted with materials made in Unity first (Resources/RTXOnMaterials).
    /// Objects are separate from the mode: Enemy texture and Enemy Glow, background, colours and environments apply in
    /// both. Saved as one ".themert" file (this class as JSON) in the Themes library next to the levels, and inside
    /// every level that uses it ("customThemes"), so the level plays anywhere.
    /// </summary>
    [Serializable]
    public class CustomTheme
    {
        public const string FileExtension = ".themert";
        public const string FormatName = "themert";
        public const int MaxTextureSize = 2048;

        public string format = FormatName;
        public int version = 1;
        public string name = "Custom Theme";
        public string author = "";
        [Tooltip("True when it came from Import (Theme picker: Fanmade tab); false when made here (Custom tab).")]
        public bool imported;
        [Tooltip("Official theme it starts from: models, tile shapes, midground, sounds.")]
        public string baseTheme = "";
        [Tooltip("Classic = uploaded tile textures; RTX = Unity materials. Only the tiles; objects are the same in both.")]
        public TileLook mode = TileLook.Classic;

        [Header("Textures (PNG as base64)")]
        public string general = "";
        public string fragile = "";
        public string fragileActive = "";
        public string mover = "";
        public string moverAuto = "";
        [Tooltip("Glow of General (emission picture over the General atlas). Named tileGlow for older .themert files.")]
        public string tileGlow = "";
        [Tooltip("Glow of Fragile (emission picture over the Fragile atlas).")]
        public string fragileGlow = "";
        [Tooltip("Glow of Fragile Active.")]
        public string fragileActiveGlow = "";
        [Tooltip("Glow of Mover.")]
        public string moverGlow = "";
        [Tooltip("Glow of Mover Auto.")]
        public string moverAutoGlow = "";
        [Tooltip("The enemy atlas (Model Material), both modes.")]
        public string enemy = "";
        [Tooltip("Glow of the enemies at night (emission picture over the enemy atlas).")]
        public string enemyGlow = "";
        [Tooltip("Picture in the theme picker. Empty = the background.")]
        public string icon = "";
        [Tooltip("Picture behind the track.")]
        public string background = "";

        [Header("RTX ON materials (by name, made in Unity first)")]
        public string floorMaterial = "";
        public string floorSideMaterial = "";
        public string fragileMaterial = "";
        public string fragileSideMaterial = "";
        public string moverMaterial = "";
        public string moverAutoMaterial = "";
        public string jumpMaterial = "";

        [Header("ColorSets")]
        [Tooltip("Ball colour while the theme is on. Alpha 0 = the ball's own colour.")]
        public Color ballColor = new Color(1f, 1f, 1f, 0f);
        public Color backgroundTint = Color.white;
        public Color midgroundTint = Color.white;
        public Color midFogColor = new Color(0.07f, 0.85f, 0.88f);
        [Tooltip("Colour of the tile glows (General, Fragile, Fragile Active, Mover, Mover Auto Glow; HDR: above 1 = brighter).")]
        public Color tileGlowColor = Color.white;
        public float tileGlowStrength = 1.5f;
        public Color enemyGlowColor = Color.white;
        public float enemyGlowStrength = 2.5f;
        [Range(0f, 1f)] public float enemySpecular = 0f;
        [Range(0f, 1f)] public float enemyGloss = 0.5f;

        [Header("Environments (first = start). Empty = the Base Theme's.")]
        public List<ThemeEnvironment> environments = new List<ThemeEnvironment>();

        public string ToJson(bool pretty = true) => JsonUtility.ToJson(this, pretty);

        /// <summary>Reads a .themert file (or one "customThemes" entry of a level.json). Null when it is not a theme.</summary>
        public static CustomTheme FromJson(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            CustomTheme t;
            try { t = JsonUtility.FromJson<CustomTheme>(text); }
            catch (Exception) { return null; }
            if (t == null || t.format != FormatName) return null;
            t.Normalize();
            return t;
        }

        public CustomTheme Clone() => FromJson(ToJson(false));

        public void Normalize()
        {
            format = FormatName;
            if (string.IsNullOrWhiteSpace(name)) name = "Custom Theme";
            name = name.Trim();
            author ??= "";
            baseTheme ??= "";
            general ??= ""; fragile ??= ""; fragileActive ??= ""; mover ??= ""; moverAuto ??= "";
            tileGlow ??= ""; fragileGlow ??= ""; fragileActiveGlow ??= ""; moverGlow ??= ""; moverAutoGlow ??= ""; enemy ??= ""; enemyGlow ??= ""; icon ??= ""; background ??= "";
            floorMaterial ??= ""; floorSideMaterial ??= ""; fragileMaterial ??= ""; fragileSideMaterial ??= "";
            moverMaterial ??= ""; moverAutoMaterial ??= ""; jumpMaterial ??= "";
            environments ??= new List<ThemeEnvironment>();
            environments.RemoveAll(e => e == null);
            tileGlowStrength = Mathf.Clamp(tileGlowStrength, 0f, 10f);
            enemyGlowStrength = Mathf.Clamp(enemyGlowStrength, 0f, 10f);
            enemySpecular = Mathf.Clamp01(enemySpecular);
            enemyGloss = Mathf.Clamp01(enemyGloss);
        }
    }

    /// <summary>
    /// The level's Theme Settings (Level Editor > Theme): models taken from another theme instead of the look's own,
    /// Mover Edge, and the background's Offset and Gradient. Applies to every look of the level. Empty = Default.
    /// </summary>
    [Serializable]
    public class LevelThemeSettings
    {
        public const string NoneName = "None";

        [Tooltip("Theme whose Roller Model is used. Empty = each look's own.")]
        public string roller = "";
        [Tooltip("Theme whose Block Model (block shapes) is used. Empty = each look's own.")]
        public string block = "";
        [Tooltip("Theme whose midground blocks are used, \"None\" = no midground. Empty = each look's own.")]
        public string midground = "";
        public MoverEdge moverEdge = MoverEdge.Auto;
        [Tooltip("Moves the background picture up (+) or down (-), in screen heights, on top of the look's own.")]
        public float backgroundOffset;
        [Tooltip("-1 = the look's own Gradient, else a BackgroundGradient.")]
        public int gradient = -1;
        [Tooltip("True = no background picture: the Environments' sky (skybox) shows instead, all the way round.")]
        public bool hideBackground;
        [Tooltip("-1 = each look's own Model Look, else a TileLook (0 = RTX: the RTX Models, 1 = Classic: the original models).")]
        public int modelLook = -1;
        [Tooltip("-1 = each look's own Texture Look, else a TileLook (0 = RTX materials, 1 = Classic atlases).")]
        public int textureLook = -1;

        public bool IsDefault => string.IsNullOrEmpty(roller) && string.IsNullOrEmpty(block) && string.IsNullOrEmpty(midground) &&
                                 moverEdge == MoverEdge.Auto && Mathf.Abs(backgroundOffset) < 0.0001f && gradient < 0 && !hideBackground &&
                                 modelLook < 0 && textureLook < 0;

        /// <summary>Text that changes with every setting (cache key).</summary>
        public string Key => $"{roller}|{block}|{midground}|{(int)moverEdge}|{backgroundOffset:0.####}|{gradient}|{hideBackground}|{modelLook}|{textureLook}";

        public LevelThemeSettings Clone() => (LevelThemeSettings)MemberwiseClone();

        public void Normalize()
        {
            roller ??= "";
            block ??= "";
            midground ??= "";
            backgroundOffset = Mathf.Clamp(backgroundOffset, -0.5f, 0.5f);
            if (gradient > (int)BackgroundGradient.Both) gradient = -1;
            if (modelLook > (int)TileLook.Classic) modelLook = -1;
            if (textureLook > (int)TileLook.Classic) textureLook = -1;
        }
    }

    /// <summary>An Object Pack (".rsrtp"): several custom obstacles in one file.</summary>
    [Serializable]
    public class ObjectPack
    {
        public const string FileExtension = ".rsrtp";
        public const string FormatName = "rsrtp";
        public string format = FormatName;
        public int version = 1;
        public string name = "Object Pack";
        public List<CustomObstacle> objects = new List<CustomObstacle>();
    }

    /// <summary>
    /// A Full Level (".levelrsrt"): the level's level.json text, which already carries the custom obstacles it paints
    /// ("customs") and the custom themes it uses ("customThemes"), and its LevelPreview's level.json text ("preview",
    /// empty when it has none): Level1 + LevelPreview1 in one file.
    /// </summary>
    [Serializable]
    public class LevelPackage
    {
        public const string FileExtension = ".levelrsrt";
        public const string FormatName = "levelrsrt";
        public string format = FormatName;
        public int version = 1;
        public string name = "Level";
        public string level = "";
        public string preview = "";
    }
}
