using UnityEngine;

namespace KitchenDesigner.Core.Handles
{
    public readonly struct HandleScreenExtent
    {
        public readonly Vector2 Root;
        public readonly Vector2 Tip;
        public readonly Vector2 Grab;
        public readonly float HalfWidthPixels;
        public readonly bool InFront;

        private HandleScreenExtent(Vector2 root, Vector2 tip, Vector2 grab,
            float halfWidthPixels, bool inFront)
        {
            Root = root;
            Tip = tip;
            Grab = grab;
            HalfWidthPixels = halfWidthPixels;
            InFront = inFront;
        }

        public float LengthPixels => (Tip - Root).magnitude;

        public static HandleScreenExtent Measure(in PinholeView view, Vector3 basePoint,
            Vector3 normal, in HandleMetrics metrics, float scale)
        {
            Vector3 n = normal.sqrMagnitude > Tolerance.EpsilonSqr
                ? normal.normalized : Vector3.forward;

            Vector3 rootWorld = basePoint + n * (metrics.Gap * scale);
            Vector3 tipWorld = basePoint + n * (metrics.ArrowLen * scale);
            Vector3 grabWorld = basePoint + n * (metrics.GrabCenterZ * scale);

            Vector3 root = view.WorldToScreen(rootWorld);
            Vector3 tip = view.WorldToScreen(tipWorld);
            Vector3 grab = view.WorldToScreen(grabWorld);

            bool inFront = root.z > 0f && tip.z > 0f && grab.z > 0f;
            float halfWidth = view.PixelsForWorldSize(grabWorld, metrics.MaxRadius * scale);

            return new HandleScreenExtent(new Vector2(root.x, root.y),
                new Vector2(tip.x, tip.y), new Vector2(grab.x, grab.y), halfWidth, inFront);
        }

        public Vector2 SilhouettePoint(float along, float across)
        {
            Vector2 axis = Tip - Root;
            Vector2 perpendicular = axis.sqrMagnitude > Tolerance.EpsilonSqr
                ? new Vector2(-axis.y, axis.x).normalized
                : Vector2.up;
            return Root + axis * along + perpendicular * (HalfWidthPixels * across);
        }

        public float DistanceToGrabPixels(Vector2 screenPoint) => (screenPoint - Grab).magnitude;

        public float DistanceToSilhouettePixels(Vector2 screenPoint) =>
            Mathf.Max(0f, ScreenDistance.PointToSegmentPixels(Root, Tip, screenPoint)
                          - HalfWidthPixels);

        public bool PickedBy(Vector2 screenPoint, float radiusPixels) =>
            InFront && DistanceToGrabPixels(screenPoint) <= radiusPixels;

        public float GrabRadiusToArrowLength(float radiusPixels)
        {
            float length = LengthPixels;
            return length < Tolerance.EpsilonUnits ? float.PositiveInfinity : radiusPixels / length;
        }
    }
}
