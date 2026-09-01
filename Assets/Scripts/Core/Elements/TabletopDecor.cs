using UnityEngine;

namespace KitchenDesigner.Core
{
    internal static class TabletopDecor
    {
        public static void ApplyBothSlots(ITabletop target, string tabletopMaterialId,
            string legsMaterialId)
        {
            var topDef = MaterialCatalog.Get(tabletopMaterialId);
            var legsDef = MaterialCatalog.Get(legsMaterialId);
            if (topDef != null)
            {
                var mat = MaterialManager.GetSharedMaterial(topDef);
                if (mat != null) target.SetTabletopMaterial(mat);
            }
            if (legsDef != null)
            {
                var mat = MaterialManager.GetSharedMaterial(legsDef);
                if (mat != null) target.SetLegsMaterial(mat);
            }
        }

        public static void SetBothSlots(ITabletop target, Material material)
        {
            target.SetTabletopMaterial(material);
            target.SetLegsMaterial(material);
        }
    }
}
