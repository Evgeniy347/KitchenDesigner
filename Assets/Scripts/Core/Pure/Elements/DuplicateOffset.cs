using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class DuplicateOffset
    {
        public static Vector3 ForViewDirection(Vector3 viewDirection, float distance)
        {
            float alongX = Mathf.Abs(viewDirection.x);
            float alongY = Mathf.Abs(viewDirection.y);
            float alongZ = Mathf.Abs(viewDirection.z);

            if (!(alongX > 0f) && !(alongY > 0f) && !(alongZ > 0f))
                return new Vector3(distance, 0f, 0f);

            if (alongX >= alongY && alongX >= alongZ)
                return new Vector3(TowardsViewer(viewDirection.x, distance), 0f, 0f);
            if (alongY >= alongZ)
                return new Vector3(0f, TowardsViewer(viewDirection.y, distance), 0f);
            return new Vector3(0f, 0f, TowardsViewer(viewDirection.z, distance));
        }

        private static float TowardsViewer(float component, float distance) =>
            component > 0f ? -distance : distance;
    }
}
