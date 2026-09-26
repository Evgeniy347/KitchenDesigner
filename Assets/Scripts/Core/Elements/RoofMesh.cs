using System.Collections.Generic;
using KitchenDesigner.Core.Construction;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class RoofMesh
    {
        public static Mesh Build(RoofFootprint footprintMm, RoofType type, RoofRidgeAxis ridgeAxis,
            float overhangMm, float pitchDeg, float thicknessMm, float verticalOffsetUnits = 0f)
        {
            var mesh = new Mesh();
            if (footprintMm.WidthXMm <= 0f && footprintMm.LengthZMm <= 0f) return mesh;

            var boundaries = RoofFrameGeometry.PlaneBoundariesUnits(footprintMm, type, ridgeAxis,
                overhangMm, pitchDeg);
            if (boundaries.Count == 0) return mesh;

            float thicknessUnits = Mathf.Max(0f, thicknessMm) * AppConstants.MM_TO_UNITS;
            var offset = new Vector3(0f, verticalOffsetUnits, 0f);

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            foreach (var boundary in boundaries)
            {
                var plane = new RoofPlaneMesh(boundary, thicknessUnits);
                int indexOffset = vertices.Count;
                foreach (var p in plane.Positions) vertices.Add(p + offset);
                normals.AddRange(plane.Normals);
                uvs.AddRange(plane.Uvs);
                foreach (var index in plane.Triangles) triangles.Add(index + indexOffset);
            }

            mesh.vertices = vertices.ToArray();
            mesh.normals = normals.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
