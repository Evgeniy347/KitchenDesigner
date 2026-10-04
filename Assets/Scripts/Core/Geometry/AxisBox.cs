using UnityEngine;

namespace KitchenDesigner.Core
{
    public readonly struct AxisBox
    {
        public readonly int Id;
        public readonly Vector3 Centre;
        public readonly Vector3 AxisA;
        public readonly Vector3 AxisB;
        public readonly Vector3 AxisC;
        public readonly Vector3 Half;
        public readonly Vector3 Min;
        public readonly Vector3 Max;

        public AxisBox(int id, Vector3 centre, Vector3 axisA, Vector3 axisB, Vector3 axisC,
            Vector3 half)
        {
            Id = id;
            Centre = centre;
            AxisA = axisA;
            AxisB = axisB;
            AxisC = axisC;
            Half = half;
            var reach = new Vector3(
                Reach(axisA.x, axisB.x, axisC.x, half),
                Reach(axisA.y, axisB.y, axisC.y, half),
                Reach(axisA.z, axisB.z, axisC.z, half));
            Min = centre - reach;
            Max = centre + reach;
        }

        public static AxisBox Aligned(int id, Vector3 min, Vector3 max) =>
            new AxisBox(id, (min + max) * 0.5f, Vector3.right, Vector3.up, Vector3.forward,
                (max - min) * 0.5f);

        public static AxisBox Of(in ElementGeometry geometry)
        {
            var faces = geometry.Faces;
            if (faces == null || faces.Length != Face.BoxFaceCount)
                return Aligned(geometry.Id, geometry.Min, geometry.Max);

            var centre = Vector3.zero;
            for (int i = 0; i < Face.BoxFaceCount; i++) centre += faces[i].center;
            centre /= Face.BoxFaceCount;

            var a = faces[0].normal.normalized;
            var b = faces[2].normal.normalized;
            var c = faces[4].normal.normalized;
            var half = new Vector3(
                Mathf.Abs(Vector3.Dot(faces[0].center - centre, a)),
                Mathf.Abs(Vector3.Dot(faces[2].center - centre, b)),
                Mathf.Abs(Vector3.Dot(faces[4].center - centre, c)));
            return new AxisBox(geometry.Id, centre, a, b, c, half);
        }

        public AxisBox Moved(Vector3 delta) =>
            new AxisBox(Id, Centre + delta, AxisA, AxisB, AxisC, Half);

        public Vector3 Axis(int k) => k == 0 ? AxisA : k == 1 ? AxisB : AxisC;

        public bool Spans(Vector3 origin, int worldAxis, out float enter, out float exit)
        {
            enter = float.NegativeInfinity;
            exit = float.PositiveInfinity;
            var offset = origin - Centre;

            for (int k = 0; k < 3; k++)
            {
                var axis = Axis(k);
                float half = Half[k];
                float along = axis[worldAxis];
                float from = Vector3.Dot(offset, axis);

                if (Mathf.Abs(along) < Tolerance.EpsilonSqr)
                {
                    if (Mathf.Abs(from) > half) return false;
                    continue;
                }

                float t1 = (-half - from) / along;
                float t2 = (half - from) / along;
                if (t1 > t2) (t1, t2) = (t2, t1);
                if (t1 > enter) enter = t1;
                if (t2 < exit) exit = t2;
                if (enter > exit) return false;
            }
            return true;
        }

        private static float Reach(float a, float b, float c, Vector3 half) =>
            Mathf.Abs(a) * half.x + Mathf.Abs(b) * half.y + Mathf.Abs(c) * half.z;
    }
}
