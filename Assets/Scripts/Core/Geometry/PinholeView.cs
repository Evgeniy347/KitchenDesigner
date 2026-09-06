using UnityEngine;

namespace KitchenDesigner.Core.Handles
{
    public readonly struct PinholeView
    {
        public readonly Vector3 Position;
        public readonly Vector3 Forward;
        public readonly Vector3 Right;
        public readonly Vector3 Up;
        public readonly float FieldOfView;
        public readonly int PixelWidth;
        public readonly int PixelHeight;
        public readonly bool Orthographic;
        public readonly float OrthographicSize;

        public PinholeView(Vector3 position, Vector3 forward, Vector3 right, Vector3 up,
            float fieldOfView, int pixelWidth, int pixelHeight,
            bool orthographic = false, float orthographicSize = 1f)
        {
            Position = position;
            Forward = forward;
            Right = right;
            Up = up;
            FieldOfView = fieldOfView;
            PixelWidth = pixelWidth;
            PixelHeight = pixelHeight;
            Orthographic = orthographic;
            OrthographicSize = orthographicSize;
        }

        public static PinholeView Perspective(Vector3 position, float fieldOfView,
            int pixelWidth, int pixelHeight) =>
            new PinholeView(position, Vector3.forward, Vector3.right, Vector3.up,
                fieldOfView, pixelWidth, pixelHeight);

        public float DepthOf(Vector3 world) => Vector3.Dot(world - Position, Forward);

        public float PixelsPerRadian => SafeHeight * 0.5f
            / Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad);

        private float SafeHeight => Mathf.Max(1, PixelHeight);

        public Vector3 WorldToScreen(Vector3 world)
        {
            Vector3 d = world - Position;
            float depth = Vector3.Dot(d, Forward);
            float x = Vector3.Dot(d, Right);
            float y = Vector3.Dot(d, Up);

            if (Orthographic)
            {
                float perUnit = SafeHeight * 0.5f
                    / Mathf.Max(OrthographicSize, Tolerance.EpsilonUnits);
                return new Vector3(PixelWidth * 0.5f + x * perUnit,
                    SafeHeight * 0.5f + y * perUnit, depth);
            }

            float safeDepth = Mathf.Abs(depth) < Tolerance.EpsilonUnits
                ? Mathf.Sign(depth == 0f ? 1f : depth) * Tolerance.EpsilonUnits
                : depth;
            float focal = PixelsPerRadian;
            return new Vector3(PixelWidth * 0.5f + x / safeDepth * focal,
                SafeHeight * 0.5f + y / safeDepth * focal, depth);
        }

        public float WorldSizeForPixels(Vector3 worldPoint, float pixels)
        {
            if (Orthographic)
                return pixels * (2f * OrthographicSize / SafeHeight);

            float depth = Mathf.Max(DepthOf(worldPoint), Tolerance.EpsilonUnits);
            return pixels * depth / PixelsPerRadian;
        }

        public float PixelsForWorldSize(Vector3 worldPoint, float worldSize)
        {
            float perPixel = WorldSizeForPixels(worldPoint, 1f);
            return perPixel < Tolerance.EpsilonSqr ? 0f : worldSize / perPixel;
        }
    }
}
