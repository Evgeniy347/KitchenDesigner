using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.Keybinding
{
    public static class KeyBindingDefaults
    {
        private static readonly InputBinding[] Primary = BuildPrimary();
        private static readonly InputBinding[] Alt = BuildAlt();

        private static InputBinding[] BuildPrimary()
        {
            var byAction = new Dictionary<InputAction, InputBinding>
            {
                [InputAction.CameraMoveForward] = InputBinding.FromKey(new KeyChord(KeyCode.W)),
                [InputAction.CameraMoveBack] = InputBinding.FromKey(new KeyChord(KeyCode.S)),
                [InputAction.CameraMoveLeft] = InputBinding.FromKey(new KeyChord(KeyCode.A)),
                [InputAction.CameraMoveRight] = InputBinding.FromKey(new KeyChord(KeyCode.D)),
                [InputAction.CameraRotateLeft] = InputBinding.FromKey(new KeyChord(KeyCode.LeftArrow)),
                [InputAction.CameraRotateRight] = InputBinding.FromKey(new KeyChord(KeyCode.RightArrow)),
                [InputAction.CameraRotateUp] = InputBinding.FromKey(new KeyChord(KeyCode.UpArrow)),
                [InputAction.CameraRotateDown] = InputBinding.FromKey(new KeyChord(KeyCode.DownArrow)),
                [InputAction.CameraZoomIn] = InputBinding.FromKey(new KeyChord(KeyCode.Equals)),
                [InputAction.CameraZoomOut] = InputBinding.FromKey(new KeyChord(KeyCode.Minus)),
                [InputAction.CameraFocusSelection] = InputBinding.FromKey(new KeyChord(KeyCode.F)),

                [InputAction.DeleteSelected] = InputBinding.FromKey(new KeyChord(KeyCode.Delete)),
                [InputAction.DuplicateSelected] = InputBinding.FromKey(new KeyChord(KeyCode.D, ctrl: true)),
                [InputAction.SaveProject] = InputBinding.FromKey(new KeyChord(KeyCode.S, ctrl: true)),
                [InputAction.Undo] = InputBinding.FromKey(new KeyChord(KeyCode.Z, ctrl: true)),
                [InputAction.Redo] = InputBinding.FromKey(new KeyChord(KeyCode.Y, ctrl: true)),
                [InputAction.ActivateSelected] = InputBinding.FromKey(new KeyChord(KeyCode.E)),
                [InputAction.DragAxisLockX] = InputBinding.FromKey(new KeyChord(KeyCode.X)),
                [InputAction.DragAxisLockZ] = InputBinding.FromKey(new KeyChord(KeyCode.Z)),

                [InputAction.CatalogOpenSearch] = InputBinding.FromKey(new KeyChord(KeyCode.Slash)),

                [InputAction.ViewTop] = InputBinding.FromKey(new KeyChord(KeyCode.Alpha1)),
                [InputAction.ViewSide] = InputBinding.FromKey(new KeyChord(KeyCode.Alpha2)),
                [InputAction.ViewFront] = InputBinding.FromKey(new KeyChord(KeyCode.Alpha3)),

                [InputAction.ToggleHelp] = InputBinding.FromKey(new KeyChord(KeyCode.F1)),
                [InputAction.TogglePhotoMode] = InputBinding.FromKey(new KeyChord(KeyCode.F10)),
                [InputAction.ErrorPanelCopy] = InputBinding.FromKey(new KeyChord(KeyCode.C, ctrl: true)),
                [InputAction.ErrorPanelSelectAll] = InputBinding.FromKey(new KeyChord(KeyCode.A, ctrl: true)),

                [InputAction.PerfMonitorToggle] = InputBinding.FromKey(new KeyChord(KeyCode.F9)),
                [InputAction.PerfMonitorToggleRecording] =
                    InputBinding.FromKey(new KeyChord(KeyCode.F9, shift: true)),
                [InputAction.ToggleDevConsole] = InputBinding.FromKey(new KeyChord(KeyCode.BackQuote)),

                [InputAction.CameraOrbit] =
                    InputBinding.FromGesture(new MouseGesture(MouseButtonKind.Right, withMotion: true)),
                [InputAction.CameraPan] =
                    InputBinding.FromGesture(new MouseGesture(MouseButtonKind.Middle, withMotion: true)),
                [InputAction.CameraZoomWheel] = InputBinding.FromGesture(MouseGesture.Wheel()),
                [InputAction.SelectClick] =
                    InputBinding.FromGesture(new MouseGesture(MouseButtonKind.Left)),
                [InputAction.SelectMultiClick] =
                    InputBinding.FromGesture(new MouseGesture(MouseButtonKind.Left, ctrl: true)),

                [InputAction.LevelUp] = InputBinding.FromKey(new KeyChord(KeyCode.PageUp)),
                [InputAction.LevelDown] = InputBinding.FromKey(new KeyChord(KeyCode.PageDown)),
            };

            var table = new InputBinding[InputActionCatalog.All.Length];
            foreach (var pair in byAction) table[(int)pair.Key] = pair.Value;
            return table;
        }

        private static InputBinding[] BuildAlt()
        {
            var byAction = new Dictionary<InputAction, InputBinding>
            {
                [InputAction.CameraZoomIn] = InputBinding.FromKey(new KeyChord(KeyCode.KeypadPlus)),
                [InputAction.CameraZoomOut] = InputBinding.FromKey(new KeyChord(KeyCode.KeypadMinus)),
                [InputAction.Redo] = InputBinding.FromKey(new KeyChord(KeyCode.Z, ctrl: true, shift: true)),
            };

            var table = new InputBinding[InputActionCatalog.All.Length];
            foreach (var pair in byAction) table[(int)pair.Key] = pair.Value;
            return table;
        }

        public static InputBinding PrimaryOf(InputAction action) => Primary[(int)action];

        public static InputBinding AltOf(InputAction action) => Alt[(int)action];
    }
}
