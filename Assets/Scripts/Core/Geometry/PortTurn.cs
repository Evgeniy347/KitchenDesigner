using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class PortTurn
    {
        public static void TurnOnto(Vector3 from, Vector3 to, out Vector3 axis, out float degrees)
        {
            Vector3 a = from.normalized;
            Vector3 b = to.normalized;
            float dot = Mathf.Clamp(Vector3.Dot(a, b), -1f, 1f);

            if (dot >= Tolerance.ParallelDot)
            {
                axis = Vector3.up;
                degrees = 0f;
                return;
            }

            if (dot <= -Tolerance.ParallelDot)
            {
                axis = PerpendicularTo(a);
                degrees = 180f;
                return;
            }

            axis = Vector3.Cross(a, b).normalized;
            degrees = Mathf.Acos(dot) * Mathf.Rad2Deg;
        }

        public static Vector3 Rotate90(Vector3 v, Vector3 axis) =>
            Vector3.Cross(axis, v) + axis * Vector3.Dot(axis, v);

        public static Vector3 RotateOnto(Vector3 v, Vector3 from, Vector3 to)
        {
            TurnOnto(from, to, out Vector3 axis, out float degrees);
            int steps = Mathf.RoundToInt(degrees / 90f);
            for (int i = 0; i < steps; i++) v = Rotate90(v, axis);
            return v;
        }

        public static Vector3 RotateWithTwist(Vector3 v, Vector3 from, Vector3 to, int twistSteps)
        {
            Vector3 rotated = RotateOnto(v, from, to);
            int k = ((twistSteps % 4) + 4) % 4;
            for (int i = 0; i < k; i++) rotated = Rotate90(rotated, to);
            return rotated;
        }

        private static Vector3 PerpendicularTo(Vector3 direction)
        {
            Vector3 upright = Vector3.up - Vector3.Dot(direction, Vector3.up) * direction;
            if (upright.sqrMagnitude > Tolerance.EpsilonSqr) return upright.normalized;

            Vector3 sideways = Vector3.right - Vector3.Dot(direction, Vector3.right) * direction;
            return sideways.normalized;
        }
    }
}
