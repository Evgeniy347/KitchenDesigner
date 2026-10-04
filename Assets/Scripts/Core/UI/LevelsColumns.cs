namespace KitchenDesigner.Core.UI
{
    internal static class LevelsColumns
    {
        public const float Gap = UIStyle.Space2;
        public const float MarkerW = UIStyle.Space3;
        public const float NumberW = 96f;
        public const float DeleteW = UIStyle.ControlHCompact;
        public const float FieldH = UIStyle.ControlHCompact;
        public const float RowH = UIStyle.TableRowInteractiveH;
        public const float HeaderH = 24f;
        public const float MarkerDot = UIStyle.Space2;
        public const float AddGap = UIStyle.Space2;

        public static float NameW(float bodyWidth) =>
            bodyWidth - MarkerW - DeleteW - 2f * NumberW - 4f * Gap;

        public static float NameX => MarkerW + Gap;

        public static float ElevationX(float bodyWidth) => NameX + NameW(bodyWidth) + Gap;

        public static float HeightX(float bodyWidth) => ElevationX(bodyWidth) + NumberW + Gap;

        public static float DeleteX(float bodyWidth) => HeightX(bodyWidth) + NumberW + Gap;
    }
}
