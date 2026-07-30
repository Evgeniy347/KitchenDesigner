using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Архитектурный сторож правила «undo на всё».
///
/// Тест не проверяет конкретное свойство — он проверяет, что про каждое
/// свойство ПОДУМАЛИ. Добавили новое публичное свойство элементу и забыли про
/// отмену — сборка красная с именем этого свойства. Ровно поэтому список
/// свойств здесь не выписан руками: он берётся отражением и не устаревает.
/// </summary>
public class UndoableCoverageTests
{
    private static IEnumerable<Type> ElementTypes()
    {
        var asm = typeof(KitchenElement).Assembly;
        return asm.GetTypes()
            .Where(t => typeof(KitchenElement).IsAssignableFrom(t) && !t.IsAbstract)
            .OrderBy(t => t.FullName, StringComparer.Ordinal);
    }

    /// <summary>Свойства, объявленные самим типом (унаследованные проверяются
    /// на базовом типе), публично читаемые И записываемые.</summary>
    private static IEnumerable<PropertyInfo> DeclaredSettableProperties(Type type)
    {
        const BindingFlags flags =
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        foreach (var p in type.GetProperties(flags))
        {
            if (p.GetIndexParameters().Length != 0) continue;
            var setter = p.GetSetMethod(nonPublic: false);
            var getter = p.GetGetMethod(nonPublic: false);
            if (setter == null || getter == null) continue;
            yield return p;
        }
    }

    [Test]
    public void EveryElementProperty_IsMarkedUndoableOrNot()
    {
        var unmarked = new List<string>();
        foreach (var type in ElementTypes())
            foreach (var prop in DeclaredSettableProperties(type))
            {
                bool undoable = prop.GetCustomAttribute<UndoableAttribute>() != null;
                bool notUndoable = prop.GetCustomAttribute<NotUndoableAttribute>() != null;
                if (undoable && notUndoable)
                    unmarked.Add($"{type.Name}.{prop.Name} — помечено И [Undoable], И [NotUndoable]");
                else if (!undoable && !notUndoable)
                    unmarked.Add($"{type.Name}.{prop.Name} ({prop.PropertyType.Name})");
            }

        if (unmarked.Count == 0) return;

        var sb = new StringBuilder();
        sb.AppendLine("Свойства элементов без решения об отмене (" + unmarked.Count + "):");
        foreach (var line in unmarked) sb.AppendLine("  • " + line);
        sb.AppendLine();
        sb.AppendLine("Пометьте каждое одним из двух:");
        sb.AppendLine("  [Undoable]                  — правка пользователя, обязана откатываться;");
        sb.AppendLine("  [NotUndoable(\"причина\")]     — своя команда или служебное состояние.");
        Assert.Fail(sb.ToString());
    }

    [Test]
    public void NotUndoable_AlwaysExplainsWhy()
    {
        var empty = new List<string>();
        foreach (var type in ElementTypes())
            foreach (var prop in DeclaredSettableProperties(type))
            {
                var attr = prop.GetCustomAttribute<NotUndoableAttribute>();
                if (attr != null && string.IsNullOrWhiteSpace(attr.Reason))
                    empty.Add($"{type.Name}.{prop.Name}");
            }

        Assert.IsEmpty(empty,
            "[NotUndoable] без причины — через полгода никто не вспомнит, ошибка это или замысел: "
            + string.Join(", ", empty));
    }

    /// <summary>Реестр обязан видеть ровно то, что помечено: опечатка в
    /// BindingFlags тихо выключила бы отмену сразу всем свойствам.</summary>
    [Test]
    public void Registry_SeesEveryUndoableProperty()
    {
        var missing = new List<string>();
        foreach (var type in ElementTypes())
        {
            var registered = UndoableProperties.For(type).Select(p => p.Name).ToHashSet();
            var expected = type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetCustomAttribute<UndoableAttribute>() != null
                            && p.GetSetMethod(false) != null && p.GetGetMethod(false) != null)
                .Select(p => p.Name);
            foreach (var name in expected)
                if (!registered.Contains(name)) missing.Add($"{type.Name}.{name}");
        }

        Assert.IsEmpty(missing, "реестр не видит помеченные свойства: " + string.Join(", ", missing));
    }

    /// <summary>Базовое свойство «габарит» должно откатываться раньше всех:
    /// по нему клампятся производные (вырез варочной, кромка).</summary>
    [Test]
    public void Dimensions_RestoreFirst()
    {
        var props = UndoableProperties.For(typeof(CooktopElement));
        Assert.Greater(props.Length, 0, "у варочной должны быть помеченные свойства");
        Assert.AreEqual(nameof(KitchenElement.DimensionsMM), props[0].Name,
            "габарит обязан идти первым — иначе производные подрежутся по старому размеру");
    }
}
