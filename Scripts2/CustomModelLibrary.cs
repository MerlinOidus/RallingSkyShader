using System.Collections.Generic;
using UnityEngine;

namespace RTXOn
{
    /// <summary>
    /// The project models a Custom Obstacle can use (Model tab > Select...), besides the themes' Props.
    /// Lives in a Resources folder as "RTXOnCustomModels", so built games have the models too.
    /// Fill it with RTX ON > Add Selected Models As Custom Models.
    /// </summary>
    [CreateAssetMenu(menuName = "RTX ON/Custom Model Library", fileName = ResourceName)]
    public class CustomModelLibrary : ScriptableObject
    {
        public const string ResourceName = "RTXOnCustomModels";

        public List<GameObject> models = new List<GameObject>();

        static CustomModelLibrary instance;
        static bool looked;

        public static CustomModelLibrary Instance
        {
            get
            {
                if (!looked || instance == null)
                {
                    instance = Resources.Load<CustomModelLibrary>(ResourceName);
                    looked = true;
                }
                return instance;
            }
        }

        public GameObject Find(string modelName)
        {
            foreach (var m in models)
                if (m != null && string.Equals(m.name, modelName, System.StringComparison.OrdinalIgnoreCase)) return m;
            return null;
        }
    }
}
