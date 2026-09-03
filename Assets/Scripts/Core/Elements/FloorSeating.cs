using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    internal static class FloorSeating
    {
        public static void Seat(KitchenElement element, IReadOnlyList<KitchenElement> scene)
        {
            if (element == null || scene == null) return;

            var box = ElementAabb.Of(element);
            var footprintX = new Span(box.minX, box.maxX);
            var footprintZ = new Span(box.minZ, box.maxZ);

            float? top = FloorDrop.SupportTopUnder(footprintX, footprintZ, box.minY,
                SupportsUnder(scene, element));
            if (!top.HasValue || !FloorDrop.WorthSeating(box.minY, top.Value)) return;

            var p = element.transform.position;
            element.transform.position = new Vector3(p.x,
                FloorDrop.SeatedCentreY(p.y, box.minY, top.Value), p.z);
        }

        private static List<FloorSupport> SupportsUnder(IReadOnlyList<KitchenElement> scene,
            KitchenElement element)
        {
            var supports = new List<FloorSupport>();
            foreach (var el in scene)
            {
                if (el == null || ReferenceEquals(el, element)) continue;
                var aabb = ElementAabb.Of(el);
                supports.Add(new FloorSupport(new Span(aabb.minX, aabb.maxX),
                    new Span(aabb.minZ, aabb.maxZ), aabb.maxY));
            }
            return supports;
        }
    }
}
