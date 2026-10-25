using System.Collections.Generic;
using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// In the game: sets the screen effects from the level's filter triggers at the ball's row every frame (so a
    /// start from a checkpoint is right at once), and turns them off when the game scene closes.
    /// </summary>
    public class ScreenFilterDirector : MonoBehaviour
    {
        BallController ball;
        Camera view;
        readonly List<FilterChange> changes = new List<FilterChange>();

        public void Use(BallController target, Camera camera, IEnumerable<FilterChange> list)
        {
            ball = target;
            view = camera;
            changes.Clear();
            if (list != null) foreach (var c in list) if (c != null) changes.Add(c);
            changes.Sort((a, b) => a.row.CompareTo(b.row));
            LateUpdate();
        }

        void LateUpdate()
        {
            if (ball == null || changes.Count == 0)
            {
                if (ScreenFilters.Active) ScreenFilters.Clear();
                return;
            }
            ScreenFilters.Target = view;
            ScreenFilters.ApplyAt(changes, ball.Position.z);
        }

        void OnDisable()
        {
            ScreenFilters.Clear();
            ScreenFilters.Target = null;
        }
    }
}
