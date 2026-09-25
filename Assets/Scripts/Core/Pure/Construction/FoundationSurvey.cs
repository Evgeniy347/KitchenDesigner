using System.Collections.Generic;

namespace KitchenDesigner.Core.Construction
{
    public readonly struct FoundationSurvey
    {
        public readonly string ElementId;
        public readonly SoilKind Soil;
        public readonly float WidthMm;
        public readonly float DepthMm;
        public readonly float SandMm;
        public readonly float GravelMm;
        public readonly bool Compacted;
        public readonly float RebarDiameterMm;
        public readonly float RebarStepMm;
        public readonly float CoverMm;
        public readonly bool FrostDepthKnown;
        public readonly float FrostDepthMm;
        public readonly IReadOnlyList<FoundationPolyline> Polylines;

        public FoundationSurvey(string elementId, SoilKind soil, float widthMm, float depthMm,
            float sandMm, float gravelMm, bool compacted, float rebarDiameterMm, float rebarStepMm,
            float coverMm, bool frostDepthKnown, float frostDepthMm,
            IReadOnlyList<FoundationPolyline> polylines)
        {
            ElementId = elementId;
            Soil = soil;
            WidthMm = widthMm;
            DepthMm = depthMm;
            SandMm = sandMm;
            GravelMm = gravelMm;
            Compacted = compacted;
            RebarDiameterMm = rebarDiameterMm;
            RebarStepMm = rebarStepMm;
            CoverMm = coverMm;
            FrostDepthKnown = frostDepthKnown;
            FrostDepthMm = frostDepthMm;
            Polylines = polylines;
        }
    }
}
