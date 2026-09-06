using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class DrawerLinks
    {
        public static DrawerElement? CreatePair(DrawerElement source)
        {
            if (source == null || source.IsUpperDrawer) return null;
            if (source.FindPaired() != null) return null;

            var upperType = DrawerConstants.UPPER_DRAWER_TYPE;
            float step = AppConstants.HalfHeightUnits(DrawerConstants.GetMinOpeningHeight(source.Type)
                        + DrawerConstants.GetMinOpeningHeight(upperType));
            var pos = source.ClosedPosition + source.ClosedRotation * Vector3.up * step;

            string pairName = UniqueName(source.PartName + "_top");
            var go = ElementFactory.CreateDrawer(upperType, source.NominalLength,
                source.Color, source.InternalWidth, pairName, pos, source.System);
            if (go == null) return null;
            go.transform.rotation = source.ClosedRotation;

            var pair = go.GetComponent<DrawerElement>();
            if (pair == null) return null;

            pair.IsDouble = true;
            pair.IsUpperDrawer = true;
            pair.PairedDrawerName = source.PartName;
            pair.Movable = false;

            source.IsDouble = true;
            source.IsUpperDrawer = false;
            source.PairedDrawerName = pair.PartName;
            return pair;
        }

        public static GameObject? DetachPairedUpper(KitchenElement? element) =>
            element is DrawerElement drawer ? DetachPair(drawer) : null;

        public static GameObject? DetachPair(DrawerElement source)
        {
            if (source == null || source.IsUpperDrawer) return null;
            var pair = source.FindPaired();

            source.IsDouble = false;
            source.PairedDrawerName = "";
            if (pair == null) return null;

            pair.IsDouble = false;
            pair.PairedDrawerName = "";
            return pair.gameObject;
        }

        public static void Rename(KitchenElement element, string newName)
        {
            if (element == null || string.IsNullOrEmpty(newName)) return;
            newName = ElementNaming.Normalize(newName, element);
            string oldName = element.PartName;
            if (oldName == newName) return;

            if (element is DrawerElement)
            {
                foreach (var e in PartRegistry.All)
                    if (e is DrawerElement d && d != element && d.PairedDrawerName == oldName)
                        d.PairedDrawerName = newName;
            }
            else if (element is FacadeElement)
            {
                foreach (var e in PartRegistry.All)
                    if (e is IFacadeHost host && host.AttachedFacadeName == oldName)
                        host.AttachedFacadeName = newName;
            }

            foreach (var e in PartRegistry.All)
                if (e != null && e != element && e.AttachedToName == oldName)
                    e.AttachedToName = newName;

            element.PartName = newName;

            if (element is LightSourceElement) LightSwitchNetwork.RenameLight(oldName, newName);
        }

        public static string UniqueName(string baseName) => ElementNaming.Normalize(baseName);

        public static bool IsFacadeInContact(IFacadeHost host, FacadeElement facade)
        {
            if (host == null || facade == null) return false;
            if (host is DrawerElement drawer) return IsFacadeInContact(drawer, facade);
            if (!(host is KitchenElement element)) return false;
            return WithFacadeClosed(facade, IsFacadeDisplacedBy(host),
                () => ConstraintValidator.AreFacadeMountable(element, facade, host.FacadeMountGapMm));
        }

        public static bool IsFacadeDisplacedBy(IFacadeHost host) =>
            host is DishwasherElement dw && dw.DoorProgress > 0f;

        public static T WithFacadeClosed<T>(FacadeElement facade, bool displaced, System.Func<T> check)
        {
            if (facade == null || !displaced || !facade.IsPassenger) return check();
            var savedPos = facade.transform.position;
            var savedRot = facade.transform.rotation;
            try
            {
                facade.transform.SetPositionAndRotation(facade.ClosedPosition, facade.ClosedRotation);
                return check();
            }
            finally
            {
                facade.transform.SetPositionAndRotation(savedPos, savedRot);
            }
        }

        public static bool IsFacadeInContact(DrawerElement drawer, FacadeElement facade)
        {
            if (drawer == null || facade == null) return false;
            var savedPos = drawer.transform.position;
            var savedRot = drawer.transform.rotation;
            try
            {
                drawer.transform.position = drawer.ClosedPosition;
                drawer.transform.rotation = drawer.ClosedRotation;
                return ConstraintValidator.AreFacadeMountable(drawer, facade,
                    drawer.FacadeMountGapMm);
            }
            finally
            {
                drawer.transform.position = savedPos;
                drawer.transform.rotation = savedRot;
            }
        }
    }
}
