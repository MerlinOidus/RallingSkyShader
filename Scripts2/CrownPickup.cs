using UnityEngine;

namespace RTXOn
{
    /// <summary>Crown pickup (tile 150): spins and bobs; touching it counts a crown, then it flies up and away.</summary>
    public class CrownPickup : MonoBehaviour
    {
        public Transform visual;
        public float spinSpeed = 120f;

        bool taken;
        float takenTime;
        Vector3 basePos;

        void Start() => basePos = visual != null ? visual.localPosition : Vector3.zero;

        /// <summary>The ball touched it.</summary>
        public void Collect()
        {
            if (taken) return;
            taken = true;
            takenTime = 0f;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            if (GameManager.Instance != null) GameManager.Instance.AddCrown();
            GameAudio.Play(Sfx.Crown);
        }

        void Update()
        {
            if (visual == null) return;
            if (!taken)
            {
                visual.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
                visual.localPosition = basePos + Vector3.up * Mathf.Sin(Time.time * 2.5f) * 0.1f;
                return;
            }
            // Fast spin, up and forward with the ball, then shrink away.
            takenTime += Time.deltaTime;
            float p = Mathf.Clamp01(takenTime / 0.7f);
            visual.Rotate(Vector3.up, 900f * Time.deltaTime, Space.World);
            visual.localPosition = basePos + new Vector3(0f, 2.2f * p, 3.5f * p);
            visual.localScale = Vector3.one * (1f + 0.6f * Mathf.Sin(p * Mathf.PI) - p * 0.9f);
            if (p >= 1f) Destroy(gameObject);
        }
    }
}
