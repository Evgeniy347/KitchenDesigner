using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public sealed class NewerVersionDialogUI : MonoBehaviour
    {
        public static NewerVersionDialogUI? Instance { get; private set; }

        private ModalDialog? _dialog;

        internal Button? OpenButton => _dialog?.PrimaryButton;
        internal Button? CancelButton => _dialog?.SecondaryButton;
        internal ModalDialog? Dialog => _dialog;

        internal bool IsVisible => _dialog != null && _dialog.IsVisible;
        internal string? MessageText => _dialog?.Body.text;

        private void Awake()
        {
            Instance = this;
            NewerVersionPrompt.Show = Ask;
        }

        private void OnDestroy()
        {
            if (!ReferenceEquals(Instance, this)) return;
            Instance = null;
            NewerVersionPrompt.Show = null;
        }

        public void Build(Transform parent)
        {
            _dialog = ModalDialog.Build(parent, "NewerVersionDialog");
        }

        public void Ask(string message, System.Action open)
        {
            if (_dialog == null)
            {
                open();
                return;
            }
            _dialog.Show(new ModalDialogContent
            {
                Title = NewerVersionStrings.Title,
                Body = message,
                PrimaryCaption = NewerVersionStrings.OpenButton,
                OnPrimary = open,
                SecondaryCaption = NewerVersionStrings.CancelButton,
            });
        }
    }
}
