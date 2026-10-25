using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// CustomTriggerTiles (159): when the ball rolls onto the tile (or, Whole Row, crosses its row anywhere), its
    /// <see cref="group"/> fires (<see cref="GroupTriggers"/>): Trigger Objects, Custom Obstacles and Custom Moves with that Group start. The pad
    /// on the tile sinks in and stays pressed.
    /// </summary>
    public class TriggerPad : TrackBehaviour
    {
        public int group = 1;
        public bool wholeRow;
        public Transform pad;
        public Renderer glow;

        float lastBallZ = float.NaN;
        float pressedAt = float.NaN;
        Vector3 padHome;

        protected override void Start()
        {
            base.Start();
            if (pad != null) padHome = pad.localPosition;
            if (GroupTriggers.TryFired(group, out float t) && float.IsNegativeInfinity(t)) pressedAt = float.NegativeInfinity;
        }

        void Update()
        {
            if (float.IsNaN(pressedAt)) Check();
            if (pad == null) return;
            float p = float.IsNaN(pressedAt) ? 0f : float.IsNegativeInfinity(pressedAt) ? 1f : Mathf.Clamp01((Time.time - pressedAt) / 0.12f);
            pad.localPosition = padHome + Vector3.down * (0.045f * p);
        }

        void Check()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.ball == null || !gm.ball.Alive || gm.State != GameState.Playing)
            {
                lastBallZ = float.NaN;
                return;
            }
            var ball = gm.ball.Position;
            float line = transform.position.z - 0.35f; // the near edge of the tile, a little in
            bool crossed = !float.IsNaN(lastBallZ) && lastBallZ < line && ball.z >= line;
            lastBallZ = ball.z;
            if (!crossed) return;
            if (!wholeRow && (Mathf.Abs(ball.x - transform.position.x) > 0.6f || ball.y > 1f)) return;
            pressedAt = Time.time;
            GroupTriggers.Fire(group);
            GameAudio.Play(Sfx.Switch);
        }
    }
}
