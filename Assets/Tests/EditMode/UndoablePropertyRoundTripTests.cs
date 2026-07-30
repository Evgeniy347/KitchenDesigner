using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Каждое свойство, помеченное <see cref="UndoableAttribute"/>, реально
/// откатывается и повторяется. Список свойств снова берётся отражением: новое
/// свойство попадает под проверку в тот же момент, когда получает атрибут.
/// </summary>
public class UndoablePropertyRoundTripTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private ProjectLoadStateGuard? _guard;

    [SetUp]
    public void SetUp() => _guard = ProjectLoadStateGuard.Capture();

    [TearDown]
    public void TearDown()
    {
        _guard?.Restore();
        foreach (var go in _spawned)
        {
            if (go == null) continue;
            var el = go.GetComponent<KitchenElement>();
            if (el != null) PartRegistry.Unregister(el);
            UnityEngine.Object.DestroyImmediate(go);
        }
        _spawned.Clear();
    }

    private static IEnumerable<Type> ElementTypes() =>
        typeof(KitchenElement).Assembly.GetTypes()
            .Where(t => typeof(KitchenElement).IsAssignableFrom(t) && !t.IsAbstract)
            .Where(t => UndoableProperties.For(t).Length > 0)
            .OrderBy(t => t.Name, StringComparer.Ordinal);

    private KitchenElement Spawn(Type type)
    {
        var go = new GameObject("T_" + type.Name);
        _spawned.Add(go);
        var el = (KitchenElement)go.AddComponent(type);
        el.PartName = "T_" + type.Name;
        // Осмысленный стартовый габарит: у нулевого многие сеттеры клампятся
        // в одну точку, и правка становится неотличима от отсутствия правки.
        el.DimensionsMM = new Vector3Int(600, 700, 18);
        return el;
    }

    /// <summary>Значение, заведомо отличное от текущего. Кламп в сеттере может
    /// вернуть его обратно — это нормально, тест сверяется со СНИМКОМ, а не с
    /// тем, что просили записать.</summary>
    private static object? DifferentValue(PropertyInfo prop, object? current)
    {
        var t = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

        if (t.IsEnum)
        {
            var values = Enum.GetValues(t).Cast<object>().ToList();
            return values.FirstOrDefault(v => !Equals(v, current)) ?? current;
        }
        if (t == typeof(bool)) return !(bool)(current ?? false);
        if (t == typeof(int)) return (int)(current ?? 0) + 37;
        if (t == typeof(float)) return (float)(current ?? 0f) + 1.5f;
        if (t == typeof(string)) return (current as string ?? "") + "_изменено";
        if (t == typeof(Vector3Int))
        {
            var v = (Vector3Int)(current ?? Vector3Int.zero);
            return new Vector3Int(v.x + 40, v.y + 40, v.z + 40);
        }
        return null;
    }

    [Test]
    public void EveryUndoableProperty_UndoRestores_RedoReapplies()
    {
        var failures = new List<string>();
        var unsupported = new List<string>();
        int checkedCount = 0;
        int inertCount = 0;

        foreach (var type in ElementTypes())
            foreach (var prop in UndoableProperties.For(type))
            {
                KitchenElement el;
                try { el = Spawn(type); }
                catch (Exception e)
                {
                    failures.Add($"{type.Name}: не создаётся ({e.GetType().Name})");
                    break;
                }

                object? original;
                try { original = prop.GetValue(el); }
                catch (Exception e) { failures.Add($"{type.Name}.{prop.Name}: чтение упало ({e.Message})"); continue; }

                var candidate = DifferentValue(prop, original);
                if (candidate == null)
                {
                    unsupported.Add($"{type.Name}.{prop.Name} ({prop.PropertyType.Name})");
                    continue;
                }

                var before = UndoableProperties.Capture(el);
                try { prop.SetValue(el, candidate); }
                catch (Exception e) { failures.Add($"{type.Name}.{prop.Name}: запись упала ({e.Message})"); continue; }
                var after = UndoableProperties.Capture(el);

                var cmd = SetPropertiesCommand.TryCreate(el, before, after);
                if (cmd == null)
                {
                    // Сеттер склампил значение обратно — правки не было, откатывать
                    // нечего. Это не ошибка, но и не проверка: считаем отдельно.
                    inertCount++;
                    continue;
                }

                var changedValue = prop.GetValue(el);
                cmd.Undo();
                if (!Equals(prop.GetValue(el), original))
                    failures.Add($"{type.Name}.{prop.Name}: отмена не вернула {Show(original)} " +
                                 $"(осталось {Show(prop.GetValue(el))})");

                cmd.Execute();
                if (!Equals(prop.GetValue(el), changedValue))
                    failures.Add($"{type.Name}.{prop.Name}: повтор не вернул {Show(changedValue)} " +
                                 $"(получилось {Show(prop.GetValue(el))})");

                checkedCount++;
            }

        Assert.IsEmpty(unsupported,
            "тест не умеет придумать другое значение для этих типов — допишите DifferentValue: "
            + string.Join(", ", unsupported));
        Assert.IsEmpty(failures, string.Join("\n", failures));
        Assert.Greater(checkedCount, 0,
            $"ни одно свойство не проверено (инертных {inertCount}) — тест не может провалиться, значит бесполезен");
    }

    private static string Show(object? v) => v?.ToString() ?? "null";
}
