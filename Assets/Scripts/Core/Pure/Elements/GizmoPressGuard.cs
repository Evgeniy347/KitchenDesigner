namespace KitchenDesigner.Core
{
    public static class GizmoPressGuard
    {
        public static bool BlocksPress(bool resizing, bool pointerOverResizeHandle,
            bool overlayActive, bool pointerOverOverlayHandle,
            bool draggingGroup, bool pointerOverGroupHandle) =>
            resizing
            || pointerOverResizeHandle
            || (overlayActive && pointerOverOverlayHandle)
            || draggingGroup
            || pointerOverGroupHandle;
    }
}
