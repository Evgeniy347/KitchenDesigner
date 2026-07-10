using UnityEngine;

namespace KitchenDesigner.Core
{
    public class ElementMover : MonoBehaviour
    {
        public static bool IsDragging { get; private set; }

        private KitchenElement _target;
        private Vector3 _offset;
        private Vector3 _startPosition;
        private Vector3 _dragPlanePoint;
        private bool _wasMoved;

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
        }

        private void OnSelectionChanged(KitchenElement element)
        {
            if (IsDragging) return;
            _target = element;
            Debug.Log("[Mover] OnSelectionChanged: target=" + (element != null ? element.name : "null"));

            if (element != null && Input.GetMouseButton(0))
            {
                Debug.Log("[Mover] Mouse already held, trying immediate drag start");
                TryStartDrag();
            }
        }

        private bool TryStartDrag()
        {
            if (_target == null)
            {
                Debug.Log("[Mover] TryStartDrag: target is null, abort");
                return false;
            }

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            Debug.Log("[Mover] TryStartDrag: ray=" + ray.origin + " dir=" + ray.direction);

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Debug.Log("[Mover] TryStartDrag: hit " + hit.collider.gameObject.name + " at " + hit.point);
                var element = hit.collider.GetComponentInParent<KitchenElement>();
                if (element == _target)
                {
                    IsDragging = true;
                    _startPosition = _target.transform.position;
                    _wasMoved = false;

                    Plane dragPlane = new Plane(Vector3.up, _startPosition);
                    if (dragPlane.Raycast(ray, out float enter))
                    {
                        _dragPlanePoint = ray.GetPoint(enter);
                        _offset = _startPosition - _dragPlanePoint;
                        Debug.Log("[Mover] Drag START: startPos=" + _startPosition + " offset=" + _offset + " dragPlanePoint=" + _dragPlanePoint);
                    }
                    else
                    {
                        Debug.Log("[Mover] Drag plane raycast failed, using fallback");
                        _dragPlanePoint = _startPosition;
                        _offset = Vector3.zero;
                    }
                    return true;
                }
                else
                {
                    Debug.Log("[Mover] TryStartDrag: hit element != target (" + (element != null ? element.name : "null") + " vs " + (_target != null ? _target.name : "null") + ")");
                }
            }
            else
            {
                Debug.Log("[Mover] TryStartDrag: raycast missed");
            }
            return false;
        }

        private void Update()
        {
            if (_target == null)
            {
                if (Input.GetMouseButtonDown(0)) Debug.Log("[Mover] No target, ignoring LMB");
                return;
            }

            if (Input.GetMouseButtonDown(0) && !IsDragging)
            {
                Debug.Log("[Mover] LMB down with target " + _target.name + " at " + _target.transform.position);
                TryStartDrag();
            }

            if (IsDragging && Input.GetMouseButton(0))
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

                if (shiftHeld)
                {
                    Plane verticalPlane = new Plane(Vector3.right, _startPosition);
                    if (verticalPlane.Raycast(ray, out float enter))
                    {
                        Vector3 point = ray.GetPoint(enter) + _offset;
                        Vector3 newPos = new Vector3(_target.transform.position.x, point.y, _target.transform.position.z);
                        float newY = newPos.y;
                        newPos.y = GridManager.SnapToGrid(newPos).y;
                        _target.transform.position = newPos;
                        _wasMoved = true;
                        Debug.Log("[Mover] Drag Y: rawY=" + newY + " snappedY=" + newPos.y + " pos=" + _target.transform.position);
                    }
                }
                else
                {
                    Plane dragPlane = new Plane(Vector3.up, _startPosition);
                    if (dragPlane.Raycast(ray, out float enter))
                    {
                        Vector3 point = ray.GetPoint(enter) + _offset;
                        point.y = _startPosition.y;
                        Vector3 snapped = GridManager.SnapToGrid(point);
                        _target.transform.position = snapped;
                        _wasMoved = true;
                        Debug.Log("[Mover] Drag XZ: raw=" + point + " snapped=" + snapped + " pos=" + _target.transform.position);
                    }
                    else
                    {
                        Debug.Log("[Mover] Drag XZ: plane raycast failed");
                    }
                }
            }

            if (Input.GetKeyDown(KeyCode.Escape) && IsDragging)
            {
                Debug.Log("[Mover] ESCAPE: restoring position from " + _target.transform.position + " to " + _startPosition);
                _target.transform.position = _startPosition;
                IsDragging = false;
                _wasMoved = false;
            }

            if (IsDragging && Input.GetMouseButtonUp(0))
            {
                if (!_wasMoved)
                {
                    Debug.Log("[Mover] Drop: was not moved, restoring to start");
                    _target.transform.position = _startPosition;
                }
                else
                {
                    Debug.Log("[Mover] Drop: finalized at " + _target.transform.position);
                }
                IsDragging = false;
            }
        }
    }
}
