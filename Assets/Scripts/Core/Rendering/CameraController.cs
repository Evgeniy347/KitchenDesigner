using TMPro;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class CameraController : MonoBehaviour
    {
        public static CameraController? Instance { get; private set; }

        [SerializeField] private float _distance = 5f;
        // Фоторежим держит собственную дистанцию зума (сохраняется в проект),
        // независимую от обычного режима.
        [SerializeField] private float _photoDistance = 8f;
        [SerializeField] private float _minDistance = 0.5f;
        [SerializeField] private float _maxDistance = 20f;
        [SerializeField] private float _zoomSpeed = 1f;
        [SerializeField] private float _orbitSpeed = 2f;
        [SerializeField] private float _panSpeed = 0.02f;
        [SerializeField] private float _moveSpeed = 3f;
        [SerializeField] private float _keyboardOrbitSpeed = 45f;

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

        private Camera? _cachedCamera;
        private GameObject? _floor;

        public void AssignTestCamera(Camera camera) => _cachedCamera = camera;
        public void AssignTestFloor(GameObject floor) => _floor = floor;

        /// <summary>Активная дистанция зума: в фоторежиме — своя (_photoDistance),
        /// иначе обычная (_distance). Оба значения храним и сохраняем независимо.</summary>
        private float Dist
        {
            get => PhotoMode.Active ? _photoDistance : _distance;
            set
            {
                float c = Mathf.Clamp(value, _minDistance, _maxDistance);
                if (PhotoMode.Active) _photoDistance = c; else _distance = c;
            }
        }

        // Множители из настроек «Управление». Без ассета настроек (юнит-тесты,
        // ранний старт) работаем как раньше — с коэффициентом 1.
        private static float MouseSensitivity => KitchenSettings.Instance != null
            ? KitchenSettings.Instance.MouseSensitivity : 1f;

        private static float WasdSpeed => KitchenSettings.Instance != null
            ? KitchenSettings.Instance.WasdSpeed : 1f;

        private static float ArrowSpeed => KitchenSettings.Instance != null
            ? KitchenSettings.Instance.ArrowSpeed : 1f;

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
            angleX = _angleX, angleY = _angleY, distance = _distance,
            photoDistance = _photoDistance
        };

        /// <summary>Восстановить состояние камеры из проекта.</summary>
        public void SetState(CameraState s)
        {
            _target = new Vector3(s.targetX, s.targetY, s.targetZ);
            _angleX = s.angleX;
            _angleY = s.angleY;
            _distance = Mathf.Clamp(s.distance, _minDistance, _maxDistance);
            // Старые проекты без photoDistance (0) → оставляем текущее значение.
            if (s.photoDistance > 0f)
                _photoDistance = Mathf.Clamp(s.photoDistance, _minDistance, _maxDistance);
            UpdateCameraPosition();
        }

        private void Update()
        {
            // Орбита: ПКМ по чему угодно (деталь или пустота). ПКМ-клик по детали — контекстное меню.
            // Pan: ЛКМ по пустому месту или СКМ. ЛКМ по детали = перемещение детали.
            bool overUI = PointerOverUI();
            bool lmbDown = Input.GetMouseButtonDown(0);
            bool rmbDown = Input.GetMouseButtonDown(1);
            bool rmbUp = Input.GetMouseButtonUp(1);
            bool mmbDown = Input.GetMouseButtonDown(2);
            float scroll = Input.GetAxis("Mouse ScrollWheel");

            // Во время размещения объекта мышь принадлежит PlacementController:
            // ПКМ отменяет установку, поэтому камера ПКМ/орбиту не обрабатывает.
            if (!PlacementController.IsActive)
            {
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
                float sens = MouseSensitivity;
                _angleY += delta.x * _orbitSpeed * 0.1f * sens;
                _angleX -= delta.y * _orbitSpeed * 0.1f * sens;
                _angleX = Mathf.Clamp(_angleX, -89f, 89f);
                _lastMouse = Input.mousePosition;
            }

            if (_isPanning)
            {
                Vector3 delta = Input.mousePosition - _lastMouse;
                float pan = _panSpeed * (Dist * 0.1f) * MouseSensitivity;
                if (KitchenSettings.Instance.CameraPanFree)
                {
                    Vector3 right = Quaternion.Euler(_angleX, _angleY, 0) * Vector3.right;
                    Vector3 up = Quaternion.Euler(_angleX, _angleY, 0) * Vector3.up;
                    _target -= (right * delta.x + up * delta.y) * pan;
                }
                else
                {
                    Vector3 forward = Quaternion.Euler(_angleX, _angleY, 0) * Vector3.forward;
                    Vector3 right = Quaternion.Euler(0, _angleY, 0) * Vector3.right;
                    forward.y = 0; forward.Normalize();
                    _target -= (right * delta.x + forward * delta.y) * pan;
                }
                _lastMouse = Input.mousePosition;
            }

            if (Mathf.Abs(scroll) > 0.01f && !overUI)
                Dist -= scroll * _zoomSpeed;   // сеттер сам ограничивает диапазон

            if (!IsTypingInInputField())
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

                HandleWASD();
                HandleArrowOrbit();
                HandlePlusMinusZoom();
            }

            UpdateCameraPosition();
            UpdateFloorVisibility();
        }

        public void UpdateFloorVisibility()
        {
            if (_cachedCamera == null) return;

            var floors = FloorElement.Active;

            // Опорная плита (BasePlate) видна только когда нет пользовательских
            // полов — иначе их совпадающие верхние плоскости (y=0) мерцают.
            // Коллайдер плиты остаётся якорем заземления/валидации.
            if (_floor != null) ApplyFloorCameraHide(_floor, rendererVisible: floors.Count == 0);

            for (int i = 0; i < floors.Count; i++)
            {
                var f = floors[i];
                if (f != null) ApplyFloorCameraHide(f.gameObject, rendererVisible: true);
            }
        }

        // Пол скрывается, когда камера ниже его верхней плоскости и смотрит вверх —
        // иначе он закрывает вид снизу. В фоторежиме не прячем: сцена цельная.
        // rendererVisible — базовая видимость рендера (для BasePlate зависит от
        // наличия пользовательских полов); коллайдер завязан только на камеру.
        private void ApplyFloorCameraHide(GameObject floor, bool rendererVisible)
        {
            var renderer = floor.GetComponent<MeshRenderer>();
            if (renderer == null) return;

            float topY = floor.transform.position.y + floor.transform.localScale.y * 0.5f;
            bool cameraBelow = _cachedCamera!.transform.position.y < topY;
            bool lookingUp = _cachedCamera.transform.forward.y > 0f;
            bool hide = !PhotoMode.Active && cameraBelow && lookingUp;

            renderer.enabled = rendererVisible && !hide;
            var collider = floor.GetComponent<Collider>();
            if (collider != null)
                collider.enabled = !hide;
        }

        private void HandleWASD()
        {
            float dt = Time.deltaTime;
            Vector2 input = new Vector2(
                (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f),
                (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f));
            ApplyWASDMovement(input, dt);
        }

        /// <summary>
        /// Применяет WASD-движение камеры. input.x: +1=D, -1=A; input.y: +1=W, -1=S.
        /// Вынесено в публичный метод для покрытия юнит-тестами.
        /// </summary>
        public void ApplyWASDMovement(Vector2 input, float dt)
        {
            if (dt < 1e-6f) return;
            float speed = _moveSpeed * Dist * 0.25f * dt * WasdSpeed;

            Vector3 fwd = Quaternion.Euler(0, _angleY, 0) * Vector3.forward;
            Vector3 right = Quaternion.Euler(0, _angleY, 0) * Vector3.right;

            _target += fwd * (input.y * speed);
            _target += right * (input.x * speed);
        }

        private void HandleArrowOrbit()
        {
            float dt = Time.deltaTime;
            Vector2 input = new Vector2(
                (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f),
                (Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.DownArrow) ? 1f : 0f));
            ApplyArrowOrbit(input, dt);
        }

        /// <summary>
        /// Применяет орбиту клавишами-стрелками. input.x: +1=вправо, -1=влево;
        /// input.y: +1=вверх, -1=вниз.
        /// </summary>
        public void ApplyArrowOrbit(Vector2 input, float dt)
        {
            if (dt < 1e-6f) return;
            float speed = _keyboardOrbitSpeed * dt * ArrowSpeed;

            _angleY += input.x * speed;
            _angleX += input.y * speed;
            _angleX = Mathf.Clamp(_angleX, -89f, 89f);
        }

        private void HandlePlusMinusZoom()
        {
            // Дефис — допустимый символ имени: без этой проверки набор «B4-upper»
            // отъезжал бы камерой на каждом «-».
            if (IsTypingInInputField()) return;
            float delta = 0f;
            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus))
                delta = -1f;
            else if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
                delta = 1f;
            ApplyZoomDelta(delta);
        }

        /// <summary>
        /// Применяет один шаг зума клавишами +/-. delta: +1 = приблизить (-), -1 = отдалить (+).
        /// </summary>
        public void ApplyZoomDelta(float delta)
        {
            if (Mathf.Abs(delta) < 0.01f) return;
            Dist += delta * _zoomSpeed * Dist * 0.1f;   // сеттер ограничивает диапазон
        }

        /// <summary>
        /// Возвращает true, если фокус сейчас в любом поле ввода (набор текста).
        /// В этом случае горячие клавиши камеры (WASD, стрелки, +/-, F и т.д.) не должны срабатывать.
        /// </summary>
        public static bool IsTypingInInputField()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) return false;
            return IsInputField(es.currentSelectedGameObject);
        }

        /// <summary>
        /// Проверяет, является ли выбранный объект полем ввода TMP_InputField. Публично для тестирования.
        /// </summary>
        public static bool IsInputField(GameObject selected)
        {
            if (selected == null) return false;
            return selected.GetComponent<TMP_InputField>() != null;
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
            if (!EditModeManager.IsInteractable(e)) return; // режим редактора блокирует
            if (UI.UIManager.Instance == null) return;

            // В режиме редактирования модуля — настройка отдельных деталей.
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

        public void UpdateCameraPosition()
        {
            Quaternion rotation = Quaternion.Euler(_angleX, _angleY, 0);
            Vector3 offset = rotation * (Vector3.back * Dist);
            if (_cachedCamera != null)
            {
                _cachedCamera.transform.position = _target + offset;
                _cachedCamera.transform.LookAt(_target);
            }
        }
    }
}
