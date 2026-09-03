using System;
using System.Collections.Generic;
using TMPro;

namespace KitchenDesigner.Core.UI
{
    internal abstract class NumberFieldsEditor : ElementFieldsEditor
    {
        private interface INumberFieldCase
        {
            bool TryRead(KitchenElement element, out int value);

            void Write(KitchenElement element, int value);
        }

        private sealed class NumberFieldCase<T> : INumberFieldCase where T : class
        {
            private readonly Func<T, int> _read;
            private readonly Action<T, int> _write;

            public NumberFieldCase(Func<T, int> read, Action<T, int> write)
            {
                _read = read;
                _write = write;
            }

            public bool TryRead(KitchenElement element, out int value)
            {
                if (element is T typed)
                {
                    value = _read(typed);
                    return true;
                }

                value = 0;
                return false;
            }

            public void Write(KitchenElement element, int value)
            {
                if (element is T typed) _write(typed, value);
            }
        }

        internal sealed class NumberFieldBinding
        {
            private readonly List<INumberFieldCase> _cases = new List<INumberFieldCase>();

            internal NumberFieldBinding(TMP_InputField field, string idleText)
            {
                Field = field;
                IdleText = idleText;
            }

            public TMP_InputField Field { get; }

            public string IdleText { get; }

            public NumberFieldBinding Or<T>(Func<T, int> read, Action<T, int> write)
                where T : class
            {
                _cases.Add(new NumberFieldCase<T>(read, write));
                return this;
            }

            public string TextOf(KitchenElement element)
            {
                foreach (var branch in _cases)
                    if (branch.TryRead(element, out int value))
                        return value.ToString();
                return IdleText;
            }

            public void Write(KitchenElement element, ContextMenuFieldTracker fields)
            {
                foreach (var branch in _cases)
                    if (branch.TryRead(element, out int current))
                    {
                        branch.Write(element, fields.ParseInt(Field, current));
                        return;
                    }
            }
        }

        private readonly List<NumberFieldBinding> _bindings = new List<NumberFieldBinding>();

        protected NumberFieldsEditor(IContextMenuHost host) : base(host) { }

        protected NumberFieldBinding Bind<T>(TMP_InputField field, Func<T, int> read,
            Action<T, int> write, string idleText) where T : class
        {
            var binding = new NumberFieldBinding(field, idleText);
            binding.Or(read, write);
            _bindings.Add(binding);
            return binding;
        }

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
