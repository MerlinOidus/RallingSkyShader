using UnityEngine;

namespace RTXOn
{
    /// <summary>Base for track pieces that animate from the ball's distance, so timing stays in sync with the level.</summary>
    public abstract class TrackBehaviour : MonoBehaviour
    {
        /// <summary>
        /// Adrian 2026-10-24: objects stand still until their trigger fires or the ball is this many rows away, then
        /// animate (Roller, Spotlight and the Binaries keep looping all the time).
        /// </summary>
        public const float WakeRows = 25f;

        /// <summary>
        /// Rows between this piece and the ball (positive = still ahead). Read from the live position, because a
        /// moving platform can carry the piece one row forward or back; no piece moves itself along z.
        /// </summary>
        /// <remarks>Under an Event Group that waits for its trigger it reads as far away until the group fires
        /// (<see cref="GroupEventRunner.SleepAhead"/>), so the piece keeps its idle pose.</remarks>
        protected float Ahead
        {
            get
            {
                var gm = GameManager.Instance;
                float ballZ;
                if (gm != null && gm.ball != null) ballZ = gm.ball.Position.z;
                else if (LevelPreviewStage.Ball != null) ballZ = LevelPreviewStage.Ball.position.z; // the Main Menu's 3D LevelPreview
                else return 999f;
                float real = transform.position.z - ballZ;
                if (!sleeperFound)
                {
                    // Looked up again until a later frame than the first look: the Event Group holder is made after the
                    // pieces of its cell, in the frame the row is built.
                    sleeper = GetComponentInParent<GroupEventRunner>();
                    if (firstLook < 0) firstLook = Time.frameCount;
                    else if (Time.frameCount > firstLook) sleeperFound = true;
                }
                return sleeper != null ? sleeper.SleepAhead(real) : real;
            }
        }

        GroupEventRunner sleeper;
        bool sleeperFound;
        int firstLook = -1;

        protected virtual void Start() { }
    }
}
