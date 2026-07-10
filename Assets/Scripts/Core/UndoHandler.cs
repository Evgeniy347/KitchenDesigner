using UnityEngine;

namespace KitchenDesigner.Core
{
    public class UndoHandler : MonoBehaviour
    {
        private void Update()
        {
            if (ElementMover.IsDragging) return;
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

            if (ctrl && Input.GetKeyDown(KeyCode.Z))
            {
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    if (CommandStack.CanRedo)
                    {
                        CommandStack.Redo();
                        Refresh();
                        Debug.Log("[Undo] Redo");
                    }
                }
                else
                {
                    if (CommandStack.CanUndo)
                    {
                        CommandStack.Undo();
                        Refresh();
                        Debug.Log("[Undo] Undo");
                    }
                }
            }

            if (ctrl && Input.GetKeyDown(KeyCode.Y))
            {
                if (CommandStack.CanRedo)
                {
                    CommandStack.Redo();
                    Refresh();
                    Debug.Log("[Undo] Redo");
                }
            }
        }

        private static void Refresh()
        {
            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();
        }
    }
}
