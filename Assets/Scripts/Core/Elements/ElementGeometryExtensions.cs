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
                element is ScrewLegElement ? element.transform.up : Vector3.zero);
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
                element is ScrewLegElement ? element.transform.up : Vector3.zero);
        }

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
