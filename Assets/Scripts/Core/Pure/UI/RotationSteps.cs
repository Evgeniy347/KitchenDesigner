using System;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public static class RotationSteps
    {
        public const float MatchToleranceDeg = 0.2f;
        public const int DisplayDecimals = 1;

        public static float Normalize(float degrees)
        {
            if (float.IsNaN(degrees) || float.IsInfinity(degrees)) return 0f;
            var rounded = (float)Math.Round(degrees, DisplayDecimals, MidpointRounding.AwayFromZero);
            var wrapped = rounded % 360f;
            if (wrapped < 0f) wrapped += 360f;
            if (wrapped == 0f) return 0f;
            return wrapped;
        }

        public static Vector3 Normalize(Vector3 euler) =>
            new Vector3(Normalize(euler.x), Normalize(euler.y), Normalize(euler.z));

        public static Vector3 Step(Vector3 euler, RotationAxis axis, float deltaDeg)
        {
            var stepped = Normalize(euler);
            switch (axis)
            {
                case RotationAxis.X: stepped.x = Normalize(stepped.x + deltaDeg); break;
                case RotationAxis.Y: stepped.y = Normalize(stepped.y + deltaDeg); break;
                case RotationAxis.Z: stepped.z = Normalize(stepped.z + deltaDeg); break;
                default: throw new ArgumentOutOfRangeException(nameof(axis), axis, null);
            }
            return stepped;
        }
    }
}
