using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class MmGrid
    {
        public const float EpsMm = MmGridMath.EpsMm;

        public static float RoundMm(float mm) => MmGridMath.RoundMm(mm);

        public static bool IsAxisAligned(Quaternion rot)
        {
            var m = Matrix4x4.Rotate(rot);
            for (int col = 0; col < 3; col++)
            {
                var axis = m.GetColumn(col);
                float max = Mathf.Max(Mathf.Abs(axis.x), Mathf.Max(Mathf.Abs(axis.y), Mathf.Abs(axis.z)));
                if (max < 0.9999f) return false;
            }
            return true;
        }

        public static bool TryMinCornerMm(KitchenElement element, out Vector3 minCornerMm)
        {
            minCornerMm = Vector3.zero;
            if (element == null) return false;

            if (!element.PoseFollowsTransform) return false;
            if (!IsAxisAligned(element.transform.rotation)) return false;
            if (element.GetComponent<ISnapPorts>() != null) return false;

            var wall = element.GetComponent<Wall>();
            if (wall != null && wall.IsLowered) return false;

            var verts = element.GetVertices();
            if (verts == null || verts.Length == 0) return false;

            Vector3 min = verts[0];
            foreach (var v in verts) min = Vector3.Min(min, v);

            minCornerMm = min * (1f / AppConstants.MM_TO_UNITS);
            return true;
        }

        public static Vector3 OffsetToGrid(KitchenElement element)
        {
            if (!TryMinCornerMm(element, out var minCornerMm)) return Vector3.zero;
            if (!MmGridMath.TryMeasureOffGrid(minCornerMm, MmGridMath.EpsMm, out var shiftsMm))
                return Vector3.zero;
            return shiftsMm * AppConstants.MM_TO_UNITS;
        }

        public static bool Snap(KitchenElement element)
        {
            var offset = OffsetToGrid(element);
            if (offset == Vector3.zero) return false;
            element.transform.position += offset;
            return true;
        }

        public static Vector3 SnapPosition(KitchenElement element, Vector3 desired)
        {
            if (element == null) return desired;
            var prev = element.transform.position;
            element.transform.position = desired;
            var offset = OffsetToGrid(element);
            element.transform.position = prev;
            return desired + offset;
        }
    }
}
