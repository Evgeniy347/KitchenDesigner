using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using KitchenDesigner.Core;
using KitchenDesigner.Tests.Geometry;

/// <summary>«Накладка в сцене ОДНА» (`docs/UI-GUIDELINES.md` §9). Красную геометрию
/// строит, хранит и гасит `HighlightOverlay`; потребители решают ТОЛЬКО какой участок
/// красить. Свой `GameObject`, `Material` или `Mesh` в потребителе — это второй набор
/// квадов со своим временем жизни, который чужой `Hide()` не уберёт, и участок
/// останется красным навсегда.
///
/// Список потребителей здесь не написан руками — прежняя версия держала два имени
/// файлов и была слепа к третьему потребителю ровно так, как описано в
/// `agents/TEST-DESIGN.md`. Он СОБИРАЕТСЯ из файлов `Core/Rendering`, упоминающих
/// накладку, а проверяются объявленные поля типа — отражением над сборкой, а не
/// грепом по тексту: `new GameObject(` можно написать и не так.
///
/// Сторож обязан доказать, что нашёл потребителей: пустой список зеленеет на любом
/// коде.</summary>
public class HighlightConsumerRuleTests
{
    private static string RenderingDir =>
        RepoPaths.Subdir("Assets", "Scripts", "Core", "Rendering");

    [Test]
    public void HighlightConsumers_OwnNoSceneObjectsOfTheirOwn()
    {
        var consumers = Consumers();

        Assert.GreaterOrEqual(consumers.Count, 2,
            "сканер обязан найти потребителей — иначе он ничего не стережёт");

        foreach (var type in consumers)
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                Assert.IsFalse(HoldsEngineObjects(field.FieldType),
                    "потребитель " + type.Name + " решает, КАКОЙ участок красить, "
                    + "а рисует, хранит и гасит один HighlightOverlay. Поле "
                    + field.Name + " типа " + field.FieldType.Name + " — это своя "
                    + "геометрия со своим временем жизни: чужой Hide() её не уберёт");
    }

    [Test]
    public void HighlightOverlay_IsTheOnlyOne_ThatKnowsThePalette()
    {
        StringAssert.Contains("UIStyle.EdgeHighlight3D", Source("HighlightOverlay.cs"),
            "красный накладки живёт в одном месте — иначе два потребителя разойдутся "
            + "в оттенке, и один участок станет краснее другого");

        var consumers = Consumers();
        Assert.GreaterOrEqual(consumers.Count, 2, "сканер обязан найти потребителей");

        foreach (var type in consumers)
            StringAssert.DoesNotContain("UIStyle", Source(type.Name + ".cs"),
                "потребитель " + type.Name + " не знает про палитру: "
                + "за цвет отвечает накладка");
    }

    private static string Source(string fileName) =>
        File.ReadAllText(Path.Combine(RenderingDir, fileName));

    private static List<Type> Consumers()
    {
        var assembly = typeof(HighlightOverlay).Assembly;
        var found = new List<Type>();

        foreach (var file in Directory.GetFiles(RenderingDir, "*.cs"))
        {
            string name = Path.GetFileNameWithoutExtension(file);
            if (name == nameof(HighlightOverlay)) continue;
            if (!File.ReadAllText(file).Contains(nameof(HighlightOverlay))) continue;

            var type = assembly.GetType("KitchenDesigner.Core." + name);
            Assert.IsNotNull(type, "файл " + name + ".cs упоминает накладку, но типа "
                + "KitchenDesigner.Core." + name + " в сборке нет — сканер смотрит "
                + "не туда, и его находки ничего не значат");
            found.Add(type!);
        }

        return found;
    }

    private static bool HoldsEngineObjects(Type type)
    {
        if (typeof(UnityEngine.Object).IsAssignableFrom(type)) return true;
        if (!type.IsGenericType) return false;

        foreach (var argument in type.GetGenericArguments())
            if (typeof(UnityEngine.Object).IsAssignableFrom(argument)) return true;

        return false;
    }
}
