using UnityEngine;

namespace KitchenDesigner.Core
{
    public readonly struct ScrewLegMargins
    {
        public readonly bool HasHost;

        public readonly Vector3 RightAxis;
        public readonly Vector3 UpAxis;

        public readonly float LeftMM;
        public readonly float RightMM;
        public readonly float TopMM;
        public readonly float BottomMM;

        public ScrewLegMargins(Vector3 rightAxis, Vector3 upAxis,
            float leftMM, float rightMM, float topMM, float bottomMM)
        {
            HasHost = true;
            RightAxis = rightAxis;
            UpAxis = upAxis;
            LeftMM = leftMM;
            RightMM = rightMM;
            TopMM = topMM;
            BottomMM = bottomMM;
        }

        public float SpanAcrossMM => LeftMM + RightMM;

        public float SpanAlongMM => TopMM + BottomMM;
    }
}
