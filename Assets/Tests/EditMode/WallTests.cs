using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class WallTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    private KitchenElement Make(string name, Vector3Int dims, Vector3 pos, bool wall)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var e = go.AddComponent<KitchenElement>();
        e.BoardName = name;
        e.DimensionsMM = dims;
        if (wall) go.AddComponent<Wall>();
        _spawned.Add(go);
        return e;
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
    }

    [Test]
    public void Specification_ExcludesWalls()
    {
        Make("Board", new Vector3Int(800, 400, 18), Vector3.zero, false);
        Make("Стена", new Vector3Int(2000, 2500, 100), new Vector3(0, 1.25f, 0), true);

        var spec = SpecificationManager.Build(_spawned.ConvertAll(g => g.GetComponent<KitchenElement>()));

        Assert.AreEqual(1, spec.totalCount, "стена не входит в спецификацию");
        Assert.AreEqual("Board", spec.lines[0].name);
    }

    [Test]
    public void Validator_LoneWall_IsNotViolation()
    {
        Make("Стена", new Vector3Int(2000, 2500, 100), new Vector3(0, 1.25f, 0), true);

        var list = _spawned.ConvertAll(g => g.GetComponent<KitchenElement>());
        var result = ConstraintValidator.Validate(list);

        Assert.IsFalse(result.violations.Exists(e => e.GetComponent<Wall>() != null),
            "стена — структурный якорь, а не нарушение");
    }

    // Баг: при захвате полускрытой (опущенной) стены база перемещения бралась в
    // опущенном состоянии (position.y смещён вниз), затем стена возвращалась на
    // полную высоту — и объект «прыгал» в неожиданное место. При захвате стену
    // надо сперва вернуть на полную высоту, ЗАТЕМ брать стартовую точку.
    [Test]
    public void LoweredWall_Grab_RestoresFull_AndMovesFromFullBase()
    {
        var e = Make("Стена", new Vector3Int(2000, 2500, 100), new Vector3(0, 1.25f, 0), true);
        var wall = e.GetComponent<Wall>();
        wall.SetLowered(true, 0.1f); // полускрытая: центр опущен до y≈0.05
        Assert.AreEqual(0.05f, e.transform.position.y, 0.001f, "стена опущена");

        // Захват: должна вернуться на полную высоту, старт — полный центр (а не опущенный).
        Vector3 start = ElementMover.GrabStart(e);
        Assert.AreEqual(2.5f, e.transform.localScale.y, 0.001f, "стена снова полностью видна");
        Assert.AreEqual(1.25f, start.y, 0.001f, "база — полный центр");

        // Горизонтальное перемещение: низ стены остаётся на полу, она не улетает по Y.
        ElementMover.ApplyDelta(new List<KitchenElement> { e }, new List<Vector3> { start }, new Vector3(1, 0, 0));
        Assert.AreEqual(1f, e.transform.position.x, 0.001f);
        Assert.AreEqual(1.25f, e.transform.position.y, 0.001f);
        float baseY = e.transform.position.y - e.transform.localScale.y * 0.5f;
        Assert.AreEqual(0f, baseY, 0.001f, "низ стены остаётся на полу");
    }

    [Test]
    public void SetLowered_ReducesHeight_KeepsBaseOnFloor()
    {
        var e = Make("Стена", new Vector3Int(2000, 2500, 100), new Vector3(0, 1.25f, 0), true);
        var wall = e.GetComponent<Wall>();
        float baseBefore = e.transform.position.y - e.transform.localScale.y * 0.5f;

        wall.SetLowered(true, 0.1f); // 100 мм
        Assert.AreEqual(0.1f, e.transform.localScale.y, 0.0001f);
        Assert.AreEqual(baseBefore, e.transform.position.y - e.transform.localScale.y * 0.5f, 0.0001f);

        wall.RestoreFull();
        Assert.AreEqual(2.5f, e.transform.localScale.y, 0.0001f);
        Assert.AreEqual(1.25f, e.transform.position.y, 0.0001f);
    }
}
