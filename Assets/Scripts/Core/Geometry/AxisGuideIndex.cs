using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public sealed class AxisGuideIndex
    {
        public const float MaxRangeUnits = 10f;
        public const float CellUnits = 0.5f;
        public const int MaxCellsPerBox = 4096;

        private readonly AxisBox[] _boxes;
        private readonly Dictionary<long, List<int>>[] _cells =
        {
            new Dictionary<long, List<int>>(),
            new Dictionary<long, List<int>>(),
            new Dictionary<long, List<int>>(),
        };
        private readonly List<int>[] _everywhere = { new List<int>(), new List<int>(), new List<int>() };

        private readonly List<AxisHit> _hits = new List<AxisHit>();

        public int BoxTests { get; private set; }

        public int Count => _boxes.Length;

        public AxisGuideIndex(IReadOnlyList<AxisBox> boxes)
        {
            _boxes = new AxisBox[boxes != null ? boxes.Count : 0];
            for (int i = 0; i < _boxes.Length; i++)
            {
                _boxes[i] = boxes![i];
                for (int axis = 0; axis < 3; axis++) File(i, axis);
            }
        }

        public AxisCast Cast(Vector3 origin, int axis, int sign, float start)
        {
            _hits.Clear();
            Collect(_everywhere[axis], origin, axis, sign, start);
            var crossing = Crossing(origin, axis);
            if (crossing != null) Collect(crossing, origin, axis, sign, start);

            bool hasNear = false, hasFar = false;
            AxisHit near = default, far = default;
            foreach (var hit in _hits)
                if (!hasNear || hit.Enter < near.Enter)
                {
                    near = hit;
                    hasNear = true;
                }

            float touch = Tolerance.ContactUnits;
            if (hasNear)
                foreach (var hit in _hits)
                {
                    if (hit.Id == near.Id || hit.Enter < near.Exit - touch) continue;
                    if (hasFar && hit.Enter >= far.Enter) continue;
                    far = hit;
                    hasFar = true;
                }

            return new AxisCast(origin, axis, sign, start, hasNear, near, hasFar, far);
        }

        private void Collect(List<int> candidates, Vector3 origin, int axis, int sign, float start)
        {
            float reach = start + MaxRangeUnits;
            float touch = Tolerance.ContactUnits;

            foreach (int index in candidates)
            {
                BoxTests++;
                ref readonly var box = ref _boxes[index];
                if (!box.Spans(origin, axis, out float lo, out float hi)) continue;

                float enter = sign > 0 ? lo : -hi;
                float exit = sign > 0 ? hi : -lo;
                if (enter < start - touch || enter > reach) continue;
                _hits.Add(new AxisHit(box.Id, enter, exit));
            }
        }

        private List<int>? Crossing(Vector3 origin, int axis)
        {
            Across(axis, out int b, out int c);
            return _cells[axis].TryGetValue(Key(Cell(origin[b]), Cell(origin[c])), out var list)
                ? list
                : null;
        }

        private void File(int index, int axis)
        {
            ref readonly var box = ref _boxes[index];
            Across(axis, out int b, out int c);
            int b0 = Cell(box.Min[b]), b1 = Cell(box.Max[b]);
            int c0 = Cell(box.Min[c]), c1 = Cell(box.Max[c]);

            long cells = (long)(b1 - b0 + 1) * (c1 - c0 + 1);
            if (cells > MaxCellsPerBox)
            {
                _everywhere[axis].Add(index);
                return;
            }

            var grid = _cells[axis];
            for (int i = b0; i <= b1; i++)
                for (int j = c0; j <= c1; j++)
                {
                    long key = Key(i, j);
                    if (!grid.TryGetValue(key, out var list))
                    {
                        list = new List<int>();
                        grid[key] = list;
                    }
                    list.Add(index);
                }
        }

        private static void Across(int axis, out int b, out int c)
        {
            b = (axis + 1) % 3;
            c = (axis + 2) % 3;
        }

        private static int Cell(float coordinate) => Mathf.FloorToInt(coordinate / CellUnits);

        private static long Key(int i, int j) => ((long)i << 32) ^ (uint)j;
    }
}
