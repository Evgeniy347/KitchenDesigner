using System.Collections.Generic;
using UnityEngine;
using KitchenDesigner.Core.Handles;
using KitchenDesigner.Core.UI;

namespace KitchenDesigner.Core
{
    public class ResizeHandle : MonoBehaviour
    {
        public int faceIndex;

        public Vector3 grabPoint;
    }

    /// <summary>DefaultExecutionOrder=100 — ввод обрабатываем ПОСЛЕ
    /// SelectionManager/ElementMover, иначе из-за недетерминированного порядка клик
    /// по новому объекту иногда попадал в ещё не убранную ручку прежнего выделения
    /// и срабатывал ресайз вместо move.</summary>
    [DefaultExecutionOrder(100)]
    public class ResizeHandleManager : MonoBehaviour
    {
        public enum HandleMode { Resize, Move }

        public static ResizeHandleManager? Instance { get; private set; }

        public static HandleMode Mode { get; private set; } = HandleMode.Resize;
        public static void ToggleMode() =>
            Mode = Mode == HandleMode.Resize ? HandleMode.Move : HandleMode.Resize;
        public static void SetMode(HandleMode mode) => Mode = mode;

        public static bool IsResizing { get; private set; }

        private static KitchenElement? _resizingElement;
        public static bool IsResizingElement(KitchenElement e) =>
            IsResizing && e != null && e == _resizingElement;

        internal const int AxisX = 0;
        internal const int AxisY = 1;
        internal const int AxisZ = 2;

        internal static readonly HandleMetrics Metrics = HandleMetrics.Resize;

        private KitchenElement? _target;
        private readonly List<ResizeHandle> _handles = new List<ResizeHandle>();
        private readonly Material?[] _axisMats = new Material?[3];

        private int _faceIndex, _axisIndex;
        private Vector3 _normal, _faceCenter0, _uAxis, _vAxis, _centerStart;
        private Vector2 _faceSize;
        private float _sizeStartUnits, _sParam0;
        private Vector3Int _dimsBefore;
        private Vector3 _posBefore;
        private Quaternion _rotBefore;
        private HandleMode _modeAtDragStart;
        private HandleMode _modeHandlesWereBuiltIn;
        private bool _ctrlHeldLastFrame;

        private void Awake() => Instance = this;

        private void Start()
        {
            for (int axis = 0; axis < _axisMats.Length; axis++)
                _axisMats[axis] = HandleMaterials.For(HandleMaterials.ForAxis(axis));

            if (SelectionManager.Instance != null)
                SelectionManager.Instance.OnSelectionChanged += OnSelectionChanged;
        }

        private void OnDestroy()
        {
            if (SelectionManager.Instance != null)
                SelectionManager.Instance.OnSelectionChanged -= OnSelectionChanged;
            IsResizing = false;
            ClearHandles();
        }

        private void OnSelectionChanged(KitchenElement? element)
        {
            if (IsResizing) return;
            var sel = SelectionManager.Instance;
            bool singleNonFloorSelection = sel != null && sel.SelectedElements.Count == 1
                          && element != null && element.GetComponent<BasePlate>() == null;
            SetTarget(singleNonFloorSelection ? element : null);
        }

        private void SetTarget(KitchenElement? element)
        {
            if (_target == element) return;
            ClearHandles();
            _target = element;
        }

        private void LateUpdate()
        {
            using var _ = PerfMarkers.ResizeHandlesLateUpdate.Auto();
            // Объект уничтожен (Unity fake-null) — убираем осиротевшие ручки.
            if (_target == null)
            {
                if (_handles.Count > 0) { IsResizing = false; ClearHandles(); }
                return;
            }
            if (!_target.gameObject.activeInHierarchy) { SetTarget(null); return; }

            bool show = HandlesAvailableFor(_target);
            bool needRebuild = show && (_handles.Count == 0 || _modeHandlesWereBuiltIn != Mode) && !IsResizing;
            if (needRebuild) { ClearHandles(); BuildHandles(); }
            else if (!show && _handles.Count > 0) { IsResizing = false; ClearHandles(); }

            if (_handles.Count > 0 && !IsResizing) PositionHandles();
        }

