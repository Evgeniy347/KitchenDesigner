namespace KitchenDesigner.Core
{
    /// <summary>Смена декора у одного слота элемента.
    ///
    /// До неё выбор в списке «Текстура» шёл прямо в MaterialManager и в стек
    /// отмены не попадал вовсе — Ctrl+Z его не возвращал (нарушение правила 2
    /// UI-GUIDELINES). Через эту команду теперь идут оба пути: и список в окне
    /// свойств, и покраска пипеткой.
    ///
    /// Хранятся id, а не MaterialDef: каталог декоров пополняется в рантайме
    /// (ExternalTextureCatalog), и держать ссылку на объект определения незачем —
    /// по id он всегда находится заново.</summary>
    public class SetMaterialCommand : IUndoCommand
    {
        private readonly KitchenElement? _element;
        private readonly MaterialSlot _slot;
        private readonly string _before;
        private readonly string _after;

        public string Description => $"Material {_element?.PartName}";

        public SetMaterialCommand(KitchenElement element, MaterialSlot slot,
            string before, string after)
        {
            _element = element;
            _slot = slot;
            _before = before;
            _after = after;
        }

        /// <summary>Команда с «до», прочитанным из самого элемента, — обычный случай.</summary>
        public SetMaterialCommand(KitchenElement element, MaterialSlot slot, string after)
            : this(element, slot, MaterialManager.MaterialIdOf(element, slot), after)
        {
        }

        public void Execute() => Apply(_after);

        public void Undo() => Apply(_before);

        private void Apply(string materialId)
        {
            if (_element == null) return;
            MaterialManager.ApplySlot(_element, _slot, MaterialCatalog.Get(materialId));
        }
    }
}
