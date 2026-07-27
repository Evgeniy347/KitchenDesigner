using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class ElementMover : MonoBehaviour
    {
        public static bool IsDragging { get; private set; }

        // Объекты, перемещаемые прямо сейчас (для WallManager: двигаемую стену не опускаем).
        private static readonly HashSet<KitchenElement> _movingSet = new HashSet<KitchenElement>();
        public static bool IsMoving(KitchenElement e) => e != null && _movingSet.Contains(e);

        private KitchenElement? _target;
        private Vector3 _offset;
        private Vector3 _startPosition;
        private Quaternion _startRotation;
        private bool _wasMoved;
        private bool _wasShift;
        private float _vOffset;
        // Текущая «удерживаемая» высота при горизонтальном перетаскивании.
        // Берётся из старта (и из Shift-подъёма), а НЕ из позиции прошлого кадра:
        // иначе Y прошлого снэпа заново округлялся сеткой каждый кадр (шаг 18 мм),
        // контакт с полом рвался, и снэп чинил вертикаль вместо прилипания к соседу.
        private float _dragY;
        private AxisLock _axisLock = AxisLock.None;
        private Wall? _dragWall;
        private bool _targetIsWallOpening;

        // ЛКМ нажата на детали, но ещё не решено клик это или drag.
        private bool _pressed;
        private Vector2 _pressMouse;
        private float _pressTime;
        // Пока курсор не сместится дальше этого порога (в пикселях) — это клик
        // (выделение), а не перетаскивание. Только после порога
        // включается drag с зелёной/красной тонировкой.
        private const float DragStartPixels = 6f;
        // Задержка (сек) перед активацией перетаскивания: защита от ложных
        // срабатываний при клике (дрожание мыши на момент нажатия кнопки).
        private const float DragStartSeconds = 0.15f;

        private enum AxisLock { None, X, Z }

        private Material? _dragOriginalMaterial;
        private Material? _dragTintMaterial;

        private Mesh? _ghostMesh;
        private Material? _ghostMaterial;
        private Vector3? _ghostPosition;
        private Quaternion _ghostRotation;
        private bool _showGhost;

        // Набор объектов, перемещаемых вместе (мультивыделение): элементы + старты.
        private readonly List<KitchenElement> _moveSet = new List<KitchenElement>();
        private readonly List<Vector3> _moveStart = new List<Vector3>();

        private void Start()
        {
            var sel = GetComponent<SelectionManager>();
            if (sel != null)
                sel.OnSelectionChanged += OnSelectionChanged;
            else
                Debug.LogError("[Mover] No SelectionManager found on same GameObject");

            CreateGhostMaterial();
        }

        private void OnDestroy()
        {
            if (_ghostMaterial != null)
                Destroy(_ghostMaterial);
        }

        private void CreateGhostMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return;
            _ghostMaterial = new Material(shader);
            _ghostMaterial.SetFloat("_Surface", 1);
            _ghostMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _ghostMaterial.renderQueue = 3000;
            _ghostMaterial.color = new Color(0.3f, 0.6f, 1f, 0.2f);
            _ghostMaterial.SetFloat("_Metallic", 0f);
            _ghostMaterial.SetFloat("_Smoothness", 0.1f);
        }

        private void OnSelectionChanged(KitchenElement? element)
        {
            if (IsDragging) return;
            _target = element;
        }

        private bool AltHeld => Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        private bool ShiftHeld => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        private static bool PointerOverUI =>
            UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

        // ЛКМ нажата: если попали по детали — запоминаем «нажатие» (кандидат на клик
        // или drag). Сам drag и тонировка НЕ включаются, пока курсор не сдвинется.
        private void TryBeginPress()
        {
            _pressed = false;
            if (AltHeld || PointerOverUI) return; // Alt+ЛКМ — орбита; клик по UI — не drag
            if (Camera.main == null) return;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit)) return;

            var element = hit.collider.GetComponentInParent<KitchenElement>();
            if (element == null) return;
            if (element.GetComponent<BasePlate>() != null) return; // пол не таскаем
            if (!ModuleEditMode.IsEditable(element)) return; // вне активного модуля — заблокировано
            if (!EditModeManager.IsInteractable(element)) return; // режим редактора блокирует

            _target = element;
            _pressed = true;
            _pressMouse = Input.mousePosition;
            _pressTime = Time.unscaledTime;
            _startPosition = element.transform.position;
            _startRotation = element.transform.rotation;
            _wasMoved = false;
            _wasShift = false;
            _targetIsWallOpening = element is WindowElement || element is DoorElement;
            _dragWall = _targetIsWallOpening ? FindAttachedWall(element) : null;

            Plane dragPlane = GetDragPlane(element);
            _offset = dragPlane.Raycast(ray, out float enter)
                ? _startPosition - ray.GetPoint(enter)
                : Vector3.zero;
        }

        private bool PressMovedEnough()
        {
            if (Time.unscaledTime - _pressTime < DragStartSeconds) return false;
            Vector2 now = Input.mousePosition;
            return (now - _pressMouse).magnitude > DragStartPixels;
        }

        // Курсор сдвинулся достаточно — это перетаскивание, а не клик.
        private void BeginDrag()
        {
            if (_target == null) return;
            IsDragging = true;
            _wasMoved = true;
            BuildMoveSet();
            // Стартовая точка и offset пересчитываются на ПОЛНОЙ геометрии (BuildMoveSet
            // мог вернуть опущенную стену на полную высоту → позиция изменилась).
            _startPosition = _target.transform.position;
            _dragY = _startPosition.y;
            RecomputeOffset();
            SaveDragMaterial(); // зелёная/красная тонировка появляется только здесь
        }

        private void RecomputeOffset()
        {
            if (Camera.main == null) return;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            Plane dragPlane = _target != null ? GetDragPlane(_target) : new Plane(Vector3.up, _startPosition);
            _offset = dragPlane.Raycast(ray, out float enter)
                ? _startPosition - ray.GetPoint(enter)
                : Vector3.zero;
        }

        // Если схвачен элемент мультивыделения — двигаем всю выборку (подвижные),
        // иначе только схваченный объект. Полускрытые стены возвращаются на полную
        // высоту (GrabStart) ДО взятия стартовых позиций — иначе объект «прыгает».
        private void BuildMoveSet()
        {
            _moveSet.Clear();
            _moveStart.Clear();

            var sel = SelectionManager.Instance;
            bool group = sel != null && sel.IsSelected(_target!) && sel.SelectedElements.Count > 1;
            if (group)
            {
                foreach (var e in sel!.SelectedElements)
                    if (e != null && e.Movable && ModuleEditMode.IsEditable(e)) _moveSet.Add(e);
            }
            if (_moveSet.Count == 0)
                _moveSet.Add(_target!);

            foreach (var e in _moveSet) _moveStart.Add(GrabStart(e));

            _movingSet.Clear();
            foreach (var e in _moveSet) _movingSet.Add(e);
        }

        /// <summary>Стартовая точка перемещения при захвате. Полускрытую (опущенную)
        /// стену сначала возвращаем на полную высоту, ИНАЧЕ старт берётся в опущенном
        /// состоянии, стена тут же восстанавливается (WallManager) и «прыгает».</summary>
        public static Vector3 GrabStart(KitchenElement e)
        {
            if (e == null) return Vector3.zero;
            var wall = e.GetComponent<Wall>();
            if (wall != null) wall.RestoreFull();
            return e.transform.position;
        }

        /// <summary>Сдвигает все элементы набора на delta от их стартовых позиций.</summary>
        public static void ApplyDelta(IList<KitchenElement> members, IList<Vector3> starts, Vector3 delta)
        {
            for (int i = 0; i < members.Count; i++)
                if (members[i] != null) members[i].transform.position = starts[i] + delta;
        }

        private void RevertMoveSet()
        {
            if (_moveSet.Count == 0)
            {
                if (_target != null) _target.transform.position = _startPosition;
                return;
            }
            ApplyDelta(_moveSet, _moveStart, Vector3.zero);
        }

        private void Update()
        {
            // Идёт размещение нового объекта — мышь принадлежит PlacementController.
            if (PlacementController.IsActive)
                return;

            // В режиме рулетки детали не двигаются и не удаляются с клавиатуры:
            // мышь целиком принадлежит замерам.
            if (Measure.MeasureMode.Active)
                return;

            // Набор текста в поле ввода не должен работать как горячие клавиши
            // сцены: Delete стирал символ И удалял выделенный элемент, Ctrl+D
            // посреди имени плодил дубль.
            if (!CameraController.IsTypingInInputField())
            {
                HandleDuplicate();
                HandleDelete();
            }
            HandleDragInput();
        }

        private void HandleDuplicate()
        {
            if (Input.GetKeyDown(KeyCode.D) && !IsDragging && _target != null &&
                (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
            {
                if (!ModuleEditMode.IsEditable(_target)) return;
                var dup = ElementFactory.Duplicate(_target);
                var newElement = dup != null ? dup.GetComponent<KitchenElement>() : null;
                if (newElement != null)
                {
                    // В режиме редактирования модуля дубль остаётся в модуле —
                    // иначе новая деталь оказалась бы заблокированной (вне модуля).
                    if (ModuleEditMode.IsActive)
                        newElement.GroupId = ModuleEditMode.Active!.id;
                    CommandStack.Execute(new CreateCommand(dup!));
                    if (SelectionManager.Instance != null)
                        SelectionManager.Instance.Select(newElement);
                }
            }
        }

        private void HandleDelete()
        {
            if (IsDragging) return;
            if (!Input.GetKeyDown(KeyCode.Delete)) return;

            var sel = SelectionManager.Instance;
            if (sel == null) return;

            // Удаляем все выделенные элементы.
            var list = sel.SelectedElements;
            if (list == null || list.Count == 0) return;

            foreach (var e in list)
                CommandStack.Execute(new DeleteCommand(e.gameObject));

            sel.DeselectAll();
        }

        private void OnRenderObject()
        {
            if (_showGhost && _ghostMesh != null && _ghostPosition.HasValue && _ghostMaterial != null)
                Graphics.DrawMesh(_ghostMesh, _ghostPosition.Value, _ghostRotation, _ghostMaterial, 0);
        }

        private void HandleDragInput()
        {
            // Идёт ресайз ручкой — перемещение объекта не запускаем.
            if (ResizeHandleManager.IsResizing) return;

            if (Input.GetKeyDown(KeyCode.Escape) && IsDragging)
            {
                CancelDrag();
                return;
            }

            if (Input.GetMouseButtonDown(0))
                TryBeginPress();

            if (_pressed && Input.GetMouseButton(0))
            {
                if (!IsDragging && PressMovedEnough())
                {
                    if (_target != null && _target.Movable) BeginDrag();
                    else _pressed = false; // перемещение запрещено — не двигаем
                }
                if (IsDragging)
                    UpdateDrag();
            }

            if (Input.GetMouseButtonUp(0))
            {
                if (IsDragging)
                    FinishDrag();
                _pressed = false;
            }
        }

        private void CancelDrag()
        {
            _showGhost = false;
            _axisLock = AxisLock.None;
            _dragWall = null;
            _targetIsWallOpening = false;
            RevertMoveSet();
            RestoreDragMaterial();
            IsDragging = false;
            _wasMoved = false;
            _pressed = false;
            _movingSet.Clear();
            RefreshHighlights();
        }

        private void UpdateDrag()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            Vector3 newPos = _target!.transform.position;
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
                    _dragY = newPos.y; // после Shift-подъёма горизонтальный drag держит новую высоту
                    computed = true;
                }
            }
            else
            {
                _wasShift = false;
                var dragPlane = GetDragPlane(_target);
                if (dragPlane.Raycast(ray, out float enter))
                {
                    Vector3 point = ray.GetPoint(enter) + _offset;
                    if (!_targetIsWallOpening)
                    {
                        // Горизонтальный drag: высота фиксирована (_dragY), сетка
                        // применяется только к X/Z. Округление Y здесь ломало бы
                        // контакт с полом каждый кадр (см. комментарий у _dragY).
                        point.y = _dragY;
                        newPos = GridManager.SnapToGridXZ(point);
                    }
                    else
                    {
                        newPos = GridManager.SnapToGrid(point);
                    }
                    computed = true;
                }
            }

            if (!computed) return;

            if (_dragTintMaterial == null) SaveDragMaterial(); // на случай, если drag начат не из BeginDrag

            if (Input.GetKeyDown(KeyCode.X)) _axisLock = _axisLock == AxisLock.X ? AxisLock.None : AxisLock.X;
            if (Input.GetKeyDown(KeyCode.Z)) _axisLock = _axisLock == AxisLock.Z ? AxisLock.None : AxisLock.Z;
            if (_axisLock == AxisLock.X) { newPos.z = _startPosition.z; if (!_targetIsWallOpening) newPos.y = _dragY; }
            else if (_axisLock == AxisLock.Z) { newPos.x = _startPosition.x; if (!_targetIsWallOpening) newPos.y = _dragY; }

            var others = PartRegistry.GetAll();
            if (_moveSet.Count > 1) others.RemoveAll(e => _moveSet.Contains(e));
            var snap = SnapSystem.TrySnap(_target, others, newPos);
            _target.transform.position = WorldBounds.Clamp(snap.snapped ? snap.position : newPos);

            // Групповое перемещение: остальные следуют за схваченным на ту же дельту.
            // При вертикальном перетаскивании окна по стене все элементы группы
            // получат тот же сдвиг по Y — это намеренное поведение; мультивыделение
            // движется как единое целое.
            if (_moveSet.Count > 1)
                ApplyDelta(_moveSet, _moveStart, _target.transform.position - _startPosition);

            // Ghost-preview: деталь стоит в позиции снэпа, а полупрозрачный призрак
            // показывает «свободную» позицию под курсором — видно, что и куда
            // притянуло. Сравнивать надо именно snap.position с newPos: старое
            // сравнение с transform.position всегда было ложным (позиция уже
            // установлена в snap.position строкой выше) — призрак не появлялся никогда.
            if (snap.snapped && (snap.position - newPos).sqrMagnitude > Tolerance.EpsilonSqr)
            {
                _showGhost = true;
                _ghostPosition = newPos;
                _ghostRotation = _target.transform.rotation;
                if (_ghostMesh == null)
                {
                    var mf = _target.GetComponent<MeshFilter>();
                    if (mf != null) _ghostMesh = mf.sharedMesh;
                }
            }
            else
            {
                _showGhost = false;
            }

            UpdateDragTint();
        }

		private void FinishDrag()
		{
			_showGhost = false;

			if (_wasMoved)
			{
				Vector3Int? pillarDimsBefore = null;
				Vector3 pillarPosBefore = Vector3.zero;
				int? pillarMidBefore = null;
				if (_target is PillarElement pillarBefore)
				{
					pillarDimsBefore = pillarBefore.DimensionsMM;
					pillarPosBefore = pillarBefore.transform.position;
					pillarMidBefore = pillarBefore.MidHeightMM;
				}

				AutoAdjustPillar();
				if (KitchenSettings.Instance.BlockOnViolation && MoveSetCausesViolation())
				{
					if (pillarMidBefore.HasValue && _target is PillarElement p)
						p.MidHeightMM = pillarMidBefore.Value;
					RevertMoveSet();
				}
				else
					CommandStack.Execute(BuildMoveCommand(pillarDimsBefore, pillarPosBefore));
			}
			else
			{
				RevertMoveSet();
			}

			_axisLock = AxisLock.None;
			RestoreDragMaterial();
			IsDragging = false;
			_wasShift = false;
			_dragWall = null;
			_targetIsWallOpening = false;
			_movingSet.Clear();
			RefreshHighlights();
		}

		private void AutoAdjustPillar()
		{
			if (!(_target is PillarElement pillar)) return;
			float toU = AppConstants.MM_TO_UNITS;
			Vector3 pillarCenter = _target.transform.position;

			float floorY = FindFloorY(pillarCenter);
			if (floorY < -999f) return;

			float pillarBottomY = floorY;
			pillar.transform.position = new Vector3(pillarCenter.x,
				pillarBottomY + pillar.TotalHeightMM * 0.5f * toU, pillarCenter.z);
			pillarCenter = _target.transform.position;

			float minAbove = pillarBottomY + 80f * toU;
			float maxAbove = pillarBottomY + 130f * toU;
			KitchenElement? bestAbove = null;
			float bestAboveBottom = float.MaxValue;
			foreach (var el in PartRegistry.GetAll())
			{
				if (el == null || el == _target) continue;
				var aabb = ComputeElementAABB(el);
				if (aabb.minY >= minAbove && aabb.minY <= maxAbove)
				{
					if (IsOverlappingXZ(pillarCenter, aabb, 0.05f))
					{
						if (aabb.minY < bestAboveBottom)
						{
							bestAboveBottom = aabb.minY;
							bestAbove = el;
						}
					}
				}
			}
			if (bestAbove == null) return;
			float gapUnits = bestAboveBottom - pillarBottomY;
			// Округление ВНИЗ (с допуском на float-шум): RoundToInt мог удлинить
			// пилон на ≤0.5 мм СКВОЗЬ деталь сверху — невидимое пересечение,
			// красная подсветка и откат всего перемещения при BlockOnViolation.
			int gapMM = Mathf.FloorToInt(gapUnits / toU + Tolerance.ClearanceMm);
			int neededMid = gapMM - PillarElement.TopHeightMM - PillarElement.BottomHeightMM;
			neededMid = Mathf.Clamp(neededMid, PillarElement.MidHeightMM_Min, PillarElement.MidHeightMM_Max);
			float bottomY = pillar.transform.position.y - pillar.TotalHeightMM * 0.5f * toU;
			pillar.MidHeightMM = neededMid;
			float newTotalHeight = pillar.TotalHeightMM * toU;
			pillar.transform.position = new Vector3(pillar.transform.position.x, bottomY + newTotalHeight * 0.5f, pillar.transform.position.z);
		}

		private static float FindFloorY(Vector3 pillarCenter)
		{
			float bestY = float.MinValue;
			foreach (var el in PartRegistry.GetAll())
			{
				if (el == null) continue;
				var aabb = ComputeElementAABB(el);
				if (aabb.maxY > pillarCenter.y - 0.01f) continue;
				if (IsOverlappingXZ(pillarCenter, aabb, 0.05f) && aabb.maxY > bestY)
					bestY = aabb.maxY;
			}
			if (bestY >= pillarCenter.y - 1f) return bestY;
			return -1000f;
		}

		private static bool IsOverlappingXZ(Vector3 point, (float minX, float maxX, float minY, float maxY, float minZ, float maxZ) aabb, float margin)
		{
			return point.x >= aabb.minX - margin && point.x <= aabb.maxX + margin
				&& point.z >= aabb.minZ - margin && point.z <= aabb.maxZ + margin;
		}

		private static (float minX, float maxX, float minY, float maxY, float minZ, float maxZ) ComputeElementAABB(KitchenElement el)
		{
			var verts = el.GetVertices();
			float minX = float.MaxValue, maxX = float.MinValue;
			float minY = float.MaxValue, maxY = float.MinValue;
			float minZ = float.MaxValue, maxZ = float.MinValue;
			foreach (var v in verts)
			{
				if (v.x < minX) minX = v.x; if (v.x > maxX) maxX = v.x;
				if (v.y < minY) minY = v.y; if (v.y > maxY) maxY = v.y;
				if (v.z < minZ) minZ = v.z; if (v.z > maxZ) maxZ = v.z;
			}
			return (minX, maxX, minY, maxY, minZ, maxZ);
		}

        // Проверяет, есть ли нарушения среди перемещаемого набора и его соседей
        // (AABB в радиусе snapThreshold * 2). Это предотвращает ситуацию, когда
        // движение детали B разрывает связь детали A с полом — и это остаётся
        // незамеченным. При этом чужая ошибка вдали не блокирует перемещение.
        // Близость меряется по ГАБАРИТАМ (HasViolationNear), а не по центрам:
        // у крупных деталей центры соседей всегда дальше радиуса, и проверка
        // по центрам пропускала нарушения вплотную к перемещаемой детали.
        private bool MoveSetCausesViolation()
        {
            var result = ConstraintValidator.Validate(PartRegistry.GetAll());
            if (result.isValid) return false;

            float radius = KitchenSettings.Instance.SnapThreshold * 2f * AppConstants.MM_TO_UNITS;
            if (_moveSet.Count == 0)
                return _target != null && ConstraintValidator.HasViolationNear(result, _target, radius);
            foreach (var m in _moveSet)
            {
                if (m != null && ConstraintValidator.HasViolationNear(result, m, radius))
                    return true;
            }
            return false;
        }

        private IUndoCommand BuildMoveCommand(Vector3Int? pillarDimsBefore = null, Vector3 pillarPosBefore = default)
        {
            var cmds = new List<IUndoCommand>();
            for (int i = 0; i < _moveSet.Count; i++)
            {
                var m = _moveSet[i];
                if (m == null) continue;
                var rotBefore = m == _target ? _startRotation : m.transform.rotation;
                cmds.Add(new MoveCommand(m, _moveStart[i], m.transform.position, rotBefore, m.transform.rotation));
            }

            if (pillarDimsBefore.HasValue && _target is PillarElement pillar)
            {
                var dimsAfter = pillar.DimensionsMM;
                if (pillarDimsBefore.Value != dimsAfter)
                {
                    cmds.Add(new ResizeCommand(pillar, pillarDimsBefore.Value, dimsAfter,
                        pillarPosBefore, pillar.transform.position,
                        pillar.transform.rotation, pillar.transform.rotation));
                }
            }

            return cmds.Count == 1 ? cmds[0] : new CompositeCommand("Move group", cmds);
        }

        private static void RefreshHighlights()
        {
            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();
        }

        private void SaveDragMaterial()
        {
            var renderer = _target!.GetComponent<MeshRenderer>();
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

            // Красный = деталь нарушает правила (пересекается с другой или повисла в
            // воздухе) и при включённой блокировке не встанет, а откатится на старт.
            // Зелёный = размещение допустимо. Так цвет совпадает с реальным исходом.
            _dragTintMaterial.color = MoveSetCausesViolation()
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

        private Plane GetDragPlane(KitchenElement target)
        {
            if (!_targetIsWallOpening)
                return new Plane(Vector3.up, target.transform.position);

            if (_dragWall == null)
                _dragWall = FindAttachedWall(target);

            if (_dragWall != null)
            {
                var wallEl = _dragWall.GetComponent<KitchenElement>();
                if (wallEl == null)
                {
                    Debug.LogWarning($"[ElementMover] Wall '{_dragWall.name}' is missing KitchenElement. " +
                        $"'{target.name}' drags on horizontal plane.");
                }
                else
                {
                    var dims = wallEl.DimensionsMM;
                    var wt = _dragWall.transform;
                    Vector3 normal = (dims.x <= dims.z) ? wt.right : wt.forward;
                    normal.y = 0f;
                    if (normal.sqrMagnitude > 1e-8f)
                        return new Plane(normal.normalized, target.transform.position);
                }
            }
            return new Plane(Vector3.up, target.transform.position);
        }

        private static Wall? FindAttachedWall(KitchenElement element)
        {
            if (element == null) return null;
            var win = element as WindowElement;
            var door = element as DoorElement;
            var name = win != null ? win.AttachedWallName
                     : door != null ? door.AttachedWallName : "";
            if (string.IsNullOrEmpty(name)) return null;
            foreach (var el in PartRegistry.GetAll())
            {
                if (el == null) continue;
                var wall = el.GetComponent<Wall>();
                if (wall != null && wall.gameObject.name == name)
                    return wall;
            }
            return null;
        }
    }
}
