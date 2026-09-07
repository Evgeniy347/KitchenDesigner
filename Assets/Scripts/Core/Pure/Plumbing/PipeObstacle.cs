namespace KitchenDesigner.Core.Plumbing
{
    public readonly struct PipeObstacle
    {
        public readonly string ElementId;
        public readonly PipeObstacleKind Kind;
        public readonly BoxMm BoundsMm;

        public PipeObstacle(string elementId, PipeObstacleKind kind, in BoxMm boundsMm)
        {
            ElementId = elementId;
            Kind = kind;
            BoundsMm = boundsMm;
        }
    }
}
