using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Чистая механика перетаскивания, вынутая из ElementMover: когда
/// нажатие становится перетаскиванием, что делает Ctrl с прилипанием, как
/// работает блокировка оси, когда показывать призрак и каким цветом красить
/// деталь под курсором.
///
/// Здесь живут причины, которые раньше были комментариями в ElementMover.cs.
/// Раньше всё это жило внутри Update и проверялось только руками.</summary>
public class DragGestureTests
{
    [Test]
    public void Press_ThatBarelyTrembles_StaysAClick()
    {
        var press = new Vector2(100f, 100f);

        Assert.IsFalse(DragGesture.PressBecameDrag(press, 0f, press + new Vector2(3f, 0f), 1f),
            "3 пикселя — это дрожание руки на кнопке, а не перетаскивание: до порога "
            + "клик обязан остаться выделением, без зелёно-красной тонировки");
    }

    [Test]
    public void Press_ThatMovesFarButInstantly_IsStillAClick()
    {
        var press = new Vector2(100f, 100f);

        Assert.IsFalse(DragGesture.PressBecameDrag(press, 0f,
                press + new Vector2(100f, 0f), DragGesture.DragStartSeconds * 0.5f),
            "мышь дёрнулась в тот же миг, что и нажатие, — это тоже ложное срабатывание");
    }

    [Test]
    public void Press_ThatMovesFarAndWaits_BecomesADrag()
    {
        var press = new Vector2(100f, 100f);

        Assert.IsTrue(DragGesture.PressBecameDrag(press, 0f,
                press + new Vector2(DragGesture.DragStartPixels + 1f, 0f),
                DragGesture.DragStartSeconds + 0.01f),
            "положительный контроль: за порогом и по расстоянию, и по времени — это drag");
    }

    [Test]
    public void Ctrl_InvertsSnapping_InBothDirections()
    {
        Assert.IsFalse(DragGesture.SnapAppliesTo(globalSnapEnabled: true, ctrlHeld: true),
            "прилипание включено глобально — Ctrl выключает его на время перетаскивания");
        Assert.IsTrue(DragGesture.SnapAppliesTo(globalSnapEnabled: false, ctrlHeld: true),
            "прилипание выключено — Ctrl включает его разово, не трогая настройку");
        Assert.IsTrue(DragGesture.SnapAppliesTo(globalSnapEnabled: true, ctrlHeld: false));
        Assert.IsFalse(DragGesture.SnapAppliesTo(globalSnapEnabled: false, ctrlHeld: false));
    }

    [Test]
    public void AxisLock_PressedTwice_TurnsItself_Off()
    {
        var afterFirst = DragGesture.Toggle(DragAxisLock.None, DragAxisLock.X);
        Assert.AreEqual(DragAxisLock.X, afterFirst);
        Assert.AreEqual(DragAxisLock.None, DragGesture.Toggle(afterFirst, DragAxisLock.X),
            "та же клавиша снимает блокировку — иначе из неё нельзя выйти");
        Assert.AreEqual(DragAxisLock.Z, DragGesture.Toggle(afterFirst, DragAxisLock.Z),
            "другая ось просто заменяет текущую");
    }

    [Test]
    public void AxisLockX_KeepsTheStartZ_AndTheHeldHeight()
    {
        var start = new Vector3(1f, 2f, 3f);
        var wanted = new Vector3(5f, 9f, 9f);

        var locked = DragGesture.ApplyAxisLock(wanted, DragAxisLock.X, start,
            heldY: 2f, keepsItsOwnHeight: true);

        Assert.AreEqual(5f, locked.x, 1e-6f, "по своей оси деталь едет свободно");
        Assert.AreEqual(3f, locked.z, 1e-6f, "поперёк — стоит там, где взяли");
        Assert.AreEqual(2f, locked.y, 1e-6f,
            "горизонтальное перетаскивание держит высоту старта: округляй Y сеткой "
            + "каждый кадр — и контакт с полом рвался бы на каждом шаге в 18 мм");
    }

    [Test]
    public void AxisLock_OnAWallOpening_LeavesTheHeightToTheMouse()
    {
        var start = new Vector3(1f, 2f, 3f);
        var wanted = new Vector3(5f, 9f, 9f);

        var locked = DragGesture.ApplyAxisLock(wanted, DragAxisLock.Z, start,
            heldY: 2f, keepsItsOwnHeight: false);

        Assert.AreEqual(1f, locked.x, 1e-6f);
        Assert.AreEqual(9f, locked.y, 1e-6f,
            "окно и дверь ездят по стене вверх-вниз — фиксировать им высоту нельзя");
    }

    [Test]
    public void AxisLockNone_ChangesNothing()
    {
        var wanted = new Vector3(5f, 9f, 9f);

        Assert.AreEqual(wanted, DragGesture.ApplyAxisLock(wanted, DragAxisLock.None,
            Vector3.zero, heldY: 0f, keepsItsOwnHeight: true));
    }

    [Test]
    public void Ghost_ShowsOnlyWhereTheSnapActuallyMovedThePart()
    {
        var free = new Vector3(1f, 0f, 0f);

        Assert.IsFalse(DragGesture.GhostIsWorthShowing(false, free, free),
            "без прилипания показывать нечего");
        Assert.IsFalse(DragGesture.GhostIsWorthShowing(true, free, free),
            "прилипло ровно туда, где была мышь — призрак совпал бы с деталью");
        Assert.IsTrue(DragGesture.GhostIsWorthShowing(true, new Vector3(1.05f, 0f, 0f), free),
            "деталь стоит в позиции снэпа, призрак — под курсором: видно, что и куда "
            + "притянуло. Сравнение с УЖЕ выставленной позицией детали всегда давало "
            + "ноль, и призрак не появлялся никогда");
    }

    [Test]
    public void Tint_IsGreenWhenAllowed_AndRedWhenTheMoveWouldBeRolledBack()
    {
        Assert.AreEqual(DragGesture.AllowedTint, DragGesture.TintFor(false));
        Assert.AreEqual(DragGesture.BlockedTint, DragGesture.TintFor(true),
            "красный обещает ровно то, что произойдёт при отпускании: при включённой "
            + "блокировке деталь откатится на старт");
        Assert.AreNotEqual(DragGesture.AllowedTint, DragGesture.BlockedTint);
    }
}
