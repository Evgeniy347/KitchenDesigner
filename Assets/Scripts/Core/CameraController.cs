using UnityEngine;

namespace KitchenDesigner.Core
{
    public class CameraController : MonoBehaviour
    {
        public static CameraController Instance { get; private set; }

        [SerializeField] private float _distance = 5f;
        [SerializeField] private float _minDistance = 0.5f;
        [SerializeField] private float _maxDistance = 20f;
        [SerializeField] private float _zoomSpeed = 1f;
        [SerializeField] private float _orbitSpeed = 2f;
        [SerializeField] private float _panSpeed = 0.02f;
        [SerializeField] private float _moveSpeed = 3f;
        [SerializeField] private float _keyboardOrbitSpeed = 90f;

        private Vector3 _target = Vector3.zero;
        private float _angleX = 30f;
        private float _angleY = 0f;
        private Vector3 _lastMouse;
        private bool _isOrbiting;
        private bool _isPanning;

        // ПКМ: клик (меню группы) vs перетаскивание (орбита) — различаем по сдвигу.
        private bool _rmbPressed;
        private Vector2 _rmbDownPos;
        private bool _rmbMoved;
        private const float RmbDragPixels = 6f;

        private Camera _cachedCamera;
        private GameObject _floor;

        private void Awake()
        {
            Instance = this;
            _cachedCamera = Camera.main;
            _floor = GameObject.FindWithTag("Floor");
        }

        private void Start()
        {
            UpdateCameraPosition();
        }

        /// <summary>Текущее состояние камеры (для сохранения в проект).</summary>
        public CameraState GetState() => new CameraState
        {
            valid = true,
            targetX = _target.x, targetY = _target.y, targetZ = _target.z,
            angleX = _angleX, angleY = _angleY, distance = _distance
        };

        /// <summary>Восстановить состояние камеры из проекта.</summary>
        public void SetState(CameraState s)
        {
            _target = new Vector3(s.targetX, s.targetY, s.targetZ);
            _angleX = s.angleX;
            _angleY = s.angleY;
            _distance = Mathf.Clamp(s.distance, _minDistance, _maxDistance);
            UpdateCameraPosition();
        }

        private void Update()
        {
            // Орбита: ПКМ по чему угодно (доска или пустота). Контекстное меню теперь по ЛКМ.
            // Pan: ЛКМ по пустому месту или СКМ. ЛКМ по доске = перемещение доски.
            bool overUI = PointerOverUI();
            bool lmbDown = Input.GetMouseButtonDown(0);
            bool rmbDown = Input.GetMouseButtonDown(1);
            bool rmbUp = Input.GetMouseButtonUp(1);
            bool mmbDown = Input.GetMouseButtonDown(2);
            float scroll = Input.GetAxis("Mouse ScrollWheel");

            // ПКМ-нажатие — пока не решено: клик (меню) или перетаскивание (орбита).
            if (rmbDown && !overUI)
            {
                _rmbPressed = true;
                _rmbMoved = false;
                _rmbDownPos = Input.mousePosition;
                _lastMouse = Input.mousePosition;
            }
            // Курсор сдвинулся — это орбита, а не клик.
            if (_rmbPressed && !_rmbMoved && Input.GetMouseButton(1) &&
                ((Vector2)Input.mousePosition - _rmbDownPos).magnitude > RmbDragPixels)
            {
                _rmbMoved = true;
                _isOrbiting = true;
                _lastMouse = Input.mousePosition;
            }
            // ПКМ отпущена без сдвига — открыть меню группы по объекту.
            if (rmbUp)
            {
                if (_rmbPressed && !_rmbMoved)
                    HandleRmbClick();
                _rmbPressed = false;
                _isOrbiting = false;
            }

            if (mmbDown || (lmbDown && !overUI && !PointerHitsBoard() && !ResizeHandleManager.PointerOverHandle()))
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
            }

            if (Input.GetKeyDown(KeyCode.Alpha1)) SetView(0, 0);
            else if (Input.GetKeyDown(KeyCode.Alpha2)) SetView(0, 90);
            else if (Input.GetKeyDown(KeyCode.Alpha3)) SetView(90, 0);

            if (Input.GetKeyDown(KeyCode.F))
                FocusOnSelection();

