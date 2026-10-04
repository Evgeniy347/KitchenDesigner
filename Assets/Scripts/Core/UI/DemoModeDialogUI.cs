using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public sealed class DemoModeDialogUI : MonoBehaviour
    {
        public static DemoModeDialogUI? Instance { get; private set; }

        private ModalDialog? _dialog;

        internal Button? SaveCopyButton => _dialog?.PrimaryButton;
        internal Button? CancelButton => _dialog?.SecondaryButton;
        internal ModalDialog? Dialog => _dialog;

        internal bool IsVisible => _dialog != null && _dialog.IsVisible;
        internal string? MessageText => _dialog?.Body.text;

        private void Awake()
        {
            Instance = this;
            DemoModeGuard.Prompt = ShowIfAvailable;
        }

        private void OnDestroy()
        {
            if (!ReferenceEquals(Instance, this)) return;
            Instance = null;
            DemoModeGuard.Prompt = null;
        }

        public static void ShowIfAvailable()
        {
            var dialog = Instance;
            if (dialog == null || dialog.IsVisible) return;
            dialog.Show();
        }

        public void Build(Transform parent)
        {
            _dialog = ModalDialog.Build(parent, "DemoModeDialog");
        }

        public void Show()
        {
            _dialog?.Show(new ModalDialogContent
            {
                Title = DemoModeStrings.Title,
                Body = DemoModeStrings.Message,
                PrimaryCaption = DemoModeStrings.SaveCopyButton,
                OnPrimary = SaveCopy,
                SecondaryCaption = DemoModeStrings.CancelButton,
            });
        }

        public void Hide() => _dialog?.Hide();

        internal void SaveCopy()
        {
            Hide();
            new ProjectFileActions().SaveAs();
        }
    }
}
