using System;
using KitchenDesigner.Core.Keybinding;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class InputMap
    {
        private static UnityEngine.Object? _muteOwner;

        public static bool SceneInputMuted => _muteOwner != null;

        public static bool IsMutedBy(UnityEngine.Object owner) => _muteOwner != null && _muteOwner == owner;

        public static void MuteSceneInput(UnityEngine.Object owner)
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner), "немой режим обязан иметь живого владельца");

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

        public static bool Down(InputAction action, ChordMatchMode mode = ChordMatchMode.ExactModifiers) =>
            !SceneInputMuted && Fires(action, Input.GetKeyDown, mode);

        public static bool Held(InputAction action, ChordMatchMode mode = ChordMatchMode.RequiredModifiersOnly) =>
            !SceneInputMuted && Fires(action, Input.GetKey, mode);

        public static bool Up(InputAction action, ChordMatchMode mode = ChordMatchMode.ExactModifiers) =>
            !SceneInputMuted && Fires(action, Input.GetKeyUp, mode);

        private static bool Fires(InputAction action, Func<KeyCode, bool> keyEvent, ChordMatchMode mode)
        {
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            return FiresCore(action, keyEvent, ctrl, alt, shift, mode);
        }

        private static bool FiresCore(
            InputAction action, Func<KeyCode, bool> keyEvent, bool ctrl, bool alt, bool shift, ChordMatchMode mode)
        {
            var bindings = KitchenSettings.Instance.KeyBindings;
            return FiresFor(bindings.Primary(action), keyEvent, ctrl, alt, shift, mode)
                || FiresFor(bindings.Alt(action), keyEvent, ctrl, alt, shift, mode);
        }

        private static bool FiresFor(
            KeyChord chord, Func<KeyCode, bool> keyEvent, bool ctrl, bool alt, bool shift, ChordMatchMode mode) =>
            ChordMatch.Fires(chord, !chord.IsEmpty && keyEvent(chord.Key), ctrl, alt, shift, mode);

        internal static bool DownWithSimulatedKeyForTests(
            InputAction action, KeyCode pressedKey,
            bool ctrl = false, bool alt = false, bool shift = false,
            ChordMatchMode mode = ChordMatchMode.ExactModifiers) =>
            !SceneInputMuted && FiresCore(action, k => k == pressedKey, ctrl, alt, shift, mode);

        internal static bool HeldWithSimulatedKeyForTests(
            InputAction action, KeyCode heldKey,
            bool ctrl = false, bool alt = false, bool shift = false,
            ChordMatchMode mode = ChordMatchMode.RequiredModifiersOnly) =>
            !SceneInputMuted && FiresCore(action, k => k == heldKey, ctrl, alt, shift, mode);

        internal static void ReleaseAnyMuteForTests() => _muteOwner = null;
    }
}
