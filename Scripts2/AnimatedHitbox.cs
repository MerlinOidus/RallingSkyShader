using System.Collections.Generic;
using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// A deadly hitbox that moves with a model's animation: a box on every mesh part (it rides its part's animated
    /// transform), and for skinned meshes a box fitted to the bent mesh every frame. The Hazard sits on the model's
    /// holder, found by the ball through GetComponentInParent.
    /// </summary>
    public class AnimatedHitbox : MonoBehaviour
    {
        const float Shrink = 0.9f; // a little smaller than the mesh, like the built-in blocks

        SkinnedMeshRenderer skin;
        BoxCollider box;

        /// <summary>Puts the boxes on <paramref name="model"/>'s parts and the Hazard on <paramref name="holder"/>.</summary>
        public static void Add(GameObject model, GameObject holder, bool jumpSafe)
        {
            if (model == null) return;
            var filters = new List<MeshFilter>();
            model.GetComponentsInChildren(true, filters);
            foreach (var f in filters)
            {
                var mesh = f.sharedMesh;
                if (mesh == null || f.GetComponent<Renderer>() == null) continue;
                var bc = f.gameObject.AddComponent<BoxCollider>();
                bc.isTrigger = true;
                bc.center = mesh.bounds.center;
                bc.size = mesh.bounds.size * Shrink;
            }
            var skins = new List<SkinnedMeshRenderer>();
            model.GetComponentsInChildren(true, skins);
            foreach (var s in skins)
            {
                s.updateWhenOffscreen = true; // its bounds follow the bones every frame
                var go = new GameObject("Skinned Hitbox");
                go.transform.SetParent(holder.transform, false);
                var hb = go.AddComponent<AnimatedHitbox>();
                hb.skin = s;
                hb.box = go.AddComponent<BoxCollider>();
                hb.box.isTrigger = true;
                hb.LateUpdate();
            }
            var hazard = holder.GetComponent<Hazard>();
            if (hazard == null) hazard = holder.AddComponent<Hazard>();
            hazard.jumpSafe = jumpSafe;
        }

        void LateUpdate()
        {
            if (skin == null || box == null) return;
            var b = skin.bounds; // world space, axis-aligned
            transform.SetPositionAndRotation(b.center, Quaternion.identity);
            var s = transform.lossyScale;
            box.center = Vector3.zero;
            box.size = new Vector3(b.size.x / Mathf.Max(0.0001f, Mathf.Abs(s.x)), b.size.y / Mathf.Max(0.0001f, Mathf.Abs(s.y)),
                                   b.size.z / Mathf.Max(0.0001f, Mathf.Abs(s.z))) * Shrink;
        }
    }
}
