using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class ThumbnailFrame
    {
        public const int MinPixels = 1;

        public static float Aspect(int widthPx, int heightPx)
        {
            if (widthPx <= 0 || heightPx <= 0) return 1f;
            return widthPx / (float)heightPx;
        }

        public static Vector2Int SizeForTile(float tileWidth, float tileHeight, int heightPx)
        {
            int height = Mathf.Max(MinPixels, heightPx);
            if (tileWidth <= 0f || tileHeight <= 0f) return new Vector2Int(height, height);

            int width = Mathf.Max(MinPixels, Mathf.RoundToInt(height * (tileWidth / tileHeight)));
            return new Vector2Int(width, height);
        }
    }
}
