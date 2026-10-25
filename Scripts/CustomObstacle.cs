using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RTXOn
{
    /// <summary>One 1x1x1 box of a custom obstacle's hitbox, in tiles from its cell: x = lanes (+ right), y = up, z = rows (+ forward).</summary>
    [Serializable]
    public struct CollisionBox
    {
        public int x, y, z;

        public CollisionBox(int x, int y, int z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
    }

    /// <summary>What touching a custom obstacle does.</summary>
    public enum CustomTouch { Kill, Nothing }

    /// <summary>A picture obstacle's shape (Model = no picture: the project model, OBJ or plain block).</summary>
    public enum CustomShape { Model, Cube, Plane, Circle }

    /// <summary>Where a custom obstacle is painted: an obstacle (Object tab), a floor tile (Tiles tab) or a pickup (Collect tab).</summary>
    public enum CustomLayer { Object, Tile, Collect }

    /// <summary>A pickup custom obstacle (layer Collect): what taking it counts as.</summary>
    public enum CustomCollect { Gem, Crown, Nothing }

    /// <summary>How a custom obstacle's approach animation moves from its start pose to its rest pose.</summary>
    public enum CustomEase { Linear, Smooth, Bounce, Overshoot }

    /// <summary>
    /// A Custom Obstacle made in the Level Editor (the + button in the Object tab), like the fan editor's custom enemies:
    /// a model (a project model by name, or an OBJ file kept inside), a hitbox of 1x1x1 boxes, what touching it does,
    /// a sound and an animation. Painted with its Enemy ID (200-999) on the Object layer. Saved as one ".rsrt" file
    /// (this class as JSON) in the editor's obstacle library, and inside every level.json that uses it ("customs"),
    /// so a level plays anywhere without the library.
    /// </summary>
    [Serializable]
    public class CustomObstacle
    {
        public const int FirstId = 200, LastId = 999;
        public const string FileExtension = ".rsrt";
        public const string FormatName = "rsrt-obstacle";

        public string format = FormatName;
        public int version = 1;
        public int id = FirstId;
        public string name = "Obstacle";

        [Header("Model")]
        [Tooltip("A project model by name (RTX ON > Add Selected Models As Custom Models, or a theme's Props). Empty = the OBJ below, or a plain block.")]
        public string model = "";
        [Tooltip("File name of the OBJ mesh kept in obj.")]
        public string objName = "";
        [Tooltip("Wavefront OBJ text, used when model is empty.")]
        public string obj = "";
        [Tooltip("PNG as base64: the OBJ's texture.")]
        public string texture = "";
        [Tooltip("PNG as base64: the picture in the palette and the grid.")]
        public string icon = "";
        [Tooltip("Picture obstacle: the Picture on a Cube, a standing Plane or a Circle. Model = no picture (model, OBJ or block).")]
        public CustomShape shape = CustomShape.Model;
        [Tooltip("PNG / JPG / GIF file as base64, shown on the shape. An animated GIF plays.")]
        public string image = "";
        public string imageName = "";
        [Tooltip("Project model: keep its own materials. Off = the theme's Model Material.")]
        public bool ownMaterial;
        public Vector3 rotation;
        public Vector3 scale = Vector3.one;
        [Tooltip("Move in tiles (Y up), from the bottom centre of the cell.")]
        public Vector3 offset;

        [Header("Collision")]
        [Tooltip("Off = no hitbox, the ball rolls through it. Always off when the model plays a Blender clip " +
                 "(theme Models > Animation > Clip): the boxes would not follow the moving model.")]
        public bool collision = true;
        [Tooltip("Object = obstacle (hitbox boxes, Kill or Nothing). Tile = floor the ball rolls on (Collision off = a hole). " +
                 "Collect = a pickup the ball takes by touching it.")]
        public CustomLayer layer = CustomLayer.Object;
        [Tooltip("Layer Collect: taking it counts as a Gem (GEMS x/N), a Crown (CROWNS x/N) or nothing (just for fun).")]
        public CustomCollect collectAs = CustomCollect.Gem;
        public List<CollisionBox> boxes = new List<CollisionBox> { new CollisionBox(0, 0, 0) };

        [Header("Event")]
        public CustomTouch touch = CustomTouch.Kill;
        [Tooltip("Cannot hurt the ball while it flies from a jump pad (like risers and pillars).")]
        public bool jumpSafe;
        [Tooltip("Sfx played when the ball comes near, -1 = none.")]
        public int sound = -1;
        [Tooltip("Own sound (WAV / OGG / MP3 file as base64), played instead of Sound when the ball comes near.")]
        public string soundFile = "";
        public string soundFileName = "";
        public float soundRows = 6f;
        public float soundVolume = 0.8f;
        [Tooltip("Dialogue: a speech bubble above the obstacle with this text, going along with it as it moves. Empty = none.")]
        public string dialogue = "";
        [Tooltip("The bubble pops up when the ball is this many rows away.")]
        public float dialogueRows = 8f;
        public float dialogueSeconds = 2f;
        [Tooltip("Height of the bubble above the obstacle's cell, in tiles.")]
        public float dialogueHeight = 1.6f;
        [Tooltip("A power-up (160-168) riding on the obstacle: it moves along with it. 0 = none.")]
        public int powerUp;
        [Tooltip("Height of that power-up above the obstacle's cell, in tiles.")]
        public float powerUpHeight = 1.2f;

        // Animation: only for models without a Blender clip (a clip plays instead of these).
        [Header("Animation")]
        [Tooltip("Moves from the From pose to its place while the ball comes from Start Rows to End Rows away.")]
        public bool approach;
        public float startRows = 8f, endRows = 3f;
        public Vector3 fromOffset = new Vector3(0f, -1.5f, 0f);
        public Vector3 fromRotation;
        public float fromScale = 1f;
        public CustomEase ease = CustomEase.Smooth;
        [Tooltip("Turns all the time, degrees per second around X, Y, Z.")]
        public Vector3 spin;
        [Tooltip("Floats up and down this far (tiles), one wave every Bob Rows of ball travel. 0 = off.")]
        public float bobHeight;
        public float bobRows = 4f;

        public static bool IsCustomId(int id) => id >= FirstId && id <= LastId;

        public bool HasObj => shape == CustomShape.Model && string.IsNullOrEmpty(model) && !string.IsNullOrEmpty(obj);
        public bool IsPicture => shape != CustomShape.Model;
        public bool HasOwnSound => !string.IsNullOrEmpty(soundFile);
        public bool HasSound => sound >= 0 || HasOwnSound;

        /// <summary>The text of a .rsrt file.</summary>
        public string ToJson(bool pretty = true) => JsonUtility.ToJson(this, pretty);

        /// <summary>Reads a .rsrt file (or one "customs" entry of a level.json). Null when it is not a custom obstacle.</summary>
        public static CustomObstacle FromJson(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            CustomObstacle c;
            try { c = JsonUtility.FromJson<CustomObstacle>(text); }
            catch (Exception) { return null; }
            if (c == null || (!string.IsNullOrEmpty(c.format) && c.format != FormatName)) return null;
            c.Normalize();
            return c;
        }

        public CustomObstacle Clone() => FromJson(ToJson(false));

        /// <summary>True when both have the same settings (same file text).</summary>
        public bool SameAs(CustomObstacle other) => other != null && ToJson(false) == other.ToJson(false);

        /// <summary>Puts every value in its allowed range (files typed by hand, older versions).</summary>
        public void Normalize()
        {
            format = FormatName;
            if (!IsCustomId(id)) id = FirstId;
            if (string.IsNullOrWhiteSpace(name)) name = "Obstacle " + id;
            model ??= "";
            objName ??= "";
            obj ??= "";
            texture ??= "";
            icon ??= "";
            image ??= "";
            imageName ??= "";
            soundFile ??= "";
            soundFileName ??= "";
            if (!Enum.IsDefined(typeof(CustomShape), shape)) shape = CustomShape.Model;
            if (scale == Vector3.zero) scale = Vector3.one;
            boxes ??= new List<CollisionBox>();
            if (!Enum.IsDefined(typeof(CustomTouch), touch)) touch = CustomTouch.Kill;
            if (!Enum.IsDefined(typeof(CustomLayer), layer)) layer = CustomLayer.Object;
            if (!Enum.IsDefined(typeof(CustomCollect), collectAs)) collectAs = CustomCollect.Gem;
            if (!Enum.IsDefined(typeof(CustomEase), ease)) ease = CustomEase.Smooth;
            if (sound < -1 || sound >= Enum.GetValues(typeof(Sfx)).Length) sound = -1;
            soundRows = Mathf.Clamp(soundRows, 0f, 100f);
            soundVolume = Mathf.Clamp01(soundVolume);
            dialogue = (dialogue ?? "").Trim();
            if (dialogue.Length > PartArgs.MaxDialogueLength) dialogue = dialogue.Substring(0, PartArgs.MaxDialogueLength);
            dialogueRows = Mathf.Clamp(dialogueRows, 0f, 100f);
            dialogueSeconds = Mathf.Clamp(dialogueSeconds, PartArgs.MinShortSeconds, PartArgs.MaxShortSeconds);
            dialogueHeight = Mathf.Clamp(dialogueHeight, -2f, 10f);
            if (!PartArgs.IsPowerUp(PartArgs.KindOf(powerUp))) powerUp = 0;
            powerUpHeight = Mathf.Clamp(powerUpHeight, 0f, 10f);
            startRows = Mathf.Clamp(startRows, 0f, 100f);
            endRows = Mathf.Clamp(endRows, 0f, 100f);
            if (fromScale < 0f) fromScale = 0f;
            bobRows = Mathf.Max(0.5f, bobRows);
        }

        /// <summary>0..1 through the approach animation for a ball <paramref name="ahead"/> rows away, eased.</summary>
        public float ApproachAt(float ahead)
        {
            if (!approach) return 1f;
            float from = Mathf.Max(startRows, endRows), to = Mathf.Min(startRows, endRows);
            float v = from - to < 0.001f ? (ahead <= to ? 1f : 0f) : Mathf.Clamp01((from - ahead) / (from - to));
            return Ease(ease, v);
        }

        public static float Ease(CustomEase ease, float v)
        {
            switch (ease)
            {
                case CustomEase.Smooth: return v * v * (3f - 2f * v);
                case CustomEase.Bounce:
                {
                    const float n = 7.5625f, d = 2.75f;
                    if (v < 1f / d) return n * v * v;
                    if (v < 2f / d) { v -= 1.5f / d; return n * v * v + 0.75f; }
                    if (v < 2.5f / d) { v -= 2.25f / d; return n * v * v + 0.9375f; }
                    v -= 2.625f / d;
                    return n * v * v + 0.984375f;
                }
                case CustomEase.Overshoot:
                {
                    const float c1 = 1.70158f, c3 = c1 + 1f;
                    float w = v - 1f;
                    return 1f + c3 * w * w * w + c1 * w * w;
                }
                default: return v;
            }
        }
    }

    /// <summary>
    /// Every custom obstacle the editor and the game know, by Enemy ID: the library (.rsrt files in the Obstacles
    /// folder next to the levels) plus the ones the open level carries. Also keeps their icons, OBJ meshes and textures.
    /// </summary>
    public static class CustomObstacles
    {
        static readonly Dictionary<int, CustomObstacle> defs = new Dictionary<int, CustomObstacle>();
        static readonly Dictionary<int, Sprite> icons = new Dictionary<int, Sprite>();
        static readonly Dictionary<int, Mesh> meshes = new Dictionary<int, Mesh>();
        static readonly Dictionary<int, Texture2D> textures = new Dictionary<int, Texture2D>();

        /// <summary>Goes up on every change, so views know to build custom obstacles again.</summary>
        public static int Version { get; private set; }

        public static bool TryGet(int id, out CustomObstacle def) => defs.TryGetValue(id, out def);

        public static CustomObstacle Get(int id) => defs.TryGetValue(id, out var d) ? d : null;

        /// <summary>All of them, by ID.</summary>
        public static List<CustomObstacle> All()
        {
            var list = new List<CustomObstacle>(defs.Values);
            list.Sort((a, b) => a.id.CompareTo(b.id));
            return list;
        }

        /// <summary>Adds or replaces the obstacle with this ID.</summary>
        public static void Set(CustomObstacle def)
        {
            if (def == null) return;
            def.Normalize();
            if (defs.TryGetValue(def.id, out var old) && old.SameAs(def)) return;
            Forget(def.id);
            defs[def.id] = def;
            Version++;
        }

        public static void Remove(int id)
        {
            if (!defs.ContainsKey(id)) return;
            Forget(id);
            defs.Remove(id);
            Version++;
        }

        static void Forget(int id)
        {
            if (icons.TryGetValue(id, out var s) && s != null) { UnityEngine.Object.Destroy(s.texture); UnityEngine.Object.Destroy(s); }
            if (meshes.TryGetValue(id, out var m) && m != null) UnityEngine.Object.Destroy(m);
            if (textures.TryGetValue(id, out var t) && t != null) UnityEngine.Object.Destroy(t);
            icons.Remove(id);
            meshes.Remove(id);
            textures.Remove(id);
        }

        /// <summary>The first free Enemy ID from 200, skipping <paramref name="taken"/> too.</summary>
        public static int NextFreeId(ICollection<int> taken = null)
        {
            for (int id = CustomObstacle.FirstId; id <= CustomObstacle.LastId; id++)
                if (!defs.ContainsKey(id) && (taken == null || !taken.Contains(id))) return id;
            return -1;
        }

        // ---------------- Levels ----------------

        /// <summary>The obstacles a level carries ("customs"): they win over library ones with the same ID while it is open.</summary>
        public static void AddFromLevel(LevelData level)
        {
            if (level == null) return;
            foreach (var c in level.Customs) Set(c.Clone());
        }

        /// <summary>The layer a custom obstacle is painted on (Object when it is not loaded).</summary>
        public static TileLayer LayerOf(int id) =>
            !defs.TryGetValue(id, out var d) ? TileLayer.Object : d.layer == CustomLayer.Tile ? TileLayer.Tile
            : d.layer == CustomLayer.Collect ? TileLayer.Collect : TileLayer.Object;

        static readonly TileLayer[] Layers = { TileLayer.Tile, TileLayer.Object, TileLayer.Collect };

        /// <summary>The obstacles a level paints (on any layer), for its "customs".</summary>
        public static List<CustomObstacle> UsedBy(LevelData level)
        {
            var ids = new SortedSet<int>();
            if (level != null)
                for (int r = 0; r < level.RowCount; r++)
                    for (int c = 0; c < LevelData.Width; c++)
                        foreach (var layer in Layers)
                        {
                            int id = level.Get(layer, r, c);
                            if (CustomObstacle.IsCustomId(id)) ids.Add(id);
                        }
            var list = new List<CustomObstacle>();
            foreach (int id in ids)
                if (defs.TryGetValue(id, out var d)) list.Add(d);
                else if (level.FindCustom(id) is CustomObstacle own) list.Add(own); // not loaded: keep what the file had
            return list;
        }

        /// <summary>How many cells of the level use this obstacle.</summary>
        public static int CountIn(LevelData level, int id)
        {
            int n = 0;
            if (level != null)
                for (int r = 0; r < level.RowCount; r++)
                    for (int c = 0; c < LevelData.Width; c++)
                        foreach (var layer in Layers)
                            if (level.Get(layer, r, c) == id) n++;
            return n;
        }

        // ---------------- Library (.rsrt files) ----------------

        /// <summary>The obstacle library: Levels/Obstacles next to Level1.json in the Unity Editor, the save folder's in a build.</summary>
        public static string LibraryFolder() => Path.Combine(LevelFiles.LevelsFolder(), "Obstacles").Replace('\\', '/');

        public static string LibraryPath(int id) => $"{LibraryFolder()}/{id}{CustomObstacle.FileExtension}";

        /// <summary>Reads every .rsrt in the library. Returns how many were read.</summary>
        public static int LoadLibrary()
        {
            string folder = LibraryFolder();
            if (!Directory.Exists(folder)) return 0;
            int n = 0;
            foreach (var path in Directory.GetFiles(folder, "*" + CustomObstacle.FileExtension))
            {
                var def = ReadFile(path, out _);
                if (def == null) continue;
                Set(def);
                n++;
            }
            return n;
        }

        public static CustomObstacle ReadFile(string path, out string problem)
        {
            problem = null;
            try
            {
                var def = CustomObstacle.FromJson(File.ReadAllText(path));
                if (def == null) problem = $"{Path.GetFileName(path)} is not a custom obstacle file.";
                return def;
            }
            catch (Exception e)
            {
                problem = $"{Path.GetFileName(path)} could not be read: {e.Message}";
                return null;
            }
        }

        public static void SaveToLibrary(CustomObstacle def)
        {
            LevelFiles.Write(LibraryPath(def.id), def.ToJson());
        }

        public static void DeleteFromLibrary(int id)
        {
            string path = LibraryPath(id);
#if UNITY_EDITOR
            if (path.StartsWith("Assets/") && UnityEditor.AssetDatabase.DeleteAsset(path)) return;
#endif
            if (File.Exists(path)) File.Delete(path);
        }

        // ---------------- Pictures and meshes ----------------

        /// <summary>The obstacle's icon (its PNG), or null.</summary>
        public static Sprite IconOf(int id)
        {
            if (icons.TryGetValue(id, out var s)) return s;
            s = null;
            if (defs.TryGetValue(id, out var def))
            {
                var tex = DecodePng(def.icon, $"Custom {id} Icon");
                if (tex != null) s = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }
            icons[id] = s;
            return s;
        }

        // The obstacle open in the editor's window is not registered yet: its mesh and texture are kept here, made
        // again only when its OBJ or PNG text changes (the preview is rebuilt on every edit).
        static string scratchObj, scratchTexture;
        static Mesh scratchMesh;
        static Texture2D scratchTex;

        /// <summary>The OBJ mesh kept in the obstacle, or null.</summary>
        public static Mesh MeshOf(CustomObstacle def)
        {
            if (def == null || !def.HasObj) return null;
            if (defs.TryGetValue(def.id, out var known) && known == def)
            {
                if (!meshes.TryGetValue(def.id, out var m)) meshes[def.id] = m = ObjMesh.Parse(def.obj, def.objName);
                return m;
            }
            if (!ReferenceEquals(scratchObj, def.obj))
            {
                if (scratchMesh != null) UnityEngine.Object.Destroy(scratchMesh);
                scratchObj = def.obj;
                scratchMesh = ObjMesh.Parse(def.obj, def.objName);
            }
            return scratchMesh;
        }

        /// <summary>The OBJ's texture, or null.</summary>
        public static Texture2D TextureOf(CustomObstacle def)
        {
            if (def == null || string.IsNullOrEmpty(def.texture)) return null;
            if (defs.TryGetValue(def.id, out var known) && known == def)
            {
                if (!textures.TryGetValue(def.id, out var t)) textures[def.id] = t = DecodePng(def.texture, $"Custom {def.id} Texture");
                return t;
            }
            if (!ReferenceEquals(scratchTexture, def.texture))
            {
                if (scratchTex != null) UnityEngine.Object.Destroy(scratchTex);
                scratchTexture = def.texture;
                scratchTex = DecodePng(def.texture, "Custom Texture");
            }
            return scratchTex;
        }

        public static Texture2D DecodePng(string base64, string name)
        {
            if (string.IsNullOrEmpty(base64)) return null;
            try
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true) { name = name };
                if (tex.LoadImage(Convert.FromBase64String(base64))) return tex;
                UnityEngine.Object.Destroy(tex);
            }
            catch (FormatException) { }
            return null;
        }

        // ---------------- Project models ----------------

        /// <summary>
        /// The Blender clip the obstacle's project model plays (its row in <paramref name="look"/>'s Models list, else in
        /// any of <paramref name="themes"/>), or null: an OBJ, a plain block, or a model without a clip.
        /// </summary>
        public static ModelAnimation ClipOf(CustomObstacle def, TrackTheme look, IEnumerable<TrackTheme> themes)
        {
            var model = def != null && !def.IsPicture ? FindModel(def.model, themes) : null;
            if (model == null) return null;
            var clip = look != null ? look.FindAnimation(model) : null;
            if (clip != null || themes == null) return clip;
            foreach (var t in themes)
            {
                clip = t != null ? t.FindAnimation(model) : null;
                if (clip != null) return clip;
            }
            return null;
        }

        /// <summary>A project model by name: the Custom Models library first, then the themes' Props lists.</summary>
        public static GameObject FindModel(string name, IEnumerable<TrackTheme> themes)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var lib = CustomModelLibrary.Instance;
            var m = lib != null ? lib.Find(name) : null;
            if (m != null) return m;
            if (themes != null)
                foreach (var t in themes)
                {
                    m = t != null ? t.FindProp(name) : null;
                    if (m != null) return m;
                }
            return null;
        }

        /// <summary>Every model the Model tab can pick: the Custom Models library, then the themes' Props.</summary>
        public static List<string> ModelNames(IEnumerable<TrackTheme> themes)
        {
            var names = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var lib = CustomModelLibrary.Instance;
            if (lib != null)
                foreach (var m in lib.models)
                    if (m != null && seen.Add(m.name)) names.Add(m.name);
            if (themes != null)
                foreach (var t in themes)
                    if (t != null)
                        foreach (var m in t.PropModels())
                            if (m != null && seen.Add(m.name)) names.Add(m.name);
            return names;
        }
    }
}
