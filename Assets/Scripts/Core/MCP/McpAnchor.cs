using UnityEngine;

namespace KitchenDesigner.Core.MCP
{
    internal static class McpAnchor
    {
        public static Vector3 MinCornerOf(KitchenElement el)
        {
            var aabb = McpAabb.Of(el.GetVertices());
            return new Vector3(aabb.minX, aabb.minY, aabb.minZ);
        }

        public static Vector3 MinCornerOffset(KitchenElement el) =>
            MinCornerOf(el) - el.transform.position;

        public static Vector3 MinCornerOffsetAfter(KitchenElement el, Quaternion rotation, Vector3Int dimensions)
        {
            var rotationBefore = el.transform.rotation;
            var dimensionsBefore = el.DimensionsMM;
            if (rotation == rotationBefore && dimensions == dimensionsBefore)
                return MinCornerOffset(el);

            el.transform.rotation = rotation;
            el.DimensionsMM = dimensions;
            var offset = MinCornerOffset(el);
            el.transform.rotation = rotationBefore;
            el.DimensionsMM = dimensionsBefore;
            return offset;
        }

        public static Vector3 PositionForAnchorMm(float? xMm, float? yMm, float? zMm,
            Vector3 minCornerOffset, Vector3 current) => new Vector3(
                xMm.HasValue ? xMm.Value * AppConstants.MM_TO_UNITS - minCornerOffset.x : current.x,
                yMm.HasValue ? yMm.Value * AppConstants.MM_TO_UNITS - minCornerOffset.y : current.y,
                zMm.HasValue ? zMm.Value * AppConstants.MM_TO_UNITS - minCornerOffset.z : current.z);

        public static void PlaceMinCornerAt(KitchenElement el, Vector3 minCornerWorld) =>
            el.transform.position += minCornerWorld - MinCornerOf(el);

        public static Vector3 FromMm(float xMm, float yMm, float zMm) =>
            new Vector3(xMm, yMm, zMm) * AppConstants.MM_TO_UNITS;

        public static float FromMm(float mm) => mm * AppConstants.MM_TO_UNITS;

        public static float ToMm(float worldUnits) => worldUnits / AppConstants.MM_TO_UNITS;

        public static AabbMmInfo ToMmBox(AabbInfo box) => new AabbMmInfo
        {
            minXMm = ToMm(box.minX), minYMm = ToMm(box.minY), minZMm = ToMm(box.minZ),
            maxXMm = ToMm(box.maxX), maxYMm = ToMm(box.maxY), maxZMm = ToMm(box.maxZ)
        };
    }
}
