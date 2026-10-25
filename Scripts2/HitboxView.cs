using System.Collections.Generic;
using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// Testing aid: draws every Hazard box (red, orange = jump-safe) and the ball's hazard hit point (green) in the Game
    /// view while GameManager.showHitboxes is on. F1 toggles it during Play. Draws only, changes nothing.
    /// </summary>
    public class HitboxView : MonoBehaviour
    {
        static readonly Color Deadly = new Color(1f, 0.1f, 0.1f, 0.35f);
        static readonly Color JumpSafe = new Color(1f, 0.6f, 0f, 0.35f);
        static readonly Color BallHit = new Color(0.2f, 1f, 0.3f, 0.45f);

        Mesh cube, sphere;
        Material deadly, jumpSafe, ballHit;
        readonly List<BoxCollider> boxes = new List<BoxCollider>();
        Hazard[] hazards = new Hazard[0];
        float nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            var go = new GameObject("Hitbox View");
            go.hideFlags = HideFlags.DontSave;
            go.AddComponent<HitboxView>();
        }

        void Awake()
        {
            cube = MakeMesh(PrimitiveType.Cube);
            sphere = MakeMesh(PrimitiveType.Sphere);
            deadly = MakeMaterial(Deadly);
            jumpSafe = MakeMaterial(JumpSafe);
            ballHit = MakeMaterial(BallHit);
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            if (GameInput.ToggleHitboxes()) gm.showHitboxes = !gm.showHitboxes;
            if (!gm.showHitboxes || deadly == null) return;

            // Levels can be rebuilt (restart, editor test), so look for hazards again every second.
            if (Time.unscaledTime >= nextScan)
            {
                hazards = FindObjectsByType<Hazard>(FindObjectsSortMode.None);
                nextScan = Time.unscaledTime + 1f;
            }

            foreach (var hz in hazards)
            {
                if (hz == null || !hz.lethal) continue;
                bool safe = hz.jumpSafe;
                hz.GetComponentsInChildren(boxes); // animated models: a box on each moving part (AnimatedHitbox)
                foreach (var box in boxes)
                {
                    if (box == null || !box.enabled || box.GetComponentInParent<Hazard>() != hz) continue;
                    var t = box.transform;
                    var m = Matrix4x4.TRS(t.TransformPoint(box.center), t.rotation, Vector3.Scale(t.lossyScale, box.size));
                    Graphics.DrawMesh(cube, m, safe ? jumpSafe : deadly, 0);
                }
            }

            if (gm.ball != null)
            {
                float d = Mathf.Max(gm.ball.hazardHitRadius * 2f, 0.06f); // BallController.CheckContacts: hazards test the centre
                Graphics.DrawMesh(sphere, Matrix4x4.TRS(gm.ball.transform.position, Quaternion.identity, Vector3.one * d), ballHit, 0);
            }
        }

        static Mesh MakeMesh(PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            Destroy(go);
            return mesh;
        }

        static Material MakeMaterial(Color c)
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) return null;
            var m = new Material(shader) { color = c, renderQueue = 3100 };
            return m;
        }
    }
}
