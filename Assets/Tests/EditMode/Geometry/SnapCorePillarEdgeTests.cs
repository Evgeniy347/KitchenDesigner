using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Опора (50×105×50) на полу, верхом впритык к дну короба 600×18×500.
///
/// Баг, ради которого набор заведён: горизонтального прилипания не было вовсе.
/// Опора всегда касается пола ВСЕЙ подошвой, а её боковая грань делит с дном
/// короба ровно ребро (кромочный контакт). Гейт в `SnapCore` отбрасывал ЛЮБОЙ
/// кромочный кандидат, если у детали уже есть полноплощадный нулевой контакт, —
/// то есть у опоры всегда. Снаружи это выглядело как «по вертикали детент
/// работает, по горизонтали опора не липнет».
///
/// Гейт заводился под `SnapCoreLineContactTests` (полка на 1.369 не должна
/// падать на кромочный детент 1.351) и там нужен, но по другой причине: тот
/// кандидат РВЁТ уже существующее выравнивание по своей оси. У опоры по X и Z
/// никакого выравнивания заранее нет, поэтому кандидат обязан пройти.</summary>
public class SnapCorePillarEdgeTests : SnapCoreTestBase
{
    private const float BoardHalfX = 300f * MM;
    private const float BoardHalfZ = 250f * MM;
    private const float BoardBottomY = 105f * MM;

    private const float PillarHalf = 25f * MM;
    private const float PillarCentreY = 52.5f * MM;

    private const float Approach = 9f * MM;
    private const float BeyondThreshold = 60f * MM;

    private static ElementGeometry Board() =>
        At(Make("Box_bottom", new Vector3Int(600, 18, 500)),
            new Vector3(0f, BoardBottomY + 9f * MM, 0f));

    private static Box Pillar() => Make("Pillar", new Vector3Int(50, 105, 50));

    private static List<ElementGeometry> Scene() => new List<ElementGeometry> { Floor(), Board() };

    private static Vector3 At(float x, float z) => new Vector3(x, PillarCentreY, z);

    [Test]
    public void Pillar_DraggedNearBoardEdge_SnapsFlushToEdgeAlongX()
    {
        float flushX = BoardHalfX + PillarHalf;
        var r = Snap(Pillar(), Scene(), At(flushX + Approach, 0f));

        Assert.IsTrue(r.snapped, "опора обязана прилипнуть к кромке дна");
        Assert.AreEqual(flushX, r.position.x, Tol,
            "бок опоры встаёт заподлицо с кромкой дна: кромочный контакт наверху не повод отказать");
    }

    [Test]
    public void Pillar_DraggedNearBoardEdge_SnapsFlushToEdgeAlongZ()
    {
        float flushZ = BoardHalfZ + PillarHalf;
        var r = Snap(Pillar(), Scene(), At(0f, flushZ + Approach));

        Assert.IsTrue(r.snapped, "вторая горизонтальная ось ведёт себя так же, как первая");
        Assert.AreEqual(flushZ, r.position.z, Tol, "бок опоры заподлицо с кромкой дна по Z");
    }

    [Test]
    public void Pillar_SnappedToBoardEdge_KeepsFloorAndBoardContacts()
    {
        float flushX = BoardHalfX + PillarHalf;
        var r = Snap(Pillar(), Scene(), At(flushX + Approach, 0f));

        Assert.AreEqual(PillarCentreY, r.position.y, Tol,
            "горизонтальный снэп не поднимает опору: подошва остаётся на полу, верх — у дна короба");
        Assert.AreEqual(0f, r.position.z, Tol, "по свободной оси опору не таскает");
    }

    [Test]
    public void Pillar_SnappedToBoardEdge_DoesNotIntersectBoard()
    {
        var pillar = Pillar();
        var r = Snap(pillar, Scene(), At(BoardHalfX + PillarHalf + Approach, 0f));

        Assert.IsFalse(Intersect(pillar.At(r.position), Board()),
            "после снэпа опора стоит вплотную к дну, а не внутри него");
    }

    [Test]
    public void Pillar_BeyondThreshold_DoesNotMoveToBoardEdge()
    {
        float startX = BoardHalfX + PillarHalf + BeyondThreshold;
        var r = Snap(Pillar(), Scene(), At(startX, 0f));

        Assert.AreEqual(startX, r.position.x, Tol,
            "60 мм больше порога 50 мм — притяжения к кромке нет");
    }

    [Test]
    public void Pillar_UnderBoard_SnapsFlushToEdgeFromInside()
    {
        float flushX = BoardHalfX - PillarHalf;
        var r = Snap(Pillar(), Scene(), At(flushX - Approach, 0f));

        Assert.IsTrue(r.snapped, "изнутри короба опора тоже ловит кромку дна");
        Assert.AreEqual(flushX, r.position.x, Tol, "бок опоры заподлицо с кромкой дна изнутри");
    }
}
