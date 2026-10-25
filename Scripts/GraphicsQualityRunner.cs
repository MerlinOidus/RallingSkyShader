using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RTXOn
{
    /// <summary>Applies the quality to each scene as it loads, and keeps new lights and probes in line.</summary>
    public class GraphicsQualityRunner : MonoBehaviour
    {
        void OnEnable() => SceneManager.sceneLoaded += Loaded;
        void OnDisable() => SceneManager.sceneLoaded -= Loaded;

        void Start() => StartCoroutine(ApplySoon());

        void Loaded(Scene scene, LoadSceneMode mode) => StartCoroutine(ApplySoon());

        IEnumerator ApplySoon()
        {
            GraphicsQuality.Apply();
            yield return null; // what the scene's scripts make in Start (camera rig, environment, editor views)
            yield return null;
            GraphicsQuality.Apply();
        }

        float next;

        void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 1f;
            GraphicsQuality.ApplyLightsAndProbes();
        }
    }
}
