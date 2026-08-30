using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static KitchenDesigner.Core.UI.ContextMenuMetrics;

namespace KitchenDesigner.Core.UI
{
    /// <summary>Контекстное меню по клику ЛКМ на детали: размеры, позиция, поворот, действия.</summary>
    public class ContextMenuUI : MonoBehaviour, IContextMenuHost
    {
        public static ContextMenuUI? Instance { get; private set; }

        private GameObject? _root;
        private KitchenElement? _target;
        private TMP_Text? _titleLabel;

        private TMP_InputField? _name, _w, _h, _d, _radius,
            _cutoutW, _cutoutD,
            _gapLeft, _gapRight, _gapTop, _gapBottom, _gapFront, _gapBack,
            _x, _y, _z, _rx, _ry, _rz, _legInset, _midHeight,
            _lightTemp, _lightPower, _lightDiffusion, _lightUp, _lightBeam,
            _lightSoftness, _lightGlow, _lightShadowStrength, _lightDrop,
            _lightUpCone, _lightUpRange, _lightRangeMin, _lightRangeMax,
            _lightEfficacy, _lightLumens;
        private TMP_Dropdown? _lightShapeDropdown, _lightShadowDropdown;
        private TMP_Text? _lightAdvancedLabel;
        private bool _lightAdvancedExpanded;  // раскрыта ли калибровка лампы
        private Toggle? _lockToggle;
        private Toggle? _transparentToggle;
        private RectTransform? _panelRt;
        private TMP_Text? _doorButtonLabel;
        private TMP_Text? _winDoorButtonLabel;
        private TMP_Text? _ovenDoorLabel;
        private TMP_Text? _dishwasherDoorLabel;
        private TMP_Dropdown? _modeDropdown;
        private TMP_Dropdown? _fillDropdown;
        private TMP_Dropdown? _materialDropdown;
        private TMP_Dropdown? _tabletopMaterialDropdown;
        private TMP_Dropdown? _legsMaterialDropdown;
        private TMP_Dropdown? _typeDropdown;
        private TMP_Dropdown? _drawerTypeDropdown, _drawerLengthDropdown, _drawerColorDropdown;
        private TMP_Dropdown? _drawerUpperLenDropdown;
        private TMP_InputField? _drawerWidth;
        private TMP_Text? _drawerAnimLabel;
        private TMP_Dropdown? _drawerFacadeDropdown;
        private TMP_Text? _drawerFacadeLabel;
        private Color _drawerFacadeNormalColor;
        private TMP_Dropdown? _attachToDropdown;
        private TMP_Text? _attachToLabel;
        private Color _attachToNormalColor;

        /// <summary>Подпись строки пристёгнутого фасада: у ящика она уточняет
        /// «ящика» (в панели ящика рядом стоят и другие «фасадные» строки), у
        /// посудомойки уточнять нечего — фасад у неё один.</summary>
        private const string DrawerFacadeLabelText = "Фасад ящика";
        private const string HostFacadeLabelText = "Фасад";
        private const string AttachToLabelText = "Прикрепить к";
        private const string AttachToNoneText = "(не прикреплено)";
        private TMP_Dropdown? _tintDropdown;
        private TMP_Dropdown? _sashTypeDropdown;
        private TMP_InputField? _sillProtrusion;
        private TMP_Dropdown? _winModeDropdown;
        private TMP_Text? _grooveCountLabel;
        private TMP_Dropdown? _grooveSideDropdown, _grooveKindDropdown;
        // Строки-слоты пазов: сторона и тип редактируются на месте (правка =
        // прямое действие, отдельного режима «редактирования» нет).
        private readonly TMP_Dropdown?[] _grooveRowSide = new TMP_Dropdown?[AppConstants.GROOVE_MAX_PER_PART];
        private readonly TMP_Dropdown?[] _grooveRowKind = new TMP_Dropdown?[AppConstants.GROOVE_MAX_PER_PART];
        private bool _groovesExpanded;  // раскрыт ли список пазов
        private int _grooveFingerprint; // отлов изменений пазов извне (undo/MCP)

        // ── Зазоры ─────────────────────────────────────────────────────
        // Устроены как пазы: раскрывашка со счётчиком (сколько сторон получили
        // ненулевой зазор) и строки полей под ней.
        private TMP_Text? _gapCountLabel;
        private bool _gapsExpanded;

        // ── Кромки ─────────────────────────────────────────────────────
        // Схема детали со сторонами L1/L2/W1/W2: зелёная сторона — кромка есть,
        // светло-серая — торец упирается в соседа. Размер схемы фиксирован и от
        // габарита детали не зависит — это условная схема, а не чертёж.
        private Toggle? _edgeToggle;
        private TMP_InputField? _edgeThickness;
        private RectTransform? _edgeDiagram;
        private Image? _edgeStripL1, _edgeStripL2, _edgeStripW1, _edgeStripW2;
        private TMP_Text? _edgeLengthLabel, _edgeWidthLabel;
        // Кромки пересчитываются по всей сцене, поэтому в Update это делается
        // не каждый кадр: соседи двигаются заметно медленнее 60 Гц.
        private const int EdgeRefreshFrames = 15;
        private int _edgeRefreshCountdown;

        private bool _openInProgress;
        private bool _currentIsTable;
        private bool _currentIsDoor;
        private string _currentTypeName = "Деталь";

        private readonly ContextMenuLayout _layout = new();
        private readonly ContextMenuTextureSection _textures;
        private readonly ContextMenuFieldTracker _fields;
        private ContextMenuRowFactory _rows = null!;

        public ContextMenuUI()
        {
            _textures = new ContextMenuTextureSection(this);
            _fields = new ContextMenuFieldTracker(Apply);
        }

        KitchenElement? IContextMenuHost.Target => _target;

        ContextMenuLayout IContextMenuHost.Layout => _layout;

        void IContextMenuHost.Relayout() => RelayoutForTarget();

        public bool TexturePreviewActive => _textures.PreviewActive;

        public void ToggleTextures() => _textures.Toggle();

        internal ContextMenuTextureSection Textures => _textures;

        private void Awake()
        {
            Instance = this;
        }

        public void Build(Transform canvas)
        {
            var panel = UIFactory.CreatePanel("ContextMenu", canvas, Vector2.zero, new Vector2(364, 560));
            UIFactory.AnchorTopRight(panel.rectTransform);
            panel.rectTransform.anchoredPosition = new Vector2(-10, -60);
            _root = panel.gameObject;
            _panelRt = panel.rectTransform;
            WindowDrag.Attach(panel.rectTransform, TopPad + TitleH + TitleGap);
            _layout.Clear();
            _rows = new ContextMenuRowFactory(panel.transform, _layout);

            BuildTitleAndType(panel.transform);
            BuildDimensions();
            BuildGrooveSection(panel.transform);
            BuildEdgeSection(panel.transform);
            BuildGapSection(panel.transform);
            BuildFacadeSection();
            BuildDrawerSection();
            BuildAttachmentSection();
            BuildWindowSection();
            BuildFurnitureSection();
            BuildLightSection();
            BuildPositionSection(panel.transform);
            BuildMaterialSection(panel.transform);
            BuildPropertySection();
            BuildActions(panel.transform);
            ConfigureFieldInput();

            UIFactory.CreateCloseButton(panel.transform, Close);

            ApplyLayout(ElementFacet.None);
            _root!.SetActive(false);

            if (SelectionManager.Instance != null)
                SelectionManager.Instance.OnSelectionChanged += OnSelectionChanged;
        }

        private void BuildTitleAndType(Transform parent)
        {
            _titleLabel = UIFactory.CreateLabel("CtxTitle", parent, "Деталь", 20,
                Vector2.zero, new Vector2(300, TitleH), TextAnchor.MiddleCenter);
            _titleLabel.overflowMode = TextOverflowModes.Ellipsis;
            _titleLabel.enableWordWrapping = false;
            _layout.Add(TitleH, TitleGap, _titleLabel.rectTransform);

            var typeOptions = new List<string>();
            foreach (var (_, label) in StructuralChoices) typeOptions.Add(label);
            _typeDropdown = _rows.Dropdown("Тип", typeOptions, OnTypeSelected,
                RowVisibility.When(() => _target != null && GroupOf(_target) != TypeGroup.None),
                "CtxType");
        }

        private void BuildDimensions()
        {
            _name = _rows.NameField();
            _rows.SectionHeader("CtxSecDims", "Размеры");
            _w = _rows.NumberField("Ширина", RowVisibility.Always);
            _h = _rows.NumberField("Высота", RowVisibility.Always);
            _d = _rows.NumberField("Глубина", RowVisibility.Always);
            _radius = _rows.NumberField("Радиус угла", RowVisibility.For(ElementFacet.Radial));
            var cooktopOnly = RowVisibility.When(() => _target is CooktopElement);
            _cutoutW = _rows.NumberField("Ширина выреза", cooktopOnly);
            _cutoutD = _rows.NumberField("Глубина выреза", cooktopOnly);
        }

        private void BuildGrooveSection(Transform parent)
        {
            var partOnly = RowVisibility.For(ElementFacet.Part);
            var expanded = RowVisibility.For(ElementFacet.Part, () => _groovesExpanded);

            _grooveCountLabel = _rows.WideButton("CtxGrooves", "Пазы (0)", ToggleGrooves, partOnly, RowGap);
            _rows.Hint("CtxGrooveHint",
                $"Паз: ширина {AppConstants.GROOVE_WIDTH_MM} мм, глубина {AppConstants.GROOVE_DEPTH_MM} мм, отступ от кромки {AppConstants.GROOVE_OFFSET_MM} мм",
                16f, 4f, expanded);

            var sideOptions = new List<string> { "Верх", "Низ", "Лево", "Право" };
            var kindOptions = new List<string> { "Сквозной", "Глухой" };

            for (int i = 0; i < AppConstants.GROOVE_MAX_PER_PART; i++)
            {
                int index = i;
                var sideDd = UIFactory.CreateDropdown($"CtxGrooveSide{i}", parent,
                    new List<string>(sideOptions), new Vector2(-114, 0), new Vector2(104, 28),
                    _ => EditGroove(index));
                var kindDd = UIFactory.CreateDropdown($"CtxGrooveKind{i}", parent,
                    new List<string>(kindOptions), new Vector2(26, 0), new Vector2(168, 28),
                    _ => EditGroove(index));
                var delBtn = UIFactory.CreateConfirmDeleteButton($"CtxGrooveDel{i}", parent,
                    UIStyle.GlyphClose, new Vector2(148, 0), new Vector2(28, 28), () => RemoveGroove(index));
                _grooveRowSide[i] = sideDd;
                _grooveRowKind[i] = kindDd;
                _layout.AddFor(ElementFacet.Part, () => _groovesExpanded && GrooveCount() > index, 28f, 4f,
                    sideDd.GetComponent<RectTransform>(),
                    kindDd.GetComponent<RectTransform>(),
                    delBtn.GetComponent<RectTransform>());
            }

            _grooveSideDropdown = UIFactory.CreateDropdown("CtxGrooveSide", parent,
                new List<string>(sideOptions), new Vector2(-114, 0), new Vector2(104, 28), _ => { });
            _grooveKindDropdown = UIFactory.CreateDropdown("CtxGrooveKind", parent,
                new List<string>(kindOptions), new Vector2(0, 0), new Vector2(116, 28), _ => { });
            var addBtn = UIFactory.CreateButton("CtxGrooveAdd", parent, "Добавить",
                new Vector2(116, 0), new Vector2(100, 28), AddGrooveFromUI);
            _layout.AddFor(ElementFacet.Part, () => _groovesExpanded, 28f, ActionGap,
                _grooveSideDropdown.GetComponent<RectTransform>(),
                _grooveKindDropdown.GetComponent<RectTransform>(),
                addBtn.GetComponent<RectTransform>());
        }

        private void BuildEdgeSection(Transform parent)
        {
            var shown = RowVisibility.For(ElementFacet.Part, EdgesShown);

            _edgeToggle = _rows.Toggle("CtxEdges", "Кромки", true, OnEdgeBandingToggled,
                RowVisibility.For(ElementFacet.Part, EdgesEligible), RowGap);

            _edgeDiagram = BuildEdgeDiagram(parent, out float edgeDiagramH);
            _layout.AddFor(ElementFacet.Part, EdgesShown, edgeDiagramH, RowGap, _edgeDiagram);

            _edgeThickness = _rows.NumberField("Толщина кромки", shown, "мм", "EdgeThickness");
            _rows.Hint("CtxEdgeHint", "Клик по стороне — кромка вручную", 20f, ActionGap, shown,
                TextAnchor.MiddleCenter);
        }

        private void BuildGapSection(Transform parent)
        {
            _gapCountLabel = _rows.WideButton("CtxGaps", "Зазоры (0)", ToggleGaps,
                RowVisibility.When(GapsEligible), RowGap);

            CreateGapRow(parent, "Слева / справа, мм", GapSide.Left, GapSide.Right,
                out _gapLeft, out _gapRight);
            CreateGapRow(parent, "Сверху / снизу, мм", GapSide.Top, GapSide.Bottom,
                out _gapTop, out _gapBottom);
            CreateGapRow(parent, "Спереди / сзади, мм", GapSide.Front, GapSide.Back,
                out _gapFront, out _gapBack);
        }

        private void BuildFacadeSection()
        {
            var facadeOnly = RowVisibility.For(ElementFacet.Facade);

            var modeOptions = new List<string>();
            for (int i = 0; i < FacadeDoor.Count; i++)
                modeOptions.Add(FacadeDoor.Label((DoorMode)i));
            _modeDropdown = _rows.Dropdown("Дверца", modeOptions, OnModeSelected, facadeOnly, "CtxMode");

            _doorButtonLabel = _rows.WideButton("CtxDoor", "Открыть", ToggleDoor, facadeOnly, ActionGap);
            _ovenDoorLabel = _rows.WideButton("CtxOvenDoor", "Открыть дверцу", ToggleOvenDoor,
                RowVisibility.When(() => _target is OvenElement), ActionGap);
            _dishwasherDoorLabel = _rows.WideButton("CtxDishwasherDoor", "Открыть дверцу",
                ToggleDishwasherDoor, RowVisibility.When(() => _target is DishwasherElement), ActionGap);

            var fillOptions = new List<string> { "Глухой (панель)", "Витрина (пусто)", "Стекло" };
            _fillDropdown = _rows.Dropdown("Заполнение", fillOptions, OnFillSelected,
                RowVisibility.For(ElementFacet.Assembled), "CtxFill");
        }

