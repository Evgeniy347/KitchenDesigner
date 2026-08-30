using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

        public Canvas? Canvas => _canvas;
        public const string QuickSaveName = "quicksave";

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

        private ElementSpawner Spawner =>
            _spawner ??= new ElementSpawner(GroundPointInFrontOfCamera, () => _placement);

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

        public void SpawnPanel(Vector3Int dims, string name,
            int gapLeft = PanelElement.DEFAULT_GAP_MM, int gapRight = PanelElement.DEFAULT_GAP_MM,
            int gapTop = PanelElement.DEFAULT_GAP_MM, int gapBottom = PanelElement.DEFAULT_GAP_MM) =>
            Spawner.SpawnPanel(dims, name, gapLeft, gapRight, gapTop, gapBottom);

        public void SpawnRadialShelf(Vector3Int dims, string name) => Spawner.SpawnRadialShelf(dims, name);

        public void SpawnWindow(Vector3Int dims, string name) => Spawner.SpawnWindow(dims, name);

        public void SpawnDoor(Vector3Int dims, string name) => Spawner.SpawnDoor(dims, name);

        public void SpawnPillar(int midHeightMM, string name) => Spawner.SpawnPillar(midHeightMM, name);

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

        /// <summary>Открыть контекстное меню детали (вызывается из CameraController по клику ПКМ).</summary>
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

        /// <summary>Открыть меню группы (вызывается из CameraController по ПКМ-клику).</summary>
        public void OpenGroupMenu(KitchenElement element)
        {
            if (_groupMenu != null)
                _groupMenu.Open(element);
        }

        /// <summary>«Сохранить»: на WebGL отправляет на сервер; на остальных
        /// платформах пишет в последний выбранный файл.</summary>
        public void SaveCurrent()
        {
#if UNITY_WEBGL
            ServerSave(forceNew: false);
#else
            if (SaveLoadManager.HasLastPath)
            {
                if (SaveLoadManager.SaveToLastPath())
                    ShowSaved(System.IO.Path.GetFileName(SaveLoadManager.LastPath));
            }
            else if (SaveLoadManager.SaveProject(QuickSaveName))
            {
                SaveLoadManager.LastPath = SaveLoadManager.PathForName(QuickSaveName);
                ShowSaved(QuickSaveName);
            }
#endif
        }

        /// <summary>«Сохранить как»: на WebGL — браузерное окно сохранения
        /// (скачивание файла); на остальных платформах — системный диалог Windows.</summary>
        public void SaveAs()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string suggested = WebFileDialog.SuggestedFileName(SaveLoadManager.LastPath);
            WebFileDialog.Save(SaveLoadManager.CaptureCurrentJson(), suggested,
                name => ShowSaved(name));
#else
            string suggested = SaveLoadManager.HasLastPath
                ? System.IO.Path.GetFileName(SaveLoadManager.LastPath)
                : "kitchen.json";
            string? path = NativeFileDialog.SaveDialog("Сохранить проект кухни",
                suggested, SaveLoadManager.LastDirectory);
            if (string.IsNullOrEmpty(path)) return;
            if (SaveLoadManager.SaveToPath(path))
                ShowSaved(System.IO.Path.GetFileName(path));
#endif
        }

        /// <summary>«Загрузить»: на WebGL — браузерное окно выбора файла, проект
        /// открывается прямо в Unity; на остальных платформах — системный диалог Windows.</summary>
        public void LoadDialog()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebFileDialog.Open((fileName, content) =>
            {
                var data = SaveLoadManager.Deserialize(content);
                if (data == null)
                {
                    Toast("Не удалось прочитать файл");
                    return;
                }
                SaveLoadManager.ClearBoards(PartRegistry.GetAll());
                SaveLoadManager.RestoreScene(data);
                if (ElementHighlighter.Instance != null)
                    ElementHighlighter.Instance.RefreshHighlights();
                Toast("Загружено: " + fileName);
            });
#else
            string? path = NativeFileDialog.OpenDialog("Открыть проект кухни",
                SaveLoadManager.LastDirectory);
            if (string.IsNullOrEmpty(path)) return;
            if (SaveLoadManager.LoadFromPath(path))
                Toast("Загружено: " + System.IO.Path.GetFileName(path));
#endif
        }

        private static void Toast(string msg)
        {
            ToastNotification.ShowIfAvailable(msg);
        }

        /// <summary>Показать «Сохранено: …» в статус-баре. Зелёный — нейтральный
        /// «всё хорошо». ToastNotification больше для этого не используется:
        /// он остался под прочие сообщения («Удалено», «Такой паз уже есть»).</summary>
        private static void ShowSaved(string name)
        {
            StatusBarUI.Instance?.ShowTransient("Сохранено: " + name,
                new Color(0.45f, 0.85f, 0.45f, 1f), 3f);
        }

        public void ToggleHelp()
        {
            if (_help != null) _help.Toggle();
        }

