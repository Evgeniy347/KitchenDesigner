using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Кромочный контакт против полноплощадного — на снимках.
///
/// Полка 260×561×18 (поворот вокруг X), поднимаемая вдоль тонкой боковины
/// 18×900×332 (поворот вокруг Y), проходит ДВА детента:
///   • Y ≈ 1.351 — ГРАНЬ: верх полки заподлицо с низом боковины (контакт по
///     кромке, line contact);
///   • Y ≈ 1.369 — БОК: задняя грань полки полностью прилегает к боковине
///     (полная площадь).
///
/// Баг, ради которого набор и заведён: на 1.369 боковой контакт уже стоял
/// заподлицо («нулевой» сдвиг), а кромочный детент на 1.351 считался
/// содержательным и перебивал его — полку кидало вниз. Правило: полноплощадная
/// опора важнее слабого кромочного притяжения.
///
/// Геометрия — из реальной сцены (2026-07-19); сценовый двойник
/// `SnapThinSidePanelLineContactTests` остаётся в Unity как проверка адаптера.</summary>
public class SnapCoreLineContactTests : SnapCoreTestBase
{
    private const float EdgeY = 1.351f;        // детент ГРАНЬ
    private const float FullSurfaceY = 1.369f; // детент БОК

    /// <summary>Боковина: world 332×900×18, низ Y = 1.360, перед Z = −1.844.</summary>
    private static ElementGeometry SidePanel() =>
        At(Make("A12_upper_B_side_L", new Vector3Int(18, 900, 332), RotY(90f)),
            new Vector3(1.419f, 1.81f, -1.853f));

    /// <summary>Стена справа: полка уже стоит заподлицо с ней по X.</summary>
    private static ElementGeometry Wall() =>
        At(Make("A", new Vector3Int(100, 2700, 7240)), new Vector3(1.635f, 1.35f, 0f));

    /// <summary>Полка: world 260×18×561, задняя грань Z = −1.844.</summary>
    private static Box Shelf() =>
        Make("A34K1_upper_bottom_copy", new Vector3Int(260, 561, 18), RotX(90f));

    private static List<ElementGeometry> Scene() =>
        new List<ElementGeometry> { SidePanel(), Wall() };

    [Test]
    public void At1369_KeepsFullSurfaceContact_DoesNotFallBackTo1351()
    {
        var testPos = new Vector3(1.455f, FullSurfaceY, -1.5635f);
        var r = Snap(Shelf(), Scene(), testPos);

        Assert.IsTrue(r.snapped, "на 1.369 полка должна прилипнуть боком к боковине");
        Assert.AreEqual(FullSurfaceY, r.position.y, Tol,
            "полка остаётся на 1.369 (полноповерхностный боковой контакт)");
        Assert.Greater(r.position.y, 1.360f,
            "полку НЕ должно откидывать вниз на кромочный контакт 1.351");
        Assert.AreEqual(testPos.x, r.position.x, Tol, "X не меняется");
        Assert.AreEqual(testPos.z, r.position.z, Tol, "Z не меняется");
    }

    [Test]
    public void Near1369_IsPulledUpToFullSurface()
    {
        var r = Snap(Shelf(), Scene(), new Vector3(1.455f, 1.365f, -1.5635f));

        Assert.IsTrue(r.snapped);
        Assert.AreEqual(FullSurfaceY, r.position.y, Tol, "подтягивается вверх к 1.369");
    }

    [Test]
    public void At1369_ResultDoesNotIntersectSidePanel()
    {
        var shelf = Shelf();
        var r = Snap(shelf, Scene(), new Vector3(1.455f, FullSurfaceY, -1.5635f));

        Assert.IsTrue(r.snapped);
        Assert.IsFalse(Intersect(shelf.At(r.position), SidePanel()),
            "после снэпа полка не должна пересекать боковину");
    }

    [Test]
    public void At1351_KeepsEdgeDetent_DoesNotJumpTo1369()
    {
        var r = Snap(Shelf(), Scene(), new Vector3(1.455f, EdgeY, -1.5635f));

        Assert.IsTrue(r.snapped, "на 1.351 полка должна прилипать (кромка)");
        Assert.Less(r.position.y, 1.360f,
            "на кромочном детенте полку НЕ должно подтягивать к боковому контакту");
        Assert.AreEqual(EdgeY, r.position.y, 0.002f, "держится у 1.351");
    }
}
