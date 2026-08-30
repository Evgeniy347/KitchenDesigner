using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static KitchenDesigner.Core.UI.ContextMenuMetrics;

namespace KitchenDesigner.Core.UI
{
    public class ContextMenuUI : MonoBehaviour, IContextMenuHost
    {
        public static ContextMenuUI? Instance { get; private set; }

        private GameObject? _root;
        private KitchenElement? _target;
        private TMP_Text? _titleLabel;

        private TMP_InputField? _name, _w, _h, _d, _x, _y, _z, _rx, _ry, _rz;
        private Toggle? _lockToggle;
        private Toggle? _transparentToggle;
        private RectTransform? _panelRt;
        private TMP_Text? _doorButtonLabel;
        private TMP_Text? _winDoorButtonLabel;
        private TMP_Text? _ovenDoorLabel;
        private TMP_Text? _dishwasherDoorLabel;
        private TMP_Dropdown? _modeDropdown;
        private TMP_Dropdown? _fillDropdown;
        private TMP_Dropdown? _drawerTypeDropdown, _drawerLengthDropdown, _drawerColorDropdown;
        private TMP_Dropdown? _drawerUpperLenDropdown;
        private TMP_Text? _drawerAnimLabel;

        private const string DrawerFacadeLabelText = "Фасад ящика";
        private const string HostFacadeLabelText = "Фасад";
        private const string AttachToLabelText = "Прикрепить к";
        private const string AttachToNoneText = "(не прикреплено)";
        private const string FacadeNoneText = "(нет фасада)";
        private TMP_Text? _drawerFacadeLabel;
        private NameDropdownBinder _attachedFacade = null!;
        private NameDropdownBinder _attachedTo = null!;
        private TMP_Dropdown? _tintDropdown;
        private TMP_Dropdown? _sashTypeDropdown;
        private TMP_Dropdown? _winModeDropdown;
        private bool _openInProgress;
        private bool _currentIsTable;
        private bool _currentIsDoor;
        private string _currentTypeName = "Деталь";

        private readonly ContextMenuLayout _layout = new();
        private readonly ContextMenuTextureSection _textures;
        private readonly ContextMenuFieldTracker _fields;
        private readonly ContextMenuEdgeSection _edges;
        private readonly ContextMenuGrooveSection _grooves;
        private readonly ContextMenuGapSection _gaps;
        private readonly ContextMenuMaterialSection _materials;
        private readonly ElementTypeConverter _types;
        private readonly LightFieldsEditor _lights;
        private readonly RadialFieldsEditor _radialFields;
        private readonly CooktopCutoutFieldsEditor _cooktopFields;
        private readonly DrawerBoxFieldsEditor _drawerFields;
        private readonly PillarFieldsEditor _pillarFields;
        private readonly TableLegFieldsEditor _tableFields;
        private readonly WallOpeningFieldsEditor _openingFields;
        private readonly ElementFieldsEditor[] _editors;
        private readonly DimensionFields _size = new();
        private ContextMenuRowFactory _rows = null!;

        public ContextMenuUI()
        {
            _textures = new ContextMenuTextureSection(this);
            _fields = new ContextMenuFieldTracker(Apply);
            _edges = new ContextMenuEdgeSection(this);
            _grooves = new ContextMenuGrooveSection(this);
            _gaps = new ContextMenuGapSection(this);
            _materials = new ContextMenuMaterialSection(this);
            _types = new ElementTypeConverter(this, Open);
            _lights = new LightFieldsEditor(this);
            _radialFields = new RadialFieldsEditor(this);
            _cooktopFields = new CooktopCutoutFieldsEditor(this);
            _drawerFields = new DrawerBoxFieldsEditor(this);
            _pillarFields = new PillarFieldsEditor(this);
            _tableFields = new TableLegFieldsEditor(this);
            _openingFields = new WallOpeningFieldsEditor(this);
            _editors = new ElementFieldsEditor[]
            {
                _radialFields, _cooktopFields, _drawerFields, _pillarFields,
                _tableFields, _openingFields, _lights,
            };
        }

        KitchenElement? IContextMenuHost.Target => _target;

        ContextMenuLayout IContextMenuHost.Layout => _layout;

        ContextMenuRowFactory IContextMenuHost.Rows => _rows;

        ContextMenuFieldTracker IContextMenuHost.Fields => _fields;

