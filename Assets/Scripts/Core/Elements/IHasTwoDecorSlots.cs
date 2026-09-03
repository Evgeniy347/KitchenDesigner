using UnityEngine;

namespace KitchenDesigner.Core
{
    public interface IHasTwoDecorSlots
    {
        string PrimaryMaterialId { get; set; }

        string SecondaryMaterialId { get; set; }

        string PrimarySlotLabel { get; }

        string SecondarySlotLabel { get; }

        void SetPrimaryMaterial(Material material);

        void SetSecondaryMaterial(Material material);
    }
}
