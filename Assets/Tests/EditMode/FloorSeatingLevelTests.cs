using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// L4 (план LEVELS): посадка на пол обязана знать про этажи. «Пол» для элемента без
/// опоры под собой — это отметка его СОБСТВЕННОГО уровня (<see cref="LevelRegistry.LevelOf"/>),
/// а не мировой ноль; и опора считается только среди деталей ТОГО ЖЕ уровня — деталь
/// на другом этаже, даже прямо под пятном по X/Z, не в счёт.
/// </summary>
public class FloorSeatingLevelTests
{
    private ProjectLoadStateGuard? _guard;

    [SetUp]
    public void SetUp()
    {
        _guard = ProjectLoadStateGuard.Capture();
        LevelRegistry.Set(new[]
        {
            new Level("1", "1 этаж", 0, 3000),
            new Level("2", "2 этаж", 3000, 3000),
        });
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var e in Object.FindObjectsByType<KitchenElement>(FindObjectsSortMode.None))
            if (e != null) Object.DestroyImmediate(e.gameObject);
        PartRegistry.Clear();
        CommandStack.Clear();
        ElementFactory.ClearPools();
        MaterialManager.ClearCache();
        LevelRegistry.Reset();
        _guard?.Restore();
        _guard = null;
    }

    private static float BottomOf(KitchenElement element) => ElementAabb.Of(element).minY;

    private static ToiletElement MakeToilet(string levelId, float bottomY)
    {
        var go = ElementFactory.CreateToilet(ToiletElement.DefaultSeatHeightMM, "Toilet_L4", Vector3.zero);
        var toilet = go.GetComponent<ToiletElement>();
        toilet.LevelId = levelId;
        var half = AppConstants.HalfHeightUnits(toilet.DimensionsMM.y);
        toilet.transform.position = new Vector3(0f, bottomY + half, 0f);
        PartRegistry.Register(toilet);
        return toilet;
    }

    private static KitchenElement MakeBoardWithTop(string levelId, float topY, float heightUnits)
    {
        var dimsMm = new Vector3Int(600, Mathf.RoundToInt(heightUnits / AppConstants.MM_TO_UNITS), 600);
        var go = ElementFactory.CreatePart(dimsMm, "Cabinet_L4", Vector3.zero);
        var element = go.GetComponent<KitchenElement>();
        element.LevelId = levelId;
        element.transform.position = new Vector3(0f, topY - heightUnits * 0.5f, 0f);
        PartRegistry.Register(element);
        return element;
    }

    [Test]
    public void ElementOnLevelAt3000_WithNothingUnderIt_SitsAt3000()
    {
        var toilet = MakeToilet("2", bottomY: 3.1f);

        toilet.SeatOnFloor(PartRegistry.GetAll());

        Assert.AreEqual(3f, BottomOf(toilet), Tolerance.EpsilonUnits,
            "без опоры под собой деталь второго этажа обязана сесть на ЕГО пол (3000 мм), "
            + "а не провалиться в мировой ноль первого этажа и не остаться висеть");
    }

    [Test]
    public void ElementOnLevelAt0_BehavesAsBefore_SitsAt0()
    {
        var toilet = MakeToilet("1", bottomY: 0.1f);

        toilet.SeatOnFloor(PartRegistry.GetAll());

        Assert.AreEqual(0f, BottomOf(toilet), Tolerance.EpsilonUnits,
            "первый этаж на отметке 0 — поведение обязано остаться прежним");
    }

    [Test]
    public void ACabinetOnLevel1_IsNotASupportForLevel2_EvenWhenGeometricallyRightUnderIt()
    {
        var cabinet = MakeBoardWithTop("1", topY: 2.95f, heightUnits: 0.1f);
        var toilet = MakeToilet("2", bottomY: 3.1f);

        toilet.SeatOnFloor(new KitchenElement[] { cabinet, toilet });

        Assert.AreEqual(3f, BottomOf(toilet), Tolerance.EpsilonUnits,
            "шкаф с первого этажа (верх на 2,95 м) стоит прямо под пятном по X/Z и был бы "
            + "ближайшей опорой без фильтра по уровню — но он на ДРУГОМ этаже, значит не "
            + "в счёт, и деталь второго этажа обязана сесть на СВОЙ пол (3 м), а не на "
            + "верх чужого шкафа");
    }

    [Test]
    public void ASupportOnTheSameLevel_IsStillUsed()
    {
        var cabinet = MakeBoardWithTop("2", topY: 3.2f, heightUnits: 0.1f);
        var toilet = MakeToilet("2", bottomY: 3.5f);

        toilet.SeatOnFloor(new KitchenElement[] { cabinet, toilet });

        Assert.AreEqual(3.2f, BottomOf(toilet), Tolerance.EpsilonUnits,
            "опора на ТОМ ЖЕ уровне обязана по-прежнему работать — фильтр не выключает "
            + "посадку целиком, а только чужие этажи");
    }
}
