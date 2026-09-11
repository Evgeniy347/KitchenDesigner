using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public sealed class StaticContactIndex
    {
        private readonly Dictionary<long, List<int>> _cells = new Dictionary<long, List<int>>();
        private readonly List<int> _hits = new List<int>();
        private int[] _stamp = System.Array.Empty<int>();
        private int _query;

        public int CellCount => _cells.Count;

        public static StaticContactIndex Over(IReadOnlyList<ValidationElement> all, bool[] skip,
            float contactDist)
        {
            var index = new StaticContactIndex();
            index._stamp = new int[all.Count];
            for (int i = 0; i < all.Count; i++)
            {
                if (skip[i]) continue;
                ValidationBroadPhase.SolidBoundsIncludingExtraBody(all[i], out var min, out var max);
                index.Insert(i, min, max, contactDist);
            }
            return index;
        }

        public List<int> Reaching(IReadOnlyList<ValidationElement> all, int subject, float contactDist)
        {
            _hits.Clear();
            _query++;

            var e = all[subject];
            ValidationBroadPhase.SolidBoundsIncludingExtraBody(e, out var min, out var max);

            int cx0 = ValidationBroadPhase.CellFloor(min.x - contactDist);
            int cx1 = ValidationBroadPhase.CellFloor(max.x + contactDist);
            int cy0 = ValidationBroadPhase.CellFloor(min.y - contactDist);
            int cy1 = ValidationBroadPhase.CellFloor(max.y + contactDist);
            int cz0 = ValidationBroadPhase.CellFloor(min.z - contactDist);
            int cz1 = ValidationBroadPhase.CellFloor(max.z + contactDist);

            for (int cx = cx0; cx <= cx1; cx++)
                for (int cy = cy0; cy <= cy1; cy++)
                    for (int cz = cz0; cz <= cz1; cz++)
                    {
                        if (!_cells.TryGetValue(ValidationBroadPhase.CellKey(cx, cy, cz),
                                out var cell)) continue;
                        for (int k = 0; k < cell.Count; k++)
                        {
                            int other = cell[k];
                            if (_stamp[other] == _query) continue;
                            _stamp[other] = _query;
                            if (ValidationBroadPhase.SolidsReach(e, all[other], contactDist))
                                _hits.Add(other);
                        }
                    }
            return _hits;
        }

        private void Insert(int index, Vector3 min, Vector3 max, float contactDist)
        {
            int cx0 = ValidationBroadPhase.CellFloor(min.x - contactDist);
            int cx1 = ValidationBroadPhase.CellFloor(max.x + contactDist);
            int cy0 = ValidationBroadPhase.CellFloor(min.y - contactDist);
            int cy1 = ValidationBroadPhase.CellFloor(max.y + contactDist);
            int cz0 = ValidationBroadPhase.CellFloor(min.z - contactDist);
            int cz1 = ValidationBroadPhase.CellFloor(max.z + contactDist);

            for (int cx = cx0; cx <= cx1; cx++)
                for (int cy = cy0; cy <= cy1; cy++)
                    for (int cz = cz0; cz <= cz1; cz++)
                    {
                        long key = ValidationBroadPhase.CellKey(cx, cy, cz);
                        if (!_cells.TryGetValue(key, out var cell))
                        {
                            cell = new List<int>();
                            _cells[key] = cell;
                        }
                        cell.Add(index);
                    }
        }
    }
}
