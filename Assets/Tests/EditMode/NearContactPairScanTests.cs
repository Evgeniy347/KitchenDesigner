using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;

/// <summary>Сторож работы для <see cref="ConstraintValidator.FindNearContacts"/> — самой
/// дорогой части <c>SceneAnalyzer.Analyze</c>. Профиль пользователя (839 кадров,
/// test-results/perf/perf_20260911_163010.csv) назвал виновника числом: <c>Analyze</c>
/// вызывался 39 раз и стоил <b>676,6 мс в среднем</b> (мин 664,6, макс 729,0). Заслонка
/// <c>SceneSettleThrottle</c> при этом работала честно — беда была не в частоте, а в цене
/// одного вызова, и потому её не видел никто: жест замирал на две трети секунды каждый раз,
/// когда сцена устаивалась, и при перетаскивании, и при открытии фасада.
///
/// Цена росла квадратично: пара деталей попадала в полный перебор граней (6×6 сравнений
/// плоскостей) без всякой широкой фазы. На проекте пользователя (401 деталь) это 80 200 пар.
/// Замерено на ядре под dotnet на том же размере: <b>243,2 мс и 80 200 глубоких пар — было,
/// 4,5 мс и 380 — стало</b>, после того как перед перебором встал отказ по огибающим.
///
/// Отказ ТОЧНЫЙ: обе ветки за ним — контакт грань-в-грань в пределах contactDist и
/// <c>NearestParallelGap</c> — сами начинаются с той же проверки AABB, расширенной на
/// broadPhase, и при её провале возвращают «ничего». Поэтому исход пары не меняется, меняется
/// только цена. Считаем ПАРЫ, дошедшие до перебора, а не миллисекунды.</summary>
public class NearContactPairScanTests : ElementTestBase
{
    [SetUp]
    public void SetUp() => PartRegistry.Clear();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var el in PartRegistry.GetAll())
            if (el != null) Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
    }

    private List<KitchenElement> AFieldOfPartsFarApart(int count)
    {
        var all = new List<KitchenElement>(count);
        for (int i = 0; i < count; i++)
            all.Add(MakePrimitiveElement("Far" + i, new Vector3Int(600, 720, 18),
                new Vector3(i * 3f, 0.36f, 0f)));
        return all;
    }

    /// <summary>Главный сенсор. Двадцать деталей стоят по три метра друг от друга — ни одна
    /// пара не может дать ни контакта, ни зазора. Полный перебор дал бы 190 пар;
    /// дойти до перебора граней не должна ни одна.</summary>
    [Test]
    public void PartsFarApart_ReachTheFaceScanInNoPairAtAll()
    {
        var all = AFieldOfPartsFarApart(20);

        var found = ConstraintValidator.FindNearContacts(all,
            SceneAnalyzer.NearContactMinGapMm, SceneAnalyzer.NearContactMaxGapMm);

        Assert.IsEmpty(found, "далёкие детали не образуют ни одного зазора");
        Assert.AreEqual(0, ConstraintValidator.NearContactPairsScannedByLastCall,
            "из 190 пар до перебора граней не должна дойти ни одна: огибающие не "
            + "дотягиваются друг до друга. Число порядка 190 = квадратичная цена, которая "
            + "на 401 детали превращается в 80 200 пар и две трети секунды заморозки");
    }

    /// <summary>Положительный контроль: без него «ноль» выше было бы зелёным и у функции,
    /// которая не проверяет вообще ничего. Соседняя пара с зазором 1 мм обязана дойти до
    /// перебора и обязана быть найдена: 1 мм при требуемых 2..4 — это «слишком мало».</summary>
    [Test]
    public void APairWithARealGap_ReachesTheFaceScanAndIsReported()
    {
        var all = AFieldOfPartsFarApart(3);
        all.Add(MakePrimitiveElement("Near", new Vector3Int(600, 720, 18),
            new Vector3(0f, 0.36f, 0.019f)));

        var found = ConstraintValidator.FindNearContacts(all,
            SceneAnalyzer.NearContactMinGapMm, SceneAnalyzer.NearContactMaxGapMm);

        Assert.AreEqual(1, ConstraintValidator.NearContactPairsScannedByLastCall,
            "ровно одна пара стоит достаточно близко, чтобы её стоило разглядывать");
        Assert.AreEqual(1, found.Count,
            "и эта пара обязана быть НАЙДЕНА: отказ по огибающим экономит время, а не ответы");
    }
}
