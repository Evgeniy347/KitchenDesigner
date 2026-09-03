namespace KitchenDesigner.Core
{
    public interface IWallDevice
    {
        int PlateWidthMM { get; set; }

        int PlateHeightMM { get; set; }

        int ProtrusionMM { get; set; }

        int PostCount { get; set; }
    }
}
