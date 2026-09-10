using UnityEngine;

namespace KitchenDesigner.Core
{
    public class WallManager : MonoBehaviour
    {
        public const float LoweredHeightMM = 100f;

        private Camera? _cachedCamera;

        private void Awake()
        {
            _cachedCamera = Camera.main;
        }

        public void LateUpdate()
        {
            using var _ = PerfMarkers.WallManagerLateUpdate.Auto();
            var view = ViewResolver.Current;
            bool show = view.WallsEnabled;
            bool lowerMode = view.LowerNearWalls;
            bool hideOpenings = lowerMode && view.HideOpeningsOnLoweredWalls;

            Vector3 camF = _cachedCamera != null ? _cachedCamera.transform.forward : Vector3.forward;
            Vector3 sceneCenter = Vector3.zero;
            float loweredUnits = LoweredHeightMM * AppConstants.MM_TO_UNITS;

            foreach (var wall in PartRegistry.Walls)
            {
                if (wall == null) continue;
                var e = wall.GetComponent<KitchenElement>();
                if (e == null) continue;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                _wallsVisited++;
#endif

                var renderer = e.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.enabled = show;

                var collider = e.GetComponent<Collider>();
                if (collider != null) collider.enabled = show || RoomMode;

                if (!show)
                {
                    wall.RestoreFull();
                    ApplyOpeningVisibility(wall, hidden: true);
                    continue;
                }

                if (ElementMover.IsMoving(e) || ResizeHandleManager.IsResizingElement(e))
                {
                    wall.RestoreFull();
                    ApplyOpeningVisibility(wall, hidden: false);
                    continue;
                }

                bool lower = lowerMode
                    && (view.LowerAllWalls
                        || WallCutaway.ShouldLower(e.transform.position, sceneCenter, camF));
                wall.SetLowered(lower, loweredUnits);
                ApplyOpeningVisibility(wall, hidden: lower && hideOpenings);
            }
        }

        public static bool RoomMode => EditModeManager.Mode == EditMode.Room;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static int _activeOpeningVisibilityApplications;

        public static int TakeActiveOpeningVisibilityApplications()
        {
            int n = _activeOpeningVisibilityApplications;
            _activeOpeningVisibilityApplications = 0;
            return n;
        }

        private static int _wallsVisited;

        public static int TakeWallsVisited()
        {
            int n = _wallsVisited;
            _wallsVisited = 0;
            return n;
        }
#endif

        private static void ApplyOpeningVisibility(Wall wall, bool hidden)
        {
            bool roomMode = RoomMode;
            if (!wall.OpeningVisibilityChanged(hidden, roomMode)) return;
            wall.MarkOpeningVisibilityApplied(hidden, roomMode);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _activeOpeningVisibilityApplications++;
#endif

            foreach (var w in wall.AttachedWindows)
                if (w != null)
                {
                    SceneVisibility.SetRenderersEnabled(w, !hidden);
                    var c = w.GetComponent<Collider>();
                    if (c != null) c.enabled = !hidden || RoomMode;
                }
            foreach (var d in wall.AttachedDoors)
                if (d != null)
                {
                    SceneVisibility.SetRenderersEnabled(d, !hidden);
                    var c = d.GetComponent<Collider>();
                    if (c != null) c.enabled = !hidden || RoomMode;
                }
        }
    }
}
