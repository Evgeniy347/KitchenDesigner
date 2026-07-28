using System.Collections.Generic;
using Unity.Profiling;

namespace KitchenDesigner.Core
{
    /// <summary>Единственный источник правды по маркерам профилировщика: и сам маркер,
    /// и его имя объявляются здесь одной строкой, поэтому <see cref="PerfMonitor"/> не
    /// нужно править при добавлении нового замера.
    ///
    /// Не под <c>#if</c>: маркеры стоят в боевом коде, а в релизной сборке
    /// <c>ProfilerMarker.Auto()</c> и так вырождается в пустышку. Обращение к
    /// <see cref="Names"/> прогревает класс целиком — после него все маркеры
    /// зарегистрированы, и ProfilerRecorder к ним цепляется сразу.</summary>
    public static class PerfMarkers
    {
        // ВАЖНО: список объявляется до маркеров — инициализаторы полей выполняются
        // в порядке объявления, и Reg() пишет в уже созданный список.
        private static readonly List<string> _names = new();

        private static ProfilerMarker Reg(string name)
        {
            _names.Add(name);
            return new ProfilerMarker(name);
        }

        // ── Камера и кадровый режим ──────────────────────────────────────
        public static readonly ProfilerMarker CameraUpdate = Reg("CameraController.Update");
        public static readonly ProfilerMarker CameraFloorVisibility = Reg("CameraController.UpdateFloorVisibility");
        public static readonly ProfilerMarker CameraPointerOverUI = Reg("CameraController.PointerOverUI");
        public static readonly ProfilerMarker FrameRateUpdate = Reg("FrameRateManager.Update");
        public static readonly ProfilerMarker FrameRateDetectInput = Reg("FrameRateManager.DetectInput");

        // ── Покадровое согласование сцены ────────────────────────────────
        public static readonly ProfilerMarker WallManagerLateUpdate = Reg("WallManager.LateUpdate");
        public static readonly ProfilerMarker SceneVisibilityApply = Reg("SceneVisibilityManager.Apply");
        public static readonly ProfilerMarker PartRegistryGetAll = Reg("PartRegistry.GetAll");
        public static readonly ProfilerMarker WallSyncOpenings = Reg("Wall.SyncOpenings");

        // ── Элементы со своим Update ─────────────────────────────────────
        public static readonly ProfilerMarker DrawerStepAnimation = Reg("DrawerElement.StepAnimation");
        public static readonly ProfilerMarker DrawerSyncToLower = Reg("DrawerElement.SyncToLower");
        public static readonly ProfilerMarker DrawerFindPaired = Reg("DrawerElement.FindPaired");
        public static readonly ProfilerMarker SinkSnapToPart = Reg("SinkElement.SnapToPart");
        public static readonly ProfilerMarker DoorSnapToWall = Reg("DoorElement.SnapToWall");
        public static readonly ProfilerMarker WindowSnapToWall = Reg("WindowElement.SnapToWall");
        public static readonly ProfilerMarker FacadeStepDoor = Reg("FacadeElement.StepDoor");

        // ── Рендер поверх сцены ──────────────────────────────────────────
        public static readonly ProfilerMarker ElementOutlineLateUpdate = Reg("ElementOutline.LateUpdate");
        public static readonly ProfilerMarker TextureOverlaySyncAll = Reg("TextureOverlayRenderer.SyncAll");
        public static readonly ProfilerMarker ResizeHandlesLateUpdate = Reg("ResizeHandleManager.LateUpdate");
        public static readonly ProfilerMarker MeasureLabelsLateUpdate = Reg("MeasureLabelsUI.LateUpdate");

        // ── UI ───────────────────────────────────────────────────────────
        public static readonly ProfilerMarker UIManagerUpdate = Reg("UIManager.Update");
        public static readonly ProfilerMarker ContextMenuUpdate = Reg("ContextMenuUI.Update");
        public static readonly ProfilerMarker HierarchyPanelUpdate = Reg("HierarchyPanelUI.Update");
        public static readonly ProfilerMarker SidebarUpdate = Reg("SidebarUI.Update");

        /// <summary>Имена всех маркеров в порядке объявления.</summary>
        public static IReadOnlyList<string> Names => _names;
    }
}