        private void BuildDrawerSection()
        {
            var drawerOnly = RowVisibility.For(ElementFacet.Drawer);

            var typeNames = new List<string>
                { "A — борт 86 мм", "B — борт 120 мм", "C — борт 168 мм", "D — борт 200 мм" };
            _drawerTypeDropdown = _rows.Dropdown("Тип ящика", typeNames, OnDrawerTypeChanged,
                drawerOnly, "CtxDrawerType");

            var lengthNames = new List<string>();
            foreach (var l in DrawerConstants.ValidLengths) lengthNames.Add($"{l} мм");
            _drawerLengthDropdown = _rows.Dropdown("Длина", lengthNames, OnDrawerLengthChanged,
                drawerOnly, "CtxDrawerLen");

            var colorNames = new List<string> { "Антрацит", "Белый", "Чёрный" };
            _drawerColorDropdown = _rows.Dropdown("Цвет", colorNames, OnDrawerColorChanged,
                drawerOnly, "CtxDrawerColor");

            _drawerWidth = _rows.NumberField("Ширина короба", drawerOnly);

            _rows.WideButton("CtxDrawerDouble", "Двойной ящик", CreatePairedDrawer,
                RowVisibility.For(ElementFacet.Drawer, CanCreateDoubleDrawer), ActionGap);

            var upperLenNames = new List<string>();
            foreach (var l in DrawerConstants.ValidLengths) upperLenNames.Add($"{l} мм");
            (_, _drawerUpperLenDropdown) = _rows.NamedDropdown("CtxDrawerUpperLen", "Верхний ящик",
                upperLenNames, OnDrawerUpperLengthChanged,
                RowVisibility.For(ElementFacet.Drawer, HasUpperDrawer));

            _rows.WideButton("CtxDrawerRemoveUpper", "Убрать верхний ящик", RemoveUpperDrawer,
                RowVisibility.For(ElementFacet.Drawer, HasUpperDrawer), ActionGap);

            _drawerAnimLabel = _rows.WideButton("CtxDrawerAnim", "Открыть", CycleDrawerAnimation,
                drawerOnly, ActionGap);
        }

        private void BuildAttachmentSection()
        {
            (_drawerFacadeLabel, _drawerFacadeDropdown) = _rows.NamedDropdown("CtxDrawerFacade",
                DrawerFacadeLabelText, new List<string> { "(нет фасада)" }, OnDrawerFacadeSelected,
                RowVisibility.When(() => _target is IFacadeHost));
            _drawerFacadeNormalColor = _drawerFacadeDropdown.captionText.color;
            var facadeHook = _drawerFacadeDropdown.template.gameObject.AddComponent<DropdownOpenHook>();
            facadeHook.OnOpen = () =>
            {
                RebuildDrawerFacadeOptions();
                SetDrawerFacadeValue(((_target as IFacadeHost)?.AttachedFacadeName) ?? "");
            };
            facadeHook.OnAfterShow = () =>
            {
                var dd = _drawerFacadeDropdown;
                var host = _target as IFacadeHost;
                if (dd == null || host == null) return;
                var attachedName = host.AttachedFacadeName;
                if (string.IsNullOrEmpty(attachedName)) return;
                if (!IsDrawerFacadeOrphaned(attachedName, host)) return;
                ColorOrphanedDrawerFacadeItem(dd, attachedName, Color.red);
            };

            (_attachToLabel, _attachToDropdown) = _rows.NamedDropdown("CtxAttachTo", AttachToLabelText,
                new List<string> { AttachToNoneText }, OnAttachToSelected,
                RowVisibility.When(() => AttachLinks.CanBeChild(_target)));
            _attachToNormalColor = _attachToDropdown.captionText.color;
            var attachHook = _attachToDropdown.template.gameObject.AddComponent<DropdownOpenHook>();
            attachHook.OnOpen = () =>
            {
                RebuildAttachToOptions();
                SetAttachToValue(_target != null ? _target.AttachedToName : "");
            };
            attachHook.OnAfterShow = () =>
            {
                var dd = _attachToDropdown;
                if (dd == null || _target == null) return;
                if (!AttachLinks.IsDetached(_target)) return;
                ColorOrphanedDrawerFacadeItem(dd, _target.AttachedToName, Color.red);
            };
        }

        private void BuildWindowSection()
        {
            var windowOnly = RowVisibility.For(ElementFacet.Window);
            var notDoor = RowVisibility.For(ElementFacet.Window, () => !_currentIsDoor);

            _tintDropdown = _rows.Dropdown("Стекло", new List<string> { "Прозрачное", "Тонированное" },
                OnTintSelected, notDoor, "CtxTint");
            _sillProtrusion = _rows.NumberField("Подоконник", notDoor);
            _sashTypeDropdown = _rows.Dropdown("Створка", new List<string> { "Стекло", "Глухая" },
                OnSashTypeSelected, RowVisibility.For(ElementFacet.Door), "CtxSashType");

            var winModeOptions = new List<string>
            {
                FacadeDoor.Label(DoorMode.HingeFrontLeft),
                FacadeDoor.Label(DoorMode.HingeFrontRight),
                FacadeDoor.Label(DoorMode.HingeFrontTop),
                FacadeDoor.Label(DoorMode.HingeFrontBottom),
            };
            _winModeDropdown = _rows.Dropdown("Открывание", winModeOptions, OnWindowModeSelected,
                windowOnly, "CtxWinMode");
            _winDoorButtonLabel = _rows.WideButton("CtxWinDoor", "Открыть", ToggleWindowDoor,
                windowOnly, ActionGap);
        }

        private void BuildFurnitureSection()
        {
            _legInset = _rows.NumberField("Сдвиг опор", RowVisibility.For(ElementFacet.Table));
            _midHeight = _rows.NumberField("Средняя секция", RowVisibility.For(ElementFacet.Pillar));
        }

        private void BuildLightSection()
        {
            var lightOnly = RowVisibility.For(ElementFacet.Light);
            var advanced = RowVisibility.For(ElementFacet.Light, () => _lightAdvancedExpanded);

            _lightTemp = _rows.NumberField("Температура", lightOnly, "K");
            _lightPower = _rows.NumberField("Мощность", lightOnly, "Вт");
            _lightDiffusion = _rows.NumberField("Рассеивание", lightOnly, "%");
            _lightBeam = _rows.NumberField("Угол пучка", lightOnly, "°");
            _lightSoftness = _rows.NumberField("Мягкость края", lightOnly, "%");
            _lightUp = _rows.NumberField("Свет вверх", lightOnly, "%");

            _lightShapeDropdown = _rows.Dropdown("Форма потока", new List<string> { "Плафон", "Шар" },
                OnLightShapeSelected, lightOnly, "CtxLightShape");
            _lightShadowDropdown = _rows.Dropdown("Тени лампы",
                new List<string> { "Нет", "Жёсткие", "Мягкие" }, OnLightShadowSelected,
                lightOnly, "CtxLightShadow");
            _lightShadowStrength = _rows.NumberField("Сила тени", lightOnly, "%");

            _lightAdvancedLabel = _rows.WideButton("CtxLightAdv",
                $"Тонкая настройка  {UIStyle.GlyphCollapsed}", ToggleLightAdvanced, lightOnly, RowGap);

            _lightGlow = _rows.NumberField("Свечение плафона", advanced, "%");
            _lightDrop = _rows.NumberField("Отступ вниз", advanced, "мм");
            _lightUpCone = _rows.NumberField("Верхний конус", advanced, "%");
            _lightUpRange = _rows.NumberField("Верхний радиус", advanced, "%");
            _lightRangeMin = _rows.NumberField("Радиус при 0 %", advanced, "мм");
            _lightRangeMax = _rows.NumberField("Радиус при 100 %", advanced, "мм");
            _lightEfficacy = _rows.NumberField("Светоотдача", advanced, "лм/Вт");
            _lightLumens = _rows.NumberField("Калибровка", advanced, "лм/ед");
        }

        private void BuildPositionSection(Transform parent)
        {
            _rows.SectionHeader("CtxSecPos", "Положение");

            _x = _rows.TriField("X, мм", TriCol1);
            _y = _rows.TriField("Y, мм", TriCol2);
            _z = _rows.TriField("Z, мм", TriCol3);
            _layout.EndTriRow(TriLabelH, 2f, FieldH, RowGap, ElementFacet.None);

            _rx = _rows.TriField("X, °", TriCol1);
            _ry = _rows.TriField("Y, °", TriCol2);
            _rz = _rows.TriField("Z, °", TriCol3);
            _layout.AddRotationXZ(_layout.PendingTriLabels[0]);
            _layout.AddRotationXZ(_layout.PendingTriLabels[2]);
            _layout.AddRotationXZ(_layout.PendingTriFields[0]);
            _layout.AddRotationXZ(_layout.PendingTriFields[2]);
            _layout.EndTriRow(TriLabelH, 2f, FieldH, RowGap, ElementFacet.Window);

            var rotLbl = UIFactory.CreateLabel("CtxRotLbl", parent, "Повернуть на 90°:", 15,
                Vector2.zero, new Vector2(340, RotLblH), TextAnchor.MiddleCenter);
            _layout.AddExcept(ElementFacet.Window, RotLblH, RotLblGap, rotLbl.rectTransform);

            var rotX = UIFactory.CreateButton("CtxRotX", parent, "X 90°",
                new Vector2(-112, 0), new Vector2(112, BtnH), () => RotateAxis(Vector3.right));
            var rotY = UIFactory.CreateButton("CtxRotY", parent, "Y 90°",
                new Vector2(0, 0), new Vector2(112, BtnH), () => RotateAxis(Vector3.up));
            var rotZ = UIFactory.CreateButton("CtxRotZ", parent, "Z 90°",
                new Vector2(112, 0), new Vector2(112, BtnH), () => RotateAxis(Vector3.forward));
            _layout.AddExcept(ElementFacet.Window, BtnH, ActionGap,
                rotX.GetComponent<RectTransform>(),
                rotY.GetComponent<RectTransform>(),
                rotZ.GetComponent<RectTransform>());
            _layout.AddRotationXZ(rotX.GetComponent<RectTransform>());
            _layout.AddRotationXZ(rotZ.GetComponent<RectTransform>());

            var rotY180 = UIFactory.CreateButton("CtxRotY180", parent, "Y 180°",
                new Vector2(0, 0), new Vector2(RowWidth, BtnH), () => RotateAxis(Vector3.up, 180f));
            _layout.AddFor(ElementFacet.Window, BtnH, ActionGap, rotY180.GetComponent<RectTransform>());
        }

        private void BuildMaterialSection(Transform parent)
        {
            _rows.SectionHeader("CtxSecMat", "Материал");

            var matOptions = MaterialOptions.DisplayNames();
            _materialDropdown = _rows.Dropdown("Текстура", matOptions, OnMaterialSelected,
                RowVisibility.When(() => !_currentIsTable), "CtxMaterial");
            _tabletopMaterialDropdown = _rows.Dropdown("Столешница", new List<string>(matOptions),
                OnMaterialSelected, RowVisibility.For(ElementFacet.Table), "CtxTableTop");
            _legsMaterialDropdown = _rows.Dropdown("Ножки", new List<string>(matOptions),
                OnLegsMaterialSelected, RowVisibility.For(ElementFacet.Table), "CtxTableLegs");

            DropdownHover.Attach(_materialDropdown,
                option => PreviewMaterial(legs: false, optionIndex: option), EndMaterialPreview);
            DropdownHover.Attach(_tabletopMaterialDropdown,
                option => PreviewMaterial(legs: false, optionIndex: option), EndMaterialPreview);
            DropdownHover.Attach(_legsMaterialDropdown,
                option => PreviewMaterial(legs: true, optionIndex: option), EndMaterialPreview);

            _textures.Build(parent, matOptions);
        }

        private void BuildPropertySection()
        {
            _transparentToggle = _rows.Toggle("CtxTransparent", "Прозрачный", false, v =>
            {
                if (_target == null) return;
                _target.Transparent = v;
                if (ElementHighlighter.Instance != null)
                    ElementHighlighter.Instance.ApplyForElement(_target);
                if (SelectionManager.Instance != null)
                    SelectionManager.Instance.RefreshHighlight(_target);
            }, RowVisibility.Always, 7f);

            _lockToggle = _rows.Toggle("CtxLock", "Закрепить", false,
                v => { if (_target != null) _target.Movable = !v; },
                RowVisibility.Always, UIStyle.GapSection);
        }

        private void BuildActions(Transform parent)
        {
            var dup = UIFactory.CreateButton("CtxDup", parent, "Дублировать",
                new Vector2(-91, 0), new Vector2(150, 32), Duplicate);
            var del = UIFactory.CreateDangerButton("CtxDel", parent, "Удалить",
                new Vector2(91, 0), new Vector2(150, 32), Delete);
            _layout.Add(32f, 0f,
                dup.GetComponent<RectTransform>(),
                del.GetComponent<RectTransform>());
        }

        private void ConfigureFieldInput()
        {
            foreach (var f in ArithmeticIntFields())
            {
                if (f == null) continue;
                f.contentType = TMP_InputField.ContentType.Custom;
                f.onValidateInput = (text, idx, ch) =>
                    ExpressionParser.IsValidDimensionChar(ch) ? ch : '\0';
            }
            foreach (var f in new[] { _rx, _ry, _rz })
            {
                if (f == null) continue;
                f.contentType = TMP_InputField.ContentType.Custom;
                f.onValidateInput = (text, idx, ch) =>
                    ExpressionParser.IsValidDimensionChar(ch, allowDecimal: true) ? ch : '\0';
            }
            _name!.onValidateInput = (text, charIndex, ch) =>
                ElementNaming.IsValid(ch.ToString()) ? ch : '\0';
        }

        private TMP_InputField?[] ArithmeticIntFields()
        {
            var fields = new List<TMP_InputField?>
            {
                _w, _h, _d, _radius, _cutoutW, _cutoutD, _drawerWidth, _legInset, _midHeight,
                _sillProtrusion, _lightTemp, _lightPower, _lightDiffusion, _lightUp, _lightBeam,
                _x, _y, _z,
            };
            fields.AddRange(LightExtraFields());
            fields.AddRange(GapFields());
            return fields.ToArray();
        }

        private void OnDestroy()
        {
            if (SelectionManager.Instance != null)
                SelectionManager.Instance.OnSelectionChanged -= OnSelectionChanged;
        }

        // ── Отложенное закрытие меню ──────────────────────────────────
        // Когда SelectionManager.Select() вызывает DeselectAll(),
        // OnSelectionChanged(null) приходит ПЕРЕД OnSelectionChanged(newElement).
        // Чтобы меню не закрылось и тут же не открылось заново — откладываем
        // закрытие на конец кадра. Если в том же кадре пришёл новый элемент —
        // отмена отложенного закрытия и обновление меню.
        private int _deferCloseFrame = -1;

