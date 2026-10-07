using UnityEngine;

namespace KitchenDesigner.Core.MCP
{
    internal static class McpAnchor
    {
        public static Vector3 RefPointOf(KitchenElement el, McpReference reference)
        {
            var aabb = McpAabb.Of(el.GetVertices());
            return reference.PointOf(
                new Vector3(aabb.minX, aabb.minY, aabb.minZ),
                new Vector3(aabb.maxX, aabb.maxY, aabb.maxZ));
        }

        public static Vector3 RefOffset(KitchenElement el, McpReference reference) =>
            RefPointOf(el, reference) - el.transform.position;

        public static Vector3 RefOffsetAfter(KitchenElement el, Quaternion rotation, Vector3Int dimensions,
            McpReference reference)
        {
            var rotationBefore = el.transform.rotation;
            var dimensionsBefore = el.DimensionsMM;
            if (rotation == rotationBefore && dimensions == dimensionsBefore)
                return RefOffset(el, reference);

            el.transform.rotation = rotation;
            el.DimensionsMM = dimensions;
            var offset = RefOffset(el, reference);
            el.transform.rotation = rotationBefore;
            el.DimensionsMM = dimensionsBefore;
            return offset;
        }

        public static Vector3 PositionForAnchorMm(float? xMm, float? yMm, float? zMm,
            Vector3 refOffset, Vector3 current) => new Vector3(
                xMm.HasValue ? xMm.Value * AppConstants.MM_TO_UNITS - refOffset.x : current.x,
                yMm.HasValue ? yMm.Value * AppConstants.MM_TO_UNITS - refOffset.y : current.y,
                zMm.HasValue ? zMm.Value * AppConstants.MM_TO_UNITS - refOffset.z : current.z);

        public static void PlaceRefPointAt(KitchenElement el, McpReference reference, Vector3 refPointWorld) =>
            el.transform.position += refPointWorld - RefPointOf(el, reference);

        public static Vector3 FromMm(float xMm, float yMm, float zMm) =>
            new Vector3(xMm, yMm, zMm) * AppConstants.MM_TO_UNITS;

        public static float FromMm(float mm) => mm * AppConstants.MM_TO_UNITS;

        public static float ToMm(float worldUnits) => worldUnits / AppConstants.MM_TO_UNITS;

        public static AabbMmInfo ToMmBox(AabbInfo box) => new AabbMmInfo
        {
            minXMm = ToMm(box.minX), minYMm = ToMm(box.minY), minZMm = ToMm(box.minZ),
            maxXMm = ToMm(box.maxX), maxYMm = ToMm(box.maxY), maxZMm = ToMm(box.maxZ)
        };

        public static float[] MmTriple(Vector3 mm) => new[] { mm.x, mm.y, mm.z };

        public static BoxMm ToMmBoxStruct(AabbInfo box) => new BoxMm(
            new Vector3(ToMm(box.minX), ToMm(box.minY), ToMm(box.minZ)),
            new Vector3(ToMm(box.maxX), ToMm(box.maxY), ToMm(box.maxZ)));
    }
}
