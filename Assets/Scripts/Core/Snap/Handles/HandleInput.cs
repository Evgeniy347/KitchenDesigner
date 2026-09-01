using UnityEngine;
using UnityEngine.EventSystems;

namespace KitchenDesigner.Core.Handles
{
    public static class HandleInput
    {
        public static bool AltHeld =>
            Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

        public static bool CtrlHeld =>
            Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

        public static bool PointerOverUI() =>
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        public static Vector2 MouseScreenPoint => Input.mousePosition;
    }
}
