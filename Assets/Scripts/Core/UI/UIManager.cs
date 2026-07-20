using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    /// <summary>
    /// Корневой UI: создаёт Canvas, верхний тулбар и панели (спецификация,
    /// настройки, контекстное меню). Строится процедурно в Start.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager? Instance { get; private set; }

        private Canvas? _canvas;
        private SpecificationPanelUI? _specPanel;
        private SettingsPanelUI? _settingsPanel;
        private ContextMenuUI? _contextMenu;
        private DayNightPanelUI? _dayNightPanel;
        private GroupMenuUI? _groupMenu;
        private HierarchyPanelUI? _hierarchyPanel;
        private HelpUI? _help;
        private Button? _undoButton;
        private Button? _redoButton;
        private TMP_Text? _modeButtonLabel;
        private TMP_Text? _tintButtonLabel;
        private TMP_Text? _lightsButtonLabel;
        private TMP_Text? _vertexLabel;

        public Canvas? Canvas => _canvas;
        public const string QuickSaveName = "quicksave";

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            _canvas = UIFactory.CreateCanvas("UICanvas");
            BuildToolbar();

            _specPanel = gameObject.AddComponent<SpecificationPanelUI>();
            _specPanel.Build(_canvas!.transform);

            _settingsPanel = gameObject.AddComponent<SettingsPanelUI>();
            _settingsPanel.Build(_canvas.transform);

            var sidebar = gameObject.AddComponent<SidebarUI>();
            sidebar.Build(_canvas.transform);

            // Слой перетаскиваемых окон: BringToFront поднимает окно в пределах
            // слоя, поэтому тосты/баннеры/help, созданные после, всегда сверху.
            var windowLayer = UIFactory.CreateRect("WindowLayer", _canvas.transform);
            windowLayer.anchorMin = Vector2.zero;
            windowLayer.anchorMax = Vector2.one;
            windowLayer.offsetMin = windowLayer.offsetMax = Vector2.zero;

            _contextMenu = gameObject.AddComponent<ContextMenuUI>();
            _contextMenu.Build(windowLayer);

            _dayNightPanel = gameObject.AddComponent<DayNightPanelUI>();
            _dayNightPanel.Build(windowLayer);

            _groupMenu = gameObject.AddComponent<GroupMenuUI>();
            _groupMenu.Build(windowLayer);

            _hierarchyPanel = gameObject.AddComponent<HierarchyPanelUI>();
            _hierarchyPanel.Build(windowLayer);

            var toast = gameObject.AddComponent<ToastNotification>();
            toast.Build(_canvas.transform);

            var autoSaveIndicator = gameObject.AddComponent<AutoSaveIndicator>();
            autoSaveIndicator.Build(_canvas.transform);

            var moduleBanner = gameObject.AddComponent<ModuleEditBannerUI>();
            moduleBanner.Build(_canvas.transform);

            _help = gameObject.AddComponent<HelpUI>();
            _help.Build(_canvas.transform);
        }

        private void BuildToolbar()
        {
            var bar = UIFactory.CreatePanel("Toolbar", _canvas!.transform, Vector2.zero, Vector2.zero);
            UIFactory.StretchTopBar(bar.rectTransform, 52f);

            float x = 8f;
            const float y = -6f;
            const float h = 40f;

            // Кнопки добавления деталей переехали в левый сайдбар (SidebarUI).
            AddBarButton(bar.transform, "Spec", "Спецификация", ref x, y, h, 150, ToggleSpecification);
            AddBarButton(bar.transform, "Hierarchy", "Сцена", ref x, y, h, 90, ToggleHierarchy);
            // Понятные значки вместо текста.
            AddIconButton(bar.transform, "Settings", IconFactory.Gear, ref x, y, h, ToggleSettings);
            AddIconButton(bar.transform, "Save", IconFactory.Floppy, ref x, y, h, SaveCurrent);
            // «Сохранить как» и «Загрузить» доступны на всех платформах:
            //   • WebGL — браузерные окна сохранения/выбора файла;
            //   • desktop/редактор — системные диалоги Windows.
            AddIconButton(bar.transform, "SaveAs", IconFactory.FloppyPlus, ref x, y, h, SaveAs);
            AddIconButton(bar.transform, "Load", IconFactory.Folder, ref x, y, h, LoadDialog);

            x += 12;
            _undoButton = AddIconButton(bar.transform, "Undo", IconFactory.Undo, ref x, y, h, DoUndo);
            _redoButton = AddIconButton(bar.transform, "Redo", IconFactory.Redo, ref x, y, h, DoRedo);

            x += 20;
            var alignBtn = UIFactory.CreateButton("Align", bar.transform, "Выравн.",
                new Vector2(x, y), new Vector2(80, h), ShowAlignMenu);
            UIFactory.AnchorTopLeft(alignBtn.GetComponent<RectTransform>());
            alignBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);
            x += 86;
            var distBtn = UIFactory.CreateButton("Distribute", bar.transform, "Распред.",
                new Vector2(x, y), new Vector2(80, h), DistributeX);
            UIFactory.AnchorTopLeft(distBtn.GetComponent<RectTransform>());
            distBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);
            x += 86;

            // Переключатель режима ручек на гранях: растяжение ↔ перемещение по оси.
            var modeBtn = UIFactory.CreateButton("HandleMode", bar.transform, ModeLabel(),
                new Vector2(x, y), new Vector2(150, h), ToggleHandleMode);
            UIFactory.AnchorTopLeft(modeBtn.GetComponent<RectTransform>());
            modeBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);
            _modeButtonLabel = modeBtn.GetComponentInChildren<TMP_Text>();
            x += 156;

            x += 12;
            // Тонировка валидности (светло-зелёный): выкл — видны текстуры деталей.
            var tintBtn = UIFactory.CreateButton("TintToggle", bar.transform, TintLabel(),
                new Vector2(x, y), new Vector2(110, h), ToggleTint);
            UIFactory.AnchorTopLeft(tintBtn.GetComponent<RectTransform>());
            tintBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);
            _tintButtonLabel = tintBtn.GetComponentInChildren<TMP_Text>();
            x += 116;

            // Глобальный выключатель источников света.
            var lightsBtn = UIFactory.CreateButton("LightsToggle", bar.transform, LightsLabel(),
                new Vector2(x, y), new Vector2(110, h), ToggleLights);
            UIFactory.AnchorTopLeft(lightsBtn.GetComponent<RectTransform>());
            lightsBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);
            _lightsButtonLabel = lightsBtn.GetComponentInChildren<TMP_Text>();
            x += 116;

            // Панель «День/Ночь» — глобальное управление солнцем.
            AddBarButton(bar.transform, "DayNight", "Солнце", ref x, y, h, 90, ToggleDayNight);

            // Показ буквенных меток вершин A-H у выделенного элемента.
            var vertexBtn = UIFactory.CreateButton("VertexLabels", bar.transform, VertexLabel(),
                new Vector2(x, y), new Vector2(150, h), ToggleVertexLabels);
            UIFactory.AnchorTopLeft(vertexBtn.GetComponent<RectTransform>());
            vertexBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);
            _vertexLabel = vertexBtn.GetComponentInChildren<TMP_Text>();
        }

        private static string TintLabel() =>
            ElementHighlighter.TintEnabled ? "Тон: вкл" : "Тон: выкл";

        private static string LightsLabel() =>
            LightSourceElement.GlobalOn ? "Свет: вкл" : "Свет: выкл";

        private void ToggleTint()
        {
            ElementHighlighter.TintEnabled = !ElementHighlighter.TintEnabled;
            if (_tintButtonLabel != null) _tintButtonLabel.text = TintLabel();
            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();
        }

        private void ToggleLights()
        {
            LightSourceElement.SetGlobalOn(!LightSourceElement.GlobalOn);
            if (_lightsButtonLabel != null) _lightsButtonLabel.text = LightsLabel();
        }

        private void ToggleDayNight()
        {
            if (_dayNightPanel != null) _dayNightPanel.Toggle();
        }

        private static string VertexLabel() =>
            VertexLabelManager.Enabled ? "Вершины: A-H" : "Вершины: выкл";

        private void ToggleVertexLabels()
        {
            VertexLabelManager.Toggle();
            if (_vertexLabel != null) _vertexLabel.text = VertexLabel();
        }

        private static string ModeLabel() =>
            ResizeHandleManager.Mode == ResizeHandleManager.HandleMode.Resize
                ? "Режим: [ ]"
                : "Режим: ->";

        private void ToggleHandleMode()
        {
            ResizeHandleManager.ToggleMode();
            if (_modeButtonLabel != null) _modeButtonLabel.text = ModeLabel();
        }

        private void AddBarButton(Transform parent, string name, string label, ref float x, float y, float h, float w, System.Action onClick)
        {
            var btn = UIFactory.CreateButton(name, parent, label, new Vector2(x, y), new Vector2(w, h), onClick);
            UIFactory.AnchorTopLeft(btn.GetComponent<RectTransform>());
            btn.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);
            x += w + 6;
        }

        private Button AddIconButton(Transform parent, string name, Sprite icon, ref float x, float y, float h, System.Action onClick)
        {
            var btn = UIFactory.CreateIconButton(name, parent, icon, new Vector2(x, y), new Vector2(h, h), onClick);
            UIFactory.AnchorTopLeft(btn.GetComponent<RectTransform>());
            btn.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);
            x += h + 6;
            return btn;
        }

        // Кнопки отмены/повтора активны только когда есть что отменять/повторять.
        private void Update()
        {
            if (_undoButton != null) _undoButton.interactable = CommandStack.CanUndo;
            if (_redoButton != null) _redoButton.interactable = CommandStack.CanRedo;
        }

        private void DoUndo()
        {
            if (!CommandStack.CanUndo) return;
            CommandStack.Undo();
            AfterUndoRedo();
        }

        private void DoRedo()
        {
            if (!CommandStack.CanRedo) return;
            CommandStack.Redo();
            AfterUndoRedo();
        }

        // После отмены/повтора деталь могла стать неактивной (отмена создания) —
        // снимаем с неё выделение и пересчитываем подсветку валидности.
        private static void AfterUndoRedo()
        {
            var sel = SelectionManager.Instance;
            if (sel != null && sel.Selected != null && !sel.Selected.gameObject.activeInHierarchy)
                sel.Deselect();
            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();
        }

        public void SpawnPreset(int index)
        {
            if (index < 0 || index >= AppConstants.PRESET_DIMENSIONS_MM.Length) return;
            SpawnBoard(AppConstants.PRESET_DIMENSIONS_MM[index]);
        }

        public void SpawnBoard(Vector3Int dims) =>
            SpawnBoard(dims, $"Board {dims.x}x{dims.y}x{dims.z}");

        public void SpawnBoard(Vector3Int dims, string name)
        {
            Vector3 pos = GroundPointInFrontOfCamera();
            pos.y = dims.y * 0.5f * AppConstants.MM_TO_UNITS; // на полу
            pos = GridManager.SnapToGrid(pos);

            var go = ElementFactory.CreatePart(dims, name, pos);
            var element = go.GetComponent<KitchenElement>();
            if (element != null)
            {
                CommandStack.Execute(new CreateCommand(go));
                if (SelectionManager.Instance != null)
                    SelectionManager.Instance.Select(element);
            }
        }

        public void SpawnFacade(Vector3Int dims, string name,
            int gapLeft = 2, int gapRight = 2, int gapTop = 2, int gapBottom = 2)
        {
            Vector3 pos = GroundPointInFrontOfCamera();
            pos.y = dims.y * 0.5f * AppConstants.MM_TO_UNITS;
            pos = GridManager.SnapToGrid(pos);

            var go = ElementFactory.CreateFacade(dims, name, pos, gapLeft, gapRight, gapTop, gapBottom);
            var element = go.GetComponent<KitchenElement>();
            if (element != null)
            {
                CommandStack.Execute(new CreateCommand(go));
                if (SelectionManager.Instance != null)
                    SelectionManager.Instance.Select(element);
            }
        }

        public void SpawnAssembledFacade(Vector3Int dims, string name,
            AssembledFill fill = AssembledFill.Blind)
        {
            Vector3 pos = GroundPointInFrontOfCamera();
            pos.y = dims.y * 0.5f * AppConstants.MM_TO_UNITS;
            pos = GridManager.SnapToGrid(pos);

            var go = ElementFactory.CreateAssembledFacade(dims, name, pos, fill);
            var element = go.GetComponent<KitchenElement>();
            if (element != null)
            {
                CommandStack.Execute(new CreateCommand(go));
                if (SelectionManager.Instance != null)
                    SelectionManager.Instance.Select(element);
            }
        }

        public void SpawnWall(Vector3Int dims, string name)
        {
            Vector3 pos = GroundPointInFrontOfCamera();
            pos.y = dims.y * 0.5f * AppConstants.MM_TO_UNITS; // на полу
            pos = GridManager.SnapToGrid(pos);

            var go = ElementFactory.CreateWall(dims, name, pos);
            var element = go.GetComponent<KitchenElement>();
            if (element != null)
            {
                CommandStack.Execute(new CreateCommand(go));
                if (SelectionManager.Instance != null)
                    SelectionManager.Instance.Select(element);
            }
        }

        public void SpawnDrawer(string drawerType, int length, string colorName, int width, string name)
        {
            var type = drawerType switch { "B" => DrawerType.B, "C" => DrawerType.C, "D" => DrawerType.D, _ => DrawerType.A };
            var color = colorName.ToLowerInvariant() switch { "white" => DrawerColor.White, "black" => DrawerColor.Black, _ => DrawerColor.Anthracite };
            Vector3 pos = GroundPointInFrontOfCamera();
            pos.y = DrawerConstants.GetMinOpeningHeight(type) * 0.5f * AppConstants.MM_TO_UNITS;
            pos = GridManager.SnapToGrid(pos);

            var go = ElementFactory.CreateDrawer(type, length, color, width, DrawerLinks.UniqueName(name), pos);
            var element = go.GetComponent<KitchenElement>();
            if (element != null)
            {
                CommandStack.Execute(new CreateCommand(go));
                if (SelectionManager.Instance != null)
                    SelectionManager.Instance.Select(element);
            }
        }

        public void SpawnTable(Vector3Int dims, string name)
        {
            Vector3 pos = GroundPointInFrontOfCamera();
            pos.y = dims.y * 0.5f * AppConstants.MM_TO_UNITS;
            pos = GridManager.SnapToGrid(pos);

            var go = ElementFactory.CreateTable(dims, name, pos);
            var element = go.GetComponent<KitchenElement>();
            if (element != null)
            {
                CommandStack.Execute(new CreateCommand(go));
                if (SelectionManager.Instance != null)
                    SelectionManager.Instance.Select(element);
            }
        }

        public void SpawnRadiusTable(Vector3Int dims, string name)
        {
            Vector3 pos = GroundPointInFrontOfCamera();
            pos.y = dims.y * 0.5f * AppConstants.MM_TO_UNITS;
            pos = GridManager.SnapToGrid(pos);

            var go = ElementFactory.CreateRadiusTable(dims, name, pos);
            var element = go.GetComponent<KitchenElement>();
            if (element != null)
            {
                CommandStack.Execute(new CreateCommand(go));
                if (SelectionManager.Instance != null)
                    SelectionManager.Instance.Select(element);
            }
        }

        public void SpawnPanel(Vector3Int dims, string name,
            int gapLeft = PanelElement.DEFAULT_GAP_MM, int gapRight = PanelElement.DEFAULT_GAP_MM,
            int gapTop = PanelElement.DEFAULT_GAP_MM, int gapBottom = PanelElement.DEFAULT_GAP_MM)
        {
            Vector3 pos = GroundPointInFrontOfCamera();
            pos.y = dims.y * 0.5f * AppConstants.MM_TO_UNITS;
            pos = GridManager.SnapToGrid(pos);

            var go = ElementFactory.Instance.CreatePanel(dims, name, pos, gapLeft, gapRight, gapTop, gapBottom);
            var element = go.GetComponent<KitchenElement>();
            if (element != null)
            {
                CommandStack.Execute(new CreateCommand(go));
                if (SelectionManager.Instance != null)
                    SelectionManager.Instance.Select(element);
            }
        }

        public void SpawnRadialShelf(Vector3Int dims, string name)
        {
            Vector3 pos = GroundPointInFrontOfCamera();
            pos.y = dims.y * 0.5f * AppConstants.MM_TO_UNITS;
            pos = GridManager.SnapToGrid(pos);

            var go = ElementFactory.CreateRadialShelf(dims.x, dims.z, dims.y,
                AppConstants.RADIAL_CORNER_RADIUS_DEFAULT, name, pos);
            var element = go.GetComponent<KitchenElement>();
            if (element != null)
            {
                CommandStack.Execute(new CreateCommand(go));
                if (SelectionManager.Instance != null)
                    SelectionManager.Instance.Select(element);
            }
        }

        public void SpawnWindow(Vector3Int dims, string name)
        {
            Vector3 pos = GroundPointInFrontOfCamera();
            pos.y = dims.y * 0.5f * AppConstants.MM_TO_UNITS;
            pos = GridManager.SnapToGrid(pos);

            var go = ElementFactory.CreateWindow(dims, name, pos);
            var element = go.GetComponent<KitchenElement>();
            if (element != null)
            {
                CommandStack.Execute(new CreateCommand(go));
                if (SelectionManager.Instance != null)
                    SelectionManager.Instance.Select(element);
            }
        }

        public void SpawnDoor(Vector3Int dims, string name)
        {
            Vector3 pos = GroundPointInFrontOfCamera();
            pos.y = dims.y * 0.5f * AppConstants.MM_TO_UNITS;
            pos = GridManager.SnapToGrid(pos);

            var go = ElementFactory.CreateDoor(dims, name, pos);
            var element = go.GetComponent<KitchenElement>();
            if (element != null)
            {
                CommandStack.Execute(new CreateCommand(go));
                if (SelectionManager.Instance != null)
                    SelectionManager.Instance.Select(element);
            }
        }

        public void SpawnPillar(int midHeightMM, string name)
        {
            int totalH = PillarElement.TopHeightMM + midHeightMM + PillarElement.BottomHeightMM;
            Vector3 pos = GroundPointInFrontOfCamera();
            pos.y = totalH * 0.5f * AppConstants.MM_TO_UNITS;
            pos = GridManager.SnapToGrid(pos);

            var go = ElementFactory.CreatePillar(midHeightMM, name, pos);
            var element = go.GetComponent<KitchenElement>();
            if (element != null)
            {
                CommandStack.Execute(new CreateCommand(go));
                if (SelectionManager.Instance != null)
                    SelectionManager.Instance.Select(element);
            }
        }

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

        public void SpawnFloor(Vector3Int dims, string name)
        {
            Vector3 pos = GroundPointInFrontOfCamera();
            pos = GridManager.SnapToGrid(pos);
            // Верхняя плоскость пола ровно на уровне земли (y = 0) — детали
            // встают на него с face-контактом и заземляются.
            pos.y = -dims.y * 0.5f * AppConstants.MM_TO_UNITS;

            var go = ElementFactory.CreateFloor(dims, name, pos);
            var element = go.GetComponent<KitchenElement>();
            if (element != null)
            {
                CommandStack.Execute(new CreateCommand(go));
                if (SelectionManager.Instance != null)
                    SelectionManager.Instance.Select(element);
            }
        }

        public void SpawnLightSource(string name)
        {
            Vector3 pos = GroundPointInFrontOfCamera();
            pos = GridManager.SnapToGrid(pos);
            pos.y = 2.2f; // подвес на высоте ~2200 мм, как люстра

            var go = ElementFactory.CreateLightSource(name, pos);
            var element = go.GetComponent<KitchenElement>();
            if (element != null)
            {
                CommandStack.Execute(new CreateCommand(go));
                if (SelectionManager.Instance != null)
                    SelectionManager.Instance.Select(element);
            }
        }

        /// <summary>Открыть меню группы (вызывается из CameraController по ПКМ-клику).</summary>
        public void OpenGroupMenu(KitchenElement element)
        {
            if (_groupMenu != null)
                _groupMenu.Open(element);
        }

        public void ToggleSpecification()
        {
            if (_specPanel == null) return;
            _settingsPanel!.SetVisible(false);
            _specPanel.Toggle();
        }

        public void ToggleSettings()
        {
            if (_settingsPanel == null) return;
            _specPanel!.SetVisible(false);
            _settingsPanel.Toggle();
        }

        public void ToggleHierarchy()
        {
            if (_hierarchyPanel == null) return;
            _hierarchyPanel.Toggle();
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
                    Toast("Сохранено: " + System.IO.Path.GetFileName(SaveLoadManager.LastPath));
            }
            else if (SaveLoadManager.SaveProject(QuickSaveName))
            {
                SaveLoadManager.LastPath = SaveLoadManager.PathForName(QuickSaveName);
                Toast("Сохранено: " + QuickSaveName);
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
                name => Toast("Сохранено: " + name));
