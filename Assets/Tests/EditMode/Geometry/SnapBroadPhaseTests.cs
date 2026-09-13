using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Цена кадра перетаскивания, названная дампом пользователя от 13.09:
/// <c>ElementMover.ApplyDragFrame</c> 15,4 мс, и <b>11,9 мс из них —
/// <c>SnapSystem.TrySnap</c></b>, то есть 78 % жеста и самое дорогое, что
/// осталось у пользователя в руках.
///
/// Внутри не оказалось широкой фазы вообще. <c>SnapCandidateCollector.Collect</c>
/// заходил в перебор граней 6×6 для КАЖДОГО соседа независимо от расстояния: на
/// 411 деталях это ~14 800 примерок пар граней за проход, а проходов у
/// <c>SnapCore</c> до четырёх — около 59 000 примерок на кадр ради нескольких
/// соседей, до которых деталь реально дотягивается. Та же дыра была закрыта в
/// валидации отбором по огибающим; на пути снэпа её просто не было.
///
/// Отсечка обязана быть КОНСЕРВАТИВНОЙ, и это доказывается, а не предполагается.
/// Предложение пары граней выживает только если (1) центры граней сходятся вдоль
/// нормали не дальше <c>maxDist</c> (<c>SnapPairOffer</c> → <c>BeyondThreshold</c>)
/// и (2) грани перекрываются в своей плоскости. Из (2) следует, что в области
/// перекрытия есть точка p на одной грани и q на другой, отличающиеся только
/// вдоль нормали, а из (1) — что |p−q| ≤ maxDist. Обе точки лежат в своих
/// коробках, значит по каждой мировой оси интервалы коробок, раздвинутые на
/// maxDist, пересекаются. Обратное неверно — отсечка пропускает лишних, — и это
/// ровно то, чего от неё требуется: она не имеет права отбросить пару, которая
/// МОГЛА бы дать кандидата.
///
/// Поэтому здесь два рода тестов, и второй важнее первого. Счётчики говорят,
/// сколько работы ушло; прилипание говорит, что ответ не поменялся — деталь,
/// подведённая к грани, липнет ровно туда же, а подведённая к чужой, не липнет.
/// Граница порога проверяется отдельно: сосед РОВНО на пороге обязан и
/// рассматриваться, и притягивать, иначе отсечка съела бы последний миллиметр,
/// которым пользователь пользуется руками.</summary>
public class SnapBroadPhaseTests : SnapCoreTestBase
{
    private const float BoxWidth = 0.8f;

    [SetUp]
    public void SetUp()
    {
        SnapCandidateCollector.TakeNeighboursSeen();
        SnapCandidateCollector.TakeNeighboursExamined();
    }

    /// <summary>Сосед вплотную слева и <paramref name="far"/> деталей, унесённых
    /// по X на километр: они в сцене есть, и до правки каждая стоила перебора
    /// 6×6 на каждом из проходов.</summary>
    private static List<ElementGeometry> ANeighbourAndSomeStrangers(int far)
    {
        var scene = new List<ElementGeometry> { Std("Target", Vector3.zero) };
        for (int i = 0; i < far; i++)
            scene.Add(Std("Far" + i, new Vector3(1000f + i * 5f, 0f, 0f)));
        return scene;
    }

    private static Vector3 NearTheTarget(float gapMM) =>
        new Vector3(BoxWidth + gapMM * MM, 0f, 0f);

    [Test]
    public void Collect_LooksAtEveryNeighbour_ButExaminesOnlyTheOnesWithinReach()
    {
        var scene = ANeighbourAndSomeStrangers(60);

        Snap(MakeStd("Moved"), scene, NearTheTarget(10f));

        int seen = SnapCandidateCollector.TakeNeighboursSeen();
        int examined = SnapCandidateCollector.TakeNeighboursExamined();

        Assert.GreaterOrEqual(seen, 60,
            "широкая фаза обязана ВИДЕТЬ всех — иначе она не отсекает, а теряет");
        Assert.LessOrEqual(examined, 4,
            $"в перебор граней 6×6 зашло {examined} соседей из {seen}: до правки "
            + "туда заходили все, и это стоило ~59 000 примерок пар граней на кадр");
    }

    [Test]
    public void Collect_ExaminesTheSameFewNeighbours_WhenTheSceneDoubles()
    {
        Snap(MakeStd("Moved"), ANeighbourAndSomeStrangers(30), NearTheTarget(10f));
        SnapCandidateCollector.TakeNeighboursSeen();
        int small = SnapCandidateCollector.TakeNeighboursExamined();

        Snap(MakeStd("Moved"), ANeighbourAndSomeStrangers(60), NearTheTarget(10f));
        SnapCandidateCollector.TakeNeighboursSeen();
        int twiceAsBig = SnapCandidateCollector.TakeNeighboursExamined();

        Assert.AreEqual(small, twiceAsBig,
            $"вдвое большая сцена дала {twiceAsBig} разборов пар вместо {small}: "
            + "работа снова растёт со сценой, а не с окрестностью детали");
    }

