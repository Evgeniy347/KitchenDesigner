using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>
/// L3 (план LEVELS): «+» над верхним уровнем — отметка нового уровня равна отметке
/// верхнего плюс его высота (docs/todo_evolution.md §3.4, строка «Новый этаж»).
/// </summary>
public class LevelPlacementTests
{
    [Test]
    public void NextAbove_NoLevels_StartsAtZero_WithGivenHeight()
    {
        var next = LevelPlacement.NextAbove(new Level[0], 3000);

        Assert.AreEqual(0, next.floorElevationMm);
        Assert.AreEqual(3000, next.heightMm);
        Assert.AreEqual("1", next.id);
    }

    [Test]
    public void NextAbove_OneLevel_SitsOnTopOfIt_ElevationPlusHeight()
    {
        var levels = new[] { new Level("1", "1 этаж", 0, 3000) };

        var next = LevelPlacement.NextAbove(levels, 2800);

        Assert.AreEqual(3000, next.floorElevationMm, "верх нового = отметка нижнего + его высота");
        Assert.AreEqual(2800, next.heightMm, "высота нового берётся из настроек, а не из соседа");
    }

    [Test]
    public void NextAbove_PicksTheHighestExistingLevel_NotTheLastInTheArray()
    {
        var levels = new[]
        {
            new Level("2", "2 этаж", 3000, 3000),
            new Level("1", "1 этаж", 0, 3000),
        };

        var next = LevelPlacement.NextAbove(levels, 3000);

        Assert.AreEqual(6000, next.floorElevationMm,
            "функция обязана искать МАКСИМАЛЬНУЮ отметку, а не брать последний элемент массива");
    }

    [Test]
    public void NextAbove_Id_IsOneMoreThanTheHighestNumericId()
    {
        var levels = new[] { new Level("1", "1 этаж", 0, 3000), new Level("5", "5 этаж", 12000, 3000) };

        var next = LevelPlacement.NextAbove(levels, 3000);

        Assert.AreEqual("6", next.id, "id обязан оставаться уникальным даже если один уровень уже удаляли");
    }

    [Test]
    public void NextAbove_NeverMutatesTheInputArray()
    {
        var levels = new[] { new Level("1", "1 этаж", 0, 3000) };
        var originalElevation = levels[0].floorElevationMm;

        LevelPlacement.NextAbove(levels, 3000);

        Assert.AreEqual(originalElevation, levels[0].floorElevationMm);
        Assert.AreEqual(1, levels.Length);
    }
}
