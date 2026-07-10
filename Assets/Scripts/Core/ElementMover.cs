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
        private SnapVisualizer _snapVisualizer;
        private SnapResult _previewSnap;
        private bool _wasSnapPreviewed;

        private Material _dragOriginalMaterial;
        private Material _dragTintMaterial;

        private void Start()
        {
            var sel = GetComponent<SelectionManager>();
            if (sel != null)
            {
                sel.OnSelectionChanged += OnSelectionChanged;
                Debug.Log("[Mover] Subscribed to SelectionManager");
            }
            else
            {
                Debug.LogError("[Mover] No SelectionManager found on same GameObject");
            }

            var go = new GameObject("SnapVisualizer");
            go.transform.SetParent(transform);
            _snapVisualizer = go.AddComponent<SnapVisualizer>();
            Debug.Log("[Mover] SnapVisualizer created");
        }

        private void OnSelectionChanged(KitchenElement element)
        {
            if (IsDragging) return;
            _target = element;
            if (element != null && Input.GetMouseButton(0))
                TryStartDrag();
        }

        private bool TryStartDrag()
        {
            if (_target == null) return false;

            // Alt+ЛКМ — орбита камеры; клик по UI — не перетаскивание.
            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
                return false;
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                return false;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                var element = hit.collider.GetComponentInParent<KitchenElement>();
                if (element == _target)
                {
                    IsDragging = true;
                    _startPosition = _target.transform.position;
                    _wasMoved = false;
                    _wasSnapPreviewed = false;

                    Plane dragPlane = new Plane(Vector3.up, _startPosition);
                    if (dragPlane.Raycast(ray, out float enter))
                    {
                        _offset = _startPosition - ray.GetPoint(enter);
                        Debug.Log("[Mover] Drag START at " + _startPosition + " offset=" + _offset);
                    }
                    else
                    {
                        _offset = Vector3.zero;
                        Debug.Log("[Mover] Drag START fallback at " + _startPosition);
                    }
                    return true;
                }
            }
            return false;
        }

        private void PreviewSnap(Vector3 position)
        {
            var allElements = FindObjectsByType<KitchenElement>();
            var others = new List<KitchenElement>(allElements);
            _previewSnap = SnapSystem.TrySnap(_target, others, position);

            if (_previewSnap.snapped)
            {
                if (!_wasSnapPreviewed)
                {
                    Debug.Log("[Snap] PREVIEW to " + _previewSnap.targetName + " gap=" +
                        Vector3.Distance(position, _previewSnap.position).ToString("F3") + "m");
                    _wasSnapPreviewed = true;
                }
                if (_previewSnap.targetName != "BasePlate")
                    _snapVisualizer.ShowProximity(_previewSnap.snapPoint, _previewSnap.targetPoint);
            }
            else
            {
                if (_wasSnapPreviewed)
                {
                    Debug.Log("[Snap] PREVIEW lost");
                    _wasSnapPreviewed = false;
                }
                _snapVisualizer.Hide();
            }
        }

        private void ApplySnapOnDrop()
        {
            Vector3 currentPos = _target.transform.position;
            var allElements = FindObjectsByType<KitchenElement>();
            var others = new List<KitchenElement>(allElements);
            var result = SnapSystem.TrySnap(_target, others, currentPos);

            if (result.snapped)
            {
                Debug.Log("[Snap] ATTACH to " + result.targetName + " at " + result.position);
                _target.transform.position = result.position;
                if (result.targetName != "BasePlate")
                    _snapVisualizer.ShowSnap(result.snapPoint, result.targetPoint);
            }
        }

        private void Update()
        {
            if (_target == null)
            {
                if (Input.GetMouseButtonDown(0))
                    Debug.Log("[Mover] LMB: no target");
                return;
            }

            if (Input.GetMouseButtonDown(0) && !IsDragging)
            {
                Debug.Log("[Mover] LMB on " + _target.name + " at " + _target.transform.position);
                TryStartDrag();
            }

            if (Input.GetKeyDown(KeyCode.D) && !IsDragging && _target != null)
            {
                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                {
                    var dup = ElementFactory.Duplicate(_target);
                    if (dup != null)
                    {
                        var newElement = dup.GetComponent<KitchenElement>();
                        if (newElement != null && SelectionManager.Instance != null)
                            SelectionManager.Instance.Select(newElement);
                        Debug.Log("[Mover] Ctrl+D duplicate " + _target.name + " -> " + dup.name);
                    }
                }
            }

            if (Input.GetKeyDown(KeyCode.Escape) && IsDragging)
            {
                Debug.Log("[Mover] ESC cancel drag");
                _target.transform.position = _startPosition;
                RestoreDragMaterial();
                IsDragging = false;
                _wasMoved = false;
                _snapVisualizer.Hide();
                _wasSnapPreviewed = false;
                if (ElementHighlighter.Instance != null)
                    ElementHighlighter.Instance.RefreshHighlights();
            }

            if (IsDragging && Input.GetMouseButton(0))
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

                Vector3 newPos = _target.transform.position;
                bool positionComputed = false;

                if (shiftHeld)
                {
                    Plane verticalPlane = new Plane(Vector3.right, _startPosition);
                    if (verticalPlane.Raycast(ray, out float enter))
                    {
                        Vector3 point = ray.GetPoint(enter) + _offset;
                        Vector3 tempPos = new Vector3(_target.transform.position.x, point.y, _target.transform.position.z);
                        tempPos.y = GridManager.SnapToGrid(tempPos).y;
                        newPos = tempPos;
                        positionComputed = true;
                    }
                }
                else
                {
                    Plane dragPlane = new Plane(Vector3.up, _startPosition);
                    if (dragPlane.Raycast(ray, out float enter))
                    {
                        Vector3 point = ray.GetPoint(enter) + _offset;
                        point.y = _startPosition.y;
                        newPos = GridManager.SnapToGrid(point);
                        positionComputed = true;
                    }
                }

                if (positionComputed)
                {
                    if (_dragTintMaterial == null)
                        SaveDragMaterial();
                    _wasMoved = true;
                    _target.transform.position = newPos;
                    PreviewSnap(newPos);
                    UpdateDragTint();
                }
            }

            if (IsDragging && Input.GetMouseButtonUp(0))
            {
                if (_wasMoved)
                {
                    ApplySnapOnDrop();

                    if (KitchenSettings.Instance.BlockOnViolation)
                    {
                        var all = FindObjectsByType<KitchenElement>();
                        var list = new List<KitchenElement>(all);
                        var valResult = ConstraintValidator.Validate(list);
                        if (!valResult.isValid)
                        {
                            Debug.Log("[Mover] BLOCKED: position causes violation, restoring");
                            _target.transform.position = _startPosition;
                            if (ElementHighlighter.Instance != null)
                                ElementHighlighter.Instance.RefreshHighlights();
                            RestoreDragMaterial();
                            IsDragging = false;
                            _snapVisualizer.Hide();
                            _wasSnapPreviewed = false;
                            return;
                        }
                    }

                    Debug.Log("[Mover] Drop at " + _target.transform.position);
                }
                else
                {
                    _target.transform.position = _startPosition;
                    Debug.Log("[Mover] Drop: no move, restored to " + _startPosition);
                }
                RestoreDragMaterial();
                IsDragging = false;
                _snapVisualizer.Hide();
                _wasSnapPreviewed = false;
                if (ElementHighlighter.Instance != null)
                    ElementHighlighter.Instance.RefreshHighlights();
            }
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

            var allElements = FindObjectsByType<KitchenElement>();
            bool overlaps = false;
            foreach (var other in allElements)
            {
                if (other == _target || other == null) continue;
                if (SnapSystem.ElementsIntersect(_target, other))
                {
                    overlaps = true;
                    break;
                }
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
