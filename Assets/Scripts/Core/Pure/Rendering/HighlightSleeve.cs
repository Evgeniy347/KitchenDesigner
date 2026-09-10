using UnityEngine;

namespace KitchenDesigner.Core
{
    public readonly struct HighlightSleeve
    {
        public readonly Vector3 FromMM;
        public readonly Vector3 ToMM;
        public readonly float RadiusMM;

        public HighlightSleeve(Vector3 fromMM, Vector3 toMM, float radiusMM)
        {
            FromMM = fromMM;
            ToMM = toMM;
            RadiusMM = radiusMM;
        }

        public Vector3 CentreMM => (FromMM + ToMM) * 0.5f;

        public float LengthMM => (ToMM - FromMM).magnitude;

        public bool IsEmpty => RadiusMM <= 0f || LengthMM <= 0f;
    }
}
