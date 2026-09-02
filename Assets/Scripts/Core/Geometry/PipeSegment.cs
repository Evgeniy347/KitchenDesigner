using UnityEngine;

namespace KitchenDesigner.Core
{
    public readonly struct PipeSegment
    {
        public readonly Vector3 FromMM;
        public readonly Vector3 ToMM;
        public readonly float FromRadiusMM;
        public readonly float ToRadiusMM;

        public PipeSegment(Vector3 fromMM, Vector3 toMM, float radiusMM)
            : this(fromMM, toMM, radiusMM, radiusMM) { }

        public PipeSegment(Vector3 fromMM, Vector3 toMM, float fromRadiusMM, float toRadiusMM)
        {
            FromMM = fromMM;
            ToMM = toMM;
            FromRadiusMM = fromRadiusMM;
            ToRadiusMM = toRadiusMM;
        }

        public Vector3 AxisMM => ToMM - FromMM;

        public float LengthMM => AxisMM.magnitude;

        public bool IsDegenerate => LengthMM <= Tolerance.EpsilonUnits;

        public Vector3 Direction
        {
            get
            {
                var axis = AxisMM;
                float length = axis.magnitude;
                return length > Tolerance.EpsilonUnits ? axis / length : Vector3.up;
            }
        }
    }
}
