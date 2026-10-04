using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    internal static class SofaBoxMesh
    {
        public static Mesh Build(IReadOnlyList<Cuboid> panels)
        {
            float contour = 1f / AppConstants.MM_TO_UNITS;
            var shift = new Vector3(contour, contour, contour) * 0.5f;
            var boxes = new List<DrawerMesh.Box>(panels.Count);
            foreach (var panel in panels)
                boxes.Add(new DrawerMesh.Box
                {
                    name = "",
                    minMM = panel.CentreMM - panel.SizeMM * 0.5f + shift,
                    sizeMM = panel.SizeMM,
                });

            return DrawerMesh.BuildFromBoxes(boxes, new Vector3(contour, contour, contour));
        }
    }
}
