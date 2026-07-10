using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Общая база для EditMode-тестов прилипания: детерминированные настройки,
/// фабрика досок с авто-очисткой и набор «оракулов» (проверок ожидаемого
/// поведения снэпа). Геометрия (в метрах, identity-поворот):
///   доска (W,H,D) мм → полуразмеры (W/2,H/2,D/2)*0.001;
///   грани ±X (нормаль X, размер H×D), ±Y (W×D), ±Z (W×H).
/// </summary>
public abstract class SnapTestBase
{
    protected readonly List<GameObject> _spawned = new List<GameObject>();
    private bool _prevVerbose;

    /// <summary>Допуск позиций — 1 мм.</summary>
    protected const float Tol = 0.001f;
    protected const float MM = 0.001f;

    [SetUp]
    public void BaseSetup()
    {
        var s = KitchenSettings.Instance;
        Assert.IsNotNull(s, "Resources/KitchenSettings.asset не найден");
        s.GridStep = 1;
        s.GridEnabled = true;
        s.SnapEnabled = true;
        s.SnapThreshold = 50f;
        s.BlockOnViolation = false;

        _prevVerbose = SnapSystem.VerboseLog;
        SnapSystem.VerboseLog = false; // не засорять вывод тестов
        OnSetup();
    }

    /// <summary>Доп. настройка в наследниках (необязательно).</summary>
    protected virtual void OnSetup() { }

    [TearDown]
    public void BaseTeardown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        SnapSystem.VerboseLog = _prevVerbose;
    }

    // --- Фабрика ---

    protected KitchenElement Make(string name, Vector3Int dims, Vector3 pos, Quaternion? rot = null)
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

    /// <summary>Стандартная доска 800×400×18.</summary>
    protected KitchenElement MakeStd(string name, Vector3 pos, Quaternion? rot = null)
        => Make(name, new Vector3Int(800, 400, 18), pos, rot);

    /// <summary>Пол 3000×18×3000 (верхняя грань на y=0).</summary>
    protected KitchenElement MakeFloor(string name = "Floor")
        => Make(name, new Vector3Int(3000, 18, 3000), Vector3.zero);

    // --- Вызовы снэпа ---

    protected static SnapResult Snap(KitchenElement moved, KitchenElement target, Vector3 testPos)
        => SnapSystem.TrySnap(moved, new List<KitchenElement> { target }, testPos);

    protected static SnapResult Snap(KitchenElement moved, List<KitchenElement> targets, Vector3 testPos)
        => SnapSystem.TrySnap(moved, targets, testPos);

    // --- Оракулы ---

    /// <summary>Прилипло и итоговая позиция совпала с ожидаемой (по 1 мм по каждой оси).</summary>
    protected SnapResult AssertSnappedAt(KitchenElement moved, KitchenElement target, Vector3 testPos, Vector3 expected, string msg = "")
    {
        var r = Snap(moved, target, testPos);
        Assert.IsTrue(r.snapped, $"ожидалось прилипание из {testPos}. {msg}");
        Assert.AreEqual(expected.x, r.position.x, Tol, $"X неверный. {msg}");
        Assert.AreEqual(expected.y, r.position.y, Tol, $"Y неверный. {msg}");
        Assert.AreEqual(expected.z, r.position.z, Tol, $"Z неверный. {msg}");
        return r;
    }

    /// <summary>Прилипло, и после применения — face-to-face контакт без пересечения.
    /// Геометрически-независимый оракул: работает для любых размеров/поворотов.</summary>
    protected SnapResult AssertFlushContact(KitchenElement moved, KitchenElement target, Vector3 testPos, string msg = "")
    {
        var r = Snap(moved, target, testPos);
        Assert.IsTrue(r.snapped, $"ожидалось прилипание из {testPos}. {msg}");
        moved.transform.position = r.position;

        Assert.IsFalse(SnapSystem.ElementsIntersect(moved, target),
            $"после снэпа доски не должны пересекаться. {msg}");

        var val = ConstraintValidator.Validate(new List<KitchenElement> { moved, target });
        Assert.IsTrue(val.contacts.Exists(c => c.isFaceToFace),
            $"после снэпа должен быть face-to-face контакт. {msg}");
        return r;
    }

    protected void AssertNotSnapped(KitchenElement moved, KitchenElement target, Vector3 testPos, string msg = "")
    {
        var r = Snap(moved, target, testPos);
        Assert.IsFalse(r.snapped, $"не ожидалось прилипание из {testPos}. {msg}");
    }
}
