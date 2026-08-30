namespace KitchenDesigner.Core
{
    public interface IPartCutout
    {
        string PartName { get; }

        GrooveMesh.Rect2 CutoutRectIn(KitchenElement part);

        int HoleAxisIn(KitchenElement part);
    }
}
