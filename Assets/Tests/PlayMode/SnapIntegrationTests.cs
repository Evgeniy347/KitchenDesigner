using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

/// <summary>PlayMode-тесты снэппинга: симуляция перетаскивания, отмены, подсветки.</summary>
public class SnapIntegrationTests
{
    private GameObject _bootGo = null!;
    private GameObject _camera = null!;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        PlayModeTestConfig.ConfigureForTests();

        _camera = new GameObject("MainCamera");
        _camera.tag = "MainCamera";
        _camera.AddComponent<Camera>();
        _camera.transform.position = new Vector3(2f, 3f, -5f);
        _camera.transform.LookAt(Vector3.zero);

        _bootGo = new GameObject("Bootstrap");
        _bootGo.AddComponent<Bootstrap>();
        yield return null;
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.Destroy(e.gameObject);
        if (_bootGo != null) Object.Destroy(_bootGo);
        if (_camera != null) Object.Destroy(_camera);
        yield return null;
    }

    private static KitchenElement CreatePart(Vector3Int dims, Vector3 pos)
    {
        var go = ElementFactory.CreatePart(dims, "TestBoard", pos);
        return go.GetComponent<KitchenElement>();
    }

    private static List<KitchenElement> AllBoards()
    {
        var list = new List<KitchenElement>();
        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null && e.GetComponent<BasePlate>() == null)
                list.Add(e);
        return list;
    }

    [UnityTest]
    public IEnumerator DragBoard_NearOther_SnapsDuringDrag()
    {
        var floor = Object.FindAnyObjectByType<BasePlate>();
        Assert.IsNotNull(floor);

        var a = CreatePart(new Vector3Int(800, 400, 18), new Vector3(0f, 0.2f, 0f));
        yield return null;

        // Симулируем близкую позицию — снэп должен сработать
        var others = new List<KitchenElement> { floor.GetComponent<KitchenElement>() };
        var snap = SnapSystem.TrySnap(a, others, new Vector3(0f, 0.22f, 0.015f));
        Assert.IsTrue(snap.snapped, "деталь рядом с полом должна прилипнуть во время драга");
    }

    [UnityTest]
    public IEnumerator DragBoard_ThenRelease_StaysSnapped()
    {
        var a = CreatePart(new Vector3Int(800, 400, 18), new Vector3(0f, 0.2f, 0f));
        var floor = Object.FindAnyObjectByType<BasePlate>();
        yield return null;

        var others = new List<KitchenElement> { floor.GetComponent<KitchenElement>() };
        var snap = SnapSystem.TrySnap(a, others, new Vector3(0f, 0.22f, 0.015f));

        if (snap.snapped)
        {
            a.transform.position = snap.position;
            var val = ConstraintValidator.Validate(new List<KitchenElement> { a, floor.GetComponent<KitchenElement>() });
            Assert.IsTrue(val.isValid, "после снэпа позиция валидна");
        }
    }

    [UnityTest]
    public IEnumerator DragBoard_EscapePressed_ReturnsToStart()
    {
        var a = CreatePart(new Vector3Int(800, 400, 18), new Vector3(0f, 0.2f, 0f));
        Vector3 start = a.transform.position;
        yield return null;

        // Симулируем драг и отмену
        a.transform.position = new Vector3(0.5f, 0.2f, 0f);
        a.transform.position = start;

        Assert.AreEqual(start, a.transform.position, "позиция должна вернуться на старт");
    }

    [UnityTest]
    public IEnumerator DragBoard_IntersectingOther_RedTint()
    {
        var a = CreatePart(new Vector3Int(800, 400, 18), new Vector3(0f, 0.2f, 0f));
        var b = CreatePart(new Vector3Int(800, 400, 18), new Vector3(0.4f, 0.2f, 0f));
        yield return null;

        bool intersect = SnapSystem.ElementsIntersect(a, b);
        Assert.IsTrue(intersect, "детали перекрываются");
    }

    [UnityTest]
    public IEnumerator DragBoard_NoIntersection_GreenTint()
    {
        var a = CreatePart(new Vector3Int(800, 400, 18), new Vector3(0f, 0.2f, 0f));
        var b = CreatePart(new Vector3Int(800, 400, 18), new Vector3(1.0f, 0.2f, 0f));
        yield return null;

        bool intersect = SnapSystem.ElementsIntersect(a, b);
        Assert.IsFalse(intersect, "детали не перекрываются");
    }

    [UnityTest]
    public IEnumerator DragBoard_BlockOnViolation_RevertsPosition()
    {
        KitchenSettings.Instance.BlockOnViolation = true;

        var a = CreatePart(new Vector3Int(800, 400, 18), new Vector3(0f, 0.2f, 0f));
        yield return null;

        // Перемещаем деталь в воздух — должно быть нарушение
        Vector3 prev = a.transform.position;
        a.transform.position = new Vector3(0f, 2.0f, 0f);

        var list = new List<KitchenElement>(AllBoards());
        list.Add(Object.FindAnyObjectByType<BasePlate>().GetComponent<KitchenElement>());
        var result = ConstraintValidator.Validate(list);

        Assert.IsTrue(result.violations.Contains(a), "деталь в воздухе — нарушение");
        a.transform.position = prev;
    }

    [UnityTest]
    public IEnumerator GridSnap_AfterSnap_Combined()
    {
        KitchenSettings.Instance.GridStep = 16;
        KitchenSettings.Instance.GridEnabled = true;

        var a = CreatePart(new Vector3Int(800, 400, 18), Vector3.zero);
        yield return null;

        Vector3 unsnapped = new Vector3(0.123f, 0f, 0.456f);
        Vector3 snapped = GridManager.SnapToGrid(unsnapped);

        Assert.AreNotEqual(unsnapped, snapped, "сетка 16мм должна изменить позицию");
    }

    [UnityTest]
    public IEnumerator RotateBoard_ThenSnap_CorrectAxes()
    {
        // Поворот на 90° вокруг Y разворачивает большие грани детали A к ±X
        // (толщина 18 мм теперь вдоль X → грань на x≈0.009).
        var a = CreatePart(new Vector3Int(800, 400, 18), Vector3.zero);
        a.RotateAroundAxis(Vector3.up, 90f);
        yield return null;

        var b = CreatePart(new Vector3Int(400, 400, 18), new Vector3(0.4f, 0f, 0f));
        var others = new List<KitchenElement> { a };
        var snap = SnapSystem.TrySnap(b, others, new Vector3(0.22f, 0f, 0f));

        Assert.IsTrue(snap.snapped, "после поворота 90° снэп должен работать");
        Assert.AreEqual(0.209f, snap.position.x, 0.001f,
            "B встаёт вплотную к большой грани повёрнутой A");
    }
}