        private void OnSelectionChanged(KitchenElement? element)
        {
            if (_openInProgress) return;
            if (_root == null || !_root.activeSelf) return;
            if (element == null)
            {
                _deferCloseFrame = Time.frameCount;
                return;
            }
            _deferCloseFrame = -1;
            if (element != _target)
                Open(element);
        }

        private void ProcessDeferredClose()
        {
            if (_deferCloseFrame >= 0 && _deferCloseFrame < Time.frameCount)
            {
                _deferCloseFrame = -1;
                Close();
            }
        }

        /// <summary>Строка секции зазоров: подпись и пара полей — по одному на
        /// противоположные стороны. Наведение на поле подсвечивает СВОЮ сторону
        /// прямо на детали (SideHighlighter) — иначе «слева» ничего не говорит о
        /// том, где это в сцене у повёрнутой детали.</summary>
        private void CreateGapRow(Transform parent, string label, GapSide first, GapSide second,
            out TMP_InputField? firstField, out TMP_InputField? secondField)
        {
            const float smallFieldW = 45f;
            const float fieldGap = 4f;
            const float gapFieldX = FieldX - 27f;

            var lbl = UIFactory.CreateLabel($"L_Gap_{first}{second}", parent, label, 13,
                new Vector2(LabelX, 0), new Vector2(140, 20), TextAnchor.MiddleLeft);
            firstField = CreateGapField(parent, first, gapFieldX, smallFieldW);
            secondField = CreateGapField(parent, second, gapFieldX + smallFieldW + fieldGap, smallFieldW);

            _layout.AddWhen(GapsExpanded, FieldH, 4f, lbl.rectTransform,
                firstField.GetComponent<RectTransform>(),
                secondField.GetComponent<RectTransform>());
        }

        private TMP_InputField CreateGapField(Transform parent, GapSide side, float x, float width)
        {
            var field = UIFactory.CreateInputField($"F_gap{side}", parent, "0",
                new Vector2(x, 0), new Vector2(width, 22));
            PointerHover.Attach(field.gameObject,
                () => OnGapSideHover(side, true), () => OnGapSideHover(side, false));
            return field;
        }

        /// <summary>Наведение на поле зазора — подсветить его сторону так же,
        /// как это делает схема кромок: торец плюс каёмки на соседних гранях.</summary>
        private void OnGapSideHover(GapSide side, bool entered)
        {
            if (_target == null || !_target.SupportsGaps) return;
            if (entered) SideHighlighter.ShowGapSide(_target, side);
            else SideHighlighter.Hide();
        }

        private void ToggleLightAdvanced()
        {
            _lightAdvancedExpanded = !_lightAdvancedExpanded;
            UpdateLightAdvancedLabel();
            RelayoutForTarget();
        }

        private void UpdateLightAdvancedLabel()
        {
            if (_lightAdvancedLabel != null)
                _lightAdvancedLabel.text =
                    $"Тонкая настройка  {(_lightAdvancedExpanded ? UIStyle.GlyphExpanded : UIStyle.GlyphCollapsed)}";
        }

        /// <summary>Тонкие параметры лампы: поле ↔ свойство ↔ значение «из
        /// коробки». Их полтора десятка, и каждый нужен в четырёх местах
        /// (Open, Apply, обновление извне, подсветка правок) — таблица держит
        /// их синхронными вместо четырёх одинаковых простыней.</summary>
        private (TMP_InputField? field, System.Func<LightSourceElement, int> get,
            System.Action<LightSourceElement, int> set, int def)[] LightExtraBindings()
        {
            System.Func<LightSourceElement, int> G(System.Func<LightSourceElement, int> f) => f;
            System.Action<LightSourceElement, int> S(System.Action<LightSourceElement, int> f) => f;
            return new[]
            {
                (_lightSoftness, G(l => l.SoftnessPct), S((l, v) => l.SoftnessPct = v), LightSourceElement.DEFAULT_SOFTNESS_PCT),
                (_lightShadowStrength, G(l => l.ShadowStrengthPct), S((l, v) => l.ShadowStrengthPct = v), LightSourceElement.DEFAULT_SHADOW_STRENGTH_PCT),
                (_lightGlow, G(l => l.GlowPct), S((l, v) => l.GlowPct = v), LightSourceElement.DEFAULT_GLOW_PCT),
                (_lightDrop, G(l => l.DropMM), S((l, v) => l.DropMM = v), LightSourceElement.DEFAULT_DROP_MM),
                (_lightUpCone, G(l => l.UpConePct), S((l, v) => l.UpConePct = v), LightSourceElement.DEFAULT_UP_CONE_PCT),
                (_lightUpRange, G(l => l.UpRangePct), S((l, v) => l.UpRangePct = v), LightSourceElement.DEFAULT_UP_RANGE_PCT),
                (_lightRangeMin, G(l => l.RangeMinMM), S((l, v) => l.RangeMinMM = v), LightSourceElement.DEFAULT_RANGE_MIN_MM),
                (_lightRangeMax, G(l => l.RangeMaxMM), S((l, v) => l.RangeMaxMM = v), LightSourceElement.DEFAULT_RANGE_MAX_MM),
                (_lightEfficacy, G(l => l.EfficacyLmPerW), S((l, v) => l.EfficacyLmPerW = v), LightSourceElement.DEFAULT_EFFICACY_LM_PER_W),
                (_lightLumens, G(l => l.LumensPerUnit), S((l, v) => l.LumensPerUnit = v), LightSourceElement.DEFAULT_LUMENS_PER_UNIT),
            };
        }

        private TMP_InputField?[] LightExtraFields()
        {
            var bindings = LightExtraBindings();
            var fields = new TMP_InputField?[bindings.Length];
            for (int i = 0; i < bindings.Length; i++) fields[i] = bindings[i].field;
            return fields;
        }

        private void OnLightShapeSelected(int index)
        {
            if (_target is LightSourceElement ls)
                ls.Shape = index == 1 ? LampShape.Sphere : LampShape.Plafond;
        }

        private void OnLightShadowSelected(int index)
        {
            if (_target is LightSourceElement ls)
                ls.Shadow = (LampShadow)Mathf.Clamp(index, 0, 2);
        }

        private void Update()
        {
            using var _ = PerfMarkers.ContextMenuUpdate.Auto();
            ProcessDeferredClose();

            if (Input.GetKeyDown(KeyCode.Escape) && _root != null && _root.activeSelf)
                Close();

            if (_root != null && _root.activeSelf && _target != null)
            {
                RefreshTransformFields();

                // Пазы могли измениться мимо меню (undo/redo, MCP-команды).
                if (_target.SupportsGrooves
                    && GrooveFingerprint(_target.Grooves) != _grooveFingerprint)
                {
                    RefreshGrooveUI();
                    RelayoutForTarget();
                }

                // Кромки зависят от соседей, а не только от самой детали:
                // отодвинули полку от боковины — торец открылся. Опрашиваем
                // сцену редко (см. EdgeRefreshFrames), это не горячий путь.
                if (_target.SupportsEdges && --_edgeRefreshCountdown <= 0)
                    RefreshEdgeUI();

                if (!_textures.PreviewActive && _textures.ChangedOutsideTheMenu())
                {
                    _textures.Refresh();
                    RelayoutForTarget();
                }
            }

            // Накладки подсветки стороны живут в мировых координатах и своего
            // апдейта не имеют: деталь могли сдвинуть, изменить или удалить
            // (undo, MCP) прямо во время наведения.
            SideHighlighter.Sync();
        }

        private bool IsAnyFieldFocused()
        {
            foreach (var f in new[] { _name, _w, _h, _d, _radius, _cutoutW, _cutoutD, _drawerWidth, _legInset, _midHeight, _sillProtrusion, _edgeThickness, _x, _y, _z, _rx, _ry, _rz })
                if (f != null && f.isFocused) return true;
            foreach (var f in GapFields())
                if (f != null && f.isFocused) return true;
            return false;
        }

        private void RefreshTransformFields()
        {
            if (_target == null) return;
            if (_target is FacadeElement f && !f.IsDoorClosed) return;
            if (_target is WindowElement w && !w.IsDoorClosed) return;
            if (_target is DoorElement d && !d.IsDoorClosed) return;

            var pos = _target.transform.position;
            _fields.RefreshUnfocused(_x, ToMM(pos.x));
            _fields.RefreshUnfocused(_y, ToMM(pos.y));
            _fields.RefreshUnfocused(_z, ToMM(pos.z));

            // Связь могла разъехаться прямо сейчас (деталь двигают мышью) —
            // подпись краснеет в реальном времени, как и у фасада ящика.
            if (AttachLinks.CanBeChild(_target)) UpdateAttachToCaptionColor();

            var eu = _target.transform.eulerAngles;
            _fields.RefreshUnfocused(_rx, eu.x.ToString("F1"));
            _fields.RefreshUnfocused(_ry, eu.y.ToString("F1"));
            _fields.RefreshUnfocused(_rz, eu.z.ToString("F1"));

            // Размеры, имя, радиус угла, зазоры — тоже обновляем в реальном времени
            var dims = _target.DimensionsMM;
            _fields.RefreshUnfocused(_w, dims.x.ToString());
            _fields.RefreshUnfocused(_h, dims.y.ToString());
            _fields.RefreshUnfocused(_d, dims.z.ToString());
            var radial = _target as RadialShelfElement;
            if (radial != null)
                _fields.RefreshUnfocused(_radius, radial.CornerRadius.ToString());

            if (_target is CooktopElement cooktopRefresh)
            {
                _fields.RefreshUnfocused(_cutoutW, cooktopRefresh.CutoutWidthMM.ToString());
                _fields.RefreshUnfocused(_cutoutD, cooktopRefresh.CutoutDepthMM.ToString());
            }

            var drawerRef = _target as DrawerElement;
            if (drawerRef != null && _drawerWidth != null)
                _fields.RefreshUnfocused(_drawerWidth, drawerRef.BoxWidth.ToString());

            _fields.RefreshUnfocused(_name, _target.PartName);
            RefreshTitle();

            if (_target.SupportsGaps)
            {
                var fields = GapFields();
                for (int i = 0; i < GapSides.All.Length; i++)
                    _fields.RefreshUnfocused(fields[i], _target.GapOf(GapSides.All[i]).ToString());
            }
            RefreshGapUI();

            var table = _target as TableElement;
            if (table != null && _legInset != null)
                _fields.RefreshUnfocused(_legInset, table.LegInsetMM.ToString());

            var radiusTable = _target as RadiusTableElement;
            if (radiusTable != null && _legInset != null)
                _fields.RefreshUnfocused(_legInset, radiusTable.LegInsetMM.ToString());

            var pillar = _target as PillarElement;
            if (pillar != null && _midHeight != null)
                _fields.RefreshUnfocused(_midHeight, pillar.MidHeightMM.ToString());

            var lightRt = _target as LightSourceElement;
            if (lightRt != null)
            {
                if (_lightTemp != null) _fields.RefreshUnfocused(_lightTemp, lightRt.TemperatureK.ToString());
                if (_lightPower != null) _fields.RefreshUnfocused(_lightPower, lightRt.PowerW.ToString());
                if (_lightDiffusion != null) _fields.RefreshUnfocused(_lightDiffusion, lightRt.DiffusionPct.ToString());
                if (_lightBeam != null) _fields.RefreshUnfocused(_lightBeam, lightRt.BeamAngleDeg.ToString());
                if (_lightUp != null) _fields.RefreshUnfocused(_lightUp, lightRt.UpLightPct.ToString());
                foreach (var b in LightExtraBindings())
                    if (b.field != null) _fields.RefreshUnfocused(b.field, b.get(lightRt).ToString());
                if (_lightShapeDropdown != null) _lightShapeDropdown.SetValueWithoutNotify((int)lightRt.Shape);
                if (_lightShadowDropdown != null) _lightShadowDropdown.SetValueWithoutNotify((int)lightRt.Shadow);
            }

            var window = _target as WindowElement;
            if (window != null && _sillProtrusion != null)
                _fields.RefreshUnfocused(_sillProtrusion, window.SillProtrusionMM.ToString());
        }

        /// <summary>Позиция в мм: единый формат чисел UI (правило 1).</summary>
        private static string ToMM(float meters) =>
            Mathf.RoundToInt(meters / AppConstants.MM_TO_UNITS).ToString();

        /// <summary>Заголовок «Тип — Имя» (обновляется при открытии и переименовании).</summary>
        private void RefreshTitle()
        {
            if (_titleLabel == null || _target == null) return;
            _titleLabel.text = $"{_currentTypeName} — {_target.PartName}";
        }

