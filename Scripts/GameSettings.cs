using UnityEngine;

namespace RTXOn
{
    /// <summary>Which controller input steers (the D-pad always steers too).</summary>
    public enum SteerSource { LeftStick, Triggers, Shoulders }

    /// <summary>The chase camera when the level has no camera trigger yet (Settings > Camera).</summary>
    public enum CameraView { Normal, Classic, High, Far }

    /// <summary>Controller buttons that can be mapped in Settings > Controller.</summary>
    public enum PadAction { LaneLeft, LaneRight, Accelerator, SteerSpeed, Pause, Back, Hitboxes }

    /// <summary>
    /// The player's own settings from the Main Menu's Settings page and the Pause menu, kept between runs
    /// (PlayerPrefs): volumes, camera, mouse and controller steering, accelerator and the controller mapping.
    /// </summary>
    public static class GameSettings
    {
        public const float MinSensitivity = 0.25f, MaxSensitivity = 3f;
        /// <summary>Mouse sensitivity as shown in Settings: 400 = BallController.dragSensitivity as set (x1).</summary>
        public const int MouseScale = 400;
        public const float MinAccelerator = 1f, MaxAccelerator = 3f, DefaultAccelerator = 2f;

        const string Prefix = "RTXOn.";
        const string DragKey = Prefix + "DragSensitivity", StickKey = Prefix + "StickSensitivity";
        const string SfxKey = Prefix + "SfxVolume", MusicKey = Prefix + "MusicVolume", CameraKey = Prefix + "Camera";
        const string AutoMoveKey = Prefix + "AutoMove", SourceKey = Prefix + "SteerSource", SmoothKey = Prefix + "SmoothSteer";
        const string AcceleratorKey = Prefix + "Accelerator", PadKey = Prefix + "Pad.", MenuMusicKey = Prefix + "MenuMusic";
        /// <summary>Menu Music choices: 0 Massif .. 4 Volcano (Music1..5), 5 = off.</summary>
        public const int MenuMusicOff = 5;

        /// <summary>Controller speeds that Y / Triangle steps through: Slow, Normal, Fast.</summary>
        public static readonly float[] StickPresets = { 0.6f, 1f, 1.6f };
        static readonly string[] PresetNames = { "SLOW", "NORMAL", "FAST" };

        static bool loaded;
        static float drag = 1f, stick = 1f, sfx = 1f, music = 1f, accelerator = DefaultAccelerator;
        static CameraView camera;
        static SteerSource source;
        static bool autoMove, smooth = true;
        static int menuMusic;
        static readonly string[] pad = new string[System.Enum.GetValues(typeof(PadAction)).Length];

        /// <summary>Raised after any setting changes (GameAudio follows the volumes at once).</summary>
        public static event System.Action Changed;

        /// <summary>Multiplier on mouse / touch drag steering (1 = BallController.dragSensitivity as set).</summary>
        public static float DragSensitivity
        {
            get { Load(); return drag; }
            set { Load(); drag = Mathf.Clamp(value, MinSensitivity, MaxSensitivity); Save(); }
        }

        /// <summary>The mouse sensitivity as a number, 100..1200 (default 400 = x1).</summary>
        public static int MouseSensitivity
        {
            get => Mathf.RoundToInt(DragSensitivity * MouseScale);
            set => DragSensitivity = value / (float)MouseScale;
        }

        /// <summary>Multiplier on controller steering (1 = BallController.stickSteerSpeed as set).</summary>
        public static float StickSensitivity
        {
            get { Load(); return stick; }
            set { Load(); stick = Mathf.Clamp(value, MinSensitivity, MaxSensitivity); Save(); }
        }

        /// <summary>Sound effects volume 0..1, on top of GameAudio's own Sfx Volume.</summary>
        public static float SfxVolume
        {
            get { Load(); return sfx; }
            set { Load(); sfx = Mathf.Clamp01(value); Save(); }
        }

        /// <summary>Music volume 0..1, on top of GameAudio's own Music Volume.</summary>
        public static float MusicVolume
        {
            get { Load(); return music; }
            set { Load(); music = Mathf.Clamp01(value); Save(); }
        }

        public static CameraView Camera
        {
            get { Load(); return camera; }
            set { Load(); camera = value; Save(); }
        }

        /// <summary>The mouse steers without holding the button (the cursor is hidden while playing). Default off.</summary>
        public static bool AutoMove
        {
            get { Load(); return autoMove; }
            set { Load(); autoMove = value; Save(); }
        }

        /// <summary>Which controller input steers: Left Stick (default), LT / RT, or LB / RB. Only that one.</summary>
        public static SteerSource SteerSource
        {
            get { Load(); return source; }
            set { Load(); source = value; Save(); }
        }

        /// <summary>On: the ball glides to where it is steered (default). Off: it is there at once.</summary>
        public static bool SmoothSteer
        {
            get { Load(); return smooth; }
            set { Load(); smooth = value; Save(); }
        }

