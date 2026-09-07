using System.Collections.Generic;
using UnityEngine;
using KitchenDesigner.Core.Plumbing;

namespace KitchenDesigner.Core.Analysis
{
    public sealed class ScenePipeSnapshot : IPipeSceneSnapshot
    {
        private readonly List<PipePort> _ports = new List<PipePort>();
        private readonly List<PipeRunSegment> _segments = new List<PipeRunSegment>();
        private readonly List<PipeObstacle> _obstacles = new List<PipeObstacle>();

        public ScenePipeSnapshot(IReadOnlyList<KitchenElement> scene)
        {
            if (scene == null) return;
            foreach (var e in scene)
            {
                if (e == null) continue;
                if (e is PipeElement pipe) AddRun(pipe);
                else _obstacles.Add(new PipeObstacle(e.PartName, KindOf(e), BoxOf(e)));
            }
        }

        public IReadOnlyList<PipePort> Ports() => _ports;

        public IReadOnlyList<PipeRunSegment> Segments() => _segments;

        public IReadOnlyList<PipeObstacle> Obstacles() => _obstacles;

        public static PipeObstacleKind KindOf(KitchenElement e)
        {
            if (e.GetComponent<Wall>() != null) return PipeObstacleKind.Wall;
            if (e.GetComponent<BasePlate>() != null || e is FloorElement) return PipeObstacleKind.Floor;
            return PipeObstacleKind.Part;
        }

        private void AddRun(PipeElement pipe)
        {
            var a = ToMm(pipe.EndAUnits);
            var b = ToMm(pipe.EndBUnits);
            var along = pipe.RunAxis;

            _segments.Add(new PipeRunSegment(pipe.PartName, a, b, pipe.OuterDiameterMm));
            _ports.Add(new PipePort(pipe.PartName, PipeNodeKind.Pipe, 0, a,
                AxisOf(-along), pipe.SizeId));
            _ports.Add(new PipePort(pipe.PartName, PipeNodeKind.Pipe, 1, b,
                AxisOf(along), pipe.SizeId));
        }

        private static PipeAxis AxisOf(Vector3 direction) =>
            new PipeAxis(direction.x, direction.y, direction.z);

        private static PointMm ToMm(Vector3 units)
        {
            float toMm = 1f / AppConstants.MM_TO_UNITS;
            return new PointMm(units.x * toMm, units.y * toMm, units.z * toMm);
        }

        private static BoxMm BoxOf(KitchenElement e)
        {
            var vertices = e.GetVertices();
            if (vertices == null || vertices.Length == 0)
                return new BoxMm(ToMm(e.transform.position), ToMm(e.transform.position));

            var min = vertices[0];
            var max = vertices[0];
            for (int i = 1; i < vertices.Length; i++)
            {
                min = Vector3.Min(min, vertices[i]);
                max = Vector3.Max(max, vertices[i]);
            }

            return new BoxMm(ToMm(min), ToMm(max));
        }
    }
}
