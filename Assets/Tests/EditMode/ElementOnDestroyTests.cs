using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

/// <summary>
/// Ловушка наследования: <c>OnDestroy</c> — сообщение Unity, а не виртуальный
/// метод. Объявил его подкласс — приватный базовый БОЛЬШЕ НЕ ЗОВЁТСЯ, и снятие
/// с учёта в <c>PartRegistry</c> молча пропадает вместе с ним. Раньше про это
/// стояли комментарии в LightSourceElement и DoorElement; здесь оно проверяется
/// сразу для всего слоя.
/// </summary>
public class ElementOnDestroyTests
{
    private static string ElementsSourceDir()
    {
        var roots = new[]
        {
            Path.GetDirectoryName(typeof(ElementOnDestroyTests).Assembly.Location),
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory(),
        };

        foreach (var root in roots)
        {
            if (string.IsNullOrEmpty(root)) continue;
            var dir = new DirectoryInfo(root);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "Assets", "Scripts", "Core", "Elements");
                if (Directory.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
        }

        throw new DirectoryNotFoundException(
            "Не найден Assets/Scripts/Core/Elements ни от одной из точек: " + string.Join(", ", roots));
    }

    private static readonly Regex ElementClass =
        new Regex(@"\bclass\s+\w+\s*:\s*KitchenElement\b");

    private static readonly Regex OwnOnDestroy =
        new Regex(@"void\s+OnDestroy\s*\(\s*\)\s*\{(?<body>[^{}]*(\{[^{}]*\}[^{}]*)*)\}",
            RegexOptions.Singleline);

    private static List<string> ElementSourcesWithTheirOwnOnDestroy(out List<string> missing)
    {
        var found = new List<string>();
        missing = new List<string>();

        foreach (var file in Directory.GetFiles(ElementsSourceDir(), "*.cs", SearchOption.TopDirectoryOnly))
        {
            var text = File.ReadAllText(file);
            if (!ElementClass.IsMatch(text)) continue;

            var match = OwnOnDestroy.Match(text);
            if (!match.Success) continue;

            var name = Path.GetFileName(file);
            found.Add(name);
            if (!match.Groups["body"].Value.Contains("PartRegistry.Unregister"))
                missing.Add(name);
        }
        return found;
    }

    [Test]
    public void EveryElement_WithItsOwnOnDestroy_UnregistersFromThePartRegistry()
    {
        ElementSourcesWithTheirOwnOnDestroy(out var missing);

        Assert.IsEmpty(missing,
            "OnDestroy — сообщение Unity, а не override: объявив свой, класс ЗАКРЫЛ приватный "
            + "OnDestroy базового KitchenElement, и вместе с ним пропало снятие с учёта в "
            + "PartRegistry — в реестре повисает уничтоженный элемент. Допишите "
            + "PartRegistry.Unregister(this) первой строкой: " + string.Join(", ", missing));
    }

    /// <summary>Скан по несуществующему пути или сломанный regex прошли бы
    /// зелёными, не проверив ничего.</summary>
    [Test]
    public void TheScan_ActuallyFindsTheElementsThatDeclareOnDestroy()
    {
        var found = ElementSourcesWithTheirOwnOnDestroy(out _);

        CollectionAssert.Contains(found, "LightSourceElement.cs",
            "лампа объявляет свой OnDestroy — она и есть подопечная правила");
        CollectionAssert.Contains(found, "SinkElement.cs");
        CollectionAssert.Contains(found, "DoorElement.cs");
        Assert.Greater(found.Count, 5, "подопечных должно быть больше горстки");
    }
}
