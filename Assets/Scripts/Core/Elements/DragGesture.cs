using UnityEngine;

namespace KitchenDesigner.Core
{
    public enum DragAxisLock { None, X, Z }

    public static class DragGesture
    {
        public const float DragStartPixels = 6f;
        public const float DragStartSeconds = 0.15f;

        public static readonly Color AllowedTint = new Color(0f, 1f, 0f, 0.3f);
        public static readonly Color BlockedTint = new Color(1f, 0f, 0f, 0.3f);

        public static bool PressBecameDrag(Vector2 pressMouse, float pressTime,
            Vector2 mouseNow, float timeNow)
        {
            if (timeNow - pressTime < DragStartSeconds) return false;
            return (mouseNow - pressMouse).magnitude > DragStartPixels;
        }

        public static bool SnapAppliesTo(bool globalSnapEnabled, bool ctrlHeld)
            => globalSnapEnabled ^ ctrlHeld;

        public static DragAxisLock Toggle(DragAxisLock current, DragAxisLock pressed)
            => current == pressed ? DragAxisLock.None : pressed;

        public static Vector3 ApplyAxisLock(Vector3 wanted, DragAxisLock axisLock,
            Vector3 dragStart, float heldY, bool keepsItsOwnHeight)
        {
            if (axisLock == DragAxisLock.None) return wanted;
            var result = wanted;
            if (axisLock == DragAxisLock.X) result.z = dragStart.z;
            else result.x = dragStart.x;
            if (keepsItsOwnHeight) result.y = heldY;
            return result;
        }

        public static bool GhostIsWorthShowing(bool snapped, Vector3 snappedPosition, Vector3 freePosition)
            => snapped && (snappedPosition - freePosition).sqrMagnitude > Tolerance.EpsilonSqr;

        public static Color TintFor(bool violates) => violates ? BlockedTint : AllowedTint;
    }
}
