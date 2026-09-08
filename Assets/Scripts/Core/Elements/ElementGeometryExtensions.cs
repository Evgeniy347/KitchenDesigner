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
                MountEdgeDetentUnitsOf(element),
                PortsOf(element, element.transform.position));
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
                MountEdgeDetentUnitsOf(element),
                PortsOf(element, position));
        }

        public static SnapPort[] PortsOf(KitchenElement element, Vector3 position)
        {
            if (element is not ISnapPorts source) return System.Array.Empty<SnapPort>();

            int count = source.SnapPortCount;
            if (count <= 0) return System.Array.Empty<SnapPort>();

            var ports = new SnapPort[count];
            for (int i = 0; i < count; i++) ports[i] = source.SnapPortAt(i, position);
            return ports;
        }

        private static Vector3 MountNormalOf(KitchenElement element) =>
            element is IMountsOnTarget mount ? mount.MountNormal : Vector3.zero;

        private static float MountEdgeDetentUnitsOf(KitchenElement element) =>
            element is IMountsOnTarget mount ? mount.MountEdgeDetentUnits : 0f;

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
