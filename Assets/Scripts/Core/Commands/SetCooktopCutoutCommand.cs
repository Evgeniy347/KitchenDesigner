using UnityEngine;

namespace KitchenDesigner.Core
{
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
