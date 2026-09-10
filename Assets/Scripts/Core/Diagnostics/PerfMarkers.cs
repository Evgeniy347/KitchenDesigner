using System.Collections.Generic;
using System.Diagnostics;
using Unity.Profiling;

namespace KitchenDesigner.Core
{
    public static class PerfMarkers
    {
        private static readonly double MillisecondsPerTick = 1000.0 / Stopwatch.Frequency;

        private static readonly List<string> _namesFilledByEveryRegBelow = new();
        private static readonly List<long> _ticksThisFrame = new();

        public static bool Measuring { get; set; }

        private static PerfMarker Reg(string name)
        {
            int slot = _namesFilledByEveryRegBelow.Count;
            _namesFilledByEveryRegBelow.Add(name);
            _ticksThisFrame.Add(0L);
            return new PerfMarker(slot, new ProfilerMarker(name));
        }

        public static readonly PerfMarker CameraUpdate = Reg("CameraController.Update");
        public static readonly PerfMarker CameraFloorVisibility = Reg("CameraController.UpdateFloorVisibility");
        public static readonly PerfMarker CameraPointerOverUI = Reg("CameraController.PointerOverUI");
        public static readonly PerfMarker FrameRateUpdate = Reg("FrameRateManager.Update");
        public static readonly PerfMarker FrameRateDetectInput = Reg("FrameRateManager.DetectInput");

        public static readonly PerfMarker WallManagerLateUpdate = Reg("WallManager.LateUpdate");
        public static readonly PerfMarker SceneVisibilityApply = Reg("SceneVisibilityManager.Apply");
        public static readonly PerfMarker PartRegistryGetAll = Reg("PartRegistry.GetAll");
        public static readonly PerfMarker WallSyncOpenings = Reg("Wall.SyncOpenings");

        public static readonly PerfMarker DrawerStepAnimation = Reg("DrawerElement.StepAnimation");
        public static readonly PerfMarker DrawerSyncToLower = Reg("DrawerElement.SyncToLower");
        public static readonly PerfMarker DrawerFindPaired = Reg("DrawerElement.FindPaired");
        public static readonly PerfMarker SinkSnapToPart = Reg("SinkElement.SnapToPart");
        public static readonly PerfMarker FacadeStepDoor = Reg("FacadeElement.StepDoor");
        public static readonly PerfMarker AttachRiderStep = Reg("AttachRider.Step");

        public static readonly PerfMarker ElementOutlineLateUpdate = Reg("ElementOutline.LateUpdate");
        public static readonly PerfMarker TextureOverlaySyncAll = Reg("TextureOverlayRenderer.SyncAll");
        public static readonly PerfMarker EdgeSubstrateSync = Reg("EdgeSubstrate.SyncScene");
        public static readonly PerfMarker ResizeHandlesLateUpdate = Reg("ResizeHandleManager.LateUpdate");
        public static readonly PerfMarker MeasureLabelsLateUpdate = Reg("MeasureLabelsUI.LateUpdate");

        public static readonly PerfMarker UIManagerUpdate = Reg("UIManager.Update");
        public static readonly PerfMarker ContextMenuUpdate = Reg("ContextMenuUI.Update");
        public static readonly PerfMarker HierarchyPanelUpdate = Reg("HierarchyPanelUI.Update");
        public static readonly PerfMarker SidebarUpdate = Reg("SidebarUI.Update");

        public static readonly PerfMarker FacadeApplyDoor = Reg("FacadeElement.ApplyDoor");
        public static readonly PerfMarker OpeningFindMaxProgress = Reg("OpeningCollision.FindMaxProgress");
        public static readonly PerfMarker OpeningBuildObstacles = Reg("OpeningCollision.BuildObstacles");
        public static readonly PerfMarker OpeningScanForBlock = Reg("OpeningCollision.ScanForBlock");
        public static readonly PerfMarker AttachLinksDescendants = Reg("AttachLinks.Descendants");

        public static readonly PerfMarker SceneChangeTrackerPoll = Reg("SceneChangeTracker.Poll");
        public static readonly PerfMarker SettleDerivedLinks = Reg("SceneChangeTracker.SettleDerivedLinks");
        public static readonly PerfMarker ScrewLegHostLinkApplyAll = Reg("ScrewLegHostLink.ApplyAll");
        public static readonly PerfMarker PipeFittingSizeLinkApplyAll = Reg("PipeFittingSizeLink.ApplyAll");

        public static readonly PerfMarker SceneAnalyzerAnalyze = Reg("SceneAnalyzer.Analyze");
        public static readonly PerfMarker ErrorPanelAnalyze = Reg("ErrorPanelUI.Analyze");
        public static readonly PerfMarker ToolbarRefresh = Reg("ToolbarUI.Refresh");

        public static readonly ProfilerMarker DoorSnapToWall = new ProfilerMarker("DoorElement.SnapToWall");
        public static readonly ProfilerMarker WindowSnapToWall = new ProfilerMarker("WindowElement.SnapToWall");

        public static IReadOnlyList<string> NamesInDeclarationOrder => _namesFilledByEveryRegBelow;

        internal static void AddTicks(int slot, long ticks) => _ticksThisFrame[slot] += ticks;

        public static float TakeFrameMs(int slot)
        {
            long ticks = _ticksThisFrame[slot];
            _ticksThisFrame[slot] = 0L;
            return (float)(ticks * MillisecondsPerTick);
        }

        public static void DropEverythingMeasuredSoFar()
        {
            for (int i = 0; i < _ticksThisFrame.Count; i++) _ticksThisFrame[i] = 0L;
        }
    }
}
