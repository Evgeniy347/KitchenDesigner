using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Сосед, которого деталь КАСАЕТСЯ уже в закрытом виде, — это её
/// рама: корпус ящика, боковина шкафа. Раньше такой сосед выбрасывался
/// целиком, и фасад на чашечной петле проходил СКВОЗЬ боковину высокого
/// модуля, выступающую на 200 мм вперёд: боковина касалась двери кромкой и
/// потому не проверялась вовсе. Гасить надо не соседа, а его ТЕНЬ — ту часть,
/// что стоит прямо за закрытой деталью. Всё, что торчит за пределы тени,
/// остаётся препятствием.</summary>
public class ContactShadowTests
{
    private const float TouchGap = 0.005f;

    private static Bounds Box(Vector3 center, Vector3 size) => new Bounds(center, size);

    private static List<Bounds> Pieces(Bounds closed, Bounds neighbour)
    {
        var pieces = new List<Bounds>();
        ContactShadow.ActivePieces(closed, neighbour, TouchGap, pieces);
        return pieces;
    }

    [Test]
    public void DistantNeighbour_StaysWholeObstacle()
    {
        var closed = Box(Vector3.zero, new Vector3(0.67f, 0.9f, 0.018f));
        var far = Box(new Vector3(0f, 0f, 0.3f), new Vector3(0.5f, 0.5f, 0.018f));

        var pieces = Pieces(closed, far);

        Assert.AreEqual(1, pieces.Count, "далёкий сосед не режется — он целиком препятствие");
        Assert.AreEqual(far.center, pieces[0].center,
            "и отдан он как есть: резать нечего, тени закрытой детали на нём нет");
    }

    [Test]
    public void NeighbourEntirelyBehindTheClosedBox_IsFullyMuted()
    {
        var closed = Box(Vector3.zero, new Vector3(0.4f, 0.086f, 0.018f));
        // Ровно та же ширина и высота, вплотную спереди: это контейнер.
        var carcass = Box(new Vector3(0f, 0f, 0.02f), new Vector3(0.4f, 0.086f, 0.018f));

        Assert.AreEqual(0, Pieces(closed, carcass).Count,
            "корпус, целиком лежащий в тени закрытой детали, не мешает ей выехать — "
            + "иначе ящик не вышел бы из собственного корпуса");
    }

    [Test]
    public void NeighbourSurroundingTheClosedBox_IsFullyMuted()
    {
        // Створка в проёме стены: рама обнимает её по всем трём осям.
        var sash = Box(Vector3.zero, new Vector3(0.86f, 0.96f, 0.04f));
        var wall = Box(new Vector3(0f, 0.35f, 0f), new Vector3(3f, 2.5f, 0.1f));

        Assert.AreEqual(0, Pieces(sash, wall).Count,
            "деталь стоит ВНУТРИ соседа — сосед ей рама целиком, а не четырьмя "
            + "кусками вокруг проёма. На петле по кромке тыльное ребро створки "
            + "уходит за линию петли на толщину полотна, и куски рамы ловили бы "
            + "это как столкновение на первых же градусах");
    }

    [Test]
    public void NeighbourCoveringOnlySomeAxes_IsNotTakenForAFrame()
    {
        // Боковина высокого модуля накрывает фасад по высоте и по глубине,
        // но не по толщине: фасад торчит из неё вперёд.
        var closed = Box(Vector3.zero, new Vector3(0.67f, 0.9f, 0.018f));
        var side = Box(new Vector3(0.347f, 0f, -0.09f), new Vector3(0.018f, 2.16f, 0.55f));

        Assert.IsFalse(ContactShadow.Surrounds(side, closed, TouchGap),
            "контроль к предыдущему: «обнимает» — это по ВСЕМ трём осям. Две из "
            + "трёх здесь совпадают, и приняв это за раму, мы вернули бы фасад "
            + "к проходу сквозь боковину");
    }

