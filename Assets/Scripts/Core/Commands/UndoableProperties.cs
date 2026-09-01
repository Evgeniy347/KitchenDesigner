using System;
using System.Collections.Generic;
using System.Reflection;

namespace KitchenDesigner.Core
{
    public sealed class ElementPropertyBag
    {
        private readonly PropertyInfo[] _props;
        private readonly object?[] _values;

        internal ElementPropertyBag(PropertyInfo[] props, object?[] values)
        {
            _props = props;
            _values = values;
        }

        public int Count => _props.Length;
        public PropertyInfo PropertyAt(int i) => _props[i];
        public object? ValueAt(int i) => _values[i];

        public bool TryGet(string name, out object? value)
        {
            for (int i = 0; i < _props.Length; i++)
                if (_props[i].Name == name) { value = _values[i]; return true; }
            value = null;
            return false;
        }
    }

    public static class UndoableProperties
    {
        private static readonly Dictionary<Type, PropertyInfo[]> _cache =
            new Dictionary<Type, PropertyInfo[]>();

        public static PropertyInfo[] For(Type type)
        {
            if (_cache.TryGetValue(type, out var cached)) return cached;

            var list = new List<PropertyInfo>();
            foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!p.CanRead || !p.CanWrite) continue;
                if (p.GetIndexParameters().Length != 0) continue;
                if (p.GetCustomAttribute<UndoableAttribute>() == null) continue;
                list.Add(p);
            }

            list.Sort((a, b) =>
            {
                int oa = a.GetCustomAttribute<UndoableAttribute>()!.Order;
                int ob = b.GetCustomAttribute<UndoableAttribute>()!.Order;
                return oa != ob ? oa.CompareTo(ob) : string.CompareOrdinal(a.Name, b.Name);
            });

            var arr = list.ToArray();
            _cache[type] = arr;
            return arr;
        }

        public static ElementPropertyBag Capture(KitchenElement element)
        {
            var props = For(element.GetType());
            var values = new object?[props.Length];
            for (int i = 0; i < props.Length; i++)
                values[i] = props[i].GetValue(element);
            return new ElementPropertyBag(props, values);
        }

        public static List<int> Changed(ElementPropertyBag before, ElementPropertyBag after)
        {
            var changed = new List<int>();
            int n = Math.Min(before.Count, after.Count);
            for (int i = 0; i < n; i++)
            {
                if (!Equals(before.ValueAt(i), after.ValueAt(i)))
                    changed.Add(i);
            }
            return changed;
        }

        public static void Restore(KitchenElement element, ElementPropertyBag bag, List<int> indices)
        {
            foreach (int i in indices)
            {
                var prop = bag.PropertyAt(i);
                var want = bag.ValueAt(i);
                if (Equals(prop.GetValue(element), want)) continue;
                prop.SetValue(element, want);
            }
        }
    }
}
