using TMPro;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class CameraController : MonoBehaviour
    {
        public static CameraController? Instance { get; private set; }

        [SerializeField] private float _distance = 5f;
        [SerializeField] private float _photoDistance = 8f;
        private Vector3 _photoTarget = Vector3.zero;
        private float _photoAngleX = 30f;
        private float _photoAngleY = 0f;
        [SerializeField] private float _minDistance = 0.5f;
        [SerializeField] private float _maxDistance = 20f;
        [SerializeField] private float _zoomSpeed = 1f;
        [SerializeField] private float _orbitSpeed = 2f;
        [SerializeField] private float _panSpeed = 0.02f;
        [SerializeField] private float _moveSpeed = 0.6f;
        [SerializeField] private float _keyboardOrbitSpeed = 45f;
        [SerializeField] private float _zoomStep = 0.3f;

        public const float WasdShiftRampSeconds = 1f;
        public const float WasdMaxMultiplier = 20f;
        private float _wasdHeld;

        public const float ScrollNotchesPerAxisUnit = 10f;
        private float _scrollTargetMetres;
        private float _scrollCurrentMetres;
        private float _scrollSmoothVelocity;
        private const float ScrollSmoothTime = 0.08f;

        public const float FocusSeconds = 2f;
        private const float NoFocus = -1f;
        private Vector3 _focusFrom;
        private Vector3 _focusTo;
        private float _focusTime = NoFocus;

        private Vector3 _target = Vector3.zero;
        private float _angleX = 30f;
        private float _angleY = 0f;
        private Vector3 _lastMouse;
        private bool _isOrbiting;
        private bool _isPanning;

        private bool _rmbPressed;
        private Vector2 _rmbDownPos;
        private bool _rmbMoved;
        private const float RmbDragPixels = 6f;

        public const float MinPitchDeg = -89f;
        public const float MaxPitchDeg = 89f;

        private Camera? _cachedCamera;
        private GameObject? _floor;

        public void AssignTestCamera(Camera camera) => _cachedCamera = camera;
        public void AssignTestFloor(GameObject floor) => _floor = floor;

        private float Dist
        {
            get => PhotoMode.Active ? _photoDistance : _distance;
            set
            {
                float c = Mathf.Clamp(value, _minDistance, _maxDistance);
                if (PhotoMode.Active) _photoDistance = c; else _distance = c;
            }
        }

        private Vector3 CurrTarget
        {
            get => PhotoMode.Active ? _photoTarget : _target;
            set
            {
                if (PhotoMode.Active) _photoTarget = value; else _target = value;
            }
        }

        private float CurrAngleX
        {
            get => PhotoMode.Active ? _photoAngleX : _angleX;
            set
            {
                if (PhotoMode.Active) _photoAngleX = value; else _angleX = value;
            }
        }

        private float CurrAngleY
        {
            get => PhotoMode.Active ? _photoAngleY : _angleY;
            set
            {
                if (PhotoMode.Active) _photoAngleY = value; else _angleY = value;
            }
        }

        private static float MouseSensitivity => KitchenSettings.Instance.MouseSensitivity;

        private static float WasdSpeed => KitchenSettings.Instance.WasdSpeed;

        private static float ArrowSpeed => KitchenSettings.Instance.ArrowSpeed;

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

        public CameraState GetState() => new CameraState
        {
            valid = true,
            targetX = _target.x, targetY = _target.y, targetZ = _target.z,
            angleX = _angleX, angleY = _angleY, distance = _distance,
            photoDistance = _photoDistance,
            photoTargetX = _photoTarget.x, photoTargetY = _photoTarget.y, photoTargetZ = _photoTarget.z,
            photoAngleX = _photoAngleX, photoAngleY = _photoAngleY
        };

        public static bool HasSavedPhotoTarget(CameraState s) =>
            s.photoTargetX != 0f || s.photoTargetY != 0f || s.photoTargetZ != 0f;

        public static bool HasSavedPhotoAngles(CameraState s) =>
            s.photoAngleX != 0f || s.photoAngleY != 0f;

        public void SetState(CameraState s)
        {
            CancelFocus();
            _target = new Vector3(s.targetX, s.targetY, s.targetZ);
            _angleX = s.angleX;
            _angleY = s.angleY;
            _distance = Mathf.Clamp(s.distance, _minDistance, _maxDistance);
            if (s.photoDistance > 0f)
                _photoDistance = Mathf.Clamp(s.photoDistance, _minDistance, _maxDistance);

            _photoTarget = HasSavedPhotoTarget(s)
                ? new Vector3(s.photoTargetX, s.photoTargetY, s.photoTargetZ)
                : _target;
            _photoAngleX = HasSavedPhotoAngles(s) ? s.photoAngleX : _angleX;
            _photoAngleY = HasSavedPhotoAngles(s) ? s.photoAngleY : _angleY;
            UpdateCameraPosition();
        }

        private void Update()
        {
            using var _ = PerfMarkers.CameraUpdate.Auto();
            bool overUI = PointerOverUI();
            float scroll = Input.GetAxis("Mouse ScrollWheel");

            if (!PlacementController.IsActive) TrackRightMouseButton(overUI);

            TrackPanStart(overUI);
            DropDragStatesWhenButtonsAreReleased();

            if (_isOrbiting) OrbitByMouse();
            if (_isPanning) PanByMouse();

            if (Mathf.Abs(scroll) > 0.01f && !overUI)
                ApplyScrollInput(scroll * ScrollNotchesPerAxisUnit
                    * _zoomStep * _zoomSpeed);

            if (!IsTypingInInputField()) HandleKeyboard();

            UpdateFocus(Time.deltaTime);
            UpdateCameraPosition();
            UpdateFloorVisibility();
        }

        private void TrackRightMouseButton(bool overUI)
        {
            if (Input.GetMouseButtonDown(1) && !overUI)
            {
                _rmbPressed = true;
                _rmbMoved = false;
                _rmbDownPos = Input.mousePosition;
                _lastMouse = Input.mousePosition;
            }

            if (_rmbPressed && !_rmbMoved && Input.GetMouseButton(1) &&
                ((Vector2)Input.mousePosition - _rmbDownPos).magnitude > RmbDragPixels)
            {
                _rmbMoved = true;
                _isOrbiting = true;
                _lastMouse = Input.mousePosition;
            }

            if (Input.GetMouseButtonUp(1))
            {
                if (_rmbPressed && !_rmbMoved)
                    HandleRmbClick();
                _rmbPressed = false;
                _isOrbiting = false;
            }
        }

        private void TrackPanStart(bool overUI)
        {
            bool middleButton = Input.GetMouseButtonDown(2);
            bool leftButtonOnEmptySpace = Input.GetMouseButtonDown(0) && !overUI
                && !PointerHitsBoard()
                && !ResizeHandleManager.PointerOverHandle()
                && !TextureOverlayHandles.PointerOverHandle();

            if (middleButton || leftButtonOnEmptySpace)
            {
                _isPanning = true;
                _lastMouse = Input.mousePosition;
            }
        }

        private void DropDragStatesWhenButtonsAreReleased()
        {
            if (_isOrbiting && !Input.GetMouseButton(1)) _isOrbiting = false;
            if (_isPanning && !Input.GetMouseButton(0) && !Input.GetMouseButton(2)) _isPanning = false;
        }

        private void OrbitByMouse()
        {
            Vector3 delta = Input.mousePosition - _lastMouse;
            float sens = MouseSensitivity;
            ApplyOrbit(delta.x * _orbitSpeed * 0.1f * sens,
                      -delta.y * _orbitSpeed * 0.1f * sens);
            _lastMouse = Input.mousePosition;
        }

        private void PanByMouse()
        {
            CancelFocus();
            Vector3 delta = Input.mousePosition - _lastMouse;
            float pan = _panSpeed * (Dist * 0.1f) * MouseSensitivity;
            if (KitchenSettings.Instance.CameraPanFree)
            {
                Vector3 right = Quaternion.Euler(CurrAngleX, CurrAngleY, 0) * Vector3.right;
                Vector3 up = Quaternion.Euler(CurrAngleX, CurrAngleY, 0) * Vector3.up;
                CurrTarget -= (right * delta.x + up * delta.y) * pan;
            }
            else
            {
                Vector3 forward = Quaternion.Euler(CurrAngleX, CurrAngleY, 0) * Vector3.forward;
                Vector3 right = Quaternion.Euler(0, CurrAngleY, 0) * Vector3.right;
                forward.y = 0; forward.Normalize();
                CurrTarget -= (right * delta.x + forward * delta.y) * pan;
            }
            _lastMouse = Input.mousePosition;
        }

        private void HandleKeyboard()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) SetView(0, 0);
            else if (Input.GetKeyDown(KeyCode.Alpha2)) SetView(0, 90);
            else if (Input.GetKeyDown(KeyCode.Alpha3)) SetView(90, 0);

            if (Input.GetKeyDown(KeyCode.F))
                FocusOnSelection();

            if (Input.GetKeyDown(KeyCode.F1) && UI.UIManager.Instance != null)
                UI.UIManager.Instance.ToggleHelp();

            if (Input.GetKeyDown(KeyCode.F10))
                PhotoMode.Toggle();

            if (Input.GetKeyDown(KeyCode.E))
                ToggleSelectedOpenables();

            HandleWASD();
            HandleArrowOrbit();
            HandlePlusMinusZoom();
            UpdateScrollSmooth(Time.deltaTime);
        }

        public void ToggleSelectedOpenables()
        {
            var sel = SelectionManager.Instance;
            if (sel == null) return;
            foreach (var el in sel.SelectedElements)
                if (el is IOpenable openable)
                    openable.CycleOpenState();

            UI.ContextMenuUI.Instance?.SyncOpenLabels();
        }

        public void UpdateFloorVisibility()
        {
            using var _ = PerfMarkers.CameraFloorVisibility.Auto();
            if (_cachedCamera == null) return;

            var floors = FloorElement.Active;

            if (_floor != null)
                ApplyFloorCameraHide(_floor, rendererVisible: BasePlateVisibleWith(floors.Count));

            for (int i = 0; i < floors.Count; i++)
            {
                var f = floors[i];
                if (f != null) ApplyFloorCameraHide(f.gameObject, rendererVisible: true);
            }
        }

        public static bool BasePlateVisibleWith(int userFloorCount) => userFloorCount == 0;

        public static bool FloorHiddenFromCamera(bool cameraBelowTop, bool lookingUp) =>
            !PhotoMode.Active && cameraBelowTop && lookingUp;

        private void ApplyFloorCameraHide(GameObject floor, bool rendererVisible)
        {
            var renderer = floor.GetComponent<MeshRenderer>();
            if (renderer == null) return;

            float topY = floor.transform.position.y + floor.transform.localScale.y * 0.5f;
            bool cameraBelow = _cachedCamera!.transform.position.y < topY;
            bool lookingUp = _cachedCamera.transform.forward.y > 0f;
            bool hide = FloorHiddenFromCamera(cameraBelow, lookingUp);

            renderer.enabled = rendererVisible && !hide;

            var mesh = floor.GetComponent<MeshCollider>();
            Collider? collider = mesh != null ? mesh : floor.GetComponent<Collider>();
            if (collider != null)
                collider.enabled = !hide;
        }

        private void HandleWASD()
        {
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) return;
            float dt = Time.deltaTime;
            Vector2 input = new Vector2(
                (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f),
                (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f));
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            ApplyWASDMovement(input, dt, shift);
        }

        public static float WasdHoldMultiplier(float heldSeconds, bool shift)
        {
            if (!shift) return 1f;
            float t = Mathf.Clamp01(heldSeconds / WasdShiftRampSeconds);
            return Mathf.Lerp(1f, WasdMaxMultiplier, t);
        }

        public void ApplyWASDMovement(Vector2 input, float dt, bool shift = false)
        {
            if (dt < 1e-6f) return;
            if (input.sqrMagnitude < 1e-6f || !shift)
            {
                _wasdHeld = 0f;
                if (input.sqrMagnitude < 1e-6f) return;
            }

            CancelFocus();
            float speed = _moveSpeed
                * WasdHoldMultiplier(_wasdHeld, shift) * dt * WasdSpeed;
            _wasdHeld += dt;

            Vector3 fwd = Quaternion.Euler(0, CurrAngleY, 0) * Vector3.forward;
            Vector3 right = Quaternion.Euler(0, CurrAngleY, 0) * Vector3.right;

            CurrTarget += fwd * (input.y * speed);
            CurrTarget += right * (input.x * speed);
        }

        public void ApplyOrbit(float deltaYaw, float deltaPitch)
        {
            CancelFocus();
            Vector3 camPos = CurrTarget + Quaternion.Euler(CurrAngleX, CurrAngleY, 0) * (Vector3.back * Dist);
            CurrAngleY += deltaYaw;
            CurrAngleX = Mathf.Clamp(CurrAngleX + deltaPitch, MinPitchDeg, MaxPitchDeg);
            CurrTarget = camPos + Quaternion.Euler(CurrAngleX, CurrAngleY, 0) * (Vector3.forward * Dist);
        }

        public void MoveForward(float metres)
        {
            if (Mathf.Abs(metres) < 1e-6f) return;
            CancelFocus();
            CurrTarget += Quaternion.Euler(CurrAngleX, CurrAngleY, 0) * (Vector3.forward * metres);
        }

        private void HandleArrowOrbit()
        {
            float dt = Time.deltaTime;
            Vector2 input = new Vector2(
                (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f),
                (Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.DownArrow) ? 1f : 0f));
            ApplyArrowOrbit(input, dt);
        }

        public void ApplyArrowOrbit(Vector2 input, float dt)
        {
            if (dt < 1e-6f) return;
            if (input.sqrMagnitude > 1e-6f) CancelFocus();
            float speed = _keyboardOrbitSpeed * dt * ArrowSpeed;

            CurrAngleY += input.x * speed;
            CurrAngleX += input.y * speed;
            CurrAngleX = Mathf.Clamp(CurrAngleX, MinPitchDeg, MaxPitchDeg);
        }

        private void HandlePlusMinusZoom()
        {
            if (IsTypingInInputField()) return;
            float direction = 0f;
            if (Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.KeypadPlus))
                direction = 1f;
            else if (Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.KeypadMinus))
                direction = -1f;
            ApplyZoomMovement(direction, Time.deltaTime);
        }

        public void ApplyZoomDelta(float delta)
        {
            if (Mathf.Abs(delta) < 0.01f) return;
            MoveForward(-delta * _zoomStep * _zoomSpeed);
        }

        public void ApplyScrollInput(float delta)
        {
            _scrollTargetMetres += delta;
        }

        public void UpdateScrollSmooth(float dt)
        {
            if (dt < 1e-6f) return;
            float smooth = Mathf.SmoothDamp(_scrollCurrentMetres, _scrollTargetMetres,
                ref _scrollSmoothVelocity, ScrollSmoothTime, Mathf.Infinity, dt);
            float delta = smooth - _scrollCurrentMetres;
            _scrollCurrentMetres = smooth;

            if (Mathf.Abs(delta) > 1e-6f)
                MoveForward(delta);

            if (Mathf.Abs(_scrollTargetMetres) < 1e-5f && Mathf.Abs(delta) < 1e-5f)
            {
                _scrollTargetMetres = 0f;
                _scrollCurrentMetres = 0f;
                _scrollSmoothVelocity = 0f;
            }
        }

        public void ResetScrollSmooth()
        {
            _scrollTargetMetres = 0f;
            _scrollCurrentMetres = 0f;
            _scrollSmoothVelocity = 0f;
        }

        public void ApplyZoomMovement(float direction, float dt)
        {
            if (Mathf.Abs(direction) < 0.01f || dt < 1e-6f) return;
            CancelFocus();
            ResetScrollSmooth();
            float speed = _moveSpeed * dt * WasdSpeed;
            MoveForward(direction * speed);
        }

        public static bool IsTypingInInputField()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) return false;
            return IsInputField(es.currentSelectedGameObject);
        }

        public static bool IsInputField(GameObject selected)
        {
            if (selected == null) return false;
            return selected.GetComponent<TMP_InputField>() != null;
        }

        private static bool PointerOverUI()
        {
            using var _ = PerfMarkers.CameraPointerOverUI.Auto();
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

        private void HandleRmbClick()
        {
            if (_cachedCamera == null) return;
            Ray ray = _cachedCamera.ScreenPointToRay(Input.mousePosition);
            bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            ResolveRmbClick(ray, shiftHeld);
        }

        public void ResolveRmbClick(Ray ray, bool shiftHeld)
        {
            if (Tools.EyedropperMode.Active)
            {
                Tools.EyedropperController.PickAt(ray, shiftHeld);
                return;
            }
            if (Tools.ToolMode.MouseCaptured) return;

            var e = SelectionManager.RaycastElementThroughGizmos(ray, shiftHeld);
            if (e == null || e.GetComponent<BasePlate>() != null) return;
            if (!EditModeManager.IsInteractable(e)) return;
            if (UI.UIManager.Instance == null) return;

            if (ModuleEditMode.IsActive)
            {
                if (ModuleEditMode.IsEditable(e))
                    UI.UIManager.Instance.OpenContextMenu(e);
                return;
            }

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
            CancelFocus();
            CurrAngleX = angleX;
            CurrAngleY = angleY;
        }

        private void FocusOnSelection()
        {
            if (SelectionManager.Instance != null && SelectionManager.Instance.Selected != null)
                FocusOn(SelectionManager.Instance.Selected.transform.position);
        }

        public void FocusOn(Vector3 point)
        {
            _focusFrom = CurrTarget;
            _focusTo = point;
            if ((point - _focusFrom).sqrMagnitude < 1e-8f)
            {
                _focusTime = NoFocus;
                CurrTarget = point;
                return;
            }
            _focusTime = 0f;
        }

        public bool IsFocusing => _focusTime >= 0f;

        public void CancelFocus() => _focusTime = NoFocus;

        public void UpdateFocus(float dt)
        {
            if (_focusTime < 0f) return;
            _focusTime += dt;
            float t = Mathf.Clamp01(_focusTime / FocusSeconds);
            CurrTarget = Vector3.Lerp(_focusFrom, _focusTo, Mathf.SmoothStep(0f, 1f, t));
            if (t >= 1f) _focusTime = NoFocus;
        }

        public void UpdateCameraPosition()
        {
            Quaternion rotation = Quaternion.Euler(CurrAngleX, CurrAngleY, 0);
            Vector3 offset = rotation * (Vector3.back * Dist);
            if (_cachedCamera != null)
            {
                _cachedCamera.transform.position = CurrTarget + offset;
                _cachedCamera.transform.LookAt(CurrTarget);
            }
        }
    }
}