        public void Open(KitchenElement element)
        {
            if (element == null) return;

            // Подсветка стороны принадлежит ПРЕДЫДУЩЕЙ детали: схема кромок под
            // курсором пересобирается, PointerExit по старой полосе не придёт.
            SideHighlighter.Hide();

            // Верхний ящик пары своего окна свойств не имеет — открываем нижний.
            if (element is DrawerElement upper && upper.IsUpperDrawer)
            {
                var lower = upper.FindPaired();
                if (lower != null) element = lower;
            }

            _openInProgress = true;
            try
            {
                // Предпросмотр принадлежал прошлому элементу — снимаем ДО смены
                // цели, иначе показанная накладка осталась бы на нём насовсем.
                _textures.EndPreview();
                EndMaterialPreview();
                _target = element;
                _groovesExpanded = false; // список пазов открывается свёрнутым
                _textures.Collapse();
                _gapsExpanded = false;     // и зазоры
                _lightAdvancedExpanded = false; // калибровка лампы — тоже
                UpdateLightAdvancedLabel();
                // Ручки области принадлежали прошлому элементу.
                TextureOverlayHandles.End();
                if (SelectionManager.Instance != null)
                    SelectionManager.Instance.Select(element);

                bool isFacade = element is FacadeElement;
                bool isRadial = element is RadialShelfElement;
                bool isDrawer = element is DrawerElement;
                bool isTable = element is TableElement;
                bool isRadiusTable = element is RadiusTableElement;
                bool isPillar = element is PillarElement;
                bool isWindow = element is WindowElement;
                bool isDoor = element is DoorElement;
            _currentIsTable = isTable || isRadiusTable;
            _currentIsDoor = isDoor;
                // Заголовок различает и подтипы («Сборный фасад» ≠ «Фасад») и
                // конкретный элемент (имя после тире).
                // У готовой модели в заголовке стоит она сама — «Варочная» ничего
                // не сказало бы о том, что размеры залочены производителем.
                _currentTypeName = element is CooktopElement fixedCooktop && fixedCooktop.HasFixedSize
                        ? fixedCooktop.Model
                        : element is CooktopElement ? "Варочная"
                    // Духовка — всегда готовая модель, поэтому в заголовке она
                    // сама: «Духовка» умолчала бы о том, что размеры залочены.
                    : element is OvenElement ? OvenElement.MODEL
                    // Посудомойка — тоже всегда готовая модель.
                    : element is DishwasherElement ? DishwasherElement.MODEL
                    : element is SinkElement ? "Мойка"
                    : element is LightSourceElement ? "Источник света"
                    : isPillar ? "Опора"
                    : isRadiusTable ? "Радиусный стол"
                    : isTable ? "Стол"
                    : isDrawer ? DrawerConstants.GetDefaultName(((DrawerElement)element).System)
                    : isWindow ? "Окно"
                    : isDoor ? "Дверь"
                    : element is PanelElement ? "ДВП/ХДФ"
                    : isRadial ? "Радиусная полка"
                    : element is AssembledFacadeElement ? "Сборный фасад"
                    : isFacade ? "Фасад"
                    : "Деталь";
                RefreshTitle();
                RefreshTypeDropdown(element);

                var dims = element.DimensionsMM;
                _name!.text = element.PartName;
                _w!.text = dims.x.ToString();
                _h!.text = dims.y.ToString();
                _d!.text = dims.z.ToString();
                var radial = element as RadialShelfElement;
                _radius!.text = radial != null
                    ? radial.CornerRadius.ToString()
                    : AppConstants.RADIAL_CORNER_RADIUS_DEFAULT.ToString();

                var cooktopEl = element as CooktopElement;
                _cutoutW!.text = (cooktopEl != null
                    ? cooktopEl.CutoutWidthMM : CooktopElement.DEFAULT_CUTOUT_WIDTH_MM).ToString();
                _cutoutD!.text = (cooktopEl != null
                    ? cooktopEl.CutoutDepthMM : CooktopElement.DEFAULT_CUTOUT_DEPTH_MM).ToString();

                var facade = element as FacadeElement;
                WriteGapFields(element);
                UpdateDoorButton(facade);
                UpdateModeDropdown(facade);
                UpdateModeDropdownEnabled(facade);

                var assembled = element as AssembledFacadeElement;
                if (assembled != null && _fillDropdown != null)
                    _fillDropdown.SetValueWithoutNotify(FillToIndex(assembled.Fill));

                var drawer = element as DrawerElement;
                if (drawer != null)
                {
                    if (_drawerTypeDropdown != null)
                        _drawerTypeDropdown.SetValueWithoutNotify(DrawerConstants.TypeIndex(drawer.Type));
                    if (_drawerLengthDropdown != null)
                        _drawerLengthDropdown.SetValueWithoutNotify(System.Array.IndexOf(DrawerConstants.ValidLengths, drawer.NominalLength));
                    if (_drawerColorDropdown != null)
                        _drawerColorDropdown.SetValueWithoutNotify((int)drawer.Color);
                    if (_drawerWidth != null)
                        _drawerWidth.text = drawer.BoxWidth.ToString();
                    var upperDrawer = drawer.FindPaired();
                    if (_drawerUpperLenDropdown != null && upperDrawer != null)
                        _drawerUpperLenDropdown.SetValueWithoutNotify(
                            System.Array.IndexOf(DrawerConstants.ValidLengths, upperDrawer.NominalLength));
                    UpdateDrawerAnimButton(drawer);
                }

                if (element is OvenElement ovenEl) UpdateOvenDoorButton(ovenEl);
                if (element is DishwasherElement dwEl) UpdateDishwasherDoorButton(dwEl);

                // Прикрепление к другой детали — только у обычной дощечки.
                if (AttachLinks.CanBeChild(element))
                {
                    RebuildAttachToOptions();
                    SetAttachToValue(element.AttachedToName);
                }

                // Пристёгнутый фасад — общая строка ящика и посудомойки.
                var facadeHost = element as IFacadeHost;
                if (facadeHost != null)
                {
                    if (_drawerFacadeLabel != null)
                        _drawerFacadeLabel.text = isDrawer ? DrawerFacadeLabelText : HostFacadeLabelText;
                    RebuildDrawerFacadeOptions();
                    SetDrawerFacadeValue(facadeHost.AttachedFacadeName);
                }

                var table = element as TableElement;
                if (table != null && _legInset != null)
                    _legInset.text = table.LegInsetMM.ToString();

                var radiusTable = element as RadiusTableElement;
                if (radiusTable != null && _legInset != null)
                    _legInset.text = radiusTable.LegInsetMM.ToString();

                var pillar = element as PillarElement;
                if (pillar != null && _midHeight != null)
                    _midHeight.text = pillar.MidHeightMM.ToString();

                var lightEl = element as LightSourceElement;
                if (lightEl != null)
                {
                    if (_lightTemp != null) _lightTemp.text = lightEl.TemperatureK.ToString();
                    if (_lightPower != null) _lightPower.text = lightEl.PowerW.ToString();
                    if (_lightDiffusion != null) _lightDiffusion.text = lightEl.DiffusionPct.ToString();
                    if (_lightBeam != null) _lightBeam.text = lightEl.BeamAngleDeg.ToString();
                    if (_lightUp != null) _lightUp.text = lightEl.UpLightPct.ToString();
                    foreach (var b in LightExtraBindings())
                        if (b.field != null) b.field.text = b.get(lightEl).ToString();
                    if (_lightShapeDropdown != null) _lightShapeDropdown.SetValueWithoutNotify((int)lightEl.Shape);
                    if (_lightShadowDropdown != null) _lightShadowDropdown.SetValueWithoutNotify((int)lightEl.Shadow);
                }

                var window = element as WindowElement;
                if (window != null)
                {
                    if (_tintDropdown != null)
                        _tintDropdown.SetValueWithoutNotify((int)window.Tint);
                    if (_sillProtrusion != null)
                        _sillProtrusion.text = window.SillProtrusionMM.ToString();
                    if (_winDoorButtonLabel != null)
                        _winDoorButtonLabel.text = window.IsOpen ? "Закрыть" : "Открыть";
                    if (_winModeDropdown != null)
                        _winModeDropdown.SetValueWithoutNotify((int)window.Mode);
                }

                var door = element as DoorElement;
                if (door != null)
                {
                    if (_sashTypeDropdown != null)
                        _sashTypeDropdown.SetValueWithoutNotify((int)door.SashType);
                    if (_winDoorButtonLabel != null)
                        _winDoorButtonLabel.text = door.IsOpen ? "Закрыть" : "Открыть";
                    if (_winModeDropdown != null)
                        _winModeDropdown.SetValueWithoutNotify((int)door.Mode);
                }

                // Габариты ящика (контурный бокс) вычисляются из типа/длины/ширины —
                // прямое редактирование недоступно, поля затемняются. У готовой
                // техники размеры и ниша врезки заданы производителем — тоже серые.
                bool fixedSize = FixedSize.IsFixed(element);
                SetDimensionFieldsEditable(!isDrawer && !fixedSize);
                SetDimensionFieldEditable(_cutoutW, !fixedSize);
                SetDimensionFieldEditable(_cutoutD, !fixedSize);
                // Глубину окна диктует толщина стены — поле только для чтения.
                if (isWindow || isDoor) SetDimensionFieldEditable(_d, false);
                // Ширина и глубина опоры фиксированы — только для чтения.
                if (isPillar) { SetDimensionFieldEditable(_w, false); SetDimensionFieldEditable(_d, false); }

                if (_materialDropdown != null)
                {
                    RebuildMaterialOptions();
                    _materialDropdown.SetValueWithoutNotify(MaterialOptions.IndexOf(element.MaterialId));
                    _materialDropdown.RefreshShownValue();
                }

                var tbl = element as TableElement;
                var rTbl = element as RadiusTableElement;
                if (_tabletopMaterialDropdown != null && (tbl != null || rTbl != null))
                {
                    RebuildTabletopMaterialOptions();
                    var topId = tbl != null ? tbl.TabletopMaterialId : rTbl!.TabletopMaterialId;
                    _tabletopMaterialDropdown.SetValueWithoutNotify(MaterialOptions.IndexOf(topId));
                    _tabletopMaterialDropdown.RefreshShownValue();
                }
                if (_legsMaterialDropdown != null && (tbl != null || rTbl != null))
                {
                    RebuildLegsMaterialOptions();
                    var legsId = tbl != null ? tbl.LegsMaterialId : rTbl!.LegsMaterialId;
                    _legsMaterialDropdown.SetValueWithoutNotify(MaterialOptions.IndexOf(legsId));
                    _legsMaterialDropdown.RefreshShownValue();
                }

                // Пересчитываем раскладку под режим: секция зазоров показывается
                // только для фасадов, радиус — только для радиусной полки, сдвиг опор — только для столов (включая радиусные), пазы — только для базовой детали, панель сама подгоняется по высоте.
            RefreshGrooveUI();
            RefreshEdgeUI();
            if (element.SupportsTextureOverlays) _textures.RebuildMaterialOptions();
            _textures.Refresh();
            RelayoutForTarget();

                RefreshTransformFields();
                _transparentToggle!.SetIsOnWithoutNotify(element.Transparent);
                _lockToggle!.SetIsOnWithoutNotify(!element.Movable);

                _fields.ClearHighlights();
                TrackAllFields();

                _root!.transform.SetAsLastSibling();
                _root.SetActive(true);
            }
            finally
            {
                _openInProgress = false;
            }
        }

        public void Close()
        {
            // Панель гаснет без PointerExit по полосе кромки — подсветку стороны
            // снимаем сами, иначе накладки остаются висеть на детали.
            SideHighlighter.Hide();
            // Панель закрыли с раскрытым списком декора — показанная накладка не
            // должна пережить закрытие, как и подсветка стороны.
            _textures.EndPreview();
            // …и показанный наведением декор тоже: список закрылся вместе с
            // панелью, onExit по нему уже не придёт.
            EndMaterialPreview();
            // Ручки области жили только пока открыто меню: без него их нечем
            // выключить, и они перехватывали бы клики по сцене.
            TextureOverlayHandles.End();
            _target = null;
            if (_root != null) _root.SetActive(false);
        }

        /// <summary>
        /// Применить содержимое полей к элементу ОДНИМ шагом отмены.
        ///
        /// Правило «undo на всё» здесь держится не перечислением полей, а двумя
        /// механизмами сразу:
        ///   • снимок всех свойств, помеченных <see cref="UndoableAttribute"/>,
        ///     до и после — разница уезжает в <see cref="SetPropertiesCommand"/>,
        ///     поэтому НОВОЕ свойство откатывается само, без правок этого файла;
        ///   • BeginCapture/EndCapture — команды, которые применение выдало по
        ///     дороге (размер, вырез, кромка), склеиваются в одну составную,
        ///     иначе одна правка стоила бы пользователю нескольких Ctrl+Z.
        /// </summary>
        private void Apply()
        {
            if (_target == null) return;
            var target = _target;
            var propsBefore = UndoableProperties.Capture(target);

            CommandStack.BeginCapture();
            try
            {
                ApplyFields(target);
                // Снимок «после» — до перерисовки полей: она читает уже применённое.
                var propsAfter = UndoableProperties.Capture(target);
                var propsCommand = SetPropertiesCommand.TryCreate(target, propsBefore, propsAfter);
                if (propsCommand != null) CommandStack.Execute(propsCommand);
            }
            finally
            {
                CommandStack.EndCapture($"Свойства {target.PartName}", commit: true);
            }

            RefreshAfterApply(target);
        }

