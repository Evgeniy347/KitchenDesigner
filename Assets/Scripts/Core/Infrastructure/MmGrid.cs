using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class MmGrid
    {
        public const float EpsMm = 0.01f;

        public static float RoundMm(float mm) => Mathf.Floor(mm + 0.5f + EpsMm);

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

        public static Vector3 OffsetToGrid(KitchenElement element)
        {
            if (element == null) return Vector3.zero;

            if (!element.PoseFollowsTransform) return Vector3.zero;
            if (!IsAxisAligned(element.transform.rotation)) return Vector3.zero;
            if (element.GetComponent<ISnapPorts>() != null) return Vector3.zero;

            var wall = element.GetComponent<Wall>();
            if (wall != null && wall.IsLowered) return Vector3.zero;

            var verts = element.GetVertices();
            if (verts == null || verts.Length == 0) return Vector3.zero;

            Vector3 min = verts[0];
            foreach (var v in verts) min = Vector3.Min(min, v);

            float toMm = 1f / AppConstants.MM_TO_UNITS;
            var offset = Vector3.zero;
            bool any = false;
            for (int axis = 0; axis < 3; axis++)
            {
                float mm = min[axis] * toMm;
                float deltaMm = RoundMm(mm) - mm;
                if (Mathf.Abs(deltaMm) <= EpsMm) continue;
                offset[axis] = deltaMm * AppConstants.MM_TO_UNITS;
                any = true;
            }
            return any ? offset : Vector3.zero;
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
