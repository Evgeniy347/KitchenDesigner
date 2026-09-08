using UnityEngine;

namespace KitchenDesigner.Core
{
    public readonly struct SnapCursor
    {
        public readonly bool Present;

        public readonly Vector3 Origin;

        public readonly Vector3 Direction;

        private SnapCursor(Vector3 origin, Vector3 direction)
        {
            bool aimed = direction.sqrMagnitude > Tolerance.EpsilonSqr;
            Present = aimed;
            Origin = origin;
            Direction = aimed ? direction.normalized : Vector3.zero;
        }

        public static SnapCursor None => default;

        public static SnapCursor AlongRay(Vector3 origin, Vector3 direction) =>
            new SnapCursor(origin, direction);

        public float DistanceTo(Vector3 point)
        {
            if (!Present) return -1f;

            Vector3 toPoint = point - Origin;
            float along = Vector3.Dot(toPoint, Direction);
            return along <= 0f
                ? toPoint.magnitude
                : (toPoint - along * Direction).magnitude;
        }
    }
}
