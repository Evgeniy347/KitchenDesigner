using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class RadialShelfMesh
    {
        public const int Segments = RoundedRectProfile.DefaultSegments;

        public static Mesh Build(float width, float depth, float thickness, float cornerRadius)
        {
            var profile = RoundedRectProfile.Build(width, depth, 0f, 0f, cornerRadius, 0f, Segments);
            return ProfileExtrusionMesh.Build(profile, width, depth, thickness);
        }
    }
}
