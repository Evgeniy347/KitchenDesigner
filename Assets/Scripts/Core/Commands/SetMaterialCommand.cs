namespace KitchenDesigner.Core
{
    public class SetMaterialCommand : IUndoCommand
    {
        private readonly KitchenElement? _element;
        private readonly MaterialSlot _slot;
        private readonly string _beforeMaterialId;
        private readonly string _afterMaterialId;

        public string Description => $"Material {_element?.PartName}";

        public SetMaterialCommand(KitchenElement element, MaterialSlot slot,
            string beforeMaterialId, string afterMaterialId)
        {
            _element = element;
            _slot = slot;
            _beforeMaterialId = beforeMaterialId;
            _afterMaterialId = afterMaterialId;
        }

        public SetMaterialCommand(KitchenElement element, MaterialSlot slot, string afterMaterialId)
            : this(element, slot, MaterialManager.MaterialIdOf(element, slot), afterMaterialId)
        {
        }

        public void Execute() => Apply(_afterMaterialId);

        public void Undo() => Apply(_beforeMaterialId);

        private void Apply(string materialId)
        {
            if (_element == null) return;
            MaterialManager.ApplySlot(_element, _slot, MaterialCatalog.Get(materialId));
        }
    }
}
