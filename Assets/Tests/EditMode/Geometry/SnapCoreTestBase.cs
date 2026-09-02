using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>База для тестов ядра прилипания. Та же геометрия, что у сценового
/// SnapTestBase (деталь (W,H,D) мм → полуразмеры (W/2,H/2,D/2)·0.001; грани
/// ±X размера H×D, ±Y — W×D, ±Z — W×H), но на СНИМКАХ: ни GameObject, ни
/// настроек, ни очистки сцены. Поэтому эти тесты гоняет и Unity, и dotnet.
///
/// Порог задаётся явно (в сценовой версии он берётся из KitchenSettings) —
/// ядро глобального состояния не читает.</summary>
public abstract class SnapCoreTestBase
{
    /// <summary>Допуск позиций — 1 мм.</summary>
    protected const float Tol = 0.001f;
    protected const float MM = 0.001f;

    /// <summary>Порог прилипания 50 мм — тот же, что ставит сценовая база.</summary>
    protected const float Threshold = 50f * MM;

    /// <summary>Движимая деталь: ядро просит её геометрию для каждой примеряемой
    /// позиции, поэтому это не снимок, а маленькая фабрика снимков.</summary>
    protected sealed class Box : IPosedGeometry
    {
        private readonly string _name;
        private readonly Vector3 _size;
        private readonly Quaternion _rotation;
        private readonly bool _isPanel;
        private readonly Vector3 _mountNormal;

        public Box(string name, Vector3Int dims, Quaternion? rotation = null, bool isPanel = false,
            Vector3 mountNormal = default)
        {
            _name = name;
            _size = new Vector3(dims.x, dims.y, dims.z) * MM;
            _rotation = rotation ?? Quaternion.identity;
            _isPanel = isPanel;
            _mountNormal = mountNormal;
        }

        public ElementGeometry At(Vector3 position)
            => ElementGeometry.Box(_name, position, _size, _rotation, _isPanel, _mountNormal);
    }

    protected static Box Make(string name, Vector3Int dims, Quaternion? rotation = null)
        => new Box(name, dims, rotation);

    /// <summary>Поворот вокруг Y, собранный ВРУЧНУЮ. `Quaternion.AngleAxis` —
    /// вызов в нативный движок и под dotnet падает; синус с косинусом берём из
    /// System.Math, он чисто управляемый.</summary>
    protected static Quaternion RotY(float degrees)
    {
        double half = degrees * System.Math.PI / 360.0; // (deg/2) в радианах
        return new Quaternion(0f, (float)System.Math.Sin(half), 0f, (float)System.Math.Cos(half));
    }

    /// <summary>Поворот вокруг X — тем же ручным способом, что и RotY.</summary>
    protected static Quaternion RotX(float degrees)
    {
        double half = degrees * System.Math.PI / 360.0;
        return new Quaternion((float)System.Math.Sin(half), 0f, 0f, (float)System.Math.Cos(half));
    }

    /// <summary>Поворот вокруг Z.</summary>
    protected static Quaternion RotZ(float degrees)
    {
        double half = degrees * System.Math.PI / 360.0;
        return new Quaternion(0f, 0f, (float)System.Math.Sin(half), (float)System.Math.Cos(half));
    }

    /// <summary>Замена запрещённого в ядре <c>Quaternion.Euler</c>: Unity
    /// применяет углы в порядке Z→X→Y, то есть q = Y·X·Z.</summary>
    protected static Quaternion Euler(float x, float y, float z)
        => RotY(y) * RotX(x) * RotZ(z);

    /// <summary>Стандартная деталь 800×400×18.</summary>
    protected static Box MakeStd(string name, Quaternion? rotation = null)
        => Make(name, new Vector3Int(800, 400, 18), rotation);

    /// <summary>Пол 3000×18×3000 (верхняя грань на y=0 при позиции −9 мм).</summary>
    protected static ElementGeometry Floor(string name = "Floor")
        => At(Make(name, new Vector3Int(3000, 18, 3000)), new Vector3(0f, -9f * MM, 0f));

    /// <summary>Неподвижный сосед — готовый снимок в заданной позиции.</summary>
    protected static ElementGeometry At(Box box, Vector3 position) => box.At(position);

    protected static ElementGeometry Std(string name, Vector3 position)
        => At(MakeStd(name), position);

    // --- Вызовы снэпа ---

    protected static SnapResult Snap(Box moved, ElementGeometry target, Vector3 testPos)
        => SnapCore.TrySnap(moved, new List<ElementGeometry> { target }, testPos, Threshold);

    protected static SnapResult Snap(Box moved, List<ElementGeometry> targets, Vector3 testPos)
        => SnapCore.TrySnap(moved, targets, testPos, Threshold);

    // --- Оракулы ---

    /// <summary>Прилипло, и итоговая позиция совпала с ожидаемой (по 1 мм по оси).</summary>
    protected static SnapResult AssertSnappedAt(Box moved, ElementGeometry target,
        Vector3 testPos, Vector3 expected, string msg = "")
    {
        var r = Snap(moved, target, testPos);
        Assert.IsTrue(r.snapped, $"ожидалось прилипание из {testPos}. {msg}");
        Assert.AreEqual(expected.x, r.position.x, Tol, $"X неверный. {msg}");
        Assert.AreEqual(expected.y, r.position.y, Tol, $"Y неверный. {msg}");
        Assert.AreEqual(expected.z, r.position.z, Tol, $"Z неверный. {msg}");
        return r;
    }

    protected static void AssertNotSnapped(Box moved, ElementGeometry target,
        Vector3 testPos, string msg = "")
    {
        var r = Snap(moved, target, testPos);
        Assert.IsFalse(r.snapped, $"не ожидалось прилипание из {testPos}. {msg}");
    }

    /// <summary>Габариты не пересекаются (с допуском на касание). Заменяет
    /// сценовый SnapSystem.ElementsIntersect: считает по снимкам.</summary>
    protected static bool Intersect(in ElementGeometry a, in ElementGeometry b)
        => Tolerance.IntervalsOverlap(a.Min.x, a.Max.x, b.Min.x, b.Max.x)
        && Tolerance.IntervalsOverlap(a.Min.y, a.Max.y, b.Min.y, b.Max.y)
        && Tolerance.IntervalsOverlap(a.Min.z, a.Max.z, b.Min.z, b.Max.z);
}
