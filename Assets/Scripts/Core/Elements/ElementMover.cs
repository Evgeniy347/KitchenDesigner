using System.Collections.Generic;
using UnityEngine;
using KitchenDesigner.Core.UI;
using KitchenDesigner.Core.Update;

namespace KitchenDesigner.Core
{
    public class ElementMover : MonoBehaviour
    {
        public static bool IsDragging { get; private set; }

        private static readonly HashSet<KitchenElement> _movingSet = new HashSet<KitchenElement>();
        public static bool IsMoving(KitchenElement e) => e != null && _movingSet.Contains(e);

        private KitchenElement? _target;
        private Vector3 _offset;
        private Vector3 _startPosition;
        private Quaternion _startRotation;
        private Vector3Int _startDimensions;
        private bool _wasMoved;
        private bool _wasShift;
        private float _vOffset;
        private float _heldDragY;
        private DragAxisLock _axisLock = DragAxisLock.None;
        private Wall? _dragWall;
        private bool _targetIsWallOpening;
        private SnapCursor _dragCursor;
        private readonly List<PipeRunHold> _pipeHold = new List<PipeRunHold>();

        private bool _pressed;
        private Vector2 _pressMouse;
        private float _pressTime;

        private readonly List<DragPaint> _dragPaint = new List<DragPaint>();

        private sealed class DragPaint
        {
            public DragPaint(MeshRenderer renderer, Material material, Material painted)
            {
                this.renderer = renderer;
                this.material = material;
                this.painted = painted;
            }

            public readonly MeshRenderer renderer;
            public readonly Material material;
            public readonly Material painted;
        }

        private Mesh? _ghostMesh;
        private Material? _ghostMaterial;
        private Vector3? _ghostPosition;
        private Quaternion _ghostRotation;
        private bool _showGhost;

        private readonly List<KitchenElement> _moveSet = new List<KitchenElement>();
        private readonly List<Vector3> _moveStart = new List<Vector3>();
        private readonly List<KitchenElement> _lonelyTarget = new List<KitchenElement>();
        private SceneViolations _violationsAtDragStart = SceneViolations.Empty;
        private DragFrameRepeat _settledFrame;

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
            DestroyNow.The(_ghostMaterial);
        }

        private void CreateGhostMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return;
            _ghostMaterial = TransparentMaterial.Make(shader, new Color(0.3f, 0.6f, 1f, 0.2f));
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
        private bool CtrlHeld => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        private bool _wasCtrl;

        private static bool PointerOverUI =>
            UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

