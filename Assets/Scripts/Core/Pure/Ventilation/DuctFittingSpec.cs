using KitchenDesigner.Core.Ports;

namespace KitchenDesigner.Core.Ventilation
{
    public static class DuctFittingSpec
    {
        public static readonly DuctNodeKind[] Kinds =
        {
            DuctNodeKind.Duct,
            DuctNodeKind.Elbow,
            DuctNodeKind.Transition,
            DuctNodeKind.Tee,
            DuctNodeKind.Cap,
            DuctNodeKind.Grille,
        };

        public static bool IsFitting(DuctNodeKind kind) => kind != DuctNodeKind.Duct;

        public static LegFrame Legs(DuctNodeKind kind) => kind switch
        {
            DuctNodeKind.Elbow => LegFrame.TwoWayCorner,
            DuctNodeKind.Transition => LegFrame.TwoWayStraight,
            DuctNodeKind.Tee => LegFrame.ThreeWay,
            DuctNodeKind.Cap => LegFrame.OneWay,
            DuctNodeKind.Grille => LegFrame.OneWay,
            _ => LegFrame.None,
        };

        public static int PortCount(DuctNodeKind kind) => Legs(kind).Count;
    }
}
