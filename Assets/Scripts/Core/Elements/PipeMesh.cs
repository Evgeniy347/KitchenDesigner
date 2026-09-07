using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class PipeMesh
    {
        public static Mesh Build(float outerRadius, float lengthUnits) =>
            CylinderStackMesh.Build(new CylinderSection(outerRadius, lengthUnits));
    }
}
