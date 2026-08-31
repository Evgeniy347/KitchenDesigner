using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class WorldBounds
    {
        public const float LimitMeters = 100f;

        public static Vector3 Clamp(Vector3 p) => new Vector3(
            Mathf.Clamp(p.x, -LimitMeters, LimitMeters),
            Mathf.Clamp(p.y, -LimitMeters, LimitMeters),
            Mathf.Clamp(p.z, -LimitMeters, LimitMeters));
    }
}
