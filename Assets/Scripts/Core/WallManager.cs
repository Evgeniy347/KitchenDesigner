using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Управляет стенами: показ/скрытие (настройка WallsEnabled) и опускание
    /// ближних стён до 100 мм в режиме обзора (LowerNearWalls, логика The Sims).
    /// Во время перетаскивания стены поднимаются на полную высоту, чтобы
    /// опущенная геометрия не ломала валидацию связности.</summary>
    public class WallManager : MonoBehaviour
    {
        private const float LoweredHeightMM = 100f;

        private void LateUpdate()
        {
            var s = KitchenSettings.Instance;
            bool show = s == null || s.WallsEnabled;
            bool lowerMode = s != null && s.LowerNearWalls && !ElementMover.IsDragging;

            var cam = Camera.main;
            Vector3 camF = cam != null ? cam.transform.forward : Vector3.forward;
            Vector3 sceneCenter = Vector3.zero; // центр пола
            float loweredUnits = LoweredHeightMM * AppConstants.MM_TO_UNITS;

            foreach (var e in BoardRegistry.GetAll())
            {
                if (e == null) continue;
                var wall = e.GetComponent<Wall>();
                if (wall == null) continue;

                var renderer = e.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.enabled = show;

                if (!show)
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
