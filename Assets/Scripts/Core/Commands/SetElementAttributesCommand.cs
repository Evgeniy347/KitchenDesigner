using UnityEngine;

namespace KitchenDesigner.Core
{
    public class SetElementAttributesCommand : IUndoCommand
    {
        private readonly KitchenElement _element;
        private readonly Vector3Int _beforeDims, _afterDims;
        private readonly string _beforeMaterial, _afterMaterial;
        private readonly bool _beforeMovable, _afterMovable;
        public string Description => $"Set attributes {_element.PartName}";

        public SetElementAttributesCommand(KitchenElement element, Vector3Int dimensions,
            string material, bool movable)
        {
            _element = element; _beforeDims = element.DimensionsMM; _afterDims = dimensions;
            _beforeMaterial = element.MaterialId; _afterMaterial = material;
            _beforeMovable = element.Movable; _afterMovable = movable;
        }

        public void Execute() => Apply(_afterDims, _afterMaterial, _afterMovable);
        public void Undo() => Apply(_beforeDims, _beforeMaterial, _beforeMovable);

        private void Apply(Vector3Int dims, string material, bool movable)
        {
            _element.DimensionsMM = dims;
            MaterialManager.ApplyById(_element, material);
            _element.Movable = movable;
        }
    }
}
