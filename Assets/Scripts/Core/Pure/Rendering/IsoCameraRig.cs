using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class IsoCameraRig
    {
        public const float MinDistance = 0.5f;

        public static readonly Vector3 IsoDir = new Vector3(0.5f, 0.5f, -0.866f).normalized;

        public static Vector3 Position(Vector3 center, Vector3 size, float distanceScale) =>
            Position(center, size, distanceScale, MinDistance);

        public static Vector3 Position(Vector3 center, Vector3 size, float distanceScale,
            float minDistance)
        {
            float maxDim = Mathf.Max(size.x, size.y, size.z);
            float distance = Mathf.Max(maxDim * distanceScale, minDistance);
            return center + IsoDir * distance;
        }

        public static float Distance(Vector3 size, float distanceScale, float minDistance) =>
            Mathf.Max(Mathf.Max(size.x, size.y, size.z) * distanceScale, minDistance);
    }
}
