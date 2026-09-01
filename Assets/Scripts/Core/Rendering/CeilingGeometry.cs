using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class CeilingGeometry
    {
        public const float OverhangBeyondWallsUnits = 0.02f;

        public const float ThicknessUnits = 0.018f;

        public static bool TryCompute(IReadOnlyList<Bounds> wallBounds, out Bounds ceiling)
        {
            ceiling = default;
            if (wallBounds == null || wallBounds.Count == 0) return false;
            if (!TryEncapsulateNonDegenerate(wallBounds, out Bounds walls)) return false;

            float sizeX = walls.size.x + OverhangBeyondWallsUnits * 2f;
            float sizeZ = walls.size.z + OverhangBeyondWallsUnits * 2f;
            if (sizeX <= 1e-4f || sizeZ <= 1e-4f) return false;

            float slabCenterY = walls.max.y + ThicknessUnits * 0.5f;
            ceiling = new Bounds(
                new Vector3(walls.center.x, slabCenterY, walls.center.z),
                new Vector3(sizeX, ThicknessUnits, sizeZ));
            return true;
        }

        private static bool IsDegenerate(Bounds b) =>
            b.size.x <= 1e-4f && b.size.y <= 1e-4f && b.size.z <= 1e-4f;

        private static bool TryEncapsulateNonDegenerate(IReadOnlyList<Bounds> parts, out Bounds total)
        {
            total = default;
            bool any = false;
            foreach (var b in parts)
            {
                if (IsDegenerate(b)) continue;
                if (!any) { total = b; any = true; }
                else total.Encapsulate(b);
            }
            return any;
        }
    }
}
