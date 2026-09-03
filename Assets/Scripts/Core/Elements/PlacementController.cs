using UnityEngine;

namespace KitchenDesigner.Core
{
    public class PlacementController : MonoBehaviour
    {
        public static PlacementController? Instance { get; private set; }

        public static bool IsActive => Instance != null && Instance._pending != null;

        private KitchenElement? _pending;
        private GameObject? _pendingGo;

        private void Awake() => Instance = this;

        public void Begin(KitchenElement element)
        {
            if (element == null) return;
            Cancel();
            _pending = element;
            _pendingGo = element.gameObject;
            SelectionManager.Instance?.DeselectAll();
            MoveToCursor();
        }

        private void Update()
        {
            if (_pending == null) return;

            if (_pendingGo == null || !_pendingGo.activeInHierarchy)
            {
                _pending = null;
                _pendingGo = null;
                return;
            }

            MoveToCursor();

            if (Input.GetMouseButtonDown(1)) { Cancel(); return; }

            if (Input.GetMouseButtonDown(0))
            {
                if (PointerOverUI) { Cancel(); return; }
                Commit();
            }
        }

        private void MoveToCursor()
        {
            var pending = _pending;
            var cam = Camera.main;
            if (cam == null || pending == null) return;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            var ground = new Plane(Vector3.up, Vector3.zero);
            Vector3 point = ground.Raycast(ray, out float enter)
                ? ray.GetPoint(enter)
                : cam.transform.position + cam.transform.forward * 2f;

            point.y = pending is LightSourceElement || pending is SinkElement
                || pending is CooktopElement || pending is IKeepsPlacementHeight
                ? pending.transform.position.y
                : pending.DimensionsMM.y * 0.5f * AppConstants.MM_TO_UNITS;

            Vector3 pos = GridManager.SnapToGridXZ(point);

            var others = PartRegistry.GetAll();
            others.Remove(pending);
            var snap = SnapSystem.TrySnap(pending, others, pos);
            pending.transform.position = WorldBounds.Clamp(snap.snapped ? snap.position : pos);

            if (pending is CooktopElement cooktop) cooktop.SnapToPart();
            if (pending is SinkElement sink) sink.SnapToPart();

            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.ApplyForElement(pending);
        }

        private void Commit()
        {
            var go = _pendingGo;
            var element = _pending;
            _pending = null;
            _pendingGo = null;
            if (go == null || element == null) return;

            CommandStack.Execute(new CreateCommand(go));
            SelectionManager.Instance?.Select(element);
            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();
        }

        public void Cancel()
        {
            var go = _pendingGo;
            _pending = null;
            _pendingGo = null;
            if (go == null) return;

            var el = go.GetComponent<KitchenElement>();
            if (el is WindowElement win) win.UnregisterFromWall();
            if (el is DoorElement door) door.UnregisterFromWall();
            if (el is SinkElement sink) sink.UnregisterFromPart();
            if (el is CooktopElement cooktop) cooktop.UnregisterFromPart();

            go.SetActive(false);
            if (el != null) PartRegistry.Unregister(el);

            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();
        }

        private static bool PointerOverUI =>
            UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
    }
}
