using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Что валидация считает «землёй», когда якоря в сцене нет вовсе.
///
/// Раньше корнем обхода становилась ПЕРВАЯ по списку деталь с контактом, и
/// ответ зависел от порядка массива: та же сцена, перечисленная иначе, давала
/// другой вердикт — статус менялся у деталей, которых никто не трогал. Теперь
/// землёй объявляется САМЫЙ НИЖНИЙ уровень сцены: все детали, чей низ совпадает
/// с низом самой нижней стоящей детали (в пределах контактного допуска),
/// подпёрты, и подпёрто всё, что стоит на них. Порядок списка в ответе больше
/// не участвует.</summary>
public class ConnectivityWithoutAnchorTests
{
    private static ValidationElement Part(string name, Vector3 centerMm, Vector3 sizeMm) =>
        ValidationTestScene.Part(name, centerMm, sizeMm);

    /// <summary>Сцена без единого якоря: две стопки на нулевом уровне в разных
    /// концах, остров из двух досок под потолком и одинокая доска, которая
    /// вообще ни с чем не соприкасается.</summary>
    private static List<ValidationElement> AnchorlessScene() => new()
    {
        Part("Low_Board", new Vector3(0, 9, 0), new Vector3(800, 18, 400)),
        Part("Low_Stack", new Vector3(0, 27, 0), new Vector3(800, 18, 400)),
        Part("Far_Board", new Vector3(5000, 9, 0), new Vector3(800, 18, 400)),
        Part("Far_Stack", new Vector3(5000, 27, 0), new Vector3(800, 18, 400)),
        Part("Island_Shelf", new Vector3(2500, 1009, 0), new Vector3(400, 18, 400)),
        Part("Island_Stack", new Vector3(2500, 1027, 0), new Vector3(400, 18, 400)),
        Part("Loose", new Vector3(9000, 2000, 0), new Vector3(400, 18, 400)),
    };

    private static IEnumerable<List<ValidationElement>> EveryOrderOf(List<ValidationElement> parts)
    {
        var order = Enumerable.Range(0, parts.Count).ToArray();
        var state = new int[parts.Count];
        yield return order.Select(i => parts[i]).ToList();

        int k = 0;
        while (k < order.Length)
        {
            if (state[k] >= k) { state[k] = 0; k++; continue; }
            int swap = k % 2 == 0 ? 0 : state[k];
            (order[swap], order[k]) = (order[k], order[swap]);
            state[k]++;
            k = 0;
            yield return order.Select(i => parts[i]).ToList();
        }
    }

    /// <summary>Вердикт по ДЕТАЛЯМ: для каждого имени — набор кодов нарушений,
    /// в которых деталь названа, плюс изолированные группы по именам. Отпечаток
    /// <c>ValidationTestScene.Fingerprint</c> здесь не годится: он пишет контакт
    /// парой «A|B», и от перестановки списка меняется сторона пары, а не
    /// статус. Сравнивать нужно то, что видит пользователь, — кто красный и
    /// почему.</summary>
    private static string VerdictOf(List<ValidationElement> parts)
    {
        var r = ValidationCore.Validate(parts);
        var byName = (r.Diagnostics ?? new List<CoreViolation>())
            .Select(d => $"{parts[d.Element].Name}|{d.Kind}")
            .Distinct().OrderBy(s => s, System.StringComparer.Ordinal);
        var groups = r.IsolatedGroups
            .Select(g => string.Join("+", g.Select(i => parts[i].Name)
                .OrderBy(s => s, System.StringComparer.Ordinal)))
            .OrderBy(s => s, System.StringComparer.Ordinal);

        return $"valid={r.IsValid}\n  {string.Join("\n  ", byName)}\n" +
               $"groups:\n  {string.Join("\n  ", groups)}";
    }

    private static HashSet<string> UnsupportedNames(List<ValidationElement> parts)
    {
        var r = ValidationCore.Validate(parts);
        return new HashSet<string>((r.Diagnostics ?? new List<CoreViolation>())
            .Where(d => d.Kind == ViolationKind.Unsupported)
            .Select(d => parts[d.Element].Name));
    }

