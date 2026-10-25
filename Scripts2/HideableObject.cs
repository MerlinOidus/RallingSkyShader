using System.Collections.Generic;
using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// A piece of an obstacle (Object layer). The Hide Objects power-up turns its renderers off for a few seconds;
    /// its colliders stay, so it still kills. Renderers that were already off stay off afterwards.
    /// </summary>
    public class HideableObject : MonoBehaviour
    {
        readonly List<Renderer> hidden = new List<Renderer>();
        float until = -1f;

        void Awake() => enabled = false; // runs only while hidden

        public void Hide(float seconds)
        {
            until = Mathf.Max(until, Time.time + seconds);
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled) continue;
                r.enabled = false;
                hidden.Add(r);
            }
            enabled = true;
        }

        void Update()
        {
            if (until < 0f || Time.time < until) return;
            foreach (var r in hidden) if (r != null) r.enabled = true;
            hidden.Clear();
            until = -1f;
            enabled = false;
        }
    }
}
