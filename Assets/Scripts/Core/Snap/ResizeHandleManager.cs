using System.Collections.Generic;
using UnityEngine;
using KitchenDesigner.Core.UI;

namespace KitchenDesigner.Core
{
    public class ResizeHandle : MonoBehaviour
    {
        public int faceIndex;
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

        internal const float Gap = 0.02f;
        internal const float ShaftLen = 0.10f;
        private const float ShaftRad = 0.012f;
        internal const float TipLen = 0.05f;
        internal const float TipSize = 0.038f;
        internal const float ArrowLen = Gap + ShaftLen + TipLen;

        internal const float GrabBoxCenterZ = Gap + ShaftLen + TipLen * 0.5f;
        internal const float GrabBoxDepth = TipLen + 0.04f;

        private KitchenElement? _target;
        private readonly List<ResizeHandle> _handles = new List<ResizeHandle>();
        private readonly Material[] _axisMats = new Material[3];

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
            _axisMats[AxisX] = MakeMat(new Color(0.90f, 0.25f, 0.25f));
            _axisMats[AxisY] = MakeMat(new Color(0.35f, 0.85f, 0.35f));
            _axisMats[AxisZ] = MakeMat(new Color(0.35f, 0.55f, 0.95f));

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
            if (AltHeld || PointerOverUI()) return;

            if (RaycastHandle(out var handle))
                BeginDrag(handle!.faceIndex);
        }

        private static bool AltHeld => Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

        private static bool ShiftHeld => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        private static bool CtrlHeld => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

        private static bool PointerOverUI() =>
            UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

        public static ResizeHandle? PickHandleFromHits(RaycastHit[] orderedHits, bool shiftHeld)
        {
            foreach (var h in orderedHits)
            {
                var handle = h.collider.GetComponentInParent<ResizeHandle>();
                if (handle == null)
                    continue;
                if (!shiftHeld)
                    return handle;
                var el = h.collider.GetComponentInParent<KitchenElement>();
                if (el == null || !el.Transparent)
                    return handle;
            }
            return null;
        }

        private static ResizeHandle? RaycastHandleTransparentAware(Ray ray, bool shiftHeld)
        {
            if (!shiftHeld)
            {
                if (Physics.Raycast(ray, out RaycastHit hit))
                    return hit.collider.GetComponentInParent<ResizeHandle>();
                return null;
            }

            var allHits = Physics.RaycastAll(ray);
            System.Array.Sort(allHits, (a, b) => a.distance.CompareTo(b.distance));
            return PickHandleFromHits(allHits, shiftHeld: true);
        }

        /// <summary>Курсор над ручкой ресайза? (для подавления выделения/панорамы камеры).</summary>
        public static bool PointerOverHandle()
        {
            var cam = Camera.main;
            if (cam == null) return false;
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            return RaycastHandleTransparentAware(ray, ShiftHeld) != null;
        }

        private bool RaycastHandle(out ResizeHandle? handle)
        {
            handle = null;
            var cam = Camera.main;
            if (cam == null) return false;
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            handle = RaycastHandleTransparentAware(ray, ShiftHeld);
            return handle != null;
        }

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

                var col = go.AddComponent<BoxCollider>();
                col.center = new Vector3(0, 0, GrabBoxCenterZ);
                col.size = new Vector3(TipSize * 1.7f, TipSize * 1.7f, GrabBoxDepth);

