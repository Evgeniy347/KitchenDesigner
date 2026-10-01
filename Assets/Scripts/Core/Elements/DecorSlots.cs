using UnityEngine;

namespace KitchenDesigner.Core
{
    internal static class DecorSlots
    {
        public const string PrimarySlotReason =
            "декор ставится через SetMaterialCommand (MaterialSlot.Tabletop)";

        public const string SecondarySlotReason =
            "декор ставится через SetMaterialCommand (MaterialSlot.Legs)";

        public static string TabletopLabel => Loc.T("decor.slot.tabletop");

        public static string SeatLabel => Loc.T("decor.slot.seat");

        public static string LegsLabel => Loc.T("decor.slot.legs");

        public static string UpholsteryLabel => Loc.T("decor.slot.upholstery");

        public static string CushionsLabel => Loc.T("decor.slot.cushions");

        public static string FrameLabel => Loc.T("decor.slot.frame");

        public static string MattressLabel => Loc.T("decor.slot.mattress");

        public const string MaterialIdAliasReason =
            "псевдоним PrimaryMaterialId — см. его причину";

        public static string SlotIdOrDefault(string? materialId)
            => materialId ?? MaterialCatalog.DefaultId;

        public static void ApplyBothSlots(IHasTwoDecorSlots target, string primaryMaterialId,
            string secondaryMaterialId)
        {
            var primaryDef = MaterialCatalog.Get(primaryMaterialId);
            var secondaryDef = MaterialCatalog.Get(secondaryMaterialId);
            if (primaryDef != null)
            {
                var mat = MaterialManager.GetSharedMaterial(primaryDef);
                if (mat != null) target.SetPrimaryMaterial(mat);
            }
            if (secondaryDef != null)
            {
                var mat = MaterialManager.GetSharedMaterial(secondaryDef);
                if (mat != null) target.SetSecondaryMaterial(mat);
            }
        }

        public static void SetBothSlots(IHasTwoDecorSlots target, Material material)
        {
            target.SetPrimaryMaterial(material);
            target.SetSecondaryMaterial(material);
        }
    }
}
