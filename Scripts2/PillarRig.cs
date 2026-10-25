using System.Collections.Generic;
using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// The original pillar (Rolling Sky 1.0 TileAnimator.RisePillar on 18_enemyPillar): a stack of 1x1 blocks under
    /// the floor that slams up <see cref="height"/> blocks (tiles 85-88 = 1-4) between 13 and 3 rows from the ball,
    /// and sinks back once the ball has passed. Safe pillars (89-92) leave a ball-sized gap at floor level: the
    /// blocks above it hang over the track and the ball rolls under them.
    /// Works on the original mesh ("anim" with "part0".."part3") or on a stack the builder made the same way.
    /// </summary>
    public class PillarRig : TrackBehaviour
    {
        public int height = 1;
        public bool safe;
        [Tooltip("Level editor view: always shown fully risen.")]
        public bool preview;

        Transform anim;
        Transform[] parts;
        readonly List<Renderer[]> partRenderers = new List<Renderer[]>();

        /// <summary>Tile.CreateAnimator (Pillar): stacks the blocks under "anim"; safe pillars move the cap down to leave the gap.</summary>
        public void Setup(int pillarHeight, bool isSafe)
        {
            height = Mathf.Clamp(pillarHeight, 1, 4);
            safe = isSafe;
            anim = EnemyRig.Find(transform, "anim");
            if (anim == null) return;
            var list = new List<Transform>();
            foreach (Transform t in anim)
                if (t.name.StartsWith("part")) list.Add(t);
            parts = list.ToArray();
            if (parts.Length == 0) return;

            int last = parts.Length - 1;
            int gap = safe ? height - 1 : -1;
            if (!safe) parts[last].gameObject.SetActive(false);
            for (int i = 0; i < last; i++)
            {
                var t = parts[i];
                int shift = 0;
                if (safe && i >= gap)
                {
                    shift = 1;
                    if (i == gap)
                    {
                        t = parts[last];
                        parts[i].gameObject.SetActive(false);
                    }
                }
                t.localPosition = new Vector3(0f, -(i + shift), 0f);
            }
            anim.localPosition = Vector3.zero;
            FindRenderers();
            Update();
        }

        void Update()
        {
            if (anim == null || parts == null) return;
            // Unity rebuilt the scripts during Play: this list is not kept, so find the renderers again.
            if (partRenderers.Count != parts.Length) FindRenderers();
            float rise = preview ? height : Rise(Ahead);
            EnemyRig.SetY(anim, rise);

            // Blocks still under the floor are hidden (they would show under the track and flicker in the floor).
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null) continue;
                float top = rise + parts[i].localPosition.y;
                bool show = top > 0.02f;
                foreach (var r in partRenderers[i]) if (r != null) r.enabled = show;
            }
        }

        void FindRenderers()
        {
            partRenderers.Clear();
            foreach (var p in parts) partRenderers.Add(p != null ? p.GetComponentsInChildren<Renderer>(true) : new Renderer[0]);
        }

        /// <summary>How many blocks the stack stands above the floor, <paramref name="d"/> rows from the ball.</summary>
        float Rise(float d)
        {
            float v = EnemyRig.P01(d, 13f, 3f, out bool below, out bool above);
            if (below) return 0f;
            if (!above)
            {
                // Slams up with a tiny rebound (FloatAnim.ImpactLate(v, 0.0125)).
                return Mathf.Max(0f, height - 4f + 4f * EnemyRig.Impact(v, 0.0125f, 0.75f));
            }
            float back = EnemyRig.P01(d, 0.5f, -3.5f, out bool notYet, out bool gone);
            if (notYet) return height;
            if (gone) return 0f;
            float sink = Mathf.Sin((back - 1f) * Mathf.PI * 0.5f) + 1f; // FloatAnim.Smooth(v, true, false)
            return Mathf.Max(0f, height - 4f * sink);
        }
    }
}
