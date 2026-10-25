using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace RTXOn
{
    /// <summary>
    /// Materials, colours, environment presets and optional model overrides for one level look.
    /// "RTX ON > Build Gameplay Scene" creates and fills one of these for Massif.
    /// Drop your own models into the model slots to replace the built-in shapes;
    /// colliders and gameplay stay the same.
    /// </summary>
    /// <summary>
    /// Classic or RTX, chosen apart for the models (Model Look) and for the paint (Texture Look).
    /// Model: Classic = the original tile meshes (Tile X..O, 07_jump, arrows) and enemy models; RTX = the RTX Models slots.
    /// Texture: Classic = the original atlases (Tile materials); RTX = this theme's RTX materials (Floor, Fragile, Mover, Jump).
    /// </summary>
    public enum TileLook
    {
        RTX,
        Classic,
    }

    public enum ModelAnimMode
    {
        /// <summary>Plays over and over, all the time.</summary>
        Loop,
        /// <summary>Plays once as the ball comes near, timed by distance like the original obstacles.</summary>
        Approach,
    }

    /// <summary>One animation clip for one model (see <see cref="ModelAnimator"/>).</summary>
    [System.Serializable]
    public class ModelAnimation
    {
        [HideInInspector] public GameObject model; // the Models row's model (kept in step by TrackTheme)
        [Tooltip("The model's Blender clip. Empty = no animation. It fills itself from the FBX when the FBX has one " +
                 "(FBX Inspector > Animation tab > Import Animation on).")]
        public AnimationClip clip;
        public bool enabled = true;
        public ModelAnimMode mode = ModelAnimMode.Loop;
        [Tooltip("Loop only: playback speed (1 = as made in Blender, 0.5 = half). Approach ignores it: use Play Rows.")]
        public float speed = 1f;
        [Tooltip("Approach: rows before the ball arrives when the clip starts.")]
        public float startRows = 6f;
        [Tooltip("Approach only: rows of ball travel the whole clip takes. Bigger = slower.")]
        public float playRows = 4f;
    }

    /// <summary>Turn, size and nudge for one model in this theme only (e.g. an original Rolling Sky mesh lying on its side).</summary>
    [System.Serializable]
    public class ModelAdjust
    {
        [HideInInspector] public GameObject model; // the Models row's model (kept in step by TrackTheme)
        [Tooltip("Extra turn in degrees, applied before Fit Models (e.g. X -90 stands up a mesh lying down, Y 90 turns it sideways).")]
        public Vector3 rotation;
        [Tooltip("Size after fitting (1 = fills the space of the shape it replaces).")]
        public float scale = 1f;
        [Tooltip("Move after fitting, in tiles (Y up).")]
        public Vector3 offset;
        [Tooltip("On: the game's code moves the model (rises, slams, spins), right for old models without animation.\n" +
                 "Off: the model stays still on its tile and only its own Blender animation plays (Model Animations). " +
                 "The danger itself still follows the obstacle's normal timing.")]
        public bool codeMotion = true;
        [Tooltip("Material for this model only, instead of the theme's Model Material. Use it for old Rolling Sky models: " +
                 "their UVs fit the old enemy texture, not the new atlas, so the theme material paints them the wrong colours.")]
        public Material material;
        [Tooltip("On: keep the model's own materials (ignores Model Material). Old shaders show magenta in URP, convert them first.")]
        public bool keepOwnMaterial;

        /// <summary>Nothing changed from a model without an adjust.</summary>
        public bool IsDefault => rotation == Vector3.zero && Mathf.Approximately(scale, 1f) && offset == Vector3.zero &&
                                 codeMotion && material == null && !keepOwnMaterial;
    }

    /// <summary>
    /// One row of a theme's Models list: a model with everything this theme sets for it. Prop = a decoration
    /// (Level Editor Props tab, level.json props, Custom Obstacle models); Adjust = turn, size, nudge, material;
    /// Animation = its Blender clip and how it plays. Replaces the old separate Model Adjusts / Model Animations / Props lists.
    /// </summary>
    [System.Serializable]
    public class ThemeModel
    {
        [HideInInspector] public string name; // shown as the row's title in the Inspector
        public GameObject model;
        [Tooltip("Decoration (no collider) you place with the Level Editor's Props tab or in level.json \"props\".")]
        public bool prop;
        public ModelAdjust adjust = new ModelAdjust();
        public ModelAnimation animation = new ModelAnimation { clip = null };
    }

    [CreateAssetMenu(menuName = "RTX ON/Track Theme", fileName = "TrackTheme")]
    public class TrackTheme : ScriptableObject
    {
        [Header("Materials")]
        public Material floor;
        [Tooltip("RTX look only: the sides (edges) of the floor tiles. Empty = Floor on the sides too. The side gets " +
                 "U 0..1 along the tile and V 0..1 from top to bottom edge, so lower the material's Tiling Y (e.g. 0.2) " +
                 "for a texture that should not look squashed.")]
        public Material floorSide;
        [Tooltip("Fragile 0 (tile 3), and every fragile variant below that has no material of its own.")]
        public Material fragile;
        [Tooltip("Fragile 1 (tile 4). Empty = Fragile with an amber tint.")]
        public Material fragile1;
        [Tooltip("Fragile 2 (tile 5). Empty = Fragile with a mint tint.")]
        public Material fragile2;
        [Tooltip("Fragile checker (tile 6 and the checker gem, star and riser). Empty = Fragile with a pink tint.")]
        public Material fragileChecker;
        [Tooltip("Fragile row (tile 7, riser 16). Empty = Fragile with a blue tint.")]
        public Material fragileRow;
        [Tooltip("Fragile column (tile 8). Empty = Fragile with a violet tint.")]
        public Material fragileColumn;
        [Tooltip("RTX look only: the sides (edges) of every fragile tile, Full and Checker alike. Empty = the tile's own " +
                 "fragile material on the sides too.")]
        public Material fragileSide;
        [Tooltip("Hatched border around every fragile section, like the original: tiles that fall together share one border.")]
        public bool fragileBorders = true;
        public Material jump;
        public Material hazard;
        public Material hazardSafe;
        public Material gem;
        public Material star;
        [Tooltip("Gems and stars (01_pickup / 02_star): the original M_Fragile (filled by Hook Up Tile Types). Empty = Gem / Star.")]
        public Material pickupMaterial;
        public Material trunk;
        public Material foliage;
        public Material beamRed;
        public Material beamWhite;
        public Material ball;
        [Tooltip("Colour of the ball while this theme (look) is on: a look change fades the ball to it. " +
                 "Alpha 0 = keep the Ball material's own colour.")]
        public Color ballColor = new Color(1f, 1f, 1f, 0f);
        [Tooltip("Platform of a touch mover (Mover Sentuh, tiles 37-40).")]
        public Material mover;
        [Tooltip("Platform of an auto mover (tiles 41-48, 57-60).")]
        public Material moverAuto;
        [Tooltip("Glowing arrow on mover tiles.")]
        public Material moverArrow;

        [Header("Background (picture behind the track, original Background1..8)")]
        [Tooltip("Picture behind the whole track, fixed to the camera like the original game's background. A look change " +
                 "fades to the next look's picture. Empty = the sky of the Environments. RTX ON > Hook Up Tile Types fills " +
                 "an empty slot with Background<Tile Set>.")]
        public Texture background;
        [Tooltip("Colour and brightness of the picture (white = as painted). Bloom and tonemapping still apply to it.")]
        [ColorUsage(false, true)] public Color backgroundTint = Color.white;
        [Tooltip("Moves the picture up (+) or down (-), in screen heights.")]
        [Range(-0.5f, 0.5f)] public float backgroundY = 0f;
        [Tooltip("How the picture runs into the fog colour: Bottom = its lower part fades, Top = its upper part, Both = a " +
                 "transition look. None = the picture as it is. Level Editor > Theme > Gradient can change it per level.")]
        public BackgroundGradient backgroundGradient = BackgroundGradient.None;
        [Tooltip("Border around mover platforms: Auto = by neighbours, Always = every mover tile framed, Never = no border. " +
                 "Level Editor > Theme > Mover Edge can change it per level.")]
        public MoverEdge moverEdge = MoverEdge.Auto;

        /// <summary>
        /// Mover Edge = Never ("Without edge"): mover platforms look like plain floor (General / Floor material) and
        /// only their arrow shows they move. Auto / Always keep the Mover and Mover Auto materials.
        /// </summary>
        public bool MoverPlain => moverEdge == MoverEdge.Never;

        [Header("Midground (original blocks that rise beside and under the track)")]
        [Tooltip("The original midground blocks (MESH/GameObject midground1a, 2a, 3L, 3R, 4L, 4R). One block per row, left or " +
                 "right of the track, rising up out of the depths as it comes near, like the original. Empty = no midground. " +
                 "RTX ON > Hook Up Tile Types fills an empty list with midground<Tile Set>*.")]
        public GameObject[] midgroundModels = new GameObject[0];
        [Tooltip("Material for the blocks, used as it is. Empty = the texture of Tile Fragile (M_TileFragile, the atlas the " +
                 "original midground used), unlit, with the Mid Fog below.")]
        public Material midgroundMaterial;
        [Tooltip("Colour of the blocks' texture (white = as painted).")]
        [ColorUsage(false, true)] public Color midgroundTint = Color.white;
        [Tooltip("Mid Fog: the blocks fade to this colour with distance, separate from the scene fog (original 6_Midground: cyan).")]
        public Color midFogColor = new Color(0.07f, 0.85f, 0.88f);
        [Tooltip("Distance from the camera where the mid fog starts and where it is full (original: about 17 and 30).")]
        public float midFogStart = 17f, midFogEnd = 30f;
        [Tooltip("How far below the track the top of a risen block stands (original Massif 5, Sky 8, set 3 7.5, set 4 7.75).")]
        public float midgroundHeight = 5f;
        [Tooltip("How far further down a block starts before it rises (original 15, 15, 27, 21).")]
        public float midgroundRise = 15f;
        [Tooltip("Sideways distance of a block from the track centre, picked between these (original Massif 0..12, Sky 2..7).")]
        public float midgroundMinX = 0f, midgroundMaxX = 12f;
        [Tooltip("Gaps: 0 = a block on every row; higher = more rows with no block (original Massif 4, Sky 2, set 3 0, set 4 12).")]
        [Range(0, 16)] public int midgroundBlank = 4;

        [Header("Tile Type: the original floor meshes (RTX ON > Hook Up Tile Types fills these by name)")]
        [Tooltip("Classic: the original models (Tile X..O below, 07_jump, the original arrows, and the normal Pillar / Laser / Block models). " +
                 "RTX: the 3D models in RTX Models (Floor, Fragile, Jump, Mover...); an empty RTX slot = the built-in slab " +
                 "(or the original tile when Texture Look is Classic). The level's Theme Settings can change it per level.")]
        public TileLook modelLook = TileLook.Classic;
        [Tooltip("Classic: painted with the original atlas textures (Tile materials below), like the old game. RTX: painted with this " +
                 "theme's RTX materials (Floor, Fragile, Mover, Jump: glossy, neon inlays, reflections). Works on both Model Looks. " +
                 "The level's Theme Settings can change it per level.")]
        [FormerlySerializedAs("tileLook")]
        public TileLook textureLook = TileLook.RTX;
        [Tooltip("Real-time shadows from the sun / moon while this theme (look) is on. Off = flat like the original game, " +
                 "which had no shadows. SSAO (soft contact shading) stays.")]
        public bool shadows = true;
        [Tooltip("Which original texture set this theme uses (General<n>, Fragile<n>, FragileActive<n>, Mover<n>, MoverAuto<n>, Enemy<n>): " +
                 "1 = Massif, 2 = Sky, 3 = Forest, 4 = Morning, 5 = Volcano, 7 = Desert, 9 = Illusion. Left on 1, Hook Up Tile Types " +
                 "picks it from the theme's name, then puts those textures on the Tile materials below (and on Model Material).")]
        public int tileSet = 1;
        [Tooltip("01_X .. 15_O: the floor is one of these, picked and turned by its neighbours like the original game. " +
                 "Tile X empty = the built-in slabs are used instead.")]
        public GameObject tileX;
        public GameObject tileX1, tileX2a, tileX2b, tileX3, tileX4, tileT, tileT1a, tileT1b, tileT2, tileL, tileL1, tileI, tileD, tileO;
        [Tooltip("16_OD (top and front): floor tiles that animate on their own (flip, fly, slide in...) use it with 15_O " +
                 "(Tile O) turned upside down under it (bottom and back), a closed block with no hollow side. Empty = 15_O only.")]
        public GameObject tileOD;
        [Tooltip("07_jump: the original jump pad (state01 before, state02 lit after the ball jumped).")]
        public GameObject tileJump;
        [Tooltip("01_moveArrow / 02_moveAutoArrow: the original arrows (dark state1 before, lit state2 after the ball set them off).")]
        public GameObject tileMoverArrow, tileMoverAutoArrow;
        [Tooltip("Atlas materials of the tile set: General, Fragile, Fragile Active (a touched fragile), Mover, Mover Auto. " +
                 "Their Emission is the glow; the environment turns it up at night.")]
        public Material tileGeneral, tileFragile, tileFragileActive, tileMover, tileMoverAuto;

        [Header("RTX Models (used when Model Look = RTX; empty = the built-in slab / the normal model)")]
        [Tooltip("Plain floor. Also used by movers whose Mover / Mover Auto slot is empty, and by all movers when Mover Edge = Never.")]
        public GameObject floorModel;
        [Tooltip("Fragile tiles. Each fragile ID gets its own material (Fragile 1, Fragile Row...).")]
        public GameObject fragileModel;
        [Tooltip("Fragile Checker tiles. Empty = Fragile Model.")]
        public GameObject fragileCheckerModel;
        public GameObject jumpModel;
        [Tooltip("Mover platforms (touch). Empty = Floor Model.")]
        public GameObject moverModel;
        [Tooltip("Auto Mover platforms. Empty = Mover Model, else Floor Model.")]
        public GameObject moverAutoModel;
        [Tooltip("Pillar for Model Look RTX (each block, or one original-style pillar with anim/part0..3). Empty = Pillar Model.")]
        public GameObject pillarModelRTX;
        [Tooltip("Laser turret for Model Look RTX. Empty = Laser Model. With Pillar or Laser set here, the pillar laser (105) is " +
                 "built as this pillar with this turret on top.")]
        public GameObject laserModelRTX;
        [Tooltip("Block for Model Look RTX (block hazards, pillar blocks). Empty = Block Model.")]
        public GameObject blockModelRTX;

        [Header("Model overrides (leave empty for built-in shapes)")]
        public GameObject gemModel;
        public GameObject starModel;
        public GameObject riserModel;
        public GameObject crusherModel;
        public GameObject floaterModel;
        [Tooltip("Binary Mini (tiles 61-64, 73-76), original 06_enemyBinaryMini. Empty = uses Binary Slasher Model.")]
        public GameObject binaryMiniModel;
        [Tooltip("Full Binary that spins (65-68, 77-80), original 05_enemySlasher.")]
        [FormerlySerializedAs("binaryModel")]
        public GameObject binarySlasherModel;
        [Tooltip("Full Binary (65-68, 77-80) that pounds instead of spinning (original 04_enemyHammer). " +
                 "With Binary Slasher Model set too, each binary picks one of the two, like the original's hammers and slashers.")]
        public GameObject binaryHammerModel;
        public GameObject pillarModel;
        [Tooltip("Laser turret (original 16_enemyLaser): placed at its real size and animated like the original " +
                 "(eyelids open, shakes, fires at 5 rows). Empty = a small block.")]
        public GameObject laserModel;
        [Tooltip("Pillar laser, tile 105 (original 19_enemyPillarLaser): a 3-block pillar that rises, shakes and fires its " +
                 "own beam (state2) down its lane at 5 rows, like the original. Empty = a pillar with the laser turret on top.")]
        public GameObject pillarLaserModel;
        [Tooltip("Spotlight (original 17_enemySpotlight): stands on its tile at its real size and swings with the beam " +
                 "(±30°, lights up at 15 rows). Empty = a hanging lamp beam.")]
        public GameObject spotlightModel;
        public GameObject rollerModel;
        public GameObject treeModel;
        public GameObject palmModel;
        [Tooltip("Extra Riser looks, each with its own tile ID: Element 0 = tile 151, Element 1 = 152, Element 2 = 153 " +
                 "(e.g. original 10/11 small trees, 13 small pyramid). The normal riser tiles (13-18) always use Riser Model.")]
        public GameObject[] riserVariants = new GameObject[0];
        [Tooltip("Extra Tree looks, each with its own tile ID: Element 0 = tile 154, Element 1 = 155, Element 2 = 156 " +
                 "(e.g. original 08 tower, 09 tall tree). The normal tree tiles (21-24) always use Tree Model.")]
        public GameObject[] treeVariants = new GameObject[0];
        [Tooltip("Crown pickup (tile 150). Empty = a built-in gold crown.")]
        public GameObject crownModel;
        [Tooltip("Finish line on the last row made only of tile 110 (original 03_finish). Placed at its real size, with the floor material; its blocks (anim) use Tile Fragile Active.")]
        public GameObject finishModel;
        [Tooltip("Portal on the tile 113s of the last row (original 04_portal). Placed at its real size, round part with the Model Material (M_TileEnemy), frame (portalOuter) with the General Model Material (M_TileGeneral).")]
        public GameObject portalModel;
        [Tooltip("Used for any block hazard that has no model of its own, and for each block of a pillar when Pillar Model is empty. " +
                 "Gets the floor material, like Finish, Portal and Pillar.")]
        public GameObject blockModel;
        [Tooltip("Trigger Object (tile 169): one object with one Blender clip. It waits (still, or the clip's first Frames) until its " +
                 "trigger tile is touched, then the rest of the clip plays and its hitbox (one box per mesh part) moves with it. " +
                 "Empty = a glowing bar that slams down. A model without a clip goes down whole.")]
        [FormerlySerializedAs("gateModel")] public GameObject triggerObjectModel;
        [Tooltip("Trigger Object Model's clip. Empty = the clip set for it in Models (Animation > Clip).")]
        [FormerlySerializedAs("gateDownClip")] public AnimationClip triggerObjectClip;
        [Tooltip("Power-up cubes, each with its own tile ID: Element 0 = 160 Fly Jump, 1 = 161 Short Jump, 2 = 162 Bounce, " +
                 "3 = 163 Switch, 4 = 164 Invisible, 5 = 165 Speed Up, 6 = 166 Speed Down, 7 = 167 Hide Objects, 8 = 168 Dialogue. Empty = a glowing cube in the power-up's colour.")]
        public GameObject[] powerUpModels = new GameObject[0];

        [Tooltip("Scale and centre each model so it fits the space of the shape it replaces.")]
        public bool fitModels = true;
        [Tooltip("Turn every model 180 degrees so its front faces the ball. Blender's Front view (-Y) arrives in Unity facing forward (+Z), away from the ball and camera. Untick if your models already face -Z.")]
        public bool modelsFaceBall = true;
        [Tooltip("If set, every hazard model and prop uses this material, e.g. a URP Lit material with your EnemyTexture atlas. " +
                 "Finish, Portal, Pillar, Pillar Laser and Block models use the floor material instead (General Model Material).")]
        public Material modelMaterial;
        [Tooltip("Build Gameplay Scene fills an empty Model Material from the first texture named *Enemy*. Untick to keep the models' own materials.")]
        public bool autoModelMaterial = true;

        [Header("Models: one row per model (Prop, Adjust, Animation), this theme only")]
        [Tooltip("Every model this theme sets something for: tick Prop for decorations, open Adjust to turn / size / move it " +
                 "or give it its own material, open Animation for its Blender clip (Loop or Approach). " +
                 "RTX ON > Add Selected Models As Props and Find Model Animations fill it too.")]
        public List<ThemeModel> models = new List<ThemeModel>();

        // The old separate lists: read once into Models (see MoveOldLists), then left empty.
        [HideInInspector] public List<ModelAdjust> modelAdjusts = new List<ModelAdjust>();
        [HideInInspector] public List<ModelAnimation> modelAnimations = new List<ModelAnimation>();
        [HideInInspector] public List<GameObject> props = new List<GameObject>();

        [Header("Environments (first = start). Level themes switch between these by name.")]
        public List<EnvironmentPreset> environments = new List<EnvironmentPreset>();

        [Header("Light colours")]
        [ColorUsage(false, true)] public Color spotlightColor = new Color(1f, 0.95f, 0.8f) * 6f;
        [ColorUsage(false, true)] public Color laserLightColor = new Color(1f, 0.15f, 0.1f) * 4f;

        /// <summary>The animation set up for this model (slot model or prop), or null.</summary>
        public ModelAnimation FindAnimation(GameObject model)
        {
            var e = Entry(model);
            return e != null && e.animation != null && e.animation.clip != null && e.animation.enabled ? e.animation : null;
        }

        /// <summary>The Models row of <paramref name="model"/>, or null.</summary>
        public ThemeModel Entry(GameObject model)
        {
            if (model == null) return null;
            MoveOldLists();
            foreach (var e in models)
                if (e != null && e.model == model) return e;
            return null;
        }

        /// <summary>The Models row of <paramref name="model"/>, added when it has none.</summary>
        public ThemeModel AddEntry(GameObject model)
        {
            var e = Entry(model);
            if (e != null || model == null) return e;
            e = new ThemeModel { model = model, name = model.name };
            Sync(e);
            models.Add(e);
            return e;
        }

        /// <summary>The models ticked Prop, in list order.</summary>
        public IEnumerable<GameObject> PropModels()
        {
            MoveOldLists();
            foreach (var e in models)
                if (e != null && e.prop && e.model != null) yield return e.model;
        }

        /// <summary>
        /// Themes made before the Models list kept three lists (Model Adjusts, Model Animations, Props): their rows
        /// move into Models, one row per model (the enabled clip when a model had several). Runs once per theme.
        /// </summary>
        void MoveOldLists()
        {
            bool any = (modelAdjusts != null && modelAdjusts.Count > 0) || (modelAnimations != null && modelAnimations.Count > 0) ||
                       (props != null && props.Count > 0);
            if (!any) return;
            models ??= new List<ThemeModel>();
            if (modelAdjusts != null)
                foreach (var a in modelAdjusts)
                    if (a != null && a.model != null) Row(a.model).adjust = a;
            if (modelAnimations != null)
            {
                foreach (var a in modelAnimations) // enabled clips first, so they win
                    if (a != null && a.model != null && a.enabled && a.clip != null && Row(a.model).animation.clip == null) Row(a.model).animation = a;
                foreach (var a in modelAnimations)
                    if (a != null && a.model != null && a.clip != null && Row(a.model).animation.clip == null) Row(a.model).animation = a;
            }
            if (props != null)
                foreach (var p in props)
                    if (p != null) Row(p).prop = true;
            modelAdjusts?.Clear();
            modelAnimations?.Clear();
            props?.Clear();
            foreach (var e in models) Sync(e);
#if UNITY_EDITOR
            var self = this;
            UnityEditor.EditorApplication.delayCall += () => { if (self != null) UnityEditor.EditorUtility.SetDirty(self); };
#endif
        }

        ThemeModel Row(GameObject model)
        {
            foreach (var e in models)
                if (e != null && e.model == model) return e;
            var row = new ThemeModel { model = model };
            models.Add(row);
            return row;
        }

        /// <summary>Keeps a row's title, sub-entries and their model in step.</summary>
        static void Sync(ThemeModel e)
        {
            if (e == null) return;
            e.adjust ??= new ModelAdjust();
            e.animation ??= new ModelAnimation();
            e.adjust.model = e.model;
            e.animation.model = e.model;
            e.name = e.model != null ? e.model.name + (e.prop ? "  (prop)" : "") + (e.animation.clip != null ? "  (anim)" : "") : "(no model)";
        }

        void OnEnable() => MoveOldLists();

#if UNITY_EDITOR
        void OnValidate()
        {
            MoveOldLists();
            foreach (var e in models)
            {
                if (e == null) continue;
                Sync(e);
                // An empty Clip takes the FBX's first clip, so Find Model Animations is not needed.
                if (e.model != null && e.animation.clip == null) e.animation.clip = FirstClip(e.model);
                Sync(e);
            }
        }

        /// <summary>Editor only: the first animation clip inside the model's FBX, or null.</summary>
        public static AnimationClip FirstClip(GameObject model)
        {
            string path = UnityEditor.AssetDatabase.GetAssetPath(model);
            if (string.IsNullOrEmpty(path)) return null;
            foreach (var asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__")) return clip;
            return null;
        }
#endif

        /// <summary>
        /// Material for the models built from the floor's atlas (Finish, Portal, Pillar, Pillar Laser, Block): the
        /// floor material (M_Floor) in the RTX look, the General atlas (M_TileGeneral) in the Classic look; either one
        /// when the other is empty, else the Model Material.
        /// </summary>
        public Material GeneralModelMaterial
        {
            get
            {
                var first = ClassicTiles ? tileGeneral : floor;
                var second = ClassicTiles ? floor : tileGeneral;
                return first != null ? first : second != null ? second : modelMaterial;
            }
        }

        /// <summary>
        /// Parts of the original landmarks that use another atlas than the rest (Adrian): the portal's frame
        /// (portalOuter) and the finish pillars (HighscorePillars) take the General Model Material (M_Floor in RTX, M_TileGeneral in Classic) while its round part keeps the
        /// Model Material (M_TileEnemy); the finish wall blocks ("anim" of 03_finish) take Tile Fragile Active
        /// (M_TileFragileActive), whose atlas holds their looks. Each block state mesh points its UVs at its own part
        /// of that atlas, so switching states switches the picture.
        /// </summary>
        public void ApplyLandmarkParts(GameObject instance, GameObject model)
        {
            if (model == null) return;
            // Floor (M_Floor) in the RTX look, Tile General (M_TileGeneral) in Classic, like Finish and Pillar.
            var general = GeneralModelMaterial;
            if (model == portalModel) SetMaterial(instance, "portalOuter", general);
            if (model == finishModel)
            {
                SetMaterial(instance, "HighscorePillars", general);
                SetMaterial(instance, "anim", tileFragileActive);
            }
        }

        static void SetMaterial(GameObject instance, string part, Material m)
        {
            if (m == null) return;
            foreach (var t in instance.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != part) continue;
                foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = m;
                    r.sharedMaterials = mats;
                }
            }
        }

        /// <summary>
        /// Paints every material slot of a placed model with <paramref name="m"/>. A <paramref name="general"/> model
        /// (pillar, pillar laser, finish, block) keeps the Enemy texture where the original used 5_Enemy: those slots
        /// take the Model Material. 19_enemyPillarLaser's blocks are half 1A_General (pillar), half 5_Enemy (laser).
        /// </summary>
        public void PaintModel(GameObject v, Material m, bool general)
        {
            foreach (var r in v.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    bool enemySlot = general && mats[i] != null && mats[i].name.Contains("Enemy") && modelMaterial != null;
                    var use = enemySlot ? modelMaterial : m;
                    if (use != null) mats[i] = use;
                }
                r.sharedMaterials = mats;
            }
        }

        /// <summary>True for the slots that take <see cref="GeneralModelMaterial"/> instead of the Model Material.</summary>
        public bool UsesGeneralMaterial(GameObject model) =>
            model != null && (model == finishModel || model == pillarModel || model == pillarLaserModel || model == blockModel);

        /// <summary>Riser look of a tile: 0 = Riser Model, 1-3 = Riser Variants element 0-2 (tiles 151-153). An empty variant falls back to Riser Model.</summary>
        /// <summary>
        /// The model this theme puts on an Object-layer tile ID (13 riser, 21 tree, 62 binary...), like TrackBuilder
        /// does, or null when the tile uses its built-in shape. The level editor names its buttons after it.
        /// </summary>
        public GameObject ObstacleModel(int id)
        {
            var spec = TileCatalog.Decode(id);
            switch (spec.item)
            {
                case ItemKind.Riser: return RiserModel(spec.param);
                case ItemKind.Crusher:
                case ItemKind.CrusherSafe: return crusherModel;
                case ItemKind.Tree: return TreeModel(spec.param);
                case ItemKind.Palm: return palmModel != null ? palmModel : treeModel;
                case ItemKind.RollerLeft:
                case ItemKind.RollerRight: return rollerModel;
                case ItemKind.FloaterGround:
                case ItemKind.FloaterAir: return floaterModel;
                case ItemKind.BinaryLeft:
                case ItemKind.BinaryRight:
                    if (spec.param == TileCatalog.BinaryHammer) return binaryHammerModel != null ? binaryHammerModel : binarySlasherModel;
                    if (spec.param >= TileCatalog.BinarySlasher) return binarySlasherModel != null ? binarySlasherModel : binaryHammerModel;
                    return binaryMiniModel != null ? binaryMiniModel : binarySlasherModel;
                case ItemKind.Pillar:
                case ItemKind.PillarSafe: return pillarModel;
                case ItemKind.LaserPillar: return pillarLaserModel != null ? pillarLaserModel : pillarModel;
                case ItemKind.Spotlight: return spotlightModel;
                case ItemKind.Laser: return laserModel;
                default: return null;
            }
        }

        public GameObject RiserModel(int variant) => Variant(riserModel, riserVariants, variant);

        /// <summary>Tree look of a tile: 0 = Tree Model, 1-3 = Tree Variants element 0-2 (tiles 154-156). An empty variant falls back to Tree Model.</summary>
        public GameObject TreeModel(int variant) => Variant(treeModel, treeVariants, variant);

        /// <summary>Model of power-up <paramref name="index"/> (0 = Fly Jump ... 6 = Speed Down), or null for the built-in cube.</summary>
        public GameObject PowerUpModel(int index) =>
            powerUpModels != null && index >= 0 && index < powerUpModels.Length ? powerUpModels[index] : null;

