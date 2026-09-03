using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public class UIManager : MonoBehaviour, IToolbarHost
    {
        public static UIManager? Instance { get; private set; }

        private readonly ToolbarUI _toolbar = new();
        private readonly Dictionary<ToolbarPanel, IProjectWindow> _panels = new();

        private static readonly Dictionary<ToolbarPanel, ToolbarPanel> ClosedWhenOpened = new()
        {
            { ToolbarPanel.Specification, ToolbarPanel.Settings },
            { ToolbarPanel.Settings, ToolbarPanel.Specification },
        };

        private Canvas? _canvas;
        private ContextMenuUI? _contextMenu;
        private GroupMenuUI? _groupMenu;
        private HelpUI? _help;
        private PlacementController? _placement;
        private ElementSpawner? _spawner;
        private ProjectFileActions? _fileActions;

        public Canvas? Canvas => _canvas;
        public const string QuickSaveName = ProjectFileActions.QuickSaveName;

        private void Awake()
        {
            Instance = this;

            _canvas = UIFactory.CreateCanvas("UICanvas");
            _toolbar.Build(_canvas.transform, this);

            var specPanel = gameObject.AddComponent<SpecificationPanelUI>();
            specPanel.Build(_canvas.transform);
            _panels[ToolbarPanel.Specification] = specPanel;

            var settingsPanel = gameObject.AddComponent<SettingsPanelUI>();
            settingsPanel.Build(_canvas.transform);
            _panels[ToolbarPanel.Settings] = settingsPanel;

            var sidebar = gameObject.AddComponent<SidebarUI>();
            sidebar.Build(_canvas.transform);

            var windowLayer = UIFactory.CreateRect("WindowLayer", _canvas.transform);
            windowLayer.anchorMin = Vector2.zero;
            windowLayer.anchorMax = Vector2.one;
            windowLayer.offsetMin = windowLayer.offsetMax = Vector2.zero;

            _contextMenu = gameObject.AddComponent<ContextMenuUI>();
            _contextMenu.Build(windowLayer);

            var dayNightPanel = gameObject.AddComponent<DayNightPanelUI>();
            dayNightPanel.Build(windowLayer);
            _panels[ToolbarPanel.DayNight] = dayNightPanel;

            _groupMenu = gameObject.AddComponent<GroupMenuUI>();
            _groupMenu.Build(windowLayer);

            var hierarchyPanel = gameObject.AddComponent<HierarchyPanelUI>();
            hierarchyPanel.Build(windowLayer);
            _panels[ToolbarPanel.Hierarchy] = hierarchyPanel;

            var errorPanel = gameObject.AddComponent<ErrorPanelUI>();
            errorPanel.Build(windowLayer);
            _panels[ToolbarPanel.Errors] = errorPanel;

            var projectInstructionsPanel = gameObject.AddComponent<ProjectInstructionsPanelUI>();
            projectInstructionsPanel.Build(windowLayer);
            _panels[ToolbarPanel.ProjectInstructions] = projectInstructionsPanel;

            var measureProperties = gameObject.AddComponent<MeasurePropertiesUI>();
            measureProperties.Build(windowLayer);

            var measureLabels = gameObject.AddComponent<MeasureLabelsUI>();
            measureLabels.Build(_canvas.transform);

            var toast = gameObject.AddComponent<ToastNotification>();
            toast.Build(_canvas.transform);

            var statusBar = gameObject.AddComponent<StatusBarUI>();
            statusBar.Build(_canvas.transform);

            var moduleBanner = gameObject.AddComponent<ModuleEditBannerUI>();
            moduleBanner.Build(_canvas.transform);

            _help = gameObject.AddComponent<HelpUI>();
            _help.Build(_canvas.transform);

            var demoDialog = gameObject.AddComponent<DemoModeDialogUI>();
            demoDialog.Build(_canvas.transform);

            _placement = gameObject.AddComponent<PlacementController>();
        }

        private void OnDestroy() => _toolbar.Dispose();

        public void TogglePanel(ToolbarPanel panel)
        {
            if (!_panels.TryGetValue(panel, out var window)) return;
            if (ClosedWhenOpened.TryGetValue(panel, out var other)
                && _panels.TryGetValue(other, out var otherWindow))
                otherWindow.SetVisible(false);
            window.SetVisible(!window.IsVisible);
        }

        public bool IsPanelVisible(ToolbarPanel panel) =>
            _panels.TryGetValue(panel, out var window) && window.IsVisible;

        public void ToggleSpecification() => TogglePanel(ToolbarPanel.Specification);

        public void ToggleSettings() => TogglePanel(ToolbarPanel.Settings);

        public void ToggleHierarchy() => TogglePanel(ToolbarPanel.Hierarchy);

        public void ToggleErrors() => TogglePanel(ToolbarPanel.Errors);

        internal ElementSpawner Spawner =>
            _spawner ??= new ElementSpawner(GroundPointInFrontOfCamera, () => _placement);

        private ProjectFileActions FileActions =>
            _fileActions ??= new ProjectFileActions();

        private void Update()
        {
            using var _ = PerfMarkers.UIManagerUpdate.Auto();
            _toolbar.Refresh();
        }

        public void SpawnPreset(int index) => Spawner.SpawnPreset(index);

        public void SpawnBoard(Vector3Int dims) =>
            SpawnBoard(dims, $"Board {dims.x}x{dims.y}x{dims.z}");

        public void SpawnBoard(Vector3Int dims, string name) => Spawner.SpawnBoard(dims, name);

        public void SpawnFacade(Vector3Int dims, string name,
            int gapLeft = 2, int gapRight = 2, int gapTop = 2, int gapBottom = 2) =>
            Spawner.SpawnFacade(dims, name, gapLeft, gapRight, gapTop, gapBottom);

        public void SpawnAssembledFacade(Vector3Int dims, string name,
            AssembledFill fill = AssembledFill.Blind) =>
            Spawner.SpawnAssembledFacade(dims, name, fill);

        public void SpawnWall(Vector3Int dims, string name) => Spawner.SpawnWall(dims, name);

        public void SpawnDrawer(string drawerType, int length, string colorName, int width, string name,
            DrawerSystem system = DrawerSystem.Gtv) =>
            Spawner.SpawnDrawer(drawerType, length, colorName, width, name, system);

        public void SpawnTable(Vector3Int dims, string name) => Spawner.SpawnTable(dims, name);

        public void SpawnRadiusTable(Vector3Int dims, string name) => Spawner.SpawnRadiusTable(dims, name);

        public void SpawnStool(Vector3Int dims, string name) => Spawner.SpawnStool(dims, name);
        public void SpawnChair(Vector3Int dims, string name) => Spawner.SpawnChair(dims, name);
        public void SpawnSofa(Vector3Int dims, string name) => Spawner.SpawnSofa(dims, name);

        public void SpawnBed(Vector3Int dims, string name) => Spawner.SpawnBed(dims, name);

        public void SpawnPouffe(Vector3Int dims, string name) => Spawner.SpawnPouffe(dims, name);

        public void SpawnToilet(string name) => Spawner.SpawnToilet(name);

        public void SpawnWallHungToilet(string name) => Spawner.SpawnWallHungToilet(name);

        public void SpawnBathtub(Vector3Int dims, string name) => Spawner.SpawnBathtub(dims, name);

        public void SpawnBathMixer(string name) => Spawner.SpawnBathMixer(name);

        public void SpawnShowerColumn(string name) => Spawner.SpawnShowerColumn(name);

        public void SpawnPanel(Vector3Int dims, string name,
            int gapLeft = PanelElement.DEFAULT_GAP_MM, int gapRight = PanelElement.DEFAULT_GAP_MM,
            int gapTop = PanelElement.DEFAULT_GAP_MM, int gapBottom = PanelElement.DEFAULT_GAP_MM) =>
            Spawner.SpawnPanel(dims, name, gapLeft, gapRight, gapTop, gapBottom);

        public void SpawnRadialShelf(Vector3Int dims, string name) => Spawner.SpawnRadialShelf(dims, name);

        public void SpawnWindow(Vector3Int dims, string name) => Spawner.SpawnWindow(dims, name);

        public void SpawnDoor(Vector3Int dims, string name) => Spawner.SpawnDoor(dims, name);

        public void SpawnPillar(int midHeightMM, string name) => Spawner.SpawnPillar(midHeightMM, name);

        public void SpawnScrewLeg(string name) => Spawner.SpawnScrewLeg(name);

        private Vector3 GroundPointInFrontOfCamera()
        {
            var cam = Camera.main;
            if (cam == null) return Vector3.zero;

            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            var ground = new Plane(Vector3.up, Vector3.zero);
            if (ground.Raycast(ray, out float enter))
                return ray.GetPoint(enter);
            return cam.transform.position + cam.transform.forward * 2f;
        }

        public void OpenContextMenu(KitchenElement element)
        {
            if (_contextMenu != null)
                _contextMenu.Open(element);
        }

        public void SpawnFloor(Vector3Int dims, string name) => Spawner.SpawnFloor(dims, name);

        public void SpawnSink(string name) => Spawner.SpawnSink(name);

        public void SpawnCooktop(string name, string model = "") => Spawner.SpawnCooktop(name, model);

        public void SpawnOven(string name) => Spawner.SpawnOven(name);

        public void SpawnDishwasher(string name) => Spawner.SpawnDishwasher(name);

        public void SpawnLightSource(string name) => Spawner.SpawnLightSource(name);

        public void OpenGroupMenu(KitchenElement element)
        {
            if (_groupMenu != null)
                _groupMenu.Open(element);
        }

        public void SaveCurrent() => FileActions.SaveCurrent();

        public void SaveAs() => FileActions.SaveAs();

        public void LoadDialog() => FileActions.LoadDialog();

        public void ToggleHelp()
        {
            if (_help != null) _help.Toggle();
        }
    }
}