        void IContextMenuHost.Relayout() => RelayoutForTarget();

        public bool TexturePreviewActive => _textures.PreviewActive;

        public bool MaterialPreviewActive => _materials.PreviewActive;

        bool IContextMenuHost.TargetIsTable => _currentIsTable;

        bool IContextMenuHost.TargetIsDoor => _currentIsDoor;

        DimensionFields IContextMenuHost.SizeFields => _size;

        public void ToggleTextures() => _textures.Toggle();

        internal ContextMenuTextureSection Textures => _textures;

        internal ContextMenuGrooveSection Grooves => _grooves;

        internal ContextMenuGapSection Gaps => _gaps;

        internal ContextMenuMaterialSection Materials => _materials;

        internal NameDropdownBinder AttachedFacade => _attachedFacade;

        internal NameDropdownBinder AttachedTo => _attachedTo;

        internal ElementTypeConverter Types => _types;

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
            _grooves.Build(panel.transform);
            _edges.Build(panel.transform);
            _gaps.Build(panel.transform);
            BuildFacadeSection();
            BuildDrawerSection();
            BuildAttachmentSection();
            BuildWindowSection();
            BuildFurnitureSection();
            _lights.Build();
            BuildPositionSection(panel.transform);
            _textures.Build(panel.transform, _materials.Build());
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

            _types.Build();
        }

        private void BuildDimensions()
        {
            _name = _rows.NameField();
            _rows.SectionHeader("CtxSecDims", "Размеры");
            _w = _size.Width = _rows.NumberField("Ширина", RowVisibility.Always);
            _h = _size.Height = _rows.NumberField("Высота", RowVisibility.Always);
            _d = _size.Depth = _rows.NumberField("Глубина", RowVisibility.Always);
            _radialFields.Build();
            _cooktopFields.Build();
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

            _drawerFields.Build();

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
            (_drawerFacadeLabel, var facadeDropdown) = _rows.NamedDropdown("CtxDrawerFacade",
                DrawerFacadeLabelText, new List<string> { FacadeNoneText }, _ => { },
                RowVisibility.When(() => _target is IFacadeHost));
            _attachedFacade = new NameDropdownBinder(facadeDropdown, FacadeNoneText,
                () => (_target as IFacadeHost)?.AttachedFacadeName ?? "",
                AttachableFacadeNames, AttachedFacadeIsDetached, CommitAttachedFacade);

            (_, var attachToDropdown) = _rows.NamedDropdown("CtxAttachTo", AttachToLabelText,
                new List<string> { AttachToNoneText }, _ => { },
                RowVisibility.When(() => AttachLinks.CanBeChild(_target)));
            _attachedTo = new NameDropdownBinder(attachToDropdown, AttachToNoneText,
                () => _target != null ? _target.AttachedToName : "",
                AttachToCandidateNames, () => AttachLinks.IsDetached(_target), CommitAttachedTo);
        }

        private IEnumerable<string> AttachableFacadeNames()
        {
            var host = _target as IFacadeHost;
            if (host == null) yield break;
            var attachedName = host.AttachedFacadeName;
            foreach (var el in PartRegistry.GetAll())
            {
                if (!(el is FacadeElement facade) || string.IsNullOrEmpty(facade.PartName)) continue;
                bool isAttached = !string.IsNullOrEmpty(attachedName) && facade.PartName == attachedName;
                if (!isAttached && !DrawerLinks.IsFacadeInContact(host, facade)) continue;
                yield return facade.PartName;
            }
        }

        private bool AttachedFacadeIsDetached()
        {
            var host = _target as IFacadeHost;
            if (host == null) return false;
            var attachedName = host.AttachedFacadeName;
            if (string.IsNullOrEmpty(attachedName)) return false;
            foreach (var el in PartRegistry.GetAll())
                if (el is FacadeElement facade && facade.PartName == attachedName)
                    return !DrawerLinks.IsFacadeInContact(host, facade);
            return true;
        }

        private void CommitAttachedFacade(string name)
        {
            if (!(_target is IFacadeHost host)) return;
            var previous = host.FindAttachedFacade();
            host.AttachedFacadeName = name;
            host.OnAttachedFacadeChanged(previous,
                string.IsNullOrEmpty(name) ? null : host.FindAttachedFacade());
        }

