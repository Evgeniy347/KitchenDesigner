using System.Collections.Generic;

namespace KitchenDesigner.Core.Construction
{
    public readonly struct RoofFrame
    {
        public readonly IReadOnlyList<RoofPlaneFootprint> Planes;
        public readonly float RidgeLengthMm;
        public readonly float EaveLengthMm;
        public readonly float GutterLengthMm;

        public RoofFrame(IReadOnlyList<RoofPlaneFootprint> planes, float ridgeLengthMm,
            float eaveLengthMm, float gutterLengthMm)
        {
            Planes = planes;
            RidgeLengthMm = ridgeLengthMm;
            EaveLengthMm = eaveLengthMm;
            GutterLengthMm = gutterLengthMm;
        }
    }
}
