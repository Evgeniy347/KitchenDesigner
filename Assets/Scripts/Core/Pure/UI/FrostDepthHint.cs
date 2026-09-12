using System.Text;
using KitchenDesigner.Core.Construction;

namespace KitchenDesigner.Core.UI
{
    public static class FrostDepthHint
    {
        public const string Key = "settings.construction.frostDepth";

        public const string ClimateSourceLine =
            "Климат — по СП 131.13330.2020; справочники по СНиП 23-01-99* дают ≈1,4 м.";

        public const string StationPrefix = "Опорная станция — ";

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
