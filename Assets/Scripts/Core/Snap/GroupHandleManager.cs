using System.Collections.Generic;
using UnityEngine;
using KitchenDesigner.Core.Handles;

namespace KitchenDesigner.Core
{
    [DefaultExecutionOrder(100)]
    public class GroupHandleManager : MonoBehaviour
    {
        public static GroupHandleManager? Instance { get; private set; }

        internal static readonly HandleMetrics Metrics = HandleMetrics.Resize;

        public static bool IsDragging { get; private set; }

        private readonly List<ResizeHandle> _handles = new List<ResizeHandle>();
        private readonly List<Vector3[]> _vertexSets = new List<Vector3[]>();
        private readonly Material?[] _axisMats = new Material?[3];
        private readonly GroupOutline _outline = new GroupOutline();

        private ElementMover? _mover;
        private ResizeHandle? _hovered;
        private Camera? _layoutCamera;
        private Face[]? _faces;
        private Vector3 _dragAxis;
        private Vector3 _dragAxisPoint;
        private Vector3 _dragStartPosition;
        private float _dragParam0;

        private void Awake() => Instance = this;

        private void Start()
        {
            for (int axis = 0; axis < _axisMats.Length; axis++)
                _axisMats[axis] = HandleMaterials.For(HandleMaterials.ForAxis(axis));

            _mover = FindAnyObjectByType<ElementMover>();
            if (_mover == null)
                Debug.LogError("[GroupHandles] No ElementMover in the scene - the group cannot be dragged");
        }

        private void OnDestroy()
        {
            IsDragging = false;
            ClearHandles();
            _outline.Dispose();
            if (Instance == this) Instance = null;
        }

        public static bool PointerOverHandle() =>
            Instance != null && Instance.PickUnderCursor(Instance._layoutCamera) != null;

        internal static GroupGizmoPlan PlanNow()
        {
            var selection = SelectionManager.Instance;
            int count = selection != null ? SelectableCount(selection) : 0;
            return GroupGizmoPlan.For(count,
                ResizeHandleManager.Mode == ResizeHandleManager.HandleMode.Resize);
        }

        private static int SelectableCount(SelectionManager selection)
        {
            int count = 0;
            foreach (var element in selection.SelectedElements)
                if (element != null && element.gameObject.activeInHierarchy) count++;
            return count;
        }

        private static bool Available() =>
            !Tools.ToolMode.MouseCaptured
            && !TextureOverlayHandles.Active
            && !PhotoMode.Active;

        private void LateUpdate()
        {
            if (!PlanNow().GroupMoveArrows || !Available())
            {
                if (IsDragging) EndDrag();
                Clear();
                return;
            }

            CollectVertexSets();
            if (!GroupBounds.Of(_vertexSets, out var center, out var size)) { Clear(); return; }

            _outline.Show(center, size);
            _faces = GroupBounds.FacesOf(center, size);

            var camera = Camera.main;
            if (_handles.Count == 0) BuildHandles();
            _layoutCamera = camera;
            HandleLayout.Place(_faces, _handles, camera, Metrics);
            if (!IsDragging) SetHover(PickUnderCursor(camera));
        }

        private void Update()
        {
            if (IsDragging)
            {
                if (!ElementMover.IsDragging) { StopTracking(); return; }
                if (Input.GetMouseButton(0)) DragFrame();
                if (Input.GetMouseButtonUp(0)) EndDrag();
                return;
            }

            if (!Input.GetMouseButtonDown(0)) return;
            if (HandleInput.AltHeld || HandleInput.PointerOverUI()) return;
            if (!Available()) return;

            var handle = PickUnderCursor(_layoutCamera);
            if (handle != null) BeginDrag(handle);
        }

        private ResizeHandle? PickUnderCursor(Camera? camera) =>
            HandleScreenPick.Nearest(HandleInput.MouseScreenPoint, camera, _handles, h => h.grabPoint);

        private void SetHover(ResizeHandle? handle)
        {
            if (handle == _hovered) return;
            HandleHover.Paint(_hovered, false);
            _hovered = handle;
            HandleHover.Paint(_hovered, true);
        }

        private void CollectVertexSets()
        {
            _vertexSets.Clear();
            var selection = SelectionManager.Instance;
            if (selection == null) return;

            foreach (var element in selection.SelectedElements)
            {
                if (element == null || !element.gameObject.activeInHierarchy) continue;
                _vertexSets.Add(element.GetVertices());
            }
        }

        private void BuildHandles()
        {
            for (int i = 0; i < Face.BoxFaceCount; i++)
            {
                var go = new GameObject("GroupHandle_" + i);
                var marker = go.AddComponent<ResizeHandle>();
                marker.faceIndex = i;
                HandleVisual.BuildArrow(go.transform, _axisMats[i / 2], Metrics,
                    HandleShaft.Cylinder, HandleTip.Cone);
                _handles.Add(marker);
            }
        }

        private void BeginDrag(ResizeHandle handle)
        {
            var camera = _layoutCamera;
            var selection = SelectionManager.Instance;
            var primary = selection != null ? selection.Selected : null;
            if (camera == null || _faces == null || _mover == null || primary == null) return;
            if (!primary.Transformable || !ModuleEditMode.IsEditable(primary)) return;
            if (handle.faceIndex < 0 || handle.faceIndex >= _faces.Length) return;

            var face = _faces[handle.faceIndex];
            Vector3 axis = face.normal.sqrMagnitude > Tolerance.EpsilonSqr
                ? face.normal.normalized
                : Vector3.forward;

            Ray ray = camera.ScreenPointToRay(Input.mousePosition);
            float start = HandleAxisRay.ParamAlongAxis(ray.origin, ray.direction, face.center, axis);
            if (float.IsNaN(start)) return;

            _dragAxis = axis;
            _dragAxisPoint = face.center;
            _dragParam0 = start;
            _dragStartPosition = primary.transform.position;
            _mover.BeginDragOn(primary);
            IsDragging = true;
        }

        private void DragFrame()
        {
            var camera = _layoutCamera;
            if (camera == null || _mover == null) return;

            Ray ray = camera.ScreenPointToRay(Input.mousePosition);
            float now = HandleAxisRay.ParamAlongAxis(ray.origin, ray.direction, _dragAxisPoint, _dragAxis);
            if (float.IsNaN(now)) return;

            _mover.DragFrameOn(_dragStartPosition + _dragAxis * (now - _dragParam0));
        }

        private void EndDrag()
        {
            if (ElementMover.IsDragging && _mover != null) _mover.FinishDragNow();
            StopTracking();
        }

        private void StopTracking()
        {
            IsDragging = false;
            SetHover(null);
        }

        private void Clear()
        {
            _outline.Hide();
            ClearHandles();
            _faces = null;
        }

        private void ClearHandles()
        {
            SetHover(null);
            foreach (var handle in _handles)
                if (handle != null) DestroyNow.The(handle.gameObject);
            _handles.Clear();
        }
    }
}