        private void ApplyFields(KitchenElement target)
        {
            _fields.ForgetRejections();
            // Правки размеров/позиции применяем к закрытой (логической) позе.
            if (target is FacadeElement fac) { fac.ForceClose(); UpdateDoorButton(fac); }
            if (target is DrawerElement dr) { dr.ForceClose(); UpdateDrawerAnimButton(dr); }
            if (target is WindowElement win) { win.ForceClose(); if (_winDoorButtonLabel != null) _winDoorButtonLabel.text = "Открыть"; }
            if (target is DoorElement doorElApp) { doorElApp.ForceClose(); if (_winDoorButtonLabel != null) _winDoorButtonLabel.text = "Открыть"; }
            // Деталь может ехать за ОТКРЫТЫМ родителем (нестандартный ящик):
            // правки идут в позу покоя, поэтому предков сперва захлопываем.
            AttachLinks.ForceRest(target);

            var oldDims = target.DimensionsMM;
            var oldPos = target.transform.position;
            var oldRot = target.transform.rotation;

            // Через DrawerLinks: переименование обновляет обратные ссылки
            // (PairedDrawerName пары, AttachedFacadeName ящиков с этим фасадом)
            // и само чистит имя/разрешает коллизию суффиксом «_N».
            DrawerLinks.Rename(target, string.IsNullOrWhiteSpace(_name!.text) ? "Board" : _name!.text);
            target.gameObject.name = target.PartName;

            var radial = target as RadialShelfElement;
            var drawer = target as DrawerElement;
            var pillar = target as PillarElement;
            var table = target as TableElement;
            var radiusTable = target as RadiusTableElement;
            if (radial != null)
            {
                target.DimensionsMM = new Vector3Int(
                    _fields.ParseInt(_w, oldDims.x),
                    _fields.ParseInt(_h, oldDims.y),
                    _fields.ParseInt(_d, oldDims.z));
                radial.CornerRadius = _fields.ParseInt(_radius, radial.CornerRadius);
            }
            else if (drawer != null)
            {
                if (_drawerWidth != null)
                {
                    int boxW = _fields.ParseInt(_drawerWidth, drawer.BoxWidth);
                    int inset = drawer.System == DrawerSystem.Movento ? DrawerConstants.MOVENTO_WIDTH_INSET : 0;
                    drawer.InternalWidth = Mathf.Max(100, boxW + inset);
                }
            }
            else if (pillar != null)
            {
                int newTotalH = _fields.ParseInt(_h, oldDims.y);
                if (newTotalH != oldDims.y)
                {
                    int newMidH = Mathf.Clamp(
                        newTotalH - PillarElement.TopHeightMM - PillarElement.BottomHeightMM,
                        PillarElement.MidHeightMM_Min, PillarElement.MidHeightMM_Max);
                    pillar.MidHeightMM = newMidH;
                    if (_midHeight != null) _midHeight.text = newMidH.ToString();
                }
                else if (_midHeight != null)
                {
                    pillar.MidHeightMM = _fields.ParseInt(_midHeight, pillar.MidHeightMM);
                    _midHeight.text = pillar.MidHeightMM.ToString();
                }
                _h!.text = pillar.TotalHeightMM.ToString();
            }
            else
            {
                // Глубину окна диктует стена — поле Г игнорируется.
                target.DimensionsMM = new Vector3Int(
                    _fields.ParseInt(_w, oldDims.x),
                    _fields.ParseInt(_h, oldDims.y),
                    target is WindowElement || target is DoorElement ? oldDims.z : _fields.ParseInt(_d, oldDims.z));
            }

            // Вырез варочной — отдельной командой: он не часть габарита детали,
            // и складывать его в ResizeCommand нечестно по отношению к откату.
            // Считаем ПОСЛЕ размеров плиты: вырез клампится по её ширине.
            var cooktopApply = target as CooktopElement;
            if (cooktopApply != null && _cutoutW != null && _cutoutD != null)
            {
                var cutBefore = SetCooktopCutoutCommand.Snapshot(cooktopApply);
                var cutAfter = new Vector2Int(
                    _fields.ParseInt(_cutoutW, cutBefore.x),
                    _fields.ParseInt(_cutoutD, cutBefore.y));
                if (cutAfter != cutBefore)
                    CommandStack.Execute(new SetCooktopCutoutCommand(cooktopApply, cutBefore, cutAfter));
                // Показываем применённый (склампленный) вырез, а не введённый.
                _cutoutW.text = cooktopApply.CutoutWidthMM.ToString();
                _cutoutD.text = cooktopApply.CutoutDepthMM.ToString();
            }

            if (table != null && _legInset != null)
                table.LegInsetMM = _fields.ParseInt(_legInset, table.LegInsetMM);

            if (radiusTable != null && _legInset != null)
                radiusTable.LegInsetMM = _fields.ParseInt(_legInset, radiusTable.LegInsetMM);

            var windowEl = target as WindowElement;
            if (windowEl != null && _sillProtrusion != null)
                windowEl.SillProtrusionMM = _fields.ParseInt(_sillProtrusion, windowEl.SillProtrusionMM);

            var lightApp = target as LightSourceElement;
            if (lightApp != null)
            {
                if (_lightTemp != null)
                {
                    lightApp.TemperatureK = _fields.ParseInt(_lightTemp, lightApp.TemperatureK);
                    _lightTemp.text = lightApp.TemperatureK.ToString();
                }
                if (_lightPower != null)
                {
                    lightApp.PowerW = _fields.ParseInt(_lightPower, lightApp.PowerW);
                    _lightPower.text = lightApp.PowerW.ToString();
                }
                if (_lightDiffusion != null)
                {
                    lightApp.DiffusionPct = _fields.ParseInt(_lightDiffusion, lightApp.DiffusionPct);
                    _lightDiffusion.text = lightApp.DiffusionPct.ToString();
                }
                if (_lightBeam != null)
                {
                    lightApp.BeamAngleDeg = _fields.ParseInt(_lightBeam, lightApp.BeamAngleDeg);
                    _lightBeam.text = lightApp.BeamAngleDeg.ToString();
                }
                if (_lightUp != null)
                {
                    lightApp.UpLightPct = _fields.ParseInt(_lightUp, lightApp.UpLightPct);
                    _lightUp.text = lightApp.UpLightPct.ToString();
                }
                foreach (var b in LightExtraBindings())
                {
                    if (b.field == null) continue;
                    b.set(lightApp, _fields.ParseInt(b.field, b.get(lightApp)));
                    b.field.text = b.get(lightApp).ToString();  // показать применённый clamp
                }
            }

            // Сохраняем материал опор (материал столешницы применяется через дропдаун).
            if (_legsMaterialDropdown != null && _currentIsTable)
            {
                var legsIndex = _legsMaterialDropdown.value;
                var legsAll = MaterialCatalog.All;
                if (legsIndex >= 0 && legsIndex < legsAll.Count)
                {
                    var legDef = legsAll[legsIndex];
                    if (table != null)
                    {
                        table.LegsMaterialId = legDef.id;
                        MaterialManager.ApplyLegs(table, legDef);
                    }
                    else if (radiusTable != null)
                    {
                        radiusTable.LegsMaterialId = legDef.id;
                        MaterialManager.ApplyLegs(radiusTable, legDef);
                    }
                }
            }

            var facade = target as FacadeElement;
            if (target.SupportsGaps)
            {
                var gapFields = GapFields();
                for (int i = 0; i < GapSides.All.Length; i++)
                {
                    var side = GapSides.All[i];
                    target.SetGap(side, _fields.ParseInt(gapFields[i], target.GapOf(side)));
                }
            }

            // Толщина кромки — отдельной командой: она не часть геометрии
            // детали, и складывать её в ResizeCommand нечестно по отношению
            // к откату («Ctrl+Z вернул размер, а толщину — нет»).
            if (target.SupportsEdges && _edgeThickness != null)
            {
                var edgesBefore = EdgeBandingState.Of(target);
                var edgesAfter = new EdgeBandingState(edgesBefore.enabled,
                    ParseEdgeThickness(_edgeThickness, edgesBefore.thicknessMM),
                    edgesBefore.manualMask);
                if (!edgesAfter.Equals(edgesBefore))
                    CommandStack.Execute(new SetEdgeBandingCommand(target, edgesBefore, edgesAfter));
                _edgeThickness.text = EdgeBanding.FormatThickness(target.EdgeThicknessMM);
            }

            // Поля позиции — целые мм; внутренняя модель остаётся в метрах.
            target.transform.position = new Vector3(
                _fields.ParseMillimetresAsMetres(_x, oldPos.x),
                _fields.ParseMillimetresAsMetres(_y, oldPos.y),
                _fields.ParseMillimetresAsMetres(_z, oldPos.z));

            // У окна поля поворота скрыты (ориентацию диктует стена) — не трогаем.
            if (!(target is WindowElement) && !(target is DoorElement))
            {
                // У техники поля X/Z скрыты (только разворот вокруг вертикали) —
                // берём их из текущей позы, а не из невидимого поля.
                bool yawOnly = FixedSize.IsYawOnly(target);
                var euler = oldRot.eulerAngles;
                target.transform.rotation = Quaternion.Euler(
                    yawOnly ? euler.x : _fields.ParseAngle(_rx, euler.x),
                    _fields.ParseAngle(_ry, euler.y),
                    yawOnly ? euler.z : _fields.ParseAngle(_rz, euler.z));
            }

            // Окно живёт только на стене — сразу возвращаем его на стену,
            // чтобы команда в стеке хранила уже «прилипшую» позу.
            if (target is WindowElement winSnap) winSnap.SnapToWall();
            if (target is DoorElement doorSnap) doorSnap.SnapToWall();

            if (KitchenSettings.Instance.BlockOnViolation && WouldCauseViolation())
            {
                target.DimensionsMM = oldDims;
                target.transform.position = oldPos;
                target.transform.rotation = oldRot;
            }
            else
            {
                CommandStack.Execute(new ResizeCommand(target,
                    oldDims, target.DimensionsMM,
                    oldPos, target.transform.position,
                    oldRot, target.transform.rotation));

                // Прикреплённые детали едут за родителем. Ресайз им не
                // передаётся вовсе (у каждой свой габарит) — только перенос и
                // поворот. Apply идёт внутри BeginCapture, так что это тот же
                // один шаг отмены.
                var followers = AttachMove.FollowersCommand(target,
                    oldPos, oldRot, target.transform.position, target.transform.rotation);
                if (followers != null) CommandStack.Execute(followers);
            }
        }

        /// <summary>Показать в полях то, что РЕАЛЬНО применилось (значения могли
        /// склампиться). Идёт после EndCapture: тот откатывает и заново
        /// проигрывает команды, и до него состояние элемента промежуточное.</summary>
        private void RefreshAfterApply(KitchenElement target)
        {
            var pillar = target as PillarElement;
            var radial = target as RadialShelfElement;
            var table = target as TableElement;
            var radiusTable = target as RadiusTableElement;
            var facade = target as FacadeElement;

            var newDims = target.DimensionsMM;
            _w!.text = newDims.x.ToString();
            if (pillar == null) _h!.text = newDims.y.ToString();
            _d!.text = newDims.z.ToString();
            if (radial != null)
                _radius!.text = radial.CornerRadius.ToString();

            if (table != null && _legInset != null)
                _legInset.text = table.LegInsetMM.ToString();

            if (radiusTable != null && _legInset != null)
                _legInset.text = radiusTable.LegInsetMM.ToString();

            if (pillar != null && _midHeight != null)
                _midHeight.text = pillar.MidHeightMM.ToString();

            WriteGapFields(_target);

            RefreshTitle();
            RefreshEdgeUI();
            RefreshHighlights();

            _fields.ClearHighlights();
            TrackAllFields();

            _fields.ShowRejections();
        }

        private bool WouldCauseViolation()
        {
            if (_target == null) return false;
            var list = PartRegistry.GetAll();
            var result = ConstraintValidator.Validate(list);
            return result.violations.Contains(_target);
        }

        private void RotateAxis(Vector3 axis, float angle = 90f)
        {
            if (_target == null) return;
            // Кнопки X 90° / Z 90° у техники скрыты; страховка на случай вызова
            // мимо панели — «на боку» встраиваемый прибор не стоит.
            if (FixedSize.IsYawOnly(_target) && Mathf.Abs(Vector3.Dot(axis.normalized, Vector3.up)) < 0.99f)
                return;
            var oldRot = _target.transform.rotation;
            _target.RotateAroundAxis(axis, angle);
            if (_target is WindowElement win) win.SnapToWall();
            if (_target is DoorElement doorRot) doorRot.SnapToWall();
            var rotCmds = new List<IUndoCommand>
            {
                new MoveCommand(_target, _target.transform.position, _target.transform.position,
                    oldRot, _target.transform.rotation)
            };
            // Прикреплённые детали разворачиваются ВОКРУГ родителя, а не вокруг
            // своих центров: связка жёсткая, иначе поворот фасада оставил бы
            // короб стоять как стоял.
            AttachMove.AppendFollowers(rotCmds, _target, _target.transform.position,
                oldRot, _target.transform.position, _target.transform.rotation);
            CommandStack.Execute(rotCmds.Count == 1
                ? rotCmds[0]
                : new CompositeCommand("Поворот " + _target.PartName, rotCmds));
            RefreshTransformFields();
            RefreshHighlights();
        }

        // ── Открывание дверцы (только фасад) ───────────────────────────

        private void ToggleDoor()
        {
            if (_target is FacadeElement f)
            {
                var drawer = FindDrawerForFacade(f);
                if (drawer != null) CameraController.ToggleDrawerFor(drawer);
                else
                {
                    // Фасад может быть пристёгнут к посудомойке — там анимацией
                    // владеет дверца, а у самой фасадной кнопки-«Открыть»
                    // осталось только перенаправить вызов на хост.
                    var dw = FindDishwasherForFacade(f);
                    if (dw != null) dw.ToggleOpen();
                    else f.ToggleOpen();
                }
                UpdateDoorButton(f);
            }
        }

        private void OnModeSelected(int index)
        {
            if (_target is FacadeElement f)
                f.Mode = (DoorMode)index;
        }

        // ── Открывание окна ─────────────────────────────────────────

        private void ToggleWindowDoor()
        {
            if (_target is WindowElement w)
            {
                w.ToggleOpen();
                if (_winDoorButtonLabel != null)
                    _winDoorButtonLabel.text = w.IsOpen ? "Закрыть" : "Открыть";
            }
            else if (_target is DoorElement d)
            {
                d.ToggleOpen();
                if (_winDoorButtonLabel != null)
                    _winDoorButtonLabel.text = d.IsOpen ? "Закрыть" : "Открыть";
            }
        }

        private void OnTintSelected(int index)
        {
            if (_target is WindowElement w)
                w.Tint = (GlassTint)index;
        }

        private void OnSashTypeSelected(int index)
        {
            if (_target is DoorElement d)
                d.SashType = (DoorSashType)index;
        }

        private void OnWindowModeSelected(int index)
        {
            if (_target is WindowElement w)
                w.Mode = (DoorMode)index;
            else if (_target is DoorElement d)
                d.Mode = (DoorMode)index;
        }

        // Порядок пунктов списка центра: 0=Глухой, 1=Витрина(пусто), 2=Стекло.
        private static readonly AssembledFill[] FillOrder =
            { AssembledFill.Blind, AssembledFill.Open, AssembledFill.Glass };

        private static int FillToIndex(AssembledFill fill)
        {
            for (int i = 0; i < FillOrder.Length; i++)
                if (FillOrder[i] == fill) return i;
            return 0;
        }

        private void OnFillSelected(int index)
        {
            if (_target is AssembledFacadeElement a && index >= 0 && index < FillOrder.Length)
            {
                a.Fill = FillOrder[index];
                if (SelectionManager.Instance != null)
                    SelectionManager.Instance.RefreshHighlight(a);
            }
        }

        // ── Зазоры ────────────────────────────────────────────────────
        // Значения читаются и пишутся общими Open/Apply (свойства помечены
        // [Undoable], откат приезжает сам). Здесь — только раскрывашка и счётчик.

        private bool GapsEligible() => _target != null && _target.SupportsGaps;

        private bool GapsExpanded() => _gapsExpanded && GapsEligible();

        private void ToggleGaps()
        {
            _gapsExpanded = !_gapsExpanded;
            RefreshGapUI();
            RelayoutForTarget();
        }

        /// <summary>Заголовок секции: число сторон с ненулевым зазором и глиф
        /// состояния. Считаем по ПОЛЯМ, а не по детали: пока курсор в поле,
        /// введённое значение ещё не применено, а счётчик должен идти за ним.</summary>
        private void RefreshGapUI()
        {
            if (_gapCountLabel == null) return;
            int filled = 0;
            foreach (var f in GapFields())
                if (f != null && _fields.ParseInt(f, 0) != 0) filled++;
            _gapCountLabel.text =
                $"Зазоры ({filled})  {(_gapsExpanded ? UIStyle.GlyphExpanded : UIStyle.GlyphCollapsed)}";
        }

        /// <summary>Поля зазоров в порядке <see cref="GapSides.All"/>.</summary>
        private TMP_InputField?[] GapFields() => new[]
        {
            _gapLeft, _gapRight, _gapTop, _gapBottom, _gapFront, _gapBack,
        };

        /// <summary>Заполнить поля значениями детали (у детали без зазоров —
        /// нулями) и пересчитать счётчик в заголовке.</summary>
        private void WriteGapFields(KitchenElement? element)
        {
            var fields = GapFields();
            for (int i = 0; i < GapSides.All.Length; i++)
            {
                if (fields[i] == null) continue;
                fields[i]!.text = element != null && element.SupportsGaps
                    ? element.GapOf(GapSides.All[i]).ToString()
                    : "0";
            }
            RefreshGapUI();
        }

        // ── Пазы детали ───────────────────────────────────────────────
        // Все правки набора пазов идут через SetGroovesCommand: Ctrl+Z обязан
        // работать для любой мутации (правило 2 UI-GUIDELINES).

