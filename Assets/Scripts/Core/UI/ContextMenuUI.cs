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

        private const string DrawerFacadeLabelText = "Фасад ящика";
        private const string HostFacadeLabelText = "Фасад";
        private const string AttachToLabelText = "Прикрепить к";
        private const string AttachToNoneText = "(не прикреплено)";
        private const string FacadeNoneText = "(нет фасада)";
        private TMP_Text? _drawerFacadeLabel;
        private NameDropdownBinder _attachedFacade = null!;
        private NameDropdownBinder _attachedTo = null!;
        private bool _openInProgress;
        private ElementFacet _facets;
        private readonly RotationDisplayState _rotationDisplay = new RotationDisplayState();

        private readonly ContextMenuLayout _layout = new();
        private readonly ContextMenuTextureSection _textures;
        private readonly ContextMenuLightLinkSection _lightLinks;
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
        private readonly ScrewLegFieldsEditor _screwLegFields;
        private readonly TableLegFieldsEditor _tableFields;
        private readonly StoolFieldsEditor _stoolFields;
        private readonly ChairFieldsEditor _chairFields;
        private readonly SofaFieldsEditor _sofaFields;
        private readonly BedFieldsEditor _bedFields;
        private readonly PouffeFieldsEditor _pouffeFields;
        private readonly ToiletFieldsEditor _toiletFields;
        private readonly BathtubFieldsEditor _bathtubFields;
        private readonly BathMixerFieldsEditor _bathMixerFields;
        private readonly ShowerColumnFieldsEditor _showerColumnFields;
        private readonly WallDeviceFieldsEditor _wallDeviceFields;
        private readonly WallOpeningFieldsEditor _openingFields;
        private readonly FacadeFieldsEditor _facadeFields;
        private readonly AssembledFacadeFieldsEditor _assembledFields;
        private readonly ElementFieldsEditor[] _editors;
        private readonly DimensionFields _size = new();
        private ContextMenuRowFactory _rows = null!;
        private readonly List<OpenButtonBinder> _openButtons = new();

        public ContextMenuUI()
        {
            _textures = new ContextMenuTextureSection(this);
            _lightLinks = new ContextMenuLightLinkSection(this);
            _fields = new ContextMenuFieldTracker(Apply);
            _edges = new ContextMenuEdgeSection(this);
            _grooves = new ContextMenuGrooveSection(this);
            _gaps = new ContextMenuGapSection(this);
            _materials = new ContextMenuMaterialSection(this);
            _types = new ElementTypeConverter(this, Open);
            _lights = new LightFieldsEditor(this);
            _radialFields = new RadialFieldsEditor(this);
            _cooktopFields = new CooktopCutoutFieldsEditor(this);
            _drawerFields = new DrawerBoxFieldsEditor(this, Open);
            _pillarFields = new PillarFieldsEditor(this);
            _screwLegFields = new ScrewLegFieldsEditor(this);
            _tableFields = new TableLegFieldsEditor(this);
            _stoolFields = new StoolFieldsEditor(this);
            _chairFields = new ChairFieldsEditor(this);
            _sofaFields = new SofaFieldsEditor(this);
            _bedFields = new BedFieldsEditor(this);
            _pouffeFields = new PouffeFieldsEditor(this);
            _toiletFields = new ToiletFieldsEditor(this);
            _bathtubFields = new BathtubFieldsEditor(this);
            _bathMixerFields = new BathMixerFieldsEditor(this);
            _showerColumnFields = new ShowerColumnFieldsEditor(this);
            _wallDeviceFields = new WallDeviceFieldsEditor(this);
            _openingFields = new WallOpeningFieldsEditor(this);
            _facadeFields = new FacadeFieldsEditor(this);
            _assembledFields = new AssembledFacadeFieldsEditor(this);
            _editors = new ElementFieldsEditor[]
            {
                _radialFields, _cooktopFields, _drawerFields, _pillarFields, _screwLegFields,
                _tableFields, _stoolFields, _chairFields, _sofaFields, _bedFields,
                _pouffeFields, _toiletFields, _bathtubFields, _bathMixerFields,
                _showerColumnFields, _wallDeviceFields, _openingFields, _lights,
                _assembledFields, _facadeFields,
            };
        }

        KitchenElement? IContextMenuHost.Target => _target;

        ContextMenuLayout IContextMenuHost.Layout => _layout;

        ContextMenuRowFactory IContextMenuHost.Rows => _rows;

        ContextMenuFieldTracker IContextMenuHost.Fields => _fields;

        void IContextMenuHost.Relayout() => RelayoutForTarget();

        public bool MaterialPreviewActive => _materials.PreviewActive;

        ElementFacet IContextMenuHost.TargetFacets => _facets;

        DimensionFields IContextMenuHost.SizeFields => _size;

        public void ToggleTextures() => _textures.Toggle();

        internal ContextMenuTextureSection Textures => _textures;

        internal ContextMenuRowFactory RowFactory => _rows;

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
            BuildWindowSection();
            BuildFurnitureSection();
            BuildAttachmentSection();
            _lights.Build();
            BuildPositionSection(panel.transform);
            _textures.Build(panel.transform, _materials.Build());
            _lightLinks.Build(panel.transform);
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
            _w = _size.Width = _rows.NumberField("Ширина",
                RowVisibility.ForExcept(ElementFacet.None, ElementFacet.Pillar));
            _h = _size.Height = _rows.NumberField("Высота", RowVisibility.Always);
            _d = _size.Depth = _rows.NumberField("Глубина",
                RowVisibility.ForExcept(ElementFacet.None, ElementFacet.Pillar));
            _radialFields.Build();
            _cooktopFields.Build();
        }

        private void BuildFacadeSection()
        {
            _facadeFields.Build();

            OpenButton("CtxDoor", OpenLabels.Open, RowVisibility.For(ElementFacet.Facade));
            OpenButton("CtxOvenDoor", OpenLabels.OpenDoor, RowVisibility.For(ElementFacet.Oven));
            OpenButton("CtxDishwasherDoor", OpenLabels.OpenDoor,
                RowVisibility.For(ElementFacet.Dishwasher));

            _assembledFields.Build();
        }

        private void BuildDrawerSection()
        {
            _drawerFields.Build();
            OpenButton("CtxDrawerAnim", OpenLabels.Open, RowVisibility.For(ElementFacet.Drawer));
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
                RowVisibility.When(() => AttachLinks.CanChooseParent(_target)));
            _attachedTo = new NameDropdownBinder(attachToDropdown, AttachToNoneText,
                () => _target != null ? _target.AttachedToName : "",
                AttachToCandidateNames, () => AttachLinks.IsDetached(_target), CommitAttachedTo);
        }

        private IEnumerable<string> AttachableFacadeNames()
        {
            var host = _target as IFacadeHost;
            if (host == null) yield break;
            var attachedName = host.AttachedFacadeName;
            foreach (var facade in FacadeLinks.All())
            {
                if (string.IsNullOrEmpty(facade.PartName)) continue;
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
            var attached = FacadeLinks.FindByName(attachedName);
            if (attached != null) return !DrawerLinks.IsFacadeInContact(host, attached);
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
            if (target == null || !AttachLinks.CanChooseParent(target)) yield break;
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
            if (target == null || !AttachLinks.CanChooseParent(target)) return;
            if (name == target.AttachedToName) return;

            var before = UndoableProperties.Capture(target);
            target.AttachedToName = name;
            var after = UndoableProperties.Capture(target);
            var command = SetPropertiesCommand.TryCreate(target, before, after);
            if (command != null) CommandStack.Execute(command);
        }

        private void BuildWindowSection()
        {
            _openingFields.Build();
            OpenButton("CtxWinDoor", OpenLabels.Open, RowVisibility.For(ElementFacet.Window));
        }

        private void BuildFurnitureSection()
        {
            _tableFields.Build();
            _stoolFields.Build();
            _chairFields.Build();
            _sofaFields.Build();
            _bedFields.Build();
            _pouffeFields.Build();
            _toiletFields.Build();
            _bathtubFields.Build();
            _bathMixerFields.Build();
            _showerColumnFields.Build();
            _wallDeviceFields.Build();
            _pillarFields.Build();
            _screwLegFields.Build();
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
                new Vector2(-112, 0), new Vector2(112, BtnH), () => RotateAxis(RotationAxis.X));
            var rotY = UIFactory.CreateButton("CtxRotY", parent, "Y 90°",
                new Vector2(0, 0), new Vector2(112, BtnH), () => RotateAxis(RotationAxis.Y));
            var rotZ = UIFactory.CreateButton("CtxRotZ", parent, "Z 90°",
                new Vector2(112, 0), new Vector2(112, BtnH), () => RotateAxis(RotationAxis.Z));
            _layout.AddExcept(ElementFacet.Window, BtnH, ActionGap,
                rotX.GetComponent<RectTransform>(),
                rotY.GetComponent<RectTransform>(),
                rotZ.GetComponent<RectTransform>());
            _layout.AddRotationXZ(rotX.GetComponent<RectTransform>());
            _layout.AddRotationXZ(rotZ.GetComponent<RectTransform>());

            var rotY180 = UIFactory.CreateButton("CtxRotY180", parent, "Y 180°",
                new Vector2(0, 0), new Vector2(RowWidth, BtnH), () => RotateAxis(RotationAxis.Y, 180f));
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
                f.onValidateInput = DimensionFieldValidation.Char();
            }
            foreach (var f in new[] { _rx, _ry, _rz })
            {
                if (f == null) continue;
                f.contentType = TMP_InputField.ContentType.Custom;
                f.onValidateInput = DimensionFieldValidation.Char(allowDecimal: true);
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

                if (_lightLinks.ChangedOutsideTheMenu())
                {
                    _lightLinks.Refresh();
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
            if (_target is IOpenable openable && !openable.IsClosedPose) return;

            var pos = _target.transform.position;
            _fields.RefreshUnfocused(_x, ToMM(pos.x));
            _fields.RefreshUnfocused(_y, ToMM(pos.y));
            _fields.RefreshUnfocused(_z, ToMM(pos.z));

            if (AttachLinks.CanBeChild(_target)) _attachedTo.UpdateCaptionColor();

            var eu = _rotationDisplay.For(_target.transform.rotation);
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
            _titleLabel.text = $"{_target.DisplayTypeName} — {_target.PartName}";
        }

        public void Open(KitchenElement element)
        {
            if (element == null) return;

            SideHighlighter.Hide();

            element = element.InspectedElement;

            _openInProgress = true;
            try
            {
                _textures.EndPreview();
                _materials.EndPreview();
                _target = element;
                _rotationDisplay.Forget();
                _grooves.Collapse();
                _textures.Collapse();
                _lightLinks.Collapse();
                _gaps.Collapse();
                _lights.Collapse();
                TextureOverlayHandles.End();
                if (SelectionManager.Instance != null)
                    SelectionManager.Instance.Select(element);

                _facets = ElementFacets.Of(element);
                RefreshTitle();
                _types.ShowFor(element);

                var dims = element.DimensionsMM;
                _name!.text = element.PartName;
                _w!.text = dims.x.ToString();
                _h!.text = dims.y.ToString();
                _d!.text = dims.z.ToString();
                foreach (var editor in _editors) editor.Show(element);

                _gaps.WriteFrom(element);
                RefreshOpenButtons();

                if (AttachLinks.CanBeChild(element))
                {
                    _attachedTo.Rebuild();
                    _attachedTo.SetValue(element.AttachedToName);
                }

                var facadeHost = element as IFacadeHost;
                if (facadeHost != null)
                {
                    if (_drawerFacadeLabel != null)
                        _drawerFacadeLabel.text = _facets.Has(ElementFacet.Drawer)
                            ? DrawerFacadeLabelText : HostFacadeLabelText;
                    _attachedFacade.Rebuild();
                    _attachedFacade.SetValue(facadeHost.AttachedFacadeName);
                }

                ShowDimensionLocks(element);

                _materials.ShowFor(element);

            _grooves.Refresh();
            _edges.Refresh();
            if (element.SupportsTextureOverlays) _textures.RebuildMaterialOptions();
            _textures.Refresh();
            _lightLinks.Refresh();
            RelayoutForTarget();

                RefreshTransformFields();
                _transparentToggle!.SetIsOnWithoutNotify(element.Transparent);
                _lockToggle!.SetIsOnWithoutNotify(!element.Movable);

                _fields.ClearHighlights();
                TrackAllFields();

                _rows.SyncEnabledState();

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
            if (target is IOpenable openable) openable.ForceClose();
            RefreshOpenButtons();
            AttachLinks.ForceRest(target);

            var oldDims = target.DimensionsMM;
            var oldPos = target.transform.position;
            var oldRot = target.transform.rotation;
            var shownRotation = _rotationDisplay.For(oldRot);

            DrawerLinks.Rename(target, string.IsNullOrWhiteSpace(_name!.text) ? "Board" : _name!.text);
            target.gameObject.name = target.PartName;

            ApplyDimensionFields(target, oldDims);
            foreach (var editor in _editors) editor.Apply(target);

            _materials.ApplySecondarySlotChoice(target);

            _gaps.ApplyTo(target);

            _edges.ApplyThickness(target);

            target.transform.position = new Vector3(
                _fields.ParseMillimetresAsMetres(_x, oldPos.x),
                _fields.ParseMillimetresAsMetres(_y, oldPos.y),
                _fields.ParseMillimetresAsMetres(_z, oldPos.z));

            if (!(target is IWallMounted))
            {
                bool yawOnly = FixedSize.IsYawOnly(target);
                var typed = RotationSteps.Normalize(new Vector3(
                    yawOnly ? shownRotation.x : _fields.ParseAngle(_rx, shownRotation.x),
                    _fields.ParseAngle(_ry, shownRotation.y),
                    yawOnly ? shownRotation.z : _fields.ParseAngle(_rz, shownRotation.z)));
                target.transform.rotation = Quaternion.Euler(typed);
                _rotationDisplay.Remember(typed);
            }

            if (target is IWallMounted wallMounted) wallMounted.SnapToWall();

            foreach (var editor in _editors) editor.ApplyAfterPosition(target);

            if (KitchenSettings.Instance.BlockOnViolation && WouldCauseViolation())
            {
                target.DimensionsMM = oldDims;
                target.transform.position = oldPos;
                target.transform.rotation = oldRot;
                _rotationDisplay.Remember(shownRotation);
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

            _rows.SyncEnabledState();

            _fields.ShowRejections();
        }

        private bool WouldCauseViolation()
        {
            if (_target == null) return false;
            var list = PartRegistry.GetAll();
            var result = ConstraintValidator.Validate(list);
            return result.violations.Contains(_target);
        }

        private void RotateAxis(RotationAxis axis, float angle = 90f)
        {
            if (_target == null) return;
            if (FixedSize.IsYawOnly(_target) && axis != RotationAxis.Y) return;
            var oldRot = _target.transform.rotation;
            var stepped = RotationSteps.Step(_rotationDisplay.For(oldRot), axis, angle);
            _target.transform.rotation = Quaternion.Euler(stepped);
            _rotationDisplay.Remember(stepped);
            if (_target is IWallMounted wallMounted) wallMounted.SnapToWall();
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

        private void RelayoutForTarget()
        {
            if (_target == null) return;
            _facets = ElementFacets.Of(_target);
            ApplyLayout(_facets);
        }

        private void ApplyLayout(ElementFacet facets)
        {
            bool showRotationXZ = (facets & ElementFacet.Window) == ElementFacet.None
                && !FixedSize.IsYawOnly(_target);
            float contentBottom = _layout.Apply(facets, showRotationXZ, TopPad);
            if (_panelRt != null)
                _panelRt.sizeDelta = new Vector2(_panelRt.sizeDelta.x, contentBottom + BottomPad);
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

        internal void SyncOpenLabels()
        {
            if (_root == null || !_root.activeSelf || _target == null) return;
            RefreshOpenButtons();
        }

        private void RefreshOpenButtons()
        {
            foreach (var button in _openButtons) button.Refresh();
        }

        private void OpenButton(string node, string caption, RowVisibility visibility)
        {
            var binder = new OpenButtonBinder(() => _target as IOpenable);
            binder.Bind(_rows.WideButton(node, caption, binder.Toggle, visibility, ActionGap));
            _openButtons.Add(binder);
        }

        internal static DishwasherElement? FindDishwasherForFacade(FacadeElement facade) =>
            FacadeLinks.FindDishwasher(facade);

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

            var upperGo = DrawerLinks.DetachPairedUpper(_target);
            if (upperGo != null) CommandStack.Execute(new DeleteCommand(upperGo));

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
            var e = _rotationDisplay.For(_target.transform.rotation);
            _fields.Track(_rx, e.x.ToString("F1"));
            _fields.Track(_ry, e.y.ToString("F1"));
            _fields.Track(_rz, e.z.ToString("F1"));
        }
    }
}
