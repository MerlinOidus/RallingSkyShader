using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// Phone screen turning: the Main Menu turns with the phone (portrait or landscape), Gameplay is locked to
    /// portrait (Adrian 2026-10-24: in landscape the ball was off screen), the Level Editor to landscape. PC is never changed.
    /// </summary>
    public static class ScreenRotation
    {
        /// <summary>Main Menu: portrait or landscape, as the phone is held.</summary>
        public static void Free()
        {
            if (!Application.isMobilePlatform) return;
            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
        }

        /// <summary>Gameplay: portrait only, like the original game.</summary>
        public static void PortraitOnly()
        {
            if (!Application.isMobilePlatform) return;
            Screen.orientation = ScreenOrientation.Portrait;
        }

        /// <summary>Level Editor: landscape only (it needs a wide screen).</summary>
        public static void Landscape()
        {
            if (!Application.isMobilePlatform) return;
            Screen.orientation = ScreenOrientation.LandscapeLeft;
        }

        /// <summary>True while the screen is taller than wide (a phone held upright, or a portrait Game view).</summary>
        public static bool Portrait => Screen.height > Screen.width;

        /// <summary>Phones held upright: share of the screen height the top row moves down (Adrian 2026-10-24: ~10%).</summary>
        public const float PhoneTopShare = 0.1f;

        /// <summary>
        /// How far the HUD's and the Main Menu's top row move down, in canvas units (<paramref name="canvasHeight"/> =
        /// the canvas's height), so the camera hole / notch doesn't cover the progress bar and buttons (Adrian
        /// 2026-10-24). Phones held upright only (the editor with Android as the build target too, to preview it):
        /// <see cref="PhoneTopShare"/>, or the phone's own safe area when that asks for more. 0 on PC and in landscape.
        /// </summary>
        public static float TopInset(float canvasHeight)
        {
            if (!GraphicsQuality.Mobile || !Portrait) return 0f;
            float h = Mathf.Max(1f, Screen.height);
            float safe = Mathf.Clamp01((h - Screen.safeArea.yMax) / h);
            return Mathf.Max(PhoneTopShare, safe) * canvasHeight;
        }
    }
}
