using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Реакция на прогон LevelMigrationRoundTripTests на fd3c7997: открытие
/// готового сохранения сдвигало Y у двух опор (Leg_9 — на самом деле колонна,
/// и Vintovaya_opora_2 — настоящая винтовая опора) на долю микрона. Причина —
/// не новый кэш валидации (A/B на cd79de23 дал тот же красный без него), а
/// SceneRestorer.RepairAutoSeatedJoints: он безусловно переписывает позицию
/// КАЖДОГО IAutoSeated при каждом открытии, и пересчитанное значение не
/// совпадает с сохранённым побитово — хотя физически это один и тот же стык.
///
/// Правка добавляет допуск: пересчитанная позиция принимается, только если она
/// разошлась с сохранённой больше чем на Tolerance.EpsilonUnits (0,1 мм) —
/// число уже используемое ApproxEqual, не новое. Пара тестов ниже — с
/// противоположными входами: стык, разошедшийся на 5 мм (настоящий дефект),
/// обязан починиться; стык, разошедшийся на долю допуска (то, что раньше
/// портил тест), обязан остаться ровно тем же значением, что лежало в файле.</summary>
public class SceneRestorerJointRepairTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void SetUp() => PartRegistry.Clear();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
    }

    private KitchenElement Board(string name, Vector3 pos, Vector3Int dims)
    {
        var go = new GameObject(name);
        _spawned.Add(go);
        go.transform.position = pos;
        var e = go.AddComponent<KitchenElement>();
        e.PartName = name;
        e.DimensionsMM = dims;
        PartRegistry.Register(e);
        return e;
    }

    private ScrewLegElement Leg(Vector3 pos)
    {
        var go = ElementFactory.CreateScrewLeg("Опора", pos);
        _spawned.Add(go);
        return go.GetComponent<ScrewLegElement>();
    }

    /// <summary>Хозяин стоит НАД опорой, под опорой ничего нет — floorY у
    /// ScrewLegAutoFit.FloorUnder детерминированно равен полу (0), и
    /// "правильная" посадка получается ровно той, какую даёт живой
    /// SeatAfterMove. Захватываем её через настоящий CaptureScene, а не
    /// вычисляем руками — так тест не зависит от точной формулы автоподгонки.</summary>
    private ProjectData CaptureSeatedLeg(out float naturalSeatY)
    {
        Board("Хозяин", new Vector3(0f, 0.3f, 0f), new Vector3Int(600, 18, 500));
        var leg = Leg(new Vector3(0f, 0.1f, 0f));
        leg.SeatAfterMove(PartRegistry.GetAll());

        naturalSeatY = leg.transform.position.y;
        var data = SaveLoadManager.CaptureScene(PartRegistry.GetAll());

        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();

        return data;
    }

    private static ElementData LegDataOf(ProjectData data) =>
        data.elements.First(e => e != null && e.isScrewLeg);

    [Test]
    public void ALegOffByFiveMillimetres_IsRepairedOnLoad()
    {
        var data = CaptureSeatedLeg(out float naturalSeatY);
        LegDataOf(data).position[1] = naturalSeatY + 5f * AppConstants.MM_TO_UNITS;

        SaveLoadManager.RestoreScene(data);

        var leg = PartRegistry.GetAll().OfType<ScrewLegElement>().First();
        Assert.AreEqual(naturalSeatY, leg.transform.position.y, 0f,
            "5 мм — это настоящий разошедшийся стык, а не шум пересчёта; открытие обязано " +
            "вправить опору на её законное место, допуск здесь ни при чём");
    }

    [Test]
    public void ACorrectlySeatedLeg_KeepsItsSavedPositionExactly()
    {
        var data = CaptureSeatedLeg(out float naturalSeatY);
        float savedY = naturalSeatY + 0.3f * Tolerance.EpsilonUnits;
        LegDataOf(data).position[1] = savedY;

        SaveLoadManager.RestoreScene(data);

        var leg = PartRegistry.GetAll().OfType<ScrewLegElement>().First();
        Assert.AreEqual(savedY, leg.transform.position.y, 0f,
            "расхождение внутри допуска — это уже посаженный стык; открытие не вправе " +
            "переписать его пересчитанным (и чуть другим по битам) значением. Ровно это " +
            "ловил LevelMigrationRoundTripTests на fd3c7997");
    }
}
