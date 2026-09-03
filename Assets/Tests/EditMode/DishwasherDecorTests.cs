using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Заводской вид посудомойки обязан пережить <c>MaterialManager.Apply</c>,
/// и выбранный декор обязан пережить его тоже.
///
/// Дефект тот же, что у духовки, и по тем же дорогам. Общий путь <c>Apply</c> для
/// элемента без своей ветки пишет материал прямо в рендерер, мимо элемента;
/// умолчание каталога — серый ЛДСП. Посудомойка строится от
/// <c>ElementRoot.NewEmpty</c>, рендерера на корне нет, и
/// <c>GetComponentInChildren&lt;MeshRenderer&gt;()</c> находит первого ребёнка —
/// «BodyBottom». Попадали туда открытие проекта (<c>ElementRestorers.ApplyShared</c>
/// → <c>ApplyById</c>) и дублирование (<c>CopyMaterial</c> → тот же
/// <c>ApplyById</c>), то есть серел корпус у копии и после загрузки файла.
///
/// Обратная сторона: выбранный декор ложился на того же единственного ребёнка и
/// исчезал при первой перестройке — <c>RebuildGeometry</c> зовёт
/// <c>ApplyMaterials</c>, знавшую только заводские материалы. Перекрасить
/// посудомойку было нельзя.
///
/// Лечится способностью <c>IPaintsItself</c>, а не веткой по типу в
/// MaterialManager (CONVENTIONS.md → «Element type checks live in ONE place per
/// layer»). Декор кладётся на дверцу и цоколь — то, что видно, когда фасад не
/// навешен; бак и чёрная панель управления остаются заводскими.
///
/// Свойство ДВУСТОРОННЕЕ, и односторонний тест был бы хуже, чем никакого:
/// заглушив первое возвратом заводской краски всегда, получили бы технику,
/// которую нельзя перекрасить.</summary>
public class DishwasherDecorTests
{
    private const string DecorId = "test_dishwasher_decor";

    private const string Tank = "BodyBottom";
    private const string Door = "Door";
    private const string Plinth = "Base";
    private const string Panel = "ControlPanel";

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

    private static DishwasherElement NewDishwasher()
    {
        var go = ElementFactory.CreateDishwasher("Посудомойка", Vector3.zero);
        var dishwasher = go.GetComponent<DishwasherElement>();
        Assert.IsNotNull(dishwasher, "фабрика обязана вернуть объект с DishwasherElement");
        return dishwasher!;
    }

    private static Material? PaintOf(DishwasherElement dishwasher, string childName)
    {
        var part = dishwasher.transform.Find(childName);
        Assert.IsNotNull(part, "посудомойка строит деталь «" + childName + "» отдельным "
            + "ребёнком — без него тест смотрит в пустоту и зеленеет по недоразумению");
        var renderer = part!.GetComponent<MeshRenderer>();
        Assert.IsNotNull(renderer, "у детали «" + childName + "» обязан быть рендерер: "
            + "именно такого ребёнка находит общий путь Apply, когда у элемента нет "
            + "своей ветки");
        return renderer!.sharedMaterial;
    }

    private static Material ChosenDecor()
    {
        var mat = MaterialManager.GetSharedMaterial(MaterialCatalog.Get(DecorId));
        Assert.IsNotNull(mat, "каталог обязан отдать материал зарегистрированного декора — "
            + "иначе сравнивать не с чем и тест зеленеет впустую");
        return mat!;
    }

    [Test]
    public void ANewDishwasher_IsFactoryPainted_NotTheFurnitureDefault()
    {
        var dishwasher = NewDishwasher();

        Assert.AreSame(ApplianceMaterials.DishwasherTank, PaintOf(dishwasher, Tank),
            "посудомойка рождается заводской: умолчание каталога — серый ЛДСП, и это декор "
            + "мебели, а не техники");
        Assert.AreSame(ApplianceMaterials.DishwasherDoor, PaintOf(dishwasher, Door),
            "дверца рождается тёмной, а не в цвете бака");
    }

    [Test]
    public void ApplyingTheCatalogDefault_LeavesTheFactoryPaintAlone()
    {
        var dishwasher = NewDishwasher();

        MaterialManager.ApplyById(dishwasher, MaterialCatalog.DefaultId);

        Assert.AreSame(ApplianceMaterials.DishwasherTank, PaintOf(dishwasher, Tank),
            "умолчательный id значит «пользователь ничего не выбирал», а не «покрась "
            + "серым». Через этот вызов ходят открытие проекта (ApplyShared) и "
            + "дублирование (CopyMaterial), поэтому серел бак после загрузки файла и у "
            + "копии");
        Assert.AreSame(ApplianceMaterials.DishwasherDoor, PaintOf(dishwasher, Door),
            "дверца тоже обязана остаться заводской");
    }

    [Test]
    public void ACopiedDishwasher_KeepsTheFactoryPaint()
    {
        var copy = ElementFactory.Duplicate(NewDishwasher()).GetComponent<DishwasherElement>();
        Assert.IsNotNull(copy, "дублирование обязано вернуть объект с DishwasherElement");

        Assert.AreSame(ApplianceMaterials.DishwasherTank, PaintOf(copy!, Tank),
            "копия проходит CopyMaterial с умолчательным id оригинала — путь, на котором "
            + "заводской вид теряется молча");
    }

    [Test]
    public void AChosenDecor_LandsOnTheDoorAndPlinth_AndSparesTheTank()
    {
        var dishwasher = NewDishwasher();

        MaterialManager.ApplyById(dishwasher, DecorId);

        Assert.AreSame(ChosenDecor(), PaintOf(dishwasher, Door),
            "выбранный декор обязан лечь на дверцу: вето на умолчание не должно "
            + "превратиться в технику, которую нельзя перекрасить");
        Assert.AreSame(ChosenDecor(), PaintOf(dishwasher, Plinth),
            "цоколь виден вместе с дверцей и красится с ней заодно — иначе фронт выйдет "
            + "двухцветным");
        Assert.AreSame(ApplianceMaterials.DishwasherTank, PaintOf(dishwasher, Tank),
            "декор — отделка видимого фронта, а не заливка всей машины; общий путь Apply "
            + "красил именно бак, потому что тот идёт первым ребёнком");
        Assert.AreSame(ApplianceMaterials.DishwasherPanel, PaintOf(dishwasher, Panel),
            "панель управления остаётся чёрной: заводские акценты декор не трогает");
        Assert.AreEqual(DecorId, dishwasher.MaterialId, "выбор обязан ещё и сохраниться");
    }

    [Test]
    public void AChosenDecor_SurvivesARebuildCausedByResizing()
    {
        var dishwasher = NewDishwasher();
        MaterialManager.ApplyById(dishwasher, DecorId);
        var chosen = PaintOf(dishwasher, Door);

        dishwasher.DimensionsMM = new Vector3Int(600, 820, 550);

        Assert.AreSame(chosen, PaintOf(dishwasher, Door),
            "перестройка меша решает про материал заново, и «поставь заводскую дверцу» "
            + "вернуло бы тёмный поверх выбранного декора при первом же изменении размера");
    }
}
