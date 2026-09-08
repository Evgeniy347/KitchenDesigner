using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class PartRegistryTests
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
        PartRegistry.Clear();
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
    }

    [Test]
    public void Register_AddsElement_GetAllReturnsCopy()
    {
        PartRegistry.Clear();
        var e = Make();
        PartRegistry.Register(e);

        var all = PartRegistry.GetAll();
        Assert.Contains(e, all);

        // GetAll отдаёт копию — мутация списка не влияет на реестр.
        all.Clear();
        Assert.AreEqual(1, PartRegistry.GetAll().Count);
    }

    [Test]
    public void Register_Duplicate_DoesNotAddTwice()
    {
        PartRegistry.Clear();
        var e = Make();
        PartRegistry.Register(e);
        PartRegistry.Register(e);
        Assert.AreEqual(1, PartRegistry.GetAll().Count);
    }

    [Test]
    public void Register_Null_Ignored()
    {
        PartRegistry.Clear();
        PartRegistry.Register(null!);
        Assert.AreEqual(0, PartRegistry.GetAll().Count);
    }

    [Test]
    public void Unregister_RemovesElement()
    {
        PartRegistry.Clear();
        var e = Make();
        PartRegistry.Register(e);
        PartRegistry.Unregister(e);
        Assert.AreEqual(0, PartRegistry.GetAll().Count);
    }

    [Test]
    public void All_ReflectsRegistrations()
    {
        PartRegistry.Clear();
        var e = Make();
        PartRegistry.Register(e);
        Assert.AreEqual(1, PartRegistry.All.Count);
        Assert.AreSame(e, PartRegistry.All[0]);
    }

    [Test]
    public void Clear_RemovesAll()
    {
        var e = Make();
        PartRegistry.Register(e);
        PartRegistry.Clear();
        Assert.AreEqual(0, PartRegistry.GetAll().Count);
    }

    // В Play Mode KitchenElement.Awake зовёт PartRegistry.Register синхронно при
    // AddComponent — раньше, чем фабрика следующей строкой присваивает PartName
    // (см. ElementFactoryInstance.CreateWall и соседей: el.PartName = go.name идёт
    // ПОСЛЕ AddComponent<KitchenElement>()). Реестр хранит только ссылку на
    // элемент и не кэширует имя на момент Register — Register(e) здесь имитирует
    // именно этот ранний вызов, до переименования.
    [Test]
    public void Register_BeforePartNameAssigned_LiveNameLookupStillFindsElement()
    {
        PartRegistry.Clear();
        var e = Make();
        PartRegistry.Register(e);

        e.PartName = "RenamedAfterRegister";

        KitchenElement? found = null;
        foreach (var el in PartRegistry.All)
            if (el.PartName == "RenamedAfterRegister") { found = el; break; }

        Assert.AreSame(e, found,
            "PartRegistry должен читать PartName живьём при каждом поиске, а не " +
            "кэшировать его на момент Register — иначе элемент, зарегистрированный " +
            "раньше своего имени (как это делает Awake), был бы ненаходим по имени");
    }
}
