using System;
using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.Handles
{
    /// <summary>Ручка под курсором — по расстоянию НА ЭКРАНЕ (см. HandleScreenPickTests).</summary>
    public static class HandleScreenPick
    {
        public const float DefaultRadiusPixels = HandleScale.GrabRadiusPixels;

        public static T? Nearest<T>(Vector2 screenPoint, Camera? camera,
            IReadOnlyList<T> handles, Func<T, Vector3> grabPoint,
            float radiusPixels = DefaultRadiusPixels) where T : Component
        {
            if (camera == null || handles == null) return null;

            T? best = null;
            float bestDistance = radiusPixels;
            for (int i = 0; i < handles.Count; i++)
            {
                var handle = handles[i];
                if (handle == null) continue;
                var projected = camera.WorldToScreenPoint(grabPoint(handle));
                bool behindCamera = projected.z <= 0f;
                if (behindCamera) continue;
                float distance = Vector2.Distance(screenPoint, new Vector2(projected.x, projected.y));
                if (distance > bestDistance) continue;
                best = handle;
                bestDistance = distance;
            }
            return best;
        }
    }
}
