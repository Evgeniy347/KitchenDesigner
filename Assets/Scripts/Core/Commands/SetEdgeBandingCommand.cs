namespace KitchenDesigner.Core
{
    public class SetEdgeBandingCommand : IUndoCommand
    {
        private readonly KitchenElement? _element;
        private readonly EdgeBandingState _before;
        private readonly EdgeBandingState _after;

        public string Description => $"Edges {_element?.PartName}";

        public SetEdgeBandingCommand(KitchenElement element,
            EdgeBandingState before, EdgeBandingState after)
        {
            _element = element;
            _before = before;
            _after = after;
        }

        public void Execute() => Apply(_after);

        public void Undo() => Apply(_before);

        private void Apply(EdgeBandingState state)
        {
            if (_element == null) return;
            _element.EdgeBandingEnabled = state.enabled;
            _element.EdgeThicknessMM = state.thicknessMM;
            _element.EdgeManualMask = state.manualMask;
        }
    }

    public readonly struct EdgeBandingState
    {
        public readonly bool enabled;
        public readonly float thicknessMM;
        public readonly int manualMask;

        public EdgeBandingState(bool enabled, float thicknessMM, int manualMask)
        {
            this.enabled = enabled;
            this.thicknessMM = thicknessMM;
            this.manualMask = manualMask;
        }

        public static EdgeBandingState Of(KitchenElement element) =>
            new EdgeBandingState(element.Data.EdgeBanding, element.Data.EdgeThicknessMM,
                element.Data.EdgeManualMask);

        public EdgeBandingState WithManual(EdgeSide side, bool manual) =>
            new EdgeBandingState(enabled, thicknessMM, EdgeManual.With(manualMask, side, manual));

        public bool Equals(EdgeBandingState other) =>
            enabled == other.enabled
            && UnityEngine.Mathf.Approximately(thicknessMM, other.thicknessMM)
            && manualMask == other.manualMask;
    }
}
