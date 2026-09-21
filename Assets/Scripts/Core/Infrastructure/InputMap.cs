using UnityEngine;
using KitchenDesigner.Core.Keybinding;

namespace KitchenDesigner.Core
{
    public static class InputMap
    {
        private static UnityEngine.Object? _muteOwner;
        private static readonly IInputState _liveState = new UnityInputState();

        public static bool SceneInputMuted => _muteOwner != null;

        public static bool IsMutedBy(UnityEngine.Object owner) => _muteOwner != null && _muteOwner == owner;

        public static void MuteSceneInput(UnityEngine.Object owner)
        {
            if (owner == null)
                throw new System.ArgumentNullException(nameof(owner), "немой режим обязан иметь живого владельца");

            if (_muteOwner != null && _muteOwner != owner)
            {
                Debug.LogError($"[InputMap] {owner} попытался заглушить горячие клавиши сцены, пока "
                    + $"она уже заглушена владельцем {_muteOwner} — запрос проигнорирован, "
                    + "снять глушение обязан тот, кто его включил");
                return;
            }

            _muteOwner = owner;
        }

        public static void UnmuteSceneInput(UnityEngine.Object owner)
        {
            if (_muteOwner != owner) return;
            _muteOwner = null;
        }

        public static bool Down(InputAction action) =>
            !SceneInputMuted && ActionFiring.Down(KitchenSettings.Instance.KeyBindings, _liveState, action);

        public static bool Held(InputAction action) =>
            !SceneInputMuted && ActionFiring.Held(KitchenSettings.Instance.KeyBindings, _liveState, action);

        public static bool Up(InputAction action) =>
            !SceneInputMuted && ActionFiring.Up(KitchenSettings.Instance.KeyBindings, _liveState, action);

        public static bool DownIgnoringOccupancy(InputAction action) =>
            !SceneInputMuted && ActionFiring.Down(
                KitchenSettings.Instance.KeyBindings, _liveState, action, ChordResolution.RequiredModifiersOnly);

        internal static void ReleaseAnyMuteForTests() => _muteOwner = null;
    }

    internal sealed class UnityInputState : IInputState
    {
        public const float WheelDeadZone = 0.01f;

        public bool IsKeyDown(KeyCode key) => Input.GetKeyDown(key);
        public bool IsKeyHeld(KeyCode key) => Input.GetKey(key);
        public bool IsKeyUp(KeyCode key) => Input.GetKeyUp(key);

        public bool IsButtonDown(MouseButtonKind button) => TryIndex(button, out var i) && Input.GetMouseButtonDown(i);
        public bool IsButtonHeld(MouseButtonKind button) => TryIndex(button, out var i) && Input.GetMouseButton(i);
        public bool IsButtonUp(MouseButtonKind button) => TryIndex(button, out var i) && Input.GetMouseButtonUp(i);

        public bool WheelMoved => Mathf.Abs(Input.GetAxis("Mouse ScrollWheel")) > WheelDeadZone;

        private static bool TryIndex(MouseButtonKind button, out int index)
        {
            switch (button)
            {
                case MouseButtonKind.Left: index = 0; return true;
                case MouseButtonKind.Right: index = 1; return true;
                case MouseButtonKind.Middle: index = 2; return true;
                case MouseButtonKind.XButton1: index = 3; return true;
                case MouseButtonKind.XButton2: index = 4; return true;
                default: index = -1; return false;
            }
        }
    }
}
