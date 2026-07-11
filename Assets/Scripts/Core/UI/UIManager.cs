using System.Collections.Generic;
using System.Runtime.InteropServices;
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
        public static UIManager Instance { get; private set; }

        private Canvas _canvas;
        private SpecificationPanelUI _specPanel;
        private SettingsPanelUI _settingsPanel;
        private ContextMenuUI _contextMenu;
        private FloorSettingsUI _floorSettings;
        private GroupMenuUI _groupMenu;
        private HelpUI _help;
        private Button _undoButton;
        private Button _redoButton;
        private Text _modeButtonLabel;

        public Canvas Canvas => _canvas;
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
            _specPanel.Build(_canvas.transform);

            _settingsPanel = gameObject.AddComponent<SettingsPanelUI>();
            _settingsPanel.Build(_canvas.transform);

            _contextMenu = gameObject.AddComponent<ContextMenuUI>();
            _contextMenu.Build(_canvas.transform);

            _floorSettings = gameObject.AddComponent<FloorSettingsUI>();
            _floorSettings.Build(_canvas.transform);

            var sidebar = gameObject.AddComponent<SidebarUI>();
            sidebar.Build(_canvas.transform);

            _groupMenu = gameObject.AddComponent<GroupMenuUI>();
            _groupMenu.Build(_canvas.transform);

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
            var bar = UIFactory.CreatePanel("Toolbar", _canvas.transform, Vector2.zero, Vector2.zero);
            UIFactory.StretchTopBar(bar.rectTransform, 52f);

            float x = 8f;
            const float y = -6f;
            const float h = 40f;

            // Кнопки добавления деталей переехали в левый сайдбар (SidebarUI).
            AddBarButton(bar.transform, "Spec", "Спецификация", ref x, y, h, 150, ToggleSpecification);
            // Понятные значки вместо текста.
            AddIconButton(bar.transform, "Settings", IconFactory.Gear, ref x, y, h, ToggleSettings);
            AddIconButton(bar.transform, "Save", IconFactory.Floppy, ref x, y, h, SaveCurrent);
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
            _modeButtonLabel = modeBtn.GetComponentInChildren<Text>();
            x += 156;
        }

        private static string ModeLabel() =>
            ResizeHandleManager.Mode == ResizeHandleManager.HandleMode.Resize
                ? "Режим: ═▣"
                : "Режим: ➡";

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
            pos.y = DrawerConstants.GetTypeHeight(type) * 0.5f * AppConstants.MM_TO_UNITS;
            pos = GridManager.SnapToGrid(pos);

            // Имя уникально: пара и фасад ящика ищутся по имени.
            var go = ElementFactory.CreateDrawer(type, length, color, width, DrawerLinks.UniqueName(name), pos);
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

        /// <summary>Открыть контекстное меню детали (вызывается из ElementMover по клику ЛКМ).</summary>
        public void OpenContextMenu(KitchenElement element)
        {
            if (_contextMenu != null)
                _contextMenu.Open(element);
        }

        public void OpenFloorSettings()
        {
            _specPanel.SetVisible(false);
            _settingsPanel.SetVisible(false);
            if (_contextMenu != null) _contextMenu.Close();
            if (_floorSettings != null) _floorSettings.Open();
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
            _settingsPanel.SetVisible(false);
            _specPanel.Toggle();
        }

        public void ToggleSettings()
        {
            if (_settingsPanel == null) return;
            _specPanel.SetVisible(false);
            _settingsPanel.Toggle();
        }

        /// <summary>«Сохранить»: пишет в последний выбранный файл. Если файла ещё
        /// нет — быстрое сохранение в quicksave.json (без диалога).</summary>
        public void SaveCurrent()
        {
            if (SaveLoadManager.HasLastPath)
            {
                if (SaveLoadManager.SaveToLastPath())
                    Toast(
#if UNITY_WEBGL
                        "Сохранено"
#else
                        "Сохранено: " + System.IO.Path.GetFileName(SaveLoadManager.LastPath)
#endif
                    );
            }
            else if (SaveLoadManager.SaveProject(QuickSaveName))
            {
                SaveLoadManager.LastPath = SaveLoadManager.PathForName(QuickSaveName);
                Toast(
#if UNITY_WEBGL
                    "Сохранено"
#else
                    "Сохранено: " + QuickSaveName
#endif
                );
            }
        }

        /// <summary>«Сохранить как»: системный диалог, путь запоминается.</summary>
        public void SaveAs()
        {
#if UNITY_WEBGL
            string json = SaveLoadManager.CaptureCurrentJson();
            string suggested = SaveLoadManager.HasLastPath
                ? System.IO.Path.GetFileName(SaveLoadManager.LastPath)
                : "kitchen.json";
            FileDialogSave(gameObject.name, json, suggested);
#else
            string suggested = SaveLoadManager.HasLastPath
                ? System.IO.Path.GetFileName(SaveLoadManager.LastPath)
                : "kitchen.json";
            string path = NativeFileDialog.SaveDialog("Сохранить проект кухни",
                suggested, SaveLoadManager.LastDirectory);
            if (string.IsNullOrEmpty(path)) return;
            if (SaveLoadManager.SaveToPath(path))
                Toast("Сохранено: " + System.IO.Path.GetFileName(path));
#endif
        }

        /// <summary>«Загрузить»: системный диалог выбора файла, путь запоминается.</summary>
        public void LoadDialog()
        {
#if UNITY_WEBGL
            FileDialogOpen(gameObject.name);
#else
            string path = NativeFileDialog.OpenDialog("Открыть проект кухни",
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
        [DllImport("__Internal")]
        private static extern void FileDialogOpen(string gameObjectName);
        [DllImport("__Internal")]
        private static extern void FileDialogSave(string gameObjectName, string content, string defaultName);

        public void OnWebGLFileOpened(string data)
        {
            int sep = data.IndexOf('\x1F');
            if (sep < 0) return;
            string fileName = data.Substring(0, sep);
            string json = data.Substring(sep + 1);

            var projectData = SaveLoadManager.Deserialize(json);
            if (projectData == null)
            {
                Toast("Ошибка загрузки");
                return;
            }

            SaveLoadManager.LastPath = fileName;
            SaveLoadManager.ClearBoards(PartRegistry.Instance.GetAll());
            SaveLoadManager.RestoreScene(projectData);

            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();

            Toast("Загружено");
        }

        public void OnWebGLFileSaved(string fileName)
        {
            if (!string.IsNullOrEmpty(fileName))
                SaveLoadManager.LastPath = fileName;
            Toast("Сохранено");
        }
#endif
    }
}
