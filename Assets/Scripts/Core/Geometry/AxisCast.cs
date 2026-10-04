using UnityEngine;

namespace KitchenDesigner.Core
{
    public readonly struct AxisCast
    {
        public readonly Vector3 Origin;
        public readonly int Axis;
        public readonly int Sign;
        public readonly float Start;
        public readonly bool HasNear;
        public readonly AxisHit Near;
        public readonly bool HasFar;
        public readonly AxisHit Far;

        public AxisCast(Vector3 origin, int axis, int sign, float start,
            bool hasNear, AxisHit near, bool hasFar, AxisHit far)
        {
            Origin = origin;
            Axis = axis;
            Sign = sign;
            Start = start;
            HasNear = hasNear;
            Near = near;
            HasFar = hasFar;
            Far = far;
        }

        public Vector3 Direction
        {
            get
            {
                var d = Vector3.zero;
                d[Axis] = Sign;
                return d;
            }
        }

        public float NearGap => Near.Enter - Start;

        public float FarGap => Far.Enter - Near.Exit;

        public Vector3 PointAt(float t) => Origin + Direction * t;
    }
}
