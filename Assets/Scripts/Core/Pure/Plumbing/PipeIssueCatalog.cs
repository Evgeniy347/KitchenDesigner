namespace KitchenDesigner.Core.Plumbing
{
    public static class PipeIssueCatalog
    {
        public const string CodeOpenEnd = "PIP-01";
        public const string CodeSizeMismatch = "PIP-02";
        public const string CodeObstacleCrossed = "PIP-03";
        public const string CodeSameRoleJoin = "PIP-04";

        public static PipeFinding OpenEnd(in PipePort port) =>
            new PipeFinding(PipeFindingLevel.Error, CodeOpenEnd, port.ElementId, null,
                Loc.F("issue.pip01.message", PipeFittingNames.Title(port.OwnerKind), port.PortIndex));

        public static PipeFinding FittingSizeMismatch(string elementId, string? sizeA, string? sizeB) =>
            new PipeFinding(PipeFindingLevel.Error, CodeSizeMismatch, elementId, null,
                Loc.F("issue.pip02.message", PipeSpec.DesignationOrDash(sizeA), PipeSpec.DesignationOrDash(sizeB)));

        public static PipeFinding SameRoleJoin(in PipePort a, in PipePort b, PipeNodeKind role) =>
            new PipeFinding(PipeFindingLevel.Error, CodeSameRoleJoin, a.ElementId, b.ElementId,
                role == PipeNodeKind.Supply
                    ? Loc.T("issue.pip04.supplyToSupply")
                    : Loc.T("issue.pip04.returnToReturn"));

        public static PipeFinding ObstacleCrossed(in PipeRunSegment segment, in PipeObstacle obstacle) =>
            new PipeFinding(PipeFindingLevel.Error, CodeObstacleCrossed, segment.ElementId,
                obstacle.ElementId,
                Loc.T("issue.pip03.message"));
    }
}
