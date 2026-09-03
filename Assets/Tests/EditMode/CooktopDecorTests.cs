using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Заводской вид варочной панели переживает <c>MaterialManager.Apply</c>,
/// и выбранный декор переживает его тоже.
///
/// Общий путь <c>Apply</c> для элемента БЕЗ своей ветки пишет материал прямо в
/// рендерер, мимо элемента, а умолчание каталога — серый ЛДСП. Варочная строится
/// от <c>ElementRoot.NewEmpty</c>, то есть рендерера на корне у неё нет и
/// <c>GetComponentInChildren&lt;MeshRenderer&gt;()</c> нашёл бы первого ребёнка —
/// «Top», ту самую стеклокерамику. Попасть туда легче всего не руками
/// пользователя: дублирование зовёт <c>CopyMaterial</c> → <c>ApplyById</c>, а
/// открытие файла — <c>ApplyShared</c> в ElementRestorers, который применяет
/// materialId каждому элементу подряд. Серой панель выходила бы у КОПИИ и после
/// открытия проекта, а не при создании.
///
/// Этого не происходит: у варочной СВОЯ ветка в Apply, зовущая
/// <c>ApplyMaterials</c>, и решение «заводское стекло или выбранный декор»
/// принимает сам элемент. Тесты ниже держат это свойство, а не чинят его.
/// Проверка на красноту: убрать из <c>MaterialManager.Apply</c> ветку
/// <c>is CooktopElement</c> — и <c>ApplyingTheCatalogDefault_LeavesTheGlassAlone</c>
/// вместе с <c>ACopiedCooktop_KeepsTheFactoryGlass</c> краснеют, потому что на
/// «Top» ляжет ЛДСП.
///
/// Свойство ДВУСТОРОННЕЕ, и односторонний тест здесь был бы хуже, чем никакого:
/// заглушив первое возвратом стекла всегда, получили бы технику, которую нельзя
/// перекрасить.</summary>
public class CooktopDecorTests
{
    private const string DecorId = "test_cooktop_decor";

    [SetUp]
    public void SetUp()
    {
        PartRegistry.Clear();
        MaterialCatalog.Register(new MaterialDef(DecorId, "Тестовый декор", "плитка",
            new Color(0.2f, 0.4f, 0.6f)));
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var el in new List<KitchenElement>(PartRegistry.GetAll()))
            if (el != null) Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
        ElementFactory.ClearPools();
        MaterialCatalog.Reset();
    }

    private static CooktopElement NewCooktop()
    {
        var go = ElementFactory.CreateCooktop("Варочная", Vector3.zero);
        var cooktop = go.GetComponent<CooktopElement>();
        Assert.IsNotNull(cooktop, "фабрика обязана вернуть объект с CooktopElement");
        return cooktop!;
    }

    private static Material? PaintOf(CooktopElement cooktop)
    {
        var plate = cooktop.transform.Find("Top");
        Assert.IsNotNull(plate, "варочная строит стеклокерамику ребёнком «Top» — без него "
            + "тест смотрит в пустоту и зеленеет по недоразумению");
        var renderer = plate!.GetComponent<MeshRenderer>();
        Assert.IsNotNull(renderer, "у «Top» обязан быть рендерер: именно его находит общий "
            + "путь Apply, когда у элемента нет своей ветки");
        return renderer!.sharedMaterial;
    }

    [Test]
    public void ANewCooktop_IsFactoryGlass_NotTheFurnitureDefault()
    {
        Assert.AreSame(ApplianceMaterials.CooktopGlass, PaintOf(NewCooktop()),
            "варочная рождается чёрной стеклокерамикой: умолчание каталога — серый ЛДСП, "
            + "и это декор мебели, а не техники");
    }

    [Test]
    public void ApplyingTheCatalogDefault_LeavesTheGlassAlone()
    {
        var cooktop = NewCooktop();

        MaterialManager.ApplyById(cooktop, MaterialCatalog.DefaultId);

        Assert.AreSame(ApplianceMaterials.CooktopGlass, PaintOf(cooktop),
            "умолчательный id значит «пользователь ничего не выбирал», а не «покрась серым». "
            + "Через этот вызов ходят дублирование (CopyMaterial) и открытие проекта "
            + "(ApplyShared), поэтому серой панель выходила бы у копии и после загрузки файла");
    }

    [Test]
    public void ACopiedCooktop_KeepsTheFactoryGlass()
    {
        var copy = ElementFactory.Duplicate(NewCooktop()).GetComponent<CooktopElement>();

        Assert.AreSame(ApplianceMaterials.CooktopGlass, PaintOf(copy),
            "копия проходит CopyMaterial с умолчательным id оригинала — путь, на котором "
            + "заводской вид теряется молча: ни один снимок габаритов материала не видит");
    }

    [Test]
    public void AChosenDecor_LandsOnThePlate()
    {
        var cooktop = NewCooktop();

        MaterialManager.ApplyById(cooktop, DecorId);

        Assert.AreNotSame(ApplianceMaterials.CooktopGlass, PaintOf(cooktop),
            "выбранный декор обязан лечь на панель: вето на умолчание не должно превратиться "
            + "в технику, которую нельзя перекрасить");
        Assert.AreEqual(DecorId, cooktop.MaterialId, "выбор обязан ещё и сохраниться");
    }

    [Test]
    public void AChosenDecor_SurvivesARebuildCausedByResizing()
    {
        var cooktop = NewCooktop();
        MaterialManager.ApplyById(cooktop, DecorId);
        var chosen = PaintOf(cooktop);

        cooktop.DimensionsMM = new Vector3Int(700, 70, 560);

        Assert.AreSame(chosen, PaintOf(cooktop),
            "перестройка меша решает про материал заново, и «поставь заводское стекло» "
            + "вернуло бы стеклокерамику поверх выбранного декора при первом же изменении "
            + "размера");
    }
}
