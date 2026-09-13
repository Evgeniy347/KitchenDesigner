using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace KitchenDesigner.Core
{
    public static class ListenerCostLog
    {
        public const int MostListenersRemembered = 12;

        private readonly struct Cost
        {
            public readonly string Who;
            public readonly float Ms;
            public readonly int Calls;

            public Cost(string who, float ms, int calls)
            {
                Who = who;
                Ms = ms;
                Calls = calls;
            }

            public Cost Plus(float ms) => new Cost(Who, Ms + ms, Calls + 1);
        }

        [ThreadStatic] private static List<Cost>? _costs;

        [ThreadStatic] private static int _listenersBeyondTheLimit;

        public static void Note(string? who, float ms)
        {
            string name = string.IsNullOrEmpty(who) ? "?" : who!;
            var costs = _costs ??= new List<Cost>(MostListenersRemembered);

            for (int i = 0; i < costs.Count; i++)
            {
                if (!string.Equals(costs[i].Who, name, StringComparison.Ordinal)) continue;
                costs[i] = costs[i].Plus(ms);
                return;
            }

            if (costs.Count >= MostListenersRemembered)
            {
                _listenersBeyondTheLimit++;
                return;
            }
            costs.Add(new Cost(name, ms, 1));
        }

        public static string Take()
        {
            var costs = _costs;
            if ((costs == null || costs.Count == 0) && _listenersBeyondTheLimit == 0)
            {
                Forget();
                return string.Empty;
            }

            var text = new StringBuilder();
            if (costs != null)
            {
                SortByDescendingCost(costs);
                for (int i = 0; i < costs.Count; i++)
                {
                    if (i > 0) text.Append(", ");
                    text.Append(costs[i].Who).Append(' ')
                        .Append(costs[i].Ms.ToString("F2", CultureInfo.InvariantCulture)).Append("мс");
                    if (costs[i].Calls > 1) text.Append(" ×").Append(costs[i].Calls);
                }
            }
            if (_listenersBeyondTheLimit > 0)
                text.Append(", и ещё ").Append(_listenersBeyondTheLimit).Append(" сверх предела");

            Forget();
            return text.ToString();
        }

        public static void Forget()
        {
            _costs?.Clear();
            _listenersBeyondTheLimit = 0;
        }

        private static void SortByDescendingCost(List<Cost> costs)
        {
            for (int i = 1; i < costs.Count; i++)
            {
                var current = costs[i];
                int j = i - 1;
                while (j >= 0 && costs[j].Ms < current.Ms)
                {
                    costs[j + 1] = costs[j];
                    j--;
                }
                costs[j + 1] = current;
            }
        }
    }
}
