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
        private float _angleX = 30f;
        private float _angleY = 0f;
        private Vector3 _lastMouse;
        private bool _isOrbiting;
        private bool _isPanning;

        private void Start()
        {
            Debug.Log("[Camera] Start: distance=" + _distance + " angleX=" + _angleX + " angleY=" + _angleY);
            UpdateCameraPosition();
        }

        private void Update()
        {
            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            // Орбита: Alt+ЛКМ (ПКМ свободна под контекстное меню). Pan: Alt+СКМ или СКМ.
            bool orbitDown = alt && Input.GetMouseButtonDown(0);
            bool orbitHeld = alt && Input.GetMouseButton(0);
            bool mmbDown = Input.GetMouseButtonDown(2);
            bool mmbUp = Input.GetMouseButtonUp(2);
            float scroll = Input.GetAxis("Mouse ScrollWheel");

            if (orbitDown)
            {
                _isOrbiting = true;
                _lastMouse = Input.mousePosition;
                Debug.Log("[Camera] Orbit START");
            }
            if (mmbDown)
            {
                _isPanning = true;
                _lastMouse = Input.mousePosition;
                Debug.Log("[Camera] Pan START");
            }

            if ((!orbitHeld || Input.GetMouseButtonUp(0)) && _isOrbiting) { Debug.Log("[Camera] Orbit STOP"); _isOrbiting = false; }
            if (mmbUp && _isPanning) { Debug.Log("[Camera] Pan STOP"); _isPanning = false; }

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

        private void SetView(float angleX, float angleY)
        {
            _angleX = angleX;
            _angleY = angleY;
        }

        private void FocusOnSelection()
        {
            if (SelectionManager.Instance != null && SelectionManager.Instance.Selected != null)
            {
                _target = SelectionManager.Instance.Selected.transform.position;
                Debug.Log("[Camera] Focus on selected at " + _target);
            }
            else
            {
                Debug.Log("[Camera] Focus: nothing selected, keeping target " + _target);
            }
        }

        public void FocusOn(Vector3 point)
        {
            _target = point;
        }

        private void UpdateCameraPosition()
        {
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
