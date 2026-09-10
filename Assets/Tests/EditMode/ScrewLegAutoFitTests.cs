using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Винтовая опора после перетаскивания: найти деталь над собой, взять её
/// в хозяева и дорастить резьбу так, чтобы пятка встала на пол.
///
/// Тест ходит той же дорогой, что и ElementMover — через SeatAfterMove, — а не
/// повторяет арифметику руками: иначе он был бы зелёным против кода, который в
/// сцене не вызывается вовсе.</summary>
public class ScrewLegAutoFitTests
{
    private const float U = AppConstants.MM_TO_UNITS;

    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void SetUp() => PartRegistry.Clear();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
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
        e.ApplyDimensions();
        return e;
    }

    private ScrewLegElement Leg(Vector3 pos)
    {
        var go = ElementFactory.CreateScrewLeg("Опора1", pos);
        _spawned.Add(go);
        return go.GetComponent<ScrewLegElement>();
    }

    private static List<KitchenElement> Scene(params KitchenElement[] elements)
        => new List<KitchenElement>(elements);

    private static float BottomOf(KitchenElement e) => ElementAabb.Of(e).minY;

    private static float TopOf(KitchenElement e) => ElementAabb.Of(e).maxY;

    /// <summary>Дно детали, под которую вешают опору: 150 мм над полом.</summary>
    private KitchenElement BottomPanelAt(float bottomY) =>
        Board("Дно", new Vector3(0f, bottomY + 9f * U, 0f), new Vector3Int(600, 18, 500));

    [Test]
    public void Seat_GrowsTheThreadUntilTheFootReachesTheFloor()
    {
        var host = BottomPanelAt(0.150f);
        var leg = Leg(new Vector3(0f, 0.100f, 0f));

        leg.SeatAfterMove(Scene(host, leg));

        Assert.AreEqual(150, leg.HeightAboveFloorMM,
            "высота над полом = резьба − заход + пятка, и тянется она до пола сама");
        Assert.AreEqual(0f, BottomOf(leg), 1e-4f, "пятка стоит на полу");
    }

    [Test]
    public void Seat_PutsTheThreadTipInsideTheHost_ByTheInsertionDepth()
    {
        var host = BottomPanelAt(0.150f);
        var leg = Leg(new Vector3(0f, 0.100f, 0f));

        leg.SeatAfterMove(Scene(host, leg));

        Assert.AreEqual(BottomOf(host) + leg.InsertionDepthMM * U, TopOf(leg), 1e-4f,
            "конец резьбы заходит в деталь ровно на глубину захода — 25 мм по умолчанию");
    }

    [Test]
    public void Seat_TakesTheHostAsItsAttachParent()
    {
        var host = BottomPanelAt(0.150f);
        var leg = Leg(new Vector3(0f, 0.100f, 0f));

        leg.SeatAfterMove(Scene(host, leg));

        Assert.AreEqual(host.PartName, leg.HostPartName,
            "опора запоминает хозяина — на этой связке держатся и переезд за деталью, "
            + "и разрешение делить с ней объём");
    }

    [Test]
    public void Seat_StandsOnThePodiumUnderIt_NotOnTheWorldFloor()
    {
        var host = BottomPanelAt(0.400f);
        var podium = Board("Цоколь", new Vector3(0f, 0.050f, 0f), new Vector3Int(600, 100, 500));
        var leg = Leg(new Vector3(0f, 0.300f, 0f));

        leg.SeatAfterMove(Scene(host, podium, leg));

        Assert.AreEqual(TopOf(podium), BottomOf(leg), 1e-4f,
            "пол — ближайшая поверхность под опорой, а не мировой ноль");
        Assert.AreEqual(300, leg.HeightAboveFloorMM);
    }

    [Test]
    public void Seat_ReactsToTheHostMovingUp_TheHeightIsNotFrozen()
    {
        var host = BottomPanelAt(0.150f);
        var leg = Leg(new Vector3(0f, 0.100f, 0f));
        leg.SeatAfterMove(Scene(host, leg));

        host.transform.position += new Vector3(0f, 0.050f, 0f);
        leg.SeatAfterMove(Scene(host, leg));

        Assert.AreEqual(200, leg.HeightAboveFloorMM,
            "деталь уехала на 50 мм вверх — опора обязана дотянуться, а не остаться висеть");
        Assert.AreEqual(0f, BottomOf(leg), 1e-4f);
    }

    [Test]
    public void Seat_LeavesTheLegAloneWhenThereIsNothingAboveIt()
    {
        var leg = Leg(new Vector3(0f, 0.100f, 0f));
        int threadBefore = leg.ThreadLengthMM;
        var posBefore = leg.transform.position;

        leg.SeatAfterMove(Scene(leg));

        Assert.AreEqual(threadBefore, leg.ThreadLengthMM,
            "без хозяина тянуться некуда: опора остаётся как есть, а не схлопывается");
        Assert.AreEqual(posBefore, leg.transform.position);
    }

    [Test]
    public void Seat_IgnoresTheFloorAsAHost_ALegIsNotScrewedIntoTheFloor()
    {
        var floorGo = new GameObject("Пол");
        _spawned.Add(floorGo);
        floorGo.transform.position = new Vector3(0f, 0.200f, 0f);
        var floor = floorGo.AddComponent<FloorElement>();
        floor.PartName = "Пол";
        floor.DimensionsMM = new Vector3Int(3000, 18, 3000);
        floor.ApplyDimensions();

        var leg = Leg(new Vector3(0f, 0.100f, 0f));
        leg.SeatAfterMove(Scene(floor, leg));

        Assert.IsNull(leg.HostPartName, "пол опоре не хозяин");
    }

    [Test]
    public void ThreadThroughASixteenMillimetreEnd_IsNotACollisionWithTheHost()
    {
        var host = Board("Бок", new Vector3(0f, 0.400f, 0f), new Vector3Int(16, 700, 500));
        var leg = Leg(new Vector3(0f, 0.020f, 0f));
        leg.SeatAfterMove(Scene(host, leg));

        var result = ConstraintValidator.Validate(new List<KitchenElement> { host, leg });

        Assert.IsFalse(result.violations.Contains(leg),
            "резьба 25 мм в торце 16 мм торчит с другой стороны на 9 мм — "
            + "это конструктив, а не пересечение");
        Assert.IsFalse(result.violations.Contains(host));
    }

    [Test]
    public void TheProtrudingThread_StillCollidesWithSomethingThatIsNotTheHost()
    {
        var host = Board("Бок", new Vector3(0f, 0.400f, 0f), new Vector3Int(16, 700, 500));
        var leg = Leg(new Vector3(0f, 0.020f, 0f));
        leg.SeatAfterMove(Scene(host, leg));

        float tipY = TopOf(leg);
        var shelf = Board("Полка", new Vector3(0f, tipY - 0.002f, 0.100f),
            new Vector3Int(600, 18, 300));

        var result = ConstraintValidator.Validate(new List<KitchenElement> { host, shelf, leg });

        Assert.IsTrue(result.violations.Contains(leg),
            "хозяину объём отдан, а всем остальным — нет: вылезшая резьба, попавшая "
            + "в чужую деталь, обязана остаться ошибкой");
    }

    [Test]
    public void RepairAfterGridSnap_SeatsTheLeg_ButNeverRecomputesItsHeight()
    {
        var host = BottomPanelAt(0.150f);
        var leg = Leg(new Vector3(0f, 0.100f, 0f));
        leg.ThreadLengthMM = 20;
        int threadByHand = leg.ThreadLengthMM;

        leg.RepairJointAfterGridSnap(Scene(host, leg));

        Assert.AreEqual(threadByHand, leg.ThreadLengthMM,
            "проход после загрузки чинит СТЫК, а не размер: пересчёт резьбы здесь "
            + "переписал бы сохранённое значение молча и без отмены");
        Assert.AreEqual(0f, BottomOf(leg), 1e-4f, "посадка на пол — это всё ещё его работа");
    }
}
