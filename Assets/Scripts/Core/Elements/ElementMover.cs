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
        private AxisLock _axisLock = AxisLock.None;

        // ЛКМ нажата на детали, но ещё не решено клик это или drag.
        private bool _pressed;
        private Vector2 _pressMouse;
        // Пока курсор не сместится дальше этого порога (в пикселях) — это клик
        // (откроется контекстное меню), а не перетаскивание. Только после порога
        // включается drag с зелёной/красной тонировкой.
        private const float DragStartPixels = 6f;

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

            _target = element;
            _pressed = true;
            _pressMouse = Input.mousePosition;
            _startPosition = element.transform.position;
            _startRotation = element.transform.rotation;
            _wasMoved = false;
            _wasShift = false;

            Plane dragPlane = new Plane(Vector3.up, _startPosition);
            _offset = dragPlane.Raycast(ray, out float enter)
                ? _startPosition - ray.GetPoint(enter)
                : Vector3.zero;
        }

        private bool PressMovedEnough()
        {
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
            RecomputeOffset();
            SaveDragMaterial(); // зелёная/красная тонировка появляется только здесь
        }

        private void RecomputeOffset()
        {
            if (Camera.main == null) return;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            Plane dragPlane = new Plane(Vector3.up, _startPosition);
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
            HandleDuplicate();
            HandleDelete();
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
                else if (_pressed)
                    OpenContextMenuForTarget(); // клик без перетаскивания → меню настроек
                _pressed = false;
            }
        }

        private void CancelDrag()
        {
            _showGhost = false;
            _axisLock = AxisLock.None;
            RevertMoveSet();
            RestoreDragMaterial();
            IsDragging = false;
            _wasMoved = false;
            _pressed = false;
            _movingSet.Clear();
            RefreshHighlights();
        }

        private void OpenContextMenuForTarget()
        {
            if (_target == null) return;
            // В режиме редактирования модуля детали настраиваются поштучно — меню
            // открывается; вне режима у связанной группы своё меню (ПКМ).
            bool editingInModule = ModuleEditMode.IsActive && ModuleEditMode.IsEditable(_target);
            if (!editingInModule && GroupManager.GroupOf(_target) != null) return;
            if (UI.UIManager.Instance != null)
                UI.UIManager.Instance.OpenContextMenu(_target);
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

            if (_dragTintMaterial == null) SaveDragMaterial(); // на случай, если drag начат не из BeginDrag

            if (Input.GetKeyDown(KeyCode.X)) _axisLock = _axisLock == AxisLock.X ? AxisLock.None : AxisLock.X;
            if (Input.GetKeyDown(KeyCode.Z)) _axisLock = _axisLock == AxisLock.Z ? AxisLock.None : AxisLock.Z;
            if (_axisLock == AxisLock.X) { newPos.z = _startPosition.z; newPos.y = _startPosition.y; }
            else if (_axisLock == AxisLock.Z) { newPos.x = _startPosition.x; newPos.y = _startPosition.y; }

            var others = PartRegistry.GetAll();
            if (_moveSet.Count > 1) others.RemoveAll(e => _moveSet.Contains(e));
            var snap = SnapSystem.TrySnap(_target, others, newPos);
            _target.transform.position = snap.snapped ? snap.position : newPos;

            // Групповое перемещение: остальные следуют за схваченным на ту же дельту.
            if (_moveSet.Count > 1)
                ApplyDelta(_moveSet, _moveStart, _target.transform.position - _startPosition);

            // Ghost-preview: полупрозрачная деталь в позиции снэпа.
            if (snap.snapped && snap.position != _target.transform.position)
            {
                _showGhost = true;
                _ghostPosition = snap.position;
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
				AutoAdjustPillar();
				if (KitchenSettings.Instance.BlockOnViolation && MoveSetCausesViolation())
					RevertMoveSet();
				else
					CommandStack.Execute(BuildMoveCommand());
			}
			else
			{
				RevertMoveSet();
			}

			_axisLock = AxisLock.None;
			RestoreDragMaterial();
			IsDragging = false;
			_wasShift = false;
			_movingSet.Clear();
			RefreshHighlights();
		}

		private void AutoAdjustPillar()
		{
			if (!(_target is PillarElement pillar)) return;
			float toU = AppConstants.MM_TO_UNITS;
			float pillarBottomY = _target.transform.position.y - pillar.TotalHeightMM * 0.5f * toU;
			if (pillarBottomY > 0.02f) return;
			float pillarTopY = _target.transform.position.y + pillar.TotalHeightMM * 0.5f * toU;
			float minAbove = pillarTopY;
			float maxAbove = pillarTopY + (130f + PillarElement.TopHeightMM + PillarElement.BottomHeightMM) * toU;
			float midDiameterU = PillarElement.MidDiameterMM * 0.5f * toU;
			Vector3 pillarCenter = _target.transform.position;
			Vector3 up = Vector3.up;
			KitchenElement bestAbove = null;
			float bestAboveBottom = float.MaxValue;
			foreach (var el in PartRegistry.GetAll())
			{
				if (el == null || el == _target) continue;
				var aabb = ComputeElementAABB(el);
				if (aabb.minY >= minAbove && aabb.minY <= maxAbove)
				{
					float dx = Mathf.Abs(aabb.minX + aabb.maxX * 0.5f - pillarCenter.x);
					float dz = Mathf.Abs(aabb.minZ + aabb.maxZ * 0.5f - pillarCenter.z);
					if (dx < midDiameterU + 0.05f && dz < midDiameterU + 0.05f)
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
			int gapMM = Mathf.RoundToInt(gapUnits / toU);
			int neededMid = gapMM - PillarElement.TopHeightMM - PillarElement.BottomHeightMM;
			neededMid = Mathf.Clamp(neededMid, PillarElement.MidHeightMM_Min, PillarElement.MidHeightMM_Max);
			pillar.MidHeightMM = neededMid;
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

        // Проверяет, есть ли нарушения среди перемещаемой детали и её соседей
        // (в радиусе snapThreshold * 2). Это предотвращает ситуацию, когда
        // движение детали B разрывает связь детали A с полом — и это остаётся
        // незамеченным. При этом чужая ошибка вдали не блокирует перемещение.
        private bool MovedCausesViolation()
        {
            var list = PartRegistry.GetAll();
            var result = ConstraintValidator.Validate(list);
            if (!result.isValid)
            {
                float radius = KitchenSettings.Instance.SnapThreshold * 2f * AppConstants.MM_TO_UNITS;
                foreach (var v in result.violations)
                {
                    if (v == _target) return true;
                    float dist = Vector3.Distance(v.transform.position, _target!.transform.position);
                    if (dist <= radius) return true;
                }
            }
            return false;
        }

        // Как MovedCausesViolation, но для всего перемещаемого набора.
        private bool MoveSetCausesViolation()
        {
            var list = PartRegistry.GetAll();
            var result = ConstraintValidator.Validate(list);
            if (result.isValid) return false;

            float radius = KitchenSettings.Instance.SnapThreshold * 2f * AppConstants.MM_TO_UNITS;
            foreach (var v in result.violations)
            {
                foreach (var m in _moveSet)
                {
                    if (v == m) return true;
                    if (m != null && Vector3.Distance(v.transform.position, m.transform.position) <= radius)
                        return true;
                }
            }
            return false;
        }

        private IUndoCommand BuildMoveCommand()
        {
            var cmds = new List<IUndoCommand>();
            for (int i = 0; i < _moveSet.Count; i++)
            {
                var m = _moveSet[i];
                if (m == null) continue;
                var rotBefore = m == _target ? _startRotation : m.transform.rotation;
                cmds.Add(new MoveCommand(m, _moveStart[i], m.transform.position, rotBefore, m.transform.rotation));
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
    }
}
