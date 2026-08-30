using UnityEngine;

namespace KitchenDesigner.Core
{
    public readonly struct PartPlane
    {
        public const float MIN_UPNESS = 0.9f;

        public readonly KitchenElement Part;
        public readonly int UpAxis;
        public readonly float UpSign;
        public readonly int AxisA;
        public readonly int AxisB;

        private PartPlane(KitchenElement part, int upAxis, float upSign, int axisA, int axisB)
        {
            Part = part;
            UpAxis = upAxis;
            UpSign = upSign;
            AxisA = axisA;
            AxisB = axisB;
        }

        public static Vector3 AxisVector(int axis) =>
            axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;

        public static PartPlane Of(KitchenElement part)
        {
            var rot = part.transform.rotation;
            int best = 2;
            float bestDot = 0f;
            for (int axis = 0; axis < 3; axis++)
            {
                float dot = Vector3.Dot(rot * AxisVector(axis), Vector3.up);
                if (Mathf.Abs(dot) > Mathf.Abs(bestDot)) { bestDot = dot; best = axis; }
            }
            var (a, b) = MeshAxesFor(best);
            return new PartPlane(part, best, bestDot < 0f ? -1f : 1f, a, b);
        }

        private static (int a, int b) MeshAxesFor(int upAxis) => upAxis switch
        {
            2 => (0, 1),
            1 => (0, 2),
            _ => (2, 1),
        };

        public bool IsHorizontal =>
            Mathf.Abs((Part.transform.rotation * AxisVector(UpAxis)).y) >= MIN_UPNESS;

        public int SizeAlongA => Part.DimensionsMM[AxisA];
        public int SizeAlongB => Part.DimensionsMM[AxisB];
        public int ThicknessMM => Part.DimensionsMM[UpAxis];

        public Vector3 UpLocal => AxisVector(UpAxis) * UpSign;

        public Vector3 UpWorld => Part.transform.rotation * UpLocal;

        public Vector3 ForwardLocal => Vector3.Cross(AxisVector(AxisA), UpLocal);

        public Quaternion RestingRotation
        {
            get
            {
                var rot = Part.transform.rotation;
                return Quaternion.LookRotation(rot * ForwardLocal, rot * UpLocal);
            }
        }

        public (int offX, int offY, float heightMM) PoseOf(Vector3 worldPosition)
        {
            var pt = Part.transform;
            float toU = AppConstants.MM_TO_UNITS;
            Vector3 local = Quaternion.Inverse(pt.rotation) * (worldPosition - pt.position);
            float heightMM = local[UpAxis] / toU * UpSign - ThicknessMM * 0.5f;
            return (Mathf.RoundToInt(local[AxisA] / toU), Mathf.RoundToInt(local[AxisB] / toU), heightMM);
        }

        public (float alongA, float alongB, float up) DecomposeMM(Vector3 worldDelta)
        {
            Vector3 local = Quaternion.Inverse(Part.transform.rotation) * worldDelta;
            float toU = AppConstants.MM_TO_UNITS;
            return (local[AxisA] / toU, local[AxisB] / toU, local[UpAxis] / toU * UpSign);
        }

        public bool CoversOffset(int offXMM, int offYMM) =>
            Mathf.Abs(offXMM) <= SizeAlongA * 0.5f && Mathf.Abs(offYMM) <= SizeAlongB * 0.5f;

        public Vector3 SurfacePoint(int offXMM, int offYMM)
        {
            float toU = AppConstants.MM_TO_UNITS;
            Vector3 local = AxisVector(AxisA) * (offXMM * toU)
                          + AxisVector(AxisB) * (offYMM * toU)
                          + UpLocal * (ThicknessMM * 0.5f * toU);
            return Part.transform.position + Part.transform.rotation * local;
        }

        public bool BoundsOf(KitchenElement other, out Vector3 min, out Vector3 max)
        {
            min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            var verts = other.GetVertices();
            if (verts.Length == 0) return false;

            var inv = Quaternion.Inverse(Part.transform.rotation);
            foreach (var w in verts)
            {
                Vector3 l = inv * (w - Part.transform.position);
                min = Vector3.Min(min, l);
                max = Vector3.Max(max, l);
            }
            return true;
        }

        public float TwistAroundUpDeg(Quaternion delta) => TwistAngleDeg(delta, UpWorld);

        public static float TwistAngleDeg(Quaternion delta, Vector3 axis)
        {
            axis = axis.normalized;
            Vector3 proj = Vector3.Project(new Vector3(delta.x, delta.y, delta.z), axis);
            var twist = new Quaternion(proj.x, proj.y, proj.z, delta.w);
            float len = twist.x * twist.x + twist.y * twist.y + twist.z * twist.z + twist.w * twist.w;
            if (len < DEGENERATE_TWIST_SQR) return 0f;
            len = Mathf.Sqrt(len);
            twist = new Quaternion(twist.x / len, twist.y / len, twist.z / len, twist.w / len);
            twist.ToAngleAxis(out float angle, out Vector3 twistAxis);
            if (angle > 180f) angle -= 360f;
            return Vector3.Dot(twistAxis, axis) < 0f ? -angle : angle;
        }

        public const float DEGENERATE_TWIST_SQR = 1e-8f;
    }
}
