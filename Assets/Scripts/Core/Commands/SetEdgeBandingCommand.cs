namespace KitchenDesigner.Core
{
    /// <summary>Правка параметров кромкования детали (галочка, толщина ленты,
    /// ручные стороны) одной командой: снимок «до» и «после». Три поля
    /// живут в одной команде, потому что правятся из одного блока меню и
    /// раздельный откат смотрелся бы как «Ctrl+Z ничего не вернул».</summary>
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

    /// <summary>Снимок параметров кромкования детали.</summary>
    public readonly struct EdgeBandingState
    {
        public readonly bool enabled;
        public readonly float thicknessMM;
        /// <summary>Битовая маска ручных сторон (<see cref="EdgeManual"/>).</summary>
        public readonly int manualMask;

        public EdgeBandingState(bool enabled, float thicknessMM, int manualMask)
        {
            this.enabled = enabled;
            this.thicknessMM = thicknessMM;
            this.manualMask = manualMask;
        }

        /// <summary>Снимок берётся из данных детали, а НЕ через
        /// EdgeBandingEnabled: тот гасит флаг у нелистовых деталей, и перенос
        /// такого снимка (конвертация типа, дублирование) молча сбрасывал бы
        /// галочку у детали, которая просто временно не лист.</summary>
        public static EdgeBandingState Of(KitchenElement element) =>
            new EdgeBandingState(element.Data.EdgeBanding, element.Data.EdgeThicknessMM,
                element.Data.EdgeManualMask);

        /// <summary>То же состояние с перевёрнутой ручной пометкой одной стороны.</summary>
        public EdgeBandingState WithManual(EdgeSide side, bool manual) =>
            new EdgeBandingState(enabled, thicknessMM, EdgeManual.With(manualMask, side, manual));

        public bool Equals(EdgeBandingState other) =>
            enabled == other.enabled
            && UnityEngine.Mathf.Approximately(thicknessMM, other.thicknessMM)
            && manualMask == other.manualMask;
    }
}
