using UnityEngine;

namespace KitchenDesigner.Core
{
    internal static class TabletopDecor
    {
        public const string TabletopSlotReason =
            "декор ставится через SetMaterialCommand (MaterialSlot.Tabletop)";

        public const string LegsSlotReason =
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

        public static void ApplyBothSlots(IHasTwoDecorSlots target, string tabletopMaterialId,
            string legsMaterialId)
        {
            var topDef = MaterialCatalog.Get(tabletopMaterialId);
            var legsDef = MaterialCatalog.Get(legsMaterialId);
            if (topDef != null)
            {
                var mat = MaterialManager.GetSharedMaterial(topDef);
                if (mat != null) target.SetPrimaryMaterial(mat);
            }
            if (legsDef != null)
            {
                var mat = MaterialManager.GetSharedMaterial(legsDef);
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
