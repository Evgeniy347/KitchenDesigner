using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Tests.Geometry;

/// <summary>Что предложение по паре граней ОБЯЗАНО замерить, и что оно имеет
/// право не мерить.
///
/// Правило одно на всех (AGENTS.md → «A rule added to candidate SELECTION must
/// reach Diagnose in the same commit»): отбор кандидатов и оракул строятся из
/// одной функции. Но потребители у неё разные. Отбор читает только принятые
/// предложения; оракул читает и отвергнутые, потому что человек через
/// `snap_diagnose` спрашивает «а почему НЕ прилипло» — и молчание в ответ
/// однажды уже стоило уверенного вранья про 100 мм.
///
/// Поэтому вход два: `For` меряет всё (оракул), `ForSelection` не тратит
/// перекрытие на пару, которую порог уже отверг (покадровый путь). РЕШЕНИЯ у
/// них обязаны совпадать до последнего поля — за это отвечает первый тест, и он
/// краснеет, если кто-нибудь начнёт «оптимизировать» ещё и правило.
///
/// Отдельно — признак `landsInsideNeighbour`. Выравнивание по дальней кромке
/// отбрасывается, когда сдвиг загнал бы деталь внутрь соседа
/// (SnapCoreContractTests.FarEdgeAlignment_ThatWouldDriveThePartIntoThe-
/// Neighbour_IsRejected). У ВЛОЖЕННЫХ деталей — окно в стене, дверь в
/// перегородке — это так при любом сдвиге, и пара не прилипнет никогда. Замер
/// шёл ПОСЛЕ выхода по «уже на месте», поэтому оракул этого не знал, а свип
/// `SnapMutationTests` требовал от таких пар прилипания и выдал 28 находок
/// EDGE-NOSNAP на стенах, дверях и окнах. Мерить — до отказа.</summary>
public class SnapPairOfferMeasurementTests : SnapCoreTestBase
{
    private const float MaxDist = Threshold + Tolerance.SnapEpsilon;

    private static readonly Face[] NoFaces = System.Array.Empty<Face>();

    private static ElementGeometry WindowInThatWall(Vector3 position)
        => At(Make("Okno", new Vector3Int(1790, 1400, 150), RotY(180f)), position);

    private static readonly Vector3 WallCentre = new Vector3(3.4425f, 1.35f, 6.47f);
    private static readonly Vector3 WindowCentre = new Vector3(3.825f, 1.5f, 6.47f);

    private static SnapPairOffer OfferFor(in ElementGeometry moved, Vector3 pos,
        in ElementGeometry other, int movedFace, int otherFace)
        => SnapPairOffer.For(moved, pos, other, moved.Faces[movedFace], other.Faces[otherFace],
            false, NoFaces, NoFaces, MaxDist);

    private static SnapPairOffer SelectionFor(in ElementGeometry moved, Vector3 pos,
        in ElementGeometry other, int movedFace, int otherFace)
        => SnapPairOffer.ForSelection(moved, pos, other, moved.Faces[movedFace],
            other.Faces[otherFace], false, NoFaces, NoFaces, MaxDist);

    private static List<(ElementGeometry moved, Vector3 pos, ElementGeometry other)> Scene()
    {
        var scene = new List<(ElementGeometry, Vector3, ElementGeometry)>();
        var neighbours = new List<ElementGeometry>
        {
            Floor(),
            Std("Beside", new Vector3(0.83f, 0.009f, 0f)),
            Std("Far", new Vector3(2.5f, 0.009f, 0f)),
            At(Make("Wall", new Vector3Int(3000, 2500, 100)), new Vector3(0f, 1.25f, 0.3f)),
            WindowInThatWall(WindowCentre),
        };

        var moved = MakeStd("Moved");
        foreach (var other in neighbours)
            for (int step = -6; step <= 6; step++)
            {
                var pos = new Vector3(step * 0.02f, 0.009f + step * 0.005f, step * 0.011f);
                scene.Add((moved.At(pos), pos, other));
            }

        var wall = Make("W150", new Vector3Int(3215, 2700, 150));
        for (int step = -3; step <= 3; step++)
        {
            var pos = WallCentre + new Vector3(0f, 0f, step * 0.005f);
            scene.Add((wall.At(pos), pos, WindowInThatWall(WindowCentre)));
        }

        return scene;
    }

