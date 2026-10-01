using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public class UIManager : MonoBehaviour, IToolbarHost
    {
        public static UIManager? Instance { get; private set; }

        private ToolbarUI _toolbar = new();
        private readonly List<Component> _interfaceComponents = new();
        private bool _languageRebuildPending;
        private readonly Dictionary<ToolbarPanel, IProjectWindow> _panels = new();

        private static readonly Dictionary<ToolbarPanel, ToolbarPanel> ClosedWhenOpened = new()
        {
            { ToolbarPanel.Specification, ToolbarPanel.Settings },
            { ToolbarPanel.Settings, ToolbarPanel.Specification },
        };

        private Canvas? _canvas;
        private ContextMenuUI? _contextMenu;
        private GroupMenuUI? _groupMenu;
        private SettingsPanelUI? _settingsPanel;
        private PlacementController? _placement;
        private ElementSpawner? _spawner;
        private ProjectFileActions? _fileActions;

        public Canvas? Canvas => _canvas;
        public int IssueBadgeRevision => _toolbar.IssueBadgeRevision;
        public const string QuickSaveName = ProjectFileActions.QuickSaveName;

        private void Awake()
        {
            Instance = this;
            BuildInterface();
            _placement = gameObject.AddComponent<PlacementController>();
            Loc.LanguageChanged += OnLanguageChanged;
        }

        private void BuildInterface()
        {
            OsFontFallback.ApplyFor(Loc.Language, UIFactory.FontAsset);
            var before = new HashSet<Component>(GetComponents<Component>());
            BuildPanels();
            foreach (var component in GetComponents<Component>())
                if (!before.Contains(component)) _interfaceComponents.Add(component);
        }

        private void BuildPanels()
        {
            _canvas = UIFactory.CreateCanvas("UICanvas");
            _toolbar.Build(_canvas.transform, this);
            PerfHud.ToolbarBottomY = ToolbarUI.BarHeight;

            var specPanel = gameObject.AddComponent<SpecificationPanelUI>();
            specPanel.Build(_canvas.transform);
            _panels[ToolbarPanel.Specification] = specPanel;

            _settingsPanel = gameObject.AddComponent<SettingsPanelUI>();
            _settingsPanel.Build(_canvas.transform);
            _panels[ToolbarPanel.Settings] = _settingsPanel;

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

            var musicPanel = gameObject.AddComponent<MusicPanelUI>();
            musicPanel.Build(windowLayer);
            _panels[ToolbarPanel.Music] = musicPanel;

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

            var loadProjectWindow = gameObject.AddComponent<LoadProjectWindowUI>();
            loadProjectWindow.Build(windowLayer);
            _panels[ToolbarPanel.LoadProject] = loadProjectWindow;

            var levelsWindow = gameObject.AddComponent<LevelsWindowUI>();
            levelsWindow.Build(windowLayer);
            _panels[ToolbarPanel.Levels] = levelsWindow;

            var measureProperties = gameObject.AddComponent<MeasurePropertiesUI>();
            measureProperties.Build(windowLayer);

            var measureLabels = gameObject.AddComponent<MeasureLabelsUI>();
            measureLabels.Build(_canvas.transform);

            var toast = gameObject.AddComponent<ToastNotification>();
            toast.Build(_canvas.transform);
            PhotoLookMigrationNotice.ShowIfPending();

            var statusBar = gameObject.AddComponent<StatusBarUI>();
            statusBar.Build(_canvas.transform);

            var moduleBanner = gameObject.AddComponent<ModuleEditBannerUI>();
            moduleBanner.Build(_canvas.transform);

            var demoDialog = gameObject.AddComponent<DemoModeDialogUI>();
            demoDialog.Build(_canvas.transform);

            var newerVersionDialog = gameObject.AddComponent<NewerVersionDialogUI>();
            newerVersionDialog.Build(_canvas.transform);
        }

        private void OnDestroy()
        {
            Loc.LanguageChanged -= OnLanguageChanged;
            _toolbar.Dispose();
        }

        private void OnLanguageChanged() => _languageRebuildPending = true;

        internal bool LanguageRebuildPending => _languageRebuildPending;

        private IEnumerator RebuildInterfaceInCurrentLanguage()
        {
            var reopen = InterfaceReopenState.Capture(this);
            TearDownInterface();
            yield return null;
            _toolbar = new ToolbarUI();
            BuildInterface();
            reopen.Restore(this);
        }

        private void TearDownInterface()
        {
            _toolbar.Dispose();
            foreach (var component in _interfaceComponents)
                DestroyNow.The(component);
            _interfaceComponents.Clear();
            _panels.Clear();
            if (_canvas != null) DestroyNow.The(_canvas.gameObject);
            _canvas = null;
        }

        internal IEnumerable<ToolbarPanel> VisiblePanels()
        {
            foreach (var kv in _panels)
                if (kv.Value.IsVisible) yield return kv.Key;
        }

        internal ContextMenuUI? ContextMenu => _contextMenu;

        internal SettingsPanelUI? SettingsPanel => _settingsPanel;

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
            if (_languageRebuildPending)
            {
                _languageRebuildPending = false;
                StartCoroutine(RebuildInterfaceInCurrentLanguage());
                return;
            }
            if (_canvas != null) _toolbar.Refresh();
        }

        public void SpawnPreset(int index) => Spawner.SpawnPreset(index);

        private Vector3 GroundPointInFrontOfCamera()
        {
            var cam = Camera.main;
            if (cam == null) return Vector3.zero;

            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            float levelY = LevelRegistry.CurrentFloorElevationMm * AppConstants.MM_TO_UNITS;
            var ground = new Plane(Vector3.up, new Vector3(0f, levelY, 0f));
            if (ground.Raycast(ray, out float enter))
                return ray.GetPoint(enter);
            return cam.transform.position + cam.transform.forward * 2f;
        }

        public void OpenContextMenu(KitchenElement element)
        {
            if (_contextMenu != null)
                _contextMenu.Open(element);
        }

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
            _settingsPanel?.OpenControlsTab();
        }
    }
}