        internal static bool HandlesAvailableFor(KitchenElement? target) =>
            target != null
            && target.Transformable
            && ModuleEditMode.IsEditable(target)
            && !Tools.ToolMode.MouseCaptured
            && !TextureOverlayHandles.Active;

        private void Update()
        {
            // В режиме инструмента ручки не строятся (см. LateUpdate) — и тянуть
            // их нечем, но страхуемся от начатого до входа в режим драга.
            if (Tools.ToolMode.MouseCaptured) { if (IsResizing) FinishDrag(); return; }
            // Правится область накладки — ручки элемента не строятся (см. LateUpdate),
            // но и ловить клики нечем: без этой проверки Update продолжал бы
            // искать ручку лучом и начинать ресайз поверх правки области.
            if (TextureOverlayHandles.Active) { if (IsResizing) FinishDrag(); return; }
            if (_target == null) return;

            if (IsResizing)
            {
                if (Input.GetMouseButton(0))
                {
                    if (_modeAtDragStart == HandleMode.Resize) UpdateResize();
                    else UpdateMove();
                }
                if (Input.GetMouseButtonUp(0)) FinishDrag();
                return;
            }

            if (!Input.GetMouseButtonDown(0)) return;
            if (HandleInput.AltHeld || HandleInput.PointerOverUI()) return;

            var handle = PickHandleUnderCursor();
            if (handle != null) BeginDrag(handle.faceIndex);
        }

        private static bool CtrlHeld => HandleInput.CtrlHeld;

        public static ResizeHandle? PickHandle(Vector2 screenPoint, Camera? camera,
            IReadOnlyList<ResizeHandle> handles) =>
            HandleScreenPick.Nearest(screenPoint, camera, handles, h => h.grabPoint);

        /// <summary>Курсор над ручкой ресайза? (для подавления выделения/панорамы камеры).</summary>
        public static bool PointerOverHandle() =>
            Instance != null && Instance.PickHandleUnderCursor() != null;

        private ResizeHandle? PickHandleUnderCursor() =>
            PickHandle(HandleInput.MouseScreenPoint, Camera.main, _handles);

        private void BeginDrag(int faceIndex)
        {
            if (_target == null || !_target.Transformable) return;

            // Полускрытую стену сперва на полную высоту, ЗАТЕМ берём геометрию грани —
            // иначе стартовые размер/центр берутся в опущенном состоянии и объект «прыгает».
            _target.GetComponent<Wall>()?.RestoreFull();

            var faces = _target.GetFaces();
            if (faceIndex < 0 || faceIndex >= faces.Length) return;
            var f = faces[faceIndex];

            _faceIndex = faceIndex;
            _axisIndex = faceIndex / 2;
            _normal = f.normal.normalized;
            _faceCenter0 = f.center;
            _uAxis = f.rightAxis;
            _vAxis = f.upAxis;
            _faceSize = f.size;
            _centerStart = _target.transform.position;

            var dims = _target.DimensionsMM;
            int dimMM = _axisIndex == 0 ? dims.x : (_axisIndex == 1 ? dims.y : dims.z);
            _sizeStartUnits = dimMM * AppConstants.MM_TO_UNITS;

            _dimsBefore = dims;
            _posBefore = _target.transform.position;
            _rotBefore = _target.transform.rotation;
            _sParam0 = ClosestParamOnNormalMeters();

            _resizingElement = _target;
            _modeAtDragStart = Mode;
            IsResizing = true;
        }

