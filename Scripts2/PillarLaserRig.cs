using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// The original pillar laser (Rolling Sky 1.0 tile 105, TileAnimator.RisePillar with the special alt anim, on
    /// 19_enemyPillarLaser): the pillar rises like any pillar (<see cref="PillarRig"/>, 3 blocks), shakes sideways from
    /// 15 to 5 rows, then fires: its "state2" beam shows, the camera shakes, and the 15 cells of its lane towards the
    /// ball turn deadly (jumping over is safe). The beam stays on the pillar while it sinks.
    /// </summary>
    public class PillarLaserRig : TrackBehaviour
    {
        const float ShakeFrom = 15f, FireRows = 5f, OffRows = -1f;

        Transform anim;
        GameObject state1, state2, beam;
        float baseX;
        bool fired;

        public void Setup(Transform animTransform, GameObject idle, GameObject firing, GameObject beamRoot)
        {
            anim = animTransform;
            state1 = idle;
            state2 = firing;
            beam = beamRoot;
            if (anim != null) baseX = anim.localPosition.x;
            Show(false);
            if (beam != null) beam.SetActive(false);
        }

        void Show(bool on)
        {
            fired = on;
            if (state1 != null) state1.SetActive(!on);
            if (state2 != null) state2.SetActive(on);
        }

        void Update()
        {
            float d = Ahead;

            // Shakes harder and harder before it fires (FloatAnim.Smooth(v, true, false) * 0.075 * a fast wave).
            if (anim != null)
            {
                float x = baseX;
                if (d <= ShakeFrom && d > FireRows)
                {
                    float v = EnemyRig.P01(d, ShakeFrom, FireRows);
                    float s = Mathf.Sin((v - 1f) * Mathf.PI * 0.5f) + 1f;
                    x += s * 0.075f * Mathf.Sin(v * 15f * Mathf.PI * 2f);
                }
                var p = anim.localPosition;
                p.x = x;
                anim.localPosition = p;
            }

            bool on = d <= FireRows;
            if (on != fired)
            {
                Show(on);
                // The original shakes the camera as it fires (force 0.8); not when starting already past it.
                if (on && d > FireRows - 1f) CameraRig.Shake(0.8f);
            }
            if (beam != null)
            {
                bool deadly = on && d > OffRows;
                if (beam.activeSelf != deadly) beam.SetActive(deadly);
            }
        }
    }
}
