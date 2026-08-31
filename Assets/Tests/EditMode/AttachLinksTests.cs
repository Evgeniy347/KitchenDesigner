using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;

/// <summary>
/// Прикрепление дощечек (<see cref="AttachLinks"/>): кто кому может быть
/// родителем, кто за кем едет, и когда сборка объявляется разъехавшейся.
/// </summary>
public class AttachLinksTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void Setup()
    {
        PartRegistry.Clear();
        CommandStack.Clear();
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var el in PartRegistry.GetAll())
            if (el != null) Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
        CommandStack.Clear();
    }

    private KitchenElement MakeBoard(string name, Vector3 pos, Vector3Int? dims = null)
    {
        var go = ElementFactory.CreatePart(dims ?? new Vector3Int(600, 18, 500), name, pos);
        _spawned.Add(go);
        return go.GetComponent<KitchenElement>();
    }

    private FacadeElement MakeFacade(string name, Vector3 pos)
    {
        var go = ElementFactory.CreateFacade(new Vector3Int(600, 716, 18), name, pos, 2, 2, 2, 2);
        _spawned.Add(go);
        return go.GetComponent<FacadeElement>();
    }

    // ── Роли ────────────────────────────────────────────────────────────

    [Test]
    public void Board_CanBeChildAndParent()
    {
        var board = MakeBoard("Дно", Vector3.zero);
        Assert.IsTrue(AttachLinks.CanBeChild(board));
        Assert.IsTrue(AttachLinks.CanBeParent(board));
    }

    [Test]
    public void Facade_CanBeParentButNeverChild()
    {
        var facade = MakeFacade("Фасад", Vector3.zero);
        Assert.IsTrue(AttachLinks.CanBeParent(facade), "к фасаду прикреплять можно");
        Assert.IsFalse(AttachLinks.CanBeChild(facade), "фасад ни к чему не прикрепляется");
    }

    [Test]
    public void CanAttach_BoardToFacade_True()
    {
        var facade = MakeFacade("Фасад", Vector3.zero);
        var board = MakeBoard("Дно", new Vector3(0f, -0.35f, 0.25f));
        Assert.IsTrue(AttachLinks.CanAttach(board, facade));
    }

    [Test]
    public void CanAttach_Cycle_Rejected()
    {
        var a = MakeBoard("A", Vector3.zero);
        var b = MakeBoard("B", new Vector3(0f, 0.018f, 0f));
        b.AttachedToName = a.PartName;
        Assert.IsTrue(AttachLinks.WouldCycle(a, b), "A к B замкнуло бы кольцо");
        Assert.IsFalse(AttachLinks.CanAttach(a, b));
    }

    // ── Дерево ──────────────────────────────────────────────────────────

    [Test]
    public void Descendants_WholeSubtree_ParentsFirst()
    {
        var facade = MakeFacade("Фасад", Vector3.zero);
        var bottom = MakeBoard("Дно", new Vector3(0f, -0.35f, 0.25f));
        var back = MakeBoard("Задняя", new Vector3(0f, -0.2f, 0.5f));
        var side = MakeBoard("Левая", new Vector3(-0.29f, -0.2f, 0.25f));
        bottom.AttachedToName = facade.PartName;
        back.AttachedToName = bottom.PartName;
        side.AttachedToName = bottom.PartName;

        var all = AttachLinks.Descendants(facade);
        CollectionAssert.AreEquivalent(new[] { bottom, back, side }, all);
        Assert.AreEqual(bottom, all[0], "родитель раньше своих детей");
    }

    [Test]
    public void Descendants_BrokenCycleFromFile_DoesNotHang()
    {
        var a = MakeBoard("A", Vector3.zero);
        var b = MakeBoard("B", new Vector3(0f, 0.018f, 0f));
        // Такое кольцо UI не создаст, но его можно принести в файле проекта.
        a.AttachedToName = b.PartName;
        b.AttachedToName = a.PartName;
        Assert.AreEqual(1, AttachLinks.Descendants(a).Count);
    }

    // ── Перемещение ─────────────────────────────────────────────────────

    [Test]
    public void MoveParent_ChildFollows_SameUndoStep()
    {
        var parent = MakeBoard("Дно", Vector3.zero);
        var child = MakeBoard("Задняя", new Vector3(0f, 0.018f, 0f));
        child.AttachedToName = parent.PartName;

        var before = parent.transform.position;
        var after = before + new Vector3(0.1f, 0f, 0f);
        var cmd = AttachMove.FollowersCommand(parent, before, parent.transform.rotation,
            after, parent.transform.rotation);
        Assert.IsNotNull(cmd);
        parent.transform.position = after;
        cmd!.Execute();

        Assert.AreEqual(0.1f, child.transform.position.x, 1e-4f);
        cmd.Undo();
        Assert.AreEqual(0f, child.transform.position.x, 1e-4f);
    }

    [Test]
    public void MoveChild_ParentStaysPut()
    {
        var parent = MakeBoard("Дно", Vector3.zero);
        var child = MakeBoard("Задняя", new Vector3(0f, 0.018f, 0f));
        child.AttachedToName = parent.PartName;

        // Ребёнок родителя не тащит: поддерево ребёнка пусто.
        Assert.IsNull(AttachMove.FollowersCommand(child, child.transform.position,
            child.transform.rotation, child.transform.position + Vector3.right * 0.2f,
            child.transform.rotation));
        child.transform.position += Vector3.right * 0.2f;
        Assert.AreEqual(Vector3.zero, parent.transform.position);
    }

    [Test]
    public void RotateParent_ChildOrbits()
    {
        var parent = MakeBoard("Дно", Vector3.zero);
        var child = MakeBoard("Задняя", new Vector3(0.5f, 0f, 0f));
        child.AttachedToName = parent.PartName;

        var rotBefore = parent.transform.rotation;
        var rotAfter = Quaternion.Euler(0f, 90f, 0f);
        var cmd = AttachMove.FollowersCommand(parent, Vector3.zero, rotBefore, Vector3.zero, rotAfter);
        Assert.IsNotNull(cmd);
        parent.transform.rotation = rotAfter;
        cmd!.Execute();

        // Поворот на 90 градусов вокруг Y переносит точку (0.5, 0, 0) в (0, 0, -0.5).
        Assert.AreEqual(0f, child.transform.position.x, 1e-4f);
        Assert.AreEqual(-0.5f, child.transform.position.z, 1e-4f);
        Assert.AreEqual(90f, child.transform.rotation.eulerAngles.y, 0.01f);
    }

    [Test]
    public void MoveRoot_WholeHierarchyFollows()
    {
        var facade = MakeFacade("Фасад", Vector3.zero);
        var bottom = MakeBoard("Дно", new Vector3(0f, -0.35f, 0.25f));
        var back = MakeBoard("Задняя", new Vector3(0f, -0.2f, 0.5f));
        bottom.AttachedToName = facade.PartName;
        back.AttachedToName = bottom.PartName;

        var after = new Vector3(1f, 0f, 0f);
        var cmd = AttachMove.FollowersCommand(facade, Vector3.zero, facade.transform.rotation,
            after, facade.transform.rotation);
        facade.transform.position = after;
        cmd!.Execute();

        Assert.AreEqual(1f, bottom.transform.position.x, 1e-4f);
        Assert.AreEqual(1f, back.transform.position.x, 1e-4f, "внук едет вместе с дедом");
    }

    // ── Езда за анимацией ───────────────────────────────────────────────

    [Test]
    public void OpenFacade_ChildRides_AndValidationStaysAtRest()
    {
        var facade = MakeFacade("Фасад", Vector3.zero);
        var bottom = MakeBoard("Дно", new Vector3(0f, -0.35f, 0.25f));
        bottom.AttachedToName = facade.PartName;
        var restPos = bottom.transform.position;

        facade.SetOpen(true);
        facade.StepDoor(1f);
        AttachRider.Step();

        Assert.AreNotEqual(restPos, bottom.transform.position, "деталь уехала вместе с фасадом");
        Assert.IsTrue(bottom.IsAttachRidden);
        Assert.IsFalse(bottom.PoseFollowsTransform, "едущую деталь нельзя двигать и растягивать");
        Assert.AreEqual(restPos, bottom.AttachRestPosition, "поза покоя стоит на месте");

        facade.ForceClose();
        AttachRider.Step();
        Assert.IsFalse(bottom.IsAttachRidden);
        Assert.AreEqual(restPos.x, bottom.transform.position.x, 1e-5f);
        Assert.AreEqual(restPos.y, bottom.transform.position.y, 1e-5f);
        Assert.AreEqual(restPos.z, bottom.transform.position.z, 1e-5f);
    }

    [Test]
    public void ForceRest_ClosesParent_AndPutsChildBack()
    {
        var facade = MakeFacade("Фасад", Vector3.zero);
        var bottom = MakeBoard("Дно", new Vector3(0f, -0.35f, 0.25f));
        bottom.AttachedToName = facade.PartName;
        var restPos = bottom.transform.position;

        facade.SetOpen(true);
        facade.StepDoor(1f);
        AttachRider.Step();
        Assert.IsTrue(bottom.IsAttachRidden);

        // Правка полей у едущей детали обязана сперва вернуть сборку в покой.
        AttachLinks.ForceRest(bottom);
        Assert.IsTrue(facade.IsDoorClosed);
        Assert.IsFalse(bottom.IsAttachRidden);
        Assert.AreEqual(restPos.y, bottom.transform.position.y, 1e-5f);
        Assert.AreEqual(restPos.z, bottom.transform.position.z, 1e-5f);
    }

    // ── Разъехавшаяся сборка ────────────────────────────────────────────

    [Test]
    public void Analyzer_GapBetweenAttached_ReportsError()
    {
        var parent = MakeBoard("Дно", Vector3.zero);
        var child = MakeBoard("Задняя", new Vector3(0f, 0.018f, 0f));
        child.AttachedToName = parent.PartName;
        Assert.IsTrue(AttachLinks.InContact(child, parent), "деталь стоит вплотную");
        Assert.IsFalse(FindAttachIssue().HasValue, "в контакте ошибки нет");

        child.transform.position += new Vector3(0f, 0.05f, 0f);
        Assert.IsTrue(AttachLinks.IsDetached(child));
        var issue = FindAttachIssue();
        Assert.IsTrue(issue.HasValue, "зазор обязан попасть в окно ошибок");
        Assert.AreEqual(IssueLevel.Error, issue!.Value.Level);
    }

    [Test]
    public void Analyzer_DeletedParent_IsNotAnError()
    {
        var parent = MakeBoard("Дно", Vector3.zero);
        var child = MakeBoard("Задняя", new Vector3(0f, 0.018f, 0f));
        child.AttachedToName = parent.PartName;

        PartRegistry.Unregister(parent);
        parent.gameObject.SetActive(false);

        Assert.IsNull(AttachLinks.Parent(child), "удалённый родитель отцепляет ребёнка");
        Assert.IsFalse(AttachLinks.IsDetached(child));
        Assert.IsFalse(FindAttachIssue().HasValue);
    }

    // ── Имя как ключ связи ──────────────────────────────────────────────

    [Test]
    public void Rename_Parent_ChildKeepsLink()
    {
        var parent = MakeBoard("Дно", Vector3.zero);
        var child = MakeBoard("Задняя", new Vector3(0f, 0.018f, 0f));
        child.AttachedToName = parent.PartName;

        DrawerLinks.Rename(parent, "Dno_new");
        Assert.AreEqual(parent.PartName, child.AttachedToName);
        Assert.AreEqual(parent, AttachLinks.Parent(child));
    }

    [Test]
    public void SaveLoad_KeepsAttachment()
    {
        var parent = MakeBoard("Дно", Vector3.zero);
        var child = MakeBoard("Задняя", new Vector3(0f, 0.018f, 0f));
        child.AttachedToName = parent.PartName;

        var data = ElementCapture.FromElement(child);
        Assert.AreEqual(parent.PartName, data.attachedToName);
    }

    private static AnalysisIssue? FindAttachIssue()
    {
        foreach (var issue in SceneAnalyzer.Analyze())
            if (issue.Code == IssueCatalog.CodeAttachDetached) return issue;
        return null;
    }
}
