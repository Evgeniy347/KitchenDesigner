using UnityEngine;

namespace KitchenDesigner.Core
{
    public class UndoHandler : MonoBehaviour
    {
        private void Update()
        {
            if (ElementMover.IsDragging) return;
            if (CtrlZBelongsToTheTextFieldBeingEdited()) return;
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

            if (ctrl && Input.GetKeyDown(KeyCode.Z))
            {
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    if (CommandStack.CanRedo)
                    {
                        CommandStack.Redo();
                        Refresh();
                    }
                }
                else
                {
                    if (CommandStack.CanUndo)
                    {
                        CommandStack.Undo();
                        Refresh();
                    }
                }
            }

            if (ctrl && Input.GetKeyDown(KeyCode.S))
            {
                UI.UIManager.Instance?.SaveCurrent();
            }

            if (ctrl && Input.GetKeyDown(KeyCode.Y))
            {
                if (CommandStack.CanRedo)
                {
                    CommandStack.Redo();
                    Refresh();
                }
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
