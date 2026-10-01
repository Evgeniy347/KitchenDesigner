namespace KitchenDesigner.Core
{
    public static class GrooveTexts
    {
        public static string KindLabel(GrooveKind kind) =>
            kind == GrooveKind.Blind ? Loc.T("groove.kind.blind") : Loc.T("groove.kind.through");

        public static string SideLabel(GrooveSide side) => side switch
        {
            GrooveSide.Top => Loc.T("groove.side.top"),
            GrooveSide.Bottom => Loc.T("groove.side.bottom"),
            GrooveSide.Left => Loc.T("groove.side.left"),
            _ => Loc.T("groove.side.right"),
        };

        public static string Designation(GrooveKind kind) =>
            $"{KindLabel(kind)} {AppConstants.GROOVE_OFFSET_MM}*{AppConstants.GROOVE_WIDTH_MM}*{AppConstants.GROOVE_DEPTH_MM}";

        public static string Of(GrooveSpec groove) => Designation(groove.kind) + ":" + SideLabel(groove.side);
    }
}
