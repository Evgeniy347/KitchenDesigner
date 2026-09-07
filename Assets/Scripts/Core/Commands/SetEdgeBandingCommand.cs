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
            _element.EdgeForcedMask = state.forcedMask;
            _element.EdgeSuppressedMask = state.suppressedMask;
        }
    }

    public readonly struct EdgeBandingState
    {
        public readonly bool enabled;
        public readonly float thicknessMM;
        public readonly int forcedMask;
        public readonly int suppressedMask;

        public EdgeBandingState(bool enabled, float thicknessMM, int forcedMask, int suppressedMask = 0)
        {
            this.enabled = enabled;
            this.thicknessMM = thicknessMM;
            this.forcedMask = forcedMask & ~suppressedMask;
            this.suppressedMask = suppressedMask;
        }

        public static EdgeBandingState Of(KitchenElement element) =>
            new EdgeBandingState(element.Data.EdgeBanding, element.Data.EdgeThicknessMM,
                element.Data.EdgeForcedMask, element.Data.EdgeSuppressedMask);

        public EdgeBandingState WithState(EdgeSide side, EdgeSideState state) =>
            new EdgeBandingState(enabled, thicknessMM,
                EdgeStates.ForcedMaskWith(forcedMask, side, state),
                EdgeStates.SuppressedMaskWith(suppressedMask, side, state));

        public bool Equals(EdgeBandingState other) =>
            enabled == other.enabled
            && UnityEngine.Mathf.Approximately(thicknessMM, other.thicknessMM)
            && forcedMask == other.forcedMask
            && suppressedMask == other.suppressedMask;
    }
}
