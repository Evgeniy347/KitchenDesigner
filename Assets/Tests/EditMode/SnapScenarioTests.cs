using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Обширное покрытие прилипания: кромки / вершины / плоскости, 2 и 3 доски,
/// повёрнутые доски, пол. Точные кейсы проверяют координаты; общий «оракул»
/// AssertSnappedFlush проверяет, что после снэпа доски касаются плоскостью
/// (face-to-face контакт) и не пересекаются — работает для любой геометрии.
/// </summary>
public class SnapScenarioTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private bool _prevVerbose;

    [SetUp]
    public void Setup()
    {
        KitchenSettings.Instance.GridStep = 1;
        KitchenSettings.Instance.GridEnabled = true;
        KitchenSettings.Instance.SnapEnabled = true;
        KitchenSettings.Instance.SnapThreshold = 50f;
        _prevVerbose = SnapSystem.VerboseLog;
        SnapSystem.VerboseLog = false; // не засорять вывод тестов
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        SnapSystem.VerboseLog = _prevVerbose;
    }

    private KitchenElement Make(string name, Vector3Int dims, Vector3 pos, Quaternion? rot = null)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        go.transform.rotation = rot ?? Quaternion.identity;
        var e = go.AddComponent<KitchenElement>();
        e.BoardName = name;
        e.DimensionsMM = dims;
        _spawned.Add(go);
        return e;
    }

    private static SnapResult Snap(KitchenElement moved, KitchenElement target, Vector3 testPos)
    {
        return SnapSystem.TrySnap(moved, new List<KitchenElement> { target }, testPos);
    }

    // Оракул: после снэпа доски касаются плоскостью и не пересекаются.
    private void AssertSnappedFlush(KitchenElement moved, KitchenElement target, Vector3 testPos)
    {
        var r = Snap(moved, target, testPos);
        Assert.IsTrue(r.snapped, "ожидалось прилипание");
        moved.transform.position = r.position;

        Assert.IsFalse(SnapSystem.ElementsIntersect(moved, target),
            "после снэпа доски не должны пересекаться");

        var val = ConstraintValidator.Validate(new List<KitchenElement> { moved, target });
        Assert.IsTrue(val.contacts.Exists(c => c.isFaceToFace),
            "после снэпа должен быть face-to-face контакт (касание плоскостью)");
    }

    // --- Кромки/вершины/центр на большой грани (мелкая доска не центрируется) ---

    [Test]
    public void SmallBoard_LeftEdge_AlignsLeft()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(400, 400, 18), Vector3.zero);
        var r = Snap(b, a, new Vector3(-0.18f, 0f, 0.02f));
        Assert.IsTrue(r.snapped);
        Assert.AreEqual(-0.20f, r.position.x, 0.001f);
        Assert.AreEqual(0.018f, r.position.z, 0.001f);
    }

    [Test]
    public void SmallBoard_RightEdge_AlignsRight()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(400, 400, 18), Vector3.zero);
        var r = Snap(b, a, new Vector3(0.18f, 0f, 0.02f));
        Assert.IsTrue(r.snapped);
        Assert.AreEqual(0.20f, r.position.x, 0.001f);
    }

    [Test]
    public void SmallBoard_NearCenter_AlignsCenter()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(400, 400, 18), Vector3.zero);
        var r = Snap(b, a, new Vector3(0.03f, 0f, 0.02f));
        Assert.IsTrue(r.snapped);
        Assert.AreEqual(0.0f, r.position.x, 0.001f);
    }

    [Test]
    public void SmallBoard_Corner_AlignsBothEdges()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(400, 200, 18), Vector3.zero);
        var r = Snap(b, a, new Vector3(-0.18f, 0.08f, 0.02f));
        Assert.IsTrue(r.snapped);
        Assert.AreEqual(-0.20f, r.position.x, 0.001f, "левая кромка");
        Assert.AreEqual(0.10f, r.position.y, 0.001f, "верхняя кромка");
        Assert.AreEqual(0.018f, r.position.z, 0.001f, "плоскости заподлицо");
    }

    // --- Стыки встык (равные доски) ---

    [Test]
    public void EqualBoards_ButtJointX_Flush()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(800, 400, 18), Vector3.zero);
        var r = Snap(b, a, new Vector3(0.82f, 0f, 0f));
        Assert.IsTrue(r.snapped);
        Assert.AreEqual(0.80f, r.position.x, 0.001f);
        AssertSnappedFlush(b, a, new Vector3(0.82f, 0f, 0f));
    }

    [Test]
    public void BoardOnTopOfBoard_Flush()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(800, 400, 18), Vector3.zero);
        var r = Snap(b, a, new Vector3(0f, 0.42f, 0f));
        Assert.IsTrue(r.snapped);
        Assert.AreEqual(0.40f, r.position.y, 0.001f);
    }

    // --- Пол ---

    [Test]
    public void BoardAboveFloor_SnapsDown()
    {
        var floor = Make("Floor", new Vector3Int(3000, 18, 3000), Vector3.zero);
        var b = Make("B", new Vector3Int(800, 400, 18), Vector3.zero);
        var r = Snap(b, floor, new Vector3(0f, 0.25f, 0f));
        Assert.IsTrue(r.snapped);
        Assert.AreEqual(0.209f, r.position.y, 0.001f);
    }

    // --- Повёрнутая доска (оракул) ---

    [Test]
    public void RotatedBoard_ButtJoint_FlushContact()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(400, 400, 18), Vector3.zero,
            Quaternion.AngleAxis(90f, Vector3.up));
        AssertSnappedFlush(b, a, new Vector3(0.43f, 0f, 0f));
    }

    // --- Три доски ---

    [Test]
    public void ThreeBoards_ThirdSnapsToNearestNeighbour()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(800, 400, 18), new Vector3(0.8f, 0f, 0f));
        var c = Make("C", new Vector3Int(800, 400, 18), Vector3.zero);

        var r = SnapSystem.TrySnap(c, new List<KitchenElement> { a, b }, new Vector3(1.62f, 0f, 0f));
        Assert.IsTrue(r.snapped);
        Assert.AreEqual("B", r.targetName, "должна прилипнуть к ближайшей доске B");
        Assert.AreEqual(1.60f, r.position.x, 0.001f);
    }

    [Test]
    public void ThreeBoards_BoxCorner_AllFlush()
    {
        // Пол + две вертикальные доски, образующие угол; каждая прилипает заподлицо.
        var floor = Make("Floor", new Vector3Int(3000, 18, 3000), Vector3.zero);
        var wallA = Make("WallA", new Vector3Int(800, 400, 18), Vector3.zero);
        var wallB = Make("WallB", new Vector3Int(800, 400, 18), Vector3.zero);

        // Стенка A встаёт на пол.
        AssertSnappedFlush(wallA, floor, new Vector3(0f, 0.25f, 0f));
        // Стенка B встаёт на пол рядом.
        AssertSnappedFlush(wallB, floor, new Vector3(0.6f, 0.25f, 0.3f));
    }

    // --- Негативные случаи ---

    [Test]
    public void NoSnap_TooFar()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(800, 400, 18), Vector3.zero);
        var r = Snap(b, a, new Vector3(2.0f, 0f, 0f));
        Assert.IsFalse(r.snapped);
    }

    [Test]
    public void NoSnap_WhenDisabled()
    {
        KitchenSettings.Instance.SnapEnabled = false;
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(800, 400, 18), Vector3.zero);
        var r = Snap(b, a, new Vector3(0.82f, 0f, 0f));
        Assert.IsFalse(r.snapped);
    }

    [Test]
    public void NoSnap_WhenIntersecting()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(800, 400, 18), Vector3.zero);
        var r = Snap(b, a, new Vector3(0.1f, 0f, 0f)); // глубоко перекрываются
        Assert.IsFalse(r.snapped);
    }

    // ====== Две доски — кромки (поименовано по todo) ======

    [Test]
    public void TwoBoards_EdgeToEdge_AlignsMinEdges()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(800, 400, 18), Vector3.zero);
        var r = Snap(b, a, new Vector3(-0.78f, 0f, 0.02f));
        Assert.IsTrue(r.snapped);
        Assert.AreEqual(-0.80f, r.position.x, 0.001f, "левые кромки совпадают");
    }

    [Test]
    public void TwoBoards_CenterToCenter_AlignsCenters()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(800, 400, 18), Vector3.zero);
        var r = Snap(b, a, new Vector3(0.01f, 0f, 0.02f));
        Assert.IsTrue(r.snapped);
        Assert.AreEqual(0f, r.position.x, 0.001f, "центры совпадают");
    }

    [Test]
    public void TwoBoards_MaxEdgeToMaxEdge()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(800, 400, 18), Vector3.zero);
        var r = Snap(b, a, new Vector3(0.78f, 0f, 0.02f));
        Assert.IsTrue(r.snapped);
        Assert.AreEqual(0.80f, r.position.x, 0.001f, "правые кромки совпадают");
    }

    [Test]
    public void TwoBoards_SmallBoardNearBigBoard_AlignsNearestEdge()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(400, 400, 18), Vector3.zero);
        var r = Snap(b, a, new Vector3(-0.18f, 0f, 0.02f));
        Assert.IsTrue(r.snapped);
        Assert.AreEqual(-0.20f, r.position.x, 0.001f, "мелкая доска липнет кромкой к большой");
    }

    [Test]
    public void TwoBoards_FaceToFace_Flush()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(800, 400, 18), Vector3.zero);
        AssertSnappedFlush(b, a, new Vector3(0f, 0f, 0.02f));
    }

    [Test]
    public void TwoBoards_Rotated90_EdgeToEdge()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(800, 400, 18), Vector3.zero,
            Quaternion.AngleAxis(90f, Vector3.up));
        AssertSnappedFlush(b, a, new Vector3(0.43f, 0f, 0f));
    }

    [Test]
    public void TwoBoards_Rotated45_EdgeToEdge()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(800, 400, 18), Vector3.zero,
            Quaternion.AngleAxis(45f, Vector3.up));
        var r = Snap(b, a, new Vector3(0.82f, 0f, 0.02f));
        Assert.IsTrue(r.snapped, "повёрнутая на 45° доска должна прилипать");
    }

    [Test]
    public void TwoBoards_PartialOverlapBelow30Percent_NotSnapped()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(800, 400, 18), Vector3.zero);
        // Сдвиг по Z так, чтобы грань A (0.8x0.4) и грань B перекрывались < 30%
        // Минимальное перекрытие 30% = 0.3 * 0.32м² = 0.096м².
        // Смещаем B по Y на 0.25м: перекрытие = 0.8 * (0.4 - 0.25*2) = 0.8 * -0.1 = нет перекрытия.
        var r = Snap(b, a, new Vector3(0f, 0.35f, 0.02f));
        Assert.IsFalse(r.snapped, "перекрытие <30% — не должно снэпаться");
    }

    [Test]
    public void TwoBoards_ExactlyAtThreshold_Snapped()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(800, 400, 18), Vector3.zero);
        // Зазор ровно 50мм = 0.05м между гранями
        var r = Snap(b, a, new Vector3(0.85f, 0f, 0f));
        Assert.IsTrue(r.snapped, "ровно на пороге 50мм — должно прилипнуть");
        Assert.AreEqual(0.80f, r.position.x, 0.001f);
    }

    [Test]
    public void TwoBoards_JustOverThreshold_NotSnapped()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(800, 400, 18), Vector3.zero);
        // Зазор 51мм = 0.051м > порога 50мм
        var r = Snap(b, a, new Vector3(0.851f, 0f, 0f));
        Assert.IsFalse(r.snapped, "51мм > 50мм — не должно прилипать");
    }

    // ====== Три доски ======

    [Test]
    public void ThreeBoards_ChainABC_AllSnapped()
    {
        var a = Make("A", new Vector3Int(600, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(600, 400, 18), new Vector3(0.6f, 0f, 0f));
        var c = Make("C", new Vector3Int(600, 400, 18), new Vector3(1.2f, 0f, 0f));

        var r = SnapSystem.TrySnap(c, new List<KitchenElement> { a, b },
            new Vector3(1.22f, 0f, 0f));
        Assert.IsTrue(r.snapped, "C должна прилипнуть к B");
        Assert.AreEqual("B", r.targetName);
        c.transform.position = r.position;

        r = SnapSystem.TrySnap(b, new List<KitchenElement> { a, c },
            new Vector3(0.62f, 0f, 0f));
        Assert.IsTrue(r.snapped, "B должна прилипнуть к A");
    }

    [Test]
    public void ThreeBoards_TShape_VerticalOnHorizontal()
    {
        var floor = Make("Floor", new Vector3Int(3000, 18, 3000), Vector3.zero);
        var horiz = Make("H", new Vector3Int(800, 400, 18), Vector3.zero);
        AssertSnappedFlush(horiz, floor, new Vector3(0f, 0.25f, 0f));

        var vert = Make("V", new Vector3Int(400, 800, 18), Vector3.zero,
            Quaternion.AngleAxis(90f, Vector3.right));
        // Вертикальная доска встаёт на горизонтальную
        AssertSnappedFlush(vert, horiz, new Vector3(0f, 0.42f, 0f));
    }

    [Test]
    public void ThreeBoards_LShape_CornerAlignment()
    {
        var floor = Make("Floor", new Vector3Int(3000, 18, 3000), Vector3.zero);
        var a = Make("A", new Vector3Int(600, 400, 18), Vector3.zero);
        AssertSnappedFlush(a, floor, new Vector3(0f, 0.25f, 0f));

        var b = Make("B", new Vector3Int(600, 400, 18), Vector3.zero);
        // B прилипает к A сбоку, образуя угол
        AssertSnappedFlush(b, a, new Vector3(0.62f, 0f, 0.3f));
    }

    [Test]
    public void ThreeBoards_UShape_ThreeWalls()
    {
        var floor = Make("Floor", new Vector3Int(3000, 18, 3000), Vector3.zero);
        var left = Make("Left", new Vector3Int(400, 400, 18), Vector3.zero);
        AssertSnappedFlush(left, floor, new Vector3(-0.3f, 0.25f, 0f));

        var right = Make("Right", new Vector3Int(400, 400, 18), Vector3.zero);
        AssertSnappedFlush(right, floor, new Vector3(0.3f, 0.25f, 0f));

        var back = Make("Back", new Vector3Int(800, 400, 18), Vector3.zero);
        // Задняя стенка прилипает к обеим боковым
        var r = SnapSystem.TrySnap(back, new List<KitchenElement> { left, right, floor },
            new Vector3(0f, 0.25f, -0.3f));
        Assert.IsTrue(r.snapped, "задняя стенка должна прилипнуть");
        AssertSnappedFlush(back, floor, new Vector3(0f, 0.25f, -0.3f));
    }

    [Test]
    public void ThreeBoards_StackOnFloor()
    {
        var floor = Make("Floor", new Vector3Int(3000, 18, 3000), Vector3.zero);
        var a = Make("A", new Vector3Int(600, 400, 18), Vector3.zero);
        AssertSnappedFlush(a, floor, new Vector3(0f, 0.25f, 0f));

        var b = Make("B", new Vector3Int(600, 400, 18), Vector3.zero);
        AssertSnappedFlush(b, a, new Vector3(0f, 0.65f, 0f));

        var c = Make("C", new Vector3Int(600, 400, 18), Vector3.zero);
        AssertSnappedFlush(c, b, new Vector3(0f, 1.05f, 0f));
    }

    [Test]
    public void ThreeBoards_ConflictingSnaps_ChoosesNearest()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(800, 400, 18), new Vector3(0.8f, 0f, 0f));
        var c = Make("C", new Vector3Int(800, 400, 18), Vector3.zero);

        var r = SnapSystem.TrySnap(c, new List<KitchenElement> { a, b },
            new Vector3(1.62f, 0f, 0f));
        Assert.IsTrue(r.snapped);
        Assert.AreEqual("B", r.targetName, "должна выбрать B — C ближе к B, чем к A");
    }

    [Test]
    public void ThreeBoards_MixedRotations()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(400, 400, 18), Vector3.zero,
            Quaternion.AngleAxis(90f, Vector3.up));
        AssertSnappedFlush(b, a, new Vector3(0.43f, 0f, 0f));

        var c = Make("C", new Vector3Int(400, 400, 18), Vector3.zero,
            Quaternion.AngleAxis(45f, Vector3.up));
        var r = Snap(c, b, new Vector3(0f, 0.42f, 0f));
        Assert.IsTrue(r.snapped, "C с поворотом 45° должна прилипнуть к B");
    }

    // ====== Особые случаи ======

    [Test]
    public void BoardOnFloor_CenterY0_2_IsValid()
    {
        var floor = Make("Floor", new Vector3Int(3000, 18, 3000), Vector3.zero);
        var b = Make("B", new Vector3Int(800, 400, 18), new Vector3(0f, 0.209f, 0f));
        // Доска стоит на полу: валидация должна пройти
        var val = ConstraintValidator.Validate(new List<KitchenElement> { floor, b });
        Assert.IsTrue(val.isValid, "доска на полу должна быть валидна");
    }

    [Test]
    public void BoardOnFloor_SnapsFromAbove()
    {
        var floor = Make("Floor", new Vector3Int(3000, 18, 3000), Vector3.zero);
        var b = Make("B", new Vector3Int(800, 400, 18), Vector3.zero);
        var r = Snap(b, floor, new Vector3(0f, 0.30f, 0f));
        Assert.IsTrue(r.snapped, "доска в воздухе должна прилипнуть к полу");
        Assert.AreEqual(0.209f, r.position.y, 0.001f);
    }

    [Test]
    public void BoardDisabled_DoesNotSnap()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(800, 400, 18), Vector3.zero);
        a.gameObject.SetActive(false); // цель отключена

        var r = SnapSystem.TrySnap(b, new List<KitchenElement> { a },
            new Vector3(0.82f, 0f, 0f));
        Assert.IsFalse(r.snapped, "отключённая цель не участвует в снэпе");
    }

    [Test]
    public void MovedBoardDisabled_DoesNotSnap()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(800, 400, 18), Vector3.zero);
        b.gameObject.SetActive(false);

        var r = SnapSystem.TrySnap(b, new List<KitchenElement> { a },
            new Vector3(0.82f, 0f, 0f));
        Assert.IsFalse(r.snapped, "отключённая доска не может снэпаться");
    }

    [Test]
    public void ZeroDimensions_ClampedToOne()
    {
        var b = Make("B", new Vector3Int(0, -5, 0), Vector3.zero);
        Assert.AreEqual(1, b.DimensionsMM.x, "X должен быть ≥1");
        Assert.AreEqual(1, b.DimensionsMM.y, "Y должен быть ≥1");
        Assert.AreEqual(1, b.DimensionsMM.z, "Z должен быть ≥1");
    }
}
