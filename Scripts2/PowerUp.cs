using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// A power-up cube (tiles 160-168): spins and bobs on its tile; when the ball touches it, it bursts and does its
    /// thing with the settings of its cell (<see cref="PartArgs"/>): Fly Jump, Short Jump, Bounce, Switch, Invisible,
    /// Speed Up, Speed Down, Hide Objects or Dialogue. Switch, Speed Up/Down, Hide Objects and Dialogue are trigger
    /// lines (<see cref="PartArgs.IsRowTrigger"/>): they also go off when the ball crosses their row in another lane or in the air.
    /// </summary>
    public class PowerUp : MonoBehaviour
    {
        public PartArgs args;
        public int row;
        public Transform visual;
        public float spinSpeed = 90f;

        bool taken;
        float takenTime;
        Vector3 basePos;
        float lastBallZ = float.NaN;

        void Start()
        {
            if (visual != null) basePos = visual.localPosition;
        }

        /// <summary>The ball touched it.</summary>
        public void Collect(BallController ball)
        {
            if (taken || args == null) return;
            taken = true;
            takenTime = 0f;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            GameAudio.Play(SoundFor(args.Kind));

            var gm = GameManager.Instance;
            float z = transform.position.z;
            switch (args.Kind)
            {
                case PartKind.FlyJump:
                case PartKind.ShortJump:
                    // Lands exactly Distance rows after the cube's tile, like a jump pad.
                    ball.Launch(args.distance + z - ball.Position.z, args.height);
                    break;
                case PartKind.Bounce:
                    ball.StartBounce(z, args);
                    break;
                case PartKind.Switch:
                    if (gm != null && gm.track != null)
                        gm.track.SwitchRows(row + Mathf.Max(1, Mathf.RoundToInt(args.startRows)), Mathf.Max(1, Mathf.RoundToInt(args.distance)));
                    CameraRig.Shake(0.3f);
                    break;
                case PartKind.Invisible:
                    ball.Hide(args.seconds);
                    break;
                case PartKind.SpeedUp:
                case PartKind.SpeedDown:
                    if (gm != null) gm.StartSpeedZone(row, args);
                    break;
                case PartKind.HideObjects:
                    if (gm != null && gm.track != null) gm.track.HideObjects(row, Mathf.Max(1, Mathf.RoundToInt(args.distance)), args.seconds);
                    break;
                case PartKind.Dialogue:
                    DialoguePopup.Show(ball, args.text, args.seconds);
                    break;
            }
        }

        static Sfx SoundFor(PartKind kind)
        {
            switch (kind)
            {
                case PartKind.SpeedUp: return Sfx.SpeedUp;
                case PartKind.SpeedDown: return Sfx.SpeedDown;
                case PartKind.Switch: return Sfx.Switch;
                case PartKind.Invisible: return Sfx.Invisible;
                default: return Sfx.PowerUp;
            }
        }

        /// <summary>Trigger lines go off when the ball crosses the row (not when it already starts past it).</summary>
        void CheckTriggerLine()
        {
            if (args == null || !PartArgs.IsRowTrigger(args.Kind)) return;
            var gm = GameManager.Instance;
            if (gm == null || gm.ball == null || !gm.ball.Alive || gm.State != GameState.Playing)
            {
                lastBallZ = float.NaN;
                return;
            }
            float z = gm.ball.Position.z, line = transform.position.z;
            if (!float.IsNaN(lastBallZ) && lastBallZ < line && z >= line) Collect(gm.ball);
            lastBallZ = z;
        }

        void Update()
        {
            if (!taken) CheckTriggerLine();
            if (visual == null) return;
            if (!taken)
            {
                visual.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
                visual.localPosition = basePos + Vector3.up * Mathf.Sin(Time.time * 3f + transform.position.z) * 0.06f;
                return;
            }
            // Bursts: grows quickly and vanishes.
            takenTime += Time.deltaTime;
            float p = Mathf.Clamp01(takenTime / 0.25f);
            visual.localScale = Vector3.one * (1f + 0.8f * p) * (1f - p);
            if (p >= 1f) Destroy(gameObject);
        }
    }
}
