using System.Collections.Generic;
using Unity.Profiling;

namespace KitchenDesigner.Core
{
    public static class PerfMarkers
    {
        private static readonly List<string> _namesFilledByEveryRegBelow = new();

        private static ProfilerMarker Reg(string name)
        {
            _namesFilledByEveryRegBelow.Add(name);
            return new ProfilerMarker(name);
        }

        public static readonly ProfilerMarker CameraUpdate = Reg("CameraController.Update");
        public static readonly ProfilerMarker CameraFloorVisibility = Reg("CameraController.UpdateFloorVisibility");
        public static readonly ProfilerMarker CameraPointerOverUI = Reg("CameraController.PointerOverUI");
        public static readonly ProfilerMarker FrameRateUpdate = Reg("FrameRateManager.Update");
        public static readonly ProfilerMarker FrameRateDetectInput = Reg("FrameRateManager.DetectInput");

        public static readonly ProfilerMarker WallManagerLateUpdate = Reg("WallManager.LateUpdate");
        public static readonly ProfilerMarker SceneVisibilityApply = Reg("SceneVisibilityManager.Apply");
        public static readonly ProfilerMarker PartRegistryGetAll = Reg("PartRegistry.GetAll");
        public static readonly ProfilerMarker WallSyncOpenings = Reg("Wall.SyncOpenings");

        public static readonly ProfilerMarker DrawerStepAnimation = Reg("DrawerElement.StepAnimation");
        public static readonly ProfilerMarker DrawerSyncToLower = Reg("DrawerElement.SyncToLower");
        public static readonly ProfilerMarker DrawerFindPaired = Reg("DrawerElement.FindPaired");
        public static readonly ProfilerMarker SinkSnapToPart = Reg("SinkElement.SnapToPart");
        public static readonly ProfilerMarker DoorSnapToWall = Reg("DoorElement.SnapToWall");
        public static readonly ProfilerMarker WindowSnapToWall = Reg("WindowElement.SnapToWall");
        public static readonly ProfilerMarker FacadeStepDoor = Reg("FacadeElement.StepDoor");
        public static readonly ProfilerMarker AttachRiderStep = Reg("AttachRider.Step");

        public static readonly ProfilerMarker ElementOutlineLateUpdate = Reg("ElementOutline.LateUpdate");
        public static readonly ProfilerMarker TextureOverlaySyncAll = Reg("TextureOverlayRenderer.SyncAll");
        public static readonly ProfilerMarker EdgeSubstrateSync = Reg("EdgeSubstrate.SyncScene");
        public static readonly ProfilerMarker ResizeHandlesLateUpdate = Reg("ResizeHandleManager.LateUpdate");
        public static readonly ProfilerMarker MeasureLabelsLateUpdate = Reg("MeasureLabelsUI.LateUpdate");

        public static readonly ProfilerMarker UIManagerUpdate = Reg("UIManager.Update");
        public static readonly ProfilerMarker ContextMenuUpdate = Reg("ContextMenuUI.Update");
        public static readonly ProfilerMarker HierarchyPanelUpdate = Reg("HierarchyPanelUI.Update");
        public static readonly ProfilerMarker SidebarUpdate = Reg("SidebarUI.Update");

        public static IReadOnlyList<string> NamesInDeclarationOrder => _namesFilledByEveryRegBelow;
    }
}
