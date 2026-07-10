using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Чистая логика выбора опускаемых стен (режим обзора «как в The Sims»).
    /// Отделена от рендера, чтобы покрываться юнит-тестами.</summary>
    public static class WallCutaway
    {
        /// <summary>Стена опускается, если она на ближней к камере стороне относительно
        /// центра сцены (между камерой и центром) — фронтальные стены/перегородки
        /// опускаются, дальние остаются. Все векторы мировые, учитывается горизонталь.</summary>
        public static bool ShouldLower(Vector3 wallCenter, Vector3 sceneCenter, Vector3 cameraForward)
        {
            Vector3 rel = wallCenter - sceneCenter;
            rel.y = 0f;
            Vector3 f = cameraForward;
            f.y = 0f;

            if (f.sqrMagnitude < 1e-6f || rel.sqrMagnitude < 1e-8f)
                return false;

            // rel ≈ -camForward → стена на стороне камеры (ближняя) → опускаем.
            return Vector3.Dot(rel.normalized, f.normalized) < -0.1f;
        }
    }
}
