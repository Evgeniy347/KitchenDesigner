using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Напольная сантехника садится низом на то, что под ней, и делает это
/// ОДНОЙ способностью на всех, а не веткой по типу.
///
/// Ванну и унитаз объединяет не класс, а поведение: габарит у них не меняется,
/// меняется только высота. Поэтому <c>IAutoSeated</c> им не подходит — он про
/// элемент, который подгоняет СВОЙ РАЗМЕР под зазор (колонна, винтовая опора), и
/// ElementMover дописывает за ним ResizeCommand. Способность называется
/// <c>IStandsOnFloor</c>, тело у неё одно на всех — <c>FloorSeating</c>, — и
/// завтрашнему поддону достаточно объявить интерфейс.
///
/// Отмена достаётся даром и потому не проверяется отдельно: ElementMover зовёт
/// посадку ДО того, как строит MoveCommand, поэтому команда запоминает уже
/// севшую позицию, а габарит посадка не трогает — дописывать ResizeCommand,
/// как для IAutoSeated, здесь нечего.
///
/// Тесты ходят той же дорогой, что и ElementMover: через <c>SeatOnFloor</c>.
/// Арифметика проверена отдельно и без сцены — FloorDropTests; здесь только то,
/// что без сцены не проверить: что элемент реально сдвинулся и что способность
/// висит на правильных типах.</summary>
public class FloorSeatingTests
{
    private const float U = AppConstants.MM_TO_UNITS;

    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void SetUp() => PartRegistry.Clear();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned) if (go != null) UnityEngine.Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
        ElementFactory.ClearPools();
    }

    private KitchenElement Slab(string name, Vector3 centre, Vector3Int dims)
    {
        var go = new GameObject(name);
        _spawned.Add(go);
        go.transform.position = centre;
        var e = go.AddComponent<KitchenElement>();
        e.PartName = name;
        e.DimensionsMM = dims;
        e.ApplyDimensions();
        return e;
    }

    private KitchenElement FloorAt(float topY) =>
        Slab("Пол", new Vector3(0f, topY - 10f * U, 0f), new Vector3Int(6000, 20, 6000));

    private BathtubElement Bathtub(Vector3 position)
    {
        var go = ElementFactory.CreateBathtub(BathtubLayout.DefaultDimensionsMM,
            BathtubLayout.DefaultRimWidthMM, BathtubLayout.DefaultBowlDepthMM,
            BathtubLayout.DefaultBowlRadiusMM, BathtubLayout.DefaultBowlFilletMM,
            "Ванна", position);
        _spawned.Add(go);
        return go.GetComponent<BathtubElement>();
    }

    private ToiletElement Toilet(Vector3 position)
    {
        var go = ElementFactory.CreateToilet(ToiletElement.DefaultSeatHeightMM, "Унитаз", position);
        _spawned.Add(go);
        return go.GetComponent<ToiletElement>();
    }

    private static List<KitchenElement> Scene(params KitchenElement[] elements)
        => new List<KitchenElement>(elements);

    private static float BottomOf(KitchenElement e) => ElementAabb.Of(e).minY;

    [Test]
    public void ABathtubDroppedInTheAir_LandsOnTheFloor()
    {
        var floor = FloorAt(0f);
        var tub = Bathtub(new Vector3(0f, 1.2f, 0f));

        tub.SeatOnFloor(Scene(floor, tub));

        Assert.AreEqual(0f, BottomOf(tub), Tolerance.EpsilonUnits,
            "ванна — напольная: после перетаскивания её низ обязан лежать на полу, "
            + "а не там, где отпустили мышь");
    }

    [Test]
    public void ABathtubOnAPodium_StaysOnThePodium()
    {
        var floor = FloorAt(0f);
        var podium = Slab("Подиум", new Vector3(0f, 0.075f, 0f), new Vector3Int(2000, 150, 900));
        var tub = Bathtub(new Vector3(0f, 1.2f, 0f));

        tub.SeatOnFloor(Scene(floor, podium, tub));

        Assert.AreEqual(0.150f, BottomOf(tub), Tolerance.EpsilonUnits,
            "ищется САМАЯ ВЫСОКАЯ опора под пятном, а не пол мира: «всегда прибивать "
            + "к нулю» уничтожало бы ванну на подиуме при каждом перетаскивании");
    }

    [Test]
    public void ABathtubWithNothingUnderIt_IsLeftWhereItWasDropped()
    {
        var tub = Bathtub(new Vector3(0f, 1.2f, 0f));
        float before = tub.transform.position.y;

        tub.SeatOnFloor(Scene(tub));

        Assert.AreEqual(before, tub.transform.position.y, Tolerance.EpsilonUnits,
            "без пола сажать не на что: посадка не имеет права выдумывать нулевую "
            + "отметку в сцене, где пола ещё нет");
    }

    [Test]
    public void ABathtubAlreadyStanding_IsNotTouchedAtAll()
    {
        var floor = FloorAt(0f);
        var tub = Bathtub(new Vector3(0f, BathtubLayout.DefaultHeightMM * 0.5f * U, 0f));
        var before = tub.transform.position;

        tub.SeatOnFloor(Scene(floor, tub));

        Assert.AreEqual(before, tub.transform.position,
            "стоящий элемент посадка не двигает — именно поэтому она не спорит с "
            + "привязкой: SnapSystem, положившая элемент на горизонтальную грань, "
            + "оставляет зазор ниже допуска контакта, и двигать оказывается нечего");
    }

    [Test]
    public void SeatingOnlyChangesHeight_SoASideSnapSurvivesIt()
    {
        var floor = FloorAt(0f);
        var tub = Bathtub(new Vector3(0.37f, 1.2f, -0.44f));

        tub.SeatOnFloor(Scene(floor, tub));

        Assert.AreEqual(0.37f, tub.transform.position.x, Tolerance.EpsilonUnits,
            "посадка ходит только по Y: плановое положение выставила привязка, и "
            + "отобрать его у неё посадка не может");
        Assert.AreEqual(-0.44f, tub.transform.position.z, Tolerance.EpsilonUnits,
            "то же по Z");
    }

    [Test]
    public void AToiletDroppedInTheAir_LandsOnTheFloor()
    {
        var floor = FloorAt(0f);
        var toilet = Toilet(new Vector3(0f, 1.2f, 0f));

        toilet.SeatOnFloor(Scene(floor, toilet));

        Assert.AreEqual(0f, BottomOf(toilet), Tolerance.EpsilonUnits,
            "напольный унитаз тоже стоит на полу, и той же способностью — своя копия "
            + "арифметики у каждого типа и есть то, что здесь убрано");
    }

    private static IEnumerable<Type> ElementTypes() =>
        typeof(KitchenElement).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(KitchenElement).IsAssignableFrom(t));

    [Test]
    public void NoElement_IsBothAutoSeatedAndFloorStanding()
    {
        var both = ElementTypes()
            .Where(t => typeof(IAutoSeated).IsAssignableFrom(t)
                        && typeof(IStandsOnFloor).IsAssignableFrom(t))
            .Select(t => t.Name)
            .ToList();

        Assert.IsEmpty(both,
            "две способности спорят: ElementMover сначала зовёт SeatAfterMove, которая "
            + "меняет габарит и порождает ResizeCommand, а потом SeatOnFloor, которая "
            + "двигает элемент. Тип обязан выбрать одну: подгоняю СЕБЯ под зазор или "
            + "просто стою на опоре. Лишние: " + string.Join(", ", both));
    }

    [Test]
    public void EveryFloorStandingType_SeatsThroughTheSharedHelper()
    {
        var standing = ElementTypes()
            .Where(t => typeof(IStandsOnFloor).IsAssignableFrom(t))
            .ToList();
        var names = standing.Select(t => t.Name).ToList();

        CollectionAssert.Contains(names, nameof(BathtubElement),
            "ванна обязана нести способность: без неё она не садится на пол");
        CollectionAssert.Contains(names, nameof(ToiletElement),
            "напольный унитаз — тоже");

        foreach (var type in standing)
        {
            var method = type.GetMethod(nameof(IStandsOnFloor.SeatOnFloor),
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(method, type.Name + " обязан реализовать SeatOnFloor");
            Assert.AreEqual(type, method!.DeclaringType,
                type.Name + " обязан объявить SeatOnFloor у себя, делегируя в FloorSeating: "
                + "своя арифметика посадки у каждого типа — это ровно тот долг, который "
                + "здесь закрыт");
        }
    }
}