                BuildArrowVisual(go.transform, _axisMats[i / 2]);
                _handles.Add(marker);
            }
            PositionHandles();
        }

        // Resize-режим: наконечник-кубик; Move-режим: наконечник-конус (стрелка).
        private void BuildArrowVisual(Transform parent, Material mat)
        {
            // Стержень (Cylinder высотой 2 по Y → ориентируем по +Z).
            var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            StripCollider(shaft);
            shaft.transform.SetParent(parent, false);
            shaft.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            shaft.transform.localPosition = new Vector3(0, 0, Gap + ShaftLen * 0.5f);
            shaft.transform.localScale = new Vector3(ShaftRad * 2f, ShaftLen * 0.5f, ShaftRad * 2f);
            shaft.GetComponent<MeshRenderer>().sharedMaterial = mat;

            GameObject tip;
            if (Mode == HandleMode.Move)
            {
                // Конус-стрелка (локальный +Z = направление оси).
                tip = new GameObject("Tip");
                tip.AddComponent<MeshFilter>().sharedMesh = ConeMesh();
                tip.AddComponent<MeshRenderer>().sharedMaterial = mat;
                tip.transform.SetParent(parent, false);
                tip.transform.localScale = new Vector3(TipSize * 1.4f, TipSize * 1.4f, TipLen * 1.3f);
            }
            else
            {
                tip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                StripCollider(tip);
                tip.transform.SetParent(parent, false);
                tip.transform.localScale = new Vector3(TipSize, TipSize, TipLen);
                tip.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
            tip.transform.localPosition = new Vector3(0, 0, Gap + ShaftLen + TipLen * 0.5f);
        }

        private static Mesh? _coneMesh;
        internal static Mesh ConeMesh()
        {
            if (_coneMesh != null) return _coneMesh;
            const int seg = 16;
            var verts = new List<Vector3> { new Vector3(0, 0, 0.5f), new Vector3(0, 0, -0.5f) };
            int apex = 0, baseC = 1, ring = verts.Count;
            for (int i = 0; i < seg; i++)
            {
                float a = (float)i / seg * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a) * 0.5f, Mathf.Sin(a) * 0.5f, -0.5f));
            }
            var tris = new List<int>();
            for (int i = 0; i < seg; i++)
            {
                int cur = ring + i, next = ring + (i + 1) % seg;
                AddSideTriangle(tris, apex, next, cur);
                AddBaseTriangle(tris, baseC, cur, next);
            }
            _coneMesh = new Mesh();
            _coneMesh.SetVertices(verts);
            _coneMesh.SetTriangles(tris, 0);
            _coneMesh.RecalculateNormals();
            return _coneMesh;
        }

        private static void AddSideTriangle(List<int> tris, int a, int b, int c)
        {
            tris.Add(a); tris.Add(b); tris.Add(c);
        }

        private static void AddBaseTriangle(List<int> tris, int a, int b, int c)
        {
            tris.Add(a); tris.Add(b); tris.Add(c);
        }

        private static void StripCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
        }

        private void PositionHandles()
        {
            if (_target == null) return;
            var faces = _target.GetFaces();
            var cam = Camera.main;

            var box = HandlePlacement.BoxOf(faces);
            int thinAxis = cam != null ? HandlePlacement.ThinAxis(box) : -1;
            Vector3 outOfPlate = thinAxis >= 0
                ? HandlePlacement.CameraOffset(box, thinAxis, cam!.transform.position, Gap)
                : Vector3.zero;

            bool pullBack = thinAxis >= 0 && ArrowPullBackApplies(_target);
            if (pullBack) CollectNeighbours(box);

            foreach (var h in _handles)
            {
                if (h == null || h.faceIndex >= faces.Length) continue;
                var f = faces[h.faceIndex];
                Vector3 n = f.normal.sqrMagnitude > Tolerance.EpsilonSqr ? f.normal.normalized : Vector3.forward;
                Vector3 up = Mathf.Abs(Vector3.Dot(n, Vector3.up)) > Tolerance.UpDotThreshold ? Vector3.forward : Vector3.up;
                Vector3 pos = f.center;

                int axis = h.faceIndex / 2;
                bool alreadyOutsideThePlate = axis == thinAxis;
                if (thinAxis >= 0 && !alreadyOutsideThePlate)
                {
                    pos += outOfPlate;
                    if (pullBack)
                        pos -= n * HandlePlacement.PullBack(pos, n, ArrowLen, box.Half[axis], _neighbours);
                }

                h.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(n, up));
            }
        }

        internal static bool ArrowPullBackApplies(KitchenElement? target) =>
            !(target is WindowElement || target is DoorElement);

        // Соседи для проверки «стрелка внутри чужой геометрии». Список переиспользуем:
        // PositionHandles вызывается каждый кадр, а PartRegistry содержит всю сцену.
        private readonly List<HandlePlacement.Box> _neighbours = new List<HandlePlacement.Box>();

        // Стрелка торчит от грани всего на ArrowLen, поэтому помешать может только
        // сосед вплотную к детали. Сперва дешёвый отсев по расстоянию до габарита
        // (без GetFaces, который каждый раз аллоцирует массивы) — иначе на стене,
        // где габарит с полкомнаты, ящики строились бы для всей сцены каждый кадр.
        private void CollectNeighbours(in HandlePlacement.Box box)
        {
            _neighbours.Clear();
            var all = PartRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                var e = all[i];
                if (e == null || e == _target) continue;
                if (!e.gameObject.activeInHierarchy) continue;
                bool isTheSceneWideFloor = e.GetComponent<BasePlate>() != null;
                if (isTheSceneWideFloor) continue;
                Vector3 half = (Vector3)e.DimensionsMM * (AppConstants.MM_TO_UNITS * 0.5f);
                if (box.DistanceTo(e.transform.position) > ArrowLen + half.magnitude) continue;
                _neighbours.Add(HandlePlacement.BoxOf(e.GetFaces()));
            }
        }

        private void ClearHandles()
        {
            foreach (var h in _handles)
                if (h != null) Destroy(h.gameObject);
            _handles.Clear();
        }

        private static Material MakeMat(Color c)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(sh);
            m.SetColor("_BaseColor", c);
            m.color = c;
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * 0.6f);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", 0.2f);
            m.SetFloat("_Cull", 0f); // двусторонний — конус виден независимо от winding
            return m;
        }
    }
}