    [Test]
    public void Connectivity_WithoutAnchor_EveryOrderOfTheSameScene_GivesTheSameVerdict()
    {
        var parts = AnchorlessScene();
        string expected = VerdictOf(parts);

        int checkedOrders = 0;
        foreach (var permuted in EveryOrderOf(parts))
        {
            checkedOrders++;
            Assert.AreEqual(expected, VerdictOf(permuted),
                "Вердикт сцены без якоря обязан зависеть только от геометрии. " +
                $"Порядок №{checkedOrders} — {string.Join(",", permuted.Select(p => p.Name))} — " +
                "дал другой ответ: значит землю снова выбирает список, и статус меняется " +
                "у деталей, которых пользователь не трогал");
        }

        Assert.AreEqual(5040, checkedOrders,
            "Свип обязан пройти ВСЕ перестановки семи деталей — иначе он зелен на пустом месте");
    }

    /// <summary>Пара к тесту выше: тот же отпечаток обязан УМЕТЬ различать
    /// сцены. Если бы сравнение было слепым, свип по перестановкам был бы
    /// зелёным при любой реализации корня.</summary>
    [Test]
    public void TheSameVerdict_Differs_WhenTheIslandIsLoweredToTheGround()
    {
        var parts = AnchorlessScene();
        var lowered = AnchorlessScene();
        lowered[4] = Part("Island_Shelf", new Vector3(2500, 9, 0), new Vector3(400, 18, 400));
        lowered[5] = Part("Island_Stack", new Vector3(2500, 27, 0), new Vector3(400, 18, 400));

        Assert.AreNotEqual(VerdictOf(parts), VerdictOf(lowered),
            "Остров, опущенный на нулевой уровень, обязан перестать быть неподпёртым — " +
            "иначе отпечаток не видит разницы и свип по перестановкам ничего не проверяет");
    }

    [Test]
    public void Connectivity_WithoutAnchor_GroundIsTheLowestLevel_NotTheFirstInTheList()
    {
        var islandFirst = AnchorlessScene();
        var low = islandFirst.GetRange(0, 4);
        islandFirst.RemoveRange(0, 4);
        islandFirst.AddRange(low);

        CollectionAssert.AreEquivalent(new[] { "Island_Shelf", "Island_Stack", "Loose" },
            UnsupportedNames(islandFirst).ToArray(),
            "Остров перечислен ПЕРВЫМ, но землёй остаётся нижний уровень: " +
            "неподпёрты только он и одинокая доска");
    }

    [Test]
    public void Connectivity_WithoutAnchor_TwoComponentsOnTheLowestLevel_AreBothGround()
    {
        var parts = AnchorlessScene();

        var unsupported = UnsupportedNames(parts);

        Assert.IsFalse(unsupported.Contains("Low_Board"),
            "Стопка на нижнем уровне — земля");
        Assert.IsFalse(unsupported.Contains("Far_Board"),
            "Вторая стопка стоит на том же уровне и в другом конце сцены: " +
            "уровень не достаётся одной компоненте, иначе вторая краснела бы " +
            "только за то, что она не первая");
        Assert.IsFalse(unsupported.Contains("Far_Stack"),
            "Доска на второй стопке подпёрта транзитивно");
    }

    /// <summary>Осознанное следствие правила «земля — нижний уровень»: деталь,
    /// опущенная НИЖЕ всех, переносит землю на себя, и прежний нижний уровень
    /// становится висящим. Это геометрия, а не порядок списка, — но это цена
    /// правила, и она записана здесь, чтобы её не открыли заново как дефект.</summary>
    [Test]
    public void Connectivity_WithoutAnchor_APartPlacedBelowEverything_TakesTheGroundOver()
    {
        var parts = AnchorlessScene();
        Assert.IsFalse(UnsupportedNames(parts).Contains("Low_Board"),
            "До появления нижней детали стопка была землёй");

        parts.Add(Part("Deeper", new Vector3(5000, -9, 0), new Vector3(800, 18, 400)));

        var unsupported = UnsupportedNames(parts);
        Assert.IsTrue(unsupported.Contains("Low_Board"),
            "Земля переехала на нижний уровень: прежняя земля в другом конце сцены теперь висит");
        Assert.IsFalse(unsupported.Contains("Far_Board"),
            "Стопка НАД новой нижней деталью стоит на ней и остаётся подпёртой");
    }
}
