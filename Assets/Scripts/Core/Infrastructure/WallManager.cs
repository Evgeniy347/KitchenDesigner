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

            foreach (var e in PartRegistry.All)
            {
                if (e == null) continue;
                var wall = e.GetComponent<Wall>();
                if (wall == null) continue;

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

        private static void ApplyOpeningVisibility(Wall wall, bool hidden)
        {
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
