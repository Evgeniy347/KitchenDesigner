using System.Collections.Generic;
using KitchenDesigner.Core.Analysis;
using KitchenDesigner.Core.Update;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public class ContextMenuUI : MonoBehaviour, IContextMenuHost
    {
        public static ContextMenuUI? Instance { get; private set; }

        private const float InspectorMinHeight = 300f;
        private const int FurnitureEditorsPerStep = 8;
        public const string RotateButtonPrefix = "CtxRot";
        private const float QuarterTurnDegrees = 90f;
        private const float WindowRotationStepDegrees = 180f;

        public bool IsOpen => _root != null && _root.activeSelf;

        internal KitchenElement? OpenTarget => IsOpen ? _target : null;

        private GameObject? _root;
        private KitchenElement? _target;
        private WindowChrome? _chrome;
        private WindowBody? _body;

        private TMP_InputField? _name, _x, _y, _z, _rx, _ry, _rz;
        private VectorField? _position;
        private VectorField? _rotation;
        private InspectorSection? _specific;
        private (float x, float width)? _yawCell;
        private bool _heightUncapped;
        private Toggle? _lockToggle;
        private Toggle? _transparentToggle;
        private RectTransform? _panelRt;
        private InspectorSectionMemory _sectionMemory = new();

        private SceneViolations _violationsBeforeApply = SceneViolations.Empty;

        private bool _openInProgress;
        private DeferredBuild _content = new(System.Array.Empty<System.Action>());
        private int _builtAtFrame;
        private ElementFacet _facets;
        private readonly RotationDisplayState _rotationDisplay = new RotationDisplayState();

        private readonly ContextMenuTextureSection _textures;
        private readonly ContextMenuLightLinkSection _lightLinks;
        private readonly ContextMenuFieldTracker _fields;
        private readonly ContextMenuEdgeSection _edges;
        private readonly ContextMenuGrooveSection _grooves;
        private readonly ContextMenuGapSection _gaps;
        private readonly ContextMenuMaterialSection _materials;
        private readonly ContextMenuLevelSection _levels;
        private readonly ElementTypeConverter _types;
        private readonly LightFieldsEditor _lights;
        private readonly RadialFieldsEditor _radialFields;
        private readonly CooktopCutoutFieldsEditor _cooktopFields;
        private readonly DrawerBoxFieldsEditor _drawerFields;
        private readonly PillarFieldsEditor _pillarFields;
        private readonly ScrewLegFieldsEditor _screwLegFields;
        private readonly PipeFieldsEditor _pipeFields;
        private readonly PipeFittingFieldsEditor _pipeFittingFields;
        private readonly TableLegFieldsEditor _tableFields;
        private readonly StoolFieldsEditor _stoolFields;
        private readonly ChairFieldsEditor _chairFields;
        private readonly SofaFieldsEditor _sofaFields;
        private readonly BedFieldsEditor _bedFields;
        private readonly LaundryMachineFieldsEditor _laundryFields;
        private readonly PouffeFieldsEditor _pouffeFields;
        private readonly ToiletFieldsEditor _toiletFields;
        private readonly BathtubFieldsEditor _bathtubFields;
        private readonly BathMixerFieldsEditor _bathMixerFields;
        private readonly ShowerColumnFieldsEditor _showerColumnFields;
        private readonly WallDeviceFieldsEditor _wallDeviceFields;
        private readonly WallOpeningFieldsEditor _openingFields;
        private readonly WallFieldsEditor _wallFields;
        private readonly FoundationFieldsEditor _foundationFields;
        private readonly FloorSlabFieldsEditor _floorSlabFields;
        private readonly FenceFieldsEditor _fenceFields;
        private readonly DuctFieldsEditor _ductFields;
        private readonly GrilleFieldsEditor _grilleFields;
        private readonly RoofFieldsEditor _roofFields;
        private readonly WallLayerFieldsEditor _wallLayerFields;
        private readonly FacadeFieldsEditor _facadeFields;
        private readonly AssembledFacadeFieldsEditor _assembledFields;
        private readonly ElementFieldsEditor[] _editors;
        private readonly ElementFieldsEditor[] _furnitureEditors;
        private readonly ContextMenuAttachmentSection _attachments;
        private readonly ContextMenuSizeSection _sizes;
        private readonly ContextMenuTestHooks _testHooks;
        private InspectorRows _rows = null!;
        private readonly List<OpenButtonBinder> _openButtons = new();

        public ContextMenuUI()
        {
            _sizes = new ContextMenuSizeSection(this);
            _testHooks = new ContextMenuTestHooks(() => _name, _sizes, Apply, () => _sectionMemory = new InspectorSectionMemory(), uncapped => _heightUncapped = uncapped);
            _textures = new ContextMenuTextureSection(this);
            _lightLinks = new ContextMenuLightLinkSection(this);
            _fields = new ContextMenuFieldTracker(Apply);
            _edges = new ContextMenuEdgeSection(this);
            _grooves = new ContextMenuGrooveSection(this);
            _gaps = new ContextMenuGapSection(this);
            _materials = new ContextMenuMaterialSection(this);
            _levels = new ContextMenuLevelSection(this);
            _types = new ElementTypeConverter(this, Open);
            _lights = new LightFieldsEditor(this);
            _radialFields = new RadialFieldsEditor(this);
            _cooktopFields = new CooktopCutoutFieldsEditor(this);
            _drawerFields = new DrawerBoxFieldsEditor(this, Open);
            _pillarFields = new PillarFieldsEditor(this);
            _screwLegFields = new ScrewLegFieldsEditor(this);
            _pipeFields = new PipeFieldsEditor(this);
            _pipeFittingFields = new PipeFittingFieldsEditor(this);
            _tableFields = new TableLegFieldsEditor(this);
            _stoolFields = new StoolFieldsEditor(this);
            _chairFields = new ChairFieldsEditor(this);
            _sofaFields = new SofaFieldsEditor(this);
            _bedFields = new BedFieldsEditor(this);
            _laundryFields = new LaundryMachineFieldsEditor(this);
            _pouffeFields = new PouffeFieldsEditor(this);
            _toiletFields = new ToiletFieldsEditor(this);
            _bathtubFields = new BathtubFieldsEditor(this);
            _bathMixerFields = new BathMixerFieldsEditor(this);
            _showerColumnFields = new ShowerColumnFieldsEditor(this);
            _wallDeviceFields = new WallDeviceFieldsEditor(this);
            _openingFields = new WallOpeningFieldsEditor(this);
            _wallFields = new WallFieldsEditor(this);
            _foundationFields = new FoundationFieldsEditor(this);
            _floorSlabFields = new FloorSlabFieldsEditor(this);
            _fenceFields = new FenceFieldsEditor(this);
            _ductFields = new DuctFieldsEditor(this);
            _grilleFields = new GrilleFieldsEditor(this);
            _roofFields = new RoofFieldsEditor(this);
            _wallLayerFields = new WallLayerFieldsEditor(this);
            _facadeFields = new FacadeFieldsEditor(this);
            _assembledFields = new AssembledFacadeFieldsEditor(this);
            _attachments = new ContextMenuAttachmentSection(this);
            _furnitureEditors = new ElementFieldsEditor[]
            {
                _tableFields, _stoolFields, _chairFields, _sofaFields, _bedFields, _pouffeFields,
                _laundryFields, _toiletFields, _bathtubFields, _bathMixerFields, _showerColumnFields,
                _wallDeviceFields, _wallFields, _foundationFields, _floorSlabFields, _fenceFields,
                _ductFields, _grilleFields, _roofFields, _wallLayerFields, _pillarFields,
                _screwLegFields, _pipeFields, _pipeFittingFields,
            };
            _editors = new ElementFieldsEditor[]
            {
                _radialFields, _cooktopFields, _drawerFields, _pillarFields, _screwLegFields,
                _pipeFields, _pipeFittingFields,
                _tableFields, _stoolFields, _chairFields, _sofaFields, _bedFields,
                _pouffeFields, _laundryFields, _toiletFields, _bathtubFields, _bathMixerFields,
                _showerColumnFields, _wallDeviceFields, _openingFields, _wallFields,
                _foundationFields, _floorSlabFields, _fenceFields, _ductFields, _grilleFields,
                _roofFields, _wallLayerFields,
                _lights, _assembledFields, _facadeFields,
            };
        }

        KitchenElement? IContextMenuHost.Target => _target;

        InspectorRows IContextMenuHost.Rows => _rows;

        ContextMenuFieldTracker IContextMenuHost.Fields => _fields;

        void IContextMenuHost.Relayout() => RelayoutForTarget();

        ElementFacet IContextMenuHost.TargetFacets => _facets;

        DimensionFields IContextMenuHost.SizeFields => _sizes.Dimensions;

        internal ContextMenuTextureSection Textures => Built()._textures;

        internal InspectorRows Rows => Built()._rows;

        internal WindowChrome? Chrome => _chrome;

        internal ContextMenuGrooveSection Grooves => Built()._grooves;

        internal ContextMenuGapSection Gaps => Built()._gaps;

        internal ContextMenuSizeSection Sizes => Built()._sizes;

        internal ContextMenuMaterialSection Materials => Built()._materials;

        internal ContextMenuLevelSection Levels => Built()._levels;

        internal NameDropdownBinder AttachedFacade => Built()._attachments.Facade;

        internal ElementTypeConverter Types => Built()._types;

        internal ElementFieldsEditor[] Editors => Built()._editors;

        internal ContextMenuTestHooks TestHooks => Built()._testHooks;

        internal bool ContentBuilt => _content.Done;

        internal void BuildContent() => _content.RunAll();

        private ContextMenuUI Built()
        {
            _content.RunAll();
            return this;
        }

        private void Awake()
        {
            Instance = this;
        }

        public void Build(Transform canvas)
        {
            _chrome = WindowChrome.Create(canvas, "ContextMenu", Loc.T("elementType.part"),
                new Vector2(UIStyle.InspectorW, InspectorMinHeight), new WindowChromeOptions
                {
                    Kind = WindowKind.Tool,
                    OnClose = Close,
                    HasFooter = true,
                    RuledHeader = true,
                });
            var panel = _chrome.Panel;
            UIFactory.AnchorTopRight(panel);
            panel.anchoredPosition = new Vector2(-UIStyle.Space3, -(UIStyle.ToolbarH + UIStyle.Space3));
            _root = panel.gameObject;
            _panelRt = panel;
            _body = _chrome.CreateBody();
            _builtAtFrame = Time.frameCount;
            _content = new DeferredBuild(ContentSteps(), BuildWhileShown);
            _root!.SetActive(false);

            if (SelectionManager.Instance != null)
                SelectionManager.Instance.OnSelectionChanged += OnSelectionChanged;
        }

        private void BuildWhileShown(System.Action build)
        {
            bool wasShown = _root!.activeSelf;
            _root.SetActive(true);
            try
            {
                build();
            }
            finally
            {
                _root.SetActive(wasShown);
            }
        }

        private IEnumerable<System.Action> ContentSteps()
        {
            yield return BuildHead;
            yield return BuildSpecificSection;
            for (int from = 0; from < _furnitureEditors.Length; from += FurnitureEditorsPerStep)
            {
                int start = from;
                yield return () => BuildFurnitureEditors(start);
            }
            yield return _lights.Build;
            yield return BuildGrooveEdgeAndGapSections;
            yield return BuildPositionSection;
            yield return BuildSurfaceSections;
            yield return BuildTail;
        }

        private void BuildHead()
        {
            _rows = new InspectorRows(_body!.Content, () => _facets);
            _types.Build();
            _name = _rows.NameField();
            BuildDimensions();
        }

        private void BuildGrooveEdgeAndGapSections()
        {
            _grooves.Build();
            _edges.Build(_body!.Content);
            _gaps.Build();
        }

        private void BuildSurfaceSections()
        {
            _textures.Build(_materials.Build());
            _lightLinks.Build();
        }

        private void BuildTail()
        {
            BuildPropertySection();
            _rows.EndSection();
            BuildFooter(_chrome!.Footer!);
            ConfigureFieldInput();
            WireSectionMemory();

            _rows.Forms.Relayouted += FitPanel;
            ApplyLayout();
        }

        private void BuildDimensions()
        {
            _sizes.Build();
            _radialFields.Build();
            _cooktopFields.Build();
        }

        private void BuildSpecificSection()
        {
            _specific = _rows.BeginSection("Specific", Loc.T("elementType.part"), true);
            _facadeFields.Build();
            OpenButton("CtxDoor", OpenLabels.Open, RowVisibility.For(ElementFacet.Facade));
            OpenButton("CtxOvenDoor", OpenLabels.OpenDoor, RowVisibility.For(ElementFacet.Oven));
            OpenButton("CtxDishwasherDoor", OpenLabels.OpenDoor,
                RowVisibility.For(ElementFacet.Dishwasher));
            _assembledFields.Build();

            _drawerFields.Build();
            OpenButton("CtxDrawerAnim", OpenLabels.Open, RowVisibility.For(ElementFacet.Drawer));
            OpenButton("CtxSofaUnfold", OpenLabels.SofaExtend, RowVisibility.For(ElementFacet.Sofa));
            _attachments.BuildFacade();

            _openingFields.Build();
            OpenButton("CtxWinDoor", OpenLabels.Open, RowVisibility.For(ElementFacet.Window));
        }

        private void BuildFurnitureEditors(int start)
        {
            int end = System.Math.Min(start + FurnitureEditorsPerStep, _furnitureEditors.Length);
            for (int i = start; i < end; i++) _furnitureEditors[i].Build();
        }

        private void BuildPositionSection()
        {
            _rows.BeginSection("Position", Loc.T("element.common.position"), true);
            _attachments.BuildParent();
            _levels.Build();

            _position = _rows.Vector("Position", Loc.T("element.common.positionMm"), null, null,
                RowVisibility.Always);
            (_x, _y, _z) = (_position.Fields[0], _position.Fields[1], _position.Fields[2]);

            _rotation = _rows.Vector("Rotation", Loc.T("element.common.rotationDeg"), null,
                axis => RotateAxis((RotationAxis)axis, RotationStepDegrees()), RowVisibility.Always);
            (_rx, _ry, _rz) = (_rotation.Fields[0], _rotation.Fields[1], _rotation.Fields[2]);
            for (int axis = 0; axis < _rotation.RotateButtons.Count; axis++)
            {
                int captured = axis;
                _rotation.RotateButtons[axis].gameObject.name = RotateButtonPrefix + VectorField.AxisNames[axis];
                TooltipUI.Attach(_rotation.RotateButtons[axis].gameObject, () => RotateTooltip(captured));
            }
        }

        private string RotateTooltip(int axis) =>
            Loc.F("element.common.rotateAxis", RotationStepDegrees(), VectorField.AxisNames[axis]);

        private float RotationStepDegrees() =>
            _facets.Has(ElementFacet.Window) ? WindowRotationStepDegrees : QuarterTurnDegrees;

        private void BuildPropertySection()
        {
            _rows.BeginSection("Properties", Loc.T("element.common.properties"), true);
            _transparentToggle = _rows.Switch("CtxTransparent", Loc.T("element.common.transparent"), false, v =>
            {
                var target = _target;
                if (target == null) return;
                ChoiceRowUndo.Commit(target, () => target.Transparent = v);
                if (ElementHighlighter.Instance != null)
                    ElementHighlighter.Instance.ApplyForElement(target);
                if (SelectionManager.Instance != null)
                    SelectionManager.Instance.RefreshHighlight(target);
            }, RowVisibility.Always);

            _lockToggle = _rows.Switch("CtxLock", Loc.T("element.common.lock"), false,
                v =>
                {
                    var target = _target;
                    if (target == null) return;
                    ChoiceRowUndo.Commit(target, () => target.Movable = !v);
                },
                RowVisibility.Always);
        }

        private void BuildFooter(WindowFooter footer)
        {
            footer.AddLeft("CtxDup", Loc.T("element.common.duplicate"), Duplicate);
            var delete = footer.AddRight("CtxDel", Loc.T("common.delete"), () => { }, ButtonRole.DangerOutline);
            var confirm = ConfirmDeleteButton.Attach(delete, Delete);
            confirm.ArmedChanged += armed =>
                ButtonRoles.Paint(delete, armed ? ButtonRole.Danger : ButtonRole.DangerOutline);
        }

        private void WireSectionMemory()
        {
            foreach (var section in _rows.Sections)
            {
                var captured = section;
                section.View.Toggled += expanded =>
                {
                    if (_target != null) _sectionMemory.Remember(TypeKeyOf(_target), captured.Id, expanded);
                };
            }
        }

        private void RestoreSectionStates(KitchenElement element)
        {
            string type = TypeKeyOf(element);
            foreach (var section in _rows.Sections)
                section.View.SetExpanded(
                    _sectionMemory.IsExpanded(type, section.Id, section.ExpandedByDefault), notify: false);
        }

        private static string TypeKeyOf(KitchenElement element) =>
            element.GetComponent<Wall>() != null ? nameof(Wall) : element.GetType().Name;

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
            var fields = new List<TMP_InputField?>();
            _sizes.CollectArithmeticFields(fields);
            fields.Add(_x);
            fields.Add(_y);
            fields.Add(_z);
            foreach (var editor in _editors) fields.AddRange(editor.ArithmeticFields());
            return fields.ToArray();
        }

        private void OnDestroy()
        {
            if (SelectionManager.Instance != null)
                SelectionManager.Instance.OnSelectionChanged -= OnSelectionChanged;
        }

        private int _deferCloseFrame = -1;

        internal bool DeferredCloseIsPending => _deferCloseFrame >= 0;

        private void ForgetDeferredClose() => _deferCloseFrame = -1;

        internal void OnSelectionChanged(KitchenElement? element)
        {
            if (_openInProgress) return;
            if (_root == null || !_root.activeSelf) return;
            if (element == null)
            {
                _deferCloseFrame = Time.frameCount;
                return;
            }
            ForgetDeferredClose();
            if (element != _target)
                Open(element);
        }

        internal void ProcessDeferredClose()
        {
            if (DeferredCloseIsPending && _deferCloseFrame < Time.frameCount)
            {
                ForgetDeferredClose();
                Close();
            }
        }

        private void Update()
        {
            using var _ = PerfMarkers.ContextMenuUpdate.Auto();
            ProcessDeferredClose();

            if (Input.GetKeyDown(KeyCode.Escape) && IsOpen)
                TryCloseFromEscape();

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
            HoverPreviewGate.Sync();
            PrewarmStep(Time.frameCount - _builtAtFrame, Input.anyKey);
        }

        internal bool PrewarmStep(int framesSinceBuild, bool inputActive) =>
            _content.TryPrewarmStep(framesSinceBuild, inputActive);

        internal void TryCloseFromEscape()
        {
            if (OwnsEscape()) Close();
        }

        private static bool OwnsEscape() =>
            EscapeOwnership.Resolve(new EscapeClaims
            {
                ModalOpen = ModalPresence.IsOpen,
                ContextMenuOpen = true,
                Dragging = ElementMover.IsDragging,
                LightPicking = Lighting.LightPickMode.Active,
                Measuring = Measure.MeasureMode.Active,
                Eyedropping = Tools.EyedropperMode.Active,
                HintOpen = HintBubbleUI.IsOpen,
                ConfirmArmed = ConfirmDeleteButton.AnyArmed,
            }) == EscapeOwner.ContextMenu;

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

            _attachments.RefreshCaptionColor();

            var eu = _rotationDisplay.For(_target.transform.rotation);
            _fields.RefreshUnfocused(_rx, NumberFormat.Input(eu.x, 1));
            _fields.RefreshUnfocused(_ry, NumberFormat.Input(eu.y, 1));
            _fields.RefreshUnfocused(_rz, NumberFormat.Input(eu.z, 1));
            _sizes.RefreshFrom(_target.DimensionsMM);
            _fields.RefreshUnfocused(_name, _target.PartName);
            RefreshTitle();

            _gaps.RefreshFromTarget();
            foreach (var editor in _editors) editor.Refresh(_target);
            _rows.SyncComputed();
        }

        private static string ToMM(float meters) =>
            Mathf.RoundToInt(meters / AppConstants.MM_TO_UNITS).ToString();

        private void RefreshTitle()
        {
            if (_chrome == null || _target == null) return;
            _chrome.SetTitle($"{_target.DisplayTypeName} — {_target.PartName}");
        }

        public void Open(KitchenElement element)
        {
            if (element == null) return;

            _content.RunAll();
            SideHighlighter.Hide();

            element = element.InspectedElement;

            _openInProgress = true;
            try
            {
                ForgetDeferredClose();
                _textures.EndPreview();
                _materials.EndPreview();
                _target = element;
                _rotationDisplay.Forget();
                _lightLinks.ForgetPicking();
                TextureOverlayHandles.End();
                if (SelectionManager.Instance != null)
                    SelectionManager.Instance.Select(element);

                _facets = ElementFacets.Of(element);
                RefreshTitle();
                _specific?.SetTitle(element.DisplayTypeName);
                RestoreSectionStates(element);
                _types.ShowFor(element);

                _name!.text = element.PartName;
                _sizes.WriteFrom(element.DimensionsMM);
                foreach (var editor in _editors) editor.Show(element);

                _gaps.WriteFrom(element);
                RefreshOpenButtons();

                _attachments.ShowFor(element);

                _sizes.ShowLocks(element, EditorFor(element));

                _materials.ShowFor(element);
                _levels.ShowFor(element);

                _grooves.Refresh();
                _edges.Refresh();
                if (element.SupportsTextureOverlays) _textures.RebuildMaterialOptions();
                _textures.Refresh();
                _lightLinks.Refresh();
                RelayoutForTarget();

                RefreshTransformFields();
                SwitchControl.SetWithoutNotify(_transparentToggle!, element.Transparent);
                SwitchControl.SetWithoutNotify(_lockToggle!, !element.Movable);

                _fields.ClearHighlights();
                TrackAllFields();

                _rows.SyncEnabledState();
                _rows.SyncComputed();

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
            ForgetDeferredClose();
            SideHighlighter.Hide();
            HoverPreviewGate.HideAll();
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
            _violationsBeforeApply = SceneViolations.OfScene();
            bool blocked = false;

            CommandStack.BeginCapture();
            try
            {
                blocked = ApplyFields(target);
                var propsAfter = UndoableProperties.Capture(target);
                if (blocked) RevertEverythingApplyTouched(target, propsBefore, propsAfter);
                else
                {
                    var propsCommand = SetPropertiesCommand.TryCreate(target, propsBefore, propsAfter);
                    if (propsCommand != null) CommandStack.Execute(propsCommand);
                }
            }
            finally
            {
                CommandStack.EndCapture($"Свойства {target.PartName}", commit: !blocked);
            }

            RefreshAfterApply(target);
        }

        private static void RevertEverythingApplyTouched(KitchenElement target,
            ElementPropertyBag propsBefore, ElementPropertyBag propsAfter)
        {
            if (propsBefore.TryGet(nameof(KitchenElement.PartName), out var oldName)
                && oldName is string named && target.PartName != named)
            {
                DrawerLinks.Rename(target, named);
                target.gameObject.name = target.PartName;
            }
            UndoableProperties.Restore(target, propsBefore,
                UndoableProperties.Changed(propsBefore, propsAfter));
        }

        private bool ApplyFields(KitchenElement target)
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

            var anchor = ResizeShift.Before(target);
            _sizes.ApplyTo(target, EditorFor(target), oldDims);
            foreach (var editor in _editors) editor.Apply(target);

            _materials.ApplySecondarySlotChoice(target);

            _gaps.ApplyTo(target);

            _edges.ApplyThickness(target);

            target.transform.position = new Vector3(
                _fields.IsDirty(_x) ? _fields.ParseMillimetresAsMetres(_x, oldPos.x) : oldPos.x,
                _fields.IsDirty(_y) ? _fields.ParseMillimetresAsMetres(_y, oldPos.y) : oldPos.y,
                _fields.IsDirty(_z) ? _fields.ParseMillimetresAsMetres(_z, oldPos.z) : oldPos.z);

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

            if (target.transform.position == oldPos && target.transform.rotation == oldRot)
                target.transform.position += anchor.After(target);

            if (KitchenSettings.Instance.BlockOnViolation
                && ThisEditIntroducedAViolation(target, out var refusal))
            {
                target.DimensionsMM = oldDims;
                target.transform.position = oldPos;
                target.transform.rotation = oldRot;
                _rotationDisplay.Remember(shownRotation);
                EditRefusalReport.Show(refusal);
                return true;
            }

            CommandStack.Execute(new ResizeCommand(target,
                oldDims, target.DimensionsMM,
                oldPos, target.transform.position,
                oldRot, target.transform.rotation));

            var followers = AttachMove.FollowersCommand(target,
                oldPos, oldRot, target.transform.position, target.transform.rotation);
            if (followers != null) CommandStack.Execute(followers);
            return false;
        }

        private void RefreshAfterApply(KitchenElement target)
        {
            _sizes.WriteAfterApply(target.DimensionsMM, EditorFor(target));
            foreach (var editor in _editors) editor.AfterApply(target);

            _gaps.WriteFrom(_target);

            RefreshTitle();
            _edges.Refresh();
            RefreshHighlights();

            _fields.ClearHighlights();
            TrackAllFields();

            _rows.SyncEnabledState();
            _rows.SyncComputed();

            _fields.ShowRejections();
        }

        private bool ThisEditIntroducedAViolation(KitchenElement target, out string refusal) =>
            EditGate.Refuses(_violationsBeforeApply, SceneViolations.OfScene(),
                target, out refusal);

        private void RotateAxis(RotationAxis axis, float angle = 90f)
        {
            if (_target == null) return;
            if (FixedSize.IsYawOnly(_target) && axis != RotationAxis.Y) return;
            var oldRot = _target.transform.rotation;
            var oldPos = _target.transform.position;
            var reseatable = _target as IReseatsPortsAfterRotation;
            var capturedLinks = reseatable?.CaptureLinksForRotation(PartRegistry.GetAll());

            var stepped = RotationSteps.Step(_rotationDisplay.For(oldRot), axis, angle);
            _target.transform.rotation = Quaternion.Euler(stepped);
            _rotationDisplay.Remember(stepped);
            if (_target is IWallMounted wallMounted) wallMounted.SnapToWall();
            reseatable?.ReseatAfterRotation(capturedLinks);

            var rotCmds = new List<IUndoCommand>
            {
                new MoveCommand(_target, oldPos, _target.transform.position,
                    oldRot, _target.transform.rotation)
            };
            AttachMove.AppendFollowers(rotCmds, _target, oldPos,
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
            ApplyLayout();
        }

        private void ApplyLayout()
        {
            ShowRotationAxes();
            _rows.Relayout();
        }

        private void ShowRotationAxes()
        {
            if (_rotation == null) return;
            bool allAxes = !_facets.Has(ElementFacet.Window) && !FixedSize.IsYawOnly(_target);
            var fields = _rotation.Fields;
            fields[0].gameObject.SetActive(allAxes);
            fields[2].gameObject.SetActive(allAxes);

            var yaw = (RectTransform)fields[1].transform;
            _yawCell ??= (yaw.anchoredPosition.x, yaw.sizeDelta.x);
            float width = allAxes ? _yawCell.Value.width : _rows.Metrics.Width;
            yaw.anchoredPosition = new Vector2(allAxes ? _yawCell.Value.x : 0f, yaw.anchoredPosition.y);
            yaw.sizeDelta = new Vector2(width, yaw.sizeDelta.y);
        }

        private void FitPanel()
        {
            if (_chrome == null || _panelRt == null || _body == null) return;
            _chrome.FitHeightTo(_rows.Forms.Height);
            float ceiling = MaxPanelHeight();
            if (_panelRt.sizeDelta.y > ceiling)
                _panelRt.sizeDelta = new Vector2(_panelRt.sizeDelta.x, ceiling);
            _body.Fit();
        }

        private float MaxPanelHeight()
        {
            if (_heightUncapped) return float.MaxValue;
            if (_panelRt == null || !(_panelRt.root is RectTransform canvas) || canvas.rect.height <= 0f)
                return float.MaxValue;
            float fits = canvas.rect.height - UIStyle.ToolbarH - UIStyle.StatusBarH - 2f * UIStyle.Space4;
            return Mathf.Max(InspectorMinHeight, fits);
        }

        private ElementFieldsEditor? EditorFor(KitchenElement element)
        {
            foreach (var editor in _editors)
                if (editor.Handles(element)) return editor;
            return null;
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
            var button = _rows.ValueButton(node, caption, binder.Toggle, visibility);
            binder.Bind(button.GetComponentInChildren<TMP_Text>());
            _openButtons.Add(binder);
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

            var upperGo = DrawerLinks.DetachPairedUpper(_target);
            if (upperGo != null) CommandStack.Execute(new DeleteCommand(upperGo));

            if (SelectionManager.Instance != null)
                SelectionManager.Instance.Deselect();
            Close();
            CommandStack.Execute(new DeleteCommand(go));
            RefreshHighlights();

            string expected = $"Delete {deletedName}";
            ToastNotification.ShowIfAvailable(Loc.F("toast.deleted", deletedName), 5f, Loc.T("common.undo"), () =>
            {
                if (CommandStack.CanUndo && CommandStack.PeekUndoDescription() == expected)
                {
                    CommandStack.Undo();
                    RefreshHighlights();
                }
            }, StatusLevel.Success);
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
            _sizes.Track(_target.DimensionsMM);
            _gaps.Track();
            foreach (var editor in _editors) editor.Track(_target);
            _edges.Track();
            var pos = _target.transform.position;
            _fields.Track(_x, ToMM(pos.x));
            _fields.Track(_y, ToMM(pos.y));
            _fields.Track(_z, ToMM(pos.z));
            var e = _rotationDisplay.For(_target.transform.rotation);
            _fields.Track(_rx, NumberFormat.Input(e.x, 1));
            _fields.Track(_ry, NumberFormat.Input(e.y, 1));
            _fields.Track(_rz, NumberFormat.Input(e.z, 1));
        }
    }
}
