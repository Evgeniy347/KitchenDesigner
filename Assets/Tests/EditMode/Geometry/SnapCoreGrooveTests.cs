using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Прилипание ПЕРЕМЕЩЕНИЕМ к пазу: посадка вкладной панели на дно и
/// разметочные детенты по стенкам паза.
///
/// Парные тесты к <see cref="ResizeSnapGrooveTests"/> — «Change one, check the
/// other»: перемещение и ресайз обязаны видеть паз одинаково. До сих пор эти
/// ветки `SnapCore` проверялись только сценовыми наборами
/// (`GroovePanelBoxTests`, `GrooveEdgeSnapTests`, `GrooveSceneReproTests`), и
/// мутационный прогон их не видел.</summary>
public class SnapCoreGrooveTests : SnapCoreTestBase
{
    /// <summary>Доска 800×400×18 в начале координат с пазом вдоль X: глубина
    /// 8 мм, стенки на y = −50 и −34. Пласть +Z на z = 9, дно паза — на z = 1.</summary>
    private static ElementGeometry Board() =>
        GroovedGeometry.Board("Board", Vector3.zero, new Vector3(800, 400, 18),
            depthMm: 8f, fromMm: -50f, toMm: -34f);

    private static ElementGeometry PlainBoard() =>
        ElementGeometry.Box("Board", Vector3.zero, new Vector3(800, 400, 18) * MM);

    /// <summary>Вкладная ДВП 600×300×4.</summary>
    private static Box PanelBox() =>
        new Box("Panel", new Vector3Int(600, 300, 4), null, isPanel: true);

    /// <summary>Такая же по габаритам, но НЕ вкладная.</summary>
    private static Box ThickBox() =>
        new Box("Thick", new Vector3Int(600, 300, 4));

    [Test]
    public void Panel_SeatsIntoGrooveFloor_EvenThoughFaceIsNearer()
    {
        // Панель подходит снаружи: её низ на z = 6 — до пласти (9) 3 мм, до дна
        // паза (1) 5 мм. Над пазом материала НЕТ, поэтому пласть перестаёт быть
        // поверхностью контакта и панель обязана сесть на дно.
        var r = Snap(PanelBox(), Board(), new Vector3(0, -50f * MM, 8f * MM));

        Assert.IsTrue(r.snapped, "панель обязана поймать дно паза");
        Assert.AreEqual(3f * MM, r.position.z, Tol, "низ панели встал на дно паза (z = 1)");
        // Заодно панель центруется ПО ПАЗУ (его середина — y = −42), а не по
        // детали: выравнивание в плоскости идёт по той грани, которая дала
        // контакт, а контакт здесь дало дно паза.
        Assert.AreEqual(-42f * MM, r.position.y, Tol);
    }

    [Test]
    public void ThickPart_LandsOnFace_NotInGroove()
    {
        // Та же поза у НЕвкладной детали: дно паза ей не предлагается, она
        // остаётся на пласти.
        var r = Snap(ThickBox(), Board(), new Vector3(0, -50f * MM, 8f * MM));

        Assert.IsTrue(r.snapped);
        Assert.AreEqual(11f * MM, r.position.z, Tol, "низ детали лёг на пласть (z = 9)");
    }

    [Test]
    public void Panel_AsideOfGroove_LandsOnFace()
    {
        // Панель напротив пласти, но мимо контура паза: пласть там не «съедена»,
        // посадки нет.
        var r = Snap(PanelBox(), Board(), new Vector3(0, 150f * MM, 8f * MM));

        Assert.IsTrue(r.snapped);
        Assert.AreEqual(11f * MM, r.position.z, Tol, "низ панели лёг на пласть (z = 9)");
    }

    [Test]
    public void GrooveWall_GivesEdgeDetent()
    {
        // Полка прислонена к пласти (низ на z = 11, пласть 9) и стоит кромкой
        // на y = −60. Стенка паза на −50 — ближайшая разметочная линия, полка
        // обязана подровняться по ней (+10 мм).
        var shelf = Make("Shelf", new Vector3Int(400, 200, 18));
        var r = Snap(shelf, Board(), new Vector3(0, 40f * MM, 20f * MM));

        Assert.IsTrue(r.snapped);
        Assert.AreEqual(18f * MM, r.position.z, Tol, "низ полки на пласти");
        Assert.AreEqual(50f * MM, r.position.y, Tol, "кромка полки выровнена по стенке паза (−50)");
    }

    [Test]
    public void WithoutGrooves_NoEdgeDetent()
    {
        // Контроль: у соседа пазов нет — детента «по стенке паза» не существует,
        // и в плоскости остаются только обычные кандидаты. Ближайший из них —
        // центр доски (0), а не −50.
        var shelf = Make("Shelf", new Vector3Int(400, 200, 18));
        var r = Snap(shelf, PlainBoard(), new Vector3(0, 40f * MM, 20f * MM));

        Assert.IsTrue(r.snapped);
        Assert.AreEqual(18f * MM, r.position.z, Tol);
        Assert.AreEqual(0f, r.position.y, Tol, "без паза кромке не по чему равняться");
    }

    // ── SeatSupersedesFace ──────────────────────────────────────────────

    [Test]
    public void SeatSupersedesFace_OverGroove_IsTrue()
    {
        var board = Board();
        var panel = ElementGeometry.Box("Panel", new Vector3(0, -42f, 8f) * MM,
            new Vector3(600, 300, 4) * MM, isPanel: true);

        Assert.IsTrue(GrooveSeating.SeatSupersedesFace(panel.Faces[5], board.Faces[4],
            board.GrooveSeatFaces), "над пазом материала нет — контактом служит дно");
    }

    [Test]
    public void SeatSupersedesFace_AsideOfGroove_IsFalse()
    {
        var board = Board();
        var panel = ElementGeometry.Box("Panel", new Vector3(0, 400f, 8f) * MM,
            new Vector3(600, 300, 4) * MM, isPanel: true);

        Assert.IsFalse(GrooveSeating.SeatSupersedesFace(panel.Faces[5], board.Faces[4],
            board.GrooveSeatFaces));
    }

    [Test]
    public void SeatSupersedesFace_OppositeFace_IsFalse()
    {
        // Тыльная пласть (−Z) паза не несёт: её нормаль дну паза не со-направлена.
        var board = Board();
        var panel = ElementGeometry.Box("Panel", new Vector3(0, -42f, 8f) * MM,
            new Vector3(600, 300, 4) * MM, isPanel: true);

        Assert.IsFalse(GrooveSeating.SeatSupersedesFace(panel.Faces[5], board.Faces[5],
            board.GrooveSeatFaces));
    }

    [Test]
    public void SeatSupersedesFace_NoGrooves_IsFalse()
    {
        var board = PlainBoard();
        var panel = ElementGeometry.Box("Panel", new Vector3(0, -42f, 8f) * MM,
            new Vector3(600, 300, 4) * MM, isPanel: true);

        Assert.IsFalse(GrooveSeating.SeatSupersedesFace(panel.Faces[5], board.Faces[4],
            board.GrooveSeatFaces));
    }
}
