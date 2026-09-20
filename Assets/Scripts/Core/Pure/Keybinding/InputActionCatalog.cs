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

        private static readonly InputActionInfo[] Table = BuildTable();

        private static InputActionInfo[] BuildTable()
        {
            var byAction = new Dictionary<InputAction, InputActionInfo>
            {
                [InputAction.CameraMoveForward] =
                    new InputActionInfo(InputActionGroup.Camera, "Камера: вперёд"),
                [InputAction.CameraMoveBack] =
                    new InputActionInfo(InputActionGroup.Camera, "Камера: назад"),
                [InputAction.CameraMoveLeft] =
                    new InputActionInfo(InputActionGroup.Camera, "Камера: влево"),
                [InputAction.CameraMoveRight] =
                    new InputActionInfo(InputActionGroup.Camera, "Камера: вправо"),
                [InputAction.CameraRotateLeft] =
                    new InputActionInfo(InputActionGroup.Camera, "Камера: поворот влево"),
                [InputAction.CameraRotateRight] =
                    new InputActionInfo(InputActionGroup.Camera, "Камера: поворот вправо"),
                [InputAction.CameraRotateUp] =
                    new InputActionInfo(InputActionGroup.Camera, "Камера: поворот вверх"),
                [InputAction.CameraRotateDown] =
                    new InputActionInfo(InputActionGroup.Camera, "Камера: поворот вниз"),
                [InputAction.CameraZoomIn] =
                    new InputActionInfo(InputActionGroup.Camera, "Камера: приблизить"),
                [InputAction.CameraZoomOut] =
                    new InputActionInfo(InputActionGroup.Camera, "Камера: отдалить"),
                [InputAction.CameraFocusSelection] =
                    new InputActionInfo(InputActionGroup.Camera, "Камера: фокус на выделенном"),

                [InputAction.DeleteSelected] =
                    new InputActionInfo(InputActionGroup.SelectionAndEditing, "Удалить объект(ы)"),
                [InputAction.DuplicateSelected] =
                    new InputActionInfo(InputActionGroup.SelectionAndEditing, "Дублировать"),
                [InputAction.SaveProject] =
                    new InputActionInfo(InputActionGroup.SelectionAndEditing, "Сохранить проект"),
                [InputAction.Undo] =
                    new InputActionInfo(InputActionGroup.SelectionAndEditing, "Отменить"),
                [InputAction.Redo] =
                    new InputActionInfo(InputActionGroup.SelectionAndEditing, "Повторить"),
                [InputAction.ActivateSelected] =
                    new InputActionInfo(InputActionGroup.SelectionAndEditing,
                        "Открыть/закрыть дверь, окно, фасад; переключить выключатель"),
                [InputAction.DragAxisLockX] =
                    new InputActionInfo(InputActionGroup.SelectionAndEditing,
                        "Перетаскивание: ограничить осью X"),
                [InputAction.DragAxisLockZ] =
                    new InputActionInfo(InputActionGroup.SelectionAndEditing,
                        "Перетаскивание: ограничить осью Z"),

                [InputAction.CatalogOpenSearch] =
                    new InputActionInfo(InputActionGroup.Catalog, "Раскрыть каталог и перейти в поиск"),

                [InputAction.ViewTop] =
                    new InputActionInfo(InputActionGroup.Views, "Вид сверху"),
                [InputAction.ViewSide] =
                    new InputActionInfo(InputActionGroup.Views, "Вид сбоку"),
                [InputAction.ViewFront] =
                    new InputActionInfo(InputActionGroup.Views, "Вид спереди"),

                [InputAction.ToggleHelp] =
                    new InputActionInfo(InputActionGroup.WindowsAndHelp, "Показать справку"),
                [InputAction.TogglePhotoMode] =
                    new InputActionInfo(InputActionGroup.WindowsAndHelp, "Переключить фоторежим"),
                [InputAction.ErrorPanelCopy] =
                    new InputActionInfo(InputActionGroup.WindowsAndHelp,
                        "Панель ошибок: скопировать выделенное"),
                [InputAction.ErrorPanelSelectAll] =
                    new InputActionInfo(InputActionGroup.WindowsAndHelp,
                        "Панель ошибок: выделить всё"),

                [InputAction.PerfMonitorToggle] =
                    new InputActionInfo(InputActionGroup.Diagnostics,
                        "Замер производительности: вкл/выкл"),
                [InputAction.PerfMonitorToggleRecording] =
                    new InputActionInfo(InputActionGroup.Diagnostics,
                        "Замер производительности: запись в файл"),
                [InputAction.ToggleDevConsole] =
                    new InputActionInfo(InputActionGroup.Diagnostics,
                        "Консоль разработчика: показать/скрыть"),
            };

            var table = new InputActionInfo[All.Length];
            foreach (var pair in byAction) table[(int)pair.Key] = pair.Value;
            return table;
        }

        public static InputActionGroup GroupOf(InputAction action) => Table[(int)action].Group;

        public static string DisplayNameOf(InputAction action) => Table[(int)action].DisplayName;
    }
}
