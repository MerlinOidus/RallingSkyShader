using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RTXOn
{
    /// <summary>
    /// The level editor's SPRITEMAKER window (Custom Theme > Textures > Make...): builds a Rolling Sky atlas from the
    /// white mask layers in Resources/SpriteMaker (see <see cref="SpriteMakerLab"/>), like Adrian's Texture Lab.
    /// Left: atlas, view (colour atlas or glow map), base colour, project files, then the selected layer (colour,
    /// opacity, blend, glow map, order) and Enemy's top right / Palette Order. Middle: the atlas; click it to pick the
    /// layer there. Right: every layer, with search. Use puts the picture in the Custom Theme texture it was opened for.
    /// </summary>
    public class SpriteMakerPanel : PanelKit
    {
        const float WindowWidth = 1860f, WindowHeight = 980f;
        const float PageLeft = 24f, PageTop = -100f;
        const float PreviewSize = 620f;
        const float ListWidth = 392f;
        const float ItemHeight = 38f, ItemGap = 4f;
        const float RebuildDelay = 0.12f; // seconds after the last change before the atlas is drawn again

        SpriteMakerLab lab;
        int groupIndex;
        int selected = -1;
        bool glowView;
        string search = "";
        Color bulkColor = new Color(1f, 0.18f, 0.58f);
        Action<string> use;      // where Use puts the PNG (base64), null = Save PNG only
        Action<string> live;     // 3D: the Custom Theme preview with this PNG until Use (null PNG = back to its own)
        string slot;             // the Custom Theme texture it was opened for
        bool show3D;             // the middle shows LevelTexturePreview.json in 3D instead of the atlas
        LevelData previewLevel;
        float previewRow;
        float rebuildAt = -1f;
        bool listDirty = true;

        Text title;
        GameObject useButton;
        Image button3D;
        RectTransform atlasArea;
        RawImage scene;
        AspectRatioFitter sceneFit;
        RawImage preview, checker;
        AspectRatioFitter previewFit;
        RectTransform selectionFrame;
        Texture2D previewTex, checkerTex;
        InputField searchField;
        ScrollRect scroll;
        RectTransform listContent;
        readonly List<Item> items = new List<Item>();
        readonly SpriteLayerState noLayer = new SpriteLayerState { n = "" };

        class Item
        {
            public GameObject go;
            public Image back, swatch;
            public Text label, eyeLabel;
            public Image eye;
        }

        protected override bool Editable => lab != null;

        SpriteMakerLab.GroupDef Def => SpriteMakerLab.Groups[groupIndex];
        SpriteMakerLab.GroupState S => lab.State(Def);
        SpriteMakerLab.Layer Sel => lab != null && selected >= 0 && selected < S.layers.Count && !Def.frames ? S.layers[selected] : null;
        SpriteLayerState L => Sel != null ? Sel.st : noLayer;

        public static SpriteMakerPanel Build(LevelEditor owner, Transform parent)
        {
            var bg = UiFactory.MakePanel(parent, "SpriteMaker", PanelColor, true);
            UiFactory.Place(bg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(WindowWidth, WindowHeight));
            var panel = bg.gameObject.AddComponent<SpriteMakerPanel>();
            panel.editor = owner;
            panel.BuildUi((RectTransform)bg.transform);
            bg.gameObject.SetActive(false);
            return panel;
        }

        // ---------------- Open / close ----------------

        /// <summary>
        /// Opens on the atlas <paramref name="groupKey"/> (null = the last one). <paramref name="useFor"/> gets the PNG as
        /// base64 when Use is pressed (null = no Use button); <paramref name="glow"/> starts on the glow map view.
        /// </summary>
        public void Open(string groupKey, string slotName, bool glow, Action<string> useFor, Action<string> liveFor = null)
        {
            if (lab == null)
            {
                lab = new SpriteMakerLab();
                LoadAutosave();
            }
            int g = SpriteMakerLab.Groups.FindIndex(x => x.key == groupKey);
            if (g >= 0 && g != groupIndex)
            {
                groupIndex = g;
                selected = -1;
                listDirty = true;
            }
            glowView = glow;
            use = useFor;
            live = liveFor;
            slot = slotName;
            useButton.SetActive(use != null);
            button3D.gameObject.SetActive(live != null);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            ClosePopups();
            Changed();
            rebuildAt = 0f;
            if (!SpriteMakerLab.HasMasks)
                Status("No masks found: copy the SpriteMaker folder into Assets (Assets/SpriteMaker/Resources/SpriteMaker/index.txt must exist).");
            else if (use != null)
                Status(glow ? $"{slot}: the glow map is black with only the layers that are In glow map. Use puts it in the Custom Theme."
                            : $"{slot}: colour the layers (click the atlas to pick one), then Use puts the atlas in the Custom Theme.");
            else Status("Colour the layers (click the atlas to pick one), then Save PNG.");
        }

        public void Close()
        {
            if (!IsOpen) return;
            if (ClosePopups()) return;
            Hide();
        }

        /// <summary>Closes; the atlases keep their colours for the next time.</summary>
        public void Hide()
        {
            if (!IsOpen) return;
            Show3D(false);
            if (refWindow != null) refWindow.gameObject.SetActive(false);
            gameObject.SetActive(false);
            use = null;
            live = null;
        }

        void Use()
        {
            if (use == null || lab == null) return;
            var png = lab.Png(S, glowView);
            var target = use;
            Hide();
            target(Convert.ToBase64String(png));
        }

        /// <summary>The middle shows the atlas, or (3D) LevelTexturePreview.json with this atlas in the Custom Theme being edited.</summary>
        void Show3D(bool on)
        {
            if (on && live == null) on = false;
            if (on && previewLevel == null)
            {
                previewLevel = LoadPreviewLevel(out string from);
                if (previewLevel == null)
                {
                    Status("No LevelTexturePreview.json: copy Assets/SpriteMaker/Resources/SpriteMaker/LevelTexturePreview.json into the project again.");
                    on = false;
                }
                else previewRow = 0f;
            }
            show3D = on;
            atlasArea.gameObject.SetActive(!on);
            scene.transform.parent.gameObject.SetActive(on);
            button3D.color = on ? AccentColor : ButtonColor;
            if (!on)
            {
                live?.Invoke(null); // the Custom Theme preview goes back to its own texture
                editor.EndSpriteMaker3D();
                return;
            }
            editor.SetPreviewLevel(previewLevel, previewRow);
            scene.texture = editor.ThemePreviewTexture;
            if (scene.texture != null) sceneFit.aspectRatio = (float)scene.texture.width / Mathf.Max(1, scene.texture.height);
            rebuildAt = Time.unscaledTime; // the atlas goes to the preview now
            Status($"3D: LevelTexturePreview.json with this {(glowView ? "glow map" : "atlas")} (not saved until Use). Drag or scroll the picture to move along it; 3D again = the atlas.");
        }

        void MoveScene(float rows)
        {
            if (!show3D || previewLevel == null) return;
            previewRow = Mathf.Clamp(previewRow + rows, 0f, Mathf.Max(0, previewLevel.RowCount - 4));
            editor.SetPreviewLevel(previewLevel, previewRow);
        }

        /// <summary>
        /// LevelTexturePreview.json (until 2026-10-25 called LevelPreview.json; that name is now the Main Menu's 3D
        /// LevelPreview) from the levels folder when there is one (made with New+ and saved under that name), else the
        /// SpriteMaker's own. The old name is still read.
        /// </summary>
        public static LevelData LoadPreviewLevel(out string from)
        {
            foreach (var name in new[] { TexturePreviewName, OldTexturePreviewName })
            {
                string path = Path.Combine(LevelFiles.LevelsFolder(), name + ".json");
                var level = LevelFiles.OpenPath(path, out _, out _);
                from = path;
                if (level != null) return level;
            }
            foreach (var name in new[] { TexturePreviewName, OldTexturePreviewName })
            {
                from = "Resources/" + SpriteMakerLab.ResourceRoot + "/" + name;
                var asset = Resources.Load<TextAsset>(SpriteMakerLab.ResourceRoot + "/" + name);
                if (asset == null) continue;
                try { return LevelData.ParseJson(asset.text); }
                catch (Exception e)
                {
                    Debug.LogWarning($"RTXOn: {name}.json could not be read: " + e.Message);
                }
            }
            from = null;
            return null;
        }

        public const string TexturePreviewName = "LevelTexturePreview";
        const string OldTexturePreviewName = "LevelPreview";

        void SavePng()
        {
            var png = lab.Png(S, glowView);
            string name = Def.key + (Def.frames ? "_" + S.frame.ToString("000") : "") + (glowView ? "_Glow" : "") + ".png";
            Status(editor.Files.SaveBytes("Save PNG", name, png));
        }

        void OpenProject()
        {
            editor.Files.Open("Open a SpriteMaker / Texture Lab project", new[] { "json" }, path =>
            {
                string text;
                try { text = File.ReadAllText(path); }
                catch (Exception e) { Status("Could not read the file: " + e.Message); return; }
                var g = lab.LoadProject(text);
                if (g == null)
                {
                    Status($"{Path.GetFileName(path)} is not a SpriteMaker / Texture Lab project.");
                    return;
                }
                groupIndex = SpriteMakerLab.Groups.IndexOf(g);
                selected = -1;
                listDirty = true;
                Changed();
                Status("Opened " + Path.GetFileName(path) + ".");
            }, Status);
        }

        void SaveProject() => Status(editor.Files.Save("Save SpriteMaker project", "SpriteMakerProject.json", lab.ProjectJson(Def)));

        void ImportPalette()
        {
            editor.Files.Open("Palette Order (JSON)", new[] { "json", "txt" }, path =>
            {
                string text;
                try { text = File.ReadAllText(path); }
                catch (Exception e) { Status("Could not read the file: " + e.Message); return; }
                var problem = lab.ApplyPaletteJson(text);
                Changed();
                Status(problem ?? $"Palette Order from {Path.GetFileName(path)}: {lab.palette.elements.Length} palette(s).");
            }, Status);
        }

        void ImportGlowPalette()
        {
            editor.Files.Open("Glow palettes: a Palette Order (JSON) for the glow map only", new[] { "json", "txt" }, path =>
            {
                string text;
                try { text = File.ReadAllText(path); }
                catch (Exception e) { Status("Could not read the file: " + e.Message); return; }
                var problem = lab.ApplyGlowPaletteJson(text);
                Changed();
                Status(problem ?? $"Glow palettes from {Path.GetFileName(path)}: {lab.glowPalette.elements.Length} palette(s), in the glow map only.");
            }, Status);
        }

        // ---------------- Changes ----------------

        protected override void Changed()
        {
            if (lab == null) return;
            selected = Mathf.Clamp(selected, -1, S.layers.Count - 1);
            Refresh();
            if (listDirty) BuildList();
            UpdateList();
            title.text = $"SPRITEMAKER  ·  {Def.label.ToUpperInvariant()}" + (glowView ? "  ·  GLOW MAP" : "") +
                         (use != null ? $"  ·  for {slot}" : "");
            rebuildAt = Time.unscaledTime + RebuildDelay;
            autosaveAt = Time.unscaledTime + AutosaveDelay;
        }

        void Update()
        {
            if (lab != null && autosaveAt >= 0f && Time.unscaledTime >= autosaveAt) WriteAutosave();
            if (lab == null || rebuildAt < 0f || Time.unscaledTime < rebuildAt) return;
            rebuildAt = -1f;
            Redraw();
        }

        // Autosave: every colour, layer and palette is kept in Application.persistentDataPath/SpriteMaker/Autosave.json
        // (a project file) a moment after each change and when the window closes, and comes back the next time it opens,
        // also after Unity stops Play or rebuilds the scripts.
        const float AutosaveDelay = 2f;
        float autosaveAt = -1f;

        static string AutosaveFile => Path.Combine(Application.persistentDataPath, "SpriteMaker", "Autosave.json");

        void WriteAutosave()
        {
            autosaveAt = -1f;
            if (lab == null) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(AutosaveFile));
                File.WriteAllText(AutosaveFile, lab.ProjectJson(Def));
            }
            catch (Exception e) { Debug.LogWarning("RTXOn: SpriteMaker autosave failed: " + e.Message); }
        }

        void LoadAutosave()
        {
            try
            {
                if (!File.Exists(AutosaveFile)) return;
                var g = lab.LoadProject(File.ReadAllText(AutosaveFile));
                if (g != null) groupIndex = SpriteMakerLab.Groups.IndexOf(g);
                listDirty = true;
            }
            catch (Exception e) { Debug.LogWarning("RTXOn: SpriteMaker autosave could not be read: " + e.Message); }
        }

        void OnDisable()
        {
            if (lab != null && autosaveAt >= 0f) WriteAutosave();
        }

        void Redraw()
        {
            var s = S;
            int w = s.def.w, h = s.def.h;
            if (previewTex == null || previewTex.width != w || previewTex.height != h)
            {
                if (previewTex != null) Destroy(previewTex);
                previewTex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "SpriteMaker Preview" };
            }
            previewTex.SetPixels(lab.Compose(s, glowView));
            previewTex.Apply();
            if (show3D && live != null) live(Convert.ToBase64String(previewTex.EncodeToPNG()));
            preview.texture = previewTex;
            previewFit.aspectRatio = (float)w / h;
            checker.uvRect = new Rect(0f, 0f, w / 16f, h / 16f);
            ShowSelection();
        }

        /// <summary>A red frame around the selected layer's pixels.</summary>
        void ShowSelection()
        {
            var layer = Sel;
            var m = layer != null ? (layer.mask = layer.mask ?? SpriteMakerLab.GetMask(layer.path)) : null;
            if (m == null || m.bb.width == 0)
            {
                selectionFrame.gameObject.SetActive(false);
                return;
            }
            selectionFrame.gameObject.SetActive(true);
            selectionFrame.anchorMin = new Vector2((float)m.bb.xMin / m.w, (float)m.bb.yMin / m.h);
            selectionFrame.anchorMax = new Vector2((float)m.bb.xMax / m.w, (float)m.bb.yMax / m.h);
            selectionFrame.offsetMin = new Vector2(-2f, -2f);
            selectionFrame.offsetMax = new Vector2(2f, 2f);
        }

        void Select(int index, bool scrollTo)
        {
            if (Def.frames)
            {
                S.frame = Mathf.Clamp(index, 0, S.layers.Count - 1);
                Changed();
                return;
            }
            selected = index;
            Refresh();
            UpdateList();
            ShowSelection();
            if (scrollTo) ScrollTo(index);
            if (Sel != null) Status($"Layer {Sel.name} ({SpriteMakerLab.Role(Sel.name)}).");
        }

        /// <summary>Click on the atlas: the topmost shown layer with a pixel there.</summary>
        void PickAt(PointerEventData e)
        {
            if (lab == null || Def.frames) return;
            var rect = preview.rectTransform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, e.pressEventCamera, out var local)) return;
            var r = rect.rect;
            float u = (local.x - r.xMin) / r.width, v = (local.y - r.yMin) / r.height;
            if (u < 0f || v < 0f || u > 1f || v > 1f) return;
            var s = S;
            int px = Mathf.FloorToInt(u * s.def.w), py = Mathf.FloorToInt(v * s.def.h);
            for (int i = s.layers.Count - 1; i >= 0; i--)
            {
                var layer = s.layers[i];
                if (!layer.st.on || layer.st.o <= 0f || (glowView && !layer.InGlowMap)) continue;
                layer.mask = layer.mask ?? SpriteMakerLab.GetMask(layer.path);
                if (SpriteMakerLab.AlphaAt(layer.mask, px, py) > 40)
                {
                    if (!string.IsNullOrEmpty(search))
                    {
                        search = "";
                        searchField.text = "";
                    }
                    Select(i, true);
                    return;
                }
            }
            selected = -1;
            Refresh();
            UpdateList();
            ShowSelection();
            Status("No shown layer at that point.");
        }

        void Move(int step)
        {
            var layers = S.layers;
            int to = selected + step;
            if (Sel == null || to < 0 || to >= layers.Count) return;
            var t = layers[selected];
            layers[selected] = layers[to];
            layers[to] = t;
            selected = to;
            listDirty = true;
            Changed();
            ScrollTo(selected);
        }

        bool Match(SpriteMakerLab.Layer layer) => string.IsNullOrEmpty(search) || layer.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;

        void SetFound(bool on)
        {
            int n = 0;
            foreach (var layer in S.layers)
                if (Match(layer))
                {
                    layer.st.on = on;
                    n++;
                }
            Changed();
            Status($"{n} layer(s) {(on ? "shown" : "hidden")}.");
        }

        void ColourFound()
        {
            if (string.IsNullOrEmpty(search))
            {
                Status("Type a search first (for example border or palette): Colour... colours the layers it finds.");
                return;
            }
            OpenColor($"Colour of the layers with \"{search}\"", bulkColor, false, c =>
            {
                bulkColor = c;
                int n = 0;
                foreach (var layer in S.layers)
                    if (Match(layer))
                    {
                        layer.st.c = SpriteMakerLab.ToHex(c);
                        n++;
                    }
                Status($"{n} layer(s) coloured {SpriteMakerLab.ToHex(c)}.");
            });
        }

        /// <summary>The found layers into the glow map, or (when they all are already) out of it.</summary>
        void GlowFound()
        {
            if (Def.frames) return;
            if (string.IsNullOrEmpty(search))
            {
                Status("Type a search first (for example border): Glow puts the layers it finds in the glow map, or takes them out.");
                return;
            }
            bool all = true;
            int n = 0;
            foreach (var layer in S.layers)
                if (Match(layer))
                {
                    all &= layer.InGlowMap;
                    n++;
                }
            foreach (var layer in S.layers)
                if (Match(layer)) layer.st.gm = all ? "off" : "on";
            Changed();
            Status($"{n} layer(s) {(all ? "taken out of" : "put in")} the glow map{(glowView ? "" : " (Show: Glow map to see it)")}.");
        }

        // ---------------- Layer list ----------------

        void BuildList()
        {
            listDirty = false;
            foreach (var it in items) Destroy(it.go);
            items.Clear();
            var layers = S.layers;
            for (int i = 0; i < layers.Count; i++)
            {
                int index = i;
                var b = UiFactory.MakeButton(listContent, "", ButtonColor, () => Select(index, false), 17);
                var it = new Item { go = b.gameObject, back = (Image)b.targetGraphic, label = b.GetComponentInChildren<Text>() };
                it.label.alignment = TextAnchor.MiddleLeft;
                UiFactory.Stretch(it.label.rectTransform, 44f, 0f, 64f, 0f);
                it.label.resizeTextForBestFit = true;
                it.label.resizeTextMinSize = 10;
                it.label.resizeTextMaxSize = 17;
                it.swatch = UiFactory.MakePanel(b.transform, "Swatch", Color.white);
                UiFactory.Place(it.swatch.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(28f, 26f));
                if (!Def.frames)
                {
                    var eye = UiFactory.MakeButton(b.transform, "on", FieldColor, () =>
                    {
                        var st = S.layers[index].st;
                        st.on = !st.on;
                        Changed();
                    }, 15);
                    UiFactory.Place((RectTransform)eye.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-4f, 0f), new Vector2(54f, ItemHeight - 8f));
                    it.eye = (Image)eye.targetGraphic;
                    it.eyeLabel = eye.GetComponentInChildren<Text>();
                }
                items.Add(it);
            }
        }

        void UpdateList()
        {
            var layers = S.layers;
            for (int i = 0; i < items.Count && i < layers.Count; i++)
            {
                var it = items[i];
                var layer = layers[i];
                bool on = Def.frames || layer.st.on;
                it.go.SetActive(Match(layer));
                it.label.text = layer.name + (glowView && !Def.frames && layer.InGlowMap ? "  (glow)" : "");
                it.label.color = on ? Color.white : new Color(1f, 1f, 1f, 0.4f);
                var c = SpriteMakerLab.Hex(layer.st.c);
                c.a = on ? 1f : 0.3f;
                it.swatch.color = c;
                bool chosen = Def.frames ? i == S.frame : i == selected;
                it.back.color = chosen ? AccentColor : ButtonColor;
                if (it.eye != null)
                {
                    it.eye.color = layer.st.on ? OnColor : FieldColor;
                    it.eyeLabel.text = layer.st.on ? "on" : "off";
                }
            }
        }

        void ScrollTo(int index)
        {
            Canvas.ForceUpdateCanvases();
            int shown = 0;
            for (int i = 0; i < index && i < items.Count; i++) if (items[i].go.activeSelf) shown++;
            float contentHeight = listContent.rect.height, viewHeight = scroll.viewport.rect.height;
            if (contentHeight <= viewHeight) return;
            float y = 8f + shown * (ItemHeight + ItemGap);
            scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01((y - viewHeight * 0.4f) / (contentHeight - viewHeight));
        }

        // ---------------- Reference picture ----------------
        // Any PNG / JPG beside the atlas, in its own window that can be dragged and zoomed. Clicking it takes the exact
        // pixel: into the colour picker when it is open, else as the selected layer's colour. The picker's Eyedropper
        // reads it from the screen too.

        const string ReferenceKey = "RTXOn.SpriteMaker.Reference";
        const float RefBase = 440f;
        RectTransform refWindow;
        RawImage refImage;
        AspectRatioFitter refFit;
        Text refTitle;
        Texture2D refTexture;
        string refName = "";
        float refZoom = 1f;

        void ToggleReference()
        {
            if (!Editable) return;
            if (refWindow == null) BuildReference((RectTransform)transform);
            if (refWindow.gameObject.activeSelf)
            {
                refWindow.gameObject.SetActive(false);
                return;
            }
            if (refTexture == null)
            {
                string last = "";
                try { last = PlayerPrefs.GetString(ReferenceKey, ""); } catch (Exception) { }
                if (string.IsNullOrEmpty(last) || !LoadReference(last))
                {
                    OpenReference();
                    return;
                }
            }
            ShowReference();
        }

        void OpenReference()
        {
            editor.Files.Open("Reference picture (PNG / JPG)", new[] { "png", "jpg", "jpeg" }, path =>
            {
                if (LoadReference(path))
                {
                    try { PlayerPrefs.SetString(ReferenceKey, path); } catch (Exception) { }
                    ShowReference();
                }
            }, Status);
        }

        bool LoadReference(string path)
        {
            try
            {
                if (!File.Exists(path)) return false;
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = Path.GetFileName(path) };
                if (!tex.LoadImage(File.ReadAllBytes(path), false))
                {
                    Destroy(tex);
                    Status("Could not read that picture (PNG or JPG only).");
                    return false;
                }
                tex.filterMode = FilterMode.Point; // sharp pixels when zoomed in, so the pick is easy to aim
                if (refTexture != null) Destroy(refTexture);
                refTexture = tex;
                refName = tex.name;
                return true;
            }
            catch (Exception e)
            {
                Status("Could not open the picture: " + e.Message);
                return false;
            }
        }

        void ShowReference()
        {
            if (refWindow == null) BuildReference((RectTransform)transform);
            refImage.texture = refTexture;
            refFit.aspectRatio = refTexture != null ? refTexture.width / (float)Mathf.Max(1, refTexture.height) : 1f;
            refTitle.text = $"REFERENCE  ·  {refName}" + (refTexture != null ? $"  ({refTexture.width}x{refTexture.height})" : "");
            SizeReference();
            refWindow.gameObject.SetActive(true);
            refWindow.SetAsLastSibling();
            Status("Reference: click it to take that colour (into the open colour picker, else the selected layer). Drag the top bar to move it.");
        }

        void SizeReference() => refWindow.sizeDelta = new Vector2(RefBase * refZoom + 20f, RefBase * refZoom + 76f);

        void ZoomReference(float factor)
        {
            refZoom = Mathf.Clamp(refZoom * factor, 0.5f, 2.5f);
            SizeReference();
        }

        /// <summary>Clicked the reference: the exact pixel there.</summary>
        void PickReference(PointerEventData e)
        {
            if (refTexture == null) return;
            var rect = refImage.rectTransform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, e.pressEventCamera, out var local)) return;
            var r = rect.rect;
            float u = (local.x - r.xMin) / Mathf.Max(1f, r.width), v = (local.y - r.yMin) / Mathf.Max(1f, r.height);
            if (u < 0f || u > 1f || v < 0f || v > 1f) return;
            var c = refTexture.GetPixel(Mathf.Clamp((int)(u * refTexture.width), 0, refTexture.width - 1),
                                        Mathf.Clamp((int)(v * refTexture.height), 0, refTexture.height - 1));
            c.a = 1f;
            string hex = "#" + ColorUtility.ToHtmlStringRGB(c);
            if (ColorPickerOpen)
            {
                PickIntoColor(c);
                Status($"Reference {hex} put in the colour picker.");
            }
            else if (Sel != null)
            {
                L.c = SpriteMakerLab.ToHex(c);
                Changed();
                Status($"{Sel.name}: colour {hex} from the reference.");
            }
            else Status($"Reference colour {hex}: pick a layer first (click the atlas or the list), or open a colour's Edit.");
        }

        void BuildReference(RectTransform root)
        {
            var bg = UiFactory.MakePanel(root, "Reference", new Color(0.09f, 0.08f, 0.11f, 0.98f), true);
            refWindow = bg.rectTransform;
            refWindow.anchorMin = refWindow.anchorMax = new Vector2(0.5f, 0.5f);
            refWindow.pivot = new Vector2(0.5f, 0.5f);
            refWindow.anchoredPosition = new Vector2(200f, 0f);

            // Top bar: drag it to move the window.
            var bar = UiFactory.MakePanel(refWindow, "Bar", new Color(0.22f, 0.19f, 0.26f), true);
            bar.rectTransform.anchorMin = new Vector2(0f, 1f);
            bar.rectTransform.anchorMax = new Vector2(1f, 1f);
            bar.rectTransform.pivot = new Vector2(0.5f, 1f);
            bar.rectTransform.sizeDelta = new Vector2(0f, 56f);
            bar.rectTransform.anchoredPosition = Vector2.zero;
            var drag = bar.gameObject.AddComponent<EventTrigger>();
            var moved = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            moved.callback.AddListener(d =>
            {
                var canvas = GetComponentInParent<Canvas>();
                float scale = canvas != null ? canvas.rootCanvas.scaleFactor : 1f;
                refWindow.anchoredPosition += ((PointerEventData)d).delta / Mathf.Max(0.01f, scale);
            });
            drag.triggers.Add(moved);
            refTitle = UiFactory.MakeLabel(bar.transform, "REFERENCE", 18, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            refTitle.rectTransform.anchorMin = Vector2.zero;
            refTitle.rectTransform.anchorMax = Vector2.one;
            refTitle.rectTransform.offsetMin = new Vector2(12f, 0f);
            refTitle.rectTransform.offsetMax = new Vector2(-250f, 0f);
            refTitle.resizeTextForBestFit = true;
            refTitle.resizeTextMinSize = 10;
            refTitle.resizeTextMaxSize = 18;
            refTitle.raycastTarget = false;
            (string label, float width, UnityEngine.Events.UnityAction act)[] buttons =
            {
                ("Open...", 92f, OpenReference), ("-", 44f, () => ZoomReference(1f / 1.25f)), ("+", 44f, () => ZoomReference(1.25f)),
                ("X", 44f, () => refWindow.gameObject.SetActive(false)),
            };
            float x = -6f;
            for (int i = buttons.Length - 1; i >= 0; i--)
            {
                var b = UiFactory.MakeButton(bar.transform, buttons[i].label, ButtonColor, buttons[i].act, 18);
                UiFactory.Place((RectTransform)b.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(x, 0f), new Vector2(buttons[i].width, 44f));
                x -= buttons[i].width + 6f;
            }

            // The picture, on a checker so see-through parts show, kept in its own shape.
            var area = UiFactory.MakeRect(refWindow, "Area");
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.offsetMin = new Vector2(10f, 10f);
            area.offsetMax = new Vector2(-10f, -66f);
            var fitted = UiFactory.MakeRect(area, "Picture");
            UiFactory.Stretch(fitted);
            refFit = fitted.gameObject.AddComponent<AspectRatioFitter>();
            refFit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            var back = UiFactory.MakeRect(fitted, "Checker").gameObject.AddComponent<RawImage>();
            UiFactory.Stretch(back.rectTransform);
            back.texture = Checker();
            back.raycastTarget = false;
            refImage = UiFactory.MakeRect(fitted, "Image").gameObject.AddComponent<RawImage>();
            UiFactory.Stretch(refImage.rectTransform);
            refImage.raycastTarget = true;
            var pick = refImage.gameObject.AddComponent<EventTrigger>();
            var click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            click.callback.AddListener(d => PickReference((PointerEventData)d));
            pick.triggers.Add(click);
            refWindow.gameObject.SetActive(false);
        }

        // ---------------- UI ----------------

        void BuildUi(RectTransform root)
        {
            pageWidth = 760f;
            RowHeight = 40f;
            RowStep = 46f;
            var header = UiFactory.MakePanel(root, "Header", HeaderColor);
            UiFactory.Place(header.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(WindowWidth, 80f));
            title = UiFactory.MakeLabel(header.transform, "SPRITEMAKER", 30, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            UiFactory.Place(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(1300f, 70f));
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 16;
            title.resizeTextMaxSize = 30;
            var close = UiFactory.MakeButton(header.transform, "X", ButtonColor, Close, 24);
            UiFactory.IconOnly(close, "Close", 24f);
            UiFactory.Place((RectTransform)close.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(64f, 56f));
            var png = UiFactory.MakeButton(header.transform, "Save PNG", ButtonColor, () => { if (Editable) SavePng(); }, 24);
            UiFactory.Place((RectTransform)png.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-90f, 0f), new Vector2(160f, 56f));
            var useB = UiFactory.MakeButton(header.transform, "Use", SaveColor, Use, 24);
            UiFactory.Place((RectTransform)useB.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-260f, 0f), new Vector2(136f, 56f));
            useButton = useB.gameObject;
            var b3 = UiFactory.MakeButton(header.transform, "3D", ButtonColor, () => { if (Editable) Show3D(!show3D); }, 24);
            UiFactory.Place((RectTransform)b3.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-412f, 0f), new Vector2(120f, 56f));
            button3D = b3.GetComponent<Image>();
            var refB = UiFactory.MakeButton(header.transform, "Reference", ButtonColor, ToggleReference, 22);
            UiFactory.Place((RectTransform)refB.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-544f, 0f), new Vector2(150f, 56f));

            var page = UiFactory.MakeRect(root, "Settings");
            UiFactory.Place(page, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(PageLeft, PageTop), new Vector2(pageWidth, WindowHeight + PageTop - 70f));
            BuildSettings(page);

            // The atlas, on a checker so see-through parts show.
            float previewX = PageLeft + pageWidth + 20f;
            var frame = UiFactory.MakePanel(root, "Preview Frame", new Color(0f, 0f, 0f, 0.4f));
            UiFactory.Place(frame.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(previewX, PageTop), new Vector2(PreviewSize, PreviewSize));
            var area = UiFactory.MakeRect(frame.transform, "Atlas");
            UiFactory.Stretch(area);
            atlasArea = area;
            previewFit = area.gameObject.AddComponent<AspectRatioFitter>();
            previewFit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            checker = UiFactory.MakeRect(area, "Checker").gameObject.AddComponent<RawImage>();
            UiFactory.Stretch(checker.rectTransform);
            checker.texture = Checker();
            preview = UiFactory.MakeRect(area, "Preview").gameObject.AddComponent<RawImage>();
            UiFactory.Stretch(preview.rectTransform);
            preview.raycastTarget = true;
            var trigger = preview.gameObject.AddComponent<EventTrigger>();
            var click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            click.callback.AddListener(d => PickAt((PointerEventData)d));
            trigger.triggers.Add(click);
            selectionFrame = UiFactory.MakeRect(area, "Selection");
            var red = new Color(1f, 0.16f, 0.24f);
            for (int i = 0; i < 4; i++)
            {
                var edge = UiFactory.MakePanel(selectionFrame, "Edge", red).rectTransform;
                bool vertical = i < 2;
                edge.anchorMin = vertical ? new Vector2(i, 0f) : new Vector2(0f, i - 2);
                edge.anchorMax = vertical ? new Vector2(i, 1f) : new Vector2(1f, i - 2);
                edge.pivot = new Vector2(0.5f, 0.5f);
                edge.sizeDelta = vertical ? new Vector2(3f, 0f) : new Vector2(0f, 3f);
                edge.anchoredPosition = Vector2.zero;
            }
            selectionFrame.gameObject.SetActive(false);

            // 3D: LevelTexturePreview.json drawn by the editor's 3D view with this atlas; drag or wheel to move along it.
            var sceneHolder = UiFactory.MakeRect(frame.transform, "3D");
            UiFactory.Stretch(sceneHolder);
            sceneFit = sceneHolder.gameObject.AddComponent<AspectRatioFitter>();
            sceneFit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            scene = UiFactory.MakeRect(sceneHolder, "Scene").gameObject.AddComponent<RawImage>();
            UiFactory.Stretch(scene.rectTransform);
            scene.raycastTarget = true;
            var sceneTrigger = scene.gameObject.AddComponent<EventTrigger>();
            var drag = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            drag.callback.AddListener(d => MoveScene(((PointerEventData)d).delta.y / 30f));
            sceneTrigger.triggers.Add(drag);
            var wheel = new EventTrigger.Entry { eventID = EventTriggerType.Scroll };
            wheel.callback.AddListener(d => MoveScene(((PointerEventData)d).scrollDelta.y));
            sceneTrigger.triggers.Add(wheel);
            sceneHolder.gameObject.SetActive(false);

            var hint = UiFactory.MakeLabel(root, "Click the atlas to pick the layer there. The list draws from top to bottom: lower layers cover " +
                "upper ones. Colour atlas = the texture; Glow map = black with only the In glow map layers, for the Custom Theme's Glow " +
                "textures (black = no glow). Save PNG writes the picture you see; Open... / Save... read and write Texture Lab projects too.",
                16, TextAnchor.UpperLeft, SoftText);
            UiFactory.Place(hint.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(previewX, PageTop - PreviewSize - 14f), new Vector2(PreviewSize, 180f));

            // The layer list, with search.
            float listX = WindowWidth - 24f - ListWidth;
            searchField = UiFactory.MakeInputField(root, 20, FieldColor, InputField.ContentType.Standard);
            UiFactory.Place((RectTransform)searchField.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(listX, PageTop), new Vector2(ListWidth, 44f));
            UiFactory.SetPlaceholder(searchField, "Search layers...");
            searchField.onValueChanged.AddListener(t =>
            {
                search = t.Trim();
                if (lab != null) UpdateList();
            });
            string[] names = { "All on", "All off", "Colour...", "Glow" };
            UnityEngine.Events.UnityAction[] actions =
            {
                () => { if (Editable) SetFound(true); }, () => { if (Editable) SetFound(false); },
                () => { if (Editable) ColourFound(); }, () => { if (Editable) GlowFound(); }
            };
            float bw = (ListWidth - 6f * (names.Length - 1)) / names.Length;
            for (int i = 0; i < names.Length; i++)
            {
                var b = UiFactory.MakeButton(root, names[i], ButtonColor, actions[i], 18);
                UiFactory.Place((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(listX + i * (bw + 6f), PageTop - 52f), new Vector2(bw, 42f));
            }
            scroll = UiFactory.MakeGridList(root, new Vector2(ListWidth - 36f, ItemHeight), ItemGap, 1, AccentColor, out listContent);
            var listRect = (RectTransform)scroll.transform;
            UiFactory.Place(listRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(listX, PageTop - 102f), new Vector2(ListWidth, WindowHeight + PageTop - 102f - 70f));

            MakeStatus(root, WindowWidth);
            BuildChooser(root, new Vector2(PageLeft, -90f), new Vector2(pageWidth, WindowHeight - 160f));
            BuildColorPicker(root);
        }

        void BuildSettings(Transform page)
        {
            float y = 0f;
            CycleRow(page, "Atlas", ref y, () => $"{Def.label} ({Def.w}x{Def.h})", step =>
            {
                int n = SpriteMakerLab.Groups.Count;
                groupIndex = (groupIndex + step + n) % n;
                selected = -1;
                listDirty = true;
            });
            ToggleRow(page, "Show", ref y, () => glowView ? "Glow map" : "Colour atlas", () => glowView, () => glowView = !glowView);
            ToggleRow(page, "Base colour", ref y, () => S.hasBase ? "On" : "Off (see-through)", () => S.hasBase, () => S.hasBase = !S.hasBase);
            ColorRow(page, "Base", ref y, () => S.baseColor, c =>
            {
                c.a = 1f;
                S.baseColor = c;
                S.hasBase = true;
            });
            ButtonsRow(page, "Project", ref y, () => "Massif/Default, .json",
                ("Massif", () =>
                {
                    lab.Reset(Def);
                    selected = -1;
                    listDirty = true;
                    Changed();
                    Status($"{Def.label} is back to Massif/Default.");
                }),
                ("Open...", OpenProject),
                ("Save...", SaveProject));

            InfoRow(page, "Layer", ref y, () => Sel != null ? $"{Sel.name}  ({SpriteMakerLab.Role(Sel.name)})"
                                                            : Def.frames ? "frames: pick one with Frame or in the list" : "none: click the atlas or a row in the list");
            ToggleRow(page, "Visible", ref y, () => Sel == null ? "-" : L.on ? "Shown" : "Hidden", () => L.on, () => { if (Sel != null) L.on = !L.on; });
            ColorRow(page, "Colour", ref y, () => SpriteMakerLab.Hex(L.c), c => { if (Sel != null) L.c = SpriteMakerLab.ToHex(c); });
            SliderRow(page, "Opacity", ref y, 0f, 1f, () => L.o, v => { if (Sel != null) L.o = v; }, "0.00", true);
            CycleRow(page, "Blend", ref y, () =>
            {
                var b = SpriteMakerLab.ParseBlend(L.m);
                return b == SpriteBlend.Glow ? "Glow (adds light)" : b.ToString();
            }, step =>
            {
                if (Sel == null) return;
                int b = ((int)SpriteMakerLab.ParseBlend(L.m) + step + 4) % 4;
                L.m = SpriteMakerLab.BlendName((SpriteBlend)b);
            });
            string[] glowModes = { "", "on", "off" };
            CycleRow(page, "In glow map", ref y, () =>
            {
                if (Sel == null) return "-";
                if (L.gm == "on") return "Yes";
                if (L.gm == "off") return "No";
                return Sel.InGlowMap ? "Auto (yes)" : "Auto (no)";
            }, step =>
            {
                if (Sel == null) return;
                int i = Mathf.Max(0, Array.IndexOf(glowModes, L.gm ?? ""));
                L.gm = glowModes[(i + step + glowModes.Length) % glowModes.Length];
            });
            ButtonsRow(page, "Order", ref y, () => Sel != null ? $"{selected + 1} of {S.layers.Count}" : "-",
                ("Up", () => Move(-1)),
                ("Down", () => Move(1)));
            float top = y;

            var enemy = Group(page, "Enemy", top, () => lab != null && Def.key == "Enemy");
            float ey = 0f;
            ToggleRow(enemy, "Top right", ref ey, () => lab.topRight == "Palette" ? "Palette (cubes, flip tiles...)" : "Star",
                () => lab.topRight == "Palette", () =>
                {
                    lab.SetTopRight(lab.topRight == "Palette" ? "Star" : "Palette");
                    selected = -1;
                    listDirty = true;
                });
            var palette = Group(page, "Palette Order", top - RowStep, () => lab != null && lab.PaletteMode(Def));
            float py = 0f;
            TextRow(palette, "Palette Order", ref py, () => lab.paletteJson, t =>
            {
                var problem = lab.ApplyPaletteJson(t);
                Status(problem ?? $"{lab.palette.elements.Length} palette(s) placed.");
            }, 20000);
            ButtonsRow(palette, "Palettes", ref py, () => $"{lab.palette.elements.Length} placed",
                ("Example", () =>
                {
                    lab.ApplyPaletteJson(SpriteMakerLab.ExamplePaletteJson());
                    Changed();
                }),
                ("Import...", ImportPalette),
                ("Clear", () =>
                {
                    lab.ApplyPaletteJson("{\"elements\": []}");
                    Changed();
                }));
            ToggleRow(palette, "On top", ref py, () => lab.palette.alwaysOnTop ? "Over every layer" : "Right after tr/basic_palette",
                () => lab.palette.alwaysOnTop, () =>
                {
                    lab.palette.alwaysOnTop = !lab.palette.alwaysOnTop;
                    lab.paletteJson = JsonUtility.ToJson(lab.palette);
                });
            ButtonsRow(palette, "Glow palettes", ref py, () =>
                    (lab.glowCells == "Off" ? "None" : lab.glowCells == "6/16" ? "6 of 16 px" : lab.glowCells) +
                    (lab.glowPalette == null ? ", same colours" : $", own ({lab.glowPalette.elements.Length})"),
                ("Size", () =>
                {
                    var modes = SpriteMakerLab.GlowCellModes;
                    lab.glowCells = modes[(Array.IndexOf(modes, lab.glowCells) + 1) % modes.Length];
                    Changed();
                    Status(lab.glowCells == "Off" ? "Glow map: no palettes." :
                        $"Glow map: palette cells keep {(lab.glowCells == "6/16" ? "6 of every 16 pixels (16 px cell = 6)" : lab.glowCells == "6 px" ? "at most 6 pixels" : "all their pixels")} in the middle, the rest is see-through.");
                }),
                ("Import...", ImportGlowPalette),
                ("Same", () =>
                {
                    lab.ApplyGlowPaletteJson("");
                    Changed();
                    Status("Glow map: the palettes use the Palette Order colours.");
                }));

            var frames = Group(page, "Frames", top, () => lab != null && Def.frames);
            float fy = 0f;
            CycleRow(frames, "Frame", ref fy, () => $"{S.frame + 1} / {S.layers.Count}", step =>
            {
                int n = Mathf.Max(1, S.layers.Count);
                S.frame = (S.frame + step + n) % n;
            });
        }

        Texture2D Checker()
        {
            if (checkerTex != null) return checkerTex;
            checkerTex = new Texture2D(2, 2) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, name = "SpriteMaker Checker" };
            var a = new Color(0.17f, 0.16f, 0.25f);
            var b = new Color(0.23f, 0.22f, 0.35f);
            checkerTex.SetPixels(new[] { a, b, b, a });
            checkerTex.Apply();
            return checkerTex;
        }

        void OnDestroy()
        {
            if (previewTex != null) Destroy(previewTex);
            if (checkerTex != null) Destroy(checkerTex);
        }
    }
}
