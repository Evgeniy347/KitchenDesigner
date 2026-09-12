using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;

/// <summary>Второй сторож работы для <c>SceneAnalyzer.CollectEdgeCover</c>, после
/// <see cref="EdgeCoverAnalysisWorkTests"/>. Тот убрал повторную сборку индекса;
/// этот убирает вопросы к ДВИЖКУ, заданные по парам.
///
/// Число из дампа пользователя <c>test-results/perf/perf_20260912_185912.csv</c>
/// (411 деталей): <c>EdgeBanding.Coverage</c> — 41 мс из 44 мс всей фазы
/// <c>CollectEdgeCover</c> и больше половины цены всего <c>SceneAnalyzer.Analyze</c>
/// (78 мс), который выбивает кадр на 83–194 мс восемнадцать раз за сессию.
///
/// Цена лежала не в арифметике, а в трёх вопросах к движку на КАЖДУЮ ПАРУ:
/// <c>other == null</c> (перегруженный <c>==</c> у <c>UnityEngine.Object</c> —
/// это вызов в нативную часть, а не сравнение ссылок), <c>other.gameObject</c> и
/// <c>activeInHierarchy</c>. На 411 деталях это 411 × 411 ≈ 169 000 пар и около
/// полумиллиона переходов в нативный код — ради ответа, который у детали ОДИН на
/// весь проход. Ответ переехал в индекс <c>SceneFaces</c>, где он считается по
/// разу на деталь, а в цикле по парам остался один <c>bool</c>.
///
/// Считаем ВОПРОСЫ (<c>EdgeBanding.TakeNeighbourProbes</c>), а не миллисекунды, и
/// проверяем ФОРМУ роста: вдвое большая сцена обязана стоить ровно вдвое больше
/// вопросов. Квадратичная форма этого равенства не выдерживает — 6 деталей дают
/// 30 вопросов вместо 6, а 12 деталей 132 вместо 12, и 2 × 30 ≠ 132. Красным тест
/// становится от переноса вызова <c>CoversEdgesOfNeighbours</c> обратно внутрь
/// цикла по парам.</summary>
public class EdgeCoverNeighbourProbeTests : ElementTestBase
{
    [SetUp]
    public void SetUp()
    {
        PartRegistry.Clear();
        EdgeBanding.TakeNeighbourProbes();
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
        EdgeBanding.TakeNeighbourProbes();
    }

    private void ARowOfBandedBoards(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var board = MakePrimitiveElement("B" + i, new Vector3Int(600, 720, 18),
                new Vector3(i * 0.6f, 0.36f, 0f));
            board.EdgeBandingEnabled = true;
        }
    }

    private static int ProbesOfOneAnalysis()
    {
        EdgeBanding.TakeNeighbourProbes();
        SceneAnalyzer.Analyze();
        return EdgeBanding.TakeNeighbourProbes();
    }

    [Test]
    public void AskingTheEngineAboutANeighbour_GrowsWithTheScene_NotWithItsSquare()
    {
        ARowOfBandedBoards(6);
        int small = ProbesOfOneAnalysis();

        ARowOfBandedBoards(6);
        int twiceAsBig = ProbesOfOneAnalysis();

        Assert.Greater(small, 0, "вопросов нет вовсе — сенсор подключён не туда");
        Assert.AreEqual(2 * small, twiceAsBig,
            $"вдвое большая сцена задала движку {twiceAsBig} вопросов вместо {2 * small}: "
            + "вопрос «закрывает ли сосед кромку» снова задаётся по парам, а не по деталям");
    }

    [Test]
    public void EachElementIsAskedAboutOnce_PerIndexBuild()
    {
        ARowOfBandedBoards(12);

        SceneFaces.TakeIndexBuilds();
        EdgeBanding.TakeNeighbourProbes();
        SceneAnalyzer.Analyze();
        int builds = SceneFaces.TakeIndexBuilds();
        int probes = EdgeBanding.TakeNeighbourProbes();

        Assert.AreEqual(12 * builds, probes,
            $"на {builds} сборок индекса по 12 деталей пришлось {probes} вопросов к движку; "
            + "квадратичная форма дала бы 132 на одну сборку");
    }
}
