using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Общий крепёж врезной техники к детали-столешнице: мойка и варочная вели его
/// каждая своей копией (SnapToPart / TrackDrift / ReleaseFrom / FindAttachedPart
/// / StillHolds), теперь он один — <see cref="PartMount"/>. Здесь проверяются те
/// его свойства, которые в копиях были описаны комментариями.
/// </summary>
public class PartMountTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private ProjectLoadStateGuard? _guard;

    private const float ToU = AppConstants.MM_TO_UNITS;
    private const int TopThicknessMM = 38;

    [SetUp]
    public void SetUp() => _guard = ProjectLoadStateGuard.Capture();

    [TearDown]
    public void TearDown()
    {
        _guard?.Restore();
        foreach (var go in _spawned)
        {
            if (go == null) continue;
            var el = go.GetComponent<KitchenElement>();
            if (el != null) PartRegistry.Unregister(el);
            Object.DestroyImmediate(go);
        }
        _spawned.Clear();
    }

    private KitchenElement Countertop(string name = "Countertop")
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _spawned.Add(go);
        var el = go.AddComponent<KitchenElement>();
        el.PartName = name;
        el.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        el.DimensionsMM = new Vector3Int(1200, 650, TopThicknessMM);
        PartRegistry.Register(el);
        return el;
    }

    private SinkElement Sink(Vector3 position)
    {
        var go = new GameObject("Sink");
        _spawned.Add(go);
        go.transform.position = position;
        var sink = go.AddComponent<SinkElement>();
        sink.PartName = "Sink";
        PartRegistry.Register(sink);
        return sink;
    }

    private float TopSurfaceY(KitchenElement top) =>
        top.transform.position.y + TopThicknessMM * 0.5f * ToU;

    /// <summary>Именно этот случай стоял комментарием в обоих SnapToPart:
    /// после загрузки проекта имя хозяина уже восстановлено из сейва, но САМА
    /// деталь про врезку ещё не знает — проверка «имя совпало, значит всё в
    /// порядке» оставляла бы столешницу без проёма.</summary>
    [Test]
    public void Snap_NameRestoredFromSave_ButPartDoesNotKnowUs_RegistersTheCutoutAnyway()
    {
        var top = Countertop();
        var sink = Sink(new Vector3(0f, TopSurfaceY(top) + 0.05f, 0f));

        sink.AttachedPartName = top.PartName;
        Assert.IsFalse(top.HasCutout(sink), "предусловие: деталь врезку ещё не знает");

        sink.SnapToPart();

        Assert.IsTrue(top.HasCutout(sink),
            "имя из сейва совпало, но членства не было — регистрация обязана произойти, "
            + "иначе проём в столешнице не режется после загрузки проекта");
    }

    /// <summary>Пока техника прилипла, её позу диктует крепёж, а намерение
    /// пользователя копится в «свободной высоте». На отрыве она обязана
    /// ДОГНАТЬ курсор — иначе осталась бы на пласти и тут же прилипла снова.</summary>
    [Test]
    public void Release_JumpsToWhereTheUserHasBeenDragging_NotBackOntoTheSurface()
    {
        var top = Countertop();
        var sink = Sink(new Vector3(0f, TopSurfaceY(top) + 0.05f, 0f));
        sink.SnapToPart();
        Assert.IsTrue(sink.IsAttached, "предусловие: мойка села на столешницу");

        float pullMM = SinkElement.SNAP_RELEASE_MM * 3f;
        for (int step = 0; step < 3; step++)
        {
            sink.transform.position += Vector3.down * (pullMM / 3f * ToU);
            sink.SnapToPart();
        }

        Assert.IsFalse(sink.IsAttached, "протащили насквозь — мойка обязана отлипнуть");
        Assert.Less(sink.transform.position.y, TopSurfaceY(top) - SinkElement.SNAP_RELEASE_MM * ToU,
            "отлипнув, мойка догоняет курсор: иначе она осталась бы на пласти и прилипла снова");
    }

    [Test]
    public void Detach_RemovesTheCutoutFromThePart_AndForgetsTheName()
    {
        var top = Countertop();
        var sink = Sink(new Vector3(0f, TopSurfaceY(top) + 0.05f, 0f));
        sink.SnapToPart();
        Assert.IsTrue(top.HasCutout(sink));

        sink.UnregisterFromPart();

        Assert.IsFalse(top.HasCutout(sink), "деталь осталась бы с дырой от несуществующей мойки");
        Assert.AreEqual("", sink.AttachedPartName);
        Assert.IsFalse(sink.IsAttached);
    }

    /// <summary>Смещение хранится в ЛОКАЛЬНЫХ мм детали, поэтому переезд
    /// столешницы везёт технику с собой, а её собственный сдвиг превращается в
    /// новое смещение.</summary>
    [Test]
    public void Drift_OwnMove_BecomesAnOffsetInThePartsAxes()
    {
        var top = Countertop();
        var sink = Sink(new Vector3(0f, TopSurfaceY(top) + 0.05f, 0f));
        sink.SnapToPart();
        Assert.AreEqual(0, sink.OffsetXMM);

        sink.transform.position += new Vector3(0.2f, 0f, 0f);
        sink.SnapToPart();

        Assert.AreEqual(200, sink.OffsetXMM,
            "сдвиг пользователя на 200 мм вдоль детали — это 200 мм смещения, а не новая поза");
    }
}
