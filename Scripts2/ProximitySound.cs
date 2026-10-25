using UnityEngine;

namespace RTXOn
{
    /// <summary>Plays a sound once, when the ball comes within <see cref="atRows"/> rows of this piece.</summary>
    public class ProximitySound : TrackBehaviour
    {
        public Sfx sound;
        public float atRows = 6f;
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("Custom obstacle's own sound file (base64), played instead of Sound.")]
        public string ownSound;
        public string ownSoundName;
        bool done;

        void Update()
        {
            if (done) return;
            float ahead = Ahead;
            if (ahead > atRows) return;
            done = true;
            var gm = GameManager.Instance;
            if (gm != null && gm.State == GameState.Playing && ahead > -1f)
            {
                if (!string.IsNullOrEmpty(ownSound)) GameAudio.PlaySfxClip(CustomMedia.Sound(ownSound, ownSoundName), volume);
                else GameAudio.Play(sound, volume);
            }
        }
    }
}
