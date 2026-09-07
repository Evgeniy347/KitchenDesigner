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
        topEl.transform.rotation = ManagedRotation.Euler(-90f, 0f, 0f);
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
        topEl.transform.rotation = ManagedRotation.Euler(-90f, 0f, 0f);
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
        source.transform.rotation = ManagedRotation.Euler(0f, 37f, 0f);

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

    /// <summary>У ванны ЧЕТЫРЕ поля формы, и все четыре подрезаются габаритом.
    /// Ветка дублирования, забывшая любое из них, отдаёт копию с заводским
    /// значением вместо заказанного — и это не видно: ванна остаётся ванной
    /// правильного размера, просто с другой чашей. Значения тут НЕзаводские
    /// специально: совпавшее с умолчанием не отличить от потерянного.</summary>
    [Test]
    public void Duplicate_Bathtub_KeepsAllFourBowlFields()
    {
        var source = Made(ElementFactory.CreateBathtub(
            BathtubLayout.DefaultDimensionsMM, 61, 401, 151, 91, "Bathtub", Vector3.zero));

        var copy = ElementFactory.Duplicate(source).GetComponent<BathtubElement>();

        Assert.AreEqual(61, copy.RimWidthMM, "борт");
        Assert.AreEqual(401, copy.BowlDepthMM, "глубина чаши");
        Assert.AreEqual(151, copy.BowlRadiusMM, "радиус чаши");
        Assert.AreEqual(91, copy.BowlFilletMM, "скругление дна");
    }

    /// <summary>Шесть полей формы смесителя подрезают друг друга, и три из них —
    /// цепочкой: диаметр корпуса опускает потолок межосевому и поднимает пол
    /// длине корпуса. Ветка дублирования, забывшая любое поле или переставившая
    /// два присвоения местами, отдаёт копию с ЗАВОДСКИМ значением вместо
    /// заказанного, и это не видно: смеситель остаётся смесителем, просто
    /// другим. Числа тут незаводские специально — совпавшее с умолчанием не
    /// отличить от потерянного.</summary>
    [Test]
    public void Duplicate_BathMixer_KeepsAllSixShapeFields()
    {
        var source = Made(ElementFactory.CreateBathMixer(
            BathMixerSpec.Clamped(163, 287, 63, 41, 127, 17), "BathMixer", Vector3.zero));

        var copy = ElementFactory.Duplicate(source).GetComponent<BathMixerElement>();

        Assert.AreEqual(163, copy.CentresMM, "межосевое");
        Assert.AreEqual(287, copy.BodyLengthMM, "длина корпуса");
        Assert.AreEqual(63, copy.BodyDiameterMM, "диаметр корпуса");
        Assert.AreEqual(41, copy.EscutcheonReachMM, "вылет отражателя");
        Assert.AreEqual(127, copy.SpoutLengthMM, "длина излива");
        Assert.AreEqual(17, copy.OutletDiameterMM, "диаметр штуцера");
    }

    /// <summary>То же для восьми полей стойки. Здесь потеря заметна ещё меньше:
    /// габарит стойки ВЫЧИСЛЯЕТСЯ из формы, поэтому копия с потерянной длиной
    /// шланга отличается от оригинала не только петлёй, но и высотой коробки —
    /// а на глаз это читается как «копия почему-то встала иначе».</summary>
    [Test]
    public void Duplicate_ShowerColumn_KeepsAllEightShapeFields()
    {
        var source = Made(ElementFactory.CreateShowerColumn(
            ShowerColumnSpec.Clamped(1213, 37, 263, 37, 407, 71, 117, 1063),
            "ShowerColumn", Vector3.zero));

        var copy = ElementFactory.Duplicate(source).GetComponent<ShowerColumnElement>();

        Assert.AreEqual(1213, copy.ColumnHeightMM, "высота стойки");
        Assert.AreEqual(37, copy.RiserDiameterMM, "диаметр штанги");
        Assert.AreEqual(263, copy.HeadDiameterMM, "диаметр лейки");
        Assert.AreEqual(37, copy.HeadThicknessMM, "толщина лейки");
        Assert.AreEqual(407, copy.ArmReachMM, "вынос лейки");
        Assert.AreEqual(71, copy.WallOffsetMM, "вылет от стены");
        Assert.AreEqual(117, copy.HandShowerDiameterMM, "диаметр ручной лейки");
        Assert.AreEqual(1063, copy.HoseLengthMM, "длина шланга");
    }

    [Test]
    public void Duplicate_Pillar_KeepsItsDiameter()
    {
        var source = Made(ElementFactory.CreatePillar(700, "Pillar", Vector3.zero));
        ((PillarElement)source).DiameterMM = 150;

        var copy = ElementFactory.Duplicate(source).GetComponent<PillarElement>();

        Assert.AreEqual(150, copy.DiameterMM,
            "копия опоры обязана сохранить сечение: фабрика по умолчанию ставит 50");
    }

    /// <summary>Репро дефекта: обычный стол при дублировании терял отступ ножек и
    /// декор ножек. Его ветка реестра отдавала только CopyMaterial, а MaterialId у
    /// TableElement — всего лишь псевдоним PrimaryMaterialId, поэтому переживала
    /// копирование одна столешница: отступ возвращался к заводскому, а ножки
    /// перекрашивались в цвет столешницы. RadiusTableElement копировал оба поля с
    /// самого начала — два родственных типа расходились в поведении.</summary>
    [Test]
    public void Duplicate_Table_KeepsLegInsetAndLegsDecor()
    {
        var dims = new Vector3Int(1200, 750, 700);
        var factoryDefault = (TableElement)Made(
            ElementFactory.CreateTable(dims, "TableDefault", Vector3.zero));
        Assume.That(factoryDefault.LegInsetMM, Is.Not.EqualTo(47),
            "предусловие: 47 мм не совпадает с заводским отступом, иначе тест остался бы "
            + "зелёным и при полностью потерянном поле");

        var source = (TableElement)Made(ElementFactory.CreateTable(dims, "Table", Vector3.zero));
        source.LegInsetMM = 47;
        source.PrimaryMaterialId = "decor-top";
        source.SecondaryMaterialId = "decor-legs";

        var copy = ElementFactory.Duplicate(source).GetComponent<TableElement>();

        Assert.AreEqual(47, copy.LegInsetMM,
            "отступ ножек обязан пережить копирование: без него копия молча встаёт с "
            + "заводскими ножками, хотя у радиусного стола это работало всегда");
        Assert.AreEqual("decor-legs", copy.SecondaryMaterialId,
            "слот ножек — отдельный декор: без него копия красит ножки в цвет столешницы, "
            + "потому что MaterialId у стола лишь псевдоним PrimaryMaterialId");
        Assert.AreEqual("decor-top", copy.PrimaryMaterialId,
            "слот столешницы не должен пострадать от переноса слота ножек");
    }

    /// <summary>Парность по ПОВЕДЕНИЮ, а не по тексту исходника: у IHasTwoDecorSlots два слота
    /// декора, и копия обязана сохранить оба у КАЖДОГО типа. Обычный стол был
    /// единственным, кто отдавал в реестр CopyMaterial вместо CopyDecorSlots, и
    /// терял слот ножек; поимённая проверка одного типа не помешала бы следующему
    /// повторить пропуск. Набор экземпляров не записан руками — он сверяется
    /// рефлексией по сборке ядра, поэтому новый IHasTwoDecorSlots сначала уронит сам список,
    /// а не пройдёт мимо проверки (CONVENTIONS.md → «A field list written out more
    /// than twice gets a parity test»).</summary>
    /// <summary>Четыре поля формы у обоих настенных устройств, и два из них —
    /// ширина и высота рамки — одного рода и в одних единицах. Ветка
    /// дублирования, переставившая их местами, отдаёт копию, которая выглядит
    /// почти как оригинал и молча врёт о размерах; поэтому здесь числа не только
    /// незаводские, но и РАЗНЫЕ между собой. Число постов проверяется отдельной
    /// строкой: оно умножает ширину габарита, и его потеря — единственная,
    /// которую видно глазом.</summary>
    [Test]
    public void Duplicate_Socket_KeepsAllFourShapeFields()
    {
        var source = Made(ElementFactory.CreateSocket(
            WallDeviceSpec.Clamped(97, 83, 13, 2), "SocketShape", Vector3.zero));

        var copy = ElementFactory.Duplicate(source).GetComponent<SocketElement>();

        Assert.AreEqual(97, copy.PlateWidthMM, "ширина рамки");
        Assert.AreEqual(83, copy.PlateHeightMM, "высота рамки");
        Assert.AreEqual(13, copy.ProtrusionMM, "вынос от стены");
        Assert.AreEqual(2, copy.PostCount, "число постов");
    }

    /// <summary>У выключателя к тем же четырём добавляются питание и список
    /// светильников. Список — это и есть вся связь: копия, потерявшая его,
    /// остаётся выключателем, который ничем не управляет, и заметить это можно
    /// только щёлкнув по нему.</summary>
    [Test]
    public void Duplicate_LightSwitch_KeepsItsShapePowerAndLinks()
    {
        Made(ElementFactory.CreateLightSource("Люстра", Vector3.zero));
        var source = Made(ElementFactory.CreateLightSwitch(
            WallDeviceSpec.Clamped(89, 91, 17, 3), false, new[] { "Люстра" },
            "SwitchShape", Vector3.zero));

        var copy = ElementFactory.Duplicate(source).GetComponent<LightSwitchElement>();

        Assert.AreEqual(89, copy.PlateWidthMM, "ширина рамки");
        Assert.AreEqual(91, copy.PlateHeightMM, "высота рамки");
        Assert.AreEqual(17, copy.ProtrusionMM, "вынос от стены");
        Assert.AreEqual(3, copy.PostCount, "число клавиш");
        Assert.IsFalse(copy.IsOn, "состояние клавиши");
        Assert.AreEqual(new[] { "Люстра" }, copy.LightNames,
            "список светильников — это вся связь, и без него копия ничем не управляет");
    }

    [Test]
    public void Duplicate_EveryCarrier_KeepsBothDecorSlots()
    {
        var originals = new List<KitchenElement>
        {
            Made(ElementFactory.CreateTable(new Vector3Int(1200, 750, 700), "Table", Vector3.zero)),
            Made(ElementFactory.CreateRadiusTable(new Vector3Int(1200, 750, 700), "RTable", Vector3.zero)),
            Made(ElementFactory.CreateStool(new Vector3Int(360, 450, 360), 20, "Stool", Vector3.zero)),
            Made(ElementFactory.CreateChair(new Vector3Int(450, 900, 450), 20, 450, "Chair", Vector3.zero)),
            Made(ElementFactory.CreateSofa(new Vector3Int(1800, 800, 900), 20, 420, "Sofa", Vector3.zero)),
            Made(ElementFactory.CreatePouffe(new Vector3Int(400, 420, 400), 20, 80, "Pouffe", Vector3.zero)),
            Made(ElementFactory.CreateBed(new Vector3Int(1600, 500, 2000), true, true, "Bed", Vector3.zero)),
            Made(ElementFactory.CreateToilet(430, "Toilet", Vector3.zero)),
            Made(ElementFactory.CreateWallHungToilet(430, 640, "WallHungToilet", Vector3.zero)),
            Made(ElementFactory.CreateSocket(WallDeviceSpec.Clamped(97, 83, 13, 2), "Socket", Vector3.zero)),
            Made(ElementFactory.CreateLightSwitch(WallDeviceSpec.Clamped(89, 91, 17, 3), false, null, "LightSwitch", Vector3.zero)),
        };

        var declared = new List<Type>();
        foreach (var type in typeof(IHasTwoDecorSlots).Assembly.GetTypes())
            if (typeof(IHasTwoDecorSlots).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                declared.Add(type);

        Assert.IsNotEmpty(declared,
            "рефлексия не нашла ни одного IHasTwoDecorSlots — сканер смотрит не в ту сборку, "
            + "и тогда проверка ниже зелёная, но не проверяет ничего");
        CollectionAssert.AreEquivalent(declared, originals.ConvertAll(el => el.GetType()),
            "список экземпляров разошёлся с типами IHasTwoDecorSlots в сборке ядра: новый тип "
            + "обязан появиться и здесь, иначе он копируется без проверки слотов");

        var lost = new List<string>();
        foreach (var source in originals)
        {
            var slots = (IHasTwoDecorSlots)source;
            var typeName = source.GetType().Name;
            slots.PrimaryMaterialId = "top-" + typeName;
            slots.SecondaryMaterialId = "legs-" + typeName;

            var copy = (IHasTwoDecorSlots)ElementFactory.Duplicate(source).GetComponent<KitchenElement>();

            if (copy.PrimaryMaterialId != slots.PrimaryMaterialId)
                lost.Add(typeName + ".PrimaryMaterialId = " + copy.PrimaryMaterialId
                    + " (ожидался " + slots.PrimaryMaterialId + ")");
            if (copy.SecondaryMaterialId != slots.SecondaryMaterialId)
                lost.Add(typeName + ".SecondaryMaterialId = " + copy.SecondaryMaterialId
                    + " (ожидался " + slots.SecondaryMaterialId + ")");
        }

        Assert.IsEmpty(lost,
            "ветка реестра ElementDuplicators отдала не CopyDecorSlots, и копия потеряла "
            + "слот декора: " + string.Join("; ", lost));
    }
}
