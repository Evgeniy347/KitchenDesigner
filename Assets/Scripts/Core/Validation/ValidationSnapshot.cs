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
            bool hasRecessed = false;
            for (int i = 0; i < elements.Count; i++)
            {
                var e = elements[i];
                if (e == null) continue;
                if (e.GetComponent<Wall>() != null)
                    (wallIndexByName ??= new Dictionary<string, int>())[e.gameObject.name] = i;
                if (e is CooktopElement) hasRecessed = true;
            }

            if (hasRecessed)
            {
                partIndexByName = new Dictionary<string, int>();
                for (int i = 0; i < elements.Count; i++)
                    if (elements[i] != null) partIndexByName[elements[i].PartName] = i;
            }

            foreach (var e in elements)
                into.Add(Build(e, wallIndexByName, partIndexByName));
        }

        private static ValidationElement Build(KitchenElement e, Dictionary<string, int>? wallIndexByName,
            Dictionary<string, int>? partIndexByName = null)
        {
            var wall = e.GetComponent<Wall>();
            var kind = KindOf(e, wall);

            float centerY = wall != null ? wall.FullPosition.y : e.transform.position.y;
            var heightSpan = Span.FromCenter(centerY, e.DimensionsMM.y * AppConstants.MM_TO_UNITS);

            int wallIndex = -1;
            string? attachedWallName = e is WindowElement win ? win.AttachedWallName
                : e is DoorElement door ? door.AttachedWallName
                : null;
            if (!string.IsNullOrEmpty(attachedWallName) && wallIndexByName != null
                && wallIndexByName.TryGetValue(attachedWallName!, out int found))
                wallIndex = found;

            var recessedBody = default(ElementGeometry);
            bool hasRecessedBody = false;
            int hostIndex = -1;
            if (e is CooktopElement cooktop && cooktop.IsAttached)
            {
                recessedBody = ElementGeometry.Box(e.PartName + "/body",
                    cooktop.BodyCenter, cooktop.BodySize, cooktop.transform.rotation);
                hasRecessedBody = true;
                if (partIndexByName != null && !string.IsNullOrEmpty(cooktop.AttachedPartName)
                    && partIndexByName.TryGetValue(cooktop.AttachedPartName, out int host))
                    hostIndex = host;
            }

            return new ValidationElement(
                e.ToGeometry(),
                e.GetVertices(),
                kind,
                e.GroupId,
                (e as DrawerElement)?.PairedDrawerName,
                heightSpan,
                wallIndex,
                recessedBody,
                hasRecessedBody,
                hostIndex);
        }

        private static ElementKind KindOf(KitchenElement e, Wall? wall)
        {
            var kind = ElementKind.None;

            bool isFloor = e.GetComponent<BasePlate>() != null || e is FloorElement;
            bool isOpening = e is WindowElement || e is DoorElement;

            if (isFloor) kind |= ElementKind.FloorAnchor;
            if (isOpening) kind |= ElementKind.Opening;
            if (isFloor || isOpening || wall != null) kind |= ElementKind.Anchor;

            if (e is DrawerElement) kind |= ElementKind.Drawer;
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
