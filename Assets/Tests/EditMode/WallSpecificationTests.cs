using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Construction;

/// <summary>Стена в ведомости. До этого изменения стена шла ЛИСТОВОЙ ДЕТАЛЬЮ:
/// строка «площадь ЛДСП» на кладку 3 000 × 2 700 × 250 — цифра, которую нельзя
/// ни заказать, ни проверить. Теперь стена считает себя сама
/// (<see cref="IQuantifies"/>), и раздел «Стены» перестал быть пустым именем в
/// <see cref="SpecSections"/>.
///
/// Маршрут в ведомость обязан быть ОДИН: <see cref="SpecificationManager.Build"/>
/// берёт <c>IQuantifies</c> и делает <c>continue</c>, поэтому листовой маршрут у
/// стены мёртв — намеренно. Сторож
/// <c>SpecificationCoverageGuardTests</c> этого не видит: он спавнит типы из
/// <c>EveryElementType</c>, а стена — это <c>KitchenElement</c> с СОСЕДНИМ
/// компонентом <c>Wall</c>, которого у его пробы нет. Поэтому решение
/// зафиксировано здесь.</summary>
public class WallSpecificationTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    private KitchenElement MakeWall(string name = "Стена")
    {
        var go = ElementFactory.CreateWall(new Vector3Int(3000, 2700, 250), name, Vector3.zero);
        _spawned.Add(go);
        var el = go.GetComponent<KitchenElement>();
        PartRegistry.Register(el);
        return el;
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.DestroyImmediate(e.gameObject);
        PartRegistry.Clear();
        CommandStack.Clear();
        ElementFactory.ClearPools();
    }

    private static SpecResult Build(params KitchenElement[] elements) =>
        SpecificationManager.Build(elements);

    [Test]
    public void ABrickWall_ReachesTheSpecification_ThroughItsOwnCount()
    {
        var wall = MakeWall();
        wall.GetComponent<Wall>().Masonry = MasonryTechnology.BrickSingle;

        var lines = Build(wall).lines;

        Assert.IsNotEmpty(lines, "стена обязана дать хотя бы одну строку ведомости");
        Assert.IsTrue(lines.All(l => l.section == SpecSections.Walls),
            "все строки стены живут в разделе «Стены»: "
            + string.Join(", ", lines.Select(l => l.section + "/" + l.name)));

        var pieces = lines.First(l => l.unit == SpecUnit.Pieces);
        Assert.Greater(pieces.qtyTotal, 0f,
            "штук в стене строго больше нуля — иначе ведомость обещает стену из воздуха");
        Assert.IsTrue(lines.Any(l => l.unit == SpecUnit.VolumeM3 && l.name == WallSpecItems.MortarName),
            "и раствор отдельной строкой в м³");
    }

    [Test]
    public void AWall_TakesTheQuantifiesRoute_AndItsFlatBoardRouteIsDeadOnPurpose()
    {
        var wall = MakeWall();

        var declared = ElementSpecCoverage.Declared(
            wall.GetComponents<IQuantifies>().Length > 0,
            wall is ISpecificationParts,
            wall.IsFlatBoardElement);

        Assert.AreEqual(SpecRoute.Quantifies, ElementSpecCoverage.Taken(declared),
            "стена считает себя сама — этот маршрут выигрывает у листового");
        Assert.IsFalse(Build(wall).lines.Any(l => l.unit == SpecUnit.AreaM2),
            "строки «площадь ЛДСП» у стены больше нет: кладку не заказывают квадратами плиты");
    }

    [Test]
    public void ADoorway_ReducesTheStonesInTheWall()
    {
        var solid = MakeWall("Глухая");
        var solidPieces = Build(solid).lines.First(l => l.unit == SpecUnit.Pieces).qtyTotal;

        var withOpening = MakeWall("СПроёмом");
        var door = ElementFactory.CreateDoor(new Vector3Int(900, 2100, 100), "Дверь",
            withOpening.transform.position);
        _spawned.Add(door);
        withOpening.GetComponent<Wall>().RegisterDoor(door.GetComponent<DoorElement>());

        var openedPieces = Build(withOpening).lines.First(l => l.unit == SpecUnit.Pieces).qtyTotal;

        Assert.Less(openedPieces, solidPieces,
            "проём вынимает кладку: стена с дверью не может требовать столько же кирпича, "
            + "сколько глухая");
    }

    [Test]
    public void AWallAndABoard_KeepTheirOwnSections()
    {
        var wall = MakeWall();
        var board = ElementFactory.CreatePart(new Vector3Int(600, 400, 18), "Полка", Vector3.zero);
        _spawned.Add(board);
        var boardEl = board.GetComponent<KitchenElement>();
        PartRegistry.Register(boardEl);

        var sections = Build(wall, boardEl).lines.Select(l => l.section).Distinct().ToList();

        CollectionAssert.Contains(sections, SpecSections.Walls);
        Assert.Greater(sections.Count, 1,
            "деталь ЛДСП обязана остаться в своём разделе — стена не поглощает ведомость");
    }
}
