using UnityEngine;

namespace KitchenDesigner.Core
{
    public class PlacementController : MonoBehaviour
    {
        public static PlacementController? Instance { get; private set; }

        public static bool IsActive => Instance != null && Instance._pending != null;

        private KitchenElement? _pending;
        private GameObject? _pendingGo;

        private readonly System.Collections.Generic.List<KitchenElement> _others =
            new System.Collections.Generic.List<KitchenElement>();

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

            using var _ = PerfMarkers.PlacementMoveToCursor.Auto();

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            var ground = new Plane(Vector3.up, Vector3.zero);
            Vector3 point = ground.Raycast(ray, out float enter)
                ? ray.GetPoint(enter)
                : cam.transform.position + cam.transform.forward * 2f;

            point.y = pending is LightSourceElement || pending is SinkElement
                || pending is CooktopElement || pending is IKeepsPlacementHeight
                ? pending.transform.position.y
                : AppConstants.HalfHeightUnits(pending.DimensionsMM.y)
                    + GappedBox.BottomSkirtUnits(pending.Gaps);

            Vector3 pos = GridManager.SnapToGridXZ(point);

            var scene = PartRegistry.GetAll();
            _others.Clear();
            for (int i = 0; i < scene.Count; i++)
                if (!ReferenceEquals(scene[i], pending)) _others.Add(scene[i]);

            var snap = SnapSystem.TrySnap(pending, _others, pos);
            pending.transform.position = WorldBounds.Clamp(snap.snapped ? snap.position : pos);

            if (pending is PartCutoutElement cutout) cutout.SnapToPart();

            var highlighter = ElementHighlighter.Current;
            if (highlighter is not null) highlighter.ApplyForElement(pending, scene);
        }

        private void Commit()
        {
            var go = _pendingGo;
            var element = _pending;
            _pending = null;
            _pendingGo = null;
            if (go == null || element == null) return;

            ElementCreation.Commit(go);
            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();
        }

        public void Cancel()
        {
            var go = _pendingGo;
            _pending = null;
            _pendingGo = null;
            if (go == null) return;

            SceneMembership.Leave(go, go.GetComponent<KitchenElement>());

            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();
        }

        private static bool PointerOverUI =>
            UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
    }
}
