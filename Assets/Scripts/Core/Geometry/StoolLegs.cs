using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class StoolLegs
    {
        public static Vector2[] Footprint(float width, float depth, float radius,
            float inset, float legCrossSection)
        {
            float pull = inset + legCrossSection * 0.5f;
            float halfW = Mathf.Max(0f, width * 0.5f - pull);
            float halfD = Mathf.Max(0f, depth * 0.5f - pull);
            float innerRadius = Mathf.Clamp(radius - pull, 0f, Mathf.Min(halfW, halfD));

            float diagonal = innerRadius * Mathf.Sqrt(0.5f);
            float x = halfW - innerRadius + diagonal;
            float z = halfD - innerRadius + diagonal;

            return new[]
            {
                new Vector2(-x, -z),
                new Vector2( x, -z),
                new Vector2( x,  z),
                new Vector2(-x,  z),
            };
        }
    }
}
