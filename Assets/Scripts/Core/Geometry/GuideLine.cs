using UnityEngine;

namespace KitchenDesigner.Core
{
    public readonly struct GuideLine
    {
        public readonly Vector3 A;
        public readonly Vector3 B;
        public readonly bool EqualGap;

        public GuideLine(Vector3 a, Vector3 b, bool equalGap)
        {
            A = a;
            B = b;
            EqualGap = equalGap;
        }

        public float Length => (B - A).magnitude;
    }
}
