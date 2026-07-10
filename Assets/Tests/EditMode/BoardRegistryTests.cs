using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class BoardRegistryTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    private KitchenElement Make()
    {
        var go = new GameObject("E");
        var e = go.AddComponent<KitchenElement>();
        _spawned.Add(go);
        return e;
    }

    [TearDown]
    public void Teardown()
    {
        BoardRegistry.Clear();
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
    }

    [Test]
    public void Register_AddsElement_GetAllReturnsCopy()
    {
        BoardRegistry.Clear();
        var e = Make();
        BoardRegistry.Register(e);

        var all = BoardRegistry.GetAll();
        Assert.Contains(e, all);

        // GetAll отдаёт копию — мутация списка не влияет на реестр.
        all.Clear();
        Assert.AreEqual(1, BoardRegistry.GetAll().Count);
    }

    [Test]
    public void Register_Duplicate_DoesNotAddTwice()
    {
        BoardRegistry.Clear();
        var e = Make();
        BoardRegistry.Register(e);
        BoardRegistry.Register(e);
        Assert.AreEqual(1, BoardRegistry.GetAll().Count);
    }

    [Test]
    public void Register_Null_Ignored()
    {
        BoardRegistry.Clear();
        BoardRegistry.Register(null);
        Assert.AreEqual(0, BoardRegistry.GetAll().Count);
    }

    [Test]
    public void Unregister_RemovesElement()
    {
        BoardRegistry.Clear();
        var e = Make();
        BoardRegistry.Register(e);
        BoardRegistry.Unregister(e);
        Assert.AreEqual(0, BoardRegistry.GetAll().Count);
    }

    [Test]
    public void All_ReflectsRegistrations()
    {
        BoardRegistry.Clear();
        var e = Make();
        BoardRegistry.Register(e);
        Assert.AreEqual(1, BoardRegistry.All.Count);
        Assert.AreSame(e, BoardRegistry.All[0]);
    }

    [Test]
    public void Clear_RemovesAll()
    {
        var e = Make();
        BoardRegistry.Register(e);
        BoardRegistry.Clear();
        Assert.AreEqual(0, BoardRegistry.GetAll().Count);
    }
}
