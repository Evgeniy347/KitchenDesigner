using UnityEngine;

namespace KitchenDesigner.Core.Handles
{
    public readonly struct HandleMetrics
    {
        public const float TipHalfWidthFactor = 0.7f;

        public readonly float Gap;
        public readonly float ShaftLen;
        public readonly float ShaftRad;
        public readonly float TipLen;
        public readonly float TipSize;
        public readonly float ConeLenFactor;

        public HandleMetrics(float gap, float shaftLen, float shaftRad,
            float tipLen, float tipSize, float coneLenFactor)
        {
            Gap = gap;
            ShaftLen = shaftLen;
            ShaftRad = shaftRad;
            TipLen = tipLen;
            TipSize = tipSize;
            ConeLenFactor = coneLenFactor;
        }

        public float ArrowLen => Gap + ShaftLen + TipLen;

        public float ShaftCenterZ => Gap + ShaftLen * 0.5f;

        public float TipCenterZ => Gap + ShaftLen + TipLen * 0.5f;

        public float DrawnLen => ShaftLen + TipLen;

        public float DrawnCenterZ => Gap + DrawnLen * 0.5f;

        public float GrabCenterZ => TipCenterZ;

        public float MaxRadius => Mathf.Max(ShaftRad, TipSize * TipHalfWidthFactor);

        public Vector3 ShaftScale => new Vector3(ShaftRad * 2f, ShaftRad * 2f, ShaftLen);

        public Vector3 BoxTipScale => new Vector3(TipSize, TipSize, TipLen);

        public Vector3 ConeTipScale =>
            new Vector3(TipSize * 1.4f, TipSize * 1.4f, TipLen * ConeLenFactor);

        public static readonly HandleMetrics Resize =
            new HandleMetrics(0.02f, 0.10f, 0.012f, 0.05f, 0.038f, 1.3f);

        public static readonly HandleMetrics Overlay =
            new HandleMetrics(0f, 0.09f, 0.012f, 0.05f, 0.04f, 1f);
    }
}
