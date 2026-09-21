using UnityEngine;
using KitchenDesigner.Core.Keybinding;

namespace KitchenDesigner.Core
{
    public static class InputMap
    {
        private static UnityEngine.Object? _muteOwner;
        private static readonly IKeyState _liveState = new UnityKeyState();

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

    internal sealed class UnityKeyState : IKeyState
    {
        public bool IsKeyDown(KeyCode key) => Input.GetKeyDown(key);
        public bool IsKeyHeld(KeyCode key) => Input.GetKey(key);
        public bool IsKeyUp(KeyCode key) => Input.GetKeyUp(key);
    }
}
