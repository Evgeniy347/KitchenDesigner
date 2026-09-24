using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.Construction
{
    public static class FoundationLayout
    {
        public static IReadOnlyList<FoundationPolyline> MergeIntoPolylines(
            IReadOnlyList<WallCentreline> centrelines)
        {
            var polylines = new List<FoundationPolyline>();
            if (centrelines == null || centrelines.Count == 0) return polylines;

            var nodePositions = new List<Vector3>();
            var nodeEdges = new List<List<int>>();
            var edgeNodeA = new List<int>();
            var edgeNodeB = new List<int>();
            var edgeLengthMm = new List<float>();

            foreach (var line in centrelines)
            {
                if (!line.IsDefined) continue;

                int a = NodeIndexFor(line.Start, nodePositions, nodeEdges);
                int b = NodeIndexFor(line.End, nodePositions, nodeEdges);
                int edgeIndex = edgeNodeA.Count;
                edgeNodeA.Add(a);
                edgeNodeB.Add(b);
                edgeLengthMm.Add(Vector3.Distance(line.Start, line.End) / AppConstants.MM_TO_UNITS);
                nodeEdges[a].Add(edgeIndex);
                nodeEdges[b].Add(edgeIndex);
            }

            int edgeCount = edgeNodeA.Count;
            var edgeVisited = new bool[edgeCount];

            for (int node = 0; node < nodePositions.Count; node++)
            {
                if (nodeEdges[node].Count == 2) continue;

                foreach (var edgeIndex in nodeEdges[node])
                {
                    if (edgeVisited[edgeIndex]) continue;
                    polylines.Add(TracePolyline(node, edgeIndex, nodePositions, nodeEdges,
                        edgeNodeA, edgeNodeB, edgeLengthMm, edgeVisited, isClosed: false));
                }
            }

            for (int edgeIndex = 0; edgeIndex < edgeCount; edgeIndex++)
            {
                if (edgeVisited[edgeIndex]) continue;
                int start = edgeNodeA[edgeIndex];
                polylines.Add(TracePolyline(start, edgeIndex, nodePositions, nodeEdges,
                    edgeNodeA, edgeNodeB, edgeLengthMm, edgeVisited, isClosed: true));
            }

            return polylines;
        }

        public static float TotalLengthMm(IReadOnlyList<WallCentreline> centrelines)
        {
            var polylines = MergeIntoPolylines(centrelines);
            float total = 0f;
            foreach (var polyline in polylines) total += polyline.LengthMm;
            return total;
        }

        private static int NodeIndexFor(Vector3 point, List<Vector3> nodePositions,
            List<List<int>> nodeEdges)
        {
            for (int i = 0; i < nodePositions.Count; i++)
                if (SamePointInPlan(nodePositions[i], point)) return i;

            nodePositions.Add(point);
            nodeEdges.Add(new List<int>());
            return nodePositions.Count - 1;
        }

        private static bool SamePointInPlan(Vector3 a, Vector3 b)
        {
            float limit = WallCentreline.CornerToleranceMm * AppConstants.MM_TO_UNITS;
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz <= limit * limit;
        }

        private static FoundationPolyline TracePolyline(int startNode, int firstEdge,
            List<Vector3> nodePositions, List<List<int>> nodeEdges,
            List<int> edgeNodeA, List<int> edgeNodeB, List<float> edgeLengthMm,
            bool[] edgeVisited, bool isClosed)
        {
            var points = new List<Vector3> { nodePositions[startNode] };
            float lengthMm = 0f;

            int currentNode = startNode;
            int edgeIndex = firstEdge;
            while (true)
            {
                edgeVisited[edgeIndex] = true;
                lengthMm += edgeLengthMm[edgeIndex];

                int nextNode = edgeNodeA[edgeIndex] == currentNode
                    ? edgeNodeB[edgeIndex]
                    : edgeNodeA[edgeIndex];
                points.Add(nodePositions[nextNode]);
                currentNode = nextNode;

                if (nodeEdges[currentNode].Count != 2) break;
                if (isClosed && currentNode == startNode) break;

                int candidate = -1;
                foreach (var e in nodeEdges[currentNode])
                {
                    if (edgeVisited[e]) continue;
                    candidate = e;
                    break;
                }
                if (candidate < 0) break;
                edgeIndex = candidate;
            }

            return new FoundationPolyline(points, lengthMm);
        }
    }
}
