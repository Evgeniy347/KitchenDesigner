using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Контракт ядра прилипания: то, на что опирается ВСЁ остальное и что
/// раньше держалось только на комментариях в исходнике.
///
/// Четыре темы:
///  - чистота: одинаковый вход даёт одинаковый выход, и сцену снэп не двигает;
///  - добор по свободным осям (три прохода) — в углу одного кандидата мало;
///  - два запрета «не загонять деталь ВНУТРЬ соседа» — один для выравнивания по
///    дальней кромке, второй для выравнивания по центру;
///  - цена кандидата: конкуренцию выигрывает МЕНЬШИЙ ЗАЗОР, а не меньший
///    суммарный сдвиг.</summary>
public class SnapCoreContractTests : SnapCoreTestBase
{
    [Test]
    public void TrySnap_OnTheSameInput_ReturnsTheSameResultEveryTime()
    {
        var moved = MakeStd("Moved");
        var scene = new List<ElementGeometry> { Floor(), Std("N", new Vector3(0.9f, 0.009f, 0f)) };
        var testPos = new Vector3(0.06f, 0.012f, 0f);

        var first = SnapCore.TrySnap(moved, scene, testPos, Threshold);
        var second = SnapCore.TrySnap(moved, scene, testPos, Threshold);

        Assert.AreEqual(first.snapped, second.snapped);
        Assert.AreEqual(first.position, second.position,
            "снэп обязан быть чистой функцией от (moved, others, testPos): скрытого "
            + "статического состояния между вызовами быть не должно");
        Assert.AreEqual(first.targetName, second.targetName);
    }

    [Test]
    public void TrySnap_LeavesTheNeighbourGeometryUntouched()
    {
        // Геометрия примеряемой позиции считается аналитически (IPosedGeometry.At),
        // а не записью в позу детали. Иначе каждый проход добора грязнил бы сцену,
        // и пересчёт оплачивал бы тот, кто следующим её читает.
        var neighbour = Std("N", new Vector3(0.9f, 0.009f, 0f));
        Vector3 minBefore = neighbour.Min, maxBefore = neighbour.Max;
        Vector3 face0Before = neighbour.Faces[0].center;

        SnapCore.TrySnap(MakeStd("Moved"), new List<ElementGeometry> { Floor(), neighbour },
            new Vector3(0.06f, 0.012f, 0f), Threshold);

        Assert.AreEqual(minBefore, neighbour.Min, "снимок соседа неизменяем");
        Assert.AreEqual(maxBefore, neighbour.Max, "снимок соседа неизменяем");
        Assert.AreEqual(face0Before, neighbour.Faces[0].center, "грани соседа не сдвинулись");
    }

    [Test]
    public void TrySnap_WithNoMovedPartOrNoScene_ReturnsNoSnap()
    {
        Assert.IsFalse(SnapCore.TrySnap(null!, new List<ElementGeometry>(), Vector3.zero,
            Threshold).snapped, "нет детали — нечему прилипать");
        Assert.IsFalse(SnapCore.TrySnap(MakeStd("Moved"), null!, Vector3.zero,
            Threshold).snapped, "нет сцены — не к чему прилипать");
    }

    [Test]
    public void InACorner_BothAxesEndUpFlush_NotJustTheNearerOne()
    {
        // Добор: один кандидат чинит одну ось. В углу (бок соседа + стена) без
        // второго прохода деталь «прилипала», но по второй оси оставался зазор —
        // снэп сработал, а подсветка красная.
        var moved = Make("Moved", new Vector3Int(800, 18, 400));
        var side = At(Make("Side", new Vector3Int(800, 18, 400)), new Vector3(0.83f, 0.009f, 0f));
        var wall = At(Make("Wall", new Vector3Int(3000, 2500, 100)),
            new Vector3(0f, 1.25f, 0.3f));

        var testPos = new Vector3(0.0f, 0.009f, 0.03f);
        var r = SnapCore.TrySnap(moved, new List<ElementGeometry> { Floor(), side, wall },
            testPos, Threshold);

        Assert.IsTrue(r.snapped);
        Assert.AreEqual(0.03f, r.position.x, Tol,
            "по X деталь встала торцом к боковине (0.83 − 0.8)");
        Assert.AreEqual(0.05f, r.position.z, Tol,
            "и ПО Z тоже — до стены (0.25 − 0.2); ось Z чинит проход добора, "
            + "первый проход закрывает только одну ось");
    }

