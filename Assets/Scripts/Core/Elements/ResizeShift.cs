using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public readonly struct ResizeShift
    {
        private readonly ElementGeometry _before;
        private readonly List<ElementGeometry>? _neighbours;

        private ResizeShift(ElementGeometry before, List<ElementGeometry> neighbours)
        {
            _before = before;
            _neighbours = neighbours;
        }

        public static ResizeShift Before(KitchenElement? element) =>
            element == null
                ? default
                : new ResizeShift(element!.ToGeometry(), PartRegistry.GetAll().ToGeometryFor(element));

        public Vector3 After(KitchenElement? element) =>
            element == null || _neighbours == null
                ? Vector3.zero
                : ResizeAnchoring.ShiftUnits(_before, element!.ToGeometry(), _neighbours);
    }
}
