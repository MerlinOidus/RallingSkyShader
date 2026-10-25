using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// Spotlight that swings ±30° like the original game (one swing every 12 rows): a hanging lamp,
    /// or with the theme's Spotlight Model a lamp on its tile beaming up. Lights up within 15 rows.
    /// </summary>
    public class Sweeper : TrackBehaviour
    {
        public float swingDegrees = 30f;
        public float rowsPerCycle = 12f;
        public float lightUpRows = 15f;
        public Renderer beam;
        public Light lamp;
        [Header("Spotlight Model (optional)")]
        [Tooltip("The model's swinging part (original: \"anim\"), turned with the beam.")]
        public Transform modelAnim;
        [Tooltip("Shown until the lamp lights up / once it is lit (original state1 / state2).")]
        public GameObject modelState1, modelState2;
        public float modelSign = 1f;

        void Update()
        {
            float ahead = Ahead;
            float angle = swingDegrees * Mathf.Sin(ahead / rowsPerCycle * Mathf.PI * 2f);
            transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            if (modelAnim != null) modelAnim.localEulerAngles = new Vector3(0f, 0f, angle * modelSign);
            bool lit = ahead <= lightUpRows;
            if (beam != null && beam.enabled != lit) beam.enabled = lit;
            if (lamp != null && lamp.enabled != lit) lamp.enabled = lit;
            if (modelState1 != null && modelState1.activeSelf == lit) modelState1.SetActive(!lit);
            if (modelState2 != null && modelState2.activeSelf != lit) modelState2.SetActive(lit);
        }
    }
}
