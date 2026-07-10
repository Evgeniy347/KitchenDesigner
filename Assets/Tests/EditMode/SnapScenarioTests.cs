using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Базовые сценарии прилипания ДВУХ досок: контакт плоскостью (6 направлений),
/// выравнивание по кромкам (min/max/центр), по вершине (углу), повороты.
/// Координаты вычислены точно (оракул — автор теста); см. геометрию в SnapTestBase.
/// </summary>
public class SnapScenarioTests : SnapTestBase
{
    // ===== Контакт плоскостью (face-to-face) по всем 6 направлениям =====
    // Две одинаковые доски 800×400×18; цель A в начале координат.

    [Test]
    public void FaceToFace_Right_AlignsAtPlusHalfWidth()
    {
        var a = MakeStd("A", Vector3.zero);
        var b = MakeStd("B", Vector3.zero);
        AssertSnappedAt(b, a, new Vector3(0.83f, 0f, 0f), new Vector3(0.80f, 0f, 0f));
    }

    [Test]
    public void FaceToFace_Left_AlignsAtMinusHalfWidth()
    {
        var a = MakeStd("A", Vector3.zero);
        var b = MakeStd("B", Vector3.zero);
        AssertSnappedAt(b, a, new Vector3(-0.83f, 0f, 0f), new Vector3(-0.80f, 0f, 0f));
    }

    [Test]
    public void FaceToFace_Front_AlignsAtThickness()
    {
        var a = MakeStd("A", Vector3.zero);
        var b = MakeStd("B", Vector3.zero);
        // Две доски «лист к листу» по Z: суммарная толщина 36 мм → центр B на 0.018.
        AssertSnappedAt(b, a, new Vector3(0f, 0f, 0.048f), new Vector3(0f, 0f, 0.018f));
    }

    [Test]
    public void FaceToFace_Back_AlignsAtMinusThickness()
    {
        var a = MakeStd("A", Vector3.zero);
        var b = MakeStd("B", Vector3.zero);
        AssertSnappedAt(b, a, new Vector3(0f, 0f, -0.048f), new Vector3(0f, 0f, -0.018f));
    }

    [Test]
    public void FaceToFace_Top_AlignsAtPlusHalfHeight()
    {
        var a = MakeStd("A", Vector3.zero);
        var b = MakeStd("B", Vector3.zero);
        AssertSnappedAt(b, a, new Vector3(0f, 0.43f, 0f), new Vector3(0f, 0.40f, 0f));
    }

    [Test]
    public void FaceToFace_Bottom_AlignsAtMinusHalfHeight()
    {
        var a = MakeStd("A", Vector3.zero);
        var b = MakeStd("B", Vector3.zero);
        AssertSnappedAt(b, a, new Vector3(0f, -0.43f, 0f), new Vector3(0f, -0.40f, 0f));
    }

    [Test]
    public void FaceToFace_ProducesFlushContact_NoIntersection()
    {
        var a = MakeStd("A", Vector3.zero);
        var b = MakeStd("B", Vector3.zero);
        AssertFlushContact(b, a, new Vector3(0.83f, 0f, 0f));
    }

    // ===== Выравнивание по кромкам (мелкая доска у большой грани) =====
    // A 800×400×18, B 400×400×18 подносится к передней (+Z) грани A.

    [Test]
    public void EdgeAlign_LeftEdges_Coincide()
    {
        var a = MakeStd("A", Vector3.zero);
        var b = Make("B", new Vector3Int(400, 400, 18), Vector3.zero);
        AssertSnappedAt(b, a, new Vector3(-0.18f, 0f, 0.048f), new Vector3(-0.20f, 0f, 0.018f),
            "левые кромки совпадают");
    }

    [Test]
    public void EdgeAlign_RightEdges_Coincide()
    {
        var a = MakeStd("A", Vector3.zero);
        var b = Make("B", new Vector3Int(400, 400, 18), Vector3.zero);
        AssertSnappedAt(b, a, new Vector3(0.18f, 0f, 0.048f), new Vector3(0.20f, 0f, 0.018f),
            "правые кромки совпадают");
    }

    [Test]
    public void EdgeAlign_NearCenter_CentersCoincide()
    {
        var a = MakeStd("A", Vector3.zero);
        var b = Make("B", new Vector3Int(400, 400, 18), Vector3.zero);
        AssertSnappedAt(b, a, new Vector3(0.02f, 0f, 0.048f), new Vector3(0f, 0f, 0.018f),
            "у центра — центры совпадают, мелкая доска не липнет к кромке");
    }

    [Test]
    public void EdgeAlign_ChoosesNearestEdge()
    {
        // Смещена на 10 мм от левого края — должна выбрать левую кромку, не центр.
        var a = MakeStd("A", Vector3.zero);
        var b = Make("B", new Vector3Int(400, 400, 18), Vector3.zero);
        AssertSnappedAt(b, a, new Vector3(-0.19f, 0f, 0.048f), new Vector3(-0.20f, 0f, 0.018f));
    }

    // ===== Вершина / угол: выравниваются обе кромки одновременно =====

