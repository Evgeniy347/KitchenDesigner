using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class ValidationBroadPhase
    {
        public const float CellSizeUnits = 1.0f;

        public const int CellsPerAxisFromOrigin = 1 << 20;

        private const int BitsPerAxis = 21;

        private static readonly Dictionary<long, List<int>> _grid = new Dictionary<long, List<int>>();
        private static readonly Stack<List<int>> _cellPool = new Stack<List<int>>();
        private static readonly HashSet<long> _seenPairs = new HashSet<long>();
        private static readonly List<(int lo, int hi)> _pairs = new List<(int lo, int hi)>();

        public static long CellKey(int cx, int cy, int cz) =>
            ((long)(cx + CellsPerAxisFromOrigin) << (BitsPerAxis * 2))
            | ((long)(cy + CellsPerAxisFromOrigin) << BitsPerAxis)
            | (long)(cz + CellsPerAxisFromOrigin);

        public static int CellFloor(float coord) => Mathf.FloorToInt(coord / CellSizeUnits);

        public static void Clear()
        {
            foreach (var kv in _grid)
            {
                kv.Value.Clear();
                _cellPool.Push(kv.Value);
            }
            _grid.Clear();
            _seenPairs.Clear();
            _pairs.Clear();
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
            _pairs.Sort((p, q) => p.lo != q.lo ? p.lo.CompareTo(q.lo) : p.hi.CompareTo(q.hi));
            return _pairs;
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
            foreach (var kv in _grid)
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
                        if (!_seenPairs.Add((long)lo << 32 | (uint)hi)) continue;
                        _pairs.Add((lo, hi));
                    }
                }
            }
        }

        private static List<int> GridCell(int cx, int cy, int cz)
        {
            long key = CellKey(cx, cy, cz);
            if (!_grid.TryGetValue(key, out var list))
            {
                list = _cellPool.Count > 0 ? _cellPool.Pop() : new List<int>();
                _grid[key] = list;
            }
            return list;
        }
    }
}
