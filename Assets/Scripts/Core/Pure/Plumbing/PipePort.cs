namespace KitchenDesigner.Core.Plumbing
{
    public readonly struct PipePort
    {
        public readonly string ElementId;
        public readonly PipeNodeKind OwnerKind;
        public readonly int PortIndex;
        public readonly PointMm PositionMm;
        public readonly PipeAxis OutwardAxis;
        public readonly string? SizeId;

        public PipePort(string elementId, PipeNodeKind ownerKind, int portIndex,
            in PointMm positionMm, in PipeAxis outwardAxis, string? sizeId = null)
        {
            ElementId = elementId;
            OwnerKind = ownerKind;
            PortIndex = portIndex;
            PositionMm = positionMm;
            OutwardAxis = outwardAxis;
            SizeId = sizeId;
        }

        public string? DeclaredSizeId =>
            PipeNodePorts.DeclaresOwnSize(OwnerKind) ? PipeSpec.NormalizeSize(SizeId) : null;
    }
}
