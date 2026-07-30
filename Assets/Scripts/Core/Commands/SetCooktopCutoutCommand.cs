using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Правка выреза варочной поверхности (ширина и глубина короба,
    /// уходящего в столешницу). Обе величины в одной команде: правятся из
    /// одного блока меню, и раздельный откат смотрелся бы как «Ctrl+Z вернул
    /// половину».
    ///
    /// Габариты самой плиты в команду не входят — их несёт общий
    /// <see cref="ResizeCommand"/>, как у любой другой детали.</summary>
    public class SetCooktopCutoutCommand : IUndoCommand
    {
        private readonly CooktopElement? _element;
        private readonly Vector2Int _before;
        private readonly Vector2Int _after;

        public string Description => $"Cooktop cutout {_element?.PartName}";

        public SetCooktopCutoutCommand(CooktopElement element, Vector2Int before, Vector2Int after)
        {
            _element = element;
            _before = before;
            _after = after;
        }

        /// <summary>Снимок выреза: x — ширина, y — глубина.</summary>
        public static Vector2Int Snapshot(CooktopElement element) =>
            element == null ? Vector2Int.zero
                : new Vector2Int(element.CutoutWidthMM, element.CutoutDepthMM);

        public void Execute() => Apply(_after);

        public void Undo() => Apply(_before);

        private void Apply(Vector2Int state)
        {
            if (_element == null) return;
            _element.CutoutWidthMM = state.x;
            _element.CutoutDepthMM = state.y;
            _element.SnapToPart();
        }
    }
}
