using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public sealed class ModalDialogKeys : MonoBehaviour
    {
        private ModalDialog? _dialog;

        public void Init(ModalDialog dialog) => _dialog = dialog;

        private void Update()
        {
            if (_dialog == null) return;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) _dialog.Confirm();
            else if (Input.GetKeyDown(KeyCode.Escape)) _dialog.Cancel();
        }
    }
}
