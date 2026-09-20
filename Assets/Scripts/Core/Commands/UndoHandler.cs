using KitchenDesigner.Core.Keybinding;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class UndoHandler : MonoBehaviour
    {
        private void Update()
        {
            if (ElementMover.IsDragging) return;
            if (CtrlZBelongsToTheTextFieldBeingEdited()) return;

            if (InputMap.Down(InputAction.Redo))
            {
                if (CommandStack.CanRedo)
                {
                    CommandStack.Redo();
                    Refresh();
                }
            }
            else if (InputMap.Down(InputAction.Undo))
            {
                if (CommandStack.CanUndo)
                {
                    CommandStack.Undo();
                    Refresh();
                }
            }

            if (InputMap.Down(InputAction.SaveProject))
            {
                UI.UIManager.Instance?.SaveCurrent();
            }
        }

        private static bool CtrlZBelongsToTheTextFieldBeingEdited() =>
            CameraController.IsTypingInInputField();

        private static void Refresh()
        {
            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();
        }
    }
}
