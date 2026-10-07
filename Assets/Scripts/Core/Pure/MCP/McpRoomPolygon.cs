namespace KitchenDesigner.Core.MCP
{
    public static class McpRoomPolygon
    {
        public const int MinCoordinates = 6;

        public static bool Contains(int[]? polygonXZ, float xMm, float zMm)
        {
            if (polygonXZ == null || polygonXZ.Length < MinCoordinates || polygonXZ.Length % 2 != 0) return false;
            bool inside = false;
            int count = polygonXZ.Length / 2;
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                float xi = polygonXZ[i * 2], zi = polygonXZ[i * 2 + 1];
                float xj = polygonXZ[j * 2], zj = polygonXZ[j * 2 + 1];
                bool crosses = (zi > zMm) != (zj > zMm) &&
                    xMm < (xj - xi) * (zMm - zi) / (zj - zi) + xi;
                if (crosses) inside = !inside;
            }
            return inside;
        }
    }
}
