using System.Collections.Generic;
using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// The tiles of one fragile section (see FragileSections), Adrian 2026-10-19 (like the original Volcano level):
    /// the ball rolls onto it like normal floor, no wobble. The moment it touches the section, the section's tiles in
    /// the touched row (the whole 5-lane row) stay solid and every other tile of the section lets go, so rolling on
    /// to the next row of the same section drops the ball. The touched row lets go as soon as the ball has left it.
    /// A tile that lets go turns its floor collider into a trigger, breaks (glass pieces), tips over and drops
    /// over 4 rows of ball travel.
    /// </summary>
    public class FragileGroup
    {
        public bool Touched { get; private set; }
        /// <summary>World z of the row the ball touched: the only row of the section that still holds it.</summary>
        public float HoldZ { get; private set; }
        public Vector3 Pivot { get; private set; }

        public void Touch(Vector3 pivot)
        {
            if (Touched) return;
            Touched = true;
            HoldZ = pivot.z;
            Pivot = pivot;
            GameAudio.Play(Sfx.Fragile, 0.6f);
        }
    }

    /// <summary>One fragile floor tile. Its children (items, hazards) fall with it.</summary>
    public class FragileTile : MonoBehaviour
    {
        public FragileGroup group;
        public int row, lane;
        /// <summary>Tile Type: the Fragile Active atlas, shown once the section is touched (the original's ActivateFragile).</summary>
        public Material activeMaterial;
        bool activated;

        Vector3 homePosition;
        Quaternion homeRotation;
        bool released;
        float releaseZ;
        readonly List<Collider> floor = new List<Collider>();
        static Mesh shardMesh;
        const int ShardCount = 5;
        const float FallRows = 4f;

        void Start()
        {
            GetComponents(floor);
            if (group == null) group = new FragileGroup();
        }

        /// <summary>
        /// Is the ball's centre over this tile? Worked out from where the tile is now, not from its lane, so it is
        /// right on a row that a Switch mirrored.
        /// </summary>
        bool BallOver(Vector3 p)
        {
            var t = transform.position;
            return Mathf.Abs(p.x - t.x) <= 0.5f && Mathf.Abs(p.z - t.z) <= 0.5f;
        }

        bool InHoldRow() { return Mathf.Abs(transform.position.z - group.HoldZ) < 0.25f; }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.ball == null) return;
            var ball = gm.ball;
            var p = ball.Position;

            if (!group.Touched)
            {
                if (ball.Alive && ball.Grounded && BallOver(p)) group.Touch(transform.position);
                return;
            }

            Activate();
            if (!released)
            {
                // The touched row holds the ball, still and solid like normal floor, until the ball leaves the row;
                // the ball may steer across all of its lanes meanwhile.
                if (InHoldRow() && ball.Alive && Mathf.Abs(p.z - group.HoldZ) <= 0.5f) return;
                Release(p.z);
            }

            float k = Mathf.Clamp01((p.z - releaseZ) / FallRows);
            if (k >= 1f)
            {
                gameObject.SetActive(false);
                return;
            }
            float s = k * k * (3f - 2f * k);
            // Tips over about the touched tile, so the rest of the section falls as one piece. Tiles behind it (the
            // ball landed in the middle of the section from a jump) tip the other way, so they go down, not up.
            var pivot = group.Pivot;
            var turn = Quaternion.Euler((homePosition.z < pivot.z ? -90f : 90f) * s, 0f, 0f);
            transform.SetPositionAndRotation(pivot + turn * (homePosition - pivot) + Vector3.down * (10f * s), turn * homeRotation);
        }

        void Activate()
        {
            if (activated || activeMaterial == null) return;
            activated = true;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = activeMaterial;
                r.sharedMaterials = mats;
            }
        }

        void Release(float ballZ)
        {
            released = true;
            releaseZ = ballZ;
            homePosition = transform.position;
            homeRotation = transform.rotation;
            foreach (var c in floor) c.isTrigger = true; // the ball goes through from now on
            Shatter();
        }

        /// <summary>Glass breaking: a few pieces of the tile fly off and fall when it lets go behind the ball.</summary>
        void Shatter()
        {
            var rend = GetComponentInChildren<MeshRenderer>();
            if (rend == null) return;
            if (shardMesh == null) shardMesh = MeshFactory.BeveledBox(new Vector3(0.22f, 0.08f, 0.22f), 0.02f);
            for (int i = 0; i < ShardCount; i++)
            {
                var shard = new GameObject("Glass Shard");
                shard.transform.position = homePosition + new Vector3(Random.Range(-0.35f, 0.35f), -0.05f, Random.Range(-0.35f, 0.35f));
                shard.transform.rotation = Random.rotation;
                shard.AddComponent<MeshFilter>().sharedMesh = shardMesh;
                shard.AddComponent<MeshRenderer>().sharedMaterial = rend.sharedMaterial;
                var rb = shard.AddComponent<Rigidbody>(); // no collider: the pieces never touch the ball or the track
                rb.mass = 0.05f;
                rb.AddForce((Random.insideUnitSphere + Vector3.up * 0.6f) * 0.12f, ForceMode.Impulse);
                rb.AddTorque(Random.onUnitSphere * 0.01f, ForceMode.Impulse);
                Destroy(shard, 1.5f);
            }
        }
    }
}
