using UnityEngine;

namespace RTXOn
{
    /// <summary>Flickers an emissive beam and its light, purely visual.</summary>
    public class BeamPulse : MonoBehaviour
    {
        public Light beamLight;
        public float speed = 9f;
        float baseIntensity;
        Vector3 baseScale;

        void Start()
        {
            baseScale = transform.localScale;
            if (beamLight != null) baseIntensity = beamLight.intensity;
        }

        void Update()
        {
            float k = 0.85f + 0.15f * Mathf.Sin(Time.time * speed + transform.position.z);
            transform.localScale = new Vector3(baseScale.x * k, baseScale.y, baseScale.z * k);
            if (beamLight != null) beamLight.intensity = baseIntensity * k;
        }
    }
}
