using System.Collections.Generic;
using KitchenDesigner.Core.Construction;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class FoundationStripMesh
    {
        public static Mesh Build(IReadOnlyList<WallCentreline> loadBearingCentrelines,
            Vector3 originWorld, float widthMm, float depthMm)
        {
            var polylines = FoundationLayout.MergeIntoPolylines(loadBearingCentrelines);
            if (polylines.Count == 0) return new Mesh();

            float widthUnits = Mathf.Max(0f, widthMm) * AppConstants.MM_TO_UNITS;
            float depthUnits = Mathf.Max(0f, depthMm) * AppConstants.MM_TO_UNITS;
            float halfWidth = widthUnits * 0.5f;
            float centreY = -depthUnits * 0.5f;

            var accumulator = new MeshAccumulator();

            foreach (var polyline in polylines)
            {
                var points = polyline.Points;
                if (points == null) continue;

                for (int i = 0; i + 1 < points.Count; i++)
                {
                    var start = points[i] - originWorld;
                    var end = points[i + 1] - originWorld;
                    AddSegment(accumulator, start, end, halfWidth, widthUnits, depthUnits, centreY);
                }
            }

            return accumulator.Build();
        }

        private static void AddSegment(MeshAccumulator accumulator, Vector3 start, Vector3 end,
            float halfWidth, float widthUnits, float depthUnits, float centreY)
        {
            var flat = new Vector3(end.x - start.x, 0f, end.z - start.z);
            float length = flat.magnitude;
            if (length < Tolerance.EpsilonUnits) return;

            var dir = flat / length;
            var right = new Vector3(dir.z, 0f, -dir.x) * halfWidth;

            var profile = new[]
            {
                new Vector2(start.x - right.x, start.z - right.z),
                new Vector2(start.x + right.x, start.z + right.z),
                new Vector2(end.x + right.x, end.z + right.z),
                new Vector2(end.x - right.x, end.z - right.z),
            };

            var segmentMesh = ProfileExtrusionMesh.Build(profile, length, widthUnits,
                depthUnits, centreY);
            accumulator.Consume(segmentMesh, Vector3.zero);
        }
    }
}
