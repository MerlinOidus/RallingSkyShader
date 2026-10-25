using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RTXOn
{
    /// <summary>
    /// Builds a <see cref="CustomObstacle"/> on a cell: the game (model, deadly hitbox, animation, sound), the editor's
    /// 3D view (the model at rest) and the Custom Obstacle window's preview (animated, hitbox boxes shown in red).
    /// </summary>
    public static class CustomObstacleBuilder
    {
        public enum Mode { Game, EditorView, Preview }

        const float HitboxSize = 0.92f; // like the built-in blocks: a little smaller than a tile
        const float FloorColliderSize = 1.25f, FloorDepth = 0.5f; // TrackBuilder's floor slab (floorColliderSize, TileDepth)
        static readonly Color HitboxColor = new Color(1f, 0.15f, 0.2f, 0.35f);
        static Material hitboxMaterial, plainMaterial;
        static Mesh blockMesh, cubeMesh;
        static readonly Dictionary<(Texture2D, Material), Material> objMaterials = new Dictionary<(Texture2D, Material), Material>();

        /// <summary>
        /// Builds <paramref name="def"/> on <paramref name="anchor"/> (the bottom centre of its cell). <paramref name="look"/>
        /// gives the materials; project models are looked up in the Custom Models library and <paramref name="themes"/>' Props.
        /// </summary>
        /// <paramref name="args"/>: the cell's ARGS; with a Group that a trigger tile fires, the obstacle waits for it (game only).
        public static GameObject Build(Transform anchor, CustomObstacle def, TrackTheme look, IEnumerable<TrackTheme> themes, Mode mode,
            bool showHitbox = false, PartArgs args = null)
        {
            var root = new GameObject($"Custom {def.id} {def.name}");
            root.transform.SetParent(anchor, false);
            // Motion moves the model and the hitbox together (Animation tab).
            var motion = new GameObject("Motion").transform;
            motion.SetParent(root.transform, false);
            var visual = new GameObject("Model").transform;
            visual.SetParent(motion, false);
            visual.localPosition = def.offset;
            visual.localRotation = Quaternion.Euler(def.rotation);
            visual.localScale = def.scale;
            // A model with a Blender clip plays the clip and no Animation tab motion; its hitbox is a box on each animated
            // part (AnimatedHitbox), so it follows the clip, instead of the hitbox boxes.
            var clip = CustomObstacles.ClipOf(def, look, themes);
            bool solid = def.collision && clip == null && def.layer == CustomLayer.Object; // hitbox boxes: obstacles only
            var instance = AddModel(visual, def, look, themes, mode, clip);
            if (mode == Mode.Game && clip != null && instance != null && def.collision && def.touch == CustomTouch.Kill && def.layer == CustomLayer.Object)
                AnimatedHitbox.Add(instance, motion.gameObject, def.jumpSafe);
            // The cell's ARGS time it: a Group fired by a trigger tile waits for the trigger; a Blender clip without one
            // waits until the ball is Start rows away, then plays once (Loop off) or again and again (Loop on).
            bool byTrigger = args != null && args.group > 0 && GroupTriggers.HasTrigger(args.group);
            bool waits = mode == Mode.Game && args != null && (byTrigger || clip != null && instance != null);
            TriggerAnimator trigger = null;
            if (waits)
            {
                trigger = root.AddComponent<TriggerAnimator>();
                trigger.group = args.group;
                trigger.startRows = byTrigger ? -1f : args.startRows > 0f ? args.startRows : PartArgs.Defaults(args.id).startRows;
                trigger.frames = args.frames;
                trigger.seconds = args.seconds;
                trigger.loop = args.loop;
                trigger.defaultSeconds = 1f;
                trigger.clip = instance != null ? instance.GetComponent<ModelAnimator>() : null;
                if (trigger.clip != null) trigger.clip.manual = true;
            }

            if (mode == Mode.Game && def.layer == CustomLayer.Tile)
            {
                // A floor tile: the ball rolls on a slab like the built-in floor's (it moves with the Animation tab).
                // Collision off = no floor: a hole with a model in it.
                if (def.collision)
                {
                    var floor = motion.gameObject.AddComponent<BoxCollider>();
                    floor.size = new Vector3(FloorColliderSize, FloorDepth, FloorColliderSize);
                    floor.center = new Vector3(0f, -FloorDepth * 0.5f, 0f);
                }
            }
            else if (mode == Mode.Game && def.layer == CustomLayer.Collect)
            {
                // A pickup: taken by touching it (a box around its cell, like the built-in gems).
                var take = motion.gameObject.AddComponent<BoxCollider>();
                take.isTrigger = true;
                take.size = new Vector3(0.8f, 0.9f, 0.8f);
                take.center = new Vector3(0f, 0.5f, 0f);
                root.AddComponent<CustomPickup>().counts = def.collectAs;
            }
            if (mode == Mode.Game && solid && def.touch == CustomTouch.Kill && def.boxes.Count > 0)
            {
                var hit = new GameObject("Hitbox");
                hit.transform.SetParent(motion, false);
                foreach (var b in def.boxes)
                {
                    var bc = hit.AddComponent<BoxCollider>();
                    bc.isTrigger = true;
                    bc.center = new Vector3(b.x, b.y + 0.5f, b.z);
                    bc.size = Vector3.one * HitboxSize;
                }
                hit.AddComponent<Hazard>().jumpSafe = def.jumpSafe;
            }
            if (showHitbox && solid) AddHitboxView(motion, def);

            bool moves = clip == null && (def.approach || def.spin != Vector3.zero || def.bobHeight != 0f);
            if (moves && mode != Mode.EditorView)
            {
                var m = motion.gameObject.AddComponent<CustomObstacleMotion>();
                m.def = def;
                m.preview = mode == Mode.Preview;
                m.trigger = trigger;
                if (trigger != null) trigger.motion = m;
            }
            if (mode == Mode.Game && def.HasSound)
            {
                var ps = root.AddComponent<ProximitySound>();
                ps.sound = def.sound >= 0 ? (Sfx)def.sound : Sfx.Woosh;
                if (def.HasOwnSound)
                {
                    ps.ownSound = def.soundFile;
                    ps.ownSoundName = def.soundFileName;
                    CustomMedia.Sound(def.soundFile, def.soundFileName); // OGG / MP3 start loading now
                }
                ps.atRows = def.soundRows;
                ps.volume = def.soundVolume;
            }
            return root;
        }

        /// <summary>Adds the obstacle's look; returns the project model's instance (null for OBJ, picture and block looks).</summary>
        static GameObject AddModel(Transform parent, CustomObstacle def, TrackTheme look, IEnumerable<TrackTheme> themes, Mode mode, ModelAnimation clip)
        {
            if (def.IsPicture)
            {
                AddPicture(parent, def);
                return null;
            }
            var model = CustomObstacles.FindModel(def.model, themes);
            if (model != null)
            {
                var v = Object.Instantiate(model, parent, false);
                v.name = model.name;
                foreach (var c in v.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
                bool plays = clip != null && mode != Mode.EditorView; // its Blender clip, from a theme's Models list
                if (mode != Mode.Game && !plays)
                    foreach (var a in v.GetComponentsInChildren<Animator>(true)) a.enabled = false;
                var mat = look != null ? look.modelMaterial : null;
                if (!def.ownMaterial && mat != null) SetMaterial(v, mat);
                if (plays)
                {
                    var animator = v.AddComponent<ModelAnimator>();
                    animator.settings = clip;
                    animator.preview = mode == Mode.Preview;
                }
                return v;
            }

            var mesh = CustomObstacles.MeshOf(def);
            Material material;
            Vector3 position = Vector3.zero;
            if (mesh != null)
            {
                material = ObjMaterial(CustomObstacles.TextureOf(def), look);
            }
            else
            {
                // No model yet (or it was not found): a plain block like the built-in ones.
                if (blockMesh == null) blockMesh = MeshFactory.BeveledBox(new Vector3(0.9f, 0.9f, 0.9f), 0.08f);
                mesh = blockMesh;
                position = new Vector3(0f, 0.45f, 0f);
                material = look != null && look.hazard != null ? look.hazard : Plain;
            }
            var go = new GameObject(mesh.name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return null;
        }

        // ---------------- Picture obstacles ----------------

        static Mesh cubeShape, circleShape;
        static readonly Dictionary<int, Mesh> planeShapes = new Dictionary<int, Mesh>(); // by aspect ratio x 100
        static readonly Dictionary<(ImageFrames, bool), Material> pictureMaterials = new Dictionary<(ImageFrames, bool), Material>();
        static Material missingPicture;

        /// <summary>The obstacle's picture on its shape: a 0.9 cube on the floor, or a plane / circle standing up facing the ball.</summary>
        static void AddPicture(Transform parent, CustomObstacle def)
        {
            var pic = CustomMedia.Image(def.image, def.imageName);
            var go = new GameObject($"{def.shape} {def.imageName}");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = ShapeMesh(def.shape, pic);
            var r = go.AddComponent<MeshRenderer>();
            bool cube = def.shape == CustomShape.Cube;
            r.sharedMaterial = PictureMaterial(pic, cube);
            r.shadowCastingMode = cube ? ShadowCastingMode.On : ShadowCastingMode.Off;
            if (pic != null && pic.Animated) go.AddComponent<PictureAnimator>().frames = pic;
        }

        static Mesh ShapeMesh(CustomShape shape, ImageFrames pic)
        {
            const float S = 0.9f, H = S / 2f;
            switch (shape)
            {
                case CustomShape.Cube:
                    if (cubeShape != null) return cubeShape;
                    return cubeShape = Quads("Picture Cube",
                        (new Vector3(-H, 0f, -H), new Vector3(S, 0f, 0f), new Vector3(0f, S, 0f)),  // front, to the ball
                        (new Vector3(H, 0f, H), new Vector3(-S, 0f, 0f), new Vector3(0f, S, 0f)),   // back
                        (new Vector3(-H, 0f, H), new Vector3(0f, 0f, -S), new Vector3(0f, S, 0f)),  // left
                        (new Vector3(H, 0f, -H), new Vector3(0f, 0f, S), new Vector3(0f, S, 0f)),   // right
                        (new Vector3(-H, S, -H), new Vector3(S, 0f, 0f), new Vector3(0f, 0f, S)),   // top
                        (new Vector3(-H, 0f, H), new Vector3(S, 0f, 0f), new Vector3(0f, 0f, -S))); // bottom
                case CustomShape.Circle:
                    return circleShape != null ? circleShape : circleShape = Disc("Picture Circle", 48);
                default:
                {
                    // Plane: the picture's own shape, 1 tile on its longer side, standing on the floor.
                    float aspect = pic != null ? Mathf.Clamp((float)pic.Width / Mathf.Max(1, pic.Height), 0.1f, 10f) : 1f;
                    int key = Mathf.RoundToInt(aspect * 100f);
                    if (planeShapes.TryGetValue(key, out var m) && m != null) return m;
                    float w = aspect >= 1f ? 1f : aspect, h = aspect >= 1f ? 1f / aspect : 1f;
                    return planeShapes[key] = Quads("Picture Plane", (new Vector3(-w / 2f, 0f, 0f), new Vector3(w, 0f, 0f), new Vector3(0f, h, 0f)));
                }
            }
        }

        /// <summary>Quads from (corner, right, up): the picture upright on each, seen from outside.</summary>
        static Mesh Quads(string name, params (Vector3 origin, Vector3 right, Vector3 up)[] faces)
        {
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var n = new List<Vector3>();
            var t = new List<int>();
            foreach (var (o, right, up) in faces)
            {
                int i = v.Count;
                v.Add(o); v.Add(o + right); v.Add(o + right + up); v.Add(o + up);
                uv.Add(new Vector2(0f, 0f)); uv.Add(new Vector2(1f, 0f)); uv.Add(new Vector2(1f, 1f)); uv.Add(new Vector2(0f, 1f));
                var normal = Vector3.Cross(up, right).normalized;
                for (int k = 0; k < 4; k++) n.Add(normal);
                t.Add(i); t.Add(i + 3); t.Add(i + 2);
                t.Add(i); t.Add(i + 2); t.Add(i + 1);
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(v);
            mesh.SetUVs(0, uv);
            mesh.SetNormals(n);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A standing disc of 1 tile across, its bottom on the floor, facing the ball; the picture fills its square.</summary>
        static Mesh Disc(string name, int segments)
        {
            var v = new List<Vector3> { new Vector3(0f, 0.5f, 0f) };
            var uv = new List<Vector2> { new Vector2(0.5f, 0.5f) };
            var t = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                float x = Mathf.Cos(a) * 0.5f, y = Mathf.Sin(a) * 0.5f;
                v.Add(new Vector3(x, 0.5f + y, 0f));
                uv.Add(new Vector2(0.5f + x, 0.5f + y));
                if (i > 0)
                {
                    t.Add(0); t.Add(i + 1); t.Add(i);
                }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(v);
            mesh.SetUVs(0, uv);
            var normals = new List<Vector3>();
            for (int i = 0; i < v.Count; i++) normals.Add(Vector3.back);
            mesh.SetNormals(normals);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Unlit, so the picture shows its own colours; see-through parts are cut out. Planes and circles show from
        /// both sides, the cube only from outside.
        /// </summary>
        static Material PictureMaterial(ImageFrames pic, bool cube)
        {
            if (pic == null)
            {
                if (missingPicture == null) missingPicture = new Material(Plain) { name = "Picture (none)", color = Color.white };
                return missingPicture;
            }
            if (pictureMaterials.TryGetValue((pic, cube), out var m) && m != null) return m;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader != null)
            {
                m = new Material(shader) { name = "Picture" };
                m.SetTexture("_BaseMap", pic.First);
                m.SetColor("_BaseColor", Color.white);
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cutoff", 0.5f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.SetFloat("_Cull", cube ? (float)CullMode.Back : (float)CullMode.Off);
                m.renderQueue = (int)RenderQueue.AlphaTest;
            }
            else m = new Material(Shader.Find("Sprites/Default")) { name = "Picture" }; // see-through, both sides
            m.mainTexture = pic.First;
            pictureMaterials[(pic, cube)] = m;
            return m;
        }

        /// <summary>The OBJ's material: the theme's Model Material with the OBJ's texture, or that material as it is.</summary>
        static Material ObjMaterial(Texture2D texture, TrackTheme look)
        {
            var baseMat = look != null && look.modelMaterial != null ? look.modelMaterial : Plain;
            if (texture == null) return baseMat;
            if (objMaterials.TryGetValue((texture, baseMat), out var m) && m != null) return m;
            m = new Material(baseMat) { name = texture.name };
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", texture);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", texture);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            objMaterials[(texture, baseMat)] = m;
            return m;
        }

        static void SetMaterial(GameObject v, Material m)
        {
            foreach (var r in v.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = m;
                r.sharedMaterials = mats;
            }
        }

        /// <summary>Red see-through cubes where the hitbox is (Collision tab).</summary>
        static void AddHitboxView(Transform parent, CustomObstacle def)
        {
            if (hitboxMaterial == null) hitboxMaterial = new Material(Shader.Find("Sprites/Default")) { name = "Custom Hitbox", color = HitboxColor };
            if (cubeMesh == null) cubeMesh = MeshFactory.BeveledBox(Vector3.one * HitboxSize, 0.02f);
            foreach (var b in def.boxes)
            {
                var go = new GameObject($"Hitbox {b.x},{b.y},{b.z}");
                go.transform.SetParent(parent, false);
                go.transform.localPosition = new Vector3(b.x, b.y + 0.5f, b.z);
                go.AddComponent<MeshFilter>().sharedMesh = cubeMesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = hitboxMaterial;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        static Material Plain
        {
            get
            {
                if (plainMaterial != null) return plainMaterial;
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                plainMaterial = new Material(shader) { name = "Custom Plain", color = new Color(0.85f, 0.35f, 0.3f) };
                return plainMaterial;
            }
        }
    }
}
