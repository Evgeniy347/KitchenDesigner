using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core.UI
{
    public static class TableSort
    {
        public static int[] Order(IReadOnlyList<bool> isItem, IReadOnlyList<bool> startsGroup,
            Func<int, string> key, bool descending)
        {
            var order = new List<int>(isItem.Count);
            var items = new List<int>();
            var trailing = new List<int>();

            void Flush()
            {
                items.Sort((a, b) =>
                {
                    int c = Compare(key(a), key(b));
                    if (descending) c = -c;
                    return c != 0 ? c : a.CompareTo(b);
                });
                order.AddRange(items);
                order.AddRange(trailing);
                items.Clear();
                trailing.Clear();
            }

            for (int i = 0; i < isItem.Count; i++)
            {
                if (startsGroup[i])
                {
                    Flush();
                    order.Add(i);
                }
                else if (isItem[i]) items.Add(i);
                else trailing.Add(i);
            }
            Flush();
            return order.ToArray();
        }

        public static int Compare(string? a, string? b)
        {
            bool na = NumberFormat.TryParse(a, out double da);
            bool nb = NumberFormat.TryParse(b, out double db);
            if (na && nb) return da.CompareTo(db);
            if (na != nb) return na ? -1 : 1;
            return string.Compare(a ?? "", b ?? "", StringComparison.CurrentCultureIgnoreCase);
        }
    }
}
