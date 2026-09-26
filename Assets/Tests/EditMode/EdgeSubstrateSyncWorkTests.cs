using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Сторож работы для <see cref="EdgeSubstrate.SyncScene"/>. Третий профиль
/// пользователя (test-results/perf/perf_20260911_184756.csv, 153 кадра, «7 fps при
/// перетаскивании») назвал его числом: <b>43-46 мс в КАЖДОМ кадре перетаскивания</b> при
/// `main_ms` 133-145, то есть треть кадра.
///
/// Внутри `SyncScene` каждый элемент спрашивал у сцены свою же сферу через
/// <c>SceneFaces.SphereOf</c> — линейный перебор со сравнением <c>KitchenElement ==
/// KitchenElement</c>, а это перегруженный Unity-оператор, то есть нативный вызов на каждое
/// сравнение. На 401 детали — около 80 000 нативных сравнений за один `SyncScene`, плюс
/// второй вызов <c>GetFaces()</c> на каждую деталь: сцена уже держала и грани, и сферы, но
/// <c>Coverage</c> строил их заново. Индекс детали в сцене известен циклу — его и надо
/// передавать.
///
/// Считаем не миллисекунды, а линейные поиски: их обязано быть НОЛЬ.</summary>
public class EdgeSubstrateSyncWorkTests : ElementTestBase
{
    [SetUp]
    public void SetUp()
    {
        PartRegistry.Clear();
        SceneFaces.TakeLinearLookups();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var el in PartRegistry.GetAll())
            if (el != null) Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
        SceneFaces.TakeLinearLookups();
    }

    private List<KitchenElement> ARowOfBoards(int count)
    {
        var all = new List<KitchenElement>(count);
        for (int i = 0; i < count; i++)
            all.Add(MakePrimitiveElement("B" + i, new Vector3Int(600, 720, 18),
                new Vector3(i * 0.6f, 0.36f, 0f)));
        return all;
    }

    private List<KitchenElement> ARowOfBandedBoards(int count)
    {
        var all = ARowOfBoards(count);
        foreach (var e in all) e.EdgeBandingEnabled = true;
        return all;
    }

    /// <summary>Главный сенсор: пересборка подложки кромок по всей сцене не имеет права
    /// ни разу искать деталь перебором — индекс у неё уже есть. Число здесь равно числу
    /// деталей, то есть на проекте пользователя — сорока тысячам нативных сравнений.</summary>
    [Test]
    public void SyncingTheWholeScene_LooksUpNoElementByScanning()
    {
        var all = ARowOfBoards(12);
        SceneFaces.TakeLinearLookups();

        EdgeSubstrate.SyncScene(all);

        Assert.AreEqual(0, SceneFaces.TakeLinearLookups(),
            "цикл по сцене знает индекс каждой детали — искать её перебором со сравнением "
            + "KitchenElement == KitchenElement (нативный вызов Unity на каждое сравнение) "
            + "не за чем");
    }

    /// <summary>Положительный контроль: счётчик обязан считать, и путь «индекса нет»
    /// обязан остаться рабочим — им пользуются одиночные вызовы <c>Coverage</c> извне
    /// цикла.</summary>
    [Test]
    public void AskingForOneElementWithoutItsIndex_StillFindsItByScanning()
    {
        var all = ARowOfBoards(4);
        var scene = SceneFaces.Of(all);
        SceneFaces.TakeLinearLookups();

        EdgeBanding.Coverage(all[2], scene);

        Assert.AreEqual(1, SceneFaces.TakeLinearLookups(),
            "без индекса деталь ищется перебором — ровно один раз и по-прежнему успешно");
    }

    /// <summary>И ответ обязан не измениться: экономия здесь про цену, а не про результат.
    /// Доска, зажатая соседями с двух сторон, обязана получить ту же маску голых торцов,
    /// что и при вычислении без индекса.</summary>
    [Test]
    public void TheMaskWithTheIndex_EqualsTheMaskWithoutIt()
    {
        var all = ARowOfBoards(3);
        var scene = SceneFaces.Of(all);

        int withIndex = EdgeSubstrate.BareFaceMask(all[1], scene, 1);
        int without = EdgeSubstrate.BareFaceMask(all[1], scene);

        Assert.AreEqual(without, withIndex,
            "индекс — это способ НАЙТИ деталь в сцене, а не другая деталь");
    }

    /// <summary>2026-09-26: `EdgeSubstrate.Sync` (одна деталь на кадр перетаскивания —
    /// `KitchenElement.ApplyDimensions`) строил `SceneFaces` заново и звал `Coverage` через
    /// `SceneFaces.NeighborsOf`, а тот на ПЕРВЫЙ запрос строит список соседей ДЛЯ ВСЕЙ сцены
    /// (`SphereSweep` + k списков) — цена целого прохода ради ответа ОДНОМУ элементу, и она
    /// платится на каждом кадре, а не на весь `SyncScene`. `BareFaceMaskForOnePart` обязан
    /// искать соседей одной детали линейно (`SceneFaces.LinearNeighborsOf`), не строя общий
    /// кеш вовсе.</summary>
    [Test]
    public void SyncingOnePart_DoesNotBuildTheWholeSceneNeighborCache()
    {
        var all = ARowOfBandedBoards(12);
        var scene = SceneFaces.Of(all);
        SceneFaces.TakeNeighborIndexBuilds();

        EdgeSubstrate.BareFaceMaskForOnePart(all[6], scene, 6);

        Assert.AreEqual(0, SceneFaces.TakeNeighborIndexBuilds(),
            "запрос одной детали не имеет права построить список соседей для ВСЕЙ сцены — "
            + "это цена целого прохода ради одного вызова, и она платится на каждом кадре "
            + "перетаскивания");
    }

    /// <summary>Считаем не миллисекунды, а линейные пробы (conventions/PERFORMANCE.md):
    /// запрос одной детали обязан остаться O(n) — вырасти в 4 раза со сценой, а не в 16,
    /// как строгий O(n²) перебор.</summary>
    [Test]
    public void SyncingOnePart_LinearNeighborProbes_GrowLinearly_NotQuadratically_AsTheSceneGrows()
    {
        int small = LinearProbesOfOneSync(100);
        int large = LinearProbesOfOneSync(400);

        Assert.Greater(small, 0, "сцена из 100 деталей обязана дать хоть одну пробу — иначе "
            + "сравнение ничего не проверяет");
        Assert.Less(large, small * 8,
            $"сцена выросла в 4 раза (100 -> 400 деталей), число линейных проб выросло "
            + $"с {small} до {large}. Запрос одной детали обязан остаться O(n) — рост должен "
            + "остаться около 4×; рост около 16× значит, что запрос одной детали снова тянет "
            + "весь общий кеш соседей");
    }

    private int LinearProbesOfOneSync(int count)
    {
        var all = ARowOfBandedBoards(count);
        var scene = SceneFaces.Of(all);
        SceneFaces.TakeLinearNeighborProbes();

        EdgeSubstrate.BareFaceMaskForOnePart(all[count / 2], scene, count / 2);
        int probes = SceneFaces.TakeLinearNeighborProbes();

        foreach (var e in all)
            if (e != null) Object.DestroyImmediate(e.gameObject);
        PartRegistry.Clear();
        return probes;
    }

    /// <summary>И ответ обязан не измениться: экономия здесь про цену, а не про результат —
    /// как и у пары индекс/без индекса выше.</summary>
    [Test]
    public void TheMaskForOnePart_EqualsTheWholeSceneMask()
    {
        var all = ARowOfBandedBoards(3);
        var scene = SceneFaces.Of(all);

        int wholeScene = EdgeSubstrate.BareFaceMask(all[1], scene, 1);
        int onePart = EdgeSubstrate.BareFaceMaskForOnePart(all[1], scene, 1);

        Assert.AreEqual(wholeScene, onePart,
            "линейный поиск соседей — это способ НАЙТИ соседей той же детали, а не другой "
            + "ответ");
    }
}
