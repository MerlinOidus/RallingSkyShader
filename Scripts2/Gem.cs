using UnityEngine;

namespace RTXOn
{
    public class Gem : MonoBehaviour
    {
        [Tooltip("Star = 5 (score value). The GEMS counter counts every gem or star as one pickup.")]
        public int value = 1;
        public float spinSpeed = 120f;
        Transform visual;
        Vector3 basePos;
        bool collected;

        void Start()
        {
            visual = transform.childCount > 0 ? transform.GetChild(0) : transform;
            basePos = visual.localPosition;
        }

        void Update()
        {
            if (collected)
            {
                visual.localScale = Vector3.Lerp(visual.localScale, Vector3.zero, Time.deltaTime * 14f);
                visual.localPosition += Vector3.up * Time.deltaTime * 3f;
                return;
            }
            visual.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
            visual.localPosition = basePos + Vector3.up * Mathf.Sin(Time.time * 3f + transform.position.z) * 0.08f;
        }

        public void Collect()
        {
            if (collected) return;
            collected = true;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            GameManager.Instance.AddGem();
            GameAudio.Play(Sfx.Pickup);
            Destroy(gameObject, 0.4f);
        }
    }
}
