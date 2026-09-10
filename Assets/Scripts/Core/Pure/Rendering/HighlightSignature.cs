using UnityEngine;

namespace KitchenDesigner.Core
{
    public struct HighlightSignature
    {
        private int _value;

        public int Value => _value;

        public HighlightSignature Add(int v)
        {
            unchecked { _value = _value * 397 ^ v; }
            return this;
        }

        public HighlightSignature Add(float v) => Add(v.GetHashCode());

        public HighlightSignature Add(bool v) => Add(v ? 1 : 0);

        public HighlightSignature Add(string? v) => Add(v == null ? 0 : v.GetHashCode());

        public HighlightSignature Add(Vector3 v) => Add(v.x).Add(v.y).Add(v.z);

        public HighlightSignature Add(Vector3Int v) => Add(v.x).Add(v.y).Add(v.z);

        public HighlightSignature Add(Quaternion v) => Add(v.x).Add(v.y).Add(v.z).Add(v.w);

        public HighlightSignature Add(HighlightSleeve v) =>
            Add(v.FromMM).Add(v.ToMM).Add(v.RadiusMM);
    }
}
