using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class RoundedRectSeating
    {
        public static Vector2[] LegCentres(float width, float depth, float radius,
            float inset, float legSpan)
        {
            float pull = inset + legSpan * 0.5f;
            float innerWidth = Mathf.Max(0f, width - pull * 2f);
            float innerDepth = Mathf.Max(0f, depth - pull * 2f);
            float innerRadius = RoundedRectProfile
                .Fit(innerWidth, innerDepth, CornerRadii.Uniform(radius - pull)).PlusXPlusZ;

            float halfW = innerWidth * 0.5f;
            float halfD = innerDepth * 0.5f;
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
