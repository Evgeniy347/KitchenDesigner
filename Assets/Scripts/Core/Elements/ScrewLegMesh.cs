using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class ScrewLegMesh
    {
        public static Mesh Build(float baseRadius, float baseHeight,
            float threadRadius, float threadLength) =>
            CylinderStackMesh.Build(
                new CylinderSection(baseRadius, baseHeight),
                new CylinderSection(threadRadius, threadLength));
    }
}
