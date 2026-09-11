using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Ctrl+D на группе. Три вещи, которых по одной детали не видно: копий столько же,
/// сколько источников; вектор смещения ОДИН на всю группу (иначе взаимное
/// расположение копий поехало бы относительно оригиналов); и в стек ложится ОДНА
/// запись, а не запись на объект — иначе Ctrl+Z убирал бы копии по одной.
/// Сам вектор считает DuplicateOffset.ForViewDirection, здесь он приходит
/// параметром: копирование группы не имеет права знать, откуда смотрит камера.
/// </summary>
public class GroupDuplicateTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void SetUp()
    {
        ModuleEditMode.Exit();
        PartRegistry.Clear();
        CommandStack.Clear();
        _spawned.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var el in new List<KitchenElement>(PartRegistry.GetAll()))
            if (el != null) _spawned.Add(el.gameObject);
        foreach (var go in _spawned)
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
        CommandStack.Clear();
        ElementFactory.ClearPools();
    }

    private KitchenElement Part(string name, Vector3 position)
    {
        var go = ElementFactory.CreatePart(new Vector3Int(800, 400, 18), name, position);
        _spawned.Add(go);
        return go.GetComponent<KitchenElement>();
    }

    private static void AssertAt(Vector3 expected, Vector3 actual, string why)
    {
        Assert.AreEqual(expected.x, actual.x, 1e-4f, why);
        Assert.AreEqual(expected.y, actual.y, 1e-4f, why);
        Assert.AreEqual(expected.z, actual.z, 1e-4f, why);
    }

    [Test]
    public void Of_TwoSources_CopiesBoth_ByOneOffset_UnderASingleUndoStep()
    {
        var first = Part("Bok", Vector3.zero);
        var second = Part("Polka", new Vector3(2f, 0f, 0f));
        var offset = new Vector3(0f, 0f, 0.1f);

        var copies = GroupDuplicate.Of(new List<KitchenElement> { first, second }, offset,
            out var command);
        foreach (var copy in copies) _spawned.Add(copy.gameObject);

        Assert.AreEqual(2, copies.Count,
            "копии создаются для ВСЕХ выделенных, а не только для того, что было последним");
        Assert.IsNotNull(command,
            "создание обязано попасть в стек: без команды копии переживают Ctrl+Z");

        CommandStack.Execute(command!);

        AssertAt(first.transform.position + offset, copies[0].transform.position,
            "копия уезжает на переданный вектор");
        AssertAt(second.transform.position + offset, copies[1].transform.position,
            "и на ТОТ ЖЕ вектор: отдельный вектор на копию развалил бы взаимное "
            + "расположение группы");
        Assert.AreEqual(1, CommandStack.UndoCount,
            "одна отмена на всю группу — по записи на объект пользователь жал бы "
            + "Ctrl+Z столько раз, сколько скопировал");

        CommandStack.Undo();

        Assert.IsFalse(copies[0].gameObject.activeSelf,
            "отмена убирает первую копию");
        Assert.IsFalse(copies[1].gameObject.activeSelf,
            "и вторую тем же нажатием — иначе половина группы осталась бы в сцене");
    }

    [Test]
    public void Of_OneSource_StaysASingleCommand_NotAGroupOfOne()
    {
        var only = Part("Bok", Vector3.zero);

        var copies = GroupDuplicate.Of(new List<KitchenElement> { only },
            new Vector3(0.1f, 0f, 0f), out var command);
        foreach (var copy in copies) _spawned.Add(copy.gameObject);

        Assert.AreEqual(1, copies.Count, "одна деталь — одна копия");
        Assert.IsNotNull(command, "и она тоже отменяема");
        Assert.IsFalse(command is CompositeCommand,
            "противоположный вход к групповому: одиночный дубликат обязан лечь в стек "
            + "прежней записью, иначе история одной детали изменилась бы задним числом");
    }

    [Test]
    public void Of_NoSources_CreatesNothing_AndHandsBackNoCommand()
    {
        var copies = GroupDuplicate.Of(new List<KitchenElement>(), Vector3.zero, out var command);

        Assert.AreEqual(0, copies.Count, "копировать нечего");
        Assert.IsNull(command,
            "пустая команда в стеке — это Ctrl+Z, который «ничего не делает», и человек "
            + "жмёт его второй раз, теряя настоящую правку");
        Assert.AreEqual(0, GroupDuplicate.Of(null, Vector3.zero, out _).Count,
            "отсутствующий список — тот же отказ, а не NullReferenceException по Ctrl+D");
    }
}