        private void UpdateResize()
        {
            float sNow = ClosestParamOnNormalMeters();
            if (float.IsNaN(sNow)) return;

            float rawDelta = sNow - _sParam0;
            var settings = KitchenSettings.Instance;
            bool globalSnap = settings != null && settings.SnapEnabled;
            bool snapEnabled = DragGesture.SnapAppliesTo(globalSnap, CtrlHeld);
            FlashCtrlHintOnce(globalSnap);
            float threshold = settings != null ? settings.SnapThreshold * AppConstants.MM_TO_UNITS : 0f;

            ResizeMath.Compute(_dimsBefore, _axisIndex, _normal, _faceCenter0, _uAxis, _vAxis, _faceSize,
                _centerStart, _sizeStartUnits, rawDelta,
                PartRegistry.GetAll().ToGeometry(), _target!.ToGeometry(),
                snapEnabled, threshold, out Vector3Int newDims, out Vector3 _, out _);

            _target!.DimensionsMM = newDims;
            _target.transform.position = ResizeMath.CenterForAppliedDims(
                _centerStart, _normal, _sizeStartUnits, _target.DimensionsMM, _axisIndex);

            PositionHandles();
            if (ElementHighlighter.Instance != null) ElementHighlighter.Instance.RefreshHighlights();
        }

        private void FlashCtrlHintOnce(bool globalSnap)
        {
            if (CtrlHeld == _ctrlHeldLastFrame) return;
            UI.StatusBarUI.Instance?.ShowTransient(
                globalSnap ? "Прилипание отключено (Ctrl)" : "Прилипание включено (Ctrl)",
                UIStyle.TextSecondary);
            _ctrlHeldLastFrame = CtrlHeld;
        }

        private void UpdateMove()
        {
            float sNow = ClosestParamOnNormalMeters();
            if (float.IsNaN(sNow)) return;

            Vector3 newPos = _centerStart + _normal * (sNow - _sParam0);

            var settings = KitchenSettings.Instance;
            bool globalSnap = settings != null && settings.SnapEnabled;
            bool effectiveSnap = DragGesture.SnapAppliesTo(globalSnap, CtrlHeld);
            FlashCtrlHintOnce(globalSnap);
            if (effectiveSnap)
            {
                var snap = SnapSystem.TrySnap(_target!, PartRegistry.GetAll(), newPos);
                if (snap.snapped)
                {
                    float alongAxis = Vector3.Dot(snap.position - _centerStart, _normal);
                    newPos = _centerStart + _normal * alongAxis;
                }
            }

            _target!.transform.position = newPos;
            PositionHandles();
            if (ElementHighlighter.Instance != null) ElementHighlighter.Instance.RefreshHighlights();
        }

        private void FinishDrag()
        {
            IsResizing = false;
            _resizingElement = null;
            _ctrlHeldLastFrame = false;

            // Итог обязан лечь на мм-сетку. Снэп ставит грань заподлицо к соседу,
            // а размер тут же округляется до целых мм — на соседе, который сам
            // стоит на 0.5 мм, это оставляло дробную грань и разносило заразу
            // дальше. Выравниваем ДО снятия afterPos, чтобы в undo-стек попало
            // уже выровненное значение.
            if (_target != null) MmGrid.Snap(_target);

            var afterDims = _target!.DimensionsMM;
            var afterPos = _target.transform.position;
            bool changed = _modeAtDragStart == HandleMode.Resize
                ? afterDims != _dimsBefore || afterPos != _posBefore
                : afterPos != _posBefore;

            // Как у перемещения (ElementMover.FinishDrag): нарушение при включённой
            // блокировке откатывает операцию. Раньше ресайз коммитился без проверки,
            // и красное состояние (например, пересечение с соседом) фиксировалось
            // в сцене и в undo-стеке.
            var settings = KitchenSettings.Instance;
            if (changed && settings != null && settings.BlockOnViolation && CausesViolation())
            {
                _target.DimensionsMM = _dimsBefore;
                _target.transform.position = _posBefore;
                changed = false;
            }

            if (changed)
            {
                if (_modeAtDragStart == HandleMode.Resize)
                    CommandStack.Execute(new ResizeCommand(_target,
                        _dimsBefore, afterDims, _posBefore, afterPos, _rotBefore, _rotBefore));
                else
                    CommandStack.Execute(new MoveCommand(_target,
                        _posBefore, afterPos, _rotBefore, _rotBefore));
            }

            if (ElementHighlighter.Instance != null) ElementHighlighter.Instance.RefreshHighlights();
            PositionHandles();
        }

