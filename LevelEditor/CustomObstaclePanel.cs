using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RTXOn
{
    /// <summary>
    /// The level editor's CUSTOM OBSTACLES window (the + button at the end of the Object tab, or EDIT beside the
    /// swatch), like the fan editor's: tabs Model, Collision, Event and Animation on the left, a 3D preview on the right
    /// (drag to turn it, Play runs the approach), and Delete / Export / Import / Save / X on top.
    /// It edits a copy: nothing changes in the level until Save, which writes the .rsrt to the obstacle library and
    /// puts the obstacle in the Object tab. Export writes a .rsrt anywhere to share; Import reads one.
    /// </summary>
    public class CustomObstaclePanel : MonoBehaviour
    {
        static Color PanelColor => EditorColors.Window;
        static Color HeaderColor => EditorColors.Header;
        static Color ButtonColor => EditorColors.Button;
        static Color AccentColor => EditorColors.Accent;
        static Color SaveColor => EditorColors.Go;
        static Color DeleteColor => EditorColors.Danger;
        static Color FieldColor => EditorColors.Field;
        static Color SoftText => EditorColors.Soft;
        static Color OnColor => EditorColors.Accent;
        static readonly Color BoxOn = new Color(0.9f, 0.2f, 0.25f);
        static Color BoxOff => EditorColors.Item;
        static readonly string[] TabNames = { "Model", "Collision", "Event", "Animation" };

        const float WindowWidth = 1400f, WindowHeight = 880f;
        const float PageWidth = 800f, PageTop = -165f;
        const float LabelWidth = 250f, RowHeight = 48f, RowStep = 54f;
        const float PreviewSize = 520f;
        const int PreviewLayer = 30;          // the editor's 3D view uses 31
        const int GridSize = 5, GridMid = 2;  // Collision tab: lanes -2..2, rows -2..2
        const int MaxHeight = 4;
        const int IconSize = 256;
        const float PlayRowsPerSecond = 6f;

        LevelEditor editor;
        CustomObstacle def;    // the copy being edited
        string savedJson;      // the copy as it was opened or last saved
        bool confirmClose, confirmDelete;
        int tab;
        Text title, status, help;
        readonly Image[] tabButtons = new Image[TabNames.Length];
        readonly GameObject[] pages = new GameObject[TabNames.Length];
        readonly List<Action> refreshers = new List<Action>();

        // Collision tab
        int boxHeight;
        readonly Image[,] boxCells = new Image[GridSize, GridSize];
        readonly Text[,] boxTexts = new Text[GridSize, GridSize];
        readonly Image[] heightButtons = new Image[MaxHeight];

        // Chooser (models, files in a build)
        RectTransform chooser;
        RectTransform chooserList;
        Text chooserTitle;
        readonly List<GameObject> chooserItems = new List<GameObject>();

        // Preview
        RawImage previewImage;
        RenderTexture previewTexture;
        Camera previewCam;
        Light previewLight;
        Transform stage;
        GameObject floor, built;
        TrackTheme stageLook;
        Vector3 focus = new Vector3(0f, 0.6f, 0f);
        float distance = 6f, yaw = 35f, pitch = 26f;
        bool showHitbox = true;
        float playStart = -1f;
        Text playLabel, hitboxLabel;
        static string lastFolder;

        public bool IsOpen => gameObject.activeSelf;

        public static CustomObstaclePanel Build(LevelEditor owner, Transform parent)
        {
            var bg = UiFactory.MakePanel(parent, "Custom Obstacles", PanelColor, true); // catches clicks: nothing paints under it
            UiFactory.Place(bg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(WindowWidth, WindowHeight));
            var panel = bg.gameObject.AddComponent<CustomObstaclePanel>();
            panel.editor = owner;
            panel.BuildUi((RectTransform)bg.transform);
            bg.gameObject.SetActive(false);
            return panel;
        }

        // ---------------- Open / close ----------------

        /// <summary>Opens on a copy of <paramref name="existing"/>, or on a new obstacle with the next free Enemy ID.</summary>
        public void Open(CustomObstacle existing)
        {
            if (existing != null) def = existing.Clone();
            else
            {
                int id = CustomObstacles.NextFreeId();
                if (id < 0)
                {
                    editor.ShowStatus("No free Enemy ID left (200-999): delete a custom obstacle first.");
                    return;
                }
                def = new CustomObstacle { id = id, name = "Obstacle " + id };
            }
            savedJson = def.ToJson(false);
            confirmClose = confirmDelete = false;
            playStart = -1f;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            CloseChooser();
            EnsureStage();
            previewCam.enabled = true;
            previewLight.enabled = true;
            ShowTab(0);
            Changed(false);
            Status(existing != null ? $"Editing {def.name}. Save changes every cell with ID {def.id}."
                                    : $"New custom obstacle, Enemy ID {def.id}. Pick a model, set the hitbox, then Save.");
        }

        bool Unsaved => def != null && def.ToJson(false) != savedJson;

        /// <summary>X / Esc: closes, after a warning when there are changes that are not saved.</summary>
        public void Close()
        {
            if (!IsOpen) return;
            if (chooser.gameObject.activeSelf)
            {
                CloseChooser();
                return;
            }
            if (Unsaved && !confirmClose)
            {
                confirmClose = true;
                Status("Not saved: press Save, or X again to close without saving.");
                return;
            }
            Hide();
        }

        /// <summary>Closes without asking.</summary>
        public void Hide()
        {
            gameObject.SetActive(false);
            if (previewCam != null) previewCam.enabled = false;
            if (previewLight != null) previewLight.enabled = false;
            if (built != null) Destroy(built);
            built = null;
            def = null;
        }

        // ---------------- Buttons ----------------

        void Save()
        {
            if (string.IsNullOrWhiteSpace(def.name)) def.name = "Obstacle " + def.id;
            def.Normalize();
            try
            {
                CustomObstacles.SaveToLibrary(def);
            }
            catch (Exception e)
            {
                Status("Could not save: " + e.Message);
                return;
            }
            CustomObstacles.Set(def.Clone());
            savedJson = def.ToJson(false);
            confirmClose = confirmDelete = false;
            editor.CustomObstacleSaved(def.id);
            Refresh();
            string tab = def.layer == CustomLayer.Tile ? "Tiles" : def.layer == CustomLayer.Collect ? "Collect" : "Object";
            Status($"Saved {def.name} (ID {def.id}) as {CustomObstacles.LibraryPath(def.id)}. It is in the {tab} tab.");
        }

        void Delete()
        {
            if (!CustomObstacles.TryGet(def.id, out _))
            {
                Hide(); // never saved: nothing to delete
                editor.ShowStatus("New custom obstacle dropped.");
                return;
            }
            int used = CustomObstacles.CountIn(editor.Level, def.id);
            if (used > 0)
            {
                Status($"{used} cell(s) of this level use ID {def.id}: erase them first (Object tab, right click).");
                return;
            }
            if (!confirmDelete)
            {
                confirmDelete = true;
                Status($"Press Delete again to delete {def.name} and its file {def.id}{CustomObstacle.FileExtension}.");
                return;
            }
            int id = def.id;
            string name = def.name;
            CustomObstacles.Remove(id);
            try { CustomObstacles.DeleteFromLibrary(id); }
            catch (Exception e) { Debug.LogWarning("RTXOn: " + e.Message); }
            Hide();
            editor.CustomObstacleDeleted(id, name);
        }

        void Export()
        {
            string file = SafeFileName(def.name) + CustomObstacle.FileExtension;
#if UNITY_EDITOR
            string path = UnityEditor.EditorUtility.SaveFilePanel("Export Custom Obstacle", LastFolder, file, CustomObstacle.FileExtension.TrimStart('.'));
            if (string.IsNullOrEmpty(path)) return;
            lastFolder = Path.GetDirectoryName(path);
#else
            string path = Path.Combine(ExportFolder(), file);
#endif
            try
            {
                string folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
                File.WriteAllText(path, def.ToJson());
#if !UNITY_EDITOR
                if (NativeFiles.Available) // Android: the player chooses where it goes
                {
                    NativeFiles.Save(path, file, Status);
                    Status($"Choose where to save {file}...");
                    return;
                }
#endif
                Status($"Exported to {path}");
            }
            catch (Exception e)
            {
                Status("Export failed: " + e.Message);
            }
        }

        void Import()
        {
            PickFile("Import Custom Obstacle", new[] { CustomObstacle.FileExtension.TrimStart('.') }, path =>
            {
                var d = CustomObstacles.ReadFile(path, out string problem);
                if (d == null)
                {
                    Status(problem);
                    return;
                }
                string note = "";
                if (CustomObstacles.TryGet(d.id, out var existing))
                {
                    if (existing.SameAs(d))
                    {
                        def = d;
                        savedJson = d.ToJson(false);
                        Changed(false);
                        Status($"{d.name} (ID {d.id}) is already in the Object tab.");
                        return;
                    }
                    int old = d.id;
                    d.id = CustomObstacles.NextFreeId();
                    if (d.id < 0)
                    {
                        Status("No free Enemy ID left (200-999): delete a custom obstacle first.");
                        return;
                    }
                    note = $" ID {old} was taken, so it gets ID {d.id}.";
                }
                def = d;
                savedJson = null; // not in the library yet
                Changed(false);
                Status($"Imported {Path.GetFileName(path)}.{note} Press Save to add it to the Object tab.");
            });
        }

        // ---------------- Model tab actions ----------------

        void ChooseModel()
        {
            var names = CustomObstacles.ModelNames(editor.CustomThemes());
            var options = new List<string> { "Plain block", "OBJ file...", "Picture on Cube (PNG / JPG / GIF)",
                                              "Picture on Plane (PNG / JPG / GIF)", "Picture on Circle (PNG / JPG / GIF)" };
            const int FirstModel = 5;
            foreach (var n in names) options.Add(n);
            OpenChooser(names.Count == 0 ? "Pick a model (add more: RTX ON > Add Selected Models As Custom Models)" : "Pick a model", options, i =>
            {
                if (i == 0) SetModel("", "", "");
                else if (i == 1) PickFile("OBJ model", new[] { "obj" }, LoadObj);
                else if (i < FirstModel) SetShape(CustomShape.Cube + (i - 2));
                else SetModel(options[i], "", "");
            });
        }

        void SetModel(string model, string objName, string obj)
        {
            def.shape = CustomShape.Model;
            def.model = model;
            def.objName = objName;
            def.obj = obj;
            Changed();
        }

        /// <summary>A picture obstacle: the picture it has, or one picked now.</summary>
        void SetShape(CustomShape shape)
        {
            def.shape = shape;
            def.model = "";
            Changed();
            if (string.IsNullOrEmpty(def.image)) PickPicture();
        }

        void PickPicture()
        {
            PickFile("Picture (PNG / JPG / GIF)", CustomMedia.ImageExtensions, path =>
            {
                var bytes = ReadSmallFile(path, "picture");
                if (bytes == null) return;
                var pic = CustomMedia.Decode(bytes, Path.GetFileName(path));
                if (pic == null)
                {
                    Status($"{Path.GetFileName(path)} could not be read as a PNG, JPG or GIF picture.");
                    return;
                }
                def.image = Convert.ToBase64String(bytes);
                def.imageName = Path.GetFileName(path);
                if (!def.IsPicture) def.shape = CustomShape.Plane;
                Changed();
                Status($"{def.imageName}: {pic.Width} x {pic.Height}" + (pic.Animated ? $", {pic.frames.Length} frames ({pic.length:0.##} s loop)." : "."));
            });
        }

        void PickSound()
        {
            PickFile("Sound (WAV / OGG / MP3)", CustomMedia.SoundExtensions, path =>
            {
                var bytes = ReadSmallFile(path, "sound");
                if (bytes == null) return;
                string ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
                if (ext == "wav" && CustomMedia.Wav(bytes, "Test") == null)
                {
                    Status($"{Path.GetFileName(path)} is not a WAV this can play (PCM 8/16/24/32 bit or float).");
                    return;
                }
                def.soundFile = Convert.ToBase64String(bytes);
                def.soundFileName = Path.GetFileName(path);
                CustomMedia.Sound(def.soundFile, def.soundFileName); // OGG / MP3: start loading for Test
                Changed();
                Status($"Own sound {def.soundFileName}: it plays when the ball is Sound at rows away. Test plays it now.");
            });
        }

        void TestSound()
        {
            if (!def.HasOwnSound)
            {
                if (def.sound >= 0) GameAudio.Play((Sfx)def.sound, def.soundVolume);
                Status(def.sound >= 0 ? "Played the Sound." : "No sound yet: pick a Sound or an own sound file.");
                return;
            }
            var clip = CustomMedia.Sound(def.soundFile, def.soundFileName);
            if (clip == null)
            {
                Status("The sound is still loading (or could not be read): press Test again.");
                return;
            }
            if (GameAudio.Instance != null) GameAudio.PlaySfxClip(clip, def.soundVolume);
            else if (Camera.main != null) AudioSource.PlayClipAtPoint(clip, Camera.main.transform.position, def.soundVolume);
            Status($"Played {def.soundFileName} ({clip.length:0.##} s).");
        }

        byte[] ReadSmallFile(string path, string what)
        {
            try
            {
                if (new FileInfo(path).Length > CustomMedia.MaxFileBytes)
                {
                    Status($"{Path.GetFileName(path)} is bigger than {CustomMedia.MaxFileBytes / (1024 * 1024)} MB: the {what} goes inside every level that uses it, so keep it small.");
                    return null;
                }
                return File.ReadAllBytes(path);
            }
            catch (Exception e)
            {
                Status($"Could not read the {what}: " + e.Message);
                return null;
            }
        }

        void LoadObj(string path)
        {
            string text;
            try { text = File.ReadAllText(path); }
            catch (Exception e) { Status("Could not read the OBJ: " + e.Message); return; }
            var test = ObjMesh.Parse(text, "Test");
            if (test == null)
            {
                Status($"{Path.GetFileName(path)} has no faces: export it from Blender as Wavefront (.obj) with Triangulated or plain faces.");
                return;
            }
            Destroy(test);
            SetModel("", Path.GetFileName(path), text);
            Status($"Model: {Path.GetFileName(path)}. Add a texture with OBJ texture > Select... (PNG or JPG).");
        }

        void PickImage(string what, int maxSize, Action<string> set)
        {
            PickFile(what, new[] { "png", "jpg", "jpeg" }, path =>
            {
                byte[] bytes;
                try { bytes = File.ReadAllBytes(path); }
                catch (Exception e) { Status("Could not read the picture: " + e.Message); return; }
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(bytes))
                {
                    Destroy(tex);
                    Status($"{Path.GetFileName(path)} is not a PNG or JPG picture.");
                    return;
                }
                var png = Shrink(tex, maxSize).EncodeToPNG();
                Destroy(tex);
                set(Convert.ToBase64String(png));
                Changed();
            });
        }

        /// <summary>The icon from the preview picture as it is now.</summary>
        void IconFromPreview()
        {
            if (previewCam == null) return;
            previewCam.Render();
            var small = RenderTexture.GetTemporary(IconSize, IconSize, 0);
            Graphics.Blit(previewTexture, small);
            var active = RenderTexture.active;
            RenderTexture.active = small;
            var tex = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, IconSize, IconSize), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(small);
            def.icon = Convert.ToBase64String(tex.EncodeToPNG());
            Destroy(tex);
            Changed();
            Status("Icon taken from the preview. Turn the preview (drag it) first for another angle.");
        }

        /// <summary><paramref name="tex"/> made at most <paramref name="max"/> pixels wide and high (0 = as it is).</summary>
        static Texture2D Shrink(Texture2D tex, int max)
        {
            if (max <= 0 || (tex.width <= max && tex.height <= max)) return tex;
            float k = (float)max / Mathf.Max(tex.width, tex.height);
            int w = Mathf.Max(1, Mathf.RoundToInt(tex.width * k)), h = Mathf.Max(1, Mathf.RoundToInt(tex.height * k));
            var rt = RenderTexture.GetTemporary(w, h, 0);
            Graphics.Blit(tex, rt);
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var small = new Texture2D(w, h, TextureFormat.RGBA32, false);
            small.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            small.Apply();
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(rt);
            tex.Reinitialize(w, h);
            tex.SetPixels32(small.GetPixels32());
            tex.Apply();
            Destroy(small);
            return tex;
        }

        string ModelText()
        {
            if (def.IsPicture) return $"Picture on {def.shape}";
            if (!string.IsNullOrEmpty(def.model))
                return CustomObstacles.FindModel(def.model, editor.CustomThemes()) != null ? def.model : def.model + " (not found)";
            if (def.HasObj) return $"{def.objName} ({def.obj.Length / 1024} KB)";
            return "Plain block";
        }

        static string PictureText(string base64) => string.IsNullOrEmpty(base64) ? "none" : $"PNG ({base64.Length * 3 / 4 / 1024} KB)";

        // ---------------- Files ----------------

        static string LastFolder => !string.IsNullOrEmpty(lastFolder) ? lastFolder : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        /// <summary>A built game has no file window: it lists the files in this folder instead.</summary>
        static string ImportFolder() => Path.Combine(Application.persistentDataPath, "Import");

        static string ExportFolder() => Path.Combine(Application.persistentDataPath, "Export");

        /// <summary>
        /// A file with one of <paramref name="extensions"/>: the system's file window in the Unity Editor, a list of the
        /// files in the Import folder in a built game.
        /// </summary>
        void PickFile(string heading, string[] extensions, Action<string> onPath)
        {
#if UNITY_EDITOR
            string path = UnityEditor.EditorUtility.OpenFilePanelWithFilters(heading, LastFolder,
                new[] { string.Join(" / ", extensions).ToUpperInvariant(), string.Join(",", extensions) });
            if (string.IsNullOrEmpty(path)) return;
            lastFolder = Path.GetDirectoryName(path);
            onPath(path);
#else
            if (NativeFiles.Available) // Android: the phone's own file window
            {
                NativeFiles.Pick(extensions, onPath, Status);
                return;
            }
            string folder = ImportFolder();
            var files = new List<string>();
            try
            {
                Directory.CreateDirectory(folder);
                foreach (var f in Directory.GetFiles(folder))
                    foreach (var e in extensions)
                        if (f.EndsWith("." + e, StringComparison.OrdinalIgnoreCase)) { files.Add(f); break; }
            }
            catch (Exception e)
            {
                Status("Could not read the Import folder: " + e.Message);
                return;
            }
            if (files.Count == 0)
            {
                Status($"No .{string.Join(" / .", extensions)} file in {folder}: copy it there first.");
                return;
            }
            files.Sort(StringComparer.OrdinalIgnoreCase);
            var names = new List<string>();
            foreach (var f in files) names.Add(Path.GetFileName(f));
            OpenChooser($"{heading} (from {folder})", names, i => onPath(files[i]));
#endif
        }

        static string SafeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return string.IsNullOrWhiteSpace(name) ? "Obstacle" : name.Trim();
        }

        // ---------------- Changes ----------------

        void Changed(bool edited = true)
        {
            if (edited)
            {
                confirmClose = false;
                confirmDelete = false;
            }
            Refresh();
            RebuildPreview();
        }

        void Refresh()
        {
            if (def == null) return;
            foreach (var r in refreshers) r();
            RefreshBoxes();
            title.text = $"CUSTOM OBSTACLES  ·  {def.name.ToUpperInvariant()}  ·  ID {def.id}" + (Unsaved ? "  *" : "");
            playLabel.text = playStart >= 0f ? "Stop" : "Play";
            hitboxLabel.text = showHitbox ? "Hitbox: On" : "Hitbox: Off";
        }

        void Status(string message) => status.text = message;

        void ShowTab(int index)
        {
            tab = index;
            for (int i = 0; i < pages.Length; i++)
            {
                pages[i].SetActive(i == index);
                tabButtons[i].color = i == index ? AccentColor : ButtonColor;
            }
            help.text = index switch
            {
                0 => "Is a: Object = an obstacle (Object tab), Tile = a floor tile the ball rolls on (Tiles tab; Collision Off = a hole), " +
                     "Collect = a pickup taken by touching it (Collect tab; Event tab: counts as Gem, Crown or Nothing). Model: a project model (RTX ON > Add Selected Models As Custom Models, or a theme's Props), an OBJ file, " +
                     "a picture (PNG / JPG / GIF, a GIF plays) on a Cube, Plane or Circle, or a plain block. Files are kept " +
                     "inside the obstacle, so they travel with Export. Rotation, Scale and Position set it " +
                     "on its cell (Position Y up, in tiles). Icon = its picture in the palette and grid.",
                1 => "Collision On: red boxes = where the ball dies (each 1x1x1). The middle square is the obstacle's own cell; " +
                     "up = ahead. Height picks the layer (0 = on the floor). Collision Off = the ball rolls through. A model " +
                     "that plays a Blender clip gets a box on each moving part instead (it follows the clip). Tile and Collect do not use the boxes.",
                2 => "When touched (Object): Kill, or Nothing (decoration). Pickup counts as (Collect): Gem, Crown or Nothing. Jump safe = cannot hurt the ball while it flies from a jump pad. " +
                     "Sound (or an own WAV / OGG / MP3 file, kept inside the obstacle) plays once when the ball is that many rows away.",
                _ => "No clip: Approach moves it from the From pose to its place while the ball comes from Start rows to End rows " +
                     "away (Ease: Linear, Smooth, Bounce, Overshoot; the hitbox moves with it). Spin and Bob run all the time. " +
                     "A model with a Blender clip (theme Models > Animation > Clip) plays the clip instead. Play shows it here.",
            };
            if (def != null) RebuildPreview(); // the hitbox shows on the Collision tab
        }

        // ---------------- UI ----------------

        void BuildUi(RectTransform root)
        {
            var header = UiFactory.MakePanel(root, "Header", HeaderColor);
            UiFactory.Place(header.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(WindowWidth, 80f));
            title = UiFactory.MakeLabel(header.transform, "CUSTOM OBSTACLES", 30, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            UiFactory.Place(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(720f, 70f));
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 16;
            title.resizeTextMaxSize = 30;

            string[] names = { "Delete", "Export", "Import", "Save", "X" };
            UnityAction[] actions = { Delete, Export, Import, Save, Close };
            Color[] colors = { DeleteColor, ButtonColor, ButtonColor, SaveColor, ButtonColor };
            float x = -16f;
            for (int i = names.Length - 1; i >= 0; i--)
            {
                float w = i == names.Length - 1 ? 64f : 136f;
                var b = UiFactory.MakeButton(header.transform, names[i], colors[i], actions[i], 24);
                UiFactory.Place((RectTransform)b.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(x, 0f), new Vector2(w, 56f));
                x -= w + 10f;
            }

            for (int i = 0; i < TabNames.Length; i++)
            {
                int index = i;
                var t = UiFactory.MakeButton(root, TabNames[i], ButtonColor, () => ShowTab(index), 24);
                UiFactory.Place((RectTransform)t.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f + i * 196f, -94f), new Vector2(186f, 54f));
                tabButtons[i] = (Image)t.targetGraphic;
                var page = UiFactory.MakeRect(root, TabNames[i] + " Page");
                UiFactory.Place(page, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, PageTop), new Vector2(PageWidth, WindowHeight + PageTop - 70f));
                pages[i] = page.gameObject;
            }
            BuildModelPage(pages[0].transform);
            BuildCollisionPage(pages[1].transform);
            BuildEventPage(pages[2].transform);
            BuildAnimationPage(pages[3].transform);
            BuildPreview(root);

            status = UiFactory.MakeLabel(root, "", 20, TextAnchor.MiddleLeft, SoftText);
            UiFactory.Place(status.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 10f), new Vector2(WindowWidth - 48f, 50f));
            status.resizeTextForBestFit = true;
            status.resizeTextMinSize = 12;
            status.resizeTextMaxSize = 20;

            BuildChooser(root);
        }

        void BuildModelPage(Transform page)
        {
            float y = 0f;
            InfoRow(page, "Enemy ID", ref y, () => def.id.ToString());
            TextRow(page, "Name", ref y, () => def.name, t => def.name = string.IsNullOrWhiteSpace(t) ? def.name : t.Trim());
            CycleRow(page, "Is a", ref y, () => def.layer == CustomLayer.Tile ? "Tile (floor)" : def.layer == CustomLayer.Collect ? "Collect (pickup)" : "Object (obstacle)",
                step => def.layer = (CustomLayer)(((int)def.layer + step + 3) % 3));
            ButtonsRow(page, "Model", ref y, ModelText, ("Select...", ChooseModel));
            ButtonsRow(page, "Picture", ref y, () => string.IsNullOrEmpty(def.image) ? "none" : $"{def.imageName} ({def.image.Length * 3 / 4 / 1024} KB)",
                ("Select...", PickPicture),
                ("Clear", () => { def.image = ""; def.imageName = ""; Changed(); }));
            ButtonsRow(page, "OBJ texture", ref y, () => PictureText(def.texture),
                ("Select...", () => PickImage("OBJ texture", 0, b => def.texture = b)),
                ("Clear", () => { def.texture = ""; Changed(); }));
            ButtonsRow(page, "Icon", ref y, () => PictureText(def.icon),
                ("Select...", () => PickImage("Icon", IconSize, b => def.icon = b)),
                ("From preview", IconFromPreview),
                ("Clear", () => { def.icon = ""; Changed(); }));
            ToggleRow(page, "Material", ref y, () => def.ownMaterial ? "Model's own" : "Theme Model Material",
                () => def.ownMaterial, () => def.ownMaterial = !def.ownMaterial);
            Vec3Row(page, "Rotation", ref y, () => def.rotation, v => def.rotation = v);
            Vec3Row(page, "Scale", ref y, () => def.scale, v => def.scale = v);
            Vec3Row(page, "Position", ref y, () => def.offset, v => def.offset = v);
        }

        /// <summary>The Blender clip the obstacle's model plays (theme Models list), or null.</summary>
        ModelAnimation CurrentClip() => def != null ? CustomObstacles.ClipOf(def, editor.CurrentLook(), editor.CustomThemes()) : null;

        static string ClipName(ModelAnimation clip) => clip != null && clip.clip != null ? clip.clip.name : "";

        /// <summary>A part of a page that is greyed out (and cannot be clicked) while <paramref name="off"/> says so.</summary>
        RectTransform Dimmable(Transform page, string name, float top, Func<bool> off)
        {
            var area = UiFactory.MakeRect(page, name);
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.offsetMin = Vector2.zero;
            area.offsetMax = new Vector2(0f, top);
            var group = area.gameObject.AddComponent<CanvasGroup>();
            refreshers.Add(() =>
            {
                bool dim = off();
                group.interactable = !dim;
                group.blocksRaycasts = !dim;
                group.alpha = dim ? 0.35f : 1f;
            });
            return area;
        }

        void BuildCollisionPage(Transform page)
        {
            float y = 0f;
            ToggleRow(page, "Collision", ref y, () =>
                {
                    var clip = CurrentClip();
                    return !def.collision ? "Off" : clip != null ? "On (follows clip " + ClipName(clip) + ")" : "On";
                },
                () => def.collision,
                () => def.collision = !def.collision);
            page = Dimmable(page, "Boxes", y, () => !def.collision || CurrentClip() != null || def.layer != CustomLayer.Object);

            var heightLabel = UiFactory.MakeLabel(page, "Height", 22, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            UiFactory.Place(heightLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, 0f), new Vector2(120f, RowHeight));
            for (int h = 0; h < MaxHeight; h++)
            {
                int level = h;
                var b = UiFactory.MakeButton(page, h.ToString(), ButtonColor, () => { boxHeight = level; RefreshBoxes(); }, 24);
                UiFactory.Place((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(130f + h * 70f, 0f), new Vector2(62f, RowHeight));
                heightButtons[h] = (Image)b.targetGraphic;
            }
            var clear = UiFactory.MakeButton(page, "Clear", ButtonColor, () => { def.boxes.Clear(); Changed(); }, 22);
            UiFactory.Place((RectTransform)clear.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(430f, 0f), new Vector2(150f, RowHeight));
            var one = UiFactory.MakeButton(page, "Just its cell", ButtonColor, () =>
            {
                def.boxes.Clear();
                def.boxes.Add(new CollisionBox(0, 0, 0));
                Changed();
            }, 22);
            UiFactory.Place((RectTransform)one.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(590f, 0f), new Vector2(190f, RowHeight));

            const float Cell = 82f, Gap = 6f, GridX = 180f, GridY = -96f;
            var ahead = UiFactory.MakeLabel(page, "ahead  ▲", 20, TextAnchor.MiddleCenter, SoftText, FontStyle.Bold);
            UiFactory.Place(ahead.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(GridX, GridY + 34f), new Vector2(GridSize * (Cell + Gap), 30f));
            for (int row = 0; row < GridSize; row++)
                for (int lane = 0; lane < GridSize; lane++)
                {
                    int dz = GridMid - row, dx = lane - GridMid; // screen row 0 = the furthest ahead
                    var cell = UiFactory.MakeButton(page, "", BoxOff, () => ToggleBox(dx, dz), 18);
                    var rt = (RectTransform)cell.transform;
                    UiFactory.Place(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(GridX + lane * (Cell + Gap), GridY - row * (Cell + Gap)), new Vector2(Cell, Cell));
                    boxCells[row, lane] = (Image)cell.targetGraphic;
                    boxTexts[row, lane] = cell.GetComponentInChildren<Text>();
                    if (dx == 0 && dz == 0)
                    {
                        var mark = UiFactory.MakeLabel(rt, "cell", 14, TextAnchor.LowerCenter, Color.white);
                        UiFactory.Stretch(mark.rectTransform, 2f, 4f, 2f, 2f);
                        var frame = UiFactory.MakePanel(rt, "Frame", new Color(1f, 1f, 1f, 0.9f));
                        UiFactory.Place(frame.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(Cell, 4f));
                    }
                }
            var lanes = UiFactory.MakeLabel(page, "lane  -2      -1       0      +1      +2", 18, TextAnchor.MiddleCenter, SoftText);
            UiFactory.Place(lanes.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(GridX, GridY - GridSize * (Cell + Gap) - 4f), new Vector2(GridSize * (Cell + Gap), 30f));
        }

        void BuildEventPage(Transform page)
        {
            float y = 0f;
            ToggleRow(page, "When touched", ref y, () => def.touch == CustomTouch.Kill ? "Kill" : "Nothing",
                () => def.touch == CustomTouch.Kill, () => def.touch = def.touch == CustomTouch.Kill ? CustomTouch.Nothing : CustomTouch.Kill);
            ToggleRow(page, "Jump safe", ref y, () => def.jumpSafe ? "On" : "Off", () => def.jumpSafe, () => def.jumpSafe = !def.jumpSafe);
            CycleRow(page, "Pickup counts as", ref y, () => def.layer != CustomLayer.Collect ? "(Collect only)" : def.collectAs.ToString(),
                step => def.collectAs = (CustomCollect)(((int)def.collectAs + step + 3) % 3));
            int sounds = Enum.GetValues(typeof(Sfx)).Length;
            CycleRow(page, "Sound", ref y, () => def.sound < 0 ? "None" : ((Sfx)def.sound).ToString(),
                step => def.sound = (def.sound + 1 + step + sounds + 1) % (sounds + 1) - 1);
            ButtonsRow(page, "Own sound", ref y, () => def.HasOwnSound ? def.soundFileName : "none (uses Sound)",
                ("Select...", PickSound),
                ("Test", TestSound),
                ("Clear", () => { def.soundFile = ""; def.soundFileName = ""; Changed(); }));
            NumberRow(page, "Sound at (rows)", ref y, () => def.soundRows, v => def.soundRows = v);
            NumberRow(page, "Volume (0-1)", ref y, () => def.soundVolume, v => def.soundVolume = v);
            // Adrian 2026-10-24: a Dialogue bubble and a power-up that go along with the obstacle as it moves.
            TextRow(page, "Dialogue", ref y, () => def.dialogue, t => def.dialogue = t);
            NumberRow(page, "Dialogue at (rows)", ref y, () => def.dialogueRows, v => def.dialogueRows = v);
            NumberRow(page, "Dialogue seconds", ref y, () => def.dialogueSeconds, v => def.dialogueSeconds = v);
            CycleRow(page, "Power-up on it", ref y, () => def.powerUp == 0 ? "None" : TilePalette.Name(def.powerUp),
                step =>
                {
                    int n = PartArgs.PowerUpCount + 1; // None, then 160..168
                    int i = def.powerUp == 0 ? 0 : def.powerUp - PartArgs.PowerUpFirst + 1;
                    i = (i + step + n) % n;
                    def.powerUp = i == 0 ? 0 : PartArgs.PowerUpFirst + i - 1;
                });
            NumberRow(page, "Power-up height", ref y, () => def.powerUpHeight, v => def.powerUpHeight = v);
        }

        void BuildAnimationPage(Transform page)
        {
            float y = 0f;
            InfoRow(page, "Model clip", ref y, () =>
            {
                var clip = CurrentClip();
                return clip != null ? $"{ClipName(clip)} ({clip.mode}) plays, below is off" : "None: the settings below move it";
            });
            var motion = Dimmable(page, "Motion", y, () => CurrentClip() != null);
            page = motion;
            y = 0f;
            ToggleRow(page, "Approach", ref y, () => def.approach ? "On" : "Off", () => def.approach, () => def.approach = !def.approach);
            NumberRow(page, "Start rows", ref y, () => def.startRows, v => def.startRows = v);
            NumberRow(page, "End rows", ref y, () => def.endRows, v => def.endRows = v);
            Vec3Row(page, "From position", ref y, () => def.fromOffset, v => def.fromOffset = v);
            Vec3Row(page, "From rotation", ref y, () => def.fromRotation, v => def.fromRotation = v);
            NumberRow(page, "From size", ref y, () => def.fromScale, v => def.fromScale = v);
            int eases = Enum.GetValues(typeof(CustomEase)).Length;
            CycleRow(page, "Ease", ref y, () => def.ease.ToString(), step => def.ease = (CustomEase)(((int)def.ease + step + eases) % eases));
            Vec3Row(page, "Spin (°/s)", ref y, () => def.spin, v => def.spin = v);
            NumberRow(page, "Bob height", ref y, () => def.bobHeight, v => def.bobHeight = v);
            NumberRow(page, "Bob rows", ref y, () => def.bobRows, v => def.bobRows = v);
        }

        void BuildPreview(RectTransform root)
        {
            var frame = UiFactory.MakePanel(root, "Preview Frame", new Color(0f, 0f, 0f, 0.4f));
            UiFactory.Place(frame.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, PageTop), new Vector2(PreviewSize, PreviewSize));
            previewImage = new GameObject("Preview", typeof(RectTransform)).AddComponent<RawImage>();
            previewImage.transform.SetParent(frame.transform, false);
            UiFactory.Stretch(previewImage.rectTransform);
            previewImage.raycastTarget = true;
            var drag = previewImage.gameObject.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            entry.callback.AddListener(e =>
            {
                var p = (PointerEventData)e;
                yaw += p.delta.x * 0.4f;
                pitch = Mathf.Clamp(pitch - p.delta.y * 0.3f, 5f, 85f);
            });
            drag.triggers.Add(entry);
            var scroll = new EventTrigger.Entry { eventID = EventTriggerType.Scroll };
            scroll.callback.AddListener(e => distance = Mathf.Clamp(distance * (((PointerEventData)e).scrollDelta.y > 0f ? 0.9f : 1.1f), 2f, 40f));
            drag.triggers.Add(scroll);

            float y = PageTop - PreviewSize - 12f;
            var play = UiFactory.MakeButton(root, "Play", AccentColor, TogglePlay, 22);
            UiFactory.Place((RectTransform)play.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f - PreviewSize + 160f, y), new Vector2(160f, 50f));
            playLabel = play.GetComponentInChildren<Text>();
            var hit = UiFactory.MakeButton(root, "Hitbox: On", ButtonColor, () => { showHitbox = !showHitbox; Changed(false); }, 20);
            UiFactory.Place((RectTransform)hit.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f - PreviewSize + 340f, y), new Vector2(170f, 50f));
            hitboxLabel = hit.GetComponentInChildren<Text>();
            var reset = UiFactory.MakeButton(root, "Reset view", ButtonColor, () => { yaw = 35f; pitch = 26f; FitCamera(); }, 20);
            UiFactory.Place((RectTransform)reset.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, y), new Vector2(170f, 50f));

            help = UiFactory.MakeLabel(root, "", 17, TextAnchor.UpperLeft, SoftText);
            UiFactory.Place(help.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, y - 62f), new Vector2(PreviewSize, 120f));
        }

        void BuildChooser(RectTransform root)
        {
            var bg = UiFactory.MakePanel(root, "Chooser", new Color(0.08f, 0.07f, 0.1f, 0.98f), true);
            chooser = bg.rectTransform;
            UiFactory.Place(chooser, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -90f), new Vector2(PageWidth, WindowHeight - 160f));
            chooserTitle = UiFactory.MakeLabel(chooser, "", 22, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            UiFactory.Place(chooserTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -8f), new Vector2(PageWidth - 180f, 50f));
            chooserTitle.resizeTextForBestFit = true;
            chooserTitle.resizeTextMinSize = 12;
            chooserTitle.resizeTextMaxSize = 22;
            var cancel = UiFactory.MakeButton(chooser, "Cancel", ButtonColor, CloseChooser, 22);
            UiFactory.Place((RectTransform)cancel.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -8f), new Vector2(140f, 50f));
            var list = UiFactory.MakeGridList(chooser, new Vector2(PageWidth - 60f, 50f), 6f, 1, AccentColor, out chooserList);
            UiFactory.Stretch((RectTransform)list.transform, 12f, 12f, 12f, 70f);
            bg.gameObject.SetActive(false);
        }

        void OpenChooser(string heading, List<string> options, Action<int> pick)
        {
            chooserTitle.text = heading;
            foreach (var go in chooserItems) Destroy(go);
            chooserItems.Clear();
            for (int i = 0; i < options.Count; i++)
            {
                int index = i;
                var b = UiFactory.MakeButton(chooserList, options[i], ButtonColor, () =>
                {
                    CloseChooser();
                    pick(index);
                }, 20);
                chooserItems.Add(b.gameObject);
            }
            chooser.gameObject.SetActive(true);
            chooser.SetAsLastSibling();
        }

        void CloseChooser()
        {
            if (chooser != null) chooser.gameObject.SetActive(false);
        }

        // ---------------- Rows ----------------

        RectTransform Row(Transform page, string label, ref float y)
        {
            var rect = UiFactory.MakeRect(page, label);
            UiFactory.Place(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, y), new Vector2(PageWidth, RowHeight));
            y -= RowStep;
            var back = UiFactory.MakePanel(rect, "Back", new Color(1f, 1f, 1f, 0.05f));
            UiFactory.Stretch(back.rectTransform);
            var text = UiFactory.MakeLabel(rect, label, 22, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            UiFactory.Place(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(LabelWidth - 24f, RowHeight));
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 12;
            text.resizeTextMaxSize = 22;
            return rect;
        }

        InputField Field(RectTransform row, float x, float width, InputField.ContentType type)
        {
            var f = UiFactory.MakeInputField(row, 22, FieldColor, type);
            UiFactory.Place((RectTransform)f.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(width, RowHeight - 6f));
            return f;
        }

        void InfoRow(Transform page, string label, ref float y, Func<string> get)
        {
            var row = Row(page, label, ref y);
            var value = UiFactory.MakeLabel(row, "", 22, TextAnchor.MiddleLeft, AccentColor, FontStyle.Bold);
            UiFactory.Place(value.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(LabelWidth, 0f), new Vector2(PageWidth - LabelWidth, RowHeight));
            refreshers.Add(() => value.text = get());
        }

        void TextRow(Transform page, string label, ref float y, Func<string> get, Action<string> set)
        {
            var row = Row(page, label, ref y);
            var f = Field(row, LabelWidth, 440f, InputField.ContentType.Standard);
            f.characterLimit = 40;
            f.onEndEdit.AddListener(t =>
            {
                if (def == null) return;
                set(t);
                Changed();
            });
            refreshers.Add(() => { if (!f.isFocused) f.text = get(); });
        }

        void NumberRow(Transform page, string label, ref float y, Func<float> get, Action<float> set)
        {
            var row = Row(page, label, ref y);
            var f = Field(row, LabelWidth, 160f, InputField.ContentType.DecimalNumber);
            f.onEndEdit.AddListener(t =>
            {
                if (def == null) return;
                if (TryParse(t, out float v))
                {
                    set(v);
                    def.Normalize();
                    Changed();
                }
                else Refresh();
            });
            refreshers.Add(() => SetField(f, get()));
        }

        void Vec3Row(Transform page, string label, ref float y, Func<Vector3> get, Action<Vector3> set)
        {
            var row = Row(page, label, ref y);
            var fields = new InputField[3];
            string[] axes = { "X", "Y", "Z" };
            for (int i = 0; i < 3; i++)
            {
                int axis = i;
                fields[i] = Field(row, LabelWidth + i * 150f, 140f, InputField.ContentType.DecimalNumber);
                UiFactory.SetPlaceholder(fields[i], axes[i]);
                fields[i].onEndEdit.AddListener(t =>
                {
                    if (def == null) return;
                    if (TryParse(t, out float v))
                    {
                        var vec = get();
                        vec[axis] = v;
                        set(vec);
                        Changed();
                    }
                    else Refresh();
                });
            }
            refreshers.Add(() =>
            {
                var v = get();
                for (int i = 0; i < 3; i++) SetField(fields[i], v[i]);
            });
        }

        void ToggleRow(Transform page, string label, ref float y, Func<string> text, Func<bool> on, Action flip)
        {
            var row = Row(page, label, ref y);
            var b = UiFactory.MakeButton(row, "", FieldColor, () =>
            {
                if (def == null) return;
                flip();
                Changed();
            }, 20);
            UiFactory.Place((RectTransform)b.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(LabelWidth, 0f), new Vector2(300f, RowHeight - 6f));
            var t = b.GetComponentInChildren<Text>();
            var image = (Image)b.targetGraphic;
            refreshers.Add(() =>
            {
                t.text = text();
                image.color = on() ? OnColor : FieldColor;
                t.color = TilePalette.TextColorOn(image.color);
            });
        }

        void CycleRow(Transform page, string label, ref float y, Func<string> text, Action<int> step)
        {
            var row = Row(page, label, ref y);
            var prev = UiFactory.MakeButton(row, "<", ButtonColor, () => { if (def == null) return; step(-1); Changed(); }, 24);
            UiFactory.Place((RectTransform)prev.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(LabelWidth, 0f), new Vector2(56f, RowHeight - 6f));
            var value = UiFactory.MakeLabel(row, "", 22, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            UiFactory.Place(value.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(LabelWidth + 62f, 0f), new Vector2(240f, RowHeight));
            var next = UiFactory.MakeButton(row, ">", ButtonColor, () => { if (def == null) return; step(1); Changed(); }, 24);
            UiFactory.Place((RectTransform)next.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(LabelWidth + 308f, 0f), new Vector2(56f, RowHeight - 6f));
            refreshers.Add(() => value.text = text());
        }

        void ButtonsRow(Transform page, string label, ref float y, Func<string> text, params (string name, UnityAction click)[] buttons)
        {
            var row = Row(page, label, ref y);
            var value = UiFactory.MakeLabel(row, "", 18, TextAnchor.MiddleLeft, AccentColor, FontStyle.Bold);
            UiFactory.Place(value.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(LabelWidth, 0f), new Vector2(230f, RowHeight));
            value.resizeTextForBestFit = true;
            value.resizeTextMinSize = 10;
            value.resizeTextMaxSize = 18;
            float x = -6f;
            for (int i = buttons.Length - 1; i >= 0; i--)
            {
                var click = buttons[i].click;
                float w = buttons[i].name.Length > 8 ? 150f : 110f;
                var b = UiFactory.MakeButton(row, buttons[i].name, ButtonColor, () => { if (def != null) click(); }, 18);
                UiFactory.Place((RectTransform)b.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(x, 0f), new Vector2(w, RowHeight - 8f));
                x -= w + 6f;
            }
            refreshers.Add(() => value.text = text());
        }

        static void SetField(InputField field, float value)
        {
            if (!field.isFocused) field.text = value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        static bool TryParse(string text, out float value) =>
            float.TryParse((text ?? "").Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        // ---------------- Collision grid ----------------

        void ToggleBox(int dx, int dz)
        {
            if (def == null) return;
            int i = def.boxes.FindIndex(b => b.x == dx && b.y == boxHeight && b.z == dz);
            if (i >= 0) def.boxes.RemoveAt(i);
            else def.boxes.Add(new CollisionBox(dx, boxHeight, dz));
            Changed();
        }

        void RefreshBoxes()
        {
            for (int h = 0; h < MaxHeight; h++) heightButtons[h].color = h == boxHeight ? AccentColor : ButtonColor;
            if (def == null) return;
            for (int row = 0; row < GridSize; row++)
                for (int lane = 0; lane < GridSize; lane++)
                {
                    int dz = GridMid - row, dx = lane - GridMid;
                    bool here = false;
                    var others = new List<int>();
                    foreach (var b in def.boxes)
                    {
                        if (b.x != dx || b.z != dz) continue;
                        if (b.y == boxHeight) here = true;
                        else others.Add(b.y);
                    }
                    others.Sort();
                    boxCells[row, lane].color = here ? BoxOn : BoxOff;
                    // The other heights with a box in this column, so stacked boxes are not forgotten.
                    boxTexts[row, lane].text = others.Count > 0 ? "h " + string.Join(",", others) : "";
                    boxTexts[row, lane].color = Color.white;
                }
        }

        // ---------------- Preview ----------------

        void EnsureStage()
        {
            if (previewCam != null) return;
            previewTexture = new RenderTexture(640, 640, 24) { name = "Custom Obstacle Preview", antiAliasing = 4 };
            previewImage.texture = previewTexture;
            stage = new GameObject("Custom Obstacle Preview").transform;
            stage.position = new Vector3(0f, -500f, 0f); // far below the level, its own layer anyway

            previewCam = new GameObject("Custom Obstacle Preview Camera").AddComponent<Camera>();
            previewCam.targetTexture = previewTexture;
            previewCam.cullingMask = 1 << PreviewLayer;
            previewCam.clearFlags = CameraClearFlags.SolidColor;
            previewCam.backgroundColor = new Color(0.35f, 0.75f, 0.85f);
            previewCam.fieldOfView = 40f;
            previewCam.nearClipPlane = 0.1f;
            previewCam.farClipPlane = 200f;
            previewCam.enabled = false;

            previewLight = new GameObject("Custom Obstacle Preview Light").AddComponent<Light>();
            previewLight.type = LightType.Directional;
            previewLight.cullingMask = 1 << PreviewLayer;
            previewLight.shadows = LightShadows.Soft;
            previewLight.intensity = 1.2f;
            previewLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            previewLight.enabled = false;

            // No other camera draws the preview.
            foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (c != previewCam) c.cullingMask &= ~(1 << PreviewLayer);
        }

        void RebuildPreview()
        {
            if (def == null || stage == null) return;
            var look = editor.CurrentLook();
            if (floor == null || look != stageLook) BuildFloor(look);
            if (built != null) Destroy(built);
            built = CustomObstacleBuilder.Build(stage, def, look, editor.CustomThemes(), CustomObstacleBuilder.Mode.Preview, showHitbox || tab == 1);
            SetLayer(built);
            FitCamera();
            BackgroundView.On(previewCam)?.Show(look);
        }

        void BuildFloor(TrackTheme look)
        {
            if (floor != null) Destroy(floor);
            stageLook = look;
            floor = new GameObject("Floor");
            floor.transform.SetParent(stage, false);
            var mesh = MeshFactory.BeveledBox(new Vector3(0.96f, 0.5f, 0.96f), 0.04f);
            var mat = look != null ? (look.floor != null ? look.floor : look.GeneralModelMaterial) : null;
            for (int z = -GridMid; z <= GridMid; z++)
                for (int x = -GridMid; x <= GridMid; x++)
                {
                    var tile = new GameObject($"Tile {x},{z}");
                    tile.transform.SetParent(floor.transform, false);
                    tile.transform.localPosition = new Vector3(x, -0.25f, z);
                    tile.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var r = tile.AddComponent<MeshRenderer>();
                    if (mat != null) r.sharedMaterial = mat;
                }
            SetLayer(floor);
        }

        /// <summary>Distance and aim from the size of the obstacle (model and hitbox).</summary>
        void FitCamera()
        {
            if (built == null) return;
            bool any = false;
            var bounds = new Bounds();
            foreach (var r in built.GetComponentsInChildren<Renderer>())
            {
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            if (!any) bounds = new Bounds(stage.position + Vector3.up * 0.5f, Vector3.one);
            focus = bounds.center - stage.position;
            distance = Mathf.Clamp(bounds.extents.magnitude * 3.2f, 3.5f, 40f);
        }

        void TogglePlay()
        {
            playStart = playStart >= 0f ? -1f : Time.unscaledTime;
            Refresh();
        }

        void Update()
        {
            if (def == null || previewCam == null) return;
            float ahead = 0f;
            if (playStart >= 0f)
            {
                var clip = CurrentClip();
                float from = Mathf.Max(def.startRows, def.endRows, def.soundRows, clip != null ? clip.startRows : 0f) + 2f;
                ahead = from - (Time.unscaledTime - playStart) * PlayRowsPerSecond;
                if (ahead < -2f) playStart = Time.unscaledTime; // loop until Stop
            }
            if (built != null)
            {
                foreach (var m in built.GetComponentsInChildren<CustomObstacleMotion>()) m.previewAhead = ahead;
                foreach (var m in built.GetComponentsInChildren<ModelAnimator>()) m.previewAhead = ahead;
            }
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            previewCam.transform.position = stage.position + focus + rot * new Vector3(0f, 0f, -distance);
            previewCam.transform.rotation = rot;
        }

        static void SetLayer(GameObject go)
        {
            go.layer = PreviewLayer;
            foreach (Transform t in go.transform) SetLayer(t.gameObject);
        }

        void OnDestroy()
        {
            if (previewTexture != null) previewTexture.Release();
        }
    }
}
