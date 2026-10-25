using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RTXOn
{
    /// <summary>
    /// Works with both the new Input System (Unity 6 default) and the old Input Manager.
    /// Touch and mouse drag steer (or the mouse alone with Settings > Auto Move), A/D or arrow keys steer, Space held
    /// is the accelerator.
    /// Controllers (PS5 DualSense, PS4, Xbox, any gamepad the Input System knows), Xbox name / PlayStation name:
    /// the D-pad steers, plus one of left stick (default), LT / RT or LB / RB (Settings > Steer With). The buttons
    /// below are the defaults and can be mapped to any button in Settings > Controller:
    /// X / Square and B / Circle move the ball exactly one lane left or right per press; A / Cross held is the
    /// accelerator (faster steering); Y / Triangle switches the steering speed Slow / Normal / Fast while playing and
    /// restarts after a crash; Menu / Options pauses, View / Create goes back, R3 shows the hitboxes.
    /// A / Cross also starts and retries, B / Circle also resumes while paused. The pad rumbles on a crash.
    /// </summary>
    public static class GameInput
    {
        /// <summary>Stick values below this count as zero (worn sticks drift a little).</summary>
        public const float StickDeadZone = 0.15f;
        /// <summary>Trigger values below this count as zero (a resting finger).</summary>
        public const float TriggerDeadZone = 0.08f;

        public static bool PressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            var p = Pointer.current;
            if (p != null && p.press.wasPressedThisFrame) return true;
            var g = Gamepad.current;
            if (g != null && (g.buttonSouth.wasPressedThisFrame || g.startButton.wasPressedThisFrame)) return true;
            var k = Keyboard.current;
            return k != null && (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame);
#else
            return Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) ||
                   Input.GetKeyDown(KeyCode.JoystickButton0) || Input.GetKeyDown(KeyCode.JoystickButton7);
#endif
        }

        /// <summary>
        /// -1..1 from the controller: the D-pad, or the input chosen in Settings > Steer With: the left stick (dead
        /// zone, then a soft curve so small pushes steer finely), RT minus LT (harder = faster) or RB minus LB.
        /// 0 without a controller.
        /// </summary>
        public static float StickAxis()
        {
            var source = GameSettings.SteerSource;
#if ENABLE_INPUT_SYSTEM
            var g = Gamepad.current;
            if (g == null) return 0f;
            float d = (g.dpad.right.isPressed ? 1f : 0f) - (g.dpad.left.isPressed ? 1f : 0f);
            if (d != 0f) return d;
            if (source == SteerSource.Triggers) return Mathf.Clamp(Trigger(g.rightTrigger.ReadValue()) - Trigger(g.leftTrigger.ReadValue()), -1f, 1f);
            if (source == SteerSource.Shoulders) return (g.rightShoulder.isPressed ? 1f : 0f) - (g.leftShoulder.isPressed ? 1f : 0f);
            float x = g.leftStick.ReadValue().x;
#else
            // The old Input Manager has no trigger axes by default: LT / RT steers only with the new Input System.
            if (source == SteerSource.Triggers) return 0f;
            if (source == SteerSource.Shoulders)
                return (Input.GetKey(KeyCode.JoystickButton5) ? 1f : 0f) - (Input.GetKey(KeyCode.JoystickButton4) ? 1f : 0f);
            if (KeyAxis() != 0f) return 0f; // the default "Horizontal" axis has the keys too: count them only once
            float x = Input.GetAxisRaw("Horizontal"); // and the joystick
#endif
            float a = Mathf.Abs(x);
            if (a < StickDeadZone) return 0f;
            a = (a - StickDeadZone) / (1f - StickDeadZone);
            return Mathf.Clamp(Mathf.Sign(x) * a * Mathf.Sqrt(a), -1f, 1f); // a^1.5
        }

        static float Trigger(float v) => v < TriggerDeadZone ? 0f : (v - TriggerDeadZone) / (1f - TriggerDeadZone);

        /// <summary>One lane per press: -1 for Lane Left (X / Square), +1 for Lane Right (B / Circle), 0 otherwise.</summary>
        public static int LaneStep() => (Pressed(PadAction.LaneRight) ? 1 : 0) - (Pressed(PadAction.LaneLeft) ? 1 : 0);

        /// <summary>Steer Speed (Y / Triangle) pressed this frame: next steering speed (Slow, Normal, Fast).</summary>
        public static bool SpeedPressed() => Pressed(PadAction.SteerSpeed);

        /// <summary>True while the accelerator is held: Accelerator on the controller (A / Cross), or Space.</summary>
        public static bool AcceleratorHeld()
        {
            if (Held(PadAction.Accelerator)) return true;
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            return k != null && k.spaceKey.isPressed;
#else
            return Input.GetKey(KeyCode.Space);
#endif
        }

        /// <summary>Up / down in the level list: arrows, W/S, D-pad or left stick.</summary>
        public static bool MenuUp() => MenuStep(true);
        public static bool MenuDown() => MenuStep(false);

        static bool MenuStep(bool up)
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k != null && (up ? k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame
                                 : k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame)) return true;
            var g = Gamepad.current;
            return g != null && (up ? g.dpad.up.wasPressedThisFrame || g.leftStick.up.wasPressedThisFrame
                                    : g.dpad.down.wasPressedThisFrame || g.leftStick.down.wasPressedThisFrame);
