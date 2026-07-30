using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Зазоры как ОБЩЕЕ свойство детали.
///
/// Раньше они жили в двух подклассах (фасад и ДВП/ХДФ) и вместе с ними —
/// четыре одинаковых переопределения геометрии. Теперь механика одна на всех,
/// и эти тесты сторожат две вещи: у кого зазоры вообще есть и что деталь с
/// нулевыми зазорами геометрически не изменилась.</summary>
public class KitchenElementGapsTests
{
    private const float U = AppConstants.MM_TO_UNITS;
    private readonly System.Collections.Generic.List<GameObject> _spawned = new();

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
    }

    private T Make<T>(string name, Vector3Int dims) where T : KitchenElement
    {
        var go = new GameObject(name);
        _spawned.Add(go);
        var el = go.AddComponent<T>();
        el.PartName = name;
        el.DimensionsMM = dims;
        el.ApplyDimensions();
        return el;
    }

    [Test]
    public void Board_SupportsGaps_AndStartsAtZero()
    {
        var board = Make<KitchenElement>("B", new Vector3Int(800, 400, 18));

        Assert.IsTrue(board.SupportsGaps);
        Assert.AreEqual(0, board.GapMM, "у детали зазоров по умолчанию нет");
    }

    [Test]
    public void RadialShelf_SupportsGaps()
    {
        var shelf = Make<RadialShelfElement>("R", new Vector3Int(400, 18, 400));

        Assert.IsTrue(shelf.SupportsGaps);
        Assert.AreEqual(0, shelf.GapMM);
    }

    [Test]
    public void Facade_And_Panel_SupportGaps()
    {
        Assert.IsTrue(Make<FacadeElement>("F", new Vector3Int(400, 700, 18)).SupportsGaps);
        Assert.IsTrue(Make<PanelElement>("P", new Vector3Int(400, 700, 4)).SupportsGaps);
    }

    /// <summary>Стена — тоже KitchenElement, но деталью не является: её габарит
    /// и есть конструкция, отступать ему не от чего.</summary>
    [Test]
    public void Wall_HasNoGaps()
    {
        var wall = Make<KitchenElement>("W", new Vector3Int(3000, 2700, 100));
        wall.gameObject.AddComponent<Wall>();

        Assert.IsFalse(wall.SupportsGaps);
    }

    /// <summary>Деталь без зазоров даёт РОВНО тот же бокс, что и до появления
    /// общего механизма: иначе поехала бы вся сцена.</summary>
    [Test]
    public void ZeroGaps_KeepTheBoxUnchanged()
    {
        var board = Make<KitchenElement>("B", new Vector3Int(800, 400, 18));
        board.transform.position = new Vector3(1f, 2f, 3f);

        var faces = board.GetFaces();

        Assert.AreEqual(800f * U * 0.5f + 1f, faces[0].center.x, 1e-5f, "правая грань");
        Assert.AreEqual(-800f * U * 0.5f + 1f, faces[1].center.x, 1e-5f, "левая грань");
        Assert.AreEqual(18f * U * 0.5f + 3f, faces[4].center.z, 1e-5f, "передняя грань");
    }

    /// <summary>Зазор двигает СВОЮ грань и не трогает противоположную —
    /// асимметрия и есть весь смысл покомпонентных зазоров.</summary>
    [Test]
    public void GapMovesOnlyItsOwnFace()
    {
        var board = Make<KitchenElement>("B", new Vector3Int(800, 400, 18));
        float rightBefore = board.GetFaces()[0].center.x;
        float leftBefore = board.GetFaces()[1].center.x;

        board.GapRight = 10;

        Assert.AreEqual(rightBefore + 10f * U, board.GetFaces()[0].center.x, 1e-5f,
            "правая грань ушла на зазор");
        Assert.AreEqual(leftBefore, board.GetFaces()[1].center.x, 1e-5f,
            "левая грань осталась на месте");
    }

    /// <summary>Зазор спереди/сзади работает по толщине — ради него всё и
    /// затевалось. Проверяем на детали, а не на фасаде: у обычной доски
    /// толщина по локальной Z.</summary>
    [Test]
    public void FrontGap_GrowsTheBoxInThickness()
    {
        var board = Make<KitchenElement>("B", new Vector3Int(800, 400, 18));
        float frontBefore = board.GetFaces()[4].center.z;
        float backBefore = board.GetFaces()[5].center.z;

        board.GapFront = 5;

        Assert.AreEqual(frontBefore + 5f * U, board.GetFaces()[4].center.z, 1e-5f);
        Assert.AreEqual(backBefore, board.GetFaces()[5].center.z, 1e-5f);
    }

    /// <summary>Радиусная полка строит меш в мировых единицах и держит
    /// localScale единичным: зазор обязан считаться от РАЗМЕРОВ, а не от
    /// масштаба, иначе он просто ничего не сдвинет.</summary>
    [Test]
    public void RadialShelf_GapCountsFromDimensions()
    {
        var shelf = Make<RadialShelfElement>("R", new Vector3Int(400, 18, 400));
        float rightBefore = shelf.GetFaces()[0].center.x;

        shelf.GapRight = 10;

        Assert.AreEqual(rightBefore + 10f * U, shelf.GetFaces()[0].center.x, 1e-5f);
    }

    [Test]
    public void Panel_SetUniformGap_CoversAllSixSides()
    {
        var panel = Make<PanelElement>("P", new Vector3Int(400, 700, 4));

        panel.SetUniformGap(PanelElement.DEFAULT_GAP_MM);

        foreach (var side in GapSides.All)
            Assert.AreEqual(PanelElement.DEFAULT_GAP_MM, panel.GapOf(side), $"сторона {side}");
    }
}
