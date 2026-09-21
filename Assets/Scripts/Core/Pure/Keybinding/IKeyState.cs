using UnityEngine;

namespace KitchenDesigner.Core.Keybinding
{
    public interface IKeyState
    {
        bool IsKeyDown(KeyCode key);
        bool IsKeyHeld(KeyCode key);
        bool IsKeyUp(KeyCode key);
    }
}