#else
            return up ? Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)
                      : Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
#endif
        }

        /// <summary>Left / right in the menu's level grid (landscape): arrows, A/D, D-pad or left stick.</summary>
        public static bool MenuLeft() => MenuSide(true);
        public static bool MenuRight() => MenuSide(false);

        static bool MenuSide(bool left)
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k != null && (left ? k.leftArrowKey.wasPressedThisFrame || k.aKey.wasPressedThisFrame
                                   : k.rightArrowKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame)) return true;
            var g = Gamepad.current;
            return g != null && (left ? g.dpad.left.wasPressedThisFrame || g.leftStick.left.wasPressedThisFrame
                                      : g.dpad.right.wasPressedThisFrame || g.leftStick.right.wasPressedThisFrame);
#else
            return left ? Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)
                        : Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);
#endif
        }

        /// <summary>Enter, or Cross / A / Options on a controller: play the selected level.</summary>
        public static bool ConfirmPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame)) return true;
            var g = Gamepad.current;
            return g != null && (g.buttonSouth.wasPressedThisFrame || g.startButton.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.JoystickButton0);
#endif
        }

        /// <summary>P, or Pause on a controller (Options / Menu): pause or resume while playing.</summary>
        public static bool PausePressed()
        {
            if (Pressed(PadAction.Pause)) return true;
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            return k != null && k.pKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.P);
#endif
        }

        /// <summary>Rumbles the controller (both motors 0..1) for a moment. Nothing without a controller.</summary>
        public static void Rumble(float low, float high, float seconds)
        {
#if ENABLE_INPUT_SYSTEM
            var g = Gamepad.current;
            if (g == null) return;
            g.SetMotorSpeeds(low, high);
            RumbleStopper.StopAfter(seconds);
#endif
        }

        /// <summary>True while a finger or mouse button is down; x is the pointer x in pixels.</summary>
        public static bool PointerHeld(out float x)
        {
#if ENABLE_INPUT_SYSTEM
            var p = Pointer.current;
            if (p != null && p.press.isPressed)
            {
                x = p.position.ReadValue().x;
                return true;
            }
#else
            if (Input.touchCount > 0)
            {
                x = Input.GetTouch(0).position.x;
                return true;
            }
            if (Input.GetMouseButton(0))
            {
                x = Input.mousePosition.x;
                return true;
            }
#endif
            x = 0f;
            return false;
        }

        /// <summary>-1..1 from the keyboard.</summary>
        public static float KeyAxis()
        {
            float v = 0f;
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return 0f;
            if (k.aKey.isPressed || k.leftArrowKey.isPressed) v -= 1f;
            if (k.dKey.isPressed || k.rightArrowKey.isPressed) v += 1f;
#else
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) v -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) v += 1f;
#endif
            return v;
        }

        /// <summary>R, or Steer Speed (Y / Triangle; only asked after a crash or the finish): restart from the very start, ignoring the checkpoint.</summary>
        public static bool RestartPressed()
        {
            if (Pressed(PadAction.SteerSpeed)) return true;
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            return k != null && k.rKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.R);
#endif
        }

        /// <summary>F1, or Hitboxes on a controller (R3), pressed this frame (hitbox view toggle).</summary>
        public static bool ToggleHitboxes()
        {
            if (Pressed(PadAction.Hitboxes)) return true;
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            return k != null && k.f1Key.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.F1);
#endif
        }

        /// <summary>
        /// Esc (or Android Back), or Back on a controller (View / Create), pressed this frame: leave the play test.
        /// <paramref name="circleToo"/>: B / Circle counts as well (Pause menu); while playing B moves one lane instead.
        /// </summary>
        public static bool BackPressed(bool circleToo = false)
        {
            if (Pressed(PadAction.Back)) return true;
#if ENABLE_INPUT_SYSTEM
            var g = Gamepad.current;
            if (circleToo && g != null && g.buttonEast.wasPressedThisFrame) return true;
            var k = Keyboard.current;
            return k != null && k.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape) || circleToo && Input.GetKeyDown(KeyCode.JoystickButton1);