        private IEnumerable<string> AttachToCandidateNames()
        {
            var target = _target;
            if (target == null || !AttachLinks.CanBeChild(target)) yield break;
            var attachedName = target.AttachedToName;
            foreach (var el in PartRegistry.GetAll())
            {
                if (el == null || el == target || string.IsNullOrEmpty(el.PartName)) continue;
                if (!AttachLinks.CanAttach(target, el)) continue;
                bool isAttached = !string.IsNullOrEmpty(attachedName) && el.PartName == attachedName;
                if (!isAttached && !AttachLinks.InContact(target, el)) continue;
                yield return el.PartName;
            }
        }

        private void CommitAttachedTo(string name)
        {
            var target = _target;
            if (target == null || !AttachLinks.CanBeChild(target)) return;
            if (name == target.AttachedToName) return;

            var before = UndoableProperties.Capture(target);
            target.AttachedToName = name;
            var after = UndoableProperties.Capture(target);
            var command = SetPropertiesCommand.TryCreate(target, before, after);
            if (command != null) CommandStack.Execute(command);
        }

        private void BuildWindowSection()
        {
            var windowOnly = RowVisibility.For(ElementFacet.Window);
            var notDoor = RowVisibility.For(ElementFacet.Window, () => !_currentIsDoor);

            _tintDropdown = _rows.Dropdown("Стекло", new List<string> { "Прозрачное", "Тонированное" },
                OnTintSelected, notDoor, "CtxTint");
            _openingFields.Build();
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
            _tableFields.Build();
            _pillarFields.Build();
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
                _w, _h, _d, _x, _y, _z,
            };
            foreach (var editor in _editors) fields.AddRange(editor.ArithmeticFields());
            return fields.ToArray();
        }

        private void OnDestroy()
        {
            if (SelectionManager.Instance != null)
                SelectionManager.Instance.OnSelectionChanged -= OnSelectionChanged;
        }

        private int _deferCloseFrame = -1;

        internal void OnSelectionChanged(KitchenElement? element)
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

        internal void ProcessDeferredClose()
        {
            if (_deferCloseFrame >= 0 && _deferCloseFrame < Time.frameCount)
            {
                _deferCloseFrame = -1;
                Close();
            }
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

                if (_grooves.ChangedOutsideTheMenu())
                {
                    _grooves.Refresh();
                    RelayoutForTarget();
                }

                _edges.Tick();

                if (!_textures.PreviewActive && _textures.ChangedOutsideTheMenu())
                {
                    _textures.Refresh();
                    RelayoutForTarget();
                }
            }

            SideHighlighter.Sync();
        }

        private bool IsAnyFieldFocused()
        {
            foreach (var f in ArithmeticIntFields())
                if (f != null && f.isFocused) return true;
            foreach (var f in new[] { _name, _edges.ThicknessField, _rx, _ry, _rz })
                if (f != null && f.isFocused) return true;
            if (_gaps.AnyFieldFocused()) return true;
            return false;
        }

        internal void RefreshTransformFields()
        {
            if (_target == null) return;
            if (_target is FacadeElement f && !f.IsDoorClosed) return;
            if (_target is WindowElement w && !w.IsDoorClosed) return;
            if (_target is DoorElement d && !d.IsDoorClosed) return;

            var pos = _target.transform.position;
            _fields.RefreshUnfocused(_x, ToMM(pos.x));
            _fields.RefreshUnfocused(_y, ToMM(pos.y));
            _fields.RefreshUnfocused(_z, ToMM(pos.z));

            if (AttachLinks.CanBeChild(_target)) _attachedTo.UpdateCaptionColor();

            var eu = _target.transform.eulerAngles;
            _fields.RefreshUnfocused(_rx, eu.x.ToString("F1"));
            _fields.RefreshUnfocused(_ry, eu.y.ToString("F1"));
            _fields.RefreshUnfocused(_rz, eu.z.ToString("F1"));
            var dims = _target.DimensionsMM;
            _fields.RefreshUnfocused(_w, dims.x.ToString());
            _fields.RefreshUnfocused(_h, dims.y.ToString());
            _fields.RefreshUnfocused(_d, dims.z.ToString());
            _fields.RefreshUnfocused(_name, _target.PartName);
            RefreshTitle();

            _gaps.RefreshFromTarget();
            foreach (var editor in _editors) editor.Refresh(_target);
        }

        private static string ToMM(float meters) =>
            Mathf.RoundToInt(meters / AppConstants.MM_TO_UNITS).ToString();

        private void RefreshTitle()
        {
            if (_titleLabel == null || _target == null) return;
            _titleLabel.text = $"{_currentTypeName} — {_target.PartName}";
        }

        public void Open(KitchenElement element)
        {
            if (element == null) return;

            SideHighlighter.Hide();

            if (element is DrawerElement upper && upper.IsUpperDrawer)
            {
                var lower = upper.FindPaired();
                if (lower != null) element = lower;
            }

            _openInProgress = true;
            try
            {
                _textures.EndPreview();
                _materials.EndPreview();
                _target = element;
                _grooves.Collapse();
                _textures.Collapse();
                _gaps.Collapse();
                _lights.Collapse();
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
                _currentTypeName = element is CooktopElement fixedCooktop && fixedCooktop.HasFixedSize
                        ? fixedCooktop.Model
                        : element is CooktopElement ? "Варочная"
                    : element is OvenElement ? OvenElement.MODEL
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
                _types.ShowFor(element);

                var dims = element.DimensionsMM;
                _name!.text = element.PartName;
                _w!.text = dims.x.ToString();
                _h!.text = dims.y.ToString();
                _d!.text = dims.z.ToString();
                foreach (var editor in _editors) editor.Show(element);

                var facade = element as FacadeElement;
                _gaps.WriteFrom(element);
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
                    var upperDrawer = drawer.FindPaired();
                    if (_drawerUpperLenDropdown != null && upperDrawer != null)
                        _drawerUpperLenDropdown.SetValueWithoutNotify(
                            System.Array.IndexOf(DrawerConstants.ValidLengths, upperDrawer.NominalLength));
                    UpdateDrawerAnimButton(drawer);
                }

                if (element is OvenElement ovenEl) UpdateOvenDoorButton(ovenEl);
                if (element is DishwasherElement dwEl) UpdateDishwasherDoorButton(dwEl);

                if (AttachLinks.CanBeChild(element))
                {
                    _attachedTo.Rebuild();
                    _attachedTo.SetValue(element.AttachedToName);
                }

                var facadeHost = element as IFacadeHost;
                if (facadeHost != null)
                {
                    if (_drawerFacadeLabel != null)
                        _drawerFacadeLabel.text = isDrawer ? DrawerFacadeLabelText : HostFacadeLabelText;
                    _attachedFacade.Rebuild();
                    _attachedFacade.SetValue(facadeHost.AttachedFacadeName);
                }

                var window = element as WindowElement;
                if (window != null)
                {
                    if (_tintDropdown != null)
                        _tintDropdown.SetValueWithoutNotify((int)window.Tint);
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

                ShowDimensionLocks(element);

                _materials.ShowFor(element);

            _grooves.Refresh();
            _edges.Refresh();
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
            SideHighlighter.Hide();
            _textures.EndPreview();
            _materials.EndPreview();
            TextureOverlayHandles.End();
            _target = null;
            if (_root != null) _root.SetActive(false);
        }

        private void Apply()
        {
            if (_target == null) return;
            var target = _target;
            var propsBefore = UndoableProperties.Capture(target);

            CommandStack.BeginCapture();
            try
            {
                ApplyFields(target);
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
            if (target is FacadeElement fac) { fac.ForceClose(); UpdateDoorButton(fac); }
            if (target is DrawerElement dr) { dr.ForceClose(); UpdateDrawerAnimButton(dr); }
            if (target is WindowElement win) { win.ForceClose(); if (_winDoorButtonLabel != null) _winDoorButtonLabel.text = "Открыть"; }
            if (target is DoorElement doorElApp) { doorElApp.ForceClose(); if (_winDoorButtonLabel != null) _winDoorButtonLabel.text = "Открыть"; }
            AttachLinks.ForceRest(target);

            var oldDims = target.DimensionsMM;
            var oldPos = target.transform.position;
            var oldRot = target.transform.rotation;

            DrawerLinks.Rename(target, string.IsNullOrWhiteSpace(_name!.text) ? "Board" : _name!.text);
            target.gameObject.name = target.PartName;

            ApplyDimensionFields(target, oldDims);
            foreach (var editor in _editors) editor.Apply(target);

            _materials.ApplyLegsChoice(target);

            var facade = target as FacadeElement;
            _gaps.ApplyTo(target);

            _edges.ApplyThickness(target);

            target.transform.position = new Vector3(
                _fields.ParseMillimetresAsMetres(_x, oldPos.x),
                _fields.ParseMillimetresAsMetres(_y, oldPos.y),
                _fields.ParseMillimetresAsMetres(_z, oldPos.z));

            if (!(target is WindowElement) && !(target is DoorElement))
            {
                bool yawOnly = FixedSize.IsYawOnly(target);
                var euler = oldRot.eulerAngles;
                target.transform.rotation = Quaternion.Euler(
                    yawOnly ? euler.x : _fields.ParseAngle(_rx, euler.x),
                    _fields.ParseAngle(_ry, euler.y),
                    yawOnly ? euler.z : _fields.ParseAngle(_rz, euler.z));
            }

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

                var followers = AttachMove.FollowersCommand(target,
                    oldPos, oldRot, target.transform.position, target.transform.rotation);
                if (followers != null) CommandStack.Execute(followers);
            }
        }

        private void RefreshAfterApply(KitchenElement target)
        {
            var newDims = target.DimensionsMM;
            _w!.text = newDims.x.ToString();
            if (EditorFor(target)?.HeightShownFromDimensions ?? true)
                _h!.text = newDims.y.ToString();
            _d!.text = newDims.z.ToString();
            foreach (var editor in _editors) editor.AfterApply(target);

            _gaps.WriteFrom(_target);

            RefreshTitle();
            _edges.Refresh();
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
            AttachMove.AppendFollowers(rotCmds, _target, _target.transform.position,
                oldRot, _target.transform.position, _target.transform.rotation);
            CommandStack.Execute(rotCmds.Count == 1
                ? rotCmds[0]
                : new CompositeCommand("Поворот " + _target.PartName, rotCmds));
            RefreshTransformFields();
            RefreshHighlights();
        }

        private void ToggleDoor()
        {
            if (_target is FacadeElement f)
            {
                var drawer = FindDrawerForFacade(f);
                if (drawer != null) CameraController.ToggleDrawerFor(drawer);
                else
                {
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

        private void OnDrawerTypeChanged(int index)
        {
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
                if (d.FindPaired() != null) d.CycleDoubleState();
                else d.ToggleOpen();
                UpdateDrawerAnimButton(d);
            }
        }

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
            Open(d);
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

        private ElementFieldsEditor? EditorFor(KitchenElement element)
        {
            foreach (var editor in _editors)
                if (editor.Handles(element)) return editor;
            return null;
        }

        private void ApplyDimensionFields(KitchenElement target, Vector3Int oldDims)
        {
            var policy = EditorFor(target)?.Dimensions ?? DimensionPolicy.FromFields;
            if (policy == DimensionPolicy.Computed) return;

            target.DimensionsMM = new Vector3Int(
                _fields.ParseInt(_w, oldDims.x),
                _fields.ParseInt(_h, oldDims.y),
                policy == DimensionPolicy.KeepDepth ? oldDims.z : _fields.ParseInt(_d, oldDims.z));
        }

        private void ShowDimensionLocks(KitchenElement element)
        {
            var editor = EditorFor(element);
            bool unlocked = !FixedSize.IsFixed(element);
            _size.SetEditable(_w, unlocked && (editor?.WidthEditable ?? true));
            _size.SetEditable(_h, unlocked && (editor?.HeightEditable ?? true));
            _size.SetEditable(_d, unlocked && (editor?.DepthEditable ?? true));
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

            string expected = $"Delete {deletedName}";
            ToastNotification.ShowIfAvailable($"Удалено: {deletedName}", 5f, "Отменить", () =>
            {
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
            _gaps.Track();
            foreach (var editor in _editors) editor.Track(_target);
            _edges.Track();
            var pos = _target.transform.position;
            _fields.Track(_x, ToMM(pos.x));
            _fields.Track(_y, ToMM(pos.y));
            _fields.Track(_z, ToMM(pos.z));
            var e = _target.transform.eulerAngles;
            _fields.Track(_rx, e.x.ToString("F1"));
            _fields.Track(_ry, e.y.ToString("F1"));
            _fields.Track(_rz, e.z.ToString("F1"));
        }
    }
}
