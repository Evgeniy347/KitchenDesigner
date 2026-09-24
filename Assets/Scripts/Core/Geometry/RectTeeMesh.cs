using System;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class RectTeeMesh
    {
        public static (Vector3[] Vertices, int[] Triangles) Build(
            Vector3 hub, Vector3 mainAxis, Vector3 branchAxis,
            float mainWidthMm, float mainHeightMm, float mainLegLengthMm,
            float branchWidthMm, float branchHeightMm, float branchLegLengthMm)
        {
            ValidateBranchFitsTheMainRun(mainWidthMm, mainHeightMm, mainLegLengthMm,
                branchWidthMm, branchHeightMm, branchLegLengthMm);

            var main = mainAxis.normalized;
            var branch = branchAxis.normalized;
            var depth = Vector3.Cross(main, branch);

            var mainSpan = MainBodySpan(hub, main, mainLegLengthMm);
            var branchSpan = BranchBodySpan(hub, branch, mainHeightMm, branchLegLengthMm);

            var mainBody = BoxRunMesh.Build(mainSpan.From, mainSpan.To, depth,
                mainWidthMm, mainHeightMm);
            var branchBody = BoxRunMesh.Build(branchSpan.From, branchSpan.To, depth,
                branchWidthMm, branchHeightMm);

            return Combine(mainBody, branchBody);
        }

        public static (Vector3 From, Vector3 To) MainBodySpan(Vector3 hub, Vector3 mainAxis,
            float mainLegLengthMm)
        {
            var main = mainAxis.normalized;
            return (hub - main * mainLegLengthMm, hub + main * mainLegLengthMm);
        }

        public static (Vector3 From, Vector3 To) BranchBodySpan(Vector3 hub, Vector3 branchAxis,
            float mainHeightMm, float branchLegLengthMm)
        {
            var branch = branchAxis.normalized;
            return (hub + branch * (mainHeightMm * 0.5f), hub + branch * branchLegLengthMm);
        }

        public static float SheetMetalAreaM2(float mainWidthMm, float mainHeightMm,
            float mainLegLengthMm, float branchWidthMm, float branchHeightMm,
            float branchLegLengthMm)
        {
            float mainLateralM2 = 2f * (mainWidthMm + mainHeightMm) * (2f * mainLegLengthMm) * 1e-6f;
            float openingM2 = branchWidthMm * branchHeightMm * 1e-6f;
            float branchSpanMm = branchLegLengthMm - mainHeightMm * 0.5f;
            float branchLateralM2 = 2f * (branchWidthMm + branchHeightMm) * branchSpanMm * 1e-6f;
            return mainLateralM2 - openingM2 + branchLateralM2;
        }

        private static void ValidateBranchFitsTheMainRun(float mainWidthMm, float mainHeightMm,
            float mainLegLengthMm, float branchWidthMm, float branchHeightMm,
            float branchLegLengthMm)
        {
            float halfBranchHeightMm = branchHeightMm * 0.5f;
            float halfMainHeightMm = mainHeightMm * 0.5f;

            if (branchWidthMm >= mainWidthMm)
                throw new ArgumentOutOfRangeException(nameof(branchWidthMm), branchWidthMm,
                    "Branch width must be strictly narrower than the main run's width");
            if (branchHeightMm > mainHeightMm)
                throw new ArgumentOutOfRangeException(nameof(branchHeightMm), branchHeightMm,
                    "Branch height must not exceed the main run's height");
            if (halfBranchHeightMm >= mainLegLengthMm)
                throw new ArgumentOutOfRangeException(nameof(mainLegLengthMm), mainLegLengthMm,
                    "Main leg should not be shorter than half the branch height");
            if (branchLegLengthMm <= halfMainHeightMm)
                throw new ArgumentOutOfRangeException(nameof(branchLegLengthMm), branchLegLengthMm,
                    "Branch leg length (measured from the hub) must clear the main run's half-height");
        }

        private static (Vector3[] Vertices, int[] Triangles) Combine(
            (Vector3[] Vertices, int[] Triangles) a, (Vector3[] Vertices, int[] Triangles) b)
        {
            var vertices = new Vector3[a.Vertices.Length + b.Vertices.Length];
            Array.Copy(a.Vertices, vertices, a.Vertices.Length);
            Array.Copy(b.Vertices, 0, vertices, a.Vertices.Length, b.Vertices.Length);

            var triangles = new int[a.Triangles.Length + b.Triangles.Length];
            Array.Copy(a.Triangles, triangles, a.Triangles.Length);
            for (int i = 0; i < b.Triangles.Length; i++)
                triangles[a.Triangles.Length + i] = b.Triangles[i] + a.Vertices.Length;

            return (vertices, triangles);
        }
    }
}
