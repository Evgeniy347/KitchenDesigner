using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Дублирование элемента. Раньше это была лестница из шестнадцати
/// <c>if (source is XxxElement)</c> в ElementFactoryInstance.Duplicate; теперь —
/// упорядоченный реестр ElementDuplicators, как ElementSpawners в слое MCP.
/// Здесь проверяется то, что в лестнице стояло комментариями, и парность реестра:
/// каждый тип, который фабрика умеет создать, она обязана уметь и скопировать.
/// </summary>
public class ElementDuplicatorTests
{
    [SetUp]
    public void SetUp() => PartRegistry.Clear();

    [TearDown]
    public void TearDown()
    {
        foreach (var el in new List<KitchenElement>(PartRegistry.GetAll()))
            if (el != null) UnityEngine.Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
        ElementFactory.ClearPools();
    }

    /// <summary>Копия берёт ИМЯ ОРИГИНАЛА, а занятость разрешает ElementNaming
    /// суффиксом «_1», «_2». Явный суффикс вроде « (copy)» после чистки стал бы
    /// «_copy», и копия копии росла бы в «X_copy_copy».</summary>
    [Test]
    public void Duplicate_Twice_NumbersTheCopies_InsteadOfGrowingASuffix()
    {
        var original = ElementFactory.CreatePart(new Vector3Int(800, 400, 18), "Bok", Vector3.zero);
        var source = original.GetComponent<KitchenElement>();

        var first = ElementFactory.Duplicate(source).GetComponent<KitchenElement>();
        var second = ElementFactory.Duplicate(first).GetComponent<KitchenElement>();

        Assert.AreNotEqual(source.PartName, first.PartName);
        Assert.AreNotEqual(first.PartName, second.PartName);
        foreach (var name in new[] { first.PartName, second.PartName })
        {
            StringAssert.StartsWith("Bok", name, "копия наследует имя оригинала");
            Assert.IsFalse(name.Contains("copy"),
                "суффикс «copy» не должен появляться: копия копии выросла бы в X_copy_copy");
        }
    }

    [Test]
    public void Duplicate_Cooktop_KeepsItsEditedSizeAndCutout()
    {
        var go = ElementFactory.CreateCooktop("Hob", Vector3.zero);
        var source = go.GetComponent<CooktopElement>();
        source.DimensionsMM = new Vector3Int(700, 70, 560);
        source.CutoutWidthMM = 620;
        source.CutoutDepthMM = 500;

        var copy = ElementFactory.Duplicate(source).GetComponent<CooktopElement>();

        Assert.AreEqual(source.DimensionsMM, copy.DimensionsMM,
            "габариты свободной варочной редактируемые — без переноса «дублировать» "
            + "молча возвращало бы панель по умолчанию");
        Assert.AreEqual(source.CutoutWidthMM, copy.CutoutWidthMM);
        Assert.AreEqual(source.CutoutDepthMM, copy.CutoutDepthMM);
    }

    [Test]
    public void Duplicate_Cooktop_DoesNotStealTheCutoutOfTheOriginal()
    {
        var top = ElementFactory.CreatePart(new Vector3Int(1600, 650, 38), "Top", Vector3.zero);
        var topEl = top.GetComponent<KitchenElement>();
        topEl.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        topEl.DimensionsMM = new Vector3Int(1600, 650, 38);

        var go = ElementFactory.CreateCooktop("Hob", new Vector3(0f, 0.019f + 0.05f, 0f));
        var source = go.GetComponent<CooktopElement>();
        source.SnapToPart();
        Assert.IsTrue(source.IsAttached, "предусловие: варочная села на столешницу");

        var copy = ElementFactory.Duplicate(source).GetComponent<CooktopElement>();

        Assert.AreEqual("", copy.AttachedPartName,
            "имя хозяина не переносим: копия «украла» бы проём оригинала — привязку она "
            + "находит сама через SnapToPart");
    }

    [Test]
    public void Duplicate_Sink_DoesNotStealTheCutoutOfTheOriginal()
    {
        var top = ElementFactory.CreatePart(new Vector3Int(1600, 650, 38), "Top", Vector3.zero);
        var topEl = top.GetComponent<KitchenElement>();
        topEl.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        topEl.DimensionsMM = new Vector3Int(1600, 650, 38);

        var go = ElementFactory.CreateSink("Sink", new Vector3(0f, 0.019f + 0.05f, 0f));
        var source = go.GetComponent<SinkElement>();
        source.SnapToPart();
        Assert.IsTrue(source.IsAttached, "предусловие: мойка села на столешницу");

        var copy = ElementFactory.Duplicate(source).GetComponent<SinkElement>();

        Assert.AreEqual("", copy.AttachedPartName,
            "как и у варочной: привязку копия находит сама, а перенос имени отдал бы ей "
            + "проём оригинала");
    }

