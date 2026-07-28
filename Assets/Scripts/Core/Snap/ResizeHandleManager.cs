using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Маркер ручки ресайза: к какой грани (0..5) относится.</summary>
    public class ResizeHandle : MonoBehaviour
    {
        public int faceIndex;
    }

    /// <summary>Два режима ручек на гранях выделенного объекта:
    /// Resize — тянем грань, меняется размер (наконечник-кубик);
    /// Move — двигаем объект вдоль одной оси (наконечник-стрелка/конус).
    /// В обоих режимах для грани/объекта работает прилипание к другим объектам.
    /// DefaultExecutionOrder=100 — ввод обрабатываем ПОСЛЕ SelectionManager/ElementMover,
    /// иначе из-за недетерминированного порядка клик по новому объекту иногда попадал
    /// в ещё не убранную ручку прежнего выделения и срабатывал ресайз вместо move.</summary>
    [DefaultExecutionOrder(100)]
    public class ResizeHandleManager : MonoBehaviour
    {
        public enum HandleMode { Resize, Move }

        public static ResizeHandleManager? Instance { get; private set; }

        /// <summary>Текущий режим ручек (переключается кнопкой в тулбаре).</summary>
        public static HandleMode Mode { get; private set; } = HandleMode.Resize;
        public static void ToggleMode() =>
            Mode = Mode == HandleMode.Resize ? HandleMode.Move : HandleMode.Resize;
        public static void SetMode(HandleMode mode) => Mode = mode;

        /// <summary>Идёт перетаскивание ручки (другие системы не должны реагировать).</summary>
        public static bool IsResizing { get; private set; }

        private static KitchenElement? _resizingElement;
        /// <summary>Этот элемент сейчас ресайзят/двигают ручкой? (WallManager не опускает его).</summary>
        public static bool IsResizingElement(KitchenElement e) =>
            IsResizing && e != null && e == _resizingElement;

        // Геометрия стрелки (в локальных координатах ручки, локальный +Z = нормаль грани).
        private const float Gap = 0.02f;
        private const float ShaftLen = 0.10f;
        private const float ShaftRad = 0.012f;
        private const float TipLen = 0.05f;
        private const float TipSize = 0.038f;
        // Вся стрелка от грани до кончика — на столько её сдвигают назад,
        // когда выносить наружу некуда (см. PositionHandles).
        private const float ArrowLen = Gap + ShaftLen + TipLen;

        private KitchenElement? _target;
        private readonly List<ResizeHandle> _handles = new List<ResizeHandle>();
        private readonly Material[] _axisMats = new Material[3];

        // Состояние активного перетаскивания.
        private int _faceIndex, _axisIndex;
        private Vector3 _normal, _faceCenter0, _uAxis, _vAxis, _centerStart;
        private Vector2 _faceSize;
        private float _sizeStartUnits, _sParam0;
        private Vector3Int _dimsBefore;
        private Vector3 _posBefore;
        private Quaternion _rotBefore;
        private HandleMode _dragMode;   // режим, в котором начали тянуть
        private HandleMode _builtMode;  // режим, в котором собраны текущие ручки

        private void Awake() => Instance = this;

        private void Start()
        {
            _axisMats[0] = MakeMat(new Color(0.90f, 0.25f, 0.25f)); // X
            _axisMats[1] = MakeMat(new Color(0.35f, 0.85f, 0.35f)); // Y
            _axisMats[2] = MakeMat(new Color(0.35f, 0.55f, 0.95f)); // Z

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

        // Один выделенный объект (не пол) → показываем ручки; иначе убираем.
        private void OnSelectionChanged(KitchenElement? element)
        {
            if (IsResizing) return;
            var sel = SelectionManager.Instance;
            bool single = sel != null && sel.SelectedElements.Count == 1 && element != null
                          && element.GetComponent<BasePlate>() == null;
            SetTarget(single ? element : null);
        }

        private void SetTarget(KitchenElement? element)
        {
            if (_target == element) return;
            ClearHandles();
            _target = element;
        }

        private void LateUpdate()
        {
            // Объект уничтожен (Unity fake-null) — убираем осиротевшие ручки.
            if (_target == null)
            {
                if (_handles.Count > 0) { IsResizing = false; ClearHandles(); }
                return;
            }
            if (!_target.gameObject.activeInHierarchy) { SetTarget(null); return; }

            // Ручки доступны только для подвижного объекта: запрет перемещения
            // запрещает и ресайз. Переключается на лету (чекбокс в свойствах).
            // Открытая дверца/выдвинутый ящик тоже недоступны — их геометрия
            // считается от закрытой позы и за трансформом не идёт (Transformable).
            // Смена режима (Resize/Move) пересобирает ручки с другим наконечником.
            // В режиме редактирования модуля чужие элементы недоступны.
            // В режиме рулетки ручек нет — они перехватывали бы клики по вершинам.
            bool show = _target.Transformable && ModuleEditMode.IsEditable(_target)
                        && !Measure.MeasureMode.Active;
            bool needRebuild = show && (_handles.Count == 0 || _builtMode != Mode) && !IsResizing;
            if (needRebuild) { ClearHandles(); BuildHandles(); }
            else if (!show && _handles.Count > 0) { IsResizing = false; ClearHandles(); }

            if (_handles.Count > 0 && !IsResizing) PositionHandles();
        }

        // --- Ввод ---

        private void Update()
        {
            // В режиме рулетки ручки не строятся (см. LateUpdate) — и тянуть
            // их нечем, но страхуемся от начатого до входа в режим драга.
            if (Measure.MeasureMode.Active) { if (IsResizing) FinishDrag(); return; }
            if (_target == null) return;

            if (IsResizing)
            {
                if (Input.GetMouseButton(0))
                {
                    if (_dragMode == HandleMode.Resize) UpdateResize();
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

        private static bool PointerOverUI() =>
            UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

        /// <summary>Курсор над ручкой ресайза? (для подавления выделения/панорамы камеры).</summary>
        public static bool PointerOverHandle()
        {
            var cam = Camera.main;
            if (cam == null) return false;
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            return Physics.Raycast(ray, out RaycastHit hit) &&
                   hit.collider.GetComponentInParent<ResizeHandle>() != null;
        }

        private bool RaycastHandle(out ResizeHandle? handle)
        {
            handle = null;
            var cam = Camera.main;
            if (cam == null) return false;
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit)) return false;
            handle = hit.collider.GetComponentInParent<ResizeHandle>();
            return handle != null;
        }

        private void BeginDrag(int faceIndex)
        {
            if (_target == null || !_target.Transformable) return; // запрет перемещения (или открытая дверца) запрещает и ресайз/move

            // Полускрытую стену сперва на полную высоту, ЗАТЕМ берём геометрию грани —
            // иначе стартовые размер/центр берутся в опущенном состоянии и объект «прыгает».
            _target.GetComponent<Wall>()?.RestoreFull();

            var faces = _target.GetFaces();
            if (faceIndex < 0 || faceIndex >= faces.Length) return;
            var f = faces[faceIndex];

            _faceIndex = faceIndex;
            _axisIndex = faceIndex / 2; // 0=X,1=Y,2=Z
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
            _sParam0 = ClosestParamOnNormal();

            _resizingElement = _target;
            _dragMode = Mode;
            IsResizing = true;
        }

        private void UpdateResize()
        {
            float sNow = ClosestParamOnNormal();
            if (float.IsNaN(sNow)) return;

            float rawDelta = sNow - _sParam0;
            var settings = KitchenSettings.Instance;
            bool snapEnabled = settings != null && settings.SnapEnabled;
            float threshold = settings != null ? settings.SnapThreshold * AppConstants.MM_TO_UNITS : 0f;

            ResizeMath.Compute(_dimsBefore, _axisIndex, _normal, _faceCenter0, _uAxis, _vAxis, _faceSize,
                _centerStart, _sizeStartUnits, rawDelta, PartRegistry.GetAll(), _target!,
                snapEnabled, threshold, out Vector3Int newDims, out Vector3 _, out _);

            _target!.DimensionsMM = newDims;
            // Центр — от ПРИНЯТОГО размера: деталь могла зажать запрошенный
            // (опора держит высоту в 80..130 мм и сечение 50×50), и центр под
            // невозможный размер отрывал её от опоры.
            _target.transform.position = ResizeMath.CenterForAppliedDims(
                _centerStart, _normal, _sizeStartUnits, _target.DimensionsMM, _axisIndex);

            PositionHandles();
            if (ElementHighlighter.Instance != null) ElementHighlighter.Instance.RefreshHighlights();
        }

        // Перемещение объекта вдоль одной оси (нормали грани) с прилипанием.
        private void UpdateMove()
        {
            float sNow = ClosestParamOnNormal();
            if (float.IsNaN(sNow)) return;

            Vector3 newPos = _centerStart + _normal * (sNow - _sParam0);

            var settings = KitchenSettings.Instance;
            if (settings != null && settings.SnapEnabled)
            {
                var snap = SnapSystem.TrySnap(_target!, PartRegistry.GetAll(), newPos);
                if (snap.snapped) // берём только составляющую снэпа вдоль оси
                    newPos = _centerStart + _normal * Vector3.Dot(snap.position - _centerStart, _normal);
            }

            _target!.transform.position = newPos;
            PositionHandles();
            if (ElementHighlighter.Instance != null) ElementHighlighter.Instance.RefreshHighlights();
        }

        private void FinishDrag()
        {
            IsResizing = false;
            _resizingElement = null;

            // Итог обязан лечь на мм-сетку. Снэп ставит грань заподлицо к соседу,
            // а размер тут же округляется до целых мм — на соседе, который сам
            // стоит на 0.5 мм, это оставляло дробную грань и разносило заразу
            // дальше. Выравниваем ДО снятия afterPos, чтобы в undo-стек попало
            // уже выровненное значение.
            if (_target != null) MmGrid.Snap(_target);

            var afterDims = _target!.DimensionsMM;
            var afterPos = _target.transform.position;
            bool changed = _dragMode == HandleMode.Resize
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
                if (_dragMode == HandleMode.Resize)
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

        // Параметр (в метрах) ближайшей точки луча мыши к прямой грань-нормаль.
        private float ClosestParamOnNormal()
        {
            var cam = Camera.main;
            if (cam == null) return float.NaN;
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            Vector3 d = _normal;                 // направление прямой (единичное)
            Vector3 e = ray.direction.normalized; // направление луча
            float b = Vector3.Dot(d, e);
            float denom = 1f - b * b;
            if (Mathf.Abs(denom) < Tolerance.EpsilonUnits) return float.NaN; // смотрим почти вдоль нормали
            Vector3 w0 = _faceCenter0 - ray.origin;
            float dW = Vector3.Dot(d, w0);
            float eW = Vector3.Dot(e, w0);
            return (b * eW - dW) / denom;
        }

        // --- Ручки ---

        private void BuildHandles()
        {
            _builtMode = Mode;
            // У ящика GTV высота и глубина фиксированы типом и длиной —
            // растягивать можно только ширину (ось X, грани 0 и 1).
            bool widthOnly = Mode == HandleMode.Resize && _target is DrawerElement;
            // У опоры сечение фиксировано (50×50) — тянуть можно только высоту
            // (ось Y, грани 2 и 3). Ручки X/Z ничего не меняли: DimensionsMM
            // возвращал прежний размер, и деталь просто не реагировала на драг.
            bool heightOnly = Mode == HandleMode.Resize && _target is PillarElement;
            // У окна ось Z (глубина) бессмысленна: двигать поперёк стены нельзя
            // (снап вернёт), а толщину диктует стена — ручки Z не создаём.
            bool skipDepth = _target is WindowElement || _target is DoorElement;
            // Мойка — покупное изделие фиксированного размера (её ApplyDimensions
            // возвращает габарит на место), тянуть у неё нечего: ручки ресайза
            // только вводили бы в заблуждение. Перемещать её можно.
            if (Mode == HandleMode.Resize && _target is SinkElement) return;
            var faces = _target!.GetFaces();
            for (int i = 0; i < faces.Length; i++)
            {
                if (widthOnly && i / 2 != 0) continue;
                if (heightOnly && i / 2 != 1) continue;
                if (skipDepth && i / 2 == 2) continue;
                var go = new GameObject($"ResizeHandle_{i}");
                var marker = go.AddComponent<ResizeHandle>();
                marker.faceIndex = i;

                // Грабельная только выступающая часть стрелки (наконечник), а не зона
                // у самой грани — иначе клик по телу объекта (особенно по центру грани,
                // обращённой к камере) случайно цеплял ручку и растягивал вместо move.
                var col = go.AddComponent<BoxCollider>();
                col.center = new Vector3(0, 0, Gap + ShaftLen + TipLen * 0.5f);
                col.size = new Vector3(TipSize * 1.7f, TipSize * 1.7f, TipLen + 0.04f);

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

        // Конус единичного масштаба вдоль +Z: основание (r=0.5) при z=-0.5, вершина при z=+0.5.
        private static Mesh? _coneMesh;
        private static Mesh ConeMesh()
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
                tris.Add(apex); tris.Add(next); tris.Add(cur);   // боковая грань
                tris.Add(baseC); tris.Add(cur); tris.Add(next);  // основание
            }
            _coneMesh = new Mesh();
            _coneMesh.SetVertices(verts);
            _coneMesh.SetTriangles(tris, 0);
            _coneMesh.RecalculateNormals();
            return _coneMesh;
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

            // Плоская деталь (стена, окно, полка, боковина, фасад, столешница):
            // центры её боковых граней лежат в толще, стрелка тонет в геометрии.
            // Выносим ручки боковых осей из плоскости в сторону камеры
            // (пересчитывается каждый кадр в LateUpdate, следит за камерой).
            var box = HandlePlacement.BoxOf(faces);
            int thinAxis = cam != null ? HandlePlacement.ThinAxis(box) : -1;
            Vector3 outOfPlate = thinAxis >= 0
                ? HandlePlacement.CameraOffset(box, thinAxis, cam!.transform.position, Gap)
                : Vector3.zero;

            // Окно/дверь сидят в проёме, и «соседом» для них всегда будет своя же
            // стена — откат стрелки только сдвигал бы ручки вдоль проёма. Выноса
            // к камере им достаточно.
            bool pullBack = thinAxis >= 0 && !(_target is WindowElement || _target is DoorElement);
            if (pullBack) CollectNeighbours(box);

            foreach (var h in _handles)
            {
                if (h == null || h.faceIndex >= faces.Length) continue;
                var f = faces[h.faceIndex];
                Vector3 n = f.normal.sqrMagnitude > Tolerance.EpsilonSqr ? f.normal.normalized : Vector3.forward;
                Vector3 up = Mathf.Abs(Vector3.Dot(n, Vector3.up)) > Tolerance.UpDotThreshold ? Vector3.forward : Vector3.up;
                Vector3 pos = f.center;

                // Ручки тонкой оси и так стоят снаружи широкой грани — сдвигать их
                // некуда: сдвиг увёл бы дальнюю из них внутрь самой детали.
                int axis = h.faceIndex / 2;
                if (thinAxis >= 0 && axis != thinAxis)
                {
                    pos += outOfPlate;
                    // Стрелка начинается внутри соседа (торец стены в смежной стене,
                    // конец полки в боковине) — сажаем её НА саму деталь, кончиком к грани.
                    if (pullBack)
                        pos -= n * HandlePlacement.PullBack(pos, n, ArrowLen, box.Half[axis], _neighbours);
                }

                h.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(n, up));
            }
        }

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
                if (e.GetComponent<BasePlate>() != null) continue; // пол под всей сценой — не помеха
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
