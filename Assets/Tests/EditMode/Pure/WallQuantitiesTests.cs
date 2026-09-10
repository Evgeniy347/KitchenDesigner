using System;
using System.Collections.Generic;
using NUnit.Framework;
using KitchenDesigner.Core.Construction;

/// <summary>Счёт кладки — первое число сметы дома, и оно обязано быть проверяемым
/// на калькуляторе. Поэтому головной тест здесь несёт ответ в ИМЕНИ, а не только
/// в теле: 3 000 × 2 700 × 250 мм = 2,025 м³; кирпич 250×120×65 со швом 10 занимает
/// 0,26 × 0,13 × 0,075 = 0,002535 м³; 2,025 / 0,002535 = 798,82 → 799 штук.
///
/// Тело теста НЕ пересчитывает формулу продукта (иначе продукт не вызывается вовсе —
/// conventions/TEST-NAMING.md, «A test that re-derives the calculation is not testing
/// anything»): здесь стоит литерал 799, посчитанный руками.
///
/// Раствор считается по УЛОЖЕННЫМ штукам, а не по купленным: запас лежит в поддоне,
/// а не в стене, и если умножать на купленное количество, объём раствора будет
/// УМЕНЬШАТЬСЯ с ростом запаса. Отсюда два поля — PiecesLaid и Pieces.</summary>
public class WallQuantitiesTests
{
    private static readonly IReadOnlyList<WallOpening> NoOpenings = Array.Empty<WallOpening>();

    [Test]
    public void WallQuantities_BrickSingle_Joint10_Spare0_Wall3000x2700x250_NoOpenings_Is799Pieces()
    {
        var result = WallQuantities.Of(MasonryTechnology.BrickSingle,
            3000f, 2700f, 250f, NoOpenings, 10f, 0f);

        Assert.AreEqual(2.025d, result.NetVolumeM3, 1e-9d,
            "3,0 × 2,7 × 0,25 = 2,025 м³ — объём кладки без проёмов");
        Assert.AreEqual(799, result.Pieces,
            "2,025 м³ / 0,002535 м³ = 798,82, вверх до целого = 799 штук. Это первое число "
            + "сметы дома: пользователь проверяет его на калькуляторе, поэтому ответ стоит "
            + "в ИМЕНИ теста, а не только здесь");
        Assert.AreEqual(799, result.PiecesLaid,
            "при запасе 0 уложенное и купленное количество совпадают");
    }

    [Test]
    public void WallQuantities_BrickSingle_Mortar_IsTheVolumeTheBricksThemselvesDoNotFill()
    {
        var result = WallQuantities.Of(MasonryTechnology.BrickSingle,
            3000f, 2700f, 250f, NoOpenings, 10f, 0f);

        Assert.AreEqual(2.025d - 799 * 0.00195d, result.MortarM3, 1e-9d,
            "раствор = 2,025 − 799 × 0,00195 = 0,46695 м³. Умножать надо на объём кирпича "
            + "БЕЗ шва: возьми объём со швом — и раствора выйдет ноль, потому что шов уже "
            + "сидит внутри каждого кирпича");
        Assert.Greater(result.MortarM3, 0d,
            "контроль: раствор положителен — иначе формула вычитает саму себя");
        Assert.Less(result.MortarM3, result.NetVolumeM3,
            "контроль: раствора меньше, чем всей кладки");
    }

    [Test]
    public void WallQuantities_Spare5Percent_BuysMoreBricks_ButLaysTheSameNumberAndTheSameMortar()
    {
        var bare = WallQuantities.Of(MasonryTechnology.BrickSingle,
            3000f, 2700f, 250f, NoOpenings, 10f, 0f);
        var spared = WallQuantities.Of(MasonryTechnology.BrickSingle,
            3000f, 2700f, 250f, NoOpenings, 10f, 5f);

        Assert.AreEqual(839, spared.Pieces,
            "799 × 1,05 = 838,95, вверх до целого = 839 штук к покупке");
        Assert.AreEqual(bare.PiecesLaid, spared.PiecesLaid,
            "запас не кладут в стену: уложенное количество от запаса не зависит");
        Assert.AreEqual(bare.MortarM3, spared.MortarM3, 1e-9d,
            "и раствора столько же. Считай раствор по КУПЛЕННЫМ штукам — и он начнёт "
            + "уменьшаться с ростом запаса, вплоть до нуля: это и есть тот дефект, "
            + "против которого разделены PiecesLaid и Pieces");
    }

    [Test]
    public void WallQuantities_Openings_AreSubtractedAtTheFullWallThickness()
    {
        var window = new WallOpening("Okno-1", 1200f, 1400f);
        var result = WallQuantities.Of(MasonryTechnology.BrickSingle,
            3000f, 2700f, 250f, new[] { window }, 10f, 0f);

        Assert.AreEqual(1.2d * 1.4d * 0.25d, result.OpeningsVolumeM3, 1e-9d,
            "проём вырезает стену НАСКВОЗЬ: 1,2 × 1,4 × 0,25 = 0,42 м³");
        Assert.AreEqual(2.025d - 0.42d, result.NetVolumeM3, 1e-9d);
        Assert.AreEqual(634, result.Pieces,
            "1,605 / 0,002535 = 633,14, вверх до целого = 634 штуки — на 165 кирпичей "
            + "меньше глухой стены");
    }

    [Test]
    public void WallQuantities_OpeningsBiggerThanTheWall_GiveZero_NotANegativeOrder()
    {
        var result = WallQuantities.Of(MasonryTechnology.BrickSingle,
            1000f, 1000f, 250f, new[] { new WallOpening("Vorota", 2000f, 2000f) }, 10f, 0f);

        Assert.AreEqual(0d, result.NetVolumeM3, 1e-9d,
            "проём шире стены — это ошибка ввода, но смета обязана показать ноль, "
            + "а не отрицательный объём и не отрицательное число кирпичей");
        Assert.AreEqual(0, result.Pieces);
        Assert.AreEqual(0d, result.MortarM3, 1e-9d);
    }

