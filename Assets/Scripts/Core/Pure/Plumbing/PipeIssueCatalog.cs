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
                $"{PipeFittingNames.Title(port.OwnerKind)}: порт {port.PortIndex} не соединён — нужна заглушка, фитинг, подача или обратка");

        public static PipeFinding FittingSizeMismatch(string elementId, string? sizeA, string? sizeB) =>
            new PipeFinding(PipeFindingLevel.Error, CodeSizeMismatch, elementId, null,
                $"Фитинг сводит разные диаметры: {PipeSpec.DesignationOrDash(sizeA)} и {PipeSpec.DesignationOrDash(sizeB)} — нужен переходник");

        public static PipeFinding SameRoleJoin(in PipePort a, in PipePort b, PipeNodeKind role) =>
            new PipeFinding(PipeFindingLevel.Error, CodeSameRoleJoin, a.ElementId, b.ElementId,
                role == PipeNodeKind.Supply
                    ? "Подача соединена с подачей напрямую — нужна обратка"
                    : "Обратка соединена с обраткой напрямую — нужна подача");

        public static PipeFinding ObstacleCrossed(in PipeRunSegment segment, in PipeObstacle obstacle) =>
            new PipeFinding(PipeFindingLevel.Error, CodeObstacleCrossed, segment.ElementId,
                obstacle.ElementId,
                "Трасса пересекает деталь — прокладка допустима только внутри стены или пола");
    }
}
