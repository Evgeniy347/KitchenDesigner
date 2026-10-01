using System.Text;
using KitchenDesigner.Core.Construction;

namespace KitchenDesigner.Core.UI
{
    public static class FrostDepthHint
    {
        public const string Key = "settings.construction.frostDepth";

        public static string ClimateSourceLine => Loc.T("settings.construction.frostDepthClimateSource");

        public static string StationPrefix => Loc.T("settings.construction.referenceStation");

        public static string For(ConstructionRegion region, SoilKind soil)
        {
            var reading = FrostDepth.Read(region, soil);
            var text = new StringBuilder(HintText.Of(Key));
            text.Append('\n').Append(ClimateSourceLine);
            if (reading.ReferenceStation.Length > 0)
                text.Append('\n').Append(StationPrefix).Append(reading.ReferenceStation).Append('.');
            if (reading.Reason.Length > 0)
                text.Append('\n').Append(reading.Reason);
            return text.ToString();
        }
    }
}