    [Test]
    public void FarEdgeAlignment_ThatWouldDriveThePartIntoTheNeighbour_IsRejected()
    {
        // Заподлицо с ДАЛЬНЕЙ гранью встают детали РЯДОМ с соседом (стойка у
        // кромки полки). Если деталь перекрывает соседа по остальным осям,
        // «выравнивание» загоняет её внутрь — так низ короба уезжал в стену.
        var moved = Make("Moved", new Vector3Int(600, 18, 400));
        var wall = At(Make("Wall", new Vector3Int(3000, 2500, 100)),
            new Vector3(0f, 1.25f, 0f));

        // Полка внутри стены по X и Z; её низ на 20 мм выше низа стены.
        var testPos = new Vector3(0f, 0.02f, 0f);
        var r = SnapCore.TrySnap(moved, new List<ElementGeometry> { wall }, testPos, Threshold);

        Assert.Greater(Mathf.Abs(r.position.y - 0.009f), Tol,
            "выравнивание низа полки по низу стены загнало бы её В стену — такой "
            + "кандидат обязан отбрасываться");
    }

    [Test]
    public void CentreAlignment_ThatWouldPutThePartInsideTheNeighbour_IsRejected()
    {
        // У тонкой панели центр совпадает с центром такой же панели-соседа, и
        // «выравнивание по центру» давало полное наложение вместо контакта.
        var moved = Make("Moved", new Vector3Int(600, 700, 18));
        var door = At(Make("Door", new Vector3Int(600, 700, 18)), new Vector3(0f, 0.5f, 1.244f));

        var testPos = new Vector3(0f, 0.5f, 1.226f);
        var r = SnapCore.TrySnap(moved, new List<ElementGeometry> { door }, testPos, Threshold);

        Assert.Greater(Mathf.Abs(r.position.z - 1.244f), Tol,
            "центр детали внутри габарита соседа — это наложение, а не снэп");
    }

    [Test]
    public void NearerGapWins_EvenWhenTheFartherCandidateAlsoAlignsAnEdge()
    {
        // Кандидат оценивается ЗАЗОРОМ (planeShift), а не суммарным сдвигом:
        // выравнивание по кромке/центру — бесплатный довесок к контакту, и оно не
        // вправе проигрывать контакт. Дно короба в 2 мм над ногой уступало стене в
        // 7 мм только потому, что заодно центровалось по опоре (7 мм по X).
        var moved = Make("Moved", new Vector3Int(600, 18, 400));

        // Нога: узкая, стоит на 7 мм в стороне по X — контакт по Y с зазором 2 мм.
        var foot = At(Make("Foot", new Vector3Int(100, 100, 400)),
            new Vector3(0.007f, -0.05f, 0f));
        // Стена: далеко по Y (зазор 7 мм), но огромная.
        var wall = At(Make("Wall", new Vector3Int(3000, 100, 3000)),
            new Vector3(0f, -0.055f, 0f));

        var testPos = new Vector3(0f, 0.011f, 0f);
        var r = SnapCore.TrySnap(moved, new List<ElementGeometry> { foot, wall }, testPos,
            Threshold);

        Assert.IsTrue(r.snapped);
        Assert.AreEqual("Foot", r.targetName,
            "зазор 2 мм до ноги ближе зазора 7 мм до стены; довесок выравнивания по X "
            + "в цену контакта не входит");
    }

    [Test]
    public void RotatedPair_WhoseAABBsAlreadyOverlap_StillSnaps()
    {
        // Отсечка кандидатов по AABB сознательно НЕ делается. У детали,
        // повёрнутой на 45°, габарит заведомо шире тела: две такие детали в 10 мм
        // друг от друга уже «пересекаются» габаритами, хотя телами не касаются.
        var rot = RotY(45f);
        Vector3 dir = rot * Vector3.right;
        var a = At(MakeStd("A", rot), Vector3.zero);
        var b = MakeStd("B", rot);
        Vector3 testPos = dir * 0.81f;

        Assert.IsTrue(Intersect(a, At(b, testPos)),
            "сцена собрана неправильно: габариты обязаны пересекаться, иначе тест "
            + "зелен независимо от того, отсекают ли кандидатов по AABB");

        var r = Snap(b, a, testPos);

        Assert.IsTrue(r.snapped,
            "тела в 10 мм друг от друга — это снэп; отсечка по габаритам убила бы его");
        Assert.AreEqual((dir * 0.8f).x, r.position.x, Tol);
        Assert.AreEqual((dir * 0.8f).z, r.position.z, Tol);
    }
}