        private int GrooveCount() => _target != null ? _target.Grooves.Count : 0;

        private void ToggleGrooves()
        {
            _groovesExpanded = !_groovesExpanded;
            RefreshGrooveUI();
            RelayoutForTarget();
        }

        private void AddGrooveFromUI()
        {
            if (_target == null || _grooveSideDropdown == null || _grooveKindDropdown == null) return;
            var spec = new GrooveSpec((GrooveKind)_grooveKindDropdown.value,
                (GrooveSide)_grooveSideDropdown.value);

            var after = new List<GrooveSpec>(_target.Grooves);
            if (after.Contains(spec))
            {
                ToastNotification.ShowIfAvailable("Такой паз уже есть");
                return;
            }
            if (after.Count >= AppConstants.GROOVE_MAX_PER_PART)
            {
                ToastNotification.ShowIfAvailable($"Не больше {AppConstants.GROOVE_MAX_PER_PART} пазов на деталь");
                return;
            }
            after.Add(spec);
            ApplyGrooves(after);
            _groovesExpanded = true;
            AfterGroovesChanged();
        }

        private void RemoveGroove(int index)
        {
            if (_target == null || index < 0 || index >= _target.Grooves.Count) return;
            var after = new List<GrooveSpec>(_target.Grooves);
            after.RemoveAt(index);
            ApplyGrooves(after);
            AfterGroovesChanged();
        }

        /// <summary>Правка паза на месте: сторона/тип берутся из дропдаунов строки.</summary>
        private void EditGroove(int index)
        {
            if (_target == null || index < 0 || index >= _target.Grooves.Count) return;
            var sideDd = _grooveRowSide[index];
            var kindDd = _grooveRowKind[index];
            if (sideDd == null || kindDd == null) return;

            var spec = new GrooveSpec((GrooveKind)kindDd.value, (GrooveSide)sideDd.value);
            var after = new List<GrooveSpec>(_target.Grooves);
            if (after[index].Equals(spec)) return;
            for (int i = 0; i < after.Count; i++)
                if (i != index && after[i].Equals(spec))
                {
                    ToastNotification.ShowIfAvailable("Такой паз уже есть");
                    RefreshGrooveUI(); // вернуть дропдауны к фактическому набору
                    return;
                }
            after[index] = spec;
            ApplyGrooves(after);
            AfterGroovesChanged();
        }

        private void ApplyGrooves(List<GrooveSpec> after)
        {
            if (_target == null) return;
            CommandStack.Execute(new SetGroovesCommand(_target, _target.Grooves, after));
        }

        private void AfterGroovesChanged()
        {
            RefreshGrooveUI();
            RelayoutForTarget();
            RefreshHighlights();
        }

        /// <summary>Обновить кнопку-раскрывашку и строки пазов.</summary>
        private void RefreshGrooveUI()
        {
            // Набор под кнопками поменялся — взведённое удаление спрашивало бы
            // уже про другую строку.
            ConfirmDeleteButton.DisarmAll();
            if (_grooveCountLabel != null)
                _grooveCountLabel.text =
                    $"Пазы ({GrooveCount()})  {(_groovesExpanded ? UIStyle.GlyphExpanded : UIStyle.GlyphCollapsed)}";

            IReadOnlyList<GrooveSpec>? grooves = _target != null ? _target.Grooves : null;
            _grooveFingerprint = GrooveFingerprint(grooves);
            for (int i = 0; i < _grooveRowSide.Length; i++)
            {
                if (grooves == null || i >= grooves.Count) continue;
                _grooveRowSide[i]?.SetValueWithoutNotify((int)grooves[i].side);
                _grooveRowSide[i]?.RefreshShownValue();
                _grooveRowKind[i]?.SetValueWithoutNotify((int)grooves[i].kind);
                _grooveRowKind[i]?.RefreshShownValue();
            }
        }

        /// <summary>Дешёвый отпечаток набора пазов: ловим изменения мимо меню
        /// (undo/redo, MCP), чтобы строки не показывали устаревший набор.</summary>
        private static int GrooveFingerprint(IReadOnlyList<GrooveSpec>? grooves)
        {
            if (grooves == null) return 0;
            unchecked
            {
                int h = 17;
                foreach (var g in grooves) h = h * 31 + g.GetHashCode();
                return h;
            }
        }


        // ── Кромки детали ─────────────────────────────────────────────
        // Кромка на конкретном торце не хранится и не редактируется: она
        // вычисляется из геометрии (открытый торец → кромка). Пользователь
        // правит только выключатель, толщину ленты и отказ от валидации —
        // все три через одну команду, чтобы Ctrl+Z возвращал блок целиком.

        /// <summary>Деталь вообще может кромковаться (лист с ровно одной
        /// стороной тоньше 50 мм).</summary>
        private bool EdgesEligible() => _target != null && _target.SupportsEdges;

        /// <summary>Показывать схему и параметры кромки.</summary>
        private bool EdgesShown() => EdgesEligible() && _target!.EdgeBandingEnabled;

        /// <summary>Схема детали: пласть с четырьмя торцами-полосами и подписи
        /// длины/ширины. Размер фиксирован — под габарит детали не подгоняется.</summary>
        private RectTransform BuildEdgeDiagram(Transform parent, out float height)
        {
            const float boardW = 200f, boardH = 110f, strip = 10f;
            const float boardX = -50f, boardY = -10f;
            height = 140f;

            var root = UIFactory.CreateRect("CtxEdgeDiagram", parent);
            root.sizeDelta = new Vector2(332f, height);

            UIFactory.CreatePanel("CtxEdgeBoard", root, new Vector2(boardX, boardY),
                new Vector2(boardW, boardH), UIStyle.EdgeBoard);

            // Полосы-торцы. L1/W1 — стороны по положительному направлению осей
            // детали (верх и право), L2/W2 — по отрицательному.
            _edgeStripL1 = UIFactory.CreatePanel("CtxEdgeL1", root,
                new Vector2(boardX, boardY + (boardH - strip) * 0.5f),
                new Vector2(boardW, strip), UIStyle.EdgeAbsent);
            _edgeStripL2 = UIFactory.CreatePanel("CtxEdgeL2", root,
                new Vector2(boardX, boardY - (boardH - strip) * 0.5f),
                new Vector2(boardW, strip), UIStyle.EdgeAbsent);
            _edgeStripW1 = UIFactory.CreatePanel("CtxEdgeW1", root,
                new Vector2(boardX + (boardW - strip) * 0.5f, boardY),
                new Vector2(strip, boardH), UIStyle.EdgeAbsent);
            _edgeStripW2 = UIFactory.CreatePanel("CtxEdgeW2", root,
                new Vector2(boardX - (boardW - strip) * 0.5f, boardY),
                new Vector2(strip, boardH), UIStyle.EdgeAbsent);

            MakeEdgeStripInteractive(_edgeStripL1, EdgeSide.L1);
            MakeEdgeStripInteractive(_edgeStripL2, EdgeSide.L2);
            MakeEdgeStripInteractive(_edgeStripW1, EdgeSide.W1);
            MakeEdgeStripInteractive(_edgeStripW2, EdgeSide.W2);

            // Подписи сторон — те же имена, что и колонки CSV.
            EdgeSideLabel(root, "CtxEdgeLblL1", "L1", new Vector2(boardX, boardY + 36f));
            EdgeSideLabel(root, "CtxEdgeLblL2", "L2", new Vector2(boardX, boardY - 36f));
            EdgeSideLabel(root, "CtxEdgeLblW1", "W1", new Vector2(boardX + 76f, boardY));
            EdgeSideLabel(root, "CtxEdgeLblW2", "W2", new Vector2(boardX - 76f, boardY));

            _edgeLengthLabel = UIFactory.CreateLabel("CtxEdgeLen", root, "", 12,
                new Vector2(boardX, boardY + boardH * 0.5f + 11f), new Vector2(120, 18),
                TextAnchor.MiddleCenter);
            _edgeLengthLabel.color = UIStyle.TextSecondary;
            _edgeWidthLabel = UIFactory.CreateLabel("CtxEdgeWid", root, "", 12,
                new Vector2(boardX + boardW * 0.5f + 43f, boardY), new Vector2(70, 18),
                TextAnchor.MiddleLeft);
            _edgeWidthLabel.color = UIStyle.TextSecondary;

            return root;
        }

        /// <summary>Полоса-торец на схеме: клик переключает полуручной режим,
        /// наведение подсвечивает эту сторону на самой детали.</summary>
        private void MakeEdgeStripInteractive(Image? strip, EdgeSide side)
        {
            if (strip == null) return;
            strip.raycastTarget = true;

            var button = strip.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None; // цвет полосы задаёт состояние кромки
            button.onClick.AddListener(() => OnEdgeStripClicked(side));

            PointerHover.Attach(strip.gameObject,
                () => OnEdgeStripHover(side, true), () => OnEdgeStripHover(side, false));
        }

        private static void EdgeSideLabel(Transform parent, string name, string text, Vector2 pos)
        {
            var lbl = UIFactory.CreateLabel(name, parent, text, 11, pos, new Vector2(30, 14),
                TextAnchor.MiddleCenter);
            lbl.color = UIStyle.TextSecondary;
            lbl.raycastTarget = false;
        }

        private void OnEdgeBandingToggled(bool on)
        {
            if (_target == null || !_target.SupportsEdges) return;
            var before = EdgeBandingState.Of(_target);
            ApplyEdgeState(before, new EdgeBandingState(on, before.thicknessMM, before.manualMask));
        }

        /// <summary>Клик по полосе на схеме — полуручной режим этой стороны:
        /// кромку на ней назначает человек, автоматическая проверка «торец
        /// перекрыт частично» на неё больше не смотрит.</summary>
        private void OnEdgeStripClicked(EdgeSide side)
        {
            if (_target == null || !_target.SupportsEdges) return;
            var before = EdgeBandingState.Of(_target);
            ApplyEdgeState(before, before.WithManual(side, !_target.IsEdgeManual(side)));
        }

        /// <summary>Наведение на полосу — подсветить сторону на самой детали,
        /// чтобы с любого ракурса было видно, о какой стороне речь.</summary>
        private void OnEdgeStripHover(EdgeSide side, bool entered)
        {
            if (_target == null || !_target.SupportsEdges) return;
            if (entered) SideHighlighter.ShowEdgeSide(_target, side);
            else SideHighlighter.Hide();
        }

        private void ApplyEdgeState(EdgeBandingState before, EdgeBandingState after)
        {
            if (_target == null || after.Equals(before)) return;
            CommandStack.Execute(new SetEdgeBandingCommand(_target, before, after));
            RefreshEdgeUI();
            RelayoutForTarget();
        }

        /// <summary>Обновить схему кромок и параметры под текущую геометрию.</summary>
        private void RefreshEdgeUI()
        {
            _edgeRefreshCountdown = EdgeRefreshFrames;
            if (_target == null || !_target.SupportsEdges) return;

            _edgeToggle?.SetIsOnWithoutNotify(_target.EdgeBandingEnabled);
            _fields.RefreshUnfocused(_edgeThickness, EdgeBanding.FormatThickness(_target.EdgeThicknessMM));

            if (!_target.EdgeBandingEnabled) return;

            var layout = EdgeBanding.LayoutOf(_target.DimensionsMM);
            if (_edgeLengthLabel != null) _edgeLengthLabel.text = $"{layout.LengthMM} мм";
            if (_edgeWidthLabel != null) _edgeWidthLabel.text = $"{layout.WidthMM} мм";

            var coverage = EdgeBanding.Coverage(_target, PartRegistry.GetAll());
            PaintEdgeStrip(_edgeStripL1, coverage, EdgeSide.L1);
            PaintEdgeStrip(_edgeStripL2, coverage, EdgeSide.L2);
            PaintEdgeStrip(_edgeStripW1, coverage, EdgeSide.W1);
            PaintEdgeStrip(_edgeStripW2, coverage, EdgeSide.W2);
        }

        /// <summary>Жёлтый — сторона в полуручном режиме (перекрывает вычисленное
        /// состояние: именно на неё пользователь и переключился).</summary>
        private void PaintEdgeStrip(Image? strip, EdgeCoverage coverage, EdgeSide side)
        {
            if (strip == null || _target == null) return;
            strip.color = _target.IsEdgeManual(side) ? UIStyle.EdgeManualSide
                : coverage.HasEdge(side) ? UIStyle.EdgePresent
                : UIStyle.EdgeAbsent;
        }

        private float ParseEdgeThickness(TMP_InputField? f, float fallback) =>
            _fields.ParseDecimalInRange(f, fallback,
                AppConstants.EDGE_THICKNESS_MIN_MM, AppConstants.EDGE_THICKNESS_MAX_MM);

        private void RelayoutForTarget()
        {
            if (_target == null) return;
            ApplyLayout(FacetsOf(_target));
        }

        private void ApplyLayout(ElementFacet facets)
        {
            bool showRotationXZ = (facets & ElementFacet.Window) == ElementFacet.None
                && !FixedSize.IsYawOnly(_target);
            float contentBottom = _layout.Apply(facets, showRotationXZ, TopPad);
            if (_panelRt != null)
                _panelRt.sizeDelta = new Vector2(_panelRt.sizeDelta.x, contentBottom + BottomPad);
        }

        private static ElementFacet FacetsOf(KitchenElement element)
        {
            var facets = ElementFacet.None;
            if (element is FacadeElement) facets |= ElementFacet.Facade;
            if (element is AssembledFacadeElement) facets |= ElementFacet.Assembled;
            if (element is RadialShelfElement) facets |= ElementFacet.Radial;
            if (element is DrawerElement) facets |= ElementFacet.Drawer;
            if (element is TableElement || element is RadiusTableElement) facets |= ElementFacet.Table;
            if (element is PillarElement) facets |= ElementFacet.Pillar;
            if (element is WindowElement || element is DoorElement) facets |= ElementFacet.Window;
            if (element is DoorElement) facets |= ElementFacet.Door;
            if (element is LightSourceElement) facets |= ElementFacet.Light;
            if (element.SupportsGrooves) facets |= ElementFacet.Part;
            return facets;
        }

        // ── Текстура/декор (детали и фасады) ────────────────────────────


        private void RebuildMaterialOptions() => MaterialOptions.Fill(_materialDropdown);

        private void RebuildTabletopMaterialOptions() => MaterialOptions.Fill(_tabletopMaterialDropdown);

        private void RebuildLegsMaterialOptions() => MaterialOptions.Fill(_legsMaterialDropdown);

        /// <summary>Списки декоров у всех строк накладок и у строки добавления.</summary>

        private void OnMaterialSelected(int index)
        {
            ApplyMaterialChoice(legs: false, index: index);
        }

