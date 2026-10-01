using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core.Keybinding
{
    public readonly struct InputActionInfo
    {
        public readonly InputActionGroup Group;
        public readonly string DisplayName;

        public InputActionInfo(InputActionGroup group, string displayName)
        {
            Group = group;
            DisplayName = displayName;
        }
    }

    public static class InputActionCatalog
    {
        public static readonly InputAction[] All = (InputAction[])Enum.GetValues(typeof(InputAction));

        private static readonly LocalizedCache<InputActionInfo[]> TableCache =
            new LocalizedCache<InputActionInfo[]>(() => BuildTable());

        private static InputActionInfo[] Table => TableCache.Value;

        private static InputActionInfo[] BuildTable()
        {
            var byAction = new Dictionary<InputAction, InputActionInfo>
            {
                [InputAction.CameraMoveForward] =
                    new InputActionInfo(InputActionGroup.Camera, Loc.T("input.action.cameraForward")),
                [InputAction.CameraMoveBack] =
                    new InputActionInfo(InputActionGroup.Camera, Loc.T("input.action.cameraBack")),
                [InputAction.CameraMoveLeft] =
                    new InputActionInfo(InputActionGroup.Camera, Loc.T("input.action.cameraLeft")),
                [InputAction.CameraMoveRight] =
                    new InputActionInfo(InputActionGroup.Camera, Loc.T("input.action.cameraRight")),
                [InputAction.CameraRotateLeft] =
                    new InputActionInfo(InputActionGroup.Camera, Loc.T("input.action.cameraRotateLeft")),
                [InputAction.CameraRotateRight] =
                    new InputActionInfo(InputActionGroup.Camera, Loc.T("input.action.cameraRotateRight")),
                [InputAction.CameraRotateUp] =
                    new InputActionInfo(InputActionGroup.Camera, Loc.T("input.action.cameraRotateUp")),
                [InputAction.CameraRotateDown] =
                    new InputActionInfo(InputActionGroup.Camera, Loc.T("input.action.cameraRotateDown")),
                [InputAction.CameraZoomIn] =
                    new InputActionInfo(InputActionGroup.Camera, Loc.T("input.action.cameraZoomIn")),
                [InputAction.CameraZoomOut] =
                    new InputActionInfo(InputActionGroup.Camera, Loc.T("input.action.cameraZoomOut")),
                [InputAction.CameraFocusSelection] =
                    new InputActionInfo(InputActionGroup.Camera, Loc.T("input.action.cameraFocusSelection")),

                [InputAction.DeleteSelected] =
                    new InputActionInfo(InputActionGroup.SelectionAndEditing, Loc.T("input.action.deleteSelected")),
                [InputAction.DuplicateSelected] =
                    new InputActionInfo(InputActionGroup.SelectionAndEditing, Loc.T("input.action.duplicate")),
                [InputAction.SaveProject] =
                    new InputActionInfo(InputActionGroup.SelectionAndEditing, Loc.T("input.action.saveProject")),
                [InputAction.Undo] =
                    new InputActionInfo(InputActionGroup.SelectionAndEditing, Loc.T("input.action.undo")),
                [InputAction.Redo] =
                    new InputActionInfo(InputActionGroup.SelectionAndEditing, Loc.T("input.action.redo")),
                [InputAction.ActivateSelected] =
                    new InputActionInfo(InputActionGroup.SelectionAndEditing,
                        Loc.T("input.action.activateSelected")),
                [InputAction.DragAxisLockX] =
                    new InputActionInfo(InputActionGroup.SelectionAndEditing,
                        Loc.T("input.action.dragLockX")),
                [InputAction.DragAxisLockZ] =
                    new InputActionInfo(InputActionGroup.SelectionAndEditing,
                        Loc.T("input.action.dragLockZ")),

                [InputAction.CatalogOpenSearch] =
                    new InputActionInfo(InputActionGroup.Catalog, Loc.T("input.action.catalogSearch")),

                [InputAction.ViewTop] =
                    new InputActionInfo(InputActionGroup.Views, Loc.T("input.action.viewTop")),
                [InputAction.ViewSide] =
                    new InputActionInfo(InputActionGroup.Views, Loc.T("input.action.viewSide")),
                [InputAction.ViewFront] =
                    new InputActionInfo(InputActionGroup.Views, Loc.T("input.action.viewFront")),

                [InputAction.ToggleHelp] =
                    new InputActionInfo(InputActionGroup.WindowsAndHelp, Loc.T("input.action.showHelp")),
                [InputAction.TogglePhotoMode] =
                    new InputActionInfo(InputActionGroup.WindowsAndHelp, Loc.T("input.action.togglePhotoMode")),
                [InputAction.ErrorPanelCopy] =
                    new InputActionInfo(InputActionGroup.WindowsAndHelp,
                        Loc.T("input.action.errorsCopy")),
                [InputAction.ErrorPanelSelectAll] =
                    new InputActionInfo(InputActionGroup.WindowsAndHelp,
                        Loc.T("input.action.errorsSelectAll")),

                [InputAction.PerfMonitorToggle] =
                    new InputActionInfo(InputActionGroup.Diagnostics,
                        Loc.T("input.action.perfToggle")),
                [InputAction.PerfMonitorToggleRecording] =
                    new InputActionInfo(InputActionGroup.Diagnostics,
                        Loc.T("input.action.perfRecord")),
                [InputAction.ToggleDevConsole] =
                    new InputActionInfo(InputActionGroup.Diagnostics,
                        Loc.T("input.action.devConsole")),

                [InputAction.CameraOrbit] =
                    new InputActionInfo(InputActionGroup.Mouse, Loc.T("input.action.cameraOrbit")),
                [InputAction.CameraPan] =
                    new InputActionInfo(InputActionGroup.Mouse, Loc.T("input.action.cameraPan")),
                [InputAction.CameraZoomWheel] =
                    new InputActionInfo(InputActionGroup.Mouse, Loc.T("input.action.cameraZoomWheel")),
                [InputAction.SelectClick] =
                    new InputActionInfo(InputActionGroup.Mouse, Loc.T("input.action.selectClick")),
                [InputAction.SelectMultiClick] =
                    new InputActionInfo(InputActionGroup.Mouse, Loc.T("input.action.selectMultiClick")),

                [InputAction.LevelUp] =
                    new InputActionInfo(InputActionGroup.Levels, Loc.T("input.action.levelUp")),
                [InputAction.LevelDown] =
                    new InputActionInfo(InputActionGroup.Levels, Loc.T("input.action.levelDown")),
            };

            var table = new InputActionInfo[All.Length];
            foreach (var pair in byAction) table[(int)pair.Key] = pair.Value;
            return table;
        }

        public static InputActionGroup GroupOf(InputAction action) => Table[(int)action].Group;

        public static string DisplayNameOf(InputAction action) => Table[(int)action].DisplayName;
    }
}
