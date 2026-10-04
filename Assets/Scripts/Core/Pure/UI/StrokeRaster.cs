using System;
using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public static class StrokeRaster
    {
        private const float ZeroLengthSquared = 1e-9f;

        public static byte[] Alpha(IReadOnlyList<List<Vector2>> strokes, float gridSize, int pixels, float strokeWidth)
        {
            var alpha = new byte[pixels * pixels];
            float scale = pixels / gridSize;
            float halfWidth = strokeWidth * 0.5f * scale;

            foreach (var stroke in strokes)
            {
                if (stroke.Count == 1) PaintSegment(alpha, pixels, stroke[0] * scale, stroke[0] * scale, halfWidth);
                for (int i = 1; i < stroke.Count; i++)
                    PaintSegment(alpha, pixels, stroke[i - 1] * scale, stroke[i] * scale, halfWidth);
            }
            return alpha;
        }

        private static void PaintSegment(byte[] alpha, int pixels, Vector2 a, Vector2 b, float halfWidth)
        {
            float reach = halfWidth + 1f;
            int x0 = Math.Max(0, (int)Math.Floor(Math.Min(a.x, b.x) - reach));
            int x1 = Math.Min(pixels - 1, (int)Math.Ceiling(Math.Max(a.x, b.x) + reach));
            int y0 = Math.Max(0, (int)Math.Floor(Math.Min(a.y, b.y) - reach));
            int y1 = Math.Min(pixels - 1, (int)Math.Ceiling(Math.Max(a.y, b.y) + reach));

            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float distance = DistanceToSegment(x + 0.5f, y + 0.5f, a, b);
                float coverage = Math.Max(0f, Math.Min(1f, halfWidth + 0.5f - distance));
                byte value = (byte)Math.Round(coverage * 255f);
                int index = y * pixels + x;
                if (value > alpha[index]) alpha[index] = value;
            }
        }

        private static float DistanceToSegment(float px, float py, Vector2 a, Vector2 b)
        {
            float abx = b.x - a.x, aby = b.y - a.y;
            float lengthSquared = abx * abx + aby * aby;
            float t = lengthSquared < ZeroLengthSquared ? 0f : ((px - a.x) * abx + (py - a.y) * aby) / lengthSquared;
            t = Math.Max(0f, Math.Min(1f, t));
            float dx = px - (a.x + abx * t), dy = py - (a.y + aby * t);
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
