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

    /// <summary>Сенсор задачи C (PipeEndFittingsMaximizeLinksTests →
    /// MissingReferenceException): PartRegistry — статический синглтон на весь прогон
    /// EditMode, так что элемент, уничтоженный через DestroyImmediate БЕЗ
    /// предварительного Unregister, переживает границу тестового класса и валит
    /// СЛЕДУЮЩИЙ тест, который трогает GetAll() без `!= null`
    /// (agents/TEST-DESIGN.md → «`!= null` before touching a scene object is
    /// load-bearing, not style»). Ровно этот порядок раньше был у
    /// <c>SnapTestBase.BaseTeardown</c>: DestroyImmediate без Unregister. Тест
    /// воспроизводит правильный порядок (Unregister ПЕРЕД DestroyImmediate) и
    /// проверяет заявленным Unity-инвариантом — <c>e == null</c> для
    /// уничтоженного объекта не бросает, а корректно возвращает true — что после
    /// него в реестре не остаётся мёртвых ссылок.</summary>
    [Test]
    public void UnregisterBeforeDestroy_LeavesNoDeadReferenceInRegistry()
    {
        PartRegistry.Clear();
        var e = Make();
        PartRegistry.Register(e);

        PartRegistry.Unregister(e);
        Object.DestroyImmediate(e.gameObject);
        _spawned.Remove(e.gameObject);

        foreach (var survivor in PartRegistry.GetAll())
            Assert.IsFalse(survivor == null,
                "уничтоженный элемент не должен остаться в реестре — Unregister обязан " +
                "идти ПЕРЕД DestroyImmediate, а не после (или не идти вовсе)");
    }

    /// <summary>Противоположный вход: то же самое, но БЕЗ Unregister вовсе — ровно
    /// то, что раньше делал SnapTestBase.BaseTeardown (DestroyImmediate и ничего
    /// больше). Доказывает, что тест выше действительно ловит регрессию, а не
    /// проходит при любом порядке операций.</summary>
    [Test]
    public void DestroyWithoutUnregister_LeavesADeadReferenceInRegistry()
    {
        PartRegistry.Clear();
        var e = Make();
        PartRegistry.Register(e);

        Object.DestroyImmediate(e.gameObject);
        _spawned.Remove(e.gameObject);

        bool anyDead = false;
        foreach (var survivor in PartRegistry.GetAll())
            if (survivor == null) anyDead = true;

        Assert.IsTrue(anyDead,
            "стенд обязан доказать сам себя: уничтожение БЕЗ Unregister обязано оставить " +
            "мёртвую ссылку в реестре — иначе он ничего не проверяет");
        PartRegistry.Clear();
    }
}
