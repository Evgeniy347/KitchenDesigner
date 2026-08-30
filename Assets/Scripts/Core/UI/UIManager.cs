using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core.Analysis;

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
        private ErrorPanelUI? _errorPanel;
        private ProjectInstructionsPanelUI? _projectInstructionsPanel;
        private HelpUI? _help;
        private PlacementController? _placement;
        private ElementSpawner? _spawner;
        private Button? _undoButton;
        private Button? _redoButton;
        private Button? _specButton;
        private Button? _hierarchyButton;
        private Button? _errorButton;
        private Button? _gotoIssueButton;
        private Image? _gotoIssueIcon;
        private Button? _projectInstructionsButton;
        private Button? _settingsButton;
        private Button? _measureButton;
        private Button? _eyedropperButton;
        private RawImage? _eyedropperSwatch;
        /// <summary>Декор, который сейчас показан в образце пипетки (пустая
        /// строка — образец спрятан).</summary>
        private string _swatchId = string.Empty;
        private Button? _tintButton;
        private Button? _lightsButton;
        private Button? _dayNightButton;
        private TMP_Text? _modeButtonLabel;
        private TMP_Text? _editModeButtonLabel;
        private TMP_Text? _errorButtonLabel;

        // Счётчик проблем на кнопке «Ошибки» пересчитывается только когда сцена
        // изменилась. Раньше это был таймер раз в секунду, и полный анализ сцены
        // (O(n²) по коллизиям и покрытию кромок) давал на 259 элементах хич в
        // 150-220 мс ровно раз в секунду — именно он ощущался как рывок камеры.
        private int _errorBadgeRevision = -1;
        private int _errorBadgePendingRevision = -1;
        private float _errorBadgeStableAt;

        /// <summary>Сколько сцена должна постоять неизменной, прежде чем пересчитывать
        /// бейдж. Без этой паузы перетаскивание детали (ревизия растёт каждый кадр)
        /// запускало бы анализ в каждом кадре — хуже прежнего таймера.</summary>
        private const float ErrorBadgeSettleSec = 0.25f;

        public Canvas? Canvas => _canvas;
        public const string QuickSaveName = "quicksave";

        private void Awake()
        {
            Instance = this;

            _canvas = UIFactory.CreateCanvas("UICanvas");
            BuildToolbar();
            EditModeManager.Changed += RefreshEditModeLabel;

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

            _errorPanel = gameObject.AddComponent<ErrorPanelUI>();
            _errorPanel.Build(windowLayer);

            _projectInstructionsPanel = gameObject.AddComponent<ProjectInstructionsPanelUI>();
            _projectInstructionsPanel.Build(windowLayer);

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

        private ElementSpawner Spawner =>
            _spawner ??= new ElementSpawner(GroundPointInFrontOfCamera, () => _placement);

        private void BuildToolbar()
        {
            var bar = UIFactory.CreatePanel("Toolbar", _canvas!.transform, Vector2.zero, Vector2.zero);
            UIFactory.StretchTopBar(bar.rectTransform, 52f);

            float x = 8f;
            const float y = -6f;
            const float h = 40f;

            // Группы разделены вертикальными линиями:
            // панели | файл | undo | инструменты | вид.
            // Кнопки добавления деталей переехали в левый сайдбар (SidebarUI).
            _specButton = AddBarButton(bar.transform, "Spec", "Спецификация", ref x, y, h, 150, ToggleSpecification);
            _hierarchyButton = AddBarButton(bar.transform, "Hierarchy", "Сцена", ref x, y, h, 90, ToggleHierarchy);
            _errorButton = AddBarButton(bar.transform, "Errors", "Ошибки", ref x, y, h, 110, ToggleErrors);
            _errorButtonLabel = _errorButton.GetComponentInChildren<TMP_Text>();
            // Быстрый переход к первой проблеме без открытия окна «Ошибки».
            _gotoIssueButton = AddIconButton(bar.transform, "GotoIssue", IconFactory.Warning,
                ref x, y, h, GotoFirstIssue, "Перейти к первой проблеме");
            _gotoIssueIcon = _gotoIssueButton.transform.Find("GotoIssue_Icon")?.GetComponent<Image>();
            _projectInstructionsButton = AddBarButton(bar.transform, "ProjectInstructions",
                "Инструкции", ref x, y, h, 120, ToggleProjectInstructions);
            AddSeparator(bar.transform, ref x, y, h);

            // Понятные значки вместо текста.
            _settingsButton = AddIconButton(bar.transform, "Settings", IconFactory.Gear, ref x, y, h, ToggleSettings, "Настройки");
            AddIconButton(bar.transform, "Save", IconFactory.Floppy, ref x, y, h, SaveCurrent, "Сохранить");
            // «Сохранить как» и «Загрузить» доступны на всех платформах:
            //   • WebGL — браузерные окна сохранения/выбора файла;
            //   • desktop/редактор — системные диалоги Windows.
            AddIconButton(bar.transform, "SaveAs", IconFactory.FloppyPlus, ref x, y, h, SaveAs, "Сохранить как");
            AddIconButton(bar.transform, "Load", IconFactory.Folder, ref x, y, h, LoadDialog, "Загрузить");
            AddSeparator(bar.transform, ref x, y, h);

            _undoButton = AddIconButton(bar.transform, "Undo", IconFactory.Undo, ref x, y, h, DoUndo, "Отменить");
            _redoButton = AddIconButton(bar.transform, "Redo", IconFactory.Redo, ref x, y, h, DoRedo, "Повторить");
            AddSeparator(bar.transform, ref x, y, h);

            // Переключатель режима ручек на гранях: растяжение ↔ перемещение по оси.
            var modeBtn = AddBarButton(bar.transform, "HandleMode", ModeLabel(), ref x, y, h, 176, ToggleHandleMode);
            _modeButtonLabel = modeBtn.GetComponentInChildren<TMP_Text>();
            // Замер расстояний между вершинами: пока режим включён, мышь
            // принадлежит только рулетке.
            _measureButton = AddIconButton(bar.transform, "MeasureToggle", IconFactory.Ruler,
                ref x, y, h, Measure.MeasureMode.Toggle, "Рулетка");
            // Перенос декора: ПКМ подбирает текстуру, ЛКМ красит ею.
            _eyedropperButton = AddIconButton(bar.transform, "Eyedropper", IconFactory.Eyedropper,
                ref x, y, h, Tools.EyedropperMode.Toggle, "Пипетка: ПКМ — взять текстуру, ЛКМ — применить");
            _eyedropperSwatch = AddSwatch(_eyedropperButton.transform);
            AddSeparator(bar.transform, ref x, y, h);

            // Тогглы вида: состояние показывает фон кнопки (нажат = включено),
            // а не слово «вкл/выкл» в подписи (правило 9).
            // Тонировка валидности (светло-зелёный): выкл — видны текстуры деталей.
            _tintButton = AddBarButton(bar.transform, "TintToggle", "Тонировка", ref x, y, h, 110, ToggleTint);
            // Глобальный выключатель источников света.
            _lightsButton = AddIconButton(bar.transform, "LightsToggle", IconFactory.Bulb,
                ref x, y, h, ToggleLights, "Свет");
            // Панель «День/Ночь» — глобальное управление солнцем.
            _dayNightButton = AddIconButton(bar.transform, "DayNight", IconFactory.Sun,
                ref x, y, h, ToggleDayNight, "Солнце");
            AddSeparator(bar.transform, ref x, y, h);

            // Переключатель режима редактора: фоторежим → помещение → обычный.
            // Стоит в конце тулбара, чтобы не путаться с «Ручки: …».
            var editModeBtn = AddBarButton(bar.transform, "EditMode",
                EditModeManager.Label(EditModeManager.Mode), ref x, y, h, 190, CycleEditMode);
            _editModeButtonLabel = editModeBtn.GetComponentInChildren<TMP_Text>();
        }

        private void ToggleTint()
        {
            ElementHighlighter.TintEnabled = !ElementHighlighter.TintEnabled;
            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();
        }

        private void ToggleLights()
        {
            LightSourceElement.SetGlobalOn(!LightSourceElement.GlobalOn);
        }

        private void ToggleDayNight()
        {
            if (_dayNightPanel != null) _dayNightPanel.Toggle();
        }

        private void ToggleProjectInstructions() => _projectInstructionsPanel?.Toggle();

        // Два именованных состояния — это не «вкл/выкл», подпись честно
        // называет текущий режим.
        private static string ModeLabel() =>
            ResizeHandleManager.Mode == ResizeHandleManager.HandleMode.Resize
                ? "Ручки: растяжение"
                : "Ручки: перенос";

        private void ToggleHandleMode()
        {
            ResizeHandleManager.ToggleMode();
            if (_modeButtonLabel != null) _modeButtonLabel.text = ModeLabel();
        }

        private void CycleEditMode() => EditModeManager.Cycle();

        // Подпись обновляем по событию, а не только по клику: режим меняют ещё
        // тумблер «Фоторежим» в настройках и F10 (через PhotoMode → EditModeManager).
        private void RefreshEditModeLabel()
        {
            if (_editModeButtonLabel != null)
                _editModeButtonLabel.text = EditModeManager.Label(EditModeManager.Mode);
        }

        private void OnDestroy()
        {
            EditModeManager.Changed -= RefreshEditModeLabel;
        }

        private Button AddBarButton(Transform parent, string name, string label, ref float x, float y, float h, float w, System.Action onClick)
        {
            var btn = UIFactory.CreateButton(name, parent, label, new Vector2(x, y), new Vector2(w, h), onClick);
            UIFactory.AnchorTopLeft(btn.GetComponent<RectTransform>());
            btn.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);
            x += w + 6;
            return btn;
        }

        private static void AddSeparator(Transform parent, ref float x, float y, float h)
        {
            x += 4;
            var sep = UIFactory.CreatePanel("Separator", parent, Vector2.zero,
                new Vector2(2, h - 8), UIStyle.Separator);
            UIFactory.AnchorTopLeft(sep.rectTransform);
            sep.rectTransform.anchoredPosition = new Vector2(x, y - 4);
            sep.raycastTarget = false;
            x += 12;
        }

        /// <summary>Нажатое состояние тоггла тулбара.</summary>
        private static void SetToggled(Button? btn, bool on)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null) img.color = on ? UIStyle.SurfaceActive : UIStyle.Surface;
        }

        /// <summary>Кнопка-значок. У неё нет подписи, поэтому tooltip обязателен —
        /// иначе назначение приходится угадывать (чек-лист UI-GUIDELINES).</summary>
        private Button AddIconButton(Transform parent, string name, Sprite icon, ref float x, float y, float h, System.Action onClick, string tooltip)
        {
            var btn = UIFactory.CreateIconButton(name, parent, icon, new Vector2(x, y), new Vector2(h, h), onClick);
            UIFactory.AnchorTopLeft(btn.GetComponent<RectTransform>());
            btn.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);
            TooltipUI.Attach(btn.gameObject, tooltip);
            x += h + 6;
            return btn;
        }

        /// <summary>Образец «что сейчас в пипетке» в углу её кнопки: текстура
        /// подобранного декора. Без него режим молчит о том, что он подобрал, и
        /// первый же клик красит наугад.</summary>
        private static RawImage AddSwatch(Transform button)
        {
            const float size = 14f;
            var rect = UIFactory.CreateRect("Swatch", button);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 0);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = new Vector2(-3f, 3f);
            var img = rect.gameObject.AddComponent<RawImage>();
            img.raycastTarget = false;
            rect.gameObject.SetActive(false);
            return img;
        }

        /// <summary>Показать в образце подобранный декор: его картинку, а у чисто
        /// цветового декора («Серый», «Белый») — его цвет. Пересчёт только при
        /// смене декора: Resources.Load каждый кадр тут ни к чему.</summary>
        private void RefreshEyedropperSwatch()
        {
            if (_eyedropperSwatch == null) return;
            var id = Tools.EyedropperMode.PickedMaterialId ?? string.Empty;
            if (id == _swatchId) return;
            _swatchId = id;

            if (id.Length == 0)
            {
                _eyedropperSwatch.gameObject.SetActive(false);
                return;
            }

            var def = MaterialCatalog.Get(id);
            var tex = MaterialManager.ResolveTexture(def);
            _eyedropperSwatch.texture = tex;
            _eyedropperSwatch.color = tex != null ? Color.white : def.baseColor;
            _eyedropperSwatch.gameObject.SetActive(true);
        }

        // Кнопки отмены/повтора активны только когда есть что отменять/повторять;
        // тогглы вида и панелей показывают своё состояние нажатым фоном.
        private void Update()
        {
            using var _ = PerfMarkers.UIManagerUpdate.Auto();
            if (_undoButton != null) _undoButton.interactable = CommandStack.CanUndo;
            if (_redoButton != null) _redoButton.interactable = CommandStack.CanRedo;

            SetToggled(_measureButton, Measure.MeasureMode.Active);
            SetToggled(_eyedropperButton, Tools.EyedropperMode.Active);
            RefreshEyedropperSwatch();
            SetToggled(_tintButton, ElementHighlighter.TintEnabled);
            SetToggled(_lightsButton, LightSourceElement.GlobalOn);
            SetToggled(_specButton, _specPanel != null && _specPanel.IsVisible);
            SetToggled(_hierarchyButton, _hierarchyPanel != null && _hierarchyPanel.IsVisible);
            SetToggled(_errorButton, _errorPanel != null && _errorPanel.IsVisible);
            SetToggled(_projectInstructionsButton,
                _projectInstructionsPanel != null && _projectInstructionsPanel.IsVisible);
            SetToggled(_settingsButton, _settingsPanel != null && _settingsPanel.IsVisible);
            SetToggled(_dayNightButton, _dayNightPanel != null && _dayNightPanel.IsVisible);

            RefreshErrorBadgeIfSettled();
        }

        /// <summary>Пересчитывает бейдж, когда сцена изменилась И успокоилась:
        /// во время перетаскивания или ресайза анализ не запускается вовсе, а
        /// сразу после отпускания считается ровно один раз.</summary>
        private void RefreshErrorBadgeIfSettled()
        {
            if (SceneRevision.Changed(ref _errorBadgePendingRevision))
            {
                _errorBadgeStableAt = Time.unscaledTime + ErrorBadgeSettleSec;
                return;
            }

            if (_errorBadgePendingRevision == _errorBadgeRevision) return;
            if (Time.unscaledTime < _errorBadgeStableAt) return;

            _errorBadgeRevision = _errorBadgePendingRevision;
            UpdateErrorBadge();
        }

        // «(N)» на кнопке «Ошибки»: N = ошибки + предупреждения. Число красное,
        // если есть хоть одна ошибка; оранжевое — если только предупреждения;
        // без скобок — если проблем нет. Красит рич-текстом только «(N)».
        private void UpdateErrorBadge()
        {
            int errors = 0, warnings = 0;
            foreach (var iss in SceneAnalyzer.Analyze())
            {
                if (iss.Level == IssueLevel.Error) errors++;
                else if (iss.Level == IssueLevel.Warning) warnings++;
            }

            int total = errors + warnings;
            Color color = errors > 0 ? UIStyle.HighlightError : UIStyle.HighlightWarning;

            if (_errorButtonLabel != null)
            {
                _errorButtonLabel.text = total == 0
                    ? "Ошибки"
                    : $"Ошибки <color=#{ColorUtility.ToHtmlStringRGB(color)}>({total})</color>";
            }

            // Кнопка перехода к проблеме живёт по тому же счётчику. Значок красим
            // вручную: Button.interactable гасит только фон кнопки, дочерний Image
            // иконки под ColorBlock не попадает.
            if (_gotoIssueButton != null) _gotoIssueButton.interactable = total > 0;
            if (_gotoIssueIcon != null)
                _gotoIssueIcon.color = total == 0 ? UIStyle.TextDisabled : color;
        }

        // Выделяет детали первой проблемы сцены, наводит на них камеру и показывает
        // её описание в статус-баре. Анализ повторяем: кэшировать AnalysisIssue между
        // кадрами нельзя — ссылки на детали могли протухнуть.
        private static void GotoFirstIssue()
        {
            foreach (var iss in SceneAnalyzer.Analyze())
            {
                if (iss.Level != IssueLevel.Error && iss.Level != IssueLevel.Warning) continue;

                ErrorPanelUI.RevealIssue(iss);
                StatusBarUI.Instance?.ShowTransient(
                    $"{iss.Code} · {iss.Detail} · {iss.Message}",
                    ErrorPanelUI.LevelColor(iss.Level), 3f);
                return;
            }
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

        public void ToggleErrors()
        {
            if (_errorPanel == null) return;
            _errorPanel.Toggle();
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
