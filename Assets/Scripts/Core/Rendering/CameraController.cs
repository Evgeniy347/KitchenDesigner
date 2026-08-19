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
        // Фоторежим держит собственные позицию и углы обзора (сохраняются в проект),
        // независимые от обычного режима.
        private Vector3 _photoTarget = Vector3.zero;
        private float _photoAngleX = 30f;
        private float _photoAngleY = 0f;
        [SerializeField] private float _minDistance = 0.5f;
        [SerializeField] private float _maxDistance = 20f;
        [SerializeField] private float _zoomSpeed = 1f;
        [SerializeField] private float _orbitSpeed = 2f;
        [SerializeField] private float _panSpeed = 0.02f;
        // Базовая (минимальная) скорость WASD в м/с. От зума НЕ зависит —
        // разгон даёт удержание клавиши, см. WasdHoldMultiplier.
        [SerializeField] private float _moveSpeed = 0.6f;
        [SerializeField] private float _keyboardOrbitSpeed = 45f;
        // Зум — это смещение камеры вперёд/назад по взгляду, а не изменение
        // радиуса орбиты. Один щелчок колеса / нажатие +− = столько метров.
        [SerializeField] private float _zoomStep = 0.3f;

        // ── Разгон WASD (только с Shift) ──────────────────────────────────
        /// <summary>С Shift разгон укладывается в секунду.</summary>
        public const float WasdShiftRampSeconds = 1f;
        /// <summary>Максимальный множитель скорости.</summary>
        public const float WasdMaxMultiplier = 20f;
        private float _wasdHeld;

        // ── Плавный зум колесом мыши ───────────────────────────────────
        private float _scrollTarget;     // накопленная цель (метры)
        private float _scrollCurrent;    // текущее плавное положение
        private float _scrollVelocity;   // для Mathf.SmoothDamp
        private const float ScrollSmoothTime = 0.08f;

        // ── Плавный фокус (клавиша F, двойной клик в «Ошибках») ───────────
        /// <summary>Длительность перелёта камеры к точке фокуса.</summary>
        public const float FocusSeconds = 2f;
        private Vector3 _focusFrom;
        private Vector3 _focusTo;
        private float _focusTime = -1f;   // < 0 — перелёта нет

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
            photoDistance = _photoDistance,
            photoTargetX = _photoTarget.x, photoTargetY = _photoTarget.y, photoTargetZ = _photoTarget.z,
            photoAngleX = _photoAngleX, photoAngleY = _photoAngleY
        };

        /// <summary>Восстановить состояние камеры из проекта.</summary>
        public void SetState(CameraState s)
        {
            CancelFocus();
            _target = new Vector3(s.targetX, s.targetY, s.targetZ);
            _angleX = s.angleX;
            _angleY = s.angleY;
            _distance = Mathf.Clamp(s.distance, _minDistance, _maxDistance);
            if (s.photoDistance > 0f)
                _photoDistance = Mathf.Clamp(s.photoDistance, _minDistance, _maxDistance);
            // Старые проекты без photoTarget (0,0,0) → фото-позиция = обычной.
            _photoTarget = s.photoTargetX != 0f || s.photoTargetY != 0f || s.photoTargetZ != 0f
                ? new Vector3(s.photoTargetX, s.photoTargetY, s.photoTargetZ)
                : _target;
            // Старые проекты без photoAngle (оба 0) → фото-углы = обычным.
            _photoAngleX = s.photoAngleX != 0f || s.photoAngleY != 0f
                ? s.photoAngleX : _angleX;
            _photoAngleY = s.photoAngleX != 0f || s.photoAngleY != 0f
                ? s.photoAngleY : _angleY;
            UpdateCameraPosition();
        }

        private void Update()
        {
            using var _ = PerfMarkers.CameraUpdate.Auto();
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

            if (mmbDown || (lmbDown && !overUI && !PointerHitsBoard()
                && !ResizeHandleManager.PointerOverHandle()
                && !TextureOverlayHandles.PointerOverHandle()))
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
                ApplyOrbit(delta.x * _orbitSpeed * 0.1f * sens,
                          -delta.y * _orbitSpeed * 0.1f * sens);
                _lastMouse = Input.mousePosition;
            }

            if (_isPanning)
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

            // Колесо мыши — накапливаем в плавный зум.
            // Щелчок колеса даёт ±0.1 по оси, поэтому масштабируем на 10.
            if (Mathf.Abs(scroll) > 0.01f && !overUI)
                ApplyScrollInput(scroll * 10f * _zoomStep * _zoomSpeed);

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

                if (Input.GetKeyDown(KeyCode.E))
                    ToggleSelectedOpenables();

                HandleWASD();
                HandleArrowOrbit();
                HandlePlusMinusZoom();
                UpdateScrollSmooth(Time.deltaTime);
            }

            UpdateFocus(Time.deltaTime);
            UpdateCameraPosition();
            UpdateFloorVisibility();
        }

        public void ToggleSelectedOpenables()
        {
            var sel = SelectionManager.Instance;
            if (sel == null) return;
            foreach (var el in sel.SelectedElements)
            {
                if (el is FacadeElement f)
                {
                    var drawer = UI.ContextMenuUI.FindDrawerForFacade(f);
                    if (drawer != null) ToggleDrawerFor(drawer);
                    else f.ToggleOpen();
                }
                else if (el is DrawerElement dr)
                {
                    ToggleDrawerFor(dr);
                }
                else if (el is IOpenable openable)
                {
                    openable.ToggleOpen();
                }
            }

            UI.ContextMenuUI.Instance?.SyncOpenLabels();
        }

        /// <summary>Поведение «открыть» для ящика: сдвоенный цикл, одиночный —
        /// обычный <see cref="IOpenable.ToggleOpen"/>. Точка, в которой сходятся
        /// «E» на выделенном ящике, «E» на фасаде с пристёгнутым ящиком и кнопка
        /// «Открыть» в контекстном меню фасада.</summary>
        internal static void ToggleDrawerFor(DrawerElement dr)
        {
            if (dr.FindPaired() != null) dr.CycleDoubleState();
            else dr.ToggleOpen();
        }

        public void UpdateFloorVisibility()
        {
            using var _ = PerfMarkers.CameraFloorVisibility.Auto();
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

            // У полигонального пола ДВА коллайдера: BoxCollider куба, выключенный
            // навсегда, и MeshCollider по контуру (FloorElement.SetPolygonLocalMm).
            // GetComponent<Collider>() возвращает первый — бокс, поэтому раньше
            // камера дёргала именно его: меш продолжал ловить клики сквозь скрытый
            // пол, а бокс ещё и включался обратно. Переключаем рабочий коллайдер.
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

        /// <summary>
        /// Множитель скорости WASD от времени удержания клавиши. Без Shift —
        /// постоянная базовая скорость (×1). С Shift разгон за одну секунду до ×20.
        /// </summary>
        public static float WasdHoldMultiplier(float heldSeconds, bool shift)
        {
            if (!shift) return 1f;
            float t = Mathf.Clamp01(heldSeconds / WasdShiftRampSeconds);
            return Mathf.Lerp(1f, WasdMaxMultiplier, t);
        }

        /// <summary>
        /// Применяет WASD-движение камеры. input.x: +1=D, -1=A; input.y: +1=W, -1=S.
        /// Скорость от зума не зависит: базовая м/с × разгон от удержания.
        /// Вынесено в публичный метод для покрытия юнит-тестами.
        /// </summary>
        public void ApplyWASDMovement(Vector2 input, float dt, bool shift = false)
        {
            if (dt < 1e-6f) return;
            // Клавиши отпущены или Shift не нажат — разгон сбрасывается.
            if (input.sqrMagnitude < 1e-6f || !shift)
            {
                _wasdHeld = 0f;
                if (input.sqrMagnitude < 1e-6f) return;
            }

            CancelFocus();
            float speed = _moveSpeed * WasdHoldMultiplier(_wasdHeld, shift) * dt * WasdSpeed;
            _wasdHeld += dt;

            Vector3 fwd = Quaternion.Euler(0, CurrAngleY, 0) * Vector3.forward;
            Vector3 right = Quaternion.Euler(0, CurrAngleY, 0) * Vector3.right;

            CurrTarget += fwd * (input.y * speed);
            CurrTarget += right * (input.x * speed);
        }

        /// <summary>
        /// Поворот камеры «на месте» (ПКМ — как будто вертим головой): позиция камеры
        /// остаётся, а точка-цель переезжает вперёд по новому направлению взгляда.
        /// </summary>
        public void ApplyOrbit(float deltaYaw, float deltaPitch)
        {
            CancelFocus();
            Vector3 camPos = CurrTarget + Quaternion.Euler(CurrAngleX, CurrAngleY, 0) * (Vector3.back * Dist);
            CurrAngleY += deltaYaw;
            CurrAngleX = Mathf.Clamp(CurrAngleX + deltaPitch, -89f, 89f);
            CurrTarget = camPos + Quaternion.Euler(CurrAngleX, CurrAngleY, 0) * (Vector3.forward * Dist);
        }

        /// <summary>Смещение камеры вперёд (+) / назад (−) по направлению взгляда.
        /// Это и есть зум: радиус орбиты не меняется.</summary>
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

        /// <summary>
        /// Применяет орбиту клавишами-стрелками. input.x: +1=вправо, -1=влево;
        /// input.y: +1=вверх, -1=вниз.
        /// </summary>
        public void ApplyArrowOrbit(Vector2 input, float dt)
        {
            if (dt < 1e-6f) return;
            if (input.sqrMagnitude > 1e-6f) CancelFocus();
            float speed = _keyboardOrbitSpeed * dt * ArrowSpeed;

            CurrAngleY += input.x * speed;
            CurrAngleX += input.y * speed;
            CurrAngleX = Mathf.Clamp(CurrAngleX, -89f, 89f);
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

        /// <summary>
        /// Применяет один шаг зума клавишами +/-. delta: -1 = приблизить (+), +1 = отдалить (-).
        /// Зум — смещение камеры вперёд/назад, дистанция орбиты не меняется.
        /// </summary>
        public void ApplyZoomDelta(float delta)
        {
            if (Mathf.Abs(delta) < 0.01f) return;
            MoveForward(-delta * _zoomStep * _zoomSpeed);
        }

        public void ApplyScrollInput(float delta)
        {
            _scrollTarget += delta;
        }

        public void UpdateScrollSmooth(float dt)
        {
            if (dt < 1e-6f) return;
            float smooth = Mathf.SmoothDamp(_scrollCurrent, _scrollTarget, ref _scrollVelocity, ScrollSmoothTime);
            float delta = smooth - _scrollCurrent;
            _scrollCurrent = smooth;

            if (Mathf.Abs(delta) > 1e-6f)
                MoveForward(delta);

            if (Mathf.Abs(_scrollTarget) < 1e-5f && Mathf.Abs(delta) < 1e-5f)
            {
                _scrollTarget = 0f;
                _scrollCurrent = 0f;
                _scrollVelocity = 0f;
            }
        }

        public void ResetScrollSmooth()
        {
            _scrollTarget = 0f;
            _scrollCurrent = 0f;
            _scrollVelocity = 0f;
        }

        public void ApplyZoomMovement(float direction, float dt)
        {
            if (Mathf.Abs(direction) < 0.01f || dt < 1e-6f) return;
            CancelFocus();
            ResetScrollSmooth();
            float speed = _moveSpeed * dt * WasdSpeed;
            MoveForward(direction * speed);
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

        // ПКМ-клик по объекту (не пол): обычный одиночный объект → окно его настроек;
        // связанная группа или мультивыделение → меню группы (связать / настройки группы).
        private void HandleRmbClick()
        {
            if (_cachedCamera == null) return;
            Ray ray = _cachedCamera.ScreenPointToRay(Input.mousePosition);
            bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            // ПКМ-клик в режиме пипетки — это забор декора, а не контекстное меню.
            // Обрабатываем здесь, а не в самом инструменте: отличить клик от
            // орбиты умеет только камера (см. _rmbMoved выше).
            if (Tools.EyedropperMode.Active)
            {
                Tools.EyedropperController.PickAt(ray, shiftHeld);
                return;
            }
            if (Tools.ToolMode.MouseCaptured) return;

            var e = KitchenDesigner.Core.SelectionManager.RaycastTransparentAware(ray, shiftHeld);
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
            CancelFocus();
            CurrAngleX = angleX;
            CurrAngleY = angleY;
        }

        private void FocusOnSelection()
        {
            if (SelectionManager.Instance != null && SelectionManager.Instance.Selected != null)
                FocusOn(SelectionManager.Instance.Selected.transform.position);
        }

        /// <summary>Плавно (за <see cref="FocusSeconds"/>) перелететь к точке —
        /// клавиша F и двойной клик по строке в «Ошибках».</summary>
        public void FocusOn(Vector3 point)
        {
            _focusFrom = CurrTarget;
            _focusTo = point;
            if ((point - _focusFrom).sqrMagnitude < 1e-8f)
            {
                _focusTime = -1f;
                CurrTarget = point;
                return;
            }
            _focusTime = 0f;
        }

        /// <summary>Идёт ли сейчас плавный перелёт.</summary>
        public bool IsFocusing => _focusTime >= 0f;

        /// <summary>Прервать перелёт: любое ручное управление камерой важнее.</summary>
        public void CancelFocus() => _focusTime = -1f;

        /// <summary>Шаг перелёта. Публично для юнит-тестов.</summary>
        public void UpdateFocus(float dt)
        {
            if (_focusTime < 0f) return;
            _focusTime += dt;
            float t = Mathf.Clamp01(_focusTime / FocusSeconds);
            // SmoothStep: плавный старт и торможение в конце.
            CurrTarget = Vector3.Lerp(_focusFrom, _focusTo, Mathf.SmoothStep(0f, 1f, t));
            if (t >= 1f) _focusTime = -1f;
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
