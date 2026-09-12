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
}
