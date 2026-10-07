using UnityEngine;

namespace KitchenDesigner.Core.MCP
{
    public readonly struct BoxMm
    {
        public readonly Vector3 Min;
        public readonly Vector3 Max;

        public BoxMm(Vector3 min, Vector3 max)
        {
            Min = min;
            Max = max;
        }

        public Vector3 Size => Max - Min;

        public Vector3 Center => (Min + Max) * 0.5f;
    }
}
