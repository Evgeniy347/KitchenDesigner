namespace KitchenDesigner.Core.Keybinding
{
    public enum InputAction
    {
        CameraMoveForward,
        CameraMoveBack,
        CameraMoveLeft,
        CameraMoveRight,
        CameraRotateLeft,
        CameraRotateRight,
        CameraRotateUp,
        CameraRotateDown,
        CameraZoomIn,
        CameraZoomOut,
        CameraFocusSelection,

        DeleteSelected,
        DuplicateSelected,
        SaveProject,
        Undo,
        Redo,
        ActivateSelected,
        DragAxisLockX,
        DragAxisLockZ,

        CatalogOpenSearch,

        ViewTop,
        ViewSide,
        ViewFront,

        ToggleHelp,
        TogglePhotoMode,
        ErrorPanelCopy,
        ErrorPanelSelectAll,

        PerfMonitorToggle,
        PerfMonitorToggleRecording,
        ToggleDevConsole,
    }
}
