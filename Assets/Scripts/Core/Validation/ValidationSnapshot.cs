using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class ValidationSnapshot
    {
        public static void Build(List<KitchenElement> elements, List<ValidationElement> into)
        {
            into.Clear();
            if (elements == null || elements.Count == 0) return;

            Dictionary<string, int>? wallIndexByName = null;
            Dictionary<string, int>? partIndexByName = null;
            bool hasHosted = false;
            for (int i = 0; i < elements.Count; i++)
            {
                var e = elements[i];
                if (e == null) continue;
                if (e.GetComponent<Wall>() != null)
                    (wallIndexByName ??= new Dictionary<string, int>())[e.gameObject.name] = i;
                if (e is CooktopElement || e is ScrewLegElement) hasHosted = true;
            }

            if (hasHosted)
            {
                partIndexByName = new Dictionary<string, int>();
                for (int i = 0; i < elements.Count; i++)
                    if (elements[i] != null) partIndexByName[elements[i].PartName] = i;
            }

            foreach (var e in elements)
                into.Add(Build(e, wallIndexByName, partIndexByName));
        }

        public static ElementGeometry MainBody(KitchenElement e) =>
            e is ScrewLegElement leg ? leg.BaseBody : e.ToGeometry();

        public static bool TryExtraBody(KitchenElement e, out ElementGeometry extra,
            out string? hostName)
        {
            if (e is ScrewLegElement leg)
            {
                extra = leg.ThreadBody;
                hostName = leg.HostPartName;
                return true;
            }
            if (e is CooktopElement cooktop && cooktop.IsAttached)
            {
                extra = ElementGeometry.Box(e.PartName + "/body",
                    cooktop.BodyCenter, cooktop.BodySize, cooktop.transform.rotation);
                hostName = cooktop.AttachedPartName;
                return true;
            }
            extra = default;
            hostName = null;
            return false;
        }

        public static ElementGeometry[] SolidBodies(KitchenElement e) =>
            TryExtraBody(e, out var extra, out _)
                ? new[] { MainBody(e), extra }
                : new[] { MainBody(e) };

        public static ElementKind KindOf(KitchenElement e) => KindOf(e, e.GetComponent<Wall>());

        public static bool IsAnchor(KitchenElement e) =>
            e != null && (KindOf(e) & ElementKind.Anchor) != 0;

        public static bool IsPanel(KitchenElement e) => e is PanelElement;

        public static bool IsDecor(KitchenElement e) => e is LightSourceElement;

        public static DishwasherElement? AsDishwasher(KitchenElement e) => e as DishwasherElement;

        public static FacadeElement? AsFacade(KitchenElement e) => e as FacadeElement;

        private static ValidationElement Build(KitchenElement e, Dictionary<string, int>? wallIndexByName,
            Dictionary<string, int>? partIndexByName = null)
        {
            var wall = e.GetComponent<Wall>();
            var kind = KindOf(e, wall);

            float centerY = wall != null ? wall.FullPosition.y : e.transform.position.y;
            var heightSpan = Span.FromCenter(centerY, e.DimensionsMM.y * AppConstants.MM_TO_UNITS);

            int wallIndex = -1;
            string? attachedWallName = e is WallOpeningElement opening ? opening.AttachedWallName : null;
            if (!string.IsNullOrEmpty(attachedWallName) && wallIndexByName != null
                && wallIndexByName.TryGetValue(attachedWallName!, out int found))
                wallIndex = found;

            bool hasExtraBody = TryExtraBody(e, out var extraBody, out string? hostName);
            int hostIndex = -1;
            if (hasExtraBody && partIndexByName != null && !string.IsNullOrEmpty(hostName)
                && partIndexByName.TryGetValue(hostName!, out int host))
                hostIndex = host;

            return new ValidationElement(
                MainBody(e),
                e.GetVertices(),
                kind,
                e.GroupId,
                (e as DrawerElement)?.PairedDrawerName ?? (e as ScrewLegElement)?.HostPartName,
                heightSpan,
                wallIndex,
                extraBody,
                hasExtraBody,
                hostIndex);
        }

        private static ElementKind KindOf(KitchenElement e, Wall? wall)
        {
            var kind = ElementKind.None;

            bool isFloor = e.GetComponent<BasePlate>() != null || e is FloorElement;
            bool isOpening = e is WallOpeningElement;

            if (isFloor) kind |= ElementKind.FloorAnchor;
            if (isOpening) kind |= ElementKind.Opening;
            if (isFloor || isOpening || wall != null) kind |= ElementKind.Anchor;

            if (e is DrawerElement) kind |= ElementKind.Drawer;
            if (e is ScrewLegElement) kind |= ElementKind.ScrewLeg;
            if (e is LightSourceElement) kind |= ElementKind.Decor;
            if (e is SinkElement || e is CooktopElement) kind |= ElementKind.Recessed;
            if (e is FacadeElement fe)
            {
                kind |= ElementKind.Facade;
                if (fe.GapMM > 0) kind |= ElementKind.FloatingFacade;
            }

            return kind;
        }
    }
}
