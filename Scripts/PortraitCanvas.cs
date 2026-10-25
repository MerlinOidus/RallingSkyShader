using UnityEngine;
using UnityEngine.UI;

namespace RTXOn
{
    /// <summary>
    /// Bigger UI on a phone held upright (Adrian 2026-10-24, a 19.5:9 screen): the canvas is 1400 units wide in
    /// portrait instead of 1920, so everything is about 1.4x bigger. Landscape keeps 1920 x 1080.
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    public class PortraitCanvas : MonoBehaviour
    {
        public static readonly Vector2 Landscape = new Vector2(1920f, 1080f);
        public static readonly Vector2 Portrait = new Vector2(1400f, 2400f);

        CanvasScaler scaler;

        public static void Add(Canvas canvas)
        {
            if (canvas != null && canvas.GetComponent<PortraitCanvas>() == null) canvas.gameObject.AddComponent<PortraitCanvas>();
        }

        void Awake()
        {
            scaler = GetComponent<CanvasScaler>();
            Apply();
        }

        void Update() => Apply();

        void Apply()
        {
            var want = ScreenRotation.Portrait ? Portrait : Landscape;
            if (scaler.referenceResolution != want) scaler.referenceResolution = want;
        }
    }
}
