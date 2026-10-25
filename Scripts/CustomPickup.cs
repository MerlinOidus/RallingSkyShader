using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// A Custom Obstacle on the Collect layer: the ball takes it by touching it. It counts as a Gem, a Crown or nothing
    /// (<see cref="CustomObstacle.collectAs"/>), then shrinks away. Its look and animation are the obstacle's own.
    /// </summary>
    public class CustomPickup : MonoBehaviour
    {
        public CustomCollect counts = CustomCollect.Gem;

        bool taken;
        float takenAt;
        Vector3 startScale;

        public void Collect()
        {
            if (taken) return;
            taken = true;
            takenAt = Time.time;
            startScale = transform.localScale;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            var gm = GameManager.Instance;
            if (counts == CustomCollect.Crown)
            {
                if (gm != null) gm.AddCrown();
                GameAudio.Play(Sfx.Crown);
            }
            else
            {
                if (counts == CustomCollect.Gem && gm != null) gm.AddGem();
                GameAudio.Play(Sfx.Pickup);
            }
        }

        void Update()
        {
            if (!taken) return;
            float t = (Time.time - takenAt) / 0.35f;
            if (t >= 1f)
            {
                gameObject.SetActive(false);
                return;
            }
            transform.localScale = startScale * (1f - t * t);
            transform.localPosition += Vector3.up * Time.deltaTime * 3f;
        }
    }
}
