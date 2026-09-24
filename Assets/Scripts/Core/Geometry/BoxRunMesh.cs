using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class BoxRunMesh
    {
        public static (Vector3[] Vertices, int[] Triangles) Build(
            Vector3 fromMm, Vector3 toMm, Vector3 widthAxis, float widthMm, float heightMm) =>
            RectTransitionMesh.Build(fromMm, toMm, widthAxis, widthMm, heightMm, widthMm, heightMm);

        public static float UnfoldedAreaM2(Vector3 fromMm, Vector3 toMm, float widthMm, float heightMm) =>
            RectTransitionMesh.LateralAreaM2(fromMm, toMm, widthMm, heightMm, widthMm, heightMm);
    }
}
