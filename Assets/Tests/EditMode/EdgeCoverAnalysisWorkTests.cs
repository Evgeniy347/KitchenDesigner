using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;

/// <summary>Сторож работы для <see cref="SceneAnalyzer"/> → <c>CollectEdgeCover</c>. Четырнадцать
/// пофазовых маркеров назвали второго слона числом: в профиле пользователя
/// (test-results/perf/perf_20260911_191534.csv) эта фаза стоила <b>332,4 мс из 416,2 мс всего
/// анализа</b> — восемь десятых цены в одной фазе из четырнадцати.
///
/// Причина — не арифметика, а повторная сборка индекса: <c>EdgeBanding.Coverage(e, all)</c>
/// строил <c>SceneFaces.Of(all)</c> ЗАНОВО ДЛЯ КАЖДОЙ детали. На 401 детали это 401 сборка
/// индекса, то есть около 160 000 вызовов <c>GetFaces()</c> со свежим массивом на каждый и
/// столько же описанных сфер — ради ответа, который для всей фазы один и тот же. Индекс
/// собирается один раз, а деталь находится в нём по индексу цикла.
///
/// Считаем сборки индекса, а не миллисекунды: их число обязано НЕ расти со сценой. Это и есть
/// формулировка дефекта — работа, растущая со сценой там, где растёт только вход.</summary>
public class EdgeCoverAnalysisWorkTests : ElementTestBase
{
    [SetUp]
    public void SetUp()
    {
        PartRegistry.Clear();
        SceneFaces.TakeIndexBuilds();
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
        SceneFaces.TakeIndexBuilds();
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

    private static int IndexBuildsOfOneAnalysis()
    {
        SceneFaces.TakeIndexBuilds();
        SceneAnalyzer.Analyze();
        return SceneFaces.TakeIndexBuilds();
    }

    /// <summary>Главный сенсор: разбор вдвое большей сцены обязан стоить ТО ЖЕ число сборок
    /// индекса. До правки их было по одной на каждую деталь с кромкой, то есть ровно столько,
    /// сколько деталей, — и потому цена фазы росла квадратично.</summary>
    [Test]
    public void AnalyzingTwiceTheScene_BuildsTheFaceIndexJustAsManyTimes()
    {
        ARowOfBandedBoards(4);
        int small = IndexBuildsOfOneAnalysis();

        ARowOfBandedBoards(8);
        int twiceAsBig = IndexBuildsOfOneAnalysis();

        Assert.AreEqual(small, twiceAsBig,
            $"на 4 деталях индекс граней собран {small} раз, на 12 — {twiceAsBig}: "
            + "сборка индекса зависит от сцены, а не от детали, и делаться обязана один раз "
            + "на фазу. Рост этого числа со сценой и есть квадратичная цена анализа");
    }

    /// <summary>И абсолютное число: один разбор — одна сборка. Тест выше остался бы зелёным и
    /// при «ноль сборок в обоих случаях», то есть при фазе, которая перестала работать
    /// вовсе.</summary>
    [Test]
    public void OneAnalysis_BuildsTheFaceIndexExactlyOnce()
    {
        ARowOfBandedBoards(6);

        Assert.AreEqual(1, IndexBuildsOfOneAnalysis(),
            "индекс граней нужен ровно одной фазе анализа и ровно один раз");
    }

    /// <summary>Противоположный вход к экономии: ответ не изменился. Доска между двумя
    /// соседями обязана получить то же покрытие торцов, что и при вычислении без индекса.</summary>
    [Test]
    public void TheCoverageWithTheIndex_EqualsTheCoverageWithoutIt()
    {
        ARowOfBandedBoards(3);
        var all = PartRegistry.GetAll();
        var scene = SceneFaces.Of(all);
        var middle = all[1];

        var withIndex = EdgeBanding.Coverage(middle, scene, 1);
        var without = EdgeBanding.Coverage(middle, all);

        foreach (EdgeSide side in EdgeStates.All)
            Assert.AreEqual(without.Ratio(side), withIndex.Ratio(side), 1e-5f,
                $"сторона {side}: индекс — способ НАЙТИ деталь в сцене, а не другая сцена");
    }
}