    [Test]
    public void ForSelection_TakesTheSameDecisionAsFor_OnEveryPairOfAWholeScene()
    {
        int rejectedByThreshold = 0;
        int candidates = 0;

        foreach (var (moved, pos, other) in Scene())
            for (int i = 0; i < Face.BoxFaceCount; i++)
                for (int j = 0; j < Face.BoxFaceCount; j++)
                {
                    var full = OfferFor(moved, pos, other, i, j);
                    var fast = SelectionFor(moved, pos, other, i, j);
                    string pair = $"{moved.Name} m{i} / {other.Name} o{j} @ {pos}";

                    Assert.AreEqual(full.role, fast.role, $"роль разошлась: {pair}");
                    Assert.AreEqual(full.rejection, fast.rejection, $"причина отказа разошлась: {pair}");
                    Assert.AreEqual(full.accepted, fast.accepted, $"приём разошёлся: {pair}");
                    Assert.AreEqual(full.alreadyInPlace, fast.alreadyInPlace, $"«уже на месте» разошлось: {pair}");
                    Assert.AreEqual(full.landsInsideNeighbour, fast.landsInsideNeighbour,
                        $"«загонит внутрь соседа» разошлось: {pair}");
                    Assert.AreEqual(full.planeDist, fast.planeDist, Tol, $"зазор разошёлся: {pair}");
                    Assert.AreEqual(full.dist, fast.dist, Tol, $"длина сдвига разошлась: {pair}");
                    Assert.AreEqual(full.snapPos, fast.snapPos, $"позиция посадки разошлась: {pair}");

                    if (full.role == SnapPairRole.NotACandidate) continue;
                    candidates++;
                    if (full.rejection == SnapPairRejection.BeyondThreshold) rejectedByThreshold++;
                    else Assert.AreEqual(full.overlapRatio, fast.overlapRatio, Tol,
                        $"перекрытие обязано совпасть у пары, которую порог НЕ отверг: {pair}");
                }

        Assert.Greater(candidates, 100,
            "стенд обязан давать кандидатов, иначе сравнивать нечего");
        Assert.Greater(rejectedByThreshold, 50,
            "и обязан давать пары ЗА порогом — иначе разница между входами не проверена");
    }

    [Test]
    public void For_NamesTheOverlap_EvenOnAPairTheThresholdRejected()
    {
        var moved = MakeStd("Moved").At(Vector3.zero);
        var far = Std("Far", new Vector3(0f, 0f, 0.5f));

        var offer = OfferFor(moved, Vector3.zero, far, 4, 5);

        Assert.AreEqual(SnapPairRejection.BeyondThreshold, offer.rejection,
            "стенд обязан быть парой ЗА порогом");
        Assert.IsTrue(offer.hasOverlap,
            "оракул обязан получить число: ранний выход обесточивал отчёт, и snap_diagnose "
            + "уверенно врал про «0 мм, перекрытия нет» на деталях в 100 мм друг от друга");
        Assert.Greater(offer.overlapRatio, 0.9f, "грани совпадают целиком");
    }

    [Test]
    public void ForSelection_LeavesTheOverlapUnmeasured_OnAPairTheThresholdRejected()
    {
        var moved = MakeStd("Moved").At(Vector3.zero);
        var far = Std("Far", new Vector3(0f, 0f, 0.5f));

        var offer = SelectionFor(moved, Vector3.zero, far, 4, 5);

        Assert.AreEqual(SnapPairRejection.BeyondThreshold, offer.rejection,
            "стенд обязан быть парой ЗА порогом");
        Assert.IsFalse(offer.hasOverlap,
            "покадровый путь перекрытие такой пары не читает, поэтому и не считает: "
            + "отсечка по порогу стоит ПЕРЕД замером");
        Assert.AreEqual(0f, offer.overlapRatio, Tol,
            "и не докладывает перекрытия, которого не мерил");
    }

