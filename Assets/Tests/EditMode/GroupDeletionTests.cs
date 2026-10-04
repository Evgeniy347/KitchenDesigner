using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

public class GroupDeletionTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void Setup()
    {
        PartRegistry.Clear();
        GroupManager.Clear();
        CommandStack.Clear();
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
        GroupManager.Clear();
        CommandStack.Clear();
    }

    private KitchenElement MakeElement(string name)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        var element = go.AddComponent<KitchenElement>();
        element.PartName = name;
        element.DimensionsMM = new Vector3Int(600, 400, 18);
        PartRegistry.Register(element);
        _spawned.Add(go);
        return element;
    }

    private (LinkGroup group, KitchenElement a, KitchenElement b, KitchenElement outsider) Module()
    {
        var a = MakeElement("Боковина");
        var b = MakeElement("Полка");
        var outsider = MakeElement("Свободная");
        var group = GroupManager.Link(new List<KitchenElement> { a, b })!;
        GroupManager.Rename(group, "Модуль");
        return (group, a, b, outsider);
    }

    private static bool InScene(KitchenElement e) => PartRegistry.GetAll().Contains(e);

    [Test]
    public void Dissolve_FreesTheMembers_AndKeepsThemInTheScene()
    {
        var (group, a, b, _) = Module();

        GroupDeletion.Dissolve(group);

        Assert.IsEmpty(new List<LinkGroup>(GroupManager.AllGroups()), "группа расформирована");
        Assert.AreEqual(0, a.GroupId);
        Assert.AreEqual(0, b.GroupId);
        Assert.IsTrue(InScene(a) && InScene(b), "«Расформировать» не трогает детали — только связь");
    }

    [Test]
    public void Dissolve_Undo_BringsTheGroupBackWithTheSameMembersAndName()
    {
        var (group, a, b, outsider) = Module();
        int id = group.id;
        GroupDeletion.Dissolve(group);

        CommandStack.Undo();

        var back = GroupManager.GroupOf(a);
        Assert.IsNotNull(back, "отмена возвращает группу");
        Assert.AreEqual(id, back!.id, "с тем же номером: ссылки на неё в проекте не рвутся");
        Assert.AreEqual("Модуль", back.name);
        Assert.AreSame(back, GroupManager.GroupOf(b));
        Assert.IsNull(GroupManager.GroupOf(outsider), "посторонняя деталь в группу не попадает");
    }

    [Test]
    public void DeleteWithContents_TakesTheGroupAndEveryMemberOutOfTheScene_ButNobodyElse()
    {
        var (group, a, b, outsider) = Module();

        GroupDeletion.DeleteWithContents(group);

        Assert.IsFalse(InScene(a));
        Assert.IsFalse(InScene(b), "«Удалить с содержимым» уносит все детали группы");
        Assert.IsTrue(InScene(outsider), "и только их");
        Assert.IsEmpty(new List<LinkGroup>(GroupManager.AllGroups()), "пустая группа после удаления не остаётся");
    }

    [Test]
    public void DeleteWithContents_IsOneUndoStep_AndUndoBringsEverythingBack()
    {
        var (group, a, b, _) = Module();
        int undoBefore = CommandStack.UndoCount;

        GroupDeletion.DeleteWithContents(group);

        Assert.AreEqual(undoBefore + 1, CommandStack.UndoCount,
            "одно удаление группы — один шаг отмены, а не по шагу на деталь");
        Assert.AreEqual(GroupDeletion.DeleteDescription(group), CommandStack.PeekUndoDescription());

        CommandStack.Undo();

        Assert.IsTrue(InScene(a) && InScene(b), "детали вернулись");
        var back = GroupManager.GroupOf(a);
        Assert.IsNotNull(back, "вместе с группой");
        Assert.AreSame(back, GroupManager.GroupOf(b));
    }

    [Test]
    public void DeleteWithContents_Redo_RemovesThemAgain()
    {
        var (group, a, b, _) = Module();
        GroupDeletion.DeleteWithContents(group);
        CommandStack.Undo();

        CommandStack.Redo();

        Assert.IsFalse(InScene(a) || InScene(b));
    }
}