    [Test]
    public void APartBroughtToAFace_StillSticksToIt_ThroughACrowdedScene()
    {
        var scene = ANeighbourAndSomeStrangers(40);

        for (int gapMM = 1; gapMM <= 50; gapMM += 7)
        {
            var r = SnapCore.TrySnap(MakeStd("Moved"), scene, NearTheTarget(gapMM), Threshold);
            Assert.IsTrue(r.snapped, $"зазор {gapMM} мм: деталь перестала липнуть к грани");
            Assert.AreEqual(BoxWidth, r.position.x, Tol,
                $"зазор {gapMM} мм: прилипла не заподлицо");
            Assert.AreEqual(0f, r.position.y, Tol, $"зазор {gapMM} мм: уехала по Y");
            Assert.AreEqual(0f, r.position.z, Tol, $"зазор {gapMM} мм: уехала по Z");
        }
    }

    [Test]
    public void APartAtTheVeryThreshold_IsStillExaminedAndStillSticks()
    {
        var scene = ANeighbourAndSomeStrangers(40);

        var r = SnapCore.TrySnap(MakeStd("Moved"), scene, NearTheTarget(50f), Threshold);

        Assert.Greater(SnapCandidateCollector.TakeNeighboursExamined(), 0,
            "сосед РОВНО на пороге обязан дойти до разбора пар: отсечка, съедающая "
            + "последний миллиметр порога, отнимает его у пользователя молча");
        Assert.IsTrue(r.snapped, "и обязан притянуть");
        Assert.AreEqual(BoxWidth, r.position.x, Tol,
            "с порога деталь обязана встать заподлицо, а не куда-нибудь между");
    }

    [Test]
    public void APartBroughtToAStrangersFace_DoesNotStick()
    {
        var scene = ANeighbourAndSomeStrangers(40);

        var far = SnapCore.TrySnap(MakeStd("Moved"), scene,
            new Vector3(BoxWidth + 300f * MM, 0f, 0f), Threshold);

        Assert.IsFalse(far.snapped,
            "деталь в 300 мм от соседа не имеет права прилипать — порог 50 мм");
    }

    [Test]
    public void TheReachTest_KeepsThePairThatSitsExactlyAtTheDistance()
    {
        var a = ElementGeometry.Box("A", Vector3.zero, Vector3.one);
        var touching = ElementGeometry.Box("B", new Vector3(1f, 0f, 0f), Vector3.one);
        var atReach = ElementGeometry.Box("B", new Vector3(1.5f, 0f, 0f), Vector3.one);
        var beyond = ElementGeometry.Box("B", new Vector3(1.75f, 0f, 0f), Vector3.one);

        Assert.IsTrue(a.WithinReachOf(touching, 0f), "касающиеся коробки — в пределах нуля");
        Assert.IsTrue(a.WithinReachOf(atReach, 0.5f),
            "зазор ровно 0,5 при допуске 0,5 — граница ВКЛЮЧЕНА, иначе снэп теряет порог");
        Assert.IsFalse(a.WithinReachOf(beyond, 0.5f),
            "зазор 0,75 при допуске 0,5 — отсечка обязана сработать, иначе она бесполезна");
    }

    [Test]
    public void TheReachTest_IsSymmetric()
    {
        var a = ElementGeometry.Box("A", Vector3.zero, Vector3.one);
        var b = ElementGeometry.Box("B", new Vector3(1.4f, 0.9f, 0f), Vector3.one);

        Assert.AreEqual(a.WithinReachOf(b, 0.5f), b.WithinReachOf(a, 0.5f),
            "несимметричная отсечка отбрасывала бы пару в зависимости от того, "
            + "кого из двоих ведёт мышь");
    }

    [Test]
    public void TheReachTest_SeparatesOnAnyAxis_NotOnlyTheFirst()
    {
        var a = ElementGeometry.Box("A", Vector3.zero, Vector3.one);

        Assert.IsFalse(a.WithinReachOf(
            ElementGeometry.Box("Y", new Vector3(0f, 2f, 0f), Vector3.one), 0.5f), "разъехались по Y");
        Assert.IsFalse(a.WithinReachOf(
            ElementGeometry.Box("Z", new Vector3(0f, 0f, 2f), Vector3.one), 0.5f), "разъехались по Z");
    }
}