    [Test]
    public void NestedPart_IsMeasuredAsLandingInsideTheNeighbour_EvenWhileTheyAreFlush()
    {
        var wall = Make("W150", new Vector3Int(3215, 2700, 150)).At(WallCentre);
        var window = WindowInThatWall(WindowCentre);

        var offer = OfferFor(wall, WallCentre, window, 4, 5);

        Assert.AreEqual(SnapPairRole.FarEdgeAlignment, offer.role,
            "окно повёрнуто на 180°, поэтому лучшая пара плоскостей у стены с окном "
            + "СОНАПРАВЛЕННАЯ, а не встречная");
        Assert.IsTrue(offer.alreadyInPlace, "плоскости совпадают");
        Assert.IsTrue(offer.landsInsideNeighbour,
            "габарит окна лежит внутри габарита стены, поэтому выравнивание по этой грани "
            + "запрещено при ЛЮБОМ сдвиге — и оракул обязан это знать, не дожидаясь сдвига");

        var facts = SnapNeighbourFacts.Of(wall, WallCentre, window, MaxDist);
        Assert.IsTrue(facts.alignmentLandsInsideNeighbour,
            "признак обязан доехать до оракула: на нём свип отличает «не прилипло, и не должно» "
            + "от настоящей находки");
    }

    [Test]
    public void PartStandingBesideTheNeighbour_IsNotMeasuredAsLandingInsideIt()
    {
        var shelf = Make("Shelf", new Vector3Int(600, 18, 400));
        var pos = new Vector3(0f, 0.5f, 0f);
        var pillar = At(Make("Pillar", new Vector3Int(100, 1000, 400)), new Vector3(0.35f, 0.5f, 0f));

        var offer = OfferFor(shelf.At(pos), pos, pillar, 2, 2);

        Assert.AreEqual(SnapPairRole.FarEdgeAlignment, offer.role,
            "верх полки против верха стойки — сонаправленные грани");
        Assert.IsFalse(offer.landsInsideNeighbour,
            "полка стоит РЯДОМ со стойкой, а не внутри неё: выравнивание по дальней кромке "
            + "для таких пар и придумано, и запрещать его нечем");
    }

    [Test]
    public void OnlyTheCandidateCollector_MayAskForTheCheaperMeasurement()
    {
        string core = RepoPaths.Subdir("Assets", "Scripts", "Core");
        var offenders = new List<string>();

        foreach (var path in System.IO.Directory.GetFiles(core, "*.cs",
                     System.IO.SearchOption.AllDirectories))
        {
            string name = System.IO.Path.GetFileName(path) ?? string.Empty;
            if (name == "SnapPairOffer.cs" || name == "SnapCandidateCollector.cs") continue;
            if (System.IO.File.ReadAllText(path).Contains("ForSelection")) offenders.Add(name);
        }

        CollectionAssert.IsEmpty(offenders,
            "ForSelection экономит замер перекрытия у пары, которую порог уже отверг — это "
            + "законно только для покадрового отбора, который такое перекрытие не читает. "
            + "Оракул (SnapNeighbourFacts → snap_diagnose) обязан звать For: ранний выход "
            + "в отчёте уже приводил к уверенному вранью про «0 мм, перекрытия нет» на "
            + "деталях в 100 мм друг от друга. Нашлось в: " + string.Join(", ", offenders));
    }

    [Test]
    public void NestedPart_PushedOffTheCommonPlane_IsLeftWhereItIs()
    {
        var wall = Make("W150", new Vector3Int(3215, 2700, 150));
        var window = WindowInThatWall(WindowCentre);
        var pushed = WallCentre + new Vector3(0f, 0f, 5f * MM);

        var result = Snap(wall, window, pushed);

        Assert.IsFalse(result.snapped,
            "стена ушла на 5 мм от плоскости окна, и вернуть её нельзя: возврат — это "
            + "выравнивание, которое загоняет стену внутрь окна. Свип требовал здесь "
            + "прилипания и давал 28 ложных EDGE-NOSNAP");
    }
}
