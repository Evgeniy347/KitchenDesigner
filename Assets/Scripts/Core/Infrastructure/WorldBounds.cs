using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Границы «мира»: объекты нельзя размещать дальше ±100 м по любой
    /// оси. Единый источник предела — применяется в снэпе к сетке, при
    /// перетаскивании, размещении и в MCP-мутациях позиции.</summary>
    public static class WorldBounds
    {
        public const float LimitMeters = 100f;

        public static Vector3 Clamp(Vector3 p) => new Vector3(
            Mathf.Clamp(p.x, -LimitMeters, LimitMeters),
            Mathf.Clamp(p.y, -LimitMeters, LimitMeters),
            Mathf.Clamp(p.z, -LimitMeters, LimitMeters));
    }
}
