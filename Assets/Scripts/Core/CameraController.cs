using UnityEngine;

namespace KitchenDesigner.Core
{
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private float _distance = 5f;
        [SerializeField] private float _minDistance = 0.5f;
        [SerializeField] private float _maxDistance = 20f;
        [SerializeField] private float _zoomSpeed = 1f;
        [SerializeField] private float _orbitSpeed = 2f;
        [SerializeField] private float _panSpeed = 0.02f;

        private Vector3 _target = Vector3.zero;
        private Vector3 _actualTarget;
        private Vector3 _focusVelocity;
        private float _focusSmoothTime = 0.3f;
        private float _angleX = 30f;
        private float _angleY = 0f;
        private Vector3 _lastMouse;
        private bool _isOrbiting;
        private bool _isPanning;

        private void Start()
        {
            Debug.Log("[Camera] Start: distance=" + _distance + " angleX=" + _angleX + " angleY=" + _angleY);
            _actualTarget = _target;
            UpdateCameraPosition();
        }

        private void Update()
        {
            // Орбита: ПКМ по пустому месту (по доске ПКМ = контекстное меню).
            // Pan: ЛКМ по пустому месту или СКМ. ЛКМ по доске = перемещение доски.
            bool overUI = PointerOverUI();
            bool lmbDown = Input.GetMouseButtonDown(0);
            bool rmbDown = Input.GetMouseButtonDown(1);
            bool mmbDown = Input.GetMouseButtonDown(2);
            float scroll = Input.GetAxis("Mouse ScrollWheel");

            if (rmbDown && !overUI && !PointerHitsBoard())
            {
                _isOrbiting = true;
                _lastMouse = Input.mousePosition;
            }

            if (mmbDown || (lmbDown && !overUI && !PointerHitsBoard()))
            {
                _isPanning = true;
                _lastMouse = Input.mousePosition;
            }

            if (_isOrbiting && !Input.GetMouseButton(1)) _isOrbiting = false;
            if (_isPanning && !Input.GetMouseButton(0) && !Input.GetMouseButton(2)) _isPanning = false;

            if (_isOrbiting)
            {
                Vector3 delta = Input.mousePosition - _lastMouse;
                _angleY += delta.x * _orbitSpeed * 0.1f;
                _angleX -= delta.y * _orbitSpeed * 0.1f;
                _angleX = Mathf.Clamp(_angleX, -89f, 89f);
                _lastMouse = Input.mousePosition;
            }

            if (_isPanning)
            {
                Vector3 delta = Input.mousePosition - _lastMouse;
                Vector3 forward = Quaternion.Euler(_angleX, _angleY, 0) * Vector3.forward;
                Vector3 right = Quaternion.Euler(0, _angleY, 0) * Vector3.right;
                forward.y = 0; forward.Normalize();
                _target -= (right * delta.x + forward * delta.y) * _panSpeed * (_distance * 0.1f);
                _lastMouse = Input.mousePosition;
            }

            if (Mathf.Abs(scroll) > 0.01f)
            {
                _distance -= scroll * _zoomSpeed;
                _distance = Mathf.Clamp(_distance, _minDistance, _maxDistance);
                Debug.Log("[Camera] Zoom dist=" + _distance);
            }

            if (Input.GetKeyDown(KeyCode.Alpha1)) { Debug.Log("[Camera] View=Front"); SetView(0, 0); }
            else if (Input.GetKeyDown(KeyCode.Alpha2)) { Debug.Log("[Camera] View=Right"); SetView(0, 90); }
            else if (Input.GetKeyDown(KeyCode.Alpha3)) { Debug.Log("[Camera] View=Top"); SetView(90, 0); }

            if (Input.GetKeyDown(KeyCode.F))
            {
                Debug.Log("[Camera] F pressed");
                FocusOnSelection();
            }

            UpdateCameraPosition();
        }

        private static bool PointerOverUI()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) return false;
            return es.IsPointerOverGameObject() || es.IsPointerOverGameObject(0);
        }

        private static bool PointerHitsBoard()
        {
            var cam = Camera.main;
            if (cam == null) return false;
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
                return hit.collider.GetComponentInParent<KitchenElement>() != null;
            return false;
        }

        private void SetView(float angleX, float angleY)
        {
            _angleX = angleX;
            _angleY = angleY;
        }

        private void FocusOnSelection()
        {
            if (SelectionManager.Instance != null && SelectionManager.Instance.Selected != null)
            {
                _actualTarget = SelectionManager.Instance.Selected.transform.position;
                Debug.Log("[Camera] Focus on selected at " + _actualTarget);
            }
            else
            {
                Debug.Log("[Camera] Focus: nothing selected, keeping target " + _target);
            }
        }

        public void FocusOn(Vector3 point)
        {
            _actualTarget = point;
        }

        private void UpdateCameraPosition()
        {
            _target = Vector3.SmoothDamp(_target, _actualTarget, ref _focusVelocity, _focusSmoothTime);

            Quaternion rotation = Quaternion.Euler(_angleX, _angleY, 0);
            Vector3 offset = rotation * (Vector3.back * _distance);
            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = _target + offset;
                cam.transform.LookAt(_target);
            }
        }
    }
}
