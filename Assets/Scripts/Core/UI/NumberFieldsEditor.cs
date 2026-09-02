using System;
using System.Collections.Generic;
using TMPro;

namespace KitchenDesigner.Core.UI
{
    internal abstract class NumberFieldsEditor : ElementFieldsEditor
    {
        private sealed class NumberFieldBinding
        {
            private readonly Func<KitchenElement, int> _read;
            private readonly Action<KitchenElement, int> _write;

            public NumberFieldBinding(TMP_InputField field, Func<KitchenElement, int> read,
                Action<KitchenElement, int> write, string idleText)
            {
                Field = field;
                IdleText = idleText;
                _read = read;
                _write = write;
            }

            public TMP_InputField Field { get; }

            public string IdleText { get; }

            public string TextOf(KitchenElement element) => _read(element).ToString();

            public void Write(KitchenElement element, ContextMenuFieldTracker fields) =>
                _write(element, fields.ParseInt(Field, _read(element)));
        }

        private readonly List<NumberFieldBinding> _bindings = new List<NumberFieldBinding>();

        protected NumberFieldsEditor(IContextMenuHost host) : base(host) { }

        protected void Bind(TMP_InputField field, Func<KitchenElement, int> read,
            Action<KitchenElement, int> write, string idleText) =>
            _bindings.Add(new NumberFieldBinding(field, read, write, idleText));

        public override IEnumerable<TMP_InputField?> ArithmeticFields()
        {
            foreach (var binding in _bindings) yield return binding.Field;
        }

        public override void Show(KitchenElement element) => WriteFields(element);

        public override void AfterApply(KitchenElement element) => WriteFields(element);

        public override void Refresh(KitchenElement element)
        {
            if (!Handles(element)) return;
            foreach (var binding in _bindings)
                Fields.RefreshUnfocused(binding.Field, binding.TextOf(element));
        }

        public override void Apply(KitchenElement element)
        {
            if (!Handles(element)) return;
            foreach (var binding in _bindings) binding.Write(element, Fields);
        }

        public override void Track(KitchenElement element)
        {
            bool mine = Handles(element);
            foreach (var binding in _bindings)
                Fields.Track(binding.Field, mine ? binding.TextOf(element) : binding.IdleText);
        }

        private void WriteFields(KitchenElement element)
        {
            if (!Handles(element)) return;
            foreach (var binding in _bindings) binding.Field.text = binding.TextOf(element);
        }
    }
}
