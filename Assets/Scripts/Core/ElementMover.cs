using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class ElementMover : MonoBehaviour
    {
        public static bool IsDragging { get; private set; }

        private KitchenElement _target;
        private Vector3 _offset;
        private Vector3 _startPosition;
        private bool _wasMoved;
        private bool _wasShift;
        private float _vOffset;

        private Material _dragOriginalMaterial;
        private Material _dragTintMaterial;

        private void Start()
        {
            var sel = GetComponent<SelectionManager>();
            if (sel != null)
                sel.OnSelectionChanged += OnSelectionChanged;
            else
                Debug.LogError("[Mover] No SelectionManager found on same GameObject");
        }

        private void OnSelectionChanged(KitchenElement element)
        {
            if (IsDragging) return;
            _target = element;
            if (element != null && Input.GetMouseButton(0))
                TryStartDrag();
        }

        private bool AltHeld => Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        private bool ShiftHeld => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        private static bool PointerOverUI =>
            UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

        private bool TryStartDrag()
        {
            if (_target == null) return false;
            if (AltHeld || PointerOverUI) return false; // Alt+ЛКМ — орбита; клик по UI — не drag

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                var element = hit.collider.GetComponentInParent<KitchenElement>();
                if (element == _target)
                {
                    IsDragging = true;
                    Debug.Log("[Mover] Начато перемещение: " + _target.Describe());
                    _startPosition = _target.transform.position;
                    _wasMoved = false;
                    _wasShift = false;

                    Plane dragPlane = new Plane(Vector3.up, _startPosition);
                    _offset = dragPlane.Raycast(ray, out float enter)
                        ? _startPosition - ray.GetPoint(enter)
                        : Vector3.zero;
                    return true;
                }
            }
            return false;
        }

        private void Update()
        {
            HandleDuplicate();
            HandleArrowKeys();
            HandleDragInput();
        }

        private void HandleDuplicate()
        {
            if (Input.GetKeyDown(KeyCode.D) && !IsDragging && _target != null &&
                (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
            {
                var dup = ElementFactory.Duplicate(_target);
                var newElement = dup != null ? dup.GetComponent<KitchenElement>() : null;
                if (newElement != null && SelectionManager.Instance != null)
                    SelectionManager.Instance.Select(newElement);
            }
        }

        // Сдвиг выделенной доски стрелками на шаг сетки (или 1мм). Shift → по вертикали.
        private void HandleArrowKeys()
        {
            if (_target == null || IsDragging) return;

            var s = KitchenSettings.Instance;
            float stepMM = (s != null && s.GridEnabled) ? s.GridStep : 1f;
            float step = stepMM * AppConstants.MM_TO_UNITS;

            Vector3 d = Vector3.zero;
            if (Input.GetKeyDown(KeyCode.LeftArrow)) d.x -= step;
            if (Input.GetKeyDown(KeyCode.RightArrow)) d.x += step;
            if (ShiftHeld)
            {
                if (Input.GetKeyDown(KeyCode.UpArrow)) d.y += step;
                if (Input.GetKeyDown(KeyCode.DownArrow)) d.y -= step;
            }
            else
            {
                if (Input.GetKeyDown(KeyCode.UpArrow)) d.z += step;
                if (Input.GetKeyDown(KeyCode.DownArrow)) d.z -= step;
            }

            if (d == Vector3.zero) return;

            Vector3 prev = _target.transform.position;
            _target.transform.position = GridManager.SnapToGrid(prev + d);

            if (KitchenSettings.Instance.BlockOnViolation && MovedCausesViolation())
                _target.transform.position = prev;

            RefreshHighlights();
        }

        private void HandleDragInput()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && IsDragging)
            {
                _target.transform.position = _startPosition;
                RestoreDragMaterial();
                IsDragging = false;
                _wasMoved = false;
                RefreshHighlights();
                return;
            }

            if (_target == null) return;

            if (Input.GetMouseButtonDown(0) && !IsDragging)
                TryStartDrag();

            if (IsDragging && Input.GetMouseButton(0))
                UpdateDrag();

            if (IsDragging && Input.GetMouseButtonUp(0))
                FinishDrag();
        }

        private void UpdateDrag()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            Vector3 newPos = _target.transform.position;
            bool computed = false;

            if (ShiftHeld)
            {
                // Плоскость, обращённая к камере и содержащая мировую вертикаль —
                // иначе при взгляде вдоль оси перемещение «убегает» от мыши.
                Vector3 viewDir = Camera.main.transform.forward;
                viewDir.y = 0f;
                if (viewDir.sqrMagnitude < 1e-4f) viewDir = Vector3.forward;
                viewDir.Normalize();

                if (!_wasShift)
                {
                    var capPlane = new Plane(viewDir, _target.transform.position);
                    _vOffset = capPlane.Raycast(ray, out float e0)
                        ? _target.transform.position.y - ray.GetPoint(e0).y
                        : 0f;
                    _wasShift = true;
                }

                var vPlane = new Plane(viewDir, _target.transform.position);
                if (vPlane.Raycast(ray, out float enter))
                {
                    float y = ray.GetPoint(enter).y + _vOffset;
                    Vector3 t = new Vector3(_target.transform.position.x, y, _target.transform.position.z);
                    newPos = new Vector3(t.x, GridManager.SnapToGrid(t).y, t.z);
                    computed = true;
                }
            }
            else
            {
                _wasShift = false;
                var dragPlane = new Plane(Vector3.up, _target.transform.position);
                if (dragPlane.Raycast(ray, out float enter))
                {
                    Vector3 point = ray.GetPoint(enter) + _offset;
                    point.y = _target.transform.position.y;
                    newPos = GridManager.SnapToGrid(point);
                    computed = true;
                }
            }

            if (!computed) return;

            if (_dragTintMaterial == null) SaveDragMaterial();
            _wasMoved = true;

            // Прилипание во время перетаскивания: доска «липнет» к снэп-позиции.
            var others = new List<KitchenElement>(FindObjectsByType<KitchenElement>());
            var snap = SnapSystem.TrySnap(_target, others, newPos);
            _target.transform.position = snap.snapped ? snap.position : newPos;

            UpdateDragTint();
        }

        private void FinishDrag()
        {
            if (_wasMoved)
            {
                if (KitchenSettings.Instance.BlockOnViolation && MovedCausesViolation())
                {
                    Debug.Log("[Mover] Заблокировано (нарушение): " + _target.Describe() + " → откат");
                    _target.transform.position = _startPosition;
                }
                else
                {
                    Debug.Log("[Mover] Размещено: " + _target.Describe());
                }
            }
            else
            {
                _target.transform.position = _startPosition;
            }

            RestoreDragMaterial();
            IsDragging = false;
            _wasShift = false;
            RefreshHighlights();
        }

        // Блокировка должна смотреть на саму перемещаемую доску, а не на всю сцену:
        // иначе любая чужая ошибка мешала бы двигать валидную доску.
        private bool MovedCausesViolation()
        {
            var list = new List<KitchenElement>(FindObjectsByType<KitchenElement>());
            var result = ConstraintValidator.Validate(list);
            return result.violations.Contains(_target);
        }

        private static void RefreshHighlights()
        {
            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();
        }

        private void SaveDragMaterial()
        {
            var renderer = _target.GetComponent<MeshRenderer>();
            if (renderer == null) return;

            _dragOriginalMaterial = renderer.material;
            _dragTintMaterial = new Material(_dragOriginalMaterial);
            _dragTintMaterial.SetFloat("_Surface", 1);
            _dragTintMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _dragTintMaterial.renderQueue = 3000;
            _dragTintMaterial.color = new Color(0f, 1f, 0f, 0.3f);
            renderer.material = _dragTintMaterial;
        }

        private void UpdateDragTint()
        {
            if (_dragTintMaterial == null || _target == null) return;

            bool overlaps = false;
            foreach (var other in FindObjectsByType<KitchenElement>())
            {
                if (other == _target || other == null) continue;
                if (SnapSystem.ElementsIntersect(_target, other)) { overlaps = true; break; }
            }

            _dragTintMaterial.color = overlaps
                ? new Color(1f, 0f, 0f, 0.3f)
                : new Color(0f, 1f, 0f, 0.3f);
        }

        private void RestoreDragMaterial()
        {
            var renderer = _target != null ? _target.GetComponent<MeshRenderer>() : null;
            if (renderer != null && _dragOriginalMaterial != null)
                renderer.material = _dragOriginalMaterial;

            if (_dragTintMaterial != null)
            {
                Destroy(_dragTintMaterial);
                _dragTintMaterial = null;
            }
            _dragOriginalMaterial = null;
        }
    }
}