#if UNITY_WEBGL
        // ── Server project save (кнопка «Сохранить») ──────────────────────

        private GameObject? _namePromptPanel;
        private TMP_InputField? _nameInputField;

        private void ServerSave(bool forceNew)
        {
            if (!Networking.ProjectApiClient.Enabled)
            {
                LocalSave(forceNew);
                return;
            }

            string json = SaveLoadManager.CaptureCurrentJson();
            var api = Networking.ProjectApiClient.Instance;

            if (!forceNew && api!.HasCurrentProject)
            {
                api.SaveCurrent(json,
                    () => Toast("Сохранено: " + api.CurrentProjectName),
                    err => Toast("Ошибка сохранения: " + err));
            }
            else
            {
                string defaultName = api!.HasCurrentProject
                    ? api.CurrentProjectName
                    : "Новый проект";
                ShowNamePrompt(defaultName, name =>
                {
                    api.CreateAndSave(name, json, id =>
                    {
                        SaveLoadManager.LastPath = id;
                        Toast("Сохранено: " + name);
                    }, err => Toast("Ошибка: " + err));
                });
            }
        }

        private void LocalSave(bool forceNew)
        {
            string json = SaveLoadManager.CaptureCurrentJson();
            if (!forceNew && SaveLoadManager.HasLastPath)
            {
                if (SaveLoadManager.SaveToLastPath())
                {
                    Toast("Сохранено");
                    return;
                }
            }

            string defaultName = SaveLoadManager.HasLastPath
                ? System.IO.Path.GetFileNameWithoutExtension(SaveLoadManager.LastPath)
                : QuickSaveName;
            ShowNamePrompt(defaultName, name =>
            {
                string path = SaveLoadManager.PathForName(name);
                SaveLoadManager.LastPath = path;
                if (SaveLoadManager.SaveToPath(path))
                    Toast("Сохранено: " + name);
            });
        }

        // ── Name prompt panel (для «Сохранить» нового серверного проекта) ──

        private void ShowNamePrompt(string defaultName, System.Action<string> onConfirm)
        {
            BuildNamePromptPanel();
            _nameInputField!.text = defaultName;
            _namePromptPanel!.SetActive(true);

            void confirmAction()
            {
                string name = _nameInputField.text.Trim();
                if (string.IsNullOrEmpty(name)) return;
                _namePromptPanel.SetActive(false);
                onConfirm(name);
            }

            var okBtn = _namePromptPanel.transform.Find("OkBtn");
            var cancelBtn = _namePromptPanel.transform.Find("CancelBtn");

            if (okBtn != null)
            {
                var btn = okBtn.GetComponent<Button>();
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(confirmAction);
            }

            if (cancelBtn != null)
            {
                var btn = cancelBtn.GetComponent<Button>();
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => _namePromptPanel.SetActive(false));
            }

            _nameInputField.onEndEdit.RemoveAllListeners();
            _nameInputField.onEndEdit.AddListener(text =>
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                    confirmAction();
            });
        }

        private void BuildNamePromptPanel()
        {
            if (_namePromptPanel != null) return;

            _namePromptPanel = new GameObject("NamePromptPanel");
            _namePromptPanel.transform.SetParent(_canvas!.transform, false);
            var rt = _namePromptPanel.AddComponent<RectTransform>();
            UIFactory.AnchorCenter(rt);
            rt.sizeDelta = new Vector2(380, 160);
            rt.anchoredPosition = Vector2.zero;

            var bg = _namePromptPanel.AddComponent<Image>();
            bg.color = UIFactory.PanelColor;

            UIFactory.CreateLabel("PromptTitle", _namePromptPanel.transform,
                "Название проекта", 20,
                new Vector2(0, -14), new Vector2(340, 32),
                TextAnchor.MiddleCenter);

            _nameInputField = UIFactory.CreateInputField("NameInput",
                _namePromptPanel.transform, "",
                new Vector2(0, 28), new Vector2(340, 34));

            var okBtn = UIFactory.CreateButton("OkBtn", _namePromptPanel.transform,
                "OK", new Vector2(-70, 68), new Vector2(110, 32), null);
            UIFactory.CreateButton("CancelBtn", _namePromptPanel.transform,
                "Отмена", new Vector2(70, 68), new Vector2(110, 32), null);

            _namePromptPanel.SetActive(false);
        }
#endif
    }
}
