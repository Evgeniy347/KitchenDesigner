using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.Measure
{
    [DefaultExecutionOrder(RunsBeforeSelectionAndMove)]
    public class MeasureController : MonoBehaviour
    {
        public const int RunsBeforeSelectionAndMove = -50;

        public static MeasureController? Instance { get; private set; }

        public const float VertexPickRadiusPx = 18f;
        public const float SegmentPickRadiusPx = 10f;

        public Vector3? Hint { get; internal set; }
        public Vector3? PlaneHint { get; internal set; }
        public Vector3? Anchor { get; internal set; }
        public Vector3? PreviewEnd { get; internal set; }
        public MeasureSegment? Hovered { get; private set; }

        public bool HasPreview => Anchor.HasValue && PreviewEnd.HasValue;

        private readonly List<Vector3> _worldVerts = new List<Vector3>();
        private readonly List<Vector2> _screenVerts = new List<Vector2>();

        internal IReadOnlyList<Vector3> CandidateVerticesWorld => _worldVerts;

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!MeasureMode.Active)
            {
                ResetState();
                return;
            }

            var cam = Camera.main;
            if (cam == null) return;

            if (Input.GetKeyDown(KeyCode.Escape)) CancelOneStep();

            Vector2 mouse = Input.mousePosition;
            CollectVertices(cam);
            UpdateHint(mouse);
            UpdatePlaneHit(cam, mouse);
            UpdatePreview(cam, mouse);
            UpdateHover(cam, mouse);

            if (Input.GetMouseButtonDown(0) && !PointerOverUI())
                HandleClick();
        }

        internal void CancelOneStep()
        {
            if (Anchor.HasValue) Anchor = null;
            else if (MeasureStore.Selected != null) MeasureStore.Select(null);
            else MeasureMode.SetActive(false);
        }

        internal void CollectVertices(Camera cam)
        {
            _worldVerts.Clear();
            _screenVerts.Clear();

            var all = PartRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                var e = all[i];
                if (!MeasurableContent(e)) continue;

                var verts = e.GetVertices();
                for (int v = 0; v < verts.Length; v++)
                {
                    Vector3 screen = cam.WorldToScreenPoint(verts[v]);
                    bool behindTheCamera = screen.z <= 0f;
                    if (behindTheCamera) continue;
                    _worldVerts.Add(verts[v]);
                    _screenVerts.Add(new Vector2(screen.x, screen.y));
                }
            }
        }

        private static bool MeasurableContent(KitchenElement e) =>
            e != null
            && e.gameObject.activeInHierarchy
            && e.GetComponent<BasePlate>() == null
            && SceneVisibility.AnyRendererEnabled(e);

        internal void UpdateHint(Vector2 mouse)
        {
            int idx = MeasureGeometry.NearestIndex(_screenVerts, mouse, VertexPickRadiusPx);
            Hint = idx >= 0 ? _worldVerts[idx] : (Vector3?)null;

            if (SuggestsTheAnchorItself(Hint)) Hint = null;
        }

        private bool SuggestsTheAnchorItself(Vector3? hint) =>
            hint.HasValue && Anchor.HasValue
            && (hint.Value - Anchor.Value).sqrMagnitude < Tolerance.EpsilonSqr;

        internal void UpdatePreview(Camera cam, Vector2 mouse)
        {
            if (!Anchor.HasValue)
            {
                PreviewEnd = null;
                return;
            }

            if (Hint.HasValue)
            {
                PreviewEnd = Hint;
                return;
            }

            if (PlaneHint.HasValue)
            {
                PreviewEnd = MeasureGeometry.ProjectOnDominantAxis(Anchor.Value, PlaneHint.Value);
                return;
            }

            PreviewEnd = FreeEndInMidAirOnTheCameraPlane(cam, mouse, Anchor.Value);
        }

        internal void UpdatePlaneHit(Camera cam, Vector2 mouse)
        {
            PlaneHint = null;
            Ray ray = cam.ScreenPointToRay(mouse);
            if (!Physics.Raycast(ray, out RaycastHit hit)) return;
            if (hit.collider.GetComponentInParent<BasePlate>() != null) return;
            PlaneHint = hit.point;
        }

        private static Vector3? FreeEndInMidAirOnTheCameraPlane(Camera cam, Vector2 mouse, Vector3 anchor)
        {
            var plane = new Plane(cam.transform.forward, anchor);
            Ray ray = cam.ScreenPointToRay(mouse);
            if (!plane.Raycast(ray, out float enter)) return null;
            return MeasureGeometry.ProjectOnDominantAxis(anchor, ray.GetPoint(enter));
        }

        internal void UpdateHover(Camera cam, Vector2 mouse)
        {
            Hovered = null;
            if (PlacingAPointTakesPriority) return;

            float best = SegmentPickRadiusPx;
            foreach (var seg in MeasureStore.Segments)
            {
                Vector3 sa = cam.WorldToScreenPoint(seg.A);
                Vector3 sb = cam.WorldToScreenPoint(seg.B);
                if (sa.z <= 0f || sb.z <= 0f) continue;

                float d = MeasureGeometry.DistancePointToSegmentPx(
                    new Vector2(sa.x, sa.y), new Vector2(sb.x, sb.y), mouse);
                if (d <= best)
                {
                    best = d;
                    Hovered = seg;
                }
            }
        }

        private bool PlacingAPointTakesPriority =>
            Hint.HasValue || PlaneHint.HasValue || Anchor.HasValue;

        internal void HandleClick()
        {
            if (Anchor.HasValue)
            {
                CommitSecondEnd(Anchor.Value);
                return;
            }

            if (Hint.HasValue)
            {
                DropAnchorAt(Hint.Value);
                return;
            }

            if (PlaneHint.HasValue)
            {
                DropAnchorAt(PlaneHint.Value);
                return;
            }

            MeasureStore.Select(Hovered);
        }

        private void CommitSecondEnd(Vector3 anchor)
        {
            if (Hint.HasValue)
                MeasureStore.Add(new MeasureSegment(anchor, Hint.Value));
            else if (PlaneHint.HasValue && PreviewEnd.HasValue)
                MeasureStore.Add(new MeasureSegment(anchor, PreviewEnd.Value));
            Anchor = null;
            PreviewEnd = null;
        }

        private void DropAnchorAt(Vector3 point)
        {
            Anchor = point;
            MeasureStore.Select(null);
        }

        private void ResetState()
        {
            Hint = null;
            PlaneHint = null;
            Anchor = null;
            PreviewEnd = null;
            Hovered = null;
        }

        private static bool PointerOverUI() =>
            UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
    }
}