#else
            string suggested = SaveLoadManager.HasLastPath
                ? System.IO.Path.GetFileName(SaveLoadManager.LastPath)
                : "kitchen.json";
            string? path = NativeFileDialog.SaveDialog("Сохранить проект кухни",
                suggested, SaveLoadManager.LastDirectory);
            if (string.IsNullOrEmpty(path)) return;
            if (SaveLoadManager.SaveToPath(path))
                Toast("Сохранено: " + System.IO.Path.GetFileName(path));
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
            if (ToastNotification.Instance != null)
                ToastNotification.Instance.Show(msg);
        }

        private void ShowAlignMenu()
        {
            var sel = SelectionManager.Instance;
            if (sel == null || sel.SelectedElements.Count < 2)
                return;

            var list = new List<KitchenElement>(sel.SelectedElements);
            AlignDistributeTool.Align(list, Axis.X, AlignmentMode.Min);
            AlignDistributeTool.RefreshHighlights();
        }

        private void DistributeX()
        {
            var sel = SelectionManager.Instance;
            if (sel == null || sel.SelectedElements.Count < 3)
                return;

            var list = new List<KitchenElement>(sel.SelectedElements);
            AlignDistributeTool.Distribute(list, Axis.X);
            AlignDistributeTool.RefreshHighlights();
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
