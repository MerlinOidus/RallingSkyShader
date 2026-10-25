using UnityEngine;

namespace RTXOn
{
    /// <summary>Plays an animated GIF on a picture obstacle: shows the frame for the time, without its own material.</summary>
    [RequireComponent(typeof(Renderer))]
    public class PictureAnimator : MonoBehaviour
    {
        public ImageFrames frames;

        Renderer target;
        MaterialPropertyBlock block;
        Texture2D shown;

        void Awake()
        {
            target = GetComponent<Renderer>();
            block = new MaterialPropertyBlock();
        }

        void Update()
        {
            if (frames == null || target == null) return;
            var frame = frames.At(Time.time);
            if (frame == shown || frame == null) return;
            shown = frame;
            target.GetPropertyBlock(block);
            block.SetTexture("_BaseMap", frame); // URP Unlit
            block.SetTexture("_MainTex", frame); // Sprites/Default
            target.SetPropertyBlock(block);
        }
    }
}
