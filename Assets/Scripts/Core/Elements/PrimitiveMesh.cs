using UnityEngine;

namespace KitchenDesigner.Core
{
    internal static class PrimitiveMesh
    {
        public const float BUILTIN_CYLINDER_HEIGHT = 2f;

        public static Vector3 CylinderScale(float diameter, float height) =>
            new Vector3(diameter, height / BUILTIN_CYLINDER_HEIGHT, diameter);
    }
}
