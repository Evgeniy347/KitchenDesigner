using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class FaceRects
    {
        public static Rect Of(in Face face, Vector3 u, Vector3 v)
        {
            var center = new Vector2(
                Vector3.Dot(face.center, u),
                Vector3.Dot(face.center, v));

            float halfU = Mathf.Abs(Vector3.Dot(face.rightAxis, u)) * face.size.x * 0.5f
                        + Mathf.Abs(Vector3.Dot(face.upAxis, u)) * face.size.y * 0.5f;
            float halfV = Mathf.Abs(Vector3.Dot(face.rightAxis, v)) * face.size.x * 0.5f
                        + Mathf.Abs(Vector3.Dot(face.upAxis, v)) * face.size.y * 0.5f;

            return new Rect(center.x - halfU, center.y - halfV, halfU * 2, halfV * 2);
        }

        public static void SignedOverlap(Rect a, Rect b, out float overlapU, out float overlapV)
        {
            overlapU = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
            overlapV = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
        }

        public static float RatioOfSmallerSide(float overlap, float smallerSide) =>
            smallerSide > 0 ? overlap / smallerSide : 0f;
    }
}
