using System;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core.UI;

namespace KitchenDesigner.Core.Update
{
    public sealed class UpdateDialogUI : MonoBehaviour, IUpdateDialog
    {
        private ModalDialog? _dialog;
        private Action? _onUpdate;
        private Action? _onCancel;

        internal Button? UpdateButton => _dialog?.PrimaryButton;
        internal Button? CancelButton => _dialog?.SecondaryButton;

        internal bool IsVisible => _dialog != null && _dialog.IsVisible;
        internal string? MessageText => _dialog?.Body.text;
        internal RectTransform? BackdropRect => _dialog?.Root;
        internal ModalDialog? Dialog => _dialog;

        public void Build(Transform parent)
        {
            _dialog = ModalDialog.Build(parent, "UpdateDialog");
        }

        public void ShowUpdateAvailable(string version, Action onUpdate, Action onCancel)
        {
            _onUpdate = onUpdate;
            _onCancel = onCancel;
            _dialog?.Show(new ModalDialogContent
            {
                Title = UpdateStrings.UpdateTitle,
                Body = string.Format(UpdateStrings.UpdateMessage, version),
                PrimaryCaption = UpdateStrings.UpdateAcceptButton,
                OnPrimary = InvokeUpdate,
                SecondaryCaption = UpdateStrings.UpdateCancelButton,
                OnSecondary = InvokeCancel,
            });
        }

        public void Hide() => _dialog?.Hide();

        private void InvokeUpdate() => _onUpdate?.Invoke();

        private void InvokeCancel() => _onCancel?.Invoke();
    }
}
