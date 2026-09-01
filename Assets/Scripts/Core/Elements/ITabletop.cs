using UnityEngine;

namespace KitchenDesigner.Core
{
    public interface ITabletop
    {
        string TabletopMaterialId { get; set; }

        string LegsMaterialId { get; set; }

        string TabletopSlotLabel { get; }

        string LegsSlotLabel { get; }

        void SetTabletopMaterial(Material material);

        void SetLegsMaterial(Material material);
    }
}
