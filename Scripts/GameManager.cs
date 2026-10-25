using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RTXOn
{
    public enum GameState { Ready, Playing, Dead, Won }

    /// <summary>
    /// Loads the level (Level.txt layer, level.json, or the level editor's play test), runs Ready → Playing → Dead/Won,
    /// and drives checkpoints, crowns, gems, music and the HUD.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Level")]
        [Tooltip("Level.txt (Tiled Flare, pick the layer below) or a level.json.")]
        public TextAsset levelFile;
        [Tooltip("Only used for Level.txt.")]
        public string layerName = "Level 1";
        public string levelTitle = "MASSIF";
        [Tooltip("Rows per second when the level sets no \"speed\". 5.98 = World 1 speed in the original game, so Music1 lines up. Old files with \"bpm\": speed = BPM / 15, at most 20.")]
        public float speed = 5.98f;
        [Tooltip("Environment changes for Level.txt. A level.json uses its own \"themes\" list instead.")]
        public List<ThemeChange> themeChanges = new List<ThemeChange>();

        [Header("Checkpoints")]
        [Tooltip("Passing a checkpoint saves the run there: after dying, tap continues from the last one (R restarts from the start).")]
        public bool useCheckpoints = true;
        [Tooltip("Checkpoint rows for Level.txt. A level.json uses its own \"checkpoints\" list instead. " +
                 "Crowns are separate: tile 150 in the level.")]
        public List<int> checkpoints = new List<int>();

        [Header("Testing")]
        [Tooltip("Shows the deadly boxes (red, orange = safe while jumping) and the ball's hit point in the Game view. F1 toggles it while playing.")]
        public bool showHitboxes;

        [Header("Themes")]
        [Tooltip("Every TrackTheme a level.json can pick with \"look\" (Build Gameplay Scene fills this). No \"look\" = the Track's own theme.")]
        public List<TrackTheme> looks = new List<TrackTheme>();
        TrackTheme defaultLook; // the scene's theme, before a level's look or Theme Settings replaced it

        [Header("Scene")]
        public BallController ball;
        public TrackBuilder track;
        public CameraRig cameraRig;
        public Hud hud;
        public GameAudio audioPlayer;
        [Tooltip("The ball follows the song's clock, so it stays on the beat from any start row (Start Pos, checkpoints).")]
        public bool followMusic = true;
        public EnvironmentDirector environment;

        public GameState State { get; private set; } = GameState.Ready;
        /// <summary>Gems and stars picked up (one each).</summary>
        public int Gems { get; private set; }
        public int GemTotal { get; private set; }
        /// <summary>Crowns picked up (tile 150).</summary>
        public int Crowns { get; private set; }
        public int CrownTotal { get; private set; }
        public LevelData Level { get; private set; }
        public bool IsPlayTest { get; private set; }
        /// <summary>Opened from the Main Menu's level list (Esc and the end of the level go back to it).</summary>
        public bool FromMenu { get; private set; }
        /// <summary>The level's Invincible setting: hazards don't kill (falling still does).</summary>
        public bool Invincible => Level != null && Level.Invincible;

        float endTime;

        // Speed Up / Speed Down the ball took: rows [from, to) at Multiple times the speed.
        bool speedZoneOn;
        LevelData.SpeedZone speedZone;
        // Rows the ball is ahead of the music's timing (a speed change that does not count for the music, or one missed).
        float musicRowOffset;
        bool musicInSync = true;

        // Survive the scene reload that continues from a crown.
        static string resumeLevel;
        static int resumeRow;
        static int resumeGems;
        static int resumeCrowns;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => resumeLevel = null;

        List<int> checkpointRows = new List<int>();
        int reachedCheckpoint;   // row of the last checkpoint passed (0 = none)
        int gemsAtCheckpoint;
        int crownsAtCheckpoint;
        int startRow;

        [Header("Start Portal (Level Settings)")]
        [Tooltip("Rows the ball flies when it jumps out of the start portal (3 by default; the portal is always in the middle lane).")]
        public float startPortalRows = 3f;
        [Tooltip("Height of that jump.")]
        public float startPortalJumpHeight = 1.6f;
        bool jumpOutOfPortal;
        PortalRig startPortal;

        string ResumeKey => IsPlayTest ? "playtest" : FromMenu ? "menu/" + LevelSelect.Key : (levelFile != null ? levelFile.name : "") + "/" + layerName;

        void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 60;
            ScreenRotation.PortraitOnly(); // a phone plays upright, like the original
            ThemeRegistry.AddTo(looks); // every official theme, also in a build (not only the scene's Looks)
            TrackBuilder.BuildProblem += ShowBuildProblem;

            IsPlayTest = PlayTest.Active && !string.IsNullOrEmpty(PlayTest.Json);
            FromMenu = !IsPlayTest && LevelSelect.Active && !string.IsNullOrEmpty(LevelSelect.Json);
            bool isJson;
            if (IsPlayTest)
            {
                Level = LevelData.ParseJson(PlayTest.Json);
                isJson = true;
            }
            else if (FromMenu)
            {
                Level = LevelData.ParseJson(LevelSelect.Json); // the level chosen in the Main Menu
                isJson = true;
            }
            else
            {
                if (levelFile == null)
                {
                    Debug.LogError("RTXOn: assign Game > Level File (Level.txt or a level.json).");
                    enabled = false;
                    return;
                }
                Level = LevelData.Load(levelFile.text, layerName);
                isJson = levelFile.text.TrimStart().StartsWith("{");
            }

            checkpointRows = new List<int>(isJson ? Level.Checkpoints : checkpoints);
            checkpointRows.RemoveAll(r => r <= 0 || r >= Level.RowCount - 1);
            checkpointRows.Sort();
            GemTotal = Level.CountGems();
            CrownTotal = Level.CountCrowns();

            // Continue from a checkpoint after a death in this same level, or start the play test where the editor was.
            bool resuming = useCheckpoints && resumeLevel == ResumeKey;
            if (resuming)
            {
                startRow = resumeRow;
                Gems = resumeGems;
                Crowns = resumeCrowns;
                reachedCheckpoint = startRow;
                gemsAtCheckpoint = Gems;
                crownsAtCheckpoint = Crowns;
            }
            else
            {
                startRow = IsPlayTest ? Mathf.Clamp(PlayTest.StartRow, 0, Mathf.Max(0, Level.RowCount - 2)) : 0;
            }
            resumeLevel = null;

            // Editor Test from a Start Pos: that lane, even with no floor (the designer chose it). Retries from a checkpoint don't use it.
            float startX = !resuming && IsPlayTest && PlayTest.StartLane >= 0 && PlayTest.StartLane < LevelData.Width
                ? PlayTest.StartLane - 2f
                : Level.SafeLaneX(startRow);
            // Start Portal: the portal and the ball always start in the middle lane.
            if ((isJson || IsPlayTest) && Level.StartPortal && !resuming && startRow == 0 && !(IsPlayTest && PlayTest.StartLane >= 0)) startX = 0f;
            ball.PlaceAt(new Vector3(startX, ball.radius, startRow));
            if (cameraRig != null)
            {
                cameraRig.SetChanges(isJson || IsPlayTest ? Level.CameraChanges : null); // camera triggers
                cameraRig.Snap();
            }
            // Screen filter triggers (Glitch, Grayscale...), worked out from the ball's row like the camera.
            var filters = GetComponent<ScreenFilterDirector>();
            if (filters == null) filters = gameObject.AddComponent<ScreenFilterDirector>();
            var view = cameraRig != null ? cameraRig.GetComponentInChildren<Camera>() : null;
            filters.Use(ball, view != null ? view : Camera.main, isJson || IsPlayTest ? Level.FilterChanges : null);
            track.checkpointRows = checkpointRows.FindAll(r => r > startRow);

            // Custom Themes the level carries (.themert inside level.json) and its Theme Settings (Level Editor > Theme).
            ThemeLibrary.LoadBundled(); // the themes shipped with the game (Levels/Themes)
            ThemeLibrary.AddFromLevel(Level);
            if (defaultLook == null) defaultLook = track.theme;
            var look = FindLook(Level.Look);
            track.theme = look != null ? look : ThemeLibrary.WithSettings(defaultLook, Level.ThemeSettings, looks);
            var changes = isJson ? (IList<ThemeChange>)Level.ThemeChanges : themeChanges;
            // Theme triggers that switch the look and / or the objects, with the full look name (Look|Objects).
            track.UseLooks(isJson ? Level.LookChanges() : changes, FindLook);
            // Dynamic Loading from level.json: rows built ahead of the ball, or the whole level when it is off.
            if (isJson || IsPlayTest)
            {
                if (Level.LoadDistance > 0) track.rowsAhead = Level.LoadDistance;
                if (!Level.DynamicLoading)
                {
                    track.rowsAhead = Level.RowCount;
                    track.rowsBehind = Level.RowCount;
                }
            }
            // Android: Dynamic Loading is always on, at most 50 rows ahead (Settings > Graphics).
            if (GraphicsQuality.Mobile)
            {
                track.rowsAhead = GraphicsQuality.RowsAhead(track.rowsAhead);
                track.rowsBehind = Mathf.Min(track.rowsBehind, 10);
            }
            track.Init(Level, ball.transform);
            // Level Settings > Start Portal: only when the level starts from the beginning, not from a Start Pos or a checkpoint.
            if (startPortal != null) Destroy(startPortal.transform.parent.gameObject);
            jumpOutOfPortal = (isJson || IsPlayTest) && Level.StartPortal && !resuming && startRow == 0 && !(IsPlayTest && PlayTest.StartLane >= 0);
            startPortal = jumpOutOfPortal ? track.AddStartPortal(ball.Position) : null;
            if (hud == null) hud = gameObject.AddComponent<Hud>();
            if (audioPlayer == null) audioPlayer = gameObject.AddComponent<GameAudio>();
            audioPlayer.PrepareMusic(Level.Music, LevelNumber()); // loaded before the tap, so it starts exactly at the start row
            if (environment == null) environment = gameObject.AddComponent<EnvironmentDirector>();
            environment.Init(track.theme, changes);
            foreach (var m in track.LookMaterials) environment.RegisterNeon(m);

            string title = !string.IsNullOrEmpty(Level.Name) && (isJson || IsPlayTest) ? Level.Name.ToUpperInvariant() : levelTitle;
            hud.ShowReady(title, IsPlayTest);
            // Pause screen: Levels = the level list, Close = Home (the editor when play testing). Both also work when the
            // Gameplay scene was started on its own (Adrian), through the MainMenu scene; Close quits without one.
            hud.SetupPause(() => SetPaused(true), Resume, RestartFromPause,
                !IsPlayTest && MenuSceneToOpen() != null ? BackFromPause : (System.Action)null, HomeFromPause);
            // PC Complete screen: Levels / Restart / Back (play test: back to the editor instead of the menu).
            hud.SetupComplete(!IsPlayTest && MenuSceneToOpen() != null ? LevelsFromComplete : (System.Action)null,
                RestartFromComplete, IsPlayTest || MenuSceneToOpen() != null ? HomeFromComplete : (System.Action)null);
            hud.SetGems(Gems, GemTotal);
            hud.SetCrowns(Crowns, CrownTotal);
            hud.SetProgress(Progress());
            if (resuming) hud.ShowCheckpointReady(IsPlayTest);

            // The track ahead rises into place when continuing or play testing from the middle.
            if (startRow > 0) track.RiseRows(startRow + 3);
        }

        /// <summary>
        /// Keeps the ball on the song: the row the music is at (its sample clock) is where the ball should be, so a
        /// start from a Start Pos or checkpoint, a slow frame or audio latency never leaves the ball early or late.
        /// Small differences are pulled in smoothly; none while the song is still loading or after it ended.
        /// </summary>
        void FollowMusic()
        {
            if (!followMusic || audioPlayer == null || !audioPlayer.MusicReady) return;
            float z = ball.Position.z;
            // While the ball's speed differs from what the music timing expects (a speed change without Incorporate
            // music offset, or one the ball missed), it is not pulled back; after it, it follows with the new offset.
            bool inSync = SameSpeedChange(z);
            if (!inSync)
            {
                musicInSync = false;
                return;
            }
            if (!musicInSync)
            {
                musicInSync = true;
                musicRowOffset = z - Level.RowAtTime(audioPlayer.MusicTime, speed);
            }
            float musicRow = Level.RowAtTime(audioPlayer.MusicTime, speed) + musicRowOffset;
            float drift = musicRow - z;
            if (Mathf.Abs(drift) > 3f || Mathf.Abs(drift) < 0.002f) return;
            ball.Nudge(drift * Mathf.Clamp01(Time.deltaTime * 10f));
        }

        /// <summary>True when the ball's speed change at <paramref name="z"/> is the one the music timing counts there.</summary>
        bool SameSpeedChange(float z)
        {
            bool timeline = Level.MusicZoneAt(z, out var counted);
            bool mine = speedZoneOn && z < speedZone.to;
            if (!timeline && !mine) return true;
            return timeline && mine && counted.row == speedZone.row && Mathf.Approximately(counted.multiple, speedZone.multiple);
        }

        /// <summary>A Speed Up / Speed Down power-up was taken: from its row, Distance rows at Speed Multiple times the speed.</summary>
        public void StartSpeedZone(int row, PartArgs args)
        {
            speedZoneOn = true;
            speedZone = new LevelData.SpeedZone
            {
                row = row, lane = args.lane, from = row, to = row + args.distance,
                multiple = args.multiple, musicOffset = args.musicOffset,
            };
        }

        /// <summary>The speed the ball rolls at here: the level's speed times the speed change it took.</summary>
        float BallSpeedAt(float z)
        {
            float s = Level.SpeedAt(z, speed);
            if (speedZoneOn && z >= speedZone.to) speedZoneOn = false;
            return speedZoneOn ? Mathf.Min(LevelData.MaxSpeed * 2f, s * speedZone.multiple) : s;
        }

        float Progress() => Mathf.Clamp01(ball.Position.z / Mathf.Max(1, Level.RowCount - 1));

        /// <summary>Seconds of music that pass before the ball reaches <paramref name="row"/> (follows speed ramps).</summary>
        float TimeAtRow(int row) => Level.TimeAtRow(row, speed);

        /// <summary>
        /// The theme named in level.json "look". "Indonesia" also finds "IndonesiaTheme".
        /// </summary>
        TrackTheme FindLook(string lookName)
        {
            if (string.IsNullOrEmpty(lookName)) return null;
            // An official look or a Custom Theme, with the level's Theme Settings on top.
            var found = ThemeLibrary.ResolveKey(lookName, looks, Level != null ? Level.ThemeSettings : null, defaultLook);
            if (found != null) return found;
            Debug.LogWarning($"RTXOn: level look '{lookName}' is not in Game > Looks. Using {(track.theme != null ? track.theme.name : "no theme")}. Run RTX ON > Build Gameplay Scene to add new themes.");
            return null;
        }

        void ShowBuildProblem(string text)
        {
            if (hud != null) hud.ShowProblem(text);
        }

        void OnDestroy()
        {
            TrackBuilder.BuildProblem -= ShowBuildProblem;
            if (Instance == this) Instance = null;
            if (paused) // left the scene from the Pause menu
            {
                Time.timeScale = 1f;
                AudioListener.pause = false;
            }
        }

        // ---------------- Pause ----------------

        bool paused;
        public bool Paused => paused;

        /// <summary>Pause button, P or Options / Menu: the game stops (time and music) and the Pause menu opens.</summary>
        void SetPaused(bool on)
        {
            if (countdown != null) // paused again during 3-2-1, or Restart / Back: no countdown
            {
                StopCoroutine(countdown);
                countdown = null;
                hud.ShowCountdown("");
                if (on)
                {
                    hud.ShowPause(true);
                    return;
                }
            }
            if (on && (State != GameState.Playing || paused)) return;
            if (!on && !paused) return;
            paused = on;
            Time.timeScale = on ? 0f : 1f;
            AudioListener.pause = on;
            hud.ShowPause(on);
        }

        Coroutine countdown;

        /// <summary>Resume: the menu closes, 3-2-1 over the still game, then it rolls on (time stays stopped until then).</summary>
        void Resume()
        {
            if (!paused || countdown != null) return;
            hud.ShowPause(false);
            countdown = StartCoroutine(CountDown());
        }

        System.Collections.IEnumerator CountDown()
        {
            for (int i = 3; i >= 1; i--)
            {
                hud.ShowCountdown(i.ToString());
                yield return new WaitForSecondsRealtime(0.55f);
            }
            hud.ShowCountdown("");
            countdown = null;
            SetPaused(false);
        }

        void RestartFromPause()
        {
            SetPaused(false);
            resumeLevel = null;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        void BackFromPause()
        {
            SetPaused(false);
            if (IsPlayTest)
            {
                BackToEditor();
                return;
            }
            resumeLevel = null;
            MainMenu.OpenOnLevels();
            OpenMenu();
        }

        /// <summary>Close on the Pause screen: the Main Menu's Home (the editor when play testing).</summary>
        void HomeFromPause()
        {
            SetPaused(false);
            if (IsPlayTest)
            {
                BackToEditor();
                return;
            }
            resumeLevel = null;
            MainMenu.OpenOnHome();
            OpenMenu();
        }

        // ---------------- PC Complete screen ----------------

        void LevelsFromComplete()
        {
            resumeLevel = null;
            MainMenu.OpenOnLevels();
            OpenMenu();
        }

        void RestartFromComplete()
        {
            resumeLevel = null;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        void HomeFromComplete()
        {
            resumeLevel = null;
            if (IsPlayTest)
            {
                BackToEditor();
                return;
            }
            MainMenu.OpenOnHome();
            OpenMenu();
        }

        const string MainMenuScene = "MainMenu";

        /// <summary>The menu to go back to: the one the level came from, else MainMenu when it is in the build list.</summary>
        static string MenuSceneToOpen()
        {
            if (!string.IsNullOrEmpty(LevelSelect.MenuScene)) return LevelSelect.MenuScene;
            return Application.CanStreamedLevelBeLoaded(MainMenuScene) ? MainMenuScene : null;
        }

        /// <summary>Loads the menu; a game without one quits (Play mode stops in the Unity Editor).</summary>
        static void OpenMenu()
        {
            if (LevelSelect.BackToMenu()) return;
            string scene = MenuSceneToOpen();
            if (scene != null)
            {
                LoadingScreen.Load(scene);
                return;
            }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void OnApplicationPause(bool appPaused)
        {
            if (appPaused) SetPaused(true); // phone call, app switched away: come back to the Pause menu
        }

        void Update()
        {
            if (paused)
            {
                // Options / P again, or Circle / B / Esc: back to the game (Back first closes the Pause screen's Settings).
                if (countdown == null)
                {
                    bool back = GameInput.BackPressed(true);
                    if (back && hud.CloseSettings()) return;
                    if (back || GameInput.PausePressed() && !hud.SettingsOpen) Resume();
                }
                else if (GameInput.PausePressed()) SetPaused(true); // back to the menu
                return;
            }
            if (IsPlayTest && GameInput.BackPressed())
            {
                BackToEditor();
                return;
            }
            // Esc / Back while the ball rolls = Pause (Adrian: it went straight to the menu); before the start, after a
            // crash or at the finish it goes back to the level list.
            if (State == GameState.Playing && GameInput.BackPressed())
            {
                SetPaused(true);
                return;
            }
            if (FromMenu && GameInput.BackPressed())
            {
                resumeLevel = null;
                LevelSelect.BackToMenu();
                return;
            }

            // Y / Triangle while playing: controller steering Slow / Normal / Fast (after a crash it restarts instead).
            if ((State == GameState.Ready || State == GameState.Playing) && GameInput.SpeedPressed())
                hud.Flash("SPEED: " + GameSettings.NextStickPreset());

            switch (State)
            {
                case GameState.Ready:
                    if (GameInput.PressedThisFrame())
                    {
                        State = GameState.Playing;
                        ball.Begin(Level.SpeedAt(startRow, speed));
                        if (jumpOutOfPortal)
                        {
                            ball.Launch(startPortalRows, startPortalJumpHeight);
                            if (startPortal != null) startPortal.Deactivate();
                            jumpOutOfPortal = false;
                        }
                        float at = TimeAtRow(startRow);
                        if (!string.IsNullOrEmpty(Level.Music)) audioPlayer.StartMusic(Level.Music, at);
                        else audioPlayer.StartMusic(LevelNumber(), at);
                        hud.ShowPlaying();
                    }
                    break;

                case GameState.Playing:
                    if (GameInput.PausePressed())
                    {
                        SetPaused(true);
                        break;
                    }
                    ball.SetSpeed(BallSpeedAt(ball.Position.z));
                    FollowMusic();
                    hud.SetProgress(Progress());
                    foreach (int row in checkpointRows)
                    {
                        if (row <= reachedCheckpoint || row <= startRow || ball.Position.z < row) continue;
                        PassCheckpoint(row);
                    }
                    if (ball.Position.z >= Level.RowCount - 1 && ball.Position.y > 0f) Win();
                    break;

                case GameState.Dead:
                case GameState.Won:
                    if (Time.time - endTime <= 0.8f) break;
                    if (State == GameState.Won && hud.CompleteHasButtons) break; // PC: its buttons and keys decide
                    bool restart = GameInput.RestartPressed();
                    if (restart || GameInput.PressedThisFrame())
                    {
                        if (IsPlayTest && State == GameState.Won && !restart)
                        {
                            BackToEditor();
                            return;
                        }
                        if (FromMenu && State == GameState.Won && !restart)
                        {
                            LevelSelect.BackToMenu(); // level done: back to the level list
                            return;
                        }
                        if (!restart && State == GameState.Dead && useCheckpoints && reachedCheckpoint > 0)
                        {
                            resumeLevel = ResumeKey;
                            resumeRow = reachedCheckpoint;
                            resumeGems = gemsAtCheckpoint;
                            resumeCrowns = crownsAtCheckpoint;
                        }
                        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                    }
                    break;
            }
        }

        /// <summary>Ball passed a checkpoint row: a chime, the rows ahead hop, and the run is saved here.</summary>
        void PassCheckpoint(int row)
        {
            reachedCheckpoint = row;
            gemsAtCheckpoint = Gems;
            crownsAtCheckpoint = Crowns;
            track.PassCheckpoint(row);
            GameAudio.Play(Sfx.Checkpoint);
            if (useCheckpoints) hud.FlashCheckpoint();
        }

        /// <summary>A crown (tile 150) picked up.</summary>
        public void AddCrown()
        {
            Crowns++;
            hud.SetCrowns(Crowns, CrownTotal);
        }

        /// <summary>One gem or star picked up.</summary>
        public void AddGem()
        {
            Gems++;
            hud.SetGems(Gems, GemTotal);
        }

        public void Die()
        {
            if (State != GameState.Playing) return;
            State = GameState.Dead;
            endTime = Time.time;
            ball.Kill(track.BallMaterial); // in the look's Ball Color
            GameAudio.Play(Sfx.Crash);
            GameInput.Rumble(0.6f, 0.9f, 0.3f);
            audioPlayer.FadeOutMusic(0.6f);
            hud.ShowDead(Progress(), useCheckpoints && reachedCheckpoint > 0, IsPlayTest);
            if (FromMenu) LevelProgress.Record(LevelSelect.Key, Progress(), Gems, Crowns);
        }

        void Win()
        {
            State = GameState.Won;
            endTime = Time.time;
            ball.Stop();
            audioPlayer.FadeOutMusic(2.5f);
            hud.ShowWon(Crowns, CrownTotal, Gems, GemTotal, IsPlayTest);
            if (FromMenu) LevelProgress.Record(LevelSelect.Key, 1f, Gems, Crowns);
        }

        /// <summary>Play test over: open the level editor again with the edits kept.</summary>
        void BackToEditor()
        {
            resumeLevel = null;
            string scene = PlayTest.EditorScene;
            PlayTest.End();
            LoadingScreen.Load(string.IsNullOrEmpty(scene) ? "LevelEditor" : scene);
        }

        /// <summary>"Level 3" → 3, used to pick Music3.</summary>
        int LevelNumber()
        {
            if (FromMenu && LevelSelect.Number > 0) return Mathf.Min(LevelSelect.Number, 5);
            var digits = System.Text.RegularExpressions.Regex.Match(layerName, @"\d+");
            return digits.Success ? int.Parse(digits.Value) : 1;
        }
    }
}
