using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class BoardRegistry
    {
        private static readonly List<KitchenElement> _all = new List<KitchenElement>();

        public static IReadOnlyList<KitchenElement> All => _all;

        public static void Register(KitchenElement element)
        {
            if (element != null && !_all.Contains(element))
                _all.Add(element);
        }

        public static void Unregister(KitchenElement element)
        {
            _all.Remove(element);
        }

        public static List<KitchenElement> GetAll()
        {
            return new List<KitchenElement>(_all);
        }

        public static void Clear()
        {
            _all.Clear();
        }
    }
}