    /// <summary>Парность реестра: всё, что фабрика умеет СОЗДАТЬ, она обязана
    /// уметь и СКОПИРОВАТЬ в тот же тип. Пропущенная запись не падает — копия
    /// молча выходит обычной доской (CONVENTIONS.md → «A capability table has a
    /// twin in the contract»).</summary>
    [Test]
    public void Duplicate_EveryFactoryType_ProducesAnElementOfTheSameType()
    {
        var originals = new List<KitchenElement>
        {
            Made(ElementFactory.CreatePart(new Vector3Int(800, 400, 18), "Board", Vector3.zero)),
            Made(ElementFactory.CreateFacade(new Vector3Int(400, 700, 18), "Facade", Vector3.zero)),
            Made(ElementFactory.CreateAssembledFacade(new Vector3Int(400, 700, 18), "Assembled", Vector3.zero)),
            Made(ElementFactory.Instance.CreatePanel(new Vector3Int(600, 400, 3), "Hdf", Vector3.zero)),
            Made(ElementFactory.CreateRadialShelf(600, 400, 18, 100, "Radial", Vector3.zero)),
            Made(ElementFactory.CreateDrawer(DrawerType.A, 350, DrawerColor.Anthracite, 400, "Drawer", Vector3.zero)),
            Made(ElementFactory.CreateTable(new Vector3Int(1200, 750, 700), "Table", Vector3.zero)),
            Made(ElementFactory.CreateRadiusTable(new Vector3Int(1200, 750, 700), "RTable", Vector3.zero)),
            Made(ElementFactory.CreatePillar(700, "Pillar", Vector3.zero)),
            Made(ElementFactory.CreateFloor(new Vector3Int(3000, 100, 3000), "Floor", Vector3.zero)),
            Made(ElementFactory.CreateLightSource("Lamp", Vector3.zero)),
            Made(ElementFactory.CreateSink("Sink", Vector3.zero)),
            Made(ElementFactory.CreateCooktop("Hob", Vector3.zero)),
            Made(ElementFactory.CreateOven("Oven", Vector3.zero)),
            Made(ElementFactory.CreateDishwasher("Dw", Vector3.zero)),
            Made(ElementFactory.CreateWindow(new Vector3Int(900, 1200, 100), "Window", Vector3.zero)),
            Made(ElementFactory.CreateDoor(new Vector3Int(900, 2000, 100), "Door", Vector3.zero)),
        };

        var wrong = new List<string>();
        foreach (var source in originals)
        {
            var copyGo = ElementFactory.Duplicate(source);
            var copy = copyGo == null ? null : copyGo.GetComponent<KitchenElement>();
            if (copy == null || copy.GetType() != source.GetType())
                wrong.Add($"{source.GetType().Name} -> {(copy == null ? "null" : copy.GetType().Name)}");
        }

        Assert.IsEmpty(wrong,
            "в реестре ElementDuplicators нет записи для этих типов, и копия выходит не тем, "
            + "чем был оригинал: " + string.Join(", ", wrong));
    }

    [Test]
    public void Duplicate_KeepsTheRotationOfTheOriginal()
    {
        var go = ElementFactory.CreatePart(new Vector3Int(800, 400, 18), "Board", Vector3.zero);
        var source = go.GetComponent<KitchenElement>();
        source.transform.rotation = Quaternion.Euler(0f, 37f, 0f);

        var copy = ElementFactory.Duplicate(source);

        Assert.AreEqual(0f, Quaternion.Angle(source.transform.rotation, copy.transform.rotation), 0.01f,
            "разворот копируется для КАЖДОГО типа — раньше эта строка повторялась в каждой "
            + "ветке лестницы и её можно было забыть");
    }

    [Test]
    public void Duplicate_OffsetsTheCopy_SoItDoesNotHideInsideTheOriginal()
    {
        var go = ElementFactory.CreatePart(new Vector3Int(800, 400, 18), "Board", Vector3.zero);
        var source = go.GetComponent<KitchenElement>();

        var copy = ElementFactory.Duplicate(source);

        Assert.AreEqual(ElementFactoryInstance.DUPLICATE_OFFSET_UNITS,
            copy.transform.position.x - source.transform.position.x, 1e-4f);
    }

    private static KitchenElement Made(GameObject go)
    {
        var el = go.GetComponent<KitchenElement>();
        Assert.IsNotNull(el, "фабрика обязана вернуть объект с KitchenElement");
        return el!;
    }
}
