using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class ElementFactorySandboxTests
{
    [SetUp]
    public void Setup()
    {
        PartRegistry.Clear();
        SceneRevision.Reset();
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var el in PartRegistry.GetAll())
            if (el != null) ElementFactory.DestroyElement(el.gameObject);
        PartRegistry.Clear();
    }

    [Test]
    public void NormalSpawn_RegistersInPartRegistry_AndBumpsSceneRevision()
    {
        int before = SceneRevision.Version;
        var go = ElementFactory.CreatePart(new Vector3Int(600, 400, 18), "Normal", Vector3.zero);

        Assert.AreEqual(1, PartRegistry.GetAll().Count,
            "обычный спавн обязан попасть в реестр");
        Assert.Greater(SceneRevision.Version, before,
            "обычный спавн обязан поднять SceneRevision — от него зависят автосохранение и отпечаток сцены");

        ElementFactory.DestroyPart(go);
    }

    [Test]
    public void NormalSpawn_ReusingPooledObject_StillRegistersInPartRegistry()
    {
        var first = ElementFactory.CreatePart(new Vector3Int(600, 400, 18), "First", Vector3.zero);
        ElementFactory.DestroyPart(first);
        Assert.AreEqual(0, PartRegistry.GetAll().Count,
            "первый спавн должен был снять себя с учёта при возврате в пул");

        var second = ElementFactory.CreatePart(new Vector3Int(600, 400, 18), "Second", Vector3.zero);

        Assert.AreEqual(1, PartRegistry.GetAll().Count,
            "второй спавн переиспользует объект из пула — Awake на нём уже не сработает, " +
            "регистрация обязана произойти на месте вызова (ElementRoot.Publish), а не только в Awake");

        ElementFactory.DestroyPart(second);
    }

    [Test]
    public void SandboxSpawn_DoesNotRegister_DoesNotBumpSceneRevision_AndLeavesNoDeadEntry()
    {
        int before = SceneRevision.Version;
        GameObject go;
        using (ElementFactorySandbox.Enter())
        {
            go = ElementFactory.CreatePart(new Vector3Int(600, 400, 18), "Sandbox", Vector3.zero);

            Assert.AreEqual(0, PartRegistry.GetAll().Count,
                "элемент песочницы попал в PartRegistry — он будет виден валидации, спецификации и автосохранению");
            Assert.AreEqual(before, SceneRevision.Version,
                "элемент песочницы поднял SceneRevision — тот же счётчик двигает undo/автосохранение/отпечаток сцены");
        }

        ElementFactory.DestroyPart(go);

        Assert.AreEqual(0, PartRegistry.GetAll().Count,
            "после уборки в реестре не должно остаться ни живой, ни мёртвой записи о песочном элементе");
        Assert.AreEqual(before, SceneRevision.Version,
            "уборка песочного элемента не должна была что-то регистрировать и тут же снимать с учёта");
    }

    [Test]
    public void SandboxScope_DoesNotLeakIntoSubsequentNormalSpawn()
    {
        using (ElementFactorySandbox.Enter())
        {
            var sandboxGo = ElementFactory.CreatePart(new Vector3Int(500, 300, 18), "Sandbox", Vector3.zero);
            ElementFactory.DestroyPart(sandboxGo);
        }

        var go = ElementFactory.CreatePart(new Vector3Int(500, 300, 18), "Normal", Vector3.zero);
        Assert.AreEqual(1, PartRegistry.GetAll().Count,
            "scope не закрылся после Dispose — следующий обычный спавн тоже не зарегистрировался");
        ElementFactory.DestroyPart(go);
    }
}
