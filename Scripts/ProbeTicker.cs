using UnityEngine;
using UnityEngine.Rendering;

namespace RTXOn
{
    /// <summary>A live reflection probe rendered every few frames (Graphics Medium / High) instead of every frame.</summary>
    public class ProbeTicker : MonoBehaviour
    {
        [HideInInspector] public int resolution = 128;
        [HideInInspector] public ReflectionProbeRefreshMode refreshMode = ReflectionProbeRefreshMode.EveryFrame;
        [HideInInspector] public ReflectionProbeTimeSlicingMode slicing = ReflectionProbeTimeSlicingMode.IndividualFaces;
        int every;
        ReflectionProbe probe;

        /// <summary><paramref name="frames"/> 0 = the probe's own refresh (every frame).</summary>
        public void Use(int size, int frames)
        {
            if (probe == null) probe = GetComponent<ReflectionProbe>();
            if (probe.resolution != size) probe.resolution = size;
            every = frames;
            if (frames <= 0)
            {
                probe.refreshMode = refreshMode;
                probe.timeSlicingMode = slicing;
            }
            else
            {
                probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
                probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
            }
        }

        void Update()
        {
            if (every <= 0 || probe == null || !probe.enabled) return;
            if (Time.frameCount % every == 0) probe.RenderProbe();
        }
    }
}