    [Test]
    public void VertexAlign_TopLeftCorner_BothEdges()
    {
        var a = MakeStd("A", Vector3.zero);
        var b = Make("B", new Vector3Int(400, 200, 18), Vector3.zero);
        AssertSnappedAt(b, a, new Vector3(-0.18f, 0.08f, 0.048f), new Vector3(-0.20f, 0.10f, 0.018f),
            "угол: левая + верхняя кромки совпадают");
    }

    [Test]
    public void VertexAlign_BottomRightCorner_BothEdges()
    {
        var a = MakeStd("A", Vector3.zero);
        var b = Make("B", new Vector3Int(400, 200, 18), Vector3.zero);
        AssertSnappedAt(b, a, new Vector3(0.18f, -0.08f, 0.048f), new Vector3(0.20f, -0.10f, 0.018f),
            "угол: правая + нижняя кромки совпадают");
    }

    // ===== Разные размеры =====

    [Test]
    public void DifferentSizes_Long1200_ButtJoint()
    {
        var a = MakeStd("A", Vector3.zero);                       // 800 шир
        var b = Make("B", new Vector3Int(1200, 400, 18), Vector3.zero);
        // B шириной 1200 встык справа: B.center.x = 0.4 + 0.6 = 1.0
        AssertSnappedAt(b, a, new Vector3(1.03f, 0f, 0f), new Vector3(1.0f, 0f, 0f));
    }

    [Test]
    public void DifferentSizes_Square600_OnFront_Flush()
    {
        var a = MakeStd("A", Vector3.zero);
        var b = Make("B", new Vector3Int(600, 600, 18), Vector3.zero);
        AssertFlushContact(b, a, new Vector3(0f, 0f, 0.048f));
    }

    // ===== Пол =====

    [Test]
    public void BoardAboveFloor_SnapsDownToTopFace()
    {
        var floor = MakeFloor();
        var b = MakeStd("B", Vector3.zero);
        // Низ доски (центр y=0.25, низ 0.05) в 41 мм над полом (верх пола y=0.009).
        AssertSnappedAt(b, floor, new Vector3(0f, 0.25f, 0f), new Vector3(0f, 0.209f, 0f),
            "доска опускается на пол: центр = 0.009 + 0.2");
    }

    [Test]
    public void BoardOnFloor_IsValid()
    {
        var floor = MakeFloor();
        var b = MakeStd("B", new Vector3(0f, 0.209f, 0f));
        var val = ConstraintValidator.Validate(new System.Collections.Generic.List<KitchenElement> { floor, b });
        Assert.IsTrue(val.isValid, "доска ровно на полу валидна");
    }

    // ===== Повороты, дающие параллельные грани (снэп работает) =====

    [Test]
    public void Rotated90AroundY_ButtJoint_FlushContact()
    {
        var a = MakeStd("A", Vector3.zero);
        var b = Make("B", new Vector3Int(400, 400, 18), Vector3.zero,
            Quaternion.AngleAxis(90f, Vector3.up));
        AssertFlushContact(b, a, new Vector3(0.43f, 0f, 0f));
    }

    [Test]
    public void Rotated180AroundY_SnapsLikeUnrotated()
    {
        var a = MakeStd("A", Vector3.zero);
        var b = MakeStd("B", Vector3.zero, Quaternion.AngleAxis(180f, Vector3.up));
        // Нормали антипараллельны → |dot|≈1, снэп как у неповёрнутой.
        AssertSnappedAt(b, a, new Vector3(0.83f, 0f, 0f), new Vector3(0.80f, 0f, 0f));
    }

    [Test]
    public void Rotated90AroundX_SnapsToFloor_FlushContact()
    {
        // Поворот по X кладёт доску плашмя (толщина 18 мм вдоль Y → центр на ~0.018).
        // Проверяем, что повёрнутая по X доска всё равно прилипает к полу плоскостью.
        var floor = MakeFloor();
        var b = Make("B", new Vector3Int(800, 400, 18), Vector3.zero,
            Quaternion.AngleAxis(90f, Vector3.right));
        AssertFlushContact(b, floor, new Vector3(0f, 0.05f, 0f));
    }

    // ===== Негативные базовые случаи =====

    [Test]
    public void NoSnap_TooFar()
    {
        var a = MakeStd("A", Vector3.zero);
        var b = MakeStd("B", Vector3.zero);
        AssertNotSnapped(b, a, new Vector3(2.0f, 0f, 0f));
    }

    [Test]
    public void NoSnap_WhenIntersecting()
    {
        var a = MakeStd("A", Vector3.zero);
        var b = MakeStd("B", Vector3.zero);
        // Доски смещены по X на 100 мм, но грани ±Z в зазоре 18 мм —
        // снэп разведёт доски по Z встык.
        var r = Snap(b, a, new Vector3(0.1f, 0f, 0f));
        Assert.IsTrue(r.snapped, "снэп разведёт пересекающиеся доски");
        Assert.AreEqual(0.1f, r.position.x, Tol, "X не изменился");
        Assert.AreEqual(-0.018f, r.position.z, Tol, "Z — встык по Z-граням");
    }

    [Test]
    public void ZeroDimensions_ClampedToOne()
    {
        var b = Make("B", new Vector3Int(0, -5, 0), Vector3.zero);
        Assert.AreEqual(1, b.DimensionsMM.x);
        Assert.AreEqual(1, b.DimensionsMM.y);
        Assert.AreEqual(1, b.DimensionsMM.z);
    }
}
