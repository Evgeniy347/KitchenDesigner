using UnityEngine;

namespace KitchenDesigner.Core
{
    internal static class DecorSlots
    {
        public const string PrimarySlotReason =
            "декор ставится через SetMaterialCommand (MaterialSlot.Tabletop)";

        public const string SecondarySlotReason =
            "декор ставится через SetMaterialCommand (MaterialSlot.Legs)";

        public const string TabletopLabel = "Столешница";

        public const string SeatLabel = "Сиденье";

        public const string LegsLabel = "Ножки";

        public const string UpholsteryLabel = "Обивка";

        public const string CushionsLabel = "Подушки";

        public const string FrameLabel = "Каркас";

        public const string MattressLabel = "Матрас";

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
