using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// A theme model with Code Motion off (Model Adjust): stays at its built place on the tile, so only its own
    /// Blender animation moves it, while the obstacle's code still moves the danger (collider) as usual.
    /// </summary>
    public class PinnedModel : MonoBehaviour
    {
        Transform tile;
        Vector3 localPos;
        Quaternion localRot;

        public void Pin(Transform tileTransform)
        {
            tile = tileTransform;
            localPos = tile.InverseTransformPoint(transform.position);
            localRot = Quaternion.Inverse(tile.rotation) * transform.rotation;
        }

        void LateUpdate()
        {
            if (tile == null) return;
            transform.SetPositionAndRotation(tile.TransformPoint(localPos), tile.rotation * localRot);
        }
    }
}
