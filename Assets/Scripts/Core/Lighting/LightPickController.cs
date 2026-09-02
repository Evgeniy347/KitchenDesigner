using System.Collections.Generic;
using UnityEngine;
using KitchenDesigner.Core.Measure;

namespace KitchenDesigner.Core.Lighting
{
    [DefaultExecutionOrder(RunsBeforeSelectionAndMove)]
    public class LightPickController : MonoBehaviour
    {
        public const int RunsBeforeSelectionAndMove = MeasureController.RunsBeforeSelectionAndMove;

        public static LightPickController? Instance { get; private set; }

        public LightSourceElement? Hovered { get; private set; }

        public Vector3? CursorPoint { get; private set; }

        public Vector3? Anchor => LightPickMode.Source != null
            ? LightPickMode.Source.transform.position
            : (Vector3?)null;

        public bool HasPreview => Anchor.HasValue && CursorPoint.HasValue;

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!LightPickMode.Active)
            {
                ResetState();
                return;
            }

            var cam = Camera.main;
            if (cam == null) return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                LightPickMode.SetSource(null);
                ResetState();
                return;
            }

            Vector2 mouse = Input.mousePosition;
            UpdateHover(cam, mouse);
            UpdateCursorPoint(cam, mouse);

            if (Input.GetMouseButtonDown(0) && !PointerOverUI()) HandleClick();
        }

        internal void UpdateHover(Camera cam, Vector2 mouse)
        {
            Hovered = null;
            Ray ray = cam.ScreenPointToRay(mouse);
            if (!Physics.Raycast(ray, out RaycastHit hit)) return;
            Hovered = hit.collider.GetComponentInParent<LightSourceElement>();
        }

        internal void UpdateCursorPoint(Camera cam, Vector2 mouse)
        {
            if (Hovered != null)
            {
                CursorPoint = Hovered.transform.position;
                return;
            }

            var anchor = Anchor;
            if (!anchor.HasValue)
            {
                CursorPoint = null;
                return;
            }

            var plane = new Plane(cam.transform.forward, anchor.Value);
            Ray ray = cam.ScreenPointToRay(mouse);
            CursorPoint = plane.Raycast(ray, out float enter) ? ray.GetPoint(enter) : (Vector3?)null;
        }

        internal void HandleClick()
        {
            var source = LightPickMode.Source;
            if (source == null || Hovered == null) return;
            LinkLights.Add(source, Hovered.PartName);
        }

        internal static List<LightLinkSegment> VisibleLinks()
        {
            var segments = new List<LightLinkSegment>();
            foreach (var source in LightSwitchNetwork.AllSwitches())
                foreach (var lamp in LightSwitchNetwork.LightsOf(source))
                    segments.Add(new LightLinkSegment(source.transform.position,
                        lamp.transform.position, source == LightPickMode.Source));
            return segments;
        }

        private void ResetState()
        {
            Hovered = null;
            CursorPoint = null;
        }

        private static bool PointerOverUI() =>
            UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
    }
}
