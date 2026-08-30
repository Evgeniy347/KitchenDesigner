using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>«Кандидат не имеет права загонять центр детали ВНУТРЬ соседа» —
/// проверка меряется по ТЕЛУ соседа, а не по его габариту.
///
/// Габарит (AABB) повёрнутой детали заведомо шире тела, поэтому у пары панелей
/// под 45° точка, стоящая снаружи, попадала внутрь габарита — и стык
/// пласть-к-пласти терялся целиком. Это было зафиксировано как ограничение в
/// `SnapKnownLimitationTests`; теперь проверка считает знак по шести граням
/// соседа, то есть по самой коробке, и ограничения больше нет.
///
/// Второй набор — про округление: исторически «сработавший» снэп при НЕЧЁТНОМ
/// размере соседа заводил деталь на ≤0.5 мм сквозь его плоскость (ресайз
/// округлял размер до целых мм). Ресайзная половина закреплена в
/// `ResizeMathTests.Resize_SnapToOffMmNeighbour_NeverOvershootsIntoIt`; здесь
/// закрепляется, что перемещение и ресайз видят одну и ту же плоскость на
/// полумиллиметровой координате.</summary>
public class SnapRotatedCentreReproTests : SnapCoreTestBase
{
    private static readonly Quaternion Rot45 = RotY(45f);

    private const float PanelHalfThickness = 9f * MM;

    [Test]
    public void RotatedPanels_FaceToFace_Snap_CentreIsMeasuredAgainstTheBody()
    {
        var a = At(MakeStd("A", Rot45), Rot45 * new Vector3(0f, 0f, 38f * MM));
        var b = MakeStd("B", Rot45);

        var r = Snap(b, a, Vector3.zero);

        Assert.IsTrue(r.snapped,
            "поворот на 45° раздувает габарит соседа, но не его тело: стык пласть-к-пласти обязан ловиться");
        Vector3 expected = Rot45 * new Vector3(0f, 0f, 20f * MM);
        Assert.AreEqual(expected.x, r.position.x, Tol, "X встал на стык 18 мм вдоль повёрнутой оси");
        Assert.AreEqual(expected.z, r.position.z, Tol, "Z встал на стык 18 мм вдоль повёрнутой оси");
    }

    [Test]
    public void UnrotatedPanels_FaceToFace_Snap()
    {
        var a = At(MakeStd("A"), new Vector3(0f, 0f, 38f * MM));
        var b = MakeStd("B");

        var r = Snap(b, a, Vector3.zero);

        Assert.IsTrue(r.snapped,
            "положительный контроль на ТОЙ ЖЕ геометрии без поворота: пара вообще умеет такой стык");
        Assert.AreEqual(20f * MM, r.position.z, Tol, "18 мм толщины — стык пластей");
    }

    private const int OddWidthMm = 601;
    private const float OddHalfX = 300.5f * MM;
    private const float MovedHalfX = 400f * MM;
    private const float Approach = 9.5f * MM;

    private static ElementGeometry OddNeighbour() =>
        At(Make("Odd", new Vector3Int(OddWidthMm, 401, 19)), Vector3.zero);

    [Test]
    public void OddSizedNeighbour_Drag_SnapsToTheHalfMillimetrePlane_WithoutOverlap()
    {
        var moved = MakeStd("Moved");
        float flushX = OddHalfX + MovedHalfX;

        var r = Snap(moved, OddNeighbour(), new Vector3(flushX + Approach, 0f, 0f));

        Assert.IsTrue(r.snapped, "нечётный размер соседа не мешает прилипанию");
        Assert.AreEqual(flushX, r.position.x, Tolerance.EpsilonUnits,
            "грань встала ровно на плоскость соседа, лежащую на полумиллиметре: допуск здесь 0.1 мм, иначе тест не увидит артефакт в 0.5 мм");
        Assert.IsFalse(Intersect(moved.At(r.position), OddNeighbour()),
            "после снэпа деталь не заходит СКВОЗЬ плоскость соседа — тот самый артефакт с 0.5 мм");
    }

    [Test]
    public void OddSizedNeighbour_Resize_SeesTheSameHalfMillimetrePlaneAsDrag()
    {
        float startX = OddHalfX + MovedHalfX + Approach;
        var moved = At(MakeStd("Moved"), new Vector3(startX, 0f, 0f));
        Face minusX = moved.Faces[1];

        bool snapped = ResizeSnap.SnapDelta(minusX.center, minusX.normal, minusX.rightAxis,
            minusX.upAxis, minusX.size, new List<ElementGeometry> { OddNeighbour() }, moved,
            Threshold, out float gap);

        Assert.IsTrue(snapped, "растягивание видит ту же плоскость, что и перетаскивание");
        Assert.AreEqual(Approach, Mathf.Abs(gap), Tolerance.EpsilonUnits,
            "зазор до плоскости соседа тот же, что закрывает перетаскивание — иначе «тянется, но не перетаскивается»");
    }
}
