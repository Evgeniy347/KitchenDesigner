using UnityEngine;

namespace KitchenDesigner.Core
{
    internal static class ApplianceCollider
    {
        public static void FitBox(GameObject go, Vector3 sizeMM, Vector3 centerMM)
        {
            var existing = go.GetComponent<Collider>();
            if (existing != null && !(existing is BoxCollider))
                Object.DestroyImmediate(existing);
            var box = go.GetComponent<BoxCollider>();
            if (box == null) box = go.AddComponent<BoxCollider>();
            float toU = AppConstants.MM_TO_UNITS;
            box.size = sizeMM * toU;
            box.center = centerMM * toU;
        }
    }
}
