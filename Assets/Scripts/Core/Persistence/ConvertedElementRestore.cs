namespace KitchenDesigner.Core
{
    internal static class ConvertedElementRestore
    {
        public static void Apply(ElementData data, KitchenElement element)
        {
            if (data == null || element == null) return;

            ElementRestorers.ApplyShared(data, element);

            var dimensions = data.Dimensions;
            if (dimensions.x > 0 && dimensions.y > 0 && dimensions.z > 0)
                element.DimensionsMM = dimensions;

            if (element is RadialShelfElement radial)
                radial.CornerRadius = data.cornerRadius;

            if (element is AssembledFacadeElement assembled)
            {
                assembled.Fill = (AssembledFill)data.assembledFill;
                assembled.GrooveCount = data.grooveCount;
            }

            if (element is FacadeElement facade)
            {
                facade.Mode = (DoorMode)data.doorMode;
                if (facade.IsOpen != data.doorOpen) facade.SetOpen(data.doorOpen);
            }
        }
    }
}
