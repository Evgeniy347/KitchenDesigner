using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public class SetTextureOverlaysCommand : IUndoCommand
    {
        private readonly KitchenElement? _element;
        private readonly List<TextureOverlaySpec> _before;
        private readonly List<TextureOverlaySpec> _after;

        public string Description => $"Textures {_element?.PartName}";

        public SetTextureOverlaysCommand(KitchenElement element,
            IEnumerable<TextureOverlaySpec> before, IEnumerable<TextureOverlaySpec> after)
        {
            _element = element;
            _before = new List<TextureOverlaySpec>(before);
            _after = new List<TextureOverlaySpec>(after);
        }

        public void Execute()
        {
            if (_element != null) _element.SetTextureOverlays(_after);
        }

        public void Undo()
        {
            if (_element != null) _element.SetTextureOverlays(_before);
        }
    }
}
