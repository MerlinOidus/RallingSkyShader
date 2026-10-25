using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// The original laser's look (Rolling Sky 1.0 TileAnimator.FireLaser) on the original 16_enemyLaser mesh: from 15
    /// rows away the eyelids ("part" children) open and the head shakes while it charges; at 5 rows it fires (state1
    /// gives way to state2), kicks back and slides home as the ball comes, and the eyelids close once it is passed.
    /// Only the look: the beam and its danger are the remake's own (<see cref="LaserBeam"/>).
    /// </summary>
    public class LaserRig : TrackBehaviour
    {
        Transform anim;
        Transform[] parts;
        GameObject state1, state2;

        public void Setup()
        {
            anim = Find("anim");
            state1 = Find("state1")?.gameObject;
            state2 = Find("state2")?.gameObject;
            var list = new System.Collections.Generic.List<Transform>();
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("part")) list.Add(t);
            parts = list.ToArray();
            // The remake draws its own glowing beam; the mesh's short beam piece would double it.
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == "beam") t.gameObject.SetActive(false);
            Update();
        }

        Transform Find(string name)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        void Update()
        {
            if (anim == null) return;
            float d = Ahead;
            float charge = P01(d, 15f, 5f, out bool below, out bool fired);
            if (below)
            {
                Pose(0f, 0f, 0f, 0f);
                Fire(false);
                return;
            }
            if (!fired)
            {
                // Charging: eyelids open, the head trembles faster and faster.
                float open = Smooth(charge, true, false);
                Pose(0.05f * open, 0f, open * 0.075f * Mathf.Sin(charge * 15f * Mathf.PI * 2f), 0f);
                Fire(false);
                return;
            }

            float kick = P01(d, 5f, 4.5f, out _, out bool kicked);
            float lids;
            float z;
            if (!kicked)
            {
                lids = Smooth(kick, false, true);
                z = lids;
                Fire(true);
            }
            else
            {
                float home = P01(d, 4.5f, 0f, out _, out bool passed);
                z = Smooth(1f - home, true, true);
                if (passed)
                {
                    lids = Smooth(1f - P01(d, 0f, -1f), true, false); // closes after the ball has passed
                    Fire(false);
                }
                else
                {
                    lids = 1f;
                    Fire(true);
                }
            }
            Pose((1f - lids) * 0.05f, -90f * lids, 0f, z);
        }

        void Pose(float partX, float partYaw, float animY, float animZ)
        {
            foreach (var p in parts)
            {
                var lp = p.localPosition;
                lp.x = partX;
                p.localPosition = lp;
                p.localEulerAngles = new Vector3(0f, partYaw, 0f);
            }
            var a = anim.localPosition;
            a.y = animY;
            a.z = animZ;
            anim.localPosition = a;
        }

        void Fire(bool on)
        {
            if (state1 != null && state1.activeSelf == on) state1.SetActive(!on);
            if (state2 != null && state2.activeSelf != on) state2.SetActive(on);
        }

        // ---- Rolling Sky 1.0 helpers (MathUtils.ToPercent01, FloatAnim.Smooth) ----

        static float P01(float value, float a, float b, out bool below, out bool above)
        {
            float t = (value - a) / (b - a);
            below = t < 0f;
            above = t > 1f;
            return Mathf.Clamp01(t);
        }

        static float P01(float value, float a, float b) => P01(value, a, b, out _, out _);

        static float Smooth(float v, bool start, bool end)
        {
            if (start && end) return (Mathf.Sin((v - 0.5f) * Mathf.PI) + 1f) / 2f;
            if (start) return Mathf.Sin((v - 1f) * Mathf.PI / 2f) + 1f;
            if (end) return Mathf.Sin(v * Mathf.PI / 2f);
            return v;
        }
    }
}
