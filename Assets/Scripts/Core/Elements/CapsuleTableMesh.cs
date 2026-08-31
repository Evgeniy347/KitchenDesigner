using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class CapsuleTableMesh
    {
        public const int Segments = RoundedRectProfile.DefaultSegments;

        public static Mesh Build(float widthUnits, float thicknessUnits, float depthUnits,
            float yOffset = 0f)
        {
            float radius = Mathf.Min(widthUnits, depthUnits) * 0.5f;
            var profile = RoundedRectProfile.Uniform(widthUnits, depthUnits, radius, Segments);
            return ProfileExtrusionMesh.Build(profile, widthUnits, depthUnits, thicknessUnits, yOffset);
        }
    }
}