        private void OnLegsMaterialSelected(int index)
        {
            ApplyMaterialChoice(legs: true, index: index);
        }

        /// <summary>Настоящий выбор декора: предпросмотр сворачивается ПЕРВЫМ
        /// делом (иначе уход курсора после клика вернул бы старый декор поверх
        /// выбранного), затем декор применяется и возвращается подсветка.
        ///
        /// Через CommandStack, а не напрямую: смена декора обязана отменяться
        /// Ctrl+Z (правило 2 UI-GUIDELINES). Порядок важен — «до» читается ПОСЛЕ
        /// свёртки предпросмотра, иначе в команду попал бы показанный декор.</summary>
        private void ApplyMaterialChoice(bool legs, int index)
        {
            if (_target == null) return;
            var all = MaterialCatalog.All;
            if (index < 0 || index >= all.Count) return;

            EndMaterialPreview();
            var slot = SlotFor(legs);
            CommandStack.Execute(new SetMaterialCommand(_target, slot, all[index].id));

            if (SelectionManager.Instance != null)
                SelectionManager.Instance.RefreshHighlight(_target);
            RefreshHighlights();
        }

        /// <summary>Какой декор правит строка: у стола «Текстура» скрыта, а её
        /// список показывает столешницу (см. Layout).</summary>
        private MaterialSlot SlotFor(bool legs)
            => legs ? MaterialSlot.Legs
                : _currentIsTable ? MaterialSlot.Tabletop : MaterialSlot.Base;

        // Применение и чтение слота живут в MaterialManager: через них ходит ещё
        // и SetMaterialCommand, и пипетка.
        private static void ApplyMaterialSlot(KitchenElement target, MaterialSlot slot, MaterialDef def)
            => MaterialManager.ApplySlot(target, slot, def);

        private static string MaterialIdOf(KitchenElement target, MaterialSlot slot)
            => MaterialManager.MaterialIdOf(target, slot);

        // ── Предпросмотр базового декора наведением ───────────────────
        // То же обещание, что и у накладок: название («Дуб каселла натуральный
        // светлый») не говорит, как декор ляжет именно на эту деталь. Пока
        // курсор стоит на пункте, декор надет на объект по-настоящему; ушли с
        // пункта или закрыли список — возвращается прежний.
        //
        // Выделение на время показа СНИМАЕТСЯ: жёлтая заливка перекрашивает
        // деталь, и оценивать под ней текстуру бессмысленно. Само выделение
        // остаётся — меню свойств никуда не девается.
        //
        // Предпросмотр пишется в элемент напрямую, минуя CommandStack: это
        // показ, а не правка, и в undo-стеке ему делать нечего.

        private KitchenElement? _matPreviewTarget;
        private string? _matPreviewBefore;
        private MaterialSlot _matPreviewSlot;

        /// <summary>Идёт предпросмотр — декор на объекте сейчас «не настоящий».</summary>
        public bool MaterialPreviewActive => _matPreviewBefore != null;

        private void PreviewMaterial(bool legs, int optionIndex)
        {
            if (_target == null) return;
            var all = MaterialCatalog.All;
            if (optionIndex < 0 || optionIndex >= all.Count) return;

            BeginMaterialPreview(SlotFor(legs));
            ApplyMaterialSlot(_target, _matPreviewSlot, all[optionIndex]);
        }

        private void BeginMaterialPreview(MaterialSlot slot)
        {
            if (_matPreviewBefore != null && _matPreviewTarget == _target && _matPreviewSlot == slot)
                return; // тот же список, соседний пункт — «до» уже запомнено
            EndMaterialPreview();
            _matPreviewTarget = _target;
            _matPreviewSlot = slot;
            _matPreviewBefore = MaterialIdOf(_target!, slot);
            SelectionManager.Instance?.SuppressHighlight(_target!);
        }

        /// <summary>Вернуть декор и подсветку в состояние до предпросмотра.
        /// Зовётся с ухода курсора, при закрытии списка (DropdownHover шлёт
        /// onExit на оба) и первым делом из настоящего выбора — повторный вызов
        /// уже ничего не делает.</summary>
        private void EndMaterialPreview()
        {
            var before = _matPreviewBefore;
            var target = _matPreviewTarget;
            var slot = _matPreviewSlot;
            _matPreviewBefore = null;
            _matPreviewTarget = null;
            if (before == null || target == null) return;

            ApplyMaterialSlot(target, slot, MaterialCatalog.Get(before));
            SelectionManager.Instance?.ResumeHighlight(target);
        }

        // ── Тип: родственные группы конвертации ─────────────────────────
        private enum TypeGroup { None, Structural, Drawer }

        private enum TypeChoice { Part, Facade, AssembledFacade, RadialShelf, DrawerGtv, DrawerMovento }

        private static readonly (TypeChoice choice, string label)[] StructuralChoices =
        {
            (TypeChoice.Part, "Деталь"),
            (TypeChoice.Facade, "Фасад"),
            (TypeChoice.AssembledFacade, "Сборный фасад"),
            (TypeChoice.RadialShelf, "Радиусная полка"),
        };

        private static readonly (TypeChoice choice, string label)[] DrawerChoices =
        {
            (TypeChoice.DrawerGtv, "Ящик GTV"),
            (TypeChoice.DrawerMovento, "Ящик Movento"),
        };

        // Выбор, соответствующий индексу текущего списка (наполняется в RefreshTypeDropdown).
        private readonly List<TypeChoice> _typeChoices = new();

        /// <summary>Родственная группа элемента для конвертации типа. None → строка «Тип»
        /// скрыта (окно/дверь/стол/опора/ДВП/свет/стена/пол — своя роль, конвертации нет).</summary>
        private static TypeGroup GroupOf(KitchenElement e)
        {
            if (e == null) return TypeGroup.None;
            if (e is DrawerElement) return TypeGroup.Drawer;
            if (e is TableElement || e is RadiusTableElement || e is PillarElement
                || e is WindowElement || e is DoorElement || e is PanelElement
                || e is LightSourceElement || e is FloorElement
                || e is SinkElement || e is CooktopElement || e is OvenElement
                || e is DishwasherElement) return TypeGroup.None;
            if (e.GetComponent<Wall>() != null || e.GetComponent<BasePlate>() != null) return TypeGroup.None;
            // AssembledFacade — подкласс Facade; порядок проверок не важен, обе → структурная.
            if (e is AssembledFacadeElement || e is RadialShelfElement || e is FacadeElement)
                return TypeGroup.Structural;
            if (e.GetType() == typeof(KitchenElement)) return TypeGroup.Structural; // голая «Деталь»
            return TypeGroup.None;
        }

        private static TypeChoice CurrentChoice(KitchenElement e)
        {
            if (e is DrawerElement d)
                return d.System == DrawerSystem.Movento ? TypeChoice.DrawerMovento : TypeChoice.DrawerGtv;
            if (e is AssembledFacadeElement) return TypeChoice.AssembledFacade;
            if (e is RadialShelfElement) return TypeChoice.RadialShelf;
            if (e is FacadeElement) return TypeChoice.Facade;
            return TypeChoice.Part;
        }

        /// <summary>Наполнить список «Тип» опциями родственной группы элемента и
        /// выставить текущее значение. Для группы None список пуст (строку гасит Layout).</summary>
        private void RefreshTypeDropdown(KitchenElement element)
        {
            if (_typeDropdown == null) return;
            _typeChoices.Clear();
            _typeDropdown.ClearOptions();

            var group = GroupOf(element);
            if (group == TypeGroup.None) return;

            var set = group == TypeGroup.Drawer ? DrawerChoices : StructuralChoices;
            var labels = new List<string>(set.Length);
            foreach (var (choice, label) in set) { _typeChoices.Add(choice); labels.Add(label); }
            _typeDropdown.AddOptions(labels);

            int idx = _typeChoices.IndexOf(CurrentChoice(element));
            _typeDropdown.SetValueWithoutNotify(idx < 0 ? 0 : idx);
            _typeDropdown.RefreshShownValue();
        }

        private void OnTypeSelected(int index)
        {
            if (_target == null || index < 0 || index >= _typeChoices.Count) return;
            var choice = _typeChoices[index];

            // Ящик: смена системы выдвижения — тот же элемент, пересобираем меш и меню.
            if (choice == TypeChoice.DrawerGtv || choice == TypeChoice.DrawerMovento)
            {
                if (_target is DrawerElement drawer)
                {
                    var sys = choice == TypeChoice.DrawerMovento ? DrawerSystem.Movento : DrawerSystem.Gtv;
                    if (drawer.System != sys)
                    {
                        drawer.System = sys;
                        Open(drawer); // обновить заголовок, значение и спецификацию
                        RefreshHighlights();
                    }
                }
                return;
            }

            // Родственные структурные типы: конвертация пересоздаёт элемент.
            var targetType = choice switch
            {
                TypeChoice.Facade => ElementConverter.TargetType.Facade,
                TypeChoice.AssembledFacade => ElementConverter.TargetType.AssembledFacade,
                TypeChoice.RadialShelf => ElementConverter.TargetType.RadialShelf,
                _ => ElementConverter.TargetType.Part,
            };
            if (ElementConverter.GetElementType(_target) == targetType) return;

            var converted = ElementConverter.Convert(_target, targetType);
            if (converted != null)
                Open(converted);
            RefreshHighlights();
        }

        private void OnDrawerTypeChanged(int index)
        {
            // Значения enum DrawerType — высоты в мм; индекс дропдауна кастовать нельзя.
            if (_target is DrawerElement d)
                d.Type = DrawerConstants.TypeFromIndex(index);
        }

        private void OnDrawerLengthChanged(int index)
        {
            if (_target is DrawerElement d && index >= 0 && index < DrawerConstants.ValidLengths.Length)
                d.NominalLength = DrawerConstants.ValidLengths[index];
        }

        private void OnDrawerColorChanged(int index)
        {
            if (_target is DrawerElement d && index >= 0 && index <= 2)
                d.Color = (DrawerColor)index;
        }

        private void CycleDrawerAnimation()
        {
            if (_target is DrawerElement d)
            {
                // Цикл трёх состояний — только при живой паре; флаг IsDouble без
                // пары (битые ссылки, старые сцены) ведёт себя как одиночный.
                if (d.FindPaired() != null) d.CycleDoubleState();
                else d.ToggleOpen();
                UpdateDrawerAnimButton(d);
            }
        }

        // ── Двойной ящик ─────────────────────────────────────────────
        // Верхний внутренний ящик (тип A) жёстко привязан к нижнему: своего окна
        // свойств не имеет, из окна нижнего настраивается только его длина.

        private bool HasUpperDrawer() =>
            _target is DrawerElement d && !d.IsUpperDrawer && d.FindPaired() != null;

        private bool CanCreateDoubleDrawer()
        {
            if (!(_target is DrawerElement d) || d.IsUpperDrawer || d.FindPaired() != null)
                return false;
            float freeMM = DrawerValidator.FreeHeightAboveMM(d, PartRegistry.GetAll());
            return freeMM >= DrawerConstants.GetMinOpeningHeight(DrawerConstants.UPPER_DRAWER_TYPE);
        }

        private void CreatePairedDrawer()
        {
            if (!(_target is DrawerElement d)) return;
            var pair = DrawerLinks.CreatePair(d);
            if (pair == null) return;
            CommandStack.Execute(new CreateCommand(pair.gameObject));
            RefreshHighlights();
            Open(d); // перестроить строки пары и подпись кнопки анимации
        }

        private void RemoveUpperDrawer()
        {
            if (!(_target is DrawerElement d)) return;
            var upperGo = DrawerLinks.DetachPair(d);
            if (upperGo != null)
                CommandStack.Execute(new DeleteCommand(upperGo));
            RefreshHighlights();
            Open(d);
        }

        private void OnDrawerUpperLengthChanged(int index)
        {
            if (!(_target is DrawerElement d)) return;
            if (index < 0 || index >= DrawerConstants.ValidLengths.Length) return;
            var upper = d.FindPaired();
            if (upper != null) upper.NominalLength = DrawerConstants.ValidLengths[index];
        }

        // Затемнить/вернуть поля Ш/В/Г: у ящика они вычисляемые (только чтение).
        private Color _dimsTextColor = Color.clear;

        private void SetDimensionFieldsEditable(bool editable)
        {
            foreach (var f in new[] { _w, _h, _d })
                SetDimensionFieldEditable(f, editable);
        }

        private void SetDimensionFieldEditable(TMP_InputField? f, bool editable)
        {
            if (f == null) return;
            var disabled = new Color(0.55f, 0.55f, 0.55f, 1f);
            if (_dimsTextColor == Color.clear && f.textComponent != null)
                _dimsTextColor = f.textComponent.color;
            f.interactable = editable;
            if (f.textComponent != null)
                f.textComponent.color = editable ? _dimsTextColor : disabled;
        }

        private void ToggleOvenDoor()
        {
            if (_target is OvenElement oven)
            {
                oven.ToggleOpen();
                UpdateOvenDoorButton(oven);
            }
        }

        private void UpdateOvenDoorButton(OvenElement oven)
        {
            if (_ovenDoorLabel == null || oven == null) return;
            _ovenDoorLabel.text = oven.IsOpen ? "Закрыть дверцу" : "Открыть дверцу";
        }

        private void ToggleDishwasherDoor()
        {
            if (_target is DishwasherElement dw)
            {
                dw.ToggleOpen();
                UpdateDishwasherDoorButton(dw);
            }
        }

        private void UpdateDishwasherDoorButton(DishwasherElement dw)
        {
            if (_dishwasherDoorLabel == null || dw == null) return;
            _dishwasherDoorLabel.text = dw.IsOpen ? "Закрыть дверцу" : "Открыть дверцу";
        }

        private void UpdateDrawerAnimButton(DrawerElement d)
        {
            if (_drawerAnimLabel == null || d == null) return;
            if (d.FindPaired() != null)
                _drawerAnimLabel.text = DrawerConstants.GetCycleButtonLabel(d.DoubleState);
            else
                _drawerAnimLabel.text = d.IsOpen ? "Закрыть ящик" : "Открыть ящик";
        }

        internal void SyncOpenLabels()
        {
            if (_root == null || !_root.activeSelf || _target == null) return;
            if (_target is FacadeElement f) UpdateDoorButton(f);
            else if (_target is DrawerElement d) UpdateDrawerAnimButton(d);
            else if (_target is OvenElement o) UpdateOvenDoorButton(o);
            else if (_target is DishwasherElement dw) UpdateDishwasherDoorButton(dw);
        }

        // ── Фасад ящика ───────────────────────────────────────────────
        // Фасад — отдельный элемент: его можно выбрать из существующих, создать
        // (фронт ящика, линейное открывание) или перейти к его настройке.