        private void TryBeginPress()
        {
            _pressed = false;
            if (AltHeld || PointerOverUI) return;
            if (Camera.main == null) return;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            var element = SelectionManager.RaycastTransparentAware(ray, ShiftHeld);
            if (element == null) return;
            if (element.GetComponent<BasePlate>() != null) return;
            if (!ModuleEditMode.IsEditable(element)) return;
            if (!EditModeManager.IsInteractable(element)) return;

            _target = element;
            _pressed = true;
            _pressMouse = Input.mousePosition;
            _pressTime = Time.unscaledTime;
            _startPosition = element.transform.position;
            _startDimensions = element.DimensionsMM;
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

        private bool PressMovedEnough() => DragGesture.PressBecameDrag(
            _pressMouse, _pressTime, Input.mousePosition, Time.unscaledTime);

        private void BeginDrag()
        {
            if (_target == null) return;
            BeginDragOn(_target);
            RecomputeOffset();
            SaveDragMaterial(_target!);
        }

        internal void BeginDragOn(KitchenElement target)
        {
            if (target == null) return;
            _target = target;
            _violationsAtDragStart = SceneViolations.OfScene();
            _settledFrame.Forget();
            IsDragging = true;
            _wasMoved = true;
            BuildMoveSet();
            _startPosition = target.transform.position;
            _startDimensions = target.DimensionsMM;
            _startRotation = target.transform.rotation;
            _heldDragY = _startPosition.y;
            HoldAttachedPipes();
        }

        internal void FinishDragNow() => FinishDrag();

        private void HoldAttachedPipes()
        {
            _pipeHold.Clear();
            if (!(_target is PipeFittingElement fitting)) return;

            var scene = PartRegistry.GetAll();
            scene.RemoveAll(e => e != _target && _moveSet.Contains(e));
            PipeRunFollow.Hold(fitting, scene, _pipeHold);
        }

        private int FollowHeldPipes(List<KitchenElement>? followed = null)
        {
            if (_pipeHold.Count == 0) return 0;
            if (!(_target is PipeFittingElement fitting)) return 0;
            return PipeRunFollow.FollowAll(fitting, _pipeHold, followed);
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

        private void BuildMoveSet()
        {
            _moveSet.Clear();
            _moveStart.Clear();

            var sel = SelectionManager.Instance;
            bool group = sel != null && sel.IsSelected(_target!) && sel.SelectedElements.Count > 1;
            if (group)
            {
                foreach (var e in sel!.SelectedElements)
                    if (e != null && e.Transformable && ModuleEditMode.IsEditable(e)) _moveSet.Add(e);
            }
            if (_moveSet.Count == 0)
                _moveSet.Add(_target!);

            AttachMove.ExpandWithDescendants(_moveSet);

            foreach (var e in _moveSet) _moveStart.Add(GrabStart(e));

            _movingSet.Clear();
            foreach (var e in _moveSet) _movingSet.Add(e);
        }

        public static Vector3 GrabStart(KitchenElement e)
        {
            if (e == null) return Vector3.zero;
            var wall = e.GetComponent<Wall>();
            if (wall != null) wall.RestoreFull();
            return e.transform.position;
        }

        public static void ApplyDelta(IList<KitchenElement> members, IList<Vector3> starts, Vector3 delta)
        {
            for (int i = 0; i < members.Count; i++)
                if (members[i] != null) members[i].transform.position = starts[i] + delta;
        }

        private void RevertMoveSet()
        {
            PipeRunFollow.Release(_pipeHold);
            if (_target != null)
            {
                if (_target.DimensionsMM != _startDimensions) _target.DimensionsMM = _startDimensions;
                _target.transform.rotation = _startRotation;
            }

            if (_moveSet.Count == 0)
            {
                if (_target != null) _target.transform.position = _startPosition;
                return;
            }
            ApplyDelta(_moveSet, _moveStart, Vector3.zero);
        }

        private void Update()
        {
            if (PlacementController.IsActive)
                return;

            if (Tools.ToolMode.MouseCaptured)
                return;

            if (!CameraController.IsTypingInInputField())
            {
                HandleDuplicate();
                HandleDelete();
            }
            HandleDragInput();
        }

        private void HandleDuplicate()
        {
            if (!Input.GetKeyDown(KeyCode.D) || IsDragging) return;
            if (!CtrlHeld) return;

            var sel = SelectionManager.Instance;
            var sources = DuplicateSources(sel);
            if (sources.Count == 0) return;

            var copies = GroupDuplicate.Of(sources,
                ElementFactoryInstance.DuplicateOffsetForCurrentView(), out var command);
            if (command == null) return;

            CommandStack.Execute(command);
            if (sel != null) sel.SelectOnly(copies);
        }

        private readonly List<KitchenElement> _duplicateSources = new List<KitchenElement>();

        private List<KitchenElement> DuplicateSources(SelectionManager? sel)
        {
            _duplicateSources.Clear();
            if (sel != null && sel.SelectedElements.Count > 1)
            {
                foreach (var e in sel.SelectedElements)
                    if (e != null) _duplicateSources.Add(e);
                return _duplicateSources;
            }
            if (_target != null) _duplicateSources.Add(_target);
            return _duplicateSources;
        }

        private void HandleDelete()
        {
            if (IsDragging) return;
            if (!Input.GetKeyDown(KeyCode.Delete)) return;

            var sel = SelectionManager.Instance;
            if (sel == null) return;

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
            if (ResizeHandleManager.IsResizing) return;
            if (TextureOverlayHandles.Active && TextureOverlayHandles.PointerOverHandle()) return;

            if (Input.GetKeyDown(KeyCode.Escape) && IsDragging)
            {
                TryCancelDragFromEscape();
                return;
            }

            if (Input.GetMouseButtonDown(0) && !GizmoPressGuard.BlocksPress(
                    ResizeHandleManager.IsResizing,
                    ResizeHandleManager.PointerOverHandle(),
                    TextureOverlayHandles.Active,
                    TextureOverlayHandles.PointerOverHandle(),
                    GroupHandleManager.IsDragging,
                    GroupHandleManager.PointerOverHandle()))
                TryBeginPress();

            if (_pressed && Input.GetMouseButton(0))
            {
                if (!IsDragging && PressMovedEnough())
                {
                    if (_target != null && _target.Transformable) BeginDrag();
                    else _pressed = false;
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

        internal void TryCancelDragFromEscape()
        {
            if (!OwnsEscape()) return;
            CancelDrag();
        }

        private static bool OwnsEscape() =>
            EscapeOwnership.Resolve(new EscapeClaims { Dragging = IsDragging }) == EscapeOwner.ElementDrag;

        private void CancelDrag()
        {
            _settledFrame.Forget();
            _showGhost = false;
            _axisLock = DragAxisLock.None;
            _dragWall = null;
            _targetIsWallOpening = false;
            RevertMoveSet();
            _pipeHold.Clear();
            RestoreDragMaterial();
            IsDragging = false;
            _wasMoved = false;
            _pressed = false;
            _wasCtrl = false;
            _movingSet.Clear();
            RefreshHighlights();
        }

        private void UpdateDrag()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            _dragCursor = SnapCursor.AlongRay(ray.origin, ray.direction);
            Vector3 newPos = _target!.transform.position;
            bool computed = false;

            if (ShiftHeld)
            {
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
                    _heldDragY = newPos.y;
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
                        point.y = _heldDragY;
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

            ApplyDragFrame(newPos);
        }

        internal void DragFrameOn(Vector3 candidatePosition) => ApplyDragFrame(candidatePosition);

        private void ApplyDragFrame(Vector3 newPos)
        {
            if (_target == null) return;
            if (_dragPaint.Count == 0) SaveDragMaterial(_target!);

            if (Input.GetKeyDown(KeyCode.X)) _axisLock = DragGesture.Toggle(_axisLock, DragAxisLock.X);
            if (Input.GetKeyDown(KeyCode.Z)) _axisLock = DragGesture.Toggle(_axisLock, DragAxisLock.Z);
            newPos = DragGesture.ApplyAxisLock(newPos, _axisLock, _startPosition, _heldDragY,
                keepsItsOwnHeight: !_targetIsWallOpening);

            var settings = KitchenSettings.Instance;
            bool globalSnap = settings != null && settings.SnapEnabled;
            bool effectiveSnap = DragGesture.SnapAppliesTo(globalSnap, CtrlHeld);
            if (CtrlHeld != _wasCtrl)
            {
                bool wasOn = globalSnap;
                UI.StatusBarUI.Instance?.ShowTransient(
                    wasOn ? "Прилипание отключено (Ctrl)" : "Прилипание включено (Ctrl)",
                    StatusLevel.Info);
                _wasCtrl = CtrlHeld;
            }

            if (_settledFrame.Repeats(newPos, SceneRevision.Version, effectiveSnap)) return;

            var others = PartRegistry.GetAll();
            if (_moveSet.Count > 1) others.RemoveAll(e => _moveSet.Contains(e));
            var snap = effectiveSnap
                ? SnapSystem.TrySnap(_target!, others, newPos)
                : default;
            var settled = WorldBounds.Clamp(snap.snapped ? snap.position : newPos);
            if (_target!.transform.position != settled) _target!.transform.position = settled;

            if (_moveSet.Count > 1)
                ApplyDelta(_moveSet, _moveStart, _target!.transform.position - _startPosition);

            FollowHeldPipes();

            if (DragGesture.GhostIsWorthShowing(snap.snapped, snap.position, newPos))
            {
                _showGhost = true;
                _ghostPosition = newPos;
                _ghostRotation = _target!.transform.rotation;
                if (_ghostMesh == null)
                {
                    var mf = _target!.GetComponent<MeshFilter>();
                    if (mf != null) _ghostMesh = mf.sharedMesh;
                }
            }
            else
            {
                _showGhost = false;
            }

            UpdateDragTint();

            _settledFrame.Remember(newPos, SceneRevision.Version, effectiveSnap);
        }

		private void FinishDrag()
		{
			_settledFrame.Forget();
			_showGhost = false;

			if (_wasMoved)
			{
				foreach (var m in _moveSet)
				{
					if (m == null) continue;
					MmGrid.Snap(m);
				}

				Vector3Int? seatedDimsBefore = null;
				Vector3 seatedPosBefore = Vector3.zero;
				if (_target is IAutoSeated seatedBefore)
				{
					seatedDimsBefore = _target.DimensionsMM;
					seatedPosBefore = _target.transform.position;
					seatedBefore.SeatAfterMove(SceneWithoutTheFollowingPipes(), _dragCursor);
				}

				if (_moveSet.Count <= 1 && _target is IStandsOnFloor standing)
					standing.SeatOnFloor(PartRegistry.GetAll());

				FollowHeldPipes();

				var refits = RefitMovedRuns();

				if (KitchenSettings.Instance.BlockOnViolation
					&& MoveSetIntroducedAViolation(out var refusal))
				{
					for (int i = refits.Count - 1; i >= 0; i--) refits[i].Undo();
					if (seatedDimsBefore.HasValue && _target != null)
						_target.DimensionsMM = seatedDimsBefore.Value;
					RevertMoveSet();
					EditRefusalReport.Show(refusal);
				}
				else
					CommandStack.Execute(
						BuildMoveCommand(seatedDimsBefore, seatedPosBefore, refits));
			}
			else
			{
				RevertMoveSet();
			}

			_pipeHold.Clear();
			_axisLock = DragAxisLock.None;
			RestoreDragMaterial();
			IsDragging = false;
			_wasShift = false;
			_wasCtrl = false;
			_dragWall = null;
			_targetIsWallOpening = false;
			_movingSet.Clear();
			RefreshHighlights();
		}

        private List<IUndoCommand> RefitMovedRuns()
        {
            var written = new List<IUndoCommand>();
            var scene = PartRegistry.GetAll();

            foreach (var m in _moveSet)
            {
                if (!(m is PipeElement pipe)) continue;

                var dimsBefore = pipe.DimensionsMM;
                var posBefore = pipe.transform.position;
                if (!PipeDocking.RefitRunAfterResize(pipe, scene)) continue;
                if (ReferenceEquals(pipe, _target)) continue;
                if (pipe.DimensionsMM == dimsBefore
                    && (pipe.transform.position - posBefore).sqrMagnitude
                       <= Tolerance.EpsilonSqr) continue;

                var rotation = pipe.transform.rotation;
                written.Add(new ResizeCommand(pipe, dimsBefore, pipe.DimensionsMM, posBefore,
                    pipe.transform.position, rotation, rotation));
            }

            return written;
        }

        private List<KitchenElement> SceneWithoutTheFollowingPipes()
        {
            var scene = PartRegistry.GetAll();
            if (_pipeHold.Count == 0) return scene;

            var following = new List<KitchenElement>();
            FollowHeldPipes(following);
            if (following.Count > 0) scene.RemoveAll(e => following.Contains(e));
            return scene;
        }

        private bool MoveSetIntroducedAViolation(out string refusal)
        {
            refusal = string.Empty;
            var after = SceneViolations.OfScene();
            if (after.IsClean) return false;

            float radius = KitchenSettings.Instance.SnapThreshold * 2f * AppConstants.MM_TO_UNITS;
            return EditGate.Refuses(_violationsAtDragStart, after,
                DraggedOrTarget(), radius, out refusal);
        }

        private List<KitchenElement> DraggedOrTarget()
        {
            if (_moveSet.Count > 0) return _moveSet;
            _lonelyTarget.Clear();
            if (_target != null) _lonelyTarget.Add(_target);
            return _lonelyTarget;
        }

        private IUndoCommand BuildMoveCommand(Vector3Int? seatedDimsBefore = null,
            Vector3 seatedPosBefore = default, List<IUndoCommand>? refits = null)
        {
            var cmds = new List<IUndoCommand>();
            for (int i = 0; i < _moveSet.Count; i++)
            {
                var m = _moveSet[i];
                if (m == null) continue;
                var rotBefore = m == _target ? _startRotation : m.transform.rotation;
                cmds.Add(new MoveCommand(m, _moveStart[i], m.transform.position, rotBefore, m.transform.rotation));
            }

            for (int i = 0; i < _pipeHold.Count; i++)
            {
                var hold = _pipeHold[i];
                if (hold.Pipe == null || !hold.Stirred) continue;
                var rotation = hold.Pipe.transform.rotation;
                cmds.Add(new ResizeCommand(hold.Pipe, hold.DimensionsBeforeMM,
                    hold.Pipe.DimensionsMM, hold.PositionBefore, hold.Pipe.transform.position,
                    rotation, rotation));
            }

            if (seatedDimsBefore.HasValue && _target is IAutoSeated)
            {
                var dimsAfter = _target.DimensionsMM;
                if (seatedDimsBefore.Value != dimsAfter)
                {
                    cmds.Add(new ResizeCommand(_target, seatedDimsBefore.Value, dimsAfter,
                        seatedPosBefore, _target.transform.position,
                        _target.transform.rotation, _target.transform.rotation));
                }
            }

            if (refits != null) cmds.AddRange(refits);

            return cmds.Count == 1 ? cmds[0] : new CompositeCommand("Move group", cmds);
        }

        private static void RefreshHighlights()
        {
            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();
        }

        internal void SaveDragMaterial(KitchenElement target)
        {
            if (target == null || _dragPaint.Count > 0) return;

            foreach (var renderer in ElementRenderers.BodyOf(target))
            {
                if (renderer == null) continue;
                var material = renderer.sharedMaterial;
                if (material == null) continue;

                var painted = TransparentMaterial.Make(material, DragGesture.AllowedTint);
                painted.name = ElementTint.DragName;
                renderer.material = painted;
                _dragPaint.Add(new DragPaint(renderer, material, painted));
            }
        }

        private void UpdateDragTint()
        {
            if (_dragPaint.Count == 0 || _target == null) return;

            var tint = DragGesture.TintFor(MoveSetIntroducedAViolation(out _));
            foreach (var paint in _dragPaint)
            {
                if (paint.painted == null) continue;
                paint.painted.color = tint;
            }
        }

        internal void RestoreDragMaterial()
        {
            foreach (var paint in _dragPaint)
            {
                if (paint.renderer == null || paint.material == null) continue;
                if (!ElementTint.Wears(paint.renderer, paint.painted, ElementTint.DragName)) continue;
                paint.renderer.material = paint.material;
            }

            foreach (var paint in _dragPaint)
                DestroyNow.The(paint.painted);

            _dragPaint.Clear();
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
