using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public static class SpecTotals
    {
        public static Dictionary<SpecUnit, float> ByUnit(IEnumerable<(SpecUnit unit, float qty)> rows)
        {
            var totals = new Dictionary<SpecUnit, float>();
            foreach (var (unit, qty) in rows)
                totals[unit] = totals.TryGetValue(unit, out var sum) ? sum + qty : qty;
            return totals;
        }
    }
}