#if UNITY_EDITOR
        /// <summary>
        /// Editor only: adds every TrackTheme in the project that <paramref name="list"/> is missing, so a theme made
        /// after the scene was built (e.g. SkyTheme) shows up at Play without running the build menus again.
        /// </summary>
        public static void AddProjectThemes(List<TrackTheme> list)
        {
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:TrackTheme"))
            {
                var t = UnityEditor.AssetDatabase.LoadAssetAtPath<TrackTheme>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                if (t != null && !list.Contains(t)) list.Add(t);
            }
            list.RemoveAll(t => t == null);
            list.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));
        }
#endif

        static GameObject Variant(GameObject main, GameObject[] list, int variant)
        {
            if (variant > 0 && list != null && variant <= list.Length && list[variant - 1] != null) return list[variant - 1];
            return main;
        }

        /// <summary>The model's Adjust, or null when it changes nothing.</summary>
        public ModelAdjust FindAdjust(GameObject model)
        {
            var e = Entry(model);
            return e != null && e.adjust != null && !e.adjust.IsDefault ? e.adjust : null;
        }

        /// <summary>A model ticked Prop, by name (level.json "props").</summary>
        public GameObject FindProp(string modelName)
        {
            if (string.IsNullOrEmpty(modelName)) return null;
            foreach (var p in PropModels())
                if (string.Equals(p.name, modelName, System.StringComparison.OrdinalIgnoreCase)) return p;
            return null;
        }

        /// <summary>
        /// Copy with its own material instances, so play mode can animate colours and glow
        /// without touching the material assets.
        /// </summary>
        public TrackTheme CreateRuntimeCopy()
        {
            var t = Instantiate(this);
            t.name = name + " (runtime)";
            copies.Clear(); // one copy per material: slots that share a material keep sharing it
            t.floor = Copy(floor);
            t.floorSide = Copy(floorSide);
            t.fragile = Copy(fragile);
            t.fragile1 = Copy(fragile1);
            t.fragile2 = Copy(fragile2);
            t.fragileChecker = Copy(fragileChecker);
            t.fragileRow = Copy(fragileRow);
            t.fragileColumn = Copy(fragileColumn);
            t.fragileSide = Copy(fragileSide);
            t.jump = Copy(jump);
            t.hazard = Copy(hazard);
            t.hazardSafe = Copy(hazardSafe);
            t.gem = Copy(gem);
            t.star = Copy(star);
            t.pickupMaterial = Copy(pickupMaterial);
            t.trunk = Copy(trunk);
            t.foliage = Copy(foliage);
            t.beamRed = Copy(beamRed);
            t.beamWhite = Copy(beamWhite);
            t.modelMaterial = Copy(modelMaterial);
            t.mover = Copy(mover);
            t.moverAuto = Copy(moverAuto);
            t.moverArrow = Copy(moverArrow);
            t.tileGeneral = Copy(tileGeneral);
            t.tileFragile = Copy(tileFragile);
            t.tileFragileActive = Copy(tileFragileActive);
            t.tileMover = Copy(tileMover);
            t.tileMoverAuto = Copy(tileMoverAuto);
            // Share the asset's entries (not copies), so changing Mode, Speed or rows in the Inspector during Play shows at once.
            MoveOldLists();
            t.models = models;
            // Shared too (the environment director only reads them), so colour edits in the Inspector show during Play.
            t.environments = environments;
            copies.Clear();
            return t;
        }

        /// <summary>Floors use the original Tile Type meshes (Tile X is set).</summary>
        public bool HasTileTypes => tileX != null;

        /// <summary>Painted with the original atlases (Texture Look = Classic) instead of the RTX materials.</summary>
        public bool ClassicTiles => HasTileTypes && textureLook == TileLook.Classic;

        /// <summary>The original models (Model Look = Classic): Tile Type meshes, 07_jump, original arrows.</summary>
        public bool ClassicModels => HasTileTypes && modelLook == TileLook.Classic;

        /// <summary>
        /// The RTX Models slot of a floor (Model Look = RTX), or null for the built-in slab. Movers fall back to
        /// Floor Model; with Mover Edge = Never they are plain floor.
        /// </summary>
        public GameObject FloorModelFor(FloorKind kind, FragileKind fragileKind, MoverKind mover)
        {
            if (kind == FloorKind.Jump || kind == FloorKind.SuperJump) return jumpModel;
            if (kind == FloorKind.Fragile)
                return fragileKind == FragileKind.Checker && fragileCheckerModel != null ? fragileCheckerModel : fragileModel;
            if (mover != MoverKind.None && !MoverPlain)
            {
                if (mover == MoverKind.Auto && moverAutoModel != null) return moverAutoModel;
                if (moverModel != null) return moverModel;
            }
            return floorModel;
        }

        /// <summary>The original atlas of a floor (Texture Look = Classic): General, Mover, Mover Auto or Fragile.</summary>
        public Material TileAtlasFor(FloorKind kind, MoverKind mover)
        {
            if (kind == FloorKind.Fragile && tileFragile != null) return tileFragile;
            if (!MoverPlain && mover == MoverKind.Touch && tileMover != null) return tileMover;
            if (!MoverPlain && mover == MoverKind.Auto && tileMoverAuto != null) return tileMoverAuto;
            return tileGeneral;
        }

        /// <summary>Any of the RTX enemy slots (Pillar, Laser, Block) is set.</summary>
        public bool HasRtxEnemyModels => pillarModelRTX != null || laserModelRTX != null || blockModelRTX != null;

        /// <summary>
        /// Model Look = RTX on a runtime copy: the set RTX enemy slots replace Pillar, Laser and Block Model. With an RTX
        /// pillar or laser, the pillar laser (105) is that pillar with that turret on top (Pillar Laser Model cleared).
        /// </summary>
        public void UseRtxModels(bool keepBlock)
        {
            if (pillarModelRTX != null || laserModelRTX != null) pillarLaserModel = null;
            if (pillarModelRTX != null) pillarModel = pillarModelRTX;
            if (laserModelRTX != null) laserModel = laserModelRTX;
            if (blockModelRTX != null && !keepBlock) blockModel = blockModelRTX;
        }

        /// <summary>
        /// A material painted with an original tile atlas (a Tile material, or a copy of one with a General / Fragile /
        /// Mover texture). It needs the meshes' own atlas UVs; on per-cell UVs the whole atlas would show on every cell.
        /// </summary>
        public bool IsTileAtlas(Material m)
        {
            if (m == null) return false;
            if (m == tileGeneral || m == tileFragile || m == tileFragileActive || m == tileMover || m == tileMoverAuto) return true;
            var tex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
            if (tex == null) return false;
            var n = tex.name;
            foreach (var prefix in new[] { "General", "FragileActive", "Fragile", "MoverAuto", "Mover" })
                if (n.Length > prefix.Length && n.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase) && char.IsDigit(n[prefix.Length]))
                    return true;
            return false;
        }

        /// <summary>The original mesh for a shape; a missing one falls back to Tile X.</summary>
        /// <summary>
        /// The mesh of a floor tile that animates on its own (<see cref="TileShapes.MovesAlone"/>): Tile OD, else Tile O
        /// (it joins no neighbour, so its shape is O). <paramref name="rotations"/> becomes 0 for Tile OD.
        /// </summary>
        public GameObject AloneTileModel(TileShape shape, ref int rotations)
        {
            if (tileOD == null) return TileModel(shape);
            rotations = 0;
            return tileOD;
        }

        public GameObject TileModel(TileShape shape)
        {
            GameObject m;
            switch (shape)
            {
                case TileShape.X1: m = tileX1; break;
                case TileShape.X2a: m = tileX2a; break;
                case TileShape.X2b: m = tileX2b; break;
                case TileShape.X3: m = tileX3; break;
                case TileShape.X4: m = tileX4; break;
                case TileShape.T: m = tileT; break;
                case TileShape.T1a: m = tileT1a; break;
                case TileShape.T1b: m = tileT1b; break;
                case TileShape.T2: m = tileT2; break;
                case TileShape.L: m = tileL; break;
                case TileShape.L1: m = tileL1; break;
                case TileShape.I: m = tileI; break;
                case TileShape.D: m = tileD; break;
                case TileShape.O: m = tileO; break;
                default: m = tileX; break;
            }
            return m != null ? m : tileX;
        }

        /// <summary>The materials a theme uses on the track (for the environment's neon glow).</summary>
        public IEnumerable<Material> TrackMaterials()
        {
            yield return floor; yield return fragile; yield return jump; yield return hazard; yield return gem;
            yield return star; yield return pickupMaterial; yield return mover; yield return moverAuto; yield return moverArrow;
            yield return fragile1; yield return fragile2; yield return fragileChecker; yield return fragileRow; yield return fragileColumn;
            yield return tileGeneral; yield return tileFragile; yield return tileFragileActive; yield return tileMover; yield return tileMoverAuto;
        }

        /// <summary>The material slot of a fragile variant (Fragile 0 = Fragile); null when that slot is empty.</summary>
        public Material FragileMaterial(FragileKind kind) => kind switch
        {
            FragileKind.Fragile1 => fragile1,
            FragileKind.Fragile2 => fragile2,
            FragileKind.Checker => fragileChecker,
            FragileKind.Row => fragileRow,
            FragileKind.Column => fragileColumn,
            _ => fragile,
        };

        static readonly Dictionary<Material, Material> copies = new Dictionary<Material, Material>();

        static Material Copy(Material m)
        {
            if (m == null) return null;
            if (!copies.TryGetValue(m, out var c)) copies[m] = c = new Material(m);
            return c;
        }
    }
}
