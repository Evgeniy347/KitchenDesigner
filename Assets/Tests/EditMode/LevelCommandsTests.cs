using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// L3 (план LEVELS): MoveToLevelCommand, CreateLevelCommand, DeleteLevelCommand — один шаг
/// отмены каждая, и отмена обязана вернуть проект БАЙТ В БАЙТ (JSON после Execute+Undo
/// равен JSON до Execute). Переключение текущего этажа для просмотра — состояние вида,
/// а не команда: у него намеренно нет своего класса здесь (docs/todo_evolution.md §3.4).
/// </summary>
public class LevelCommandsTests
{
    private ProjectLoadStateGuard? _guard;

    [SetUp]
    public void SetUp()
    {
        _guard = ProjectLoadStateGuard.Capture();
        CommandStack.Clear();
        LevelRegistry.Reset();
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

    private static string SnapshotJson() =>
        SaveLoadManager.Serialize(SaveLoadManager.CaptureScene(PartRegistry.GetAll()));

    [Test]
    public void MoveToLevelCommand_MovesOnExecute_AndUndoRestoresTheProjectByteEqual()
    {
        var go = ElementFactory.CreatePart(new Vector3Int(600, 18, 500), "Board", Vector3.zero);
        var element = go.GetComponent<KitchenElement>();
        element.LevelId = "1";
        PartRegistry.Register(element);

        var before = SnapshotJson();

        CommandStack.Execute(new MoveToLevelCommand(new[] { element }, "2"));
        Assert.AreEqual("2", element.LevelId, "Execute обязан перевести деталь на новый уровень");

        CommandStack.Undo();
        Assert.AreEqual("1", element.LevelId, "Undo обязан вернуть деталь на прежний уровень");

        Assert.AreEqual(before, SnapshotJson(),
            "отмена перевода на уровень обязана вернуть проект байт в байт");
    }

    /// <summary>L8 (review-ui-mcp): AboveTop() читал LevelRegistry.Items напрямую вместо
    /// Snapshot() — на пустом реестре Items пуст, а Snapshot() отдаёт виртуальный первый
    /// уровень. LevelPlacement.NextAbove(пустой массив, ...) расходился с тем, что видит
    /// остальной код (LevelsWindowUI.AddLevel уже читает Snapshot()), и на пустом реестре
    /// отдал бы уровень «1» на 0 мм вместо «2» поверх виртуального первого.</summary>
    [Test]
    public void CreateLevelCommand_AboveTop_OnAnEmptyRegistry_AgreesWithSnapshot_NotWithEmptyItems()
    {
        Assert.IsEmpty(LevelRegistry.Items, "предусловие: реестр пуст");

        var cmd = CreateLevelCommand.AboveTop();
        CommandStack.Execute(cmd);

        Assert.AreEqual(1, LevelRegistry.Items.Count);
        Assert.AreEqual("2", LevelRegistry.Items[0].id,
            "обязан встать НАД виртуальным первым уровнем (Snapshot), а не занять id «1» " +
            "на 0 мм, как если бы Items считался пустой сценой без уровней вовсе");
    }

    [Test]
    public void CreateLevelCommand_AddsOneLevelAboveTheTop_AndUndoRestoresTheProjectByteEqual()
    {
        LevelRegistry.Set(new[] { new Level("1", "1 этаж", 0, 3000) });
        var before = SnapshotJson();

        var cmd = CreateLevelCommand.AboveTop();
        CommandStack.Execute(cmd);

        Assert.AreEqual(2, LevelRegistry.Items.Count);
        Assert.AreEqual(3000, LevelRegistry.Items[1].floorElevationMm,
            "новый уровень обязан встать на отметку верхнего плюс его высоту");

        CommandStack.Undo();

        Assert.AreEqual(1, LevelRegistry.Items.Count);
        Assert.AreEqual(before, SnapshotJson(),
            "отмена создания уровня обязана вернуть проект байт в байт");
    }

    [Test]
    public void DeleteLevelCommand_RemovesOneLevel_AndUndoRestoresItAtTheSameIndex_ByteEqual()
    {
        LevelRegistry.Set(new[]
        {
            new Level("1", "1 этаж", 0, 3000),
            new Level("2", "2 этаж", 3000, 3000),
            new Level("3", "3 этаж", 6000, 3000),
        });
        var before = SnapshotJson();

        var cmd = new DeleteLevelCommand("2");
        CommandStack.Execute(cmd);

        Assert.AreEqual(2, LevelRegistry.Items.Count);
        Assert.AreEqual(-1, LevelRegistry.IndexOf("2"), "удалённый уровень обязан пропасть из реестра");

        CommandStack.Undo();

        Assert.AreEqual(3, LevelRegistry.Items.Count);
        Assert.AreEqual(1, LevelRegistry.IndexOf("2"),
            "отмена удаления обязана вернуть уровень НА ТО ЖЕ МЕСТО, а не в конец списка");
        Assert.AreEqual(before, SnapshotJson(),
            "отмена удаления уровня обязана вернуть проект байт в байт");
    }

    [Test]
    public void DeleteLevelCommand_UnknownId_ThrowsImmediately_RatherThanCorruptingUndoLater()
    {
        LevelRegistry.Set(new[] { new Level("1", "1 этаж", 0, 3000) });

        Assert.Throws<System.ArgumentException>(() => new DeleteLevelCommand("does-not-exist"));
    }

    /// <summary>H7/addendum#2 (review-persistence, review-ui-mcp): удаление уровня, на
    /// котором ещё стоят детали, раньше оставляло их с «висячим» LevelId — деталь молча
    /// съезжала на LevelResolution.ResolveElementLevel(...).id == effectiveLevels[0], то есть
    /// на первый уровень, на реальной высоте бывшего второго этажа. Теперь удаление уровня
    /// уносит с собой и его детали, одним шагом отмены.</summary>
    [Test]
    public void DeleteLevelCommand_WithElementsOnIt_RemovesThemToo_AndUndoRestoresEverythingByteEqual()
    {
        LevelRegistry.Set(new[]
        {
            new Level("1", "1 этаж", 0, 3000),
            new Level("2", "2 этаж", 3000, 3000),
        });
        var go = ElementFactory.CreatePart(new Vector3Int(600, 18, 500), "Board", Vector3.zero);
        var element = go.GetComponent<KitchenElement>();
        element.LevelId = "2";
        PartRegistry.Register(element);
        var before = SnapshotJson();

        CommandStack.Execute(new DeleteLevelCommand("2"));

        Assert.AreEqual(-1, LevelRegistry.IndexOf("2"), "уровень удалён");
        Assert.IsFalse(element.gameObject.activeSelf,
            "деталь удалённого уровня обязана уйти вместе с ним, а не остаться с висячим LevelId");

        CommandStack.Undo();

        Assert.AreEqual(1, LevelRegistry.IndexOf("2"), "уровень вернулся на своё место");
        Assert.IsTrue(element.gameObject.activeSelf, "деталь вернулась вместе с уровнем");
        Assert.AreEqual("2", element.LevelId);
        Assert.AreEqual(before, SnapshotJson(),
            "отмена удаления уровня с деталями обязана вернуть проект байт в байт");
    }
}
