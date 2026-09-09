using UnityEngine;

namespace KitchenDesigner.Core
{
    public readonly struct SpecItem
    {
        public readonly string section;
        public readonly string name;
        public readonly string material;
        public readonly SpecUnit unit;
        public readonly float qty;
        public readonly Vector3Int dimsMM;
        public readonly bool hasDims;

        public SpecItem(string section, string name, string material, SpecUnit unit, float qty,
            Vector3Int dimsMM = default, bool hasDims = false)
        {
            this.section = section ?? "";
            this.name = name ?? "";
            this.material = material ?? "";
            this.unit = unit;
            this.qty = qty;
            this.dimsMM = dimsMM;
            this.hasDims = hasDims;
        }

        public string GroupKey() => $"Q|{section}|{name}|{unit}|{material}|{(hasDims ? dimsMM.ToString() : "")}";
    }
}
