using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Управляет стенами: показ/скрытие (настройка WallsEnabled) и опускание
    /// ближних стён до 100 мм в режиме обзора (LowerNearWalls, логика The Sims).
    /// Опускание сохраняется и во время перетаскивания объектов (чтобы стены не
    /// «выскакивали» при движении), кроме самой перемещаемой стены — её не опускаем,
    /// иначе опускание дралось бы с drag за позицию по Y.</summary>
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
            var s = KitchenSettings.Instance;
            bool show = s == null || s.WallsEnabled;
            bool lowerMode = s != null && s.LowerNearWalls;

            Vector3 camF = _cachedCamera != null ? _cachedCamera.transform.forward : Vector3.forward;
            Vector3 sceneCenter = Vector3.zero; // центр пола
            float loweredUnits = LoweredHeightMM * AppConstants.MM_TO_UNITS;

            foreach (var e in PartRegistry.GetAll())
            {
                if (e == null) continue;
                var wall = e.GetComponent<Wall>();
                if (wall == null) continue;

                var renderer = e.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.enabled = show;

                // Стену, которую сейчас перетаскивают или ресайзят, держим на полной
                // высоте (иначе изменение геометрии ломает drag/прилипание к полу).
                if (!show || ElementMover.IsMoving(e) || ResizeHandleManager.IsResizingElement(e))
                {
                    wall.RestoreFull();
                    continue;
                }

                bool lower = lowerMode && WallCutaway.ShouldLower(e.transform.position, sceneCenter, camF);
                wall.SetLowered(lower, loweredUnits);
            }
        }
    }
}