            if (Input.GetKeyDown(KeyCode.F1) && UI.UIManager.Instance != null)
                UI.UIManager.Instance.ToggleHelp();

            HandleWASD();
            HandleArrowOrbit();
            HandlePlusMinusZoom();

            UpdateCameraPosition();
            UpdateFloorVisibility();
        }

        internal void UpdateFloorVisibility()
        {
            if (_cachedCamera == null) return;
            if (_floor == null) return;

            var renderer = _floor.GetComponent<MeshRenderer>();
            if (renderer == null) return;

            float floorTopY = _floor.transform.position.y + _floor.transform.localScale.y * 0.5f;
            renderer.enabled = _cachedCamera.transform.position.y > floorTopY;
        }

        private void HandleWASD()
        {
            float dt = Time.deltaTime;
            if (dt < 1e-6f) return;
            float speed = _moveSpeed * _distance * 0.5f * dt;

            Vector3 fwd = Quaternion.Euler(0, _angleY, 0) * Vector3.forward;
            Vector3 right = Quaternion.Euler(0, _angleY, 0) * Vector3.right;

            if (Input.GetKey(KeyCode.W)) _target += fwd * speed;
            if (Input.GetKey(KeyCode.S)) _target -= fwd * speed;
            if (Input.GetKey(KeyCode.A)) _target -= right * speed;
            if (Input.GetKey(KeyCode.D)) _target += right * speed;
        }

        private void HandleArrowOrbit()
        {
            float dt = Time.deltaTime;
            if (dt < 1e-6f) return;
            float speed = _keyboardOrbitSpeed * dt;

            if (Input.GetKey(KeyCode.LeftArrow)) _angleY -= speed;
            if (Input.GetKey(KeyCode.RightArrow)) _angleY += speed;
            if (Input.GetKey(KeyCode.UpArrow)) _angleX += speed;
            if (Input.GetKey(KeyCode.DownArrow)) _angleX -= speed;
            _angleX = Mathf.Clamp(_angleX, -89f, 89f);
        }

        private void HandlePlusMinusZoom()
        {
            float delta = 0f;
            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus))
                delta = -1f;
            else if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
                delta = 1f;
            if (Mathf.Abs(delta) > 0.01f)
            {
                _distance += delta * _zoomSpeed * _distance * 0.2f;
                _distance = Mathf.Clamp(_distance, _minDistance, _maxDistance);
            }
        }

        private static bool PointerOverUI()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) return false;
            return es.IsPointerOverGameObject() || es.IsPointerOverGameObject(0);
        }

        private bool PointerHitsBoard()
        {
            if (_cachedCamera == null) return false;
            Ray ray = _cachedCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
                return hit.collider.GetComponentInParent<KitchenElement>() != null;
            return false;
        }

        // ПКМ-клик по объекту (не пол): обычный одиночный объект → окно его настроек;
        // связанная группа или мультивыделение → меню группы (связать / настройки группы).
        private void HandleRmbClick()
        {
            if (_cachedCamera == null) return;
            Ray ray = _cachedCamera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit)) return;
            var e = hit.collider.GetComponentInParent<KitchenElement>();
            if (e == null || e.GetComponent<BasePlate>() != null) return;
            if (UI.UIManager.Instance == null) return;

            var sel = SelectionManager.Instance;
            bool grouped = GroupManager.GroupOf(e) != null;
            bool multi = sel != null && sel.SelectedElements.Count >= 2 && sel.IsSelected(e);
            if (grouped || multi)
                UI.UIManager.Instance.OpenGroupMenu(e);
            else
                UI.UIManager.Instance.OpenContextMenu(e);
        }

        private void SetView(float angleX, float angleY)
        {
            _angleX = angleX;
            _angleY = angleY;
        }

        private void FocusOnSelection()
        {
            if (SelectionManager.Instance != null && SelectionManager.Instance.Selected != null)
                _target = SelectionManager.Instance.Selected.transform.position;
        }

        public void FocusOn(Vector3 point)
        {
            _target = point;
        }

        internal void UpdateCameraPosition()
        {
            Quaternion rotation = Quaternion.Euler(_angleX, _angleY, 0);
            Vector3 offset = rotation * (Vector3.back * _distance);
            if (_cachedCamera != null)
            {
                _cachedCamera.transform.position = _target + offset;
                _cachedCamera.transform.LookAt(_target);
            }
        }
    }
}
