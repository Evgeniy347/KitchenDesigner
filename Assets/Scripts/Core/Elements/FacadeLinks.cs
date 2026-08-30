using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public static class FacadeLinks
    {
        public static IEnumerable<FacadeElement> All()
        {
            foreach (var e in PartRegistry.GetAll())
                if (e is FacadeElement facade) yield return facade;
        }

        public static FacadeElement? FindByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (var facade in All())
                if (facade.PartName == name) return facade;
            return null;
        }

        public static DrawerElement? FindDrawer(FacadeElement facade)
        {
            if (string.IsNullOrEmpty(facade.PartName)) return null;
            foreach (var e in PartRegistry.GetAll())
                if (e is DrawerElement d && d.AttachedFacadeName == facade.PartName) return d;
            return null;
        }

        public static DishwasherElement? FindDishwasher(FacadeElement facade)
        {
            if (string.IsNullOrEmpty(facade.PartName)) return null;
            foreach (var e in PartRegistry.GetAll())
                if (e is DishwasherElement dw && dw.AttachedFacadeName == facade.PartName) return dw;
            return null;
        }
    }
}
