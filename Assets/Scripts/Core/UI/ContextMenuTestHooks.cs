using System;
using TMPro;

namespace KitchenDesigner.Core.UI
{
    internal sealed class ContextMenuTestHooks
    {
        private readonly Func<TMP_InputField?> _nameField;
        private readonly ContextMenuSizeSection _sizes;
        private readonly Action _apply;
        private readonly Action _forgetSectionStates;

        public ContextMenuTestHooks(Func<TMP_InputField?> nameField, ContextMenuSizeSection sizes,
            Action apply, Action forgetSectionStates)
        {
            _nameField = nameField;
            _sizes = sizes;
            _apply = apply;
            _forgetSectionStates = forgetSectionStates;
        }

        public void SetNameFieldText(string text) => _nameField()!.text = text;

        public void SetWidthFieldText(string text) => _sizes.Width!.text = text;

        public void SetHeightFieldText(string text) => _sizes.Height!.text = text;

        public void SimulateApply() => _apply();

        public void ForgetSectionStates() => _forgetSectionStates();
    }
}