        /// <summary>Steering speed while the accelerator (A / Cross, or Space) is held: 1.0..3.0.</summary>
        public static float Accelerator
        {
            get { Load(); return accelerator; }
            set { Load(); accelerator = Mathf.Clamp(value, MinAccelerator, MaxAccelerator); Save(); }
        }

        /// <summary>Song of the Main Menu: 0 Massif (default), 1 Sky, 2 Forest, 3 Desert, 4 Volcano, 5 off.</summary>
        public static int MenuMusic
        {
            get { Load(); return menuMusic; }
            set { Load(); menuMusic = Mathf.Clamp(value, 0, MenuMusicOff); Save(); }
        }

        /// <summary>The control mapped to <paramref name="action"/> (see <see cref="GameInput.DefaultBinding"/>).</summary>
        public static string Binding(PadAction action)
        {
            Load();
            var b = pad[(int)action];
            return string.IsNullOrEmpty(b) ? GameInput.DefaultBinding(action) : b;
        }

        /// <summary>
        /// Maps <paramref name="action"/> to <paramref name="control"/>. An action that had that control gets this
        /// action's old one, so no button does two things.
        /// </summary>
        public static void SetBinding(PadAction action, string control)
        {
            Load();
            string old = Binding(action);
            foreach (PadAction other in System.Enum.GetValues(typeof(PadAction)))
                if (other != action && Binding(other) == control) pad[(int)other] = old;
            pad[(int)action] = control;
            Save();
        }

        public static void ResetBindings()
        {
            Load();
            for (int i = 0; i < pad.Length; i++) pad[i] = null;
            Save();
        }

        /// <summary>Every setting back to its default (the controller mapping too).</summary>
        public static void ResetAll()
        {
            Load();
            drag = stick = sfx = music = 1f;
            accelerator = DefaultAccelerator;
            camera = CameraView.Normal;
            source = SteerSource.LeftStick;
            autoMove = false;
            smooth = true;
            menuMusic = 0;
            for (int i = 0; i < pad.Length; i++) pad[i] = null;
            GraphicsQuality.Level = GraphicsQuality.Default;
            Save();
        }

        /// <summary>The preset nearest to the controller sensitivity: 0 Slow, 1 Normal, 2 Fast.</summary>
        public static int StickPreset
        {
            get
            {
                float s = StickSensitivity;
                int best = 0;
                for (int i = 1; i < StickPresets.Length; i++)
                    if (Mathf.Abs(StickPresets[i] - s) < Mathf.Abs(StickPresets[best] - s)) best = i;
                return best;
            }
        }

        public static string StickPresetName => PresetNames[StickPreset];

        /// <summary>Y / Triangle: Slow -> Normal -> Fast -> Slow. Returns the new name.</summary>
        public static string NextStickPreset()
        {
            StickSensitivity = StickPresets[(StickPreset + 1) % StickPresets.Length];
            return StickPresetName;
        }

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            drag = Mathf.Clamp(PlayerPrefs.GetFloat(DragKey, 1f), MinSensitivity, MaxSensitivity);
            stick = Mathf.Clamp(PlayerPrefs.GetFloat(StickKey, 1f), MinSensitivity, MaxSensitivity);
            sfx = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxKey, 1f));
            music = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, 1f));
            accelerator = Mathf.Clamp(PlayerPrefs.GetFloat(AcceleratorKey, DefaultAccelerator), MinAccelerator, MaxAccelerator);
            camera = (CameraView)Mathf.Clamp(PlayerPrefs.GetInt(CameraKey, 0), 0, (int)CameraView.Far);
            source = (SteerSource)Mathf.Clamp(PlayerPrefs.GetInt(SourceKey, 0), 0, (int)SteerSource.Shoulders);
            autoMove = PlayerPrefs.GetInt(AutoMoveKey, 0) == 1;
            smooth = PlayerPrefs.GetInt(SmoothKey, 1) == 1;
            menuMusic = Mathf.Clamp(PlayerPrefs.GetInt(MenuMusicKey, 0), 0, MenuMusicOff);
            for (int i = 0; i < pad.Length; i++) pad[i] = PlayerPrefs.GetString(PadKey + (PadAction)i, "");
        }

        static void Save()
        {
            PlayerPrefs.SetFloat(DragKey, drag);
            PlayerPrefs.SetFloat(StickKey, stick);
            PlayerPrefs.SetFloat(SfxKey, sfx);
            PlayerPrefs.SetFloat(MusicKey, music);
            PlayerPrefs.SetFloat(AcceleratorKey, accelerator);
            PlayerPrefs.SetInt(CameraKey, (int)camera);
            PlayerPrefs.SetInt(SourceKey, (int)source);
            PlayerPrefs.SetInt(AutoMoveKey, autoMove ? 1 : 0);
            PlayerPrefs.SetInt(SmoothKey, smooth ? 1 : 0);
            PlayerPrefs.SetInt(MenuMusicKey, menuMusic);
            for (int i = 0; i < pad.Length; i++) PlayerPrefs.SetString(PadKey + (PadAction)i, pad[i] ?? "");
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            loaded = false; // Enter Play Mode without domain reload: read the prefs again
            Changed = null;
        }
    }
}
