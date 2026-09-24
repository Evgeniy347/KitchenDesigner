using KitchenDesigner.Core.Plumbing;

namespace KitchenDesigner.Core.Ports
{
    public readonly struct Port
    {
        public readonly string ElementId;
        public readonly int PortIndex;
        public readonly PointMm PositionMm;
        public readonly PipeAxis OutwardAxis;
        public readonly string? ProfileId;

        public Port(string elementId, int portIndex, in PointMm positionMm,
            in PipeAxis outwardAxis, string? profileId = null)
        {
            ElementId = elementId;
            PortIndex = portIndex;
            PositionMm = positionMm;
            OutwardAxis = outwardAxis;
            ProfileId = profileId;
        }
    }
}
