using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Полка 260×561×18 (rotX=90), поднимаемая вверх вдоль тонкой боковины
/// 18×900×332 (rotY=90), проходит ДВА детента прилипания:
///   • Y≈1.351 — ГРАНЬ: верх полки заподлицо с низом боковины (Y-грань,
///     line contact по кромке Z). Работало и должно работать.
///   • Y≈1.369 — БОК: низ полки заподлицо с низом боковины, задняя грань полки
///     полностью прилегает к боковине (Z-грань, полная площадь). НЕ работало.
///
/// Геометрия из реальной сцены (2026-07-19), полка заподлицо со стеной A по X:
///   A12_upper_B_side_L — боковина, world 332×900×18, низ Y=1.360, перед Z=-1.844.
///   A34K1_upper_bottom (copy) — полка, world 260×18×561, задняя грань Z=-1.844.
///   A — стена справа, левая грань X=1.585 (= правая грань полки).
///
/// Баг 1.369: на этой позиции боковой Z-контакт уже заподлицо → «нулевой» сдвиг,
/// а грань-контакт с низом боковины (line contact, сдвиг 18 мм вниз) считался
/// «содержательным» и перебивал его — полку кидало обратно на 1.351.
/// Фикс: полноплощадный «нулевой» контакт (боковой Z) важнее слабого кромочного
/// притяжения (Y-грань) — деталь остаётся на 1.369.
///
/// ВНИМАНИЕ: тесты не запускались (сборка на паузе — правится MCP). Это
/// спецификация ожидаемого поведения; прогнать при первой возможности.
/// </summary>
public class SnapThinSidePanelLineContactTests : SnapTestBase
{
    private KitchenElement? _sidePanel; // A12_upper_B_side_L
    private KitchenElement? _shelf;     // A34K1_upper_bottom (copy)

    private const float EdgeY = 1.351f;        // детент ГРАНЬ (верх полки = низ боковины)
    private const float FullSurfaceY = 1.369f; // детент БОК (низ полки = низ боковины)

    [SetUp]
    public void BuildScene()
    {
        // Боковина: world X=332, Y=900, Z=18. Низ Y=1.360, передняя грань Z=-1.844.
        _sidePanel = Make("A12_upper_B_side_L",
            new Vector3Int(18, 900, 332),
            new Vector3(1.419f, 1.81f, -1.853f),
            ManagedRotation.Euler(0f, 90f, 0f));

        // Полка: world X=260, Y=18, Z=561. Задняя грань Z=-1.844 (заподлицо с
        // передней гранью боковины по Z). Старт — «покой» ниже боковины.
        _shelf = Make("A34K1_upper_bottom_copy",
            new Vector3Int(260, 561, 18),
            new Vector3(1.455f, 1.350f, -1.5635f),
            ManagedRotation.Euler(90f, 0f, 0f));

        // Стена A справа: полка уже заподлицо с ней по X (right face полки x=1.585).
        // Это полноплощадный X-контакт, как в реальной сцене.
        Make("A",
            new Vector3Int(100, 2700, 7240),
            new Vector3(1.635f, 1.35f, 0f));
    }

    private List<KitchenElement> Others() =>
        new List<KitchenElement>(_spawned.ConvertAll(go => go.GetComponent<KitchenElement>()));

    // ── Детент 1.369 (БОК) — то, что чиним ───────────────────────────────────

    [Test]
    public void Lifted_To1369_SnapsFullSurface_DoesNotFallBackTo1351()
    {
        var testPos = new Vector3(1.455f, FullSurfaceY, -1.5635f);
        var r = SnapSystem.TrySnap(_shelf!, Others(), testPos);

        Assert.IsTrue(r.snapped, "на 1.369 полка должна прилипнуть боком к боковине");
        Assert.AreEqual(FullSurfaceY, r.position.y, Tol,
            "полка остаётся на 1.369 (полноповерхностный боковой контакт)");
        Assert.Greater(r.position.y, 1.360f,
            "полку НЕ должно откидывать вниз на грань-контакт 1.351");
        Assert.AreEqual(testPos.x, r.position.x, Tol, "X не меняется");
        Assert.AreEqual(testPos.z, r.position.z, Tol, "Z не меняется");
    }

    [Test]
    public void LiftingUp_Near1369_SnapsUpTo1369()
    {
        // Ближе к боковому детенту (перекрытие Z-граней по Y ~ 78%): подтягивается
        // вверх к 1.369, а не вниз к 1.351.
        var testPos = new Vector3(1.455f, 1.365f, -1.5635f);
        var r = SnapSystem.TrySnap(_shelf!, Others(), testPos);

        Assert.IsTrue(r.snapped);
        Assert.AreEqual(FullSurfaceY, r.position.y, Tol, "подтягивается вверх к 1.369");
    }

    [Test]
    public void SnappedTo1369_NoIntersectionWithSidePanel()
    {
        var testPos = new Vector3(1.455f, FullSurfaceY, -1.5635f);
        var r = SnapSystem.TrySnap(_shelf!, Others(), testPos);

        Assert.IsTrue(r.snapped);
        _shelf!.transform.position = r.position;
        Assert.IsFalse(SnapSystem.ElementsIntersect(_shelf, _sidePanel!),
            "после снэпа полка не должна пересекать боковину");
    }

    // ── Детент 1.351 (ГРАНЬ) — не должен сломаться ───────────────────────────

    [Test]
    public void Lifted_To1351_SnapsEdge_DoesNotJumpTo1369()
    {
        // На грань-детенте (верх полки у низа боковины) полка держится у ~1.351
        // и НЕ должна преждевременно прыгать к боковому контакту 1.369.
        var testPos = new Vector3(1.455f, EdgeY, -1.5635f);
        var r = SnapSystem.TrySnap(_shelf!, Others(), testPos);

        Assert.IsTrue(r.snapped, "на 1.351 полка должна прилипать (грань)");
        Assert.Less(r.position.y, 1.360f,
            "на грань-детенте полку НЕ должно подтягивать к боковому контакту 1.369");
        Assert.AreEqual(EdgeY, r.position.y, 0.002f, "держится у 1.351");
    }
}
