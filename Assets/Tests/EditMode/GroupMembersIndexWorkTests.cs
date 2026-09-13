using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Прибор назвал виновника кадра смены выделения (дамп
/// <c>perf_20260913_080209.csv</c>, лог плеера): <c>GroupServiceInstance.MembersOf</c>
/// звучал ТРИ раза на одном кадре, и каждый раз обходил всю сцену ради членов
/// ОДНОЙ группы — 411 деталей на вопрос «кто в группе 7».
///
/// Приём тот же, что уже трижды окупился в этом репозитории: спрашивать индекс,
/// построенный один раз, а не обходить сцену на каждый вопрос. Индекс
/// перестраивается только когда членство МОГЛО измениться —
/// <c>GroupMembershipRevision</c> бампит и сеттер <c>KitchenElement.GroupId</c>,
/// и приход/уход детали из реестра. Поэтому здесь проверяется И работа (сколько
/// обходов сцены и сколько пересборок), И обратный вход: ответ обязан остаться
/// правильным после каждого способа поменять членство, иначе подсветка группы
/// покрасит не ту группу.</summary>
public class GroupMembersIndexWorkTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private GroupServiceInstance? _svc;

    [SetUp]
    public void SetUp()
    {
        PartRegistry.Clear();
        GroupManager.Clear();
        _svc = new GroupServiceInstance();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
        GroupManager.Clear();
    }

    private KitchenElement MakeElement(string name)
    {
        var go = new GameObject(name);
        var e = go.AddComponent<KitchenElement>();
        e.PartName = name;
        e.DimensionsMM = new Vector3Int(600, 400, 18);
        PartRegistry.Register(e);
        _spawned.Add(go);
        return e;
    }

    private static void StartCounting()
    {
        PartRegistryInstance.TakeGetAllCalls();
        GroupServiceInstance.TakeIndexRebuilds();
    }

    [Test]
    public void ThreeQuestionsOnOneFrame_CostOneSceneScan_NotThree()
    {
        var a = MakeElement("A");
        var b = MakeElement("B");
        MakeElement("C");
        var g = _svc!.Link(new List<KitchenElement> { a, b })!;

        StartCounting();
        _svc.MembersOf(g);
        _svc.MembersOf(g);
        _svc.MembersOf(g);

        Assert.AreEqual(1, PartRegistryInstance.TakeGetAllCalls(),
            "три вопроса на одном кадре — один обход сцены; на кадре 3001 их было три");
        Assert.AreEqual(1, GroupServiceInstance.TakeIndexRebuilds());
    }

    [Test]
    public void SecondFrameWithoutAChange_CostsNoSceneScanAtAll()
    {
        var a = MakeElement("A");
        var b = MakeElement("B");
        var g = _svc!.Link(new List<KitchenElement> { a, b })!;
        _svc.MembersOf(g);

        StartCounting();
        _svc.MembersOf(g);

        Assert.AreEqual(0, PartRegistryInstance.TakeGetAllCalls(),
            "членство не менялось — обходить сцену незачем");
        Assert.AreEqual(0, GroupServiceInstance.TakeIndexRebuilds());
    }

    [Test]
    public void MembersOf_AnswersTheGroupItWasAskedAbout()
    {
        var a = MakeElement("A");
        var b = MakeElement("B");
        var c = MakeElement("C");
        var d = MakeElement("D");
        var one = _svc!.Link(new List<KitchenElement> { a, b })!;
        var two = _svc.Link(new List<KitchenElement> { c, d })!;

        CollectionAssert.AreEquivalent(new[] { a, b }, _svc.MembersOf(one));
        CollectionAssert.AreEquivalent(new[] { c, d }, _svc.MembersOf(two),
            "подсветка группы обязана красить ТУ группу");
    }

    [Test]
    public void AddTo_IsSeenByTheVeryNextQuestion()
    {
        var a = MakeElement("A");
        var b = MakeElement("B");
        var g = _svc!.Link(new List<KitchenElement> { a, b })!;
        var late = MakeElement("Late");
        _svc.MembersOf(g);

        _svc.AddTo(g, late);

        CollectionAssert.Contains(_svc.MembersOf(g), late,
            "устаревший индекс — это деталь, которая не поедет вместе с модулем");
    }

    [Test]
    public void RemoveFrom_IsSeenByTheVeryNextQuestion()
    {
        var a = MakeElement("A");
        var b = MakeElement("B");
        var g = _svc!.Link(new List<KitchenElement> { a, b })!;
        _svc.MembersOf(g);

        _svc.RemoveFrom(b);

        CollectionAssert.DoesNotContain(_svc.MembersOf(g), b);
    }

    [Test]
    public void GroupIdWrittenBehindTheService_IsStillSeen()
    {
        var a = MakeElement("A");
        var b = MakeElement("B");
        var g = _svc!.Link(new List<KitchenElement> { a, b })!;
        var outsider = MakeElement("Outsider");
        _svc.MembersOf(g);

        outsider.GroupId = g.id;

        CollectionAssert.Contains(_svc.MembersOf(g), outsider,
            "загрузка проекта пишет GroupId мимо сервиса — индекс обязан это заметить");
    }

    [Test]
    public void ElementLeavingTheScene_IsSeenByTheVeryNextQuestion()
    {
        var a = MakeElement("A");
        var b = MakeElement("B");
        var g = _svc!.Link(new List<KitchenElement> { a, b })!;
        _svc.MembersOf(g);

        PartRegistry.Unregister(b);

        CollectionAssert.DoesNotContain(_svc.MembersOf(g), b);
    }

    [Test]
    public void MembersOf_HandsOutACopy_SoCallersCannotCorruptTheIndex()
    {
        var a = MakeElement("A");
        var b = MakeElement("B");
        var g = _svc!.Link(new List<KitchenElement> { a, b })!;

        _svc.MembersOf(g).Clear();

        Assert.AreEqual(2, _svc.MembersOf(g).Count,
            "общий список наружу — это индекс, который вычистит первый же вызывающий");
    }

    [Test]
    public void UnknownGroup_AnswersEmpty_NotTheWholeScene()
    {
        MakeElement("A");
        var stranger = new LinkGroup { id = 4242 };

        Assert.IsEmpty(_svc!.MembersOf(stranger));
    }
}
