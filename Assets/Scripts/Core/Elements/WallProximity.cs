using UnityEngine;

namespace KitchenDesigner.Core
{
    internal static class WallProximity
    {
        public static Wall? Nearest(KitchenElement self)
        {
            if (self == null) return null;

            float bestDistance = float.MaxValue;
            Wall? best = null;

            foreach (var el in PartRegistry.All)
            {
                if (el == null || el == self) continue;
                var wall = el.GetComponent<Wall>();
                if (wall == null) continue;

                float distance = DistanceTo(wall, self.transform.position);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = wall;
            }

            return best;
        }

        public static float DistanceTo(Wall wall, Vector3 point)
        {
            var t = wall.transform;
            var centre = wall.FullPosition;
            var half = t.localScale;
            half.y = wall.FullScaleY;
            half = new Vector3(Mathf.Abs(half.x), Mathf.Abs(half.y), Mathf.Abs(half.z)) * 0.5f;

            var local = Quaternion.Inverse(t.rotation) * (point - centre);
            local.x = Mathf.Clamp(local.x, -half.x, half.x);
            local.y = Mathf.Clamp(local.y, -half.y, half.y);
            local.z = Mathf.Clamp(local.z, -half.z, half.z);
            return (centre + t.rotation * local - point).magnitude;
        }

        public static bool IsThinAlongX(Wall wall)
        {
            var dims = DimensionsOf(wall);
            return dims.x <= dims.z;
        }

        public static Vector3 FaceNormal(Wall wall)
            => IsThinAlongX(wall) ? wall.transform.right : wall.transform.forward;

        public static float HalfThicknessUnits(Wall wall)
        {
            var dims = DimensionsOf(wall);
            return Mathf.Min(dims.x, dims.z) * 0.5f * AppConstants.MM_TO_UNITS;
        }

        private static Vector3Int DimensionsOf(Wall wall)
        {
            var el = wall.GetComponent<KitchenElement>();
            return el != null ? el.DimensionsMM : Vector3Int.one;
        }
    }
}
