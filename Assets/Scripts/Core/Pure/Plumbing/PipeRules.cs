using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core.Plumbing
{
    public static class PipeRules
    {
        public static bool BlocksRouting(PipeObstacleKind kind) =>
            kind == PipeObstacleKind.Part || kind == PipeObstacleKind.Furniture;

        public static IReadOnlyList<PipeFinding> Collect(IPipeSceneSnapshot scene) =>
            Collect(PipeSurvey.Of(scene.Ports()), scene.Segments(), scene.Obstacles());

        public static IReadOnlyList<PipeFinding> Collect(PipeSurvey survey,
            IReadOnlyList<PipeRunSegment> segments, IReadOnlyList<PipeObstacle> obstacles)
        {
            var findings = new List<PipeFinding>();
            CollectOpenEnds(survey, findings);
            CollectSizeMismatches(survey, findings);
            CollectObstacles(segments, obstacles, findings);
            return findings;
        }

        private static void CollectOpenEnds(PipeSurvey survey, List<PipeFinding> findings)
        {
            var ports = survey.Ports;
            for (int i = 0; i < ports.Count; i++)
            {
                if (ports[i].OwnerKind != PipeNodeKind.Pipe) continue;
                if (!survey.Network.IsFree(i)) continue;
                findings.Add(PipeIssueCatalog.OpenEnd(ports[i]));
            }
        }

        private static void CollectSizeMismatches(PipeSurvey survey, List<PipeFinding> findings)
        {
            var ports = survey.Ports;

            foreach (var link in survey.Network.Links)
            {
                var sizeA = survey.SizeOf(link.APortIndex);
                var sizeB = survey.SizeOf(link.BPortIndex);
                if (sizeA == null || sizeB == null) continue;
                if (string.Equals(sizeA, sizeB, StringComparison.Ordinal)) continue;
                findings.Add(PipeIssueCatalog.DirectSizeMismatch(
                    ports[link.APortIndex], ports[link.BPortIndex], sizeA, sizeB));
            }

            var reported = new List<string>();
            for (int i = 0; i < ports.Count; i++)
            {
                if (!PipeNodePorts.RequiresOneSize(ports[i].OwnerKind)) continue;
                var elementId = ports[i].ElementId;
                if (reported.Contains(elementId)) continue;

                var sizes = survey.SizesOfElement(elementId);
                if (!TryFindDisagreement(sizes, out var first, out var second)) continue;

                reported.Add(elementId);
                findings.Add(PipeIssueCatalog.FittingSizeMismatch(elementId, first, second));
            }
        }

        private static bool TryFindDisagreement(IReadOnlyList<string?> sizes,
            out string? first, out string? second)
        {
            first = null;
            second = null;
            foreach (var size in sizes)
            {
                if (size == null) continue;
                if (first == null)
                {
                    first = size;
                    continue;
                }

                if (string.Equals(first, size, StringComparison.Ordinal)) continue;
                second = size;
                return true;
            }

            return false;
        }

        private static void CollectObstacles(IReadOnlyList<PipeRunSegment> segments,
            IReadOnlyList<PipeObstacle> obstacles, List<PipeFinding> findings)
        {
            foreach (var segment in segments)
            {
                var bounds = segment.BoundsMm;
                foreach (var obstacle in obstacles)
                {
                    if (!BlocksRouting(obstacle.Kind)) continue;
                    if (!bounds.Overlaps(obstacle.BoundsMm)) continue;
                    findings.Add(PipeIssueCatalog.ObstacleCrossed(segment, obstacle));
                }
            }
        }
    }
}