#endif
        }

        // ---------------- Mouse without holding (Settings > Auto Move) ----------------

        /// <summary>A mouse is there to steer with (not on phones and tablets).</summary>
        public static bool HasMouse
        {
            get
            {
                if (Application.isMobilePlatform) return false;
#if ENABLE_INPUT_SYSTEM
                return Mouse.current != null;
#else
                return Input.mousePresent;
#endif
            }
        }

        /// <summary>How far the mouse moved sideways this frame, in pixels (also while the cursor is locked).</summary>
        public static float MouseDeltaX()
        {
#if ENABLE_INPUT_SYSTEM
            var m = Mouse.current;
            return m != null ? m.delta.ReadValue().x : 0f;
#else
            return Input.GetAxisRaw("Mouse X") * 10f; // the default Mouse X axis is 0.1 per pixel
#endif
        }

        // ---------------- Controller mapping (Settings > Controller) ----------------

        /// <summary>The button an action has until it is mapped: a control path of the Gamepad (new Input System) or a JoystickButton (old).</summary>
        public static string DefaultBinding(PadAction action)
        {
#if ENABLE_INPUT_SYSTEM
            switch (action)
            {
                case PadAction.LaneLeft: return "buttonWest";
                case PadAction.LaneRight: return "buttonEast";
                case PadAction.Accelerator: return "buttonSouth";
                case PadAction.SteerSpeed: return "buttonNorth";
                case PadAction.Pause: return "start";
                case PadAction.Back: return "select";
                default: return "rightStickPress";
            }
#else
            switch (action)
            {
                case PadAction.LaneLeft: return "JoystickButton2";
                case PadAction.LaneRight: return "JoystickButton1";
                case PadAction.Accelerator: return "JoystickButton0";
                case PadAction.SteerSpeed: return "JoystickButton3";
                case PadAction.Pause: return "JoystickButton7";
                case PadAction.Back: return "JoystickButton6";
                default: return "JoystickButton9";
            }
#endif
        }

        public static bool Pressed(PadAction action)
        {
#if ENABLE_INPUT_SYSTEM
            var b = Button(action);
            return b != null && b.wasPressedThisFrame;
#else
            return Input.GetKeyDown(Key(action));
#endif
        }

        public static bool Held(PadAction action)
        {
#if ENABLE_INPUT_SYSTEM
            var b = Button(action);
            return b != null && b.isPressed;
#else
            return Input.GetKey(Key(action));
#endif
        }

        /// <summary>The name of the button mapped to <paramref name="action"/>, as the controller calls it ("Cross", "A", "L1"...).</summary>
        public static string ButtonName(PadAction action) => BindingName(GameSettings.Binding(action));

        public static string BindingName(string binding)
        {
#if ENABLE_INPUT_SYSTEM
            var g = Gamepad.current;
            var c = g != null ? Find(g, binding) : null;
            if (c != null && !string.IsNullOrEmpty(c.displayName)) return c.displayName; // "Cross" on a DualSense, "A" on Xbox
            switch (binding) // no controller connected: both names
            {
                case "buttonSouth": return "A / Cross";
                case "buttonEast": return "B / Circle";
                case "buttonWest": return "X / Square";
                case "buttonNorth": return "Y / Triangle";
                case "start": return "Menu / Options";
                case "select": return "View / Create";
                case "leftShoulder": return "LB / L1";
                case "rightShoulder": return "RB / R1";
                case "leftTrigger": return "LT / L2";
                case "rightTrigger": return "RT / R2";
                case "leftStickPress": return "L3";
                case "rightStickPress": return "R3";
                default: return binding != null && binding.StartsWith("dpad/") ? "D-pad " + binding.Substring(5) : binding;
            }
#else
            return binding != null && binding.StartsWith("JoystickButton") ? "Button " + binding.Substring(14) : binding;
#endif
        }

        /// <summary>
        /// Settings > Controller, waiting for a press: the first controller button pressed this frame, as a binding
        /// for <see cref="GameSettings.SetBinding"/>. False while nothing is pressed.
        /// </summary>
        public static bool ListenForButton(out string binding)
        {
            binding = null;
#if ENABLE_INPUT_SYSTEM
            var g = Gamepad.current;
            if (g == null) return false;
            foreach (var c in g.allControls)
            {
                // Stick directions are made from the stick (synthetic): a tilted stick would map itself.
                if (!(c is UnityEngine.InputSystem.Controls.ButtonControl b) || b.synthetic || !b.wasPressedThisFrame) continue;
                binding = b.path.Substring(g.path.Length + 1); // "buttonSouth", "dpad/up", "leftTrigger"...
                return true;
            }
            return false;
#else
            for (int i = 0; i < 20; i++)
                if (Input.GetKeyDown(KeyCode.JoystickButton0 + i))
                {
                    binding = "JoystickButton" + i;
                    return true;
                }
            return false;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        static Gamepad cachedPad;
        static readonly string[] cachedPath = new string[System.Enum.GetValues(typeof(PadAction)).Length];
        static readonly UnityEngine.InputSystem.Controls.ButtonControl[] cachedButton =
            new UnityEngine.InputSystem.Controls.ButtonControl[cachedPath.Length];

        /// <summary>The current controller's button mapped to <paramref name="action"/> (its default when the mapping is not on this controller).</summary>
        static UnityEngine.InputSystem.Controls.ButtonControl Button(PadAction action)
        {
            var g = Gamepad.current;
            if (g == null) return null;
            if (g != cachedPad)
            {
                cachedPad = g;
                System.Array.Clear(cachedPath, 0, cachedPath.Length);
            }
            int i = (int)action;
            string path = GameSettings.Binding(action);
            if (cachedPath[i] != path)
            {
                cachedPath[i] = path;
                cachedButton[i] = Find(g, path) ?? Find(g, DefaultBinding(action));
            }
            return cachedButton[i];
        }

        static UnityEngine.InputSystem.Controls.ButtonControl Find(Gamepad g, string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            try { return g.TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>(path); }
            catch (System.InvalidOperationException) { return null; } // there, but not a button
        }
#else
        static KeyCode Key(PadAction action)
        {
            return System.Enum.TryParse(GameSettings.Binding(action), out KeyCode k) ? k
                 : (KeyCode)System.Enum.Parse(typeof(KeyCode), DefaultBinding(action));
        }
#endif
    }

#if ENABLE_INPUT_SYSTEM
    /// <summary>Turns the controller's rumble off again after <see cref="GameInput.Rumble"/> (also when the scene changes or the game pauses).</summary>
    class RumbleStopper : MonoBehaviour
    {
        static RumbleStopper instance;
        float stopAt;

        public static void StopAfter(float seconds)
        {
            if (instance == null)
            {
                var go = new GameObject("Rumble") { hideFlags = HideFlags.HideInHierarchy };
                DontDestroyOnLoad(go);
                instance = go.AddComponent<RumbleStopper>();
            }
            instance.stopAt = Time.unscaledTime + seconds;
            instance.enabled = true;
        }

        void Update()
        {
            if (Time.unscaledTime < stopAt) return;
            Stop();
            enabled = false;
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) Stop();
        }

        void OnDisable() => Stop();

        static void Stop() => Gamepad.current?.SetMotorSpeeds(0f, 0f);
    }
#endif
}
