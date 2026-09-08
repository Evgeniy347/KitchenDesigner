using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class IsoCameraRig
    {
        public const float MinDistance = 0.5f;

        public static readonly Vector3 IsoDir = new Vector3(0.5f, 0.5f, -0.866f).normalized;

        public static Vector3 Position(Vector3 center, Vector3 size, float distanceScale)
        {
            float maxDim = Mathf.Max(size.x, size.y, size.z);
            float distance = Mathf.Max(maxDim * distanceScale, MinDistance);
            return center + IsoDir * distance;
        }
    }
}
