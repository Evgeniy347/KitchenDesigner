namespace KitchenDesigner.Core.Plumbing
{
    public static class PipeIssueCatalog
    {
        public const string CodeOpenEnd = "PIP-01";
        public const string CodeSizeMismatch = "PIP-02";
        public const string CodeObstacleCrossed = "PIP-03";

        public static PipeFinding OpenEnd(in PipePort port) =>
            new PipeFinding(PipeFindingLevel.Error, CodeOpenEnd, port.ElementId, null,
                $"Свободный конец трубы (порт {port.PortIndex}) — нужна заглушка, фитинг, подача или обратка");

        public static PipeFinding DirectSizeMismatch(in PipePort a, in PipePort b,
            string? sizeA, string? sizeB) =>
            new PipeFinding(PipeFindingLevel.Error, CodeSizeMismatch, a.ElementId, b.ElementId,
                $"Трубы {PipeSpec.DesignationOrDash(sizeA)} и {PipeSpec.DesignationOrDash(sizeB)} состыкованы напрямую — нужен переходник");

        public static PipeFinding FittingSizeMismatch(string elementId, string? sizeA, string? sizeB) =>
            new PipeFinding(PipeFindingLevel.Error, CodeSizeMismatch, elementId, null,
                $"Фитинг сводит разные диаметры: {PipeSpec.DesignationOrDash(sizeA)} и {PipeSpec.DesignationOrDash(sizeB)} — нужен переходник");

        public static PipeFinding ObstacleCrossed(in PipeRunSegment segment, in PipeObstacle obstacle) =>
            new PipeFinding(PipeFindingLevel.Error, CodeObstacleCrossed, segment.ElementId,
                obstacle.ElementId,
                "Трасса пересекает деталь — прокладка допустима только внутри стены или пола");
    }
}
