using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Управляет стенами: показ/скрытие (настройка WallsEnabled) и опускание
    /// ближних стён до 100 мм в режиме обзора (LowerNearWalls, логика The Sims).
    /// Опускание сохраняется и во время перетаскивания объектов (чтобы стены не
    /// «выскакивали» при движении), кроме самой перемещаемой стены — её не опускаем,
    /// иначе опускание дралось бы с drag за позицию по Y.
    /// В режиме «помещение» опускание принудительно выключено: там правят саму
    /// конструкцию, и обрезанные стены мешали бы.
    /// Окна и двери опущенных стен прячутся отдельной настройкой
    /// (HideOpeningsOnLoweredWalls) — вырез в стене при этом сохраняется, потому
    /// что он часть меша стены, а не окна.</summary>
    public class WallManager : MonoBehaviour
    {
        private const float LoweredHeightMM = 100f;

        private Camera? _cachedCamera;

        private void Awake()
        {
            _cachedCamera = Camera.main;
        }

        public void LateUpdate()
        {
            using var _ = PerfMarkers.WallManagerLateUpdate.Auto();
            var s = KitchenSettings.Instance;
            // В фоторежиме стены всегда видимы и не опускаются — комната цельная.
            bool show = PhotoMode.Active || s == null || s.WallsEnabled;
            bool lowerMode = !PhotoMode.Active && !RoomMode && s != null && s.LowerNearWalls;
            bool hideOpenings = lowerMode && s != null && s.HideOpeningsOnLoweredWalls;

            Vector3 camF = _cachedCamera != null ? _cachedCamera.transform.forward : Vector3.forward;
            Vector3 sceneCenter = Vector3.zero; // центр пола
            float loweredUnits = LoweredHeightMM * AppConstants.MM_TO_UNITS;

            foreach (var e in PartRegistry.All)
            {
                if (e == null) continue;
                var wall = e.GetComponent<Wall>();
                if (wall == null) continue;

                var renderer = e.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.enabled = show;

                var collider = e.GetComponent<Collider>();
                if (collider != null) collider.enabled = show;

                // Стену, которую сейчас перетаскивают или ресайзят, держим на полной
                // высоте (иначе изменение геометрии ломает drag/прилипание к полу).
                if (!show || ElementMover.IsMoving(e) || ResizeHandleManager.IsResizingElement(e))
                {
                    wall.RestoreFull();
                    ApplyOpeningVisibility(wall, hidden: false);
                    continue;
                }

                bool lower = lowerMode && WallCutaway.ShouldLower(e.transform.position, sceneCenter, camF);
                wall.SetLowered(lower, loweredUnits);
                ApplyOpeningVisibility(wall, hidden: lower && hideOpenings);
            }
        }

        /// <summary>Режим «помещение»: стены всегда в полный рост независимо от
        /// настройки «опускать ближние стены».</summary>
        public static bool RoomMode => EditModeManager.Mode == EditMode.Room;

        private static void ApplyOpeningVisibility(Wall wall, bool hidden)
        {
            foreach (var w in wall.AttachedWindows)
                if (w != null) SceneVisibility.SetRenderersEnabled(w, !hidden);
            foreach (var d in wall.AttachedDoors)
                if (d != null) SceneVisibility.SetRenderersEnabled(d, !hidden);
        }
    }
}
