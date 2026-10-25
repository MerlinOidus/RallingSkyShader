using UnityEngine;

namespace RTXOn
{
    /// <summary>Touching any trigger collider under this kills the ball (unless not lethal).</summary>
    public class Hazard : MonoBehaviour
    {
        public bool lethal = true;
        [Tooltip("Cannot hurt the ball while it flies from a jump pad. In the original this is true for risers, binary blocks, pillars, spotlights and lasers.")]
        public bool jumpSafe;
    }
}
