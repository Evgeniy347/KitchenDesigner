using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Автоподгонка опоры после перетаскивания: сесть на пол под собой и
/// дорастить ногу до детали сверху.
///
/// Раньше это жило приватным методом внутри ElementMover, и проверить его было
/// нечем: тесты в PillarElementTests повторяли арифметику руками и настоящий код
/// не звали ни разу.</summary>
public class PillarAutoFitTests
{
    private const float U = AppConstants.MM_TO_UNITS;

    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void SetUp() => PartRegistry.Clear();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
    }

    private KitchenElement Board(Vector3 pos, Vector3Int dims)
    {
        var go = new GameObject("Board");
        _spawned.Add(go);
        go.transform.position = pos;
        var e = go.AddComponent<KitchenElement>();
        e.DimensionsMM = dims;
        e.ApplyDimensions();
        return e;
    }

    private PillarElement Pillar(Vector3 pos, int midHeightMM)
    {
        var go = ElementFactory.CreatePillar(midHeightMM, "Опора", pos);
        _spawned.Add(go);
        return go.GetComponent<PillarElement>();
    }

    private static List<KitchenElement> Scene(params KitchenElement[] elements)
        => new List<KitchenElement>(elements);

    private static float BottomOf(KitchenElement e)
    {
        float min = float.MaxValue;
        foreach (var v in e.GetVertices()) if (v.y < min) min = v.y;
        return min;
    }

    private static float TopOf(KitchenElement e)
    {
        float max = float.MinValue;
        foreach (var v in e.GetVertices()) if (v.y > max) max = v.y;
        return max;
    }

    [Test]
    public void FloorUnder_TakesTheHighestSurfaceBelowTheCentre()
    {
        var floor = Board(new Vector3(0f, -0.009f, 0f), new Vector3Int(3000, 18, 3000));
        var podium = Board(new Vector3(0f, 0.01f, 0f), new Vector3Int(600, 20, 600));
        var pillar = Pillar(new Vector3(0f, 0.08f, 0f), 75);

        float floorY = PillarAutoFit.FloorUnder(pillar, Scene(floor, podium, pillar));

        Assert.AreEqual(TopOf(podium), floorY, 1e-5f,
            "опора садится на ближайшую поверхность под собой, а не на самый низ сцены");
    }

    [Test]
    public void FloorUnder_WithNothingBelow_ReportsNoFloor()
    {
        var pillar = Pillar(new Vector3(0f, 2f, 0f), 75);
        var farBelow = Board(new Vector3(0f, -0.009f, 0f), new Vector3Int(3000, 18, 3000));

        Assert.AreEqual(PillarAutoFit.NoFloorFound,
            PillarAutoFit.FloorUnder(pillar, Scene(farBelow, pillar)), 1e-3f,
            "пол в метре под опорой — не её пол: висящую в воздухе опору автоподгонка не трогает");
    }

    /// <summary>Пол опоры — то, что под её ПЯТОЙ, а не то, что проходит рядом.
    ///
    /// Числа взяты из проекта пользователя: дно цокольного ящика верхом на 20 мм
    /// стоит в 11 мм от края опоры и не пересекает её пятно. Пока годность
    /// кандидата решалась допуском 50 мм от ЦЕНТРА опоры, проход после загрузки
    /// «сажал» опору на эту полку и поднимал её над полом на 20 мм — молча, без
    /// команды и без отмены, а следующее сохранение уносило подмену в файл.</summary>
    [Test]
    public void FloorUnder_IgnoresABoardBesideThePillar_EvenWhenItPassesWithinTheMargin()
    {
        var floor = Board(new Vector3(0f, -0.009f, 0f), new Vector3Int(3000, 18, 3000));
        var beside = Board(new Vector3(0.286f, 0.012f, 0f), new Vector3Int(500, 16, 500));
        var pillar = Pillar(new Vector3(0f, 0.05f, 0f), 70);

        Assert.IsTrue(ElementAabb.Of(beside).CoversInXZ(pillar.transform.position,
                PillarAutoFit.OverlapMarginUnits),
            "доска обязана проходить в прежнем допуске от центра — иначе проверять нечего");

        float floorY = PillarAutoFit.FloorUnder(pillar, Scene(floor, beside, pillar));

        Assert.AreEqual(TopOf(floor), floorY, 1e-5f,
            "доска рядом с опорой не держит её: пол опоры — ближайшая поверхность "
            + "ПОД её пятном, а не любая в полуметре от центра");
    }

    /// <summary>Парный к предыдущему: сдвинь ту же доску на 16 мм, чтобы она зашла
    /// под пяту, — и она СТАНОВИТСЯ полом. Без этой половины запрет читался бы как
    /// «опора всегда падает на самый низ».</summary>
    [Test]
    public void FloorUnder_TakesTheSameBoard_OnceItReachesUnderTheFootprint()
    {
        var floor = Board(new Vector3(0f, -0.009f, 0f), new Vector3Int(3000, 18, 3000));
        var under = Board(new Vector3(0.27f, 0.012f, 0f), new Vector3Int(500, 16, 500));
        var pillar = Pillar(new Vector3(0f, 0.05f, 0f), 70);

        float floorY = PillarAutoFit.FloorUnder(pillar, Scene(floor, under, pillar));

        Assert.AreEqual(TopOf(under), floorY, 1e-5f,
            "доска, заходящая под пяту на 5 мм, — это опора для опоры");
    }

    [Test]
    public void Seat_PutsThePillarOnTheFloor_AndGrowsItUpToTheBoardAbove()
    {
        var floor = Board(new Vector3(0f, -0.009f, 0f), new Vector3Int(3000, 18, 3000));
        var board = Board(new Vector3(0f, 0.108f, 0f), new Vector3Int(540, 16, 564));
        var pillar = Pillar(new Vector3(0f, 0.09f, 0f), 75);
        float floorTop = TopOf(floor);
        float boardBottom = BottomOf(board);

        PillarAutoFit.Seat(pillar, Scene(floor, board, pillar));

        Assert.AreEqual(floorTop, BottomOf(pillar), 1e-4f, "низ опоры — на полу");
        Assert.LessOrEqual(TopOf(pillar), boardBottom + 1e-4f,
            "верх опоры не должен пройти СКВОЗЬ деталь над ней");
        Assert.Greater(TopOf(pillar), boardBottom - 1.5f * U,
            "и не должен не достать до неё больше чем на миллиметр");
    }

    [Test]
    public void Seat_WithNothingAbove_JustStandsOnTheFloor()
    {
        var floor = Board(new Vector3(0f, -0.009f, 0f), new Vector3Int(3000, 18, 3000));
        var pillar = Pillar(new Vector3(0f, 0.09f, 0f), 75);
        int midBefore = pillar.MidHeightMM;

        PillarAutoFit.Seat(pillar, Scene(floor, pillar));

        Assert.AreEqual(TopOf(floor), BottomOf(pillar), 1e-4f);
        Assert.AreEqual(midBefore, pillar.MidHeightMM,
            "подпирать нечего — высота ноги остаётся пользовательской");
    }

    [Test]
    public void Seat_IgnoresABoardBeyondTheReachOfTheLeg()
    {
        var floor = Board(new Vector3(0f, -0.009f, 0f), new Vector3Int(3000, 18, 3000));
        var tooHigh = Board(new Vector3(0f, 0.4f, 0f), new Vector3Int(540, 16, 564));
        var pillar = Pillar(new Vector3(0f, 0.09f, 0f), 75);
        int midBefore = pillar.MidHeightMM;

        PillarAutoFit.Seat(pillar, Scene(floor, tooHigh, pillar));

        Assert.AreEqual(midBefore, pillar.MidHeightMM,
            $"нога тянется только на {PillarAutoFit.MinGapAboveMM}..{PillarAutoFit.MaxGapAboveMM} мм "
            + "над полом — столешницу под потолком опора подпирать не пытается");
    }

    [Test]
    public void RepairAfterGridSnap_SeatsThePillar_ButNeverRecomputesItsHeight()
    {
        var floor = Board(new Vector3(0f, -0.009f, 0f), new Vector3Int(3000, 18, 3000));
        var board = Board(new Vector3(0f, 0.108f, 0f), new Vector3Int(540, 16, 564));
        var pillar = Pillar(new Vector3(0f, 0.09f, 0f), 50);
        int midByHand = pillar.MidHeightMM;

        pillar.RepairJointAfterGridSnap(Scene(floor, board, pillar));

        Assert.AreEqual(midByHand, pillar.MidHeightMM,
            "проход после загрузки чинит СТЫК, а не размер: пересчёт по зазору здесь "
            + "переписал бы высоту из файла молча и без отмены");
        Assert.AreEqual(TopOf(floor), BottomOf(pillar), 1e-4f,
            "посадка на пол — это всё ещё его работа");
    }

    [Test]
    public void GapMM_RoundsDown_SoTheLegNeverGrowsThroughTheBoardAbove()
    {
        Assert.AreEqual(107, PillarAutoFit.GapMM(107.6f * U),
            "округление ВВЕРХ удлинило бы опору на пол-миллиметра СКВОЗЬ деталь: "
            + "невидимое пересечение, красная подсветка и откат всего перемещения");
        Assert.AreEqual(107, PillarAutoFit.GapMM(107f * U),
            "точное значение округление вниз портить не должно");
        Assert.AreEqual(108, PillarAutoFit.GapMM(107.95f * U),
            $"допуск {Tolerance.ClearanceMm} мм съедает float-шум, иначе 108 мм читались бы как 107");
    }

    [Test]
    public void MidHeightForGapMM_SubtractsTheFixedHeadAndFoot_AndStaysInRange()
    {
        int gap = 105;
        Assert.AreEqual(gap - PillarElement.TopHeightMM - PillarElement.BottomHeightMM,
            PillarAutoFit.MidHeightForGapMM(gap), "нога занимает зазор минус пятка и шляпка");

        Assert.AreEqual(PillarElement.MidHeightMM_Min, PillarAutoFit.MidHeightForGapMM(0));
        Assert.AreEqual(PillarElement.MidHeightMM_Max, PillarAutoFit.MidHeightForGapMM(10000));
    }
}
