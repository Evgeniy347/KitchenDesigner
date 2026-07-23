using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class ResizeSnapTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    private KitchenElement Make(Vector3 pos, Vector3Int dims)
    {
        var go = new GameObject("E");
        go.transform.position = pos;
        var e = go.AddComponent<KitchenElement>();
        e.DimensionsMM = dims;
        _spawned.Add(go);
        return e;
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
    }

    // A: центр (0,0.2,0), +X-грань на x=0.4. B слева/справа варьируем.
    private static (Vector3 c, Vector3 n, Vector3 u, Vector3 v, Vector2 s) PlusXFace(KitchenElement a)
    {
        var f = a.GetFaces()[0]; // +X
        return (f.center, f.normal, f.rightAxis, f.upAxis, f.size);
    }

    [Test]
    public void SnapDelta_OpposingFaceWithinThreshold_ReturnsGap()
    {
        var a = Make(new Vector3(0, 0.2f, 0), new Vector3Int(800, 400, 18));
        // -X-грань B на x=0.45 → зазор 0.05 от +X-грани A (x=0.4).
        var b = Make(new Vector3(0.85f, 0.2f, 0), new Vector3Int(800, 400, 18));
        var fa = PlusXFace(a);

        bool snapped = ResizeSnap.SnapDelta(fa.c, fa.n, fa.u, fa.v, fa.s,
            new List<KitchenElement> { b }, a, 0.05f, out float gap);

        Assert.IsTrue(snapped);
        Assert.AreEqual(0.05f, gap, 0.0005f);
    }

    [Test]
    public void SnapDelta_BeyondThreshold_ReturnsFalse()
    {
        var a = Make(new Vector3(0, 0.2f, 0), new Vector3Int(800, 400, 18));
        var b = Make(new Vector3(1.5f, 0.2f, 0), new Vector3Int(800, 400, 18)); // -X на x=1.1
        var fa = PlusXFace(a);

        bool snapped = ResizeSnap.SnapDelta(fa.c, fa.n, fa.u, fa.v, fa.s,
            new List<KitchenElement> { b }, a, 0.05f, out _);

        Assert.IsFalse(snapped);
    }

    [Test]
    public void SnapDelta_NoInPlaneOverlap_ReturnsFalse()
    {
        var a = Make(new Vector3(0, 0.2f, 0), new Vector3Int(800, 400, 18));
        // По нормали близко, но смещена по Y → грани не перекрываются в плоскости.
        var b = Make(new Vector3(0.85f, 5f, 0), new Vector3Int(800, 400, 18));
        var fa = PlusXFace(a);

        bool snapped = ResizeSnap.SnapDelta(fa.c, fa.n, fa.u, fa.v, fa.s,
            new List<KitchenElement> { b }, a, 0.05f, out _);

        Assert.IsFalse(snapped);
    }

    [Test]
    public void SnapDelta_CoDirectionalFace_NotSnapped()
    {
        var a = Make(new Vector3(0, 0.2f, 0), new Vector3Int(800, 400, 18));
        // B слева: к +X-грани A обращена со-направленная +X-грань B (dot≈+1) — не стык.
        var b = Make(new Vector3(-0.85f, 0.2f, 0), new Vector3Int(800, 400, 18));
        var fa = PlusXFace(a);

        bool snapped = ResizeSnap.SnapDelta(fa.c, fa.n, fa.u, fa.v, fa.s,
            new List<KitchenElement> { b }, a, 0.05f, out _);

        Assert.IsFalse(snapped);
    }

    [Test]
    public void SnapDelta_NullOthers_ReturnsFalse()
    {
        var a = Make(new Vector3(0, 0.2f, 0), new Vector3Int(800, 400, 18));
        var fa = PlusXFace(a);
        Assert.IsFalse(ResizeSnap.SnapDelta(fa.c, fa.n, fa.u, fa.v, fa.s, null!, a, 0.05f, out _));
    }

    /// <summary>Репро из сцены: вертикальная стойка (A12_upper_shelf_2_1_2) стоит
    /// торцом ровно у кромки горизонтальной панели (B12_upper_top) — их footprint'ы
    /// делят РОВНО РЕБРО по X. Растягивание стойки вверх обязано ловить плоскость
    /// панели, но перекрытие с нулевой площадью отбрасывалось, и деталь проезжала
    /// весь диапазон без единого прилипания.</summary>
    [Test]
    public void SnapDelta_EdgeOnlyContact_Snaps()
    {
        // Панель: X[545..1567], Y[2242..2260], Z[-3620..-3288].
        var top = Make(new Vector3(1.056f, 2.251f, -3.454f), new Vector3Int(1022, 18, 332));
        // Стойка: X[1567..1585] — примыкает к панели ровно по x=1567.
        var post = Make(new Vector3(1.576f, 1.818f, -3.454f), new Vector3Int(18, 883, 331));

        var f = post.GetFaces()[2]; // +Y — верхняя грань стойки (y=2.2595)
        Assert.AreEqual(1f, Vector3.Dot(f.normal, Vector3.up), 0.001f, "грань смотрит вверх");

        bool snapped = ResizeSnap.SnapDelta(f.center, f.normal, f.rightAxis, f.upAxis, f.size,
            new List<KitchenElement> { top }, post, 0.05f, out float gap);

        Assert.IsTrue(snapped, "касание ровно по ребру — это контакт, прилипание обязано сработать");
        // Верх стойки на 2259.5 — ближайшая плоскость панели это её верх (2260),
        // а не низ (2242). Выбор ближайшего детента проверяется отдельно в
        // SnapDelta_FarSideOfNeighbour_IsSecondDetent.
        Assert.AreEqual(0.0005f, gap, 0.0002f, "заподлицо с верхом панели (2260 мм)");
    }

    /// <summary>Второй детент той же панели: доведя стойку под низ панели (2242),
    /// пользователь тянет дальше вверх — и она обязана поймать ВЕРХ панели (2260),
    /// встав с ней заподлицо. Плоскость соседа для ресайза двусторонняя; раньше
    /// принимались только встречные грани, и после первого детента стойка тянулась
    /// вверх без единого прилипания.</summary>
    [Test]
    public void SnapDelta_FarSideOfNeighbour_IsSecondDetent()
    {
        var top = Make(new Vector3(1.056f, 2.251f, -3.454f), new Vector3Int(1022, 18, 332));
        var post = Make(new Vector3(1.576f, 1.818f, -3.454f), new Vector3Int(18, 883, 331));
        var f = post.GetFaces()[2]; // +Y, верх стойки на 2259.5

        // Верх стойки поднят до 2255 — ближе к верху панели (2260), чем к низу (2242).
        Vector3 raised = f.center + f.normal * (-0.0045f);
        Assert.IsTrue(ResizeSnap.SnapDelta(raised, f.normal, f.rightAxis, f.upAxis, f.size,
            new List<KitchenElement> { top }, post, 0.05f, out float gapUp));
        Assert.AreEqual(0.005f, gapUp, 0.0005f, "заподлицо с верхом панели (2260 мм)");

        // Верх стойки опущен до 2230 — ближе к низу панели (2242).
        Vector3 lowered = f.center + f.normal * (-0.0295f);
        Assert.IsTrue(ResizeSnap.SnapDelta(lowered, f.normal, f.rightAxis, f.upAxis, f.size,
            new List<KitchenElement> { top }, post, 0.05f, out float gapDown));
        Assert.AreEqual(0.012f, gapDown, 0.0005f, "встык под низ панели (2242 мм)");
    }

    /// <summary>Контраст к предыдущему: если между footprint'ами есть настоящий
    /// зазор в плоскости грани (детали разнесены по X), контакта нет и снэпа быть
    /// не должно — послабление на касание не превращается в «липнет ко всему».</summary>
    [Test]
    public void SnapDelta_FootprintsApartInPlane_NoSnap()
    {
        var top = Make(new Vector3(1.056f, 2.251f, -3.454f), new Vector3Int(1022, 18, 332));
        // Стойка отодвинута ещё на 20 мм вправо: X[1587..1605], между ними 20 мм.
        var post = Make(new Vector3(1.596f, 1.818f, -3.454f), new Vector3Int(18, 883, 331));

        var f = post.GetFaces()[2];
        Assert.IsFalse(ResizeSnap.SnapDelta(f.center, f.normal, f.rightAxis, f.upAxis, f.size,
            new List<KitchenElement> { top }, post, 0.05f, out _),
            "footprint'ы разнесены — контакта нет");
    }
}
