using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.Keybinding
{
    public static class KeyBindingDefaults
    {
        private static readonly KeyChord[] Primary = BuildPrimary();

        private static KeyChord[] BuildPrimary()
        {
            var byAction = new Dictionary<InputAction, KeyChord>
            {
                [InputAction.CameraMoveForward] = new KeyChord(KeyCode.W),
                [InputAction.CameraMoveBack] = new KeyChord(KeyCode.S),
                [InputAction.CameraMoveLeft] = new KeyChord(KeyCode.A),
                [InputAction.CameraMoveRight] = new KeyChord(KeyCode.D),
                [InputAction.CameraRotateLeft] = new KeyChord(KeyCode.LeftArrow),
                [InputAction.CameraRotateRight] = new KeyChord(KeyCode.RightArrow),
                [InputAction.CameraRotateUp] = new KeyChord(KeyCode.UpArrow),
                [InputAction.CameraRotateDown] = new KeyChord(KeyCode.DownArrow),
                [InputAction.CameraZoomIn] = new KeyChord(KeyCode.Equals),
                [InputAction.CameraZoomOut] = new KeyChord(KeyCode.Minus),
                [InputAction.CameraFocusSelection] = new KeyChord(KeyCode.F),

                [InputAction.DeleteSelected] = new KeyChord(KeyCode.Delete),
                [InputAction.DuplicateSelected] = new KeyChord(KeyCode.D, ctrl: true),
                [InputAction.SaveProject] = new KeyChord(KeyCode.S, ctrl: true),
                [InputAction.Undo] = new KeyChord(KeyCode.Z, ctrl: true),
                [InputAction.Redo] = new KeyChord(KeyCode.Y, ctrl: true),
                [InputAction.ActivateSelected] = new KeyChord(KeyCode.E),
                [InputAction.DragAxisLockX] = new KeyChord(KeyCode.X),
                [InputAction.DragAxisLockZ] = new KeyChord(KeyCode.Z),

                [InputAction.CatalogOpenSearch] = new KeyChord(KeyCode.Slash),

                [InputAction.ViewTop] = new KeyChord(KeyCode.Alpha1),
                [InputAction.ViewSide] = new KeyChord(KeyCode.Alpha2),
                [InputAction.ViewFront] = new KeyChord(KeyCode.Alpha3),

                [InputAction.ToggleHelp] = new KeyChord(KeyCode.F1),
                [InputAction.TogglePhotoMode] = new KeyChord(KeyCode.F10),
                [InputAction.ErrorPanelCopy] = new KeyChord(KeyCode.C, ctrl: true),
                [InputAction.ErrorPanelSelectAll] = new KeyChord(KeyCode.A, ctrl: true),

                [InputAction.PerfMonitorToggle] = new KeyChord(KeyCode.F9),
                [InputAction.PerfMonitorToggleRecording] = new KeyChord(KeyCode.F9, shift: true),
                [InputAction.ToggleDevConsole] = new KeyChord(KeyCode.BackQuote),
            };

            var table = new KeyChord[InputActionCatalog.All.Length];
            foreach (var pair in byAction) table[(int)pair.Key] = pair.Value;
            return table;
        }

        public static KeyChord PrimaryOf(InputAction action) => Primary[(int)action];

        public static KeyChord AltOf(InputAction action) => KeyChord.Empty;
    }
}
