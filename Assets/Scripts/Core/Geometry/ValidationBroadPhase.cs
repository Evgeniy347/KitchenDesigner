using System;
using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class ValidationBroadPhase
    {
        public const float CellSizeUnits = 1.0f;

        public const int CellsPerAxisFromOrigin = 1 << 20;

        private const int BitsPerAxis = 21;

        [ThreadStatic] private static Dictionary<long, List<int>>? _gridPerThread;
        [ThreadStatic] private static Stack<List<int>>? _cellPoolPerThread;
        [ThreadStatic] private static HashSet<long>? _seenPairsPerThread;
        [ThreadStatic] private static List<(int lo, int hi)>? _pairsPerThread;

        private static Dictionary<long, List<int>> Grid =>
            _gridPerThread ??= new Dictionary<long, List<int>>();

        private static Stack<List<int>> CellPool =>
            _cellPoolPerThread ??= new Stack<List<int>>();

        private static HashSet<long> SeenPairs =>
            _seenPairsPerThread ??= new HashSet<long>();

        private static List<(int lo, int hi)> Pairs =>
            _pairsPerThread ??= new List<(int lo, int hi)>();

        public static long CellKey(int cx, int cy, int cz) =>
            ((long)(cx + CellsPerAxisFromOrigin) << (BitsPerAxis * 2))
            | ((long)(cy + CellsPerAxisFromOrigin) << BitsPerAxis)
            | (long)(cz + CellsPerAxisFromOrigin);

        public static int CellFloor(float coord) => Mathf.FloorToInt(coord / CellSizeUnits);

        public static void Clear()
        {
            foreach (var kv in Grid)
            {
                kv.Value.Clear();
                CellPool.Push(kv.Value);
            }
            Grid.Clear();
            SeenPairs.Clear();
            Pairs.Clear();
        }

        public static List<(int lo, int hi)> CandidatePairsInNestedLoopOrder(
            IReadOnlyList<ValidationElement> all, float contactDist)
        {
            for (int k = 0; k < all.Count; k++)
            {
                SolidBoundsIncludingExtraBody(all[k], out var min, out var max);
                Insert(k, min, max, contactDist);
            }

            CollectPairs();
            var pairs = Pairs;
            pairs.Sort((p, q) => p.lo != q.lo ? p.lo.CompareTo(q.lo) : p.hi.CompareTo(q.hi));
            return pairs;
        }

        public static void SolidBoundsIncludingExtraBody(in ValidationElement e,
            out Vector3 min, out Vector3 max)
        {
            min = e.Geometry.Min;
            max = e.Geometry.Max;
            if (e.HasExtraBody)
            {
                min = Vector3.Min(min, e.ExtraBody.Min);
                max = Vector3.Max(max, e.ExtraBody.Max);
            }
        }

        private static void Insert(int index, Vector3 min, Vector3 max, float contactDist)
        {
            int cx0 = CellFloor(min.x - contactDist), cx1 = CellFloor(max.x + contactDist);
            int cy0 = CellFloor(min.y - contactDist), cy1 = CellFloor(max.y + contactDist);
            int cz0 = CellFloor(min.z - contactDist), cz1 = CellFloor(max.z + contactDist);
            for (int cx = cx0; cx <= cx1; cx++)
                for (int cy = cy0; cy <= cy1; cy++)
                    for (int cz = cz0; cz <= cz1; cz++)
                        GridCell(cx, cy, cz).Add(index);
        }

        private static void CollectPairs()
        {
            var seen = SeenPairs;
            var pairs = Pairs;
            foreach (var kv in Grid)
            {
                var cell = kv.Value;
                int count = cell.Count;
                if (count < 2) continue;
                for (int x = 0; x < count; x++)
                {
                    int a = cell[x];
                    for (int y = x + 1; y < count; y++)
                    {
                        int b = cell[y];
                        int lo = a < b ? a : b;
                        int hi = a < b ? b : a;
                        if (!seen.Add((long)lo << 32 | (uint)hi)) continue;
                        pairs.Add((lo, hi));
                    }
                }
            }
        }

        private static List<int> GridCell(int cx, int cy, int cz)
        {
            long key = CellKey(cx, cy, cz);
            var grid = Grid;
            if (!grid.TryGetValue(key, out var list))
            {
                var pool = CellPool;
                list = pool.Count > 0 ? pool.Pop() : new List<int>();
                grid[key] = list;
            }
            return list;
        }
    }
}