    [Test]
    public void NeighbourSpillingPastTheShadow_KeepsThatPartAsObstacle()
    {
        var closed = Box(Vector3.zero, new Vector3(0.67f, 0.9f, 0.018f));
        // Боковина стоит сбоку кромкой к двери (касание по X) и торчит на
        // 200 мм вперёд по Z — туда, куда дверь уедет на 90°.
        var side = Box(new Vector3(0.347f, 0f, -0.09f), new Vector3(0.018f, 2.16f, 0.55f));

        var pieces = Pieces(closed, side);

        Assert.Greater(pieces.Count, 0,
            "выступающая часть боковины осталась препятствием — фасад упирается в неё "
            + "на 90°, и раньше это терялось вместе со всем соседом");

        float shadowFront = closed.max.z;
        bool coversTheProtrusion = false;
        foreach (var piece in pieces)
            if (piece.max.z > shadowFront + Tolerance.EpsilonUnits) coversTheProtrusion = true;

        Assert.IsTrue(coversTheProtrusion,
            "среди активных кусков есть тот, что торчит перед закрытым фасадом");

        foreach (var piece in pieces)
            Assert.IsTrue(piece.min.x >= closed.max.x - Tolerance.EpsilonUnits,
                "куски остаются по свою сторону от оси касания — тень не размазывается");
    }

    /// <summary>Мойка B2 из сцены пользователя, в системе координат двери
    /// B2_door (541×720×18 с зазорами): она свисает на 6 мм ПРАВЕЕ двери — а
    /// справа петля — и уходит на 500 мм вглубь корпуса, до 3 мм перед
    /// плоскостью фасада. Ось касания тут Z (толщина двери), поэтому режется
    /// сосед по X и по Y, а по Z остаётся целым: боковой кусок шириной 6 мм
    /// сохраняет всю глубину мойки, в том числе слой ВНУТРИ собственной
    /// толщины двери.
    ///
    /// Именно туда уходит дверь на чашечной петле: ось утоплена внутрь
    /// полотна, и передняя кромка у петли описывает дугу радиусом 8,5 мм —
    /// на 2 мм ЗА свою закрытую кромку, оставаясь на своей же глубине. Дверь
    /// упиралась в этот кусок и вставала на 12°.</summary>
    [Test]
    public void NeighbourOverhangingTheHingeEdge_KeepsNothingInsideTheClosedThickness()
    {
        var closed = Box(Vector3.zero, new Vector3(0.541f, 0.720f, 0.018f));
        var sink = Box(new Vector3(0.0265f, 0.400f, -0.238f), new Vector3(0.5f, 0.188f, 0.5f));

        float deepestMm = 0f;
        foreach (var piece in Pieces(closed, sink))
        {
            float inside = Mathf.Min(piece.max.z, closed.max.z) - Mathf.Max(piece.min.z, closed.min.z);
            if (inside > deepestMm) deepestMm = inside;
        }

        Assert.AreEqual(0f, deepestMm / AppConstants.MM_TO_UNITS, 0.5f,
            "кромке у петли нужен зазор в собственном слое двери: активный кусок "
            + "не имеет права стоять между её передней и тыльной пластями");
    }

    /// <summary>Контроль к предыдущему: гасится слой, а не сосед. Та же мойка,
    /// но вылезшая на 100 мм ПЕРЕД фасадом, обязана остаться препятствием —
    /// иначе «дверь открывается» стало бы неотличимо от «ничего не
    /// проверяется».</summary>
    [Test]
    public void NeighbourOverhangingTheHingeEdge_KeepsWhatSticksOutInFront()
    {
        var closed = Box(Vector3.zero, new Vector3(0.541f, 0.720f, 0.018f));
        var sink = Box(new Vector3(0.0265f, 0.400f, -0.194f), new Vector3(0.5f, 0.188f, 0.5f));

        bool inFront = false;
        foreach (var piece in Pieces(closed, sink))
            if (piece.max.z > closed.max.z + Tolerance.EpsilonUnits) inFront = true;

        Assert.IsTrue(inFront,
            "часть соседа перед плоскостью фасада — препятствие: дверь уезжает именно туда");
    }

    [Test]
    public void ShadowPieces_NeverCoverTheClosedBoxItself()
    {
        var closed = Box(Vector3.zero, new Vector3(0.67f, 0.9f, 0.018f));
        var side = Box(new Vector3(0.347f, 0f, -0.09f), new Vector3(0.018f, 2.16f, 0.55f));

        foreach (var piece in Pieces(closed, side))
        {
            bool insideY = piece.min.y >= closed.min.y - Tolerance.EpsilonUnits
                           && piece.max.y <= closed.max.y + Tolerance.EpsilonUnits;
            bool insideZ = piece.min.z >= closed.min.z - Tolerance.EpsilonUnits
                           && piece.max.z <= closed.max.z + Tolerance.EpsilonUnits;

            Assert.IsFalse(insideY && insideZ,
                "кусок, целиком лежащий в тени по обеим поперечным осям, должен быть "
                + "погашен: это контакт закрытой детали с рамой, а не препятствие");
        }
    }
}