        // Нарушение на самой детали или вплотную к ней (AABB в радиусе
        // snapThreshold * 2) — тот же критерий, что при перемещении.
        private bool CausesViolation()
        {
            var result = ConstraintValidator.Validate(PartRegistry.GetAll());
            if (result.isValid) return false;
            float radius = KitchenSettings.Instance.SnapThreshold * 2f * AppConstants.MM_TO_UNITS;
            return ConstraintValidator.HasViolationNear(result, _target!, radius);
        }

        private float ClosestParamOnNormalMeters()
        {
            var cam = Camera.main;
            if (cam == null) return float.NaN;
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            Vector3 lineDir = _normal;
            Vector3 rayDir = ray.direction.normalized;
            float b = Vector3.Dot(lineDir, rayDir);
            float denom = 1f - b * b;
            bool lookingAlongTheNormal = Mathf.Abs(denom) < Tolerance.EpsilonUnits;
            if (lookingAlongTheNormal) return float.NaN;
            Vector3 w0 = _faceCenter0 - ray.origin;
            float dW = Vector3.Dot(lineDir, w0);
            float eW = Vector3.Dot(rayDir, w0);
            return (b * eW - dW) / denom;
        }

        public static bool SupportsHandleResize(KitchenElement? element) =>
            element != null && !(element is SinkElement) && !(element is CooktopElement)
            && !FixedSize.IsFixed(element);

        private void BuildHandles()
        {
            _modeHandlesWereBuiltIn = Mode;
            bool widthOnly = Mode == HandleMode.Resize && _target is DrawerElement;
            bool heightOnly = Mode == HandleMode.Resize && _target is PillarElement;
            bool skipDepth = _target is WindowElement || _target is DoorElement;
            if (Mode == HandleMode.Resize && !SupportsHandleResize(_target)) return;
            var faces = _target!.GetFaces();
            for (int i = 0; i < faces.Length; i++)
            {
                if (widthOnly && i / 2 != 0) continue;
                if (heightOnly && i / 2 != 1) continue;
                if (skipDepth && i / 2 == 2) continue;
                var go = new GameObject($"ResizeHandle_{i}");
                var marker = go.AddComponent<ResizeHandle>();
                marker.faceIndex = i;

                HandleVisual.BuildArrow(go.transform, _axisMats[i / 2], Metrics,
                    HandleShaft.Cylinder,
                    Mode == HandleMode.Move ? HandleTip.Cone : HandleTip.Box);
                _handles.Add(marker);
            }
            PositionHandles();
        }

        private void PositionHandles()
        {
            if (_target == null) return;
            var faces = _target.GetFaces();
            var cam = Camera.main;

            var box = HandlePlacement.BoxOf(faces);
            int thinAxis = cam != null ? HandlePlacement.ThinAxis(box) : -1;
            Vector3 outOfPlate = thinAxis >= 0
                ? HandlePlacement.CameraOffset(box, thinAxis, cam!.transform.position, Metrics.Gap)
                : Vector3.zero;

            foreach (var h in _handles)
            {
                if (h == null || h.faceIndex >= faces.Length) continue;
                var f = faces[h.faceIndex];
                Vector3 n = f.normal.sqrMagnitude > Tolerance.EpsilonSqr ? f.normal.normalized : Vector3.forward;
                Vector3 up = Mathf.Abs(Vector3.Dot(n, Vector3.up)) > Tolerance.UpDotThreshold ? Vector3.forward : Vector3.up;
                Vector3 pos = f.center;

                bool alreadyOutsideThePlate = h.faceIndex / 2 == thinAxis;
                if (thinAxis >= 0 && !alreadyOutsideThePlate) pos += outOfPlate;

                h.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(n, up));
                h.grabPoint = pos + n * Metrics.TipCenterZ;
            }
        }

        private void ClearHandles()
        {
            foreach (var h in _handles)
                if (h != null) Destroy(h.gameObject);
            _handles.Clear();
        }
    }
}
