using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class ElementGeometryExtensions
    {
        public static ElementGeometry ToGeometry(this KitchenElement element)
        {
            if (element == null) return default;

            var faces = FaceCache.GetFaces(element);
            ElementGeometry.BoundsOf(element.GetVertices(), out var min, out var max);

            return new ElementGeometry(
                element.GetInstanceID(),
                element.PartName,
                faces,
                element.GetGrooveSeatFaces(),
                element.GetGrooveWallFaces(),
                min, max,
                element is PanelElement,
                MountNormalOf(element),
                MountEdgeDetentUnitsOf(element));
        }

        public static ElementGeometry ToGeometryAt(this KitchenElement element, Vector3 position)
        {
            if (element == null) return default;

            ElementGeometry.BoundsOf(element.GetVerticesAt(position), out var min, out var max);

            return new ElementGeometry(
                element.GetInstanceID(),
                element.PartName,
                element.GetFacesAt(position),
                element.GetGrooveSeatFacesAt(position),
                element.GetGrooveWallFacesAt(position),
                min, max,
                element is PanelElement,
                MountNormalOf(element),
                MountEdgeDetentUnitsOf(element));
        }

        private static Vector3 MountNormalOf(KitchenElement element) =>
            element is ScrewLegElement ? element.transform.up : Vector3.zero;

        private static float MountEdgeDetentUnitsOf(KitchenElement element) =>
            element is ScrewLegElement
                ? ScrewLegSpec.MOUNT_DETENT_FROM_EDGE_MM * AppConstants.MM_TO_UNITS
                : 0f;

        public static List<ElementGeometry> ToGeometry(this IEnumerable<KitchenElement> elements)
        {
            var result = new List<ElementGeometry>();
            if (elements == null) return result;

            foreach (var e in elements)
            {
                if (e == null || !e.gameObject.activeInHierarchy) continue;
                result.Add(e.ToGeometry());
            }
            return result;
        }
    }
}
