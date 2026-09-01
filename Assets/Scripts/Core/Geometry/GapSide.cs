namespace KitchenDesigner.Core
{
    public enum GapSide
    {
        Left,
        Right,
        Top,
        Bottom,
        Front,
        Back,
    }

    public static class GapSides
    {
        public static readonly GapSide[] All =
        {
            GapSide.Left, GapSide.Right, GapSide.Top,
            GapSide.Bottom, GapSide.Front, GapSide.Back,
        };

        public static int FaceIndex(GapSide side)
        {
            switch (side)
            {
                case GapSide.Right: return 0;
                case GapSide.Left: return 1;
                case GapSide.Top: return 2;
                case GapSide.Bottom: return 3;
                case GapSide.Front: return 4;
                default: return 5;
            }
        }
    }
}
