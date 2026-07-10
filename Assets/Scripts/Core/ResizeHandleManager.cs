using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Маркер ручки ресайза: к какой грани (0..5) относится.</summary>
    public class ResizeHandle : MonoBehaviour
    {
        public int faceIndex;
    }

    /// <summary>Показывает 6 стрелок по центрам граней выделенного объекта. Тянем
    /// стрелку → соответствующая грань вытягивается (меняется размер по этой оси),
    /// противоположная грань стоит на месте. Для движущейся грани работает прилипание
    /// к встречным граням других объектов (ResizeSnap).</summary>
    public class ResizeHandleManager : MonoBehaviour
    {
        public static ResizeHandleManager Instance { get; private set; }

        /// <summary>Идёт перетаскивание ручки (другие системы не должны реагировать).</summary>
        public static bool IsResizing { get; private set; }

        private static KitchenElement _resizingElement;
        /// <summary>Этот элемент сейчас ресайзят? (WallManager не опускает такую стену).</summary>
        public static bool IsResizingElement(KitchenElement e) =>
            IsResizing && e != null && e == _resizingElement;

        // Геометрия стрелки (в локальных координатах ручки, локальный +Z = нормаль грани).
        private const float Gap = 0.02f;
        private const float ShaftLen = 0.10f;
        private const float ShaftRad = 0.012f;
        private const float TipLen = 0.05f;
        private const float TipSize = 0.038f;

        private KitchenElement _target;
        private readonly List<ResizeHandle> _handles = new List<ResizeHandle>();
        private readonly Material[] _axisMats = new Material[3];

        // Состояние активного ресайза.
        private int _faceIndex, _axisIndex;
        private Vector3 _normal, _faceCenter0, _uAxis, _vAxis, _centerStart;
        private Vector2 _faceSize;
        private float _sizeStartUnits, _sParam0;
        private Vector3Int _dimsBefore;
        private Vector3 _posBefore;
        private Quaternion _rotBefore;

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
        private void OnSelectionChanged(KitchenElement element)
        {
            if (IsResizing) return;
            var sel = SelectionManager.Instance;
            bool single = sel != null && sel.SelectedElements.Count == 1 && element != null
                          && element.GetComponent<BasePlate>() == null;
            SetTarget(single ? element : null);
        }

        private void SetTarget(KitchenElement element)
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
            bool show = _target.Movable;
            if (show && _handles.Count == 0) BuildHandles();
            else if (!show && _handles.Count > 0) { IsResizing = false; ClearHandles(); }

            if (_handles.Count > 0 && !IsResizing) PositionHandles();
        }

        // --- Ввод ---

        private void Update()
        {
            if (_target == null) return;

            if (IsResizing)
            {
                if (Input.GetMouseButton(0)) UpdateResize();
                if (Input.GetMouseButtonUp(0)) FinishResize();
                return;
            }

            if (!Input.GetMouseButtonDown(0)) return;
            if (AltHeld || PointerOverUI()) return;

            if (RaycastHandle(out var handle))
                BeginResize(handle.faceIndex);
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

        private bool RaycastHandle(out ResizeHandle handle)
        {
            handle = null;
            var cam = Camera.main;
            if (cam == null) return false;
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit)) return false;
            handle = hit.collider.GetComponentInParent<ResizeHandle>();
            return handle != null;
        }

        private void BeginResize(int faceIndex)
        {
            if (!_target.Movable) return; // запрет перемещения запрещает и ресайз
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
                _centerStart, _sizeStartUnits, rawDelta, BoardRegistry.GetAll(), _target,
                snapEnabled, threshold, out Vector3Int newDims, out Vector3 newCenter, out _);

            _target.DimensionsMM = newDims;
            _target.transform.position = newCenter;

            PositionHandles();
            if (ElementHighlighter.Instance != null) ElementHighlighter.Instance.RefreshHighlights();
        }

        private void FinishResize()
        {
            IsResizing = false;
            _resizingElement = null;

            var afterDims = _target.DimensionsMM;
            var afterPos = _target.transform.position;
            if (afterDims != _dimsBefore || afterPos != _posBefore)
            {
                CommandStack.Execute(new ResizeCommand(_target,
                    _dimsBefore, afterDims, _posBefore, afterPos, _rotBefore, _rotBefore));
            }
            PositionHandles();
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
            if (Mathf.Abs(denom) < 1e-4f) return float.NaN; // смотрим почти вдоль нормали
            Vector3 w0 = _faceCenter0 - ray.origin;
            float dW = Vector3.Dot(d, w0);
            float eW = Vector3.Dot(e, w0);
            return (b * eW - dW) / denom;
        }

        // --- Ручки ---

        private void BuildHandles()
        {
            var faces = _target.GetFaces();
            for (int i = 0; i < faces.Length; i++)
            {
                var go = new GameObject($"ResizeHandle_{i}");
                var marker = go.AddComponent<ResizeHandle>();
                marker.faceIndex = i;

                var col = go.AddComponent<BoxCollider>();
                col.center = new Vector3(0, 0, Gap + (ShaftLen + TipLen) * 0.5f);
                col.size = new Vector3(TipSize * 1.6f, TipSize * 1.6f, ShaftLen + TipLen + 0.04f);

                var mat = _axisMats[i / 2];
                BuildArrowVisual(go.transform, mat);
                _handles.Add(marker);
            }
            PositionHandles();
        }

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

            // Наконечник (Cube).
            var tip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            StripCollider(tip);
            tip.transform.SetParent(parent, false);
            tip.transform.localPosition = new Vector3(0, 0, Gap + ShaftLen + TipLen * 0.5f);
            tip.transform.localScale = new Vector3(TipSize, TipSize, TipLen);
            tip.GetComponent<MeshRenderer>().sharedMaterial = mat;
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
            foreach (var h in _handles)
            {
                if (h == null || h.faceIndex >= faces.Length) continue;
                var f = faces[h.faceIndex];
                Vector3 n = f.normal.sqrMagnitude > 1e-6f ? f.normal.normalized : Vector3.forward;
                Vector3 up = Mathf.Abs(Vector3.Dot(n, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
                h.transform.SetPositionAndRotation(f.center, Quaternion.LookRotation(n, up));
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
            return m;
        }
    }
}
