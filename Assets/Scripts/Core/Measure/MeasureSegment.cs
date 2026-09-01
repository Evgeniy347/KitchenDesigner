using UnityEngine;

namespace KitchenDesigner.Core.Measure
{
    public class MeasureSegment
    {
        public Vector3 A { get; }
        public Vector3 B { get; }

        public MeasureSegment(Vector3 a, Vector3 b)
        {
            A = a;
            B = b;
        }

        public float LengthMm => MeasureGeometry.ToMm((B - A).magnitude);

        public int Axis => MeasureGeometry.AxisOf(A, B);

        public Vector3 Middle => (A + B) * 0.5f;
    }
}