    [Test]
    public void WallQuantities_AeratedBlock_Joint10_Wall3000x2700x300_Is62Blocks()
    {
        var result = WallQuantities.Of(MasonryTechnology.AeratedBlock,
            3000f, 2700f, 300f, NoOpenings, 10f, 0f);

        Assert.AreEqual(2.43d, result.NetVolumeM3, 1e-9d, "3,0 × 2,7 × 0,3 = 2,43 м³");
        Assert.AreEqual(62, result.Pieces,
            "блок со швом 10: 0,61 × 0,21 × 0,31 = 0,039711 м³; 2,43 / 0,039711 = 61,19, "
            + "вверх до целого = 62 блока. Шов газоблока считается той же формулой, что и "
            + "у кирпича — на клей его ставят в 2–3 мм, и это значение настройки, а не кода");
        Assert.AreEqual(27, (int)Math.Floor(1d
                / MasonryUnit.Of(MasonryTechnology.AeratedBlock).BareVolumeM3),
            "контроль формата: без шва в кубометре 1 / (0,6 × 0,3 × 0,2) = 27,8 блока — "
            + "справочные 27,8 шт/м³ для 600×300×200");
    }

    [Test]
    public void WallQuantities_Timber_CountsCubicMetres_AndNeitherPiecesNorMortar()
    {
        var result = WallQuantities.Of(MasonryTechnology.Timber,
            3000f, 2700f, 200f, NoOpenings, 10f, 5f);

        Assert.AreEqual(MasonryCounting.Volume, result.Counting);
        Assert.AreEqual(1.62d, result.TimberM3, 1e-9d,
            "брус: м³ = объём стены, 3,0 × 2,7 × 0,2 = 1,62 м³");
        Assert.AreEqual(result.NetVolumeM3, result.TimberM3, 1e-9d);
        Assert.AreEqual(0, result.Pieces, "у бруса нет «штук» — считать их нечем");
        Assert.AreEqual(0d, result.MortarM3, 1e-9d, "и раствора у бруса нет");
    }

    [Test]
    public void WallQuantities_Frame_Wall3000Long_Has6Studs_AtThe600mmInsulationStep()
    {
        var result = WallQuantities.Of(MasonryTechnology.Frame,
            3000f, 2700f, 200f, NoOpenings, 10f, 0f);

        Assert.AreEqual(MasonryCounting.Studs, result.Counting);
        Assert.AreEqual(6, result.Studs,
            "3 000 / 600 = 5 пролётов, значит 6 стоек — обе крайние считаются. "
            + "Забудь «+1» — и на каждой стене недостанет по стойке");
        Assert.AreEqual(6 * 2.7d, result.StudMetres, 1e-9d, "6 стоек по 2,7 м = 16,2 м.п.");
        Assert.AreEqual(2 * 3.0d, result.PlateMetres, 1e-9d,
            "нижняя и верхняя обвязки — по длине стены каждая");
        Assert.AreEqual(16.2d + 6.0d, result.RunningMetres, 1e-9d);
        Assert.AreEqual(0, result.Pieces, "каркас не считается штуками кладки");
        Assert.AreEqual(0d, result.MortarM3, 1e-9d);
    }

    [Test]
    public void WallQuantities_Frame_StudCount_GrowsOnlyWhenAWholeStepIsCrossed()
    {
        Assert.AreEqual(1, WallQuantities.StudCount(1f), "любая стена начинается со стойки");
        Assert.AreEqual(2, WallQuantities.StudCount(600f), "ровно один шаг — две стойки");
        Assert.AreEqual(2, WallQuantities.StudCount(1199f),
            "1 199 мм — ещё не два шага: лишняя стойка это лишние деньги в каждой стене");
        Assert.AreEqual(3, WallQuantities.StudCount(1200f));
        Assert.AreEqual(0, WallQuantities.StudCount(0f),
            "у стены нулевой длины стоек нет — иначе смета покажет стойку в пустоте");
    }

    [Test]
    public void WallQuantities_ZeroJoint_StillCounts_AndNeedsMoreBricksThanAJointedWall()
    {
        var jointed = WallQuantities.Of(MasonryTechnology.BrickSingle,
            3000f, 2700f, 250f, NoOpenings, 10f, 0f);
        var dry = WallQuantities.Of(MasonryTechnology.BrickSingle,
            3000f, 2700f, 250f, NoOpenings, 0f, 0f);

        Assert.AreEqual(1039, dry.Pieces,
            "без шва: 2,025 / 0,00195 = 1 038,46 → 1 039 штук");
        Assert.Greater(dry.Pieces, jointed.Pieces,
            "шов — это объём, который не занят кирпичом: чем он толще, тем меньше штук. "
            + "Разъедься знак — и смета будет расти от шва вместо того, чтобы падать");
        Assert.AreEqual(0d, dry.MortarM3, 1e-6d,
            "при нулевом шве раствора нет: 1 039 × 0,00195 = 2,026 ≥ 2,025, обрезается до нуля");
    }

    [Test]
    public void WallQuantities_NullOpenings_AreReadAsAWallWithoutOpenings()
    {
        var result = WallQuantities.Of(MasonryTechnology.BrickSingle,
            3000f, 2700f, 250f, null, 10f, 0f);

        Assert.AreEqual(799, result.Pieces,
            "стена, у которой список проёмов ещё не собран, обязана считаться как глухая, "
            + "а не падать: проводка со стороны сцены отдаёт null до первого проёма");
    }
}