        private void RebuildDrawerFacadeOptions()
        {
            if (_drawerFacadeDropdown == null) return;
            var opts = new List<TMP_Dropdown.OptionData> { new TMP_Dropdown.OptionData("(нет фасада)") };
            var host = _target as IFacadeHost;
            var attachedName = host?.AttachedFacadeName ?? "";
            var names = new HashSet<string>();
            foreach (var el in PartRegistry.GetAll())
            {
                if (!(el is FacadeElement fe) || string.IsNullOrEmpty(fe.PartName)) continue;
                bool isAttached = !string.IsNullOrEmpty(attachedName) && fe.PartName == attachedName;
                bool inContact = host != null && DrawerLinks.IsFacadeInContact(host, fe);
                if (!isAttached && !inContact) continue;
                opts.Add(new TMP_Dropdown.OptionData(fe.PartName));
                names.Add(fe.PartName);
            }
            // Если прикреплённый фасад отсутствует в реестре — добавить принудительно.
            if (!string.IsNullOrEmpty(attachedName) && !names.Contains(attachedName))
            {
                opts.Add(new TMP_Dropdown.OptionData(attachedName));
            }
            _drawerFacadeDropdown.options = opts;
        }

        private int DrawerFacadeIndex(string name)
        {
            if (string.IsNullOrEmpty(name) || _drawerFacadeDropdown == null) return 0;
            var opts = _drawerFacadeDropdown.options;
            for (int i = 1; i < opts.Count; i++)
                if (opts[i].text == name) return i;
            return 0;
        }

        private void SetDrawerFacadeValue(string name)
        {
            if (_drawerFacadeDropdown == null) return;
            _drawerFacadeDropdown.SetValueWithoutNotify(DrawerFacadeIndex(name));
            _drawerFacadeDropdown.RefreshShownValue();
            UpdateDrawerFacadeCaptionColor();
        }

        private void UpdateDrawerFacadeCaptionColor()
        {
            if (_drawerFacadeDropdown?.captionText == null) return;
            var host = _target as IFacadeHost;
            var attachedName = host?.AttachedFacadeName ?? "";
            if (string.IsNullOrEmpty(attachedName))
            {
                _drawerFacadeDropdown.captionText.color = _drawerFacadeNormalColor;
                return;
            }
            bool orphaned = IsDrawerFacadeOrphaned(attachedName, host!);
            _drawerFacadeDropdown.captionText.color = orphaned ? Color.red : _drawerFacadeNormalColor;
        }

        private static bool IsDrawerFacadeOrphaned(string facadeName, IFacadeHost host)
        {
            foreach (var el in PartRegistry.GetAll())
            {
                if (el is FacadeElement fe && fe.PartName == facadeName)
                    return !DrawerLinks.IsFacadeInContact(host, fe);
            }
            return true; // фасад не найден в реестре
        }

        private static void ColorOrphanedDrawerFacadeItem(TMP_Dropdown dd, string name, Color color)
        {
            var content = dd.template.Find("Viewport/Content");
            if (content == null) return;
            foreach (Transform child in content)
            {
                var label = child.GetComponentInChildren<TMP_Text>();
                if (label != null && label.text == name)
                    label.color = color;
            }
        }

        private void OnDrawerFacadeSelected(int index)
        {
            if (!(_target is IFacadeHost d)) return;
            var prev = d.FindAttachedFacade();
            string newName = (index <= 0 || _drawerFacadeDropdown == null) ? "" : _drawerFacadeDropdown.options[index].text;
            d.AttachedFacadeName = newName;
            // Хук смены пристёгнутого фасада: посудомойка здесь включает
            // пассажирский режим на новом фасаде и снимает со старого.
            d.OnAttachedFacadeChanged(prev, string.IsNullOrEmpty(newName) ? null : d.FindAttachedFacade());
            UpdateDrawerFacadeCaptionColor();
            SceneRevision.Bump();
        }

        // ── Прикрепление к другой детали ───────────────────────────────
        // Тот же приём, что и у фасада ящика: в списке только то, к чему
        // деталь РЕАЛЬНО прилегает (плюс текущий родитель, даже если сборка
        // разъехалась), а разъехавшаяся связь горит красным.

        private void RebuildAttachToOptions()
        {
            if (_attachToDropdown == null) return;
            var opts = new List<TMP_Dropdown.OptionData> { new TMP_Dropdown.OptionData(AttachToNoneText) };
            var target = _target;
            var attachedName = target != null ? target.AttachedToName : "";
            var names = new HashSet<string>();
            if (target != null && AttachLinks.CanBeChild(target))
            {
                foreach (var el in PartRegistry.GetAll())
                {
                    if (el == null || el == target || string.IsNullOrEmpty(el.PartName)) continue;
                    if (!AttachLinks.CanAttach(target, el)) continue;
                    bool isAttached = !string.IsNullOrEmpty(attachedName) && el.PartName == attachedName;
                    if (!isAttached && !AttachLinks.InContact(target, el)) continue;
                    opts.Add(new TMP_Dropdown.OptionData(el.PartName));
                    names.Add(el.PartName);
                }
            }
            // Родителя удалили, а имя осталось — показываем его, иначе список
            // молча сбросился бы на «(не прикреплено)» и связь пропала бы при
            // первом же выборе.
            if (!string.IsNullOrEmpty(attachedName) && !names.Contains(attachedName))
                opts.Add(new TMP_Dropdown.OptionData(attachedName));
            _attachToDropdown.options = opts;
        }

        private int AttachToIndex(string name)
        {
            if (string.IsNullOrEmpty(name) || _attachToDropdown == null) return 0;
            var opts = _attachToDropdown.options;
            for (int i = 1; i < opts.Count; i++)
                if (opts[i].text == name) return i;
            return 0;
        }

        private void SetAttachToValue(string name)
        {
            if (_attachToDropdown == null) return;
            _attachToDropdown.SetValueWithoutNotify(AttachToIndex(name));
            _attachToDropdown.RefreshShownValue();
            UpdateAttachToCaptionColor();
        }

        private void UpdateAttachToCaptionColor()
        {
            if (_attachToDropdown?.captionText == null) return;
            _attachToDropdown.captionText.color =
                AttachLinks.IsDetached(_target) ? Color.red : _attachToNormalColor;
        }

        private void OnAttachToSelected(int index)
        {
            var target = _target;
            if (target == null || !AttachLinks.CanBeChild(target) || _attachToDropdown == null) return;
            string newName = index <= 0 ? "" : _attachToDropdown.options[index].text;
            if (newName == target.AttachedToName) return;

            // Отмена: AttachedToName помечено [Undoable], но выбор в списке идёт
            // мимо «Применить» — снимок до/после снимаем здесь сами.
            var before = UndoableProperties.Capture(target);
            target.AttachedToName = newName;
            var after = UndoableProperties.Capture(target);
            var cmd = SetPropertiesCommand.TryCreate(target, before, after);
            if (cmd != null) CommandStack.Execute(cmd);

            UpdateAttachToCaptionColor();
            SceneRevision.Bump();
        }

        private void UpdateDoorButton(FacadeElement? facade)
        {
            if (_doorButtonLabel == null) return;
            if (facade == null) { _doorButtonLabel.text = "Открыть"; return; }
            var drawer = FindDrawerForFacade(facade);
            if (drawer != null)
            {
                if (drawer.FindPaired() != null)
                    _doorButtonLabel.text = DrawerConstants.GetCycleButtonLabel(drawer.DoubleState);
                else
                    _doorButtonLabel.text = drawer.IsOpen ? "Закрыть ящик" : "Открыть ящик";
            }
            else
            {
                var dw = FindDishwasherForFacade(facade);
                if (dw != null)
                {
                    _doorButtonLabel.text = dw.IsOpen ? "Закрыть дверцу" : "Открыть дверцу";
                }
                else
                {
                    _doorButtonLabel.text = facade.IsOpen ? "Закрыть" : "Открыть";
                }
            }
        }

        /// <summary>У пассажира (фасад пристёгнут к посудомойке) собственного
        /// режима открывания нет — кинематику диктует хост. Дропдаун
        /// «Открывание» в этом случае бесполезен, и его проще скрыть, чем
        /// держать серым с пояснением.</summary>
        private void UpdateModeDropdownEnabled(FacadeElement? facade)
        {
            if (_modeDropdown == null) return;
            var row = _modeDropdown.transform.parent;
            if (row == null) return;
            bool hostable = facade != null && (
                FindDrawerForFacade(facade) != null || FindDishwasherForFacade(facade) == null);
            row.gameObject.SetActive(hostable);
        }

        internal static DrawerElement? FindDrawerForFacade(FacadeElement facade)
        {
            if (string.IsNullOrEmpty(facade.PartName)) return null;
            foreach (var e in PartRegistry.GetAll())
                if (e is DrawerElement d && d.AttachedFacadeName == facade.PartName) return d;
            return null;
        }

        internal static DishwasherElement? FindDishwasherForFacade(FacadeElement facade)
        {
            if (string.IsNullOrEmpty(facade.PartName)) return null;
            foreach (var e in PartRegistry.GetAll())
                if (e is DishwasherElement dw && dw.AttachedFacadeName == facade.PartName) return dw;
            return null;
        }

        private void UpdateModeDropdown(FacadeElement? facade)
        {
            if (_modeDropdown == null) return;
            _modeDropdown.SetValueWithoutNotify(facade != null ? (int)facade.Mode : 0);
            _modeDropdown.RefreshShownValue();
        }

        private void Duplicate()
        {
            if (_target == null) return;
            var dup = ElementFactory.Duplicate(_target);
            var element = dup != null ? dup.GetComponent<KitchenElement>() : null;
            if (element != null)
            {
                CommandStack.Execute(new CreateCommand(dup!));
                Open(element);
            }
            RefreshHighlights();
        }

        private void Delete()
        {
            if (_target == null) return;
            var go = _target.gameObject;
            string deletedName = _target.PartName;

            // Верхний ящик пары жёстко привязан — удаляется вместе с нижним.
            if (_target is DrawerElement d && !d.IsUpperDrawer)
            {
                var upperGo = DrawerLinks.DetachPair(d);
                if (upperGo != null) CommandStack.Execute(new DeleteCommand(upperGo));
            }

            if (SelectionManager.Instance != null)
                SelectionManager.Instance.Deselect();
            Close();
            CommandStack.Execute(new DeleteCommand(go));
            RefreshHighlights();

            // Подтверждения нет намеренно: удаление обратимо на месте — тост
            // с «Отменить» (правило 3 UI-GUIDELINES).
            string expected = $"Delete {deletedName}";
            ToastNotification.ShowIfAvailable($"Удалено: {deletedName}", 5f, "Отменить", () =>
            {
                // Отменяем только если удаление всё ещё наверху стека — иначе
                // Ctrl+Z-семантика тоста откатила бы чужое действие.
                if (CommandStack.CanUndo && CommandStack.PeekUndoDescription() == expected)
                {
                    CommandStack.Undo();
                    RefreshHighlights();
                }
            });
        }

        private static void RefreshHighlights()
        {
            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();
        }

        private void TrackAllFields()
        {
            if (_target == null) return;
            _fields.Track(_name, _target.PartName);
            var dims = _target.DimensionsMM;
            _fields.Track(_w, dims.x.ToString());
            _fields.Track(_h, dims.y.ToString());
            _fields.Track(_d, dims.z.ToString());
            var radial = _target as RadialShelfElement;
            _fields.Track(_radius, radial != null
                ? radial.CornerRadius.ToString()
                : AppConstants.RADIAL_CORNER_RADIUS_DEFAULT.ToString());
            var gapTrackFields = GapFields();
            for (int i = 0; i < GapSides.All.Length; i++)
                _fields.Track(gapTrackFields[i], _target.SupportsGaps
                    ? _target.GapOf(GapSides.All[i]).ToString() : "0");
            var drawerEl2 = _target as DrawerElement;
            _fields.Track(_drawerWidth, drawerEl2 != null ? drawerEl2.BoxWidth.ToString() : "400");
            var windowEl2 = _target as WindowElement;
            _fields.Track(_sillProtrusion, windowEl2 != null ? windowEl2.SillProtrusionMM.ToString() : "50");
            var tableEl2 = _target as TableElement;
            var radiusTableEl2 = _target as RadiusTableElement;
            _fields.Track(_legInset, tableEl2 != null ? tableEl2.LegInsetMM.ToString() : (radiusTableEl2 != null ? radiusTableEl2.LegInsetMM.ToString() : "100"));
            var pillarEl = _target as PillarElement;
            _fields.Track(_midHeight, pillarEl != null ? pillarEl.MidHeightMM.ToString() : PillarElement.MidHeightMM_Default.ToString());
            var lightTrack = _target as LightSourceElement;
            _fields.Track(_lightTemp, lightTrack != null ? lightTrack.TemperatureK.ToString() : LightSourceElement.DEFAULT_TEMPERATURE_K.ToString());
            _fields.Track(_lightPower, lightTrack != null ? lightTrack.PowerW.ToString() : LightSourceElement.DEFAULT_POWER_W.ToString());
            _fields.Track(_lightDiffusion, lightTrack != null ? lightTrack.DiffusionPct.ToString() : LightSourceElement.DEFAULT_DIFFUSION_PCT.ToString());
            _fields.Track(_lightBeam, lightTrack != null ? lightTrack.BeamAngleDeg.ToString() : LightSourceElement.DEFAULT_BEAM_DEG.ToString());
            _fields.Track(_lightUp, lightTrack != null ? lightTrack.UpLightPct.ToString() : LightSourceElement.DEFAULT_UP_PCT.ToString());
            foreach (var b in LightExtraBindings())
                _fields.Track(b.field, lightTrack != null ? b.get(lightTrack).ToString() : b.def.ToString());
            _fields.Track(_edgeThickness, EdgeBanding.FormatThickness(_target.EdgeThicknessMM));
            var pos = _target.transform.position;
            _fields.Track(_x, ToMM(pos.x));
            _fields.Track(_y, ToMM(pos.y));
            _fields.Track(_z, ToMM(pos.z));
            var e = _target.transform.eulerAngles;
            _fields.Track(_rx, e.x.ToString("F1"));
            _fields.Track(_ry, e.y.ToString("F1"));
            _fields.Track(_rz, e.z.ToString("F1"));
        }

        private class DropdownOpenHook : MonoBehaviour
        {
            public System.Action? OnOpen;
            public System.Action? OnAfterShow;
            private void OnEnable()
            {
                OnOpen?.Invoke();
                StartCoroutine(DelayedAfterShow());
            }
            private System.Collections.IEnumerator DelayedAfterShow()
            {
                yield return null;
                OnAfterShow?.Invoke();
            }
        }
    }
}
