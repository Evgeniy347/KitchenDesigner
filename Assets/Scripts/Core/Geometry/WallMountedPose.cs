using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class WallMountedPose
    {
        public static Vector3 OutwardNormal(Vector3 wallNormal, Vector3 wallCentre,
            Vector3 elementPosition)
        {
            var flat = new Vector3(wallNormal.x, 0f, wallNormal.z);
            if (flat.sqrMagnitude < Tolerance.EpsilonSqr) return Vector3.forward;
            flat = flat.normalized;

            var offset = elementPosition - wallCentre;
            offset.y = 0f;
            return Vector3.Dot(offset, flat) < 0f ? -flat : flat;
        }

        public static Vector3 SeatedPosition(Vector3 elementPosition, Vector3 wallCentre,
            Vector3 outwardNormal, float standoff)
        {
            var offset = elementPosition - wallCentre;
            offset.y = 0f;
            float along = Vector3.Dot(offset, outwardNormal);
            return elementPosition + outwardNormal * (standoff - along);
        }

        public static float YawDegrees(Vector3 outwardNormal)
            => Mathf.Atan2(outwardNormal.x, outwardNormal.z) * Mathf.Rad2Deg;
    }
}
