using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Сценарии прилипания ТРЁХ и более деталей: цепочки, стопки, стены на полу,
/// выбор ближайшей цели, конфликты. Каждая деталь ставится через реальный снэп,
/// затем проверяется итоговая связная конструкция.
/// </summary>
public class SnapMultiBoardTests : SnapTestBase
{
    private void Place(KitchenElement e, SnapResult r)
    {
        Assert.IsTrue(r.snapped, "деталь должна была прилипнуть");
        e.transform.position = r.position;
    }

    // ===== Цепочка A→B→C вдоль X =====

    [Test]
    public void Chain_ABC_Linear_PositionsCorrect()
    {
        var a = Make("A", new Vector3Int(600, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(600, 400, 18), Vector3.zero);
        var c = Make("C", new Vector3Int(600, 400, 18), Vector3.zero);

        Place(b, Snap(b, a, new Vector3(0.62f, 0f, 0f)));
        Assert.AreEqual(0.6f, b.transform.position.x, Tol, "B встык справа от A");

        var rc = Snap(c, new List<KitchenElement> { a, b }, new Vector3(1.22f, 0f, 0f));
        Assert.IsTrue(rc.snapped);
        Assert.AreEqual("B", rc.targetName, "C липнет к ближайшей B");
        Assert.AreEqual(1.2f, rc.position.x, Tol);
    }

    [Test]
    public void Chain_ABC_AllConnected_Valid()
    {
        var a = Make("A", new Vector3Int(600, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(600, 400, 18), new Vector3(0.6f, 0f, 0f));
        var c = Make("C", new Vector3Int(600, 400, 18), new Vector3(1.2f, 0f, 0f));

        var val = ConstraintValidator.Validate(new List<KitchenElement> { a, b, c });
        Assert.IsTrue(val.isValid, "цепочка A-B-C — единый связный граф");
    }

    // ===== Стопка на полу =====

    [Test]
    public void Stack_ABC_OnFloor_PositionsCorrect()
    {
        var floor = MakeFloor();
        var a = Make("A", new Vector3Int(600, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(600, 400, 18), Vector3.zero);
        var c = Make("C", new Vector3Int(600, 400, 18), Vector3.zero);

        Place(a, Snap(a, floor, new Vector3(0f, 0.25f, 0f)));
        Assert.AreEqual(0.209f, a.transform.position.y, Tol, "A на полу");

        Place(b, Snap(b, a, new Vector3(0f, 0.65f, 0f)));
        Assert.AreEqual(0.609f, b.transform.position.y, Tol, "B на A");

        Place(c, Snap(c, b, new Vector3(0f, 1.05f, 0f)));
        Assert.AreEqual(1.009f, c.transform.position.y, Tol, "C на B");
    }

    [Test]
    public void Stack_OnFloor_AllValid()
    {
        var floor = MakeFloor();
        var a = Make("A", new Vector3Int(600, 400, 18), new Vector3(0f, 0.209f, 0f));
        var b = Make("B", new Vector3Int(600, 400, 18), new Vector3(0f, 0.609f, 0f));
        var val = ConstraintValidator.Validate(new List<KitchenElement> { floor, a, b });
        Assert.IsTrue(val.isValid, "пол + 2 детали стопкой — валидно");
    }

    // ===== Две стены на полу (каждая прилипает к полу) =====

    [Test]
    public void TwoWallsOnFloor_BothSnapAndValid()
    {
        var floor = MakeFloor();
        var left = Make("Left", new Vector3Int(400, 400, 18), Vector3.zero);
        var right = Make("Right", new Vector3Int(400, 400, 18), Vector3.zero);

        Place(left, Snap(left, floor, new Vector3(-0.3f, 0.25f, 0f)));
        Place(right, Snap(right, floor, new Vector3(0.3f, 0.25f, 0f)));

        Assert.AreEqual(0.209f, left.transform.position.y, Tol);
        Assert.AreEqual(0.209f, right.transform.position.y, Tol);

        var val = ConstraintValidator.Validate(new List<KitchenElement> { floor, left, right });
        Assert.IsTrue(val.isValid, "обе стены связаны через пол");
    }

    // ===== T-образное соединение (вертикаль на горизонтали) =====

    [Test]
    public void TShape_VerticalOnHorizontal_FlushContact()
    {
        // Горизонтальная деталь лежит плашмя (поворот по X), сверху встаёт вертикальная.
        var horiz = Make("H", new Vector3Int(800, 400, 18), Vector3.zero,
            Quaternion.AngleAxis(90f, Vector3.right)); // 18 мм толщина по Y, верх на y=0.009
        // верхняя грань горизонтали на y = 0.009; вертикаль 400 высотой встаёт сверху.
        var vert = Make("V", new Vector3Int(400, 400, 18), Vector3.zero);
        AssertFlushContact(vert, horiz, new Vector3(0f, 0.22f, 0f));
    }

    // ===== Выбор ближайшей цели =====

    [Test]
    public void NearestTarget_SnapsToCloser()
    {
        var a = MakeStd("A", Vector3.zero);
        var b = MakeStd("B", new Vector3(0.8f, 0f, 0f));
        var c = MakeStd("C", Vector3.zero);

        var r = Snap(c, new List<KitchenElement> { a, b }, new Vector3(1.62f, 0f, 0f));
        Assert.IsTrue(r.snapped);
        Assert.AreEqual("B", r.targetName, "C ближе к B");
        Assert.AreEqual(1.6f, r.position.x, Tol);
    }

    [Test]
    public void Conflict_Deterministic_SameResultTwice()
    {
        var a = MakeStd("A", Vector3.zero);
        var b = MakeStd("B", new Vector3(0.8f, 0f, 0f));
        var c = MakeStd("C", Vector3.zero);

        var r1 = Snap(c, new List<KitchenElement> { a, b }, new Vector3(1.62f, 0f, 0.01f));
        var r2 = Snap(c, new List<KitchenElement> { a, b }, new Vector3(1.62f, 0f, 0.01f));
        Assert.AreEqual(r1.snapped, r2.snapped);
        Assert.AreEqual(r1.targetName, r2.targetName, "детерминированный выбор цели");
        Assert.AreEqual(r1.position, r2.position, "детерминированная позиция");
    }
}
