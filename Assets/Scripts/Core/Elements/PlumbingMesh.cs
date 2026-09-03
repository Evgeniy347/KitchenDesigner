using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    internal sealed class PlumbingMesh
    {
        private readonly MeshAccumulator _target = new MeshAccumulator();
        private readonly Vector3 _originMM;

        public PlumbingMesh(Vector3 originMM) => _originMM = originMM;

        public void AddSegment(PipeSegment segmentMM)
        {
            if (segmentMM.IsDegenerate) return;
            float toU = AppConstants.MM_TO_UNITS;
            TubeMesh.AppendSegment(_target,
                Local(segmentMM.FromMM), Local(segmentMM.ToMM),
                segmentMM.FromRadiusMM * toU, segmentMM.ToRadiusMM * toU,
                PipeTessellation.RadialSegmentsFor(
                    Mathf.Max(segmentMM.FromRadiusMM, segmentMM.ToRadiusMM)));
        }

        public void AddSegments(IReadOnlyList<PipeSegment> segmentsMM)
        {
            for (int i = 0; i < segmentsMM.Count; i++) AddSegment(segmentsMM[i]);
        }

        public void AddTube(IReadOnlyList<Vector3> pointsMM, float radiusMM)
        {
            if (pointsMM.Count < 2) return;
            float toU = AppConstants.MM_TO_UNITS;

            var points = new Vector3[pointsMM.Count];
            var radii = new float[pointsMM.Count];
            for (int i = 0; i < pointsMM.Count; i++)
            {
                points[i] = Local(pointsMM[i]);
                radii[i] = radiusMM * toU;
            }

            TubeMesh.Append(_target, points, radii,
                PipeTessellation.RadialSegmentsFor(radiusMM));
        }

        public void AddRoundedBox(Vector3 centreMM, Vector3 sizeMM, float cornerRadiusMM)
        {
            float toU = AppConstants.MM_TO_UNITS;
            float width = sizeMM.x * toU;
            float depth = sizeMM.z * toU;
            var profile = RoundedRectProfile.Uniform(width, depth, cornerRadiusMM * toU,
                RoundedRectProfile.DefaultSegments);
            _target.Consume(ProfileExtrusionMesh.Build(profile, width, depth, sizeMM.y * toU),
                Local(centreMM));
        }

        public Mesh Build() => _target.Build();

        private Vector3 Local(Vector3 pointMM) =>
            (pointMM - _originMM) * AppConstants.MM_TO_UNITS;
    }
}
