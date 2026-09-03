using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Заводской вид духовки обязан пережить <c>MaterialManager.Apply</c>,
/// и выбранный декор обязан пережить его тоже.
///
/// Общий путь <c>Apply</c> для элемента БЕЗ своей ветки пишет материал прямо в
/// рендерер, мимо элемента, а умолчание каталога — серый ЛДСП. Духовка строится
/// от <c>ElementRoot.NewEmpty</c>: рендерера на корне нет, поэтому
/// <c>GetComponentInChildren&lt;MeshRenderer&gt;()</c> находит ПЕРВОГО ребёнка —
/// «BodyBottom». Ходят туда не руками пользователя: открытие проекта зовёт
/// <c>ElementRestorers.ApplyShared</c> → <c>ApplyById(el, data.materialId)</c>, а
/// дублирование — <c>CopyMaterial</c> → тот же <c>ApplyById</c>. То есть серым
/// дно корпуса становилось после открытия файла и у копии, а не при создании, и
/// держалось до следующей перестройки — самый неудобный вид дефекта: ни один
/// снимок габаритов материала не видит.
///
/// Обратная сторона того же дефекта: выбранный декор ложился ровно на одного
/// ребёнка (то же «BodyBottom») и исчезал при первой перестройке, потому что
/// <c>RebuildGeometry</c> зовёт <c>ApplyMaterials</c>, а та знала только про
/// заводские материалы. Перекрасить духовку было нельзя вообще.
///
/// Лечится не веткой по типу в MaterialManager (лестница типов там уже была, и
/// растить её запрещено — CONVENTIONS.md → «Element type checks live in ONE place
/// per layer»), а способностью <c>IPaintsItself</c>: элемент получает материал на
/// вход и сам решает, что с ним делать. Решение «заводской вид или выбранный
/// декор» задаёт один помощник <c>SanitaryDecor</c> — тот же, что у ванны,
/// унитазов, розеток и выключателей.
///
/// Декор кладётся на «Facade» — видимую дверцу; корпус, стекло, панель и ручка
/// остаются заводскими. Свойство ДВУСТОРОННЕЕ, и односторонний тест здесь был бы
/// хуже, чем никакого: заглушив первое возвратом заводской краски всегда,
/// получили бы технику, которую нельзя перекрасить.</summary>
public class OvenDecorTests
{
    private const string DecorId = "test_oven_decor";

    private const string Corpus = "BodyBottom";
    private const string Facade = "Facade";
    private const string Handle = "Handle";

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

    private static OvenElement NewOven()
    {
        var go = ElementFactory.CreateOven("Духовка", Vector3.zero);
        var oven = go.GetComponent<OvenElement>();
        Assert.IsNotNull(oven, "фабрика обязана вернуть объект с OvenElement");
        return oven!;
    }

    private static Material? PaintOf(OvenElement oven, string childName)
    {
        var part = oven.transform.Find(childName);
        Assert.IsNotNull(part, "духовка строит деталь «" + childName + "» отдельным "
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
    public void ANewOven_IsFactoryPainted_NotTheFurnitureDefault()
    {
        var oven = NewOven();

        Assert.AreSame(ApplianceMaterials.OvenBody, PaintOf(oven, Corpus),
            "духовка рождается заводской: умолчание каталога — серый ЛДСП, и это декор "
            + "мебели, а не техники");
        Assert.AreSame(ApplianceMaterials.OvenFacade, PaintOf(oven, Facade),
            "дверца рождается чёрной, а не в цвете корпуса");
    }

    [Test]
    public void ApplyingTheCatalogDefault_LeavesTheFactoryPaintAlone()
    {
        var oven = NewOven();

        MaterialManager.ApplyById(oven, MaterialCatalog.DefaultId);

        Assert.AreSame(ApplianceMaterials.OvenBody, PaintOf(oven, Corpus),
            "умолчательный id значит «пользователь ничего не выбирал», а не «покрась "
            + "серым». Через этот вызов ходят открытие проекта (ApplyShared) и "
            + "дублирование (CopyMaterial), поэтому серело дно корпуса после загрузки "
            + "файла и у копии");
        Assert.AreSame(ApplianceMaterials.OvenFacade, PaintOf(oven, Facade),
            "дверца тоже обязана остаться заводской");
    }

    [Test]
    public void ACopiedOven_KeepsTheFactoryPaint()
    {
        var copy = ElementFactory.Duplicate(NewOven()).GetComponent<OvenElement>();
        Assert.IsNotNull(copy, "дублирование обязано вернуть объект с OvenElement");

        Assert.AreSame(ApplianceMaterials.OvenBody, PaintOf(copy!, Corpus),
            "копия проходит CopyMaterial с умолчательным id оригинала — путь, на котором "
            + "заводской вид теряется молча");
    }

    [Test]
    public void AChosenDecor_LandsOnTheFacade_AndSparesTheCorpus()
    {
        var oven = NewOven();

        MaterialManager.ApplyById(oven, DecorId);

        Assert.AreSame(ChosenDecor(), PaintOf(oven, Facade),
            "выбранный декор обязан лечь на дверцу: вето на умолчание не должно "
            + "превратиться в технику, которую нельзя перекрасить");
        Assert.AreSame(ApplianceMaterials.OvenBody, PaintOf(oven, Corpus),
            "декор — отделка видимой дверцы, а не заливка всей духовки; общий путь Apply "
            + "красил именно корпус, потому что тот идёт первым ребёнком");
        Assert.AreSame(ApplianceMaterials.OvenHandle, PaintOf(oven, Handle),
            "ручка остаётся металлической: заводские акценты декор не трогает");
        Assert.AreEqual(DecorId, oven.MaterialId, "выбор обязан ещё и сохраниться");
    }

    [Test]
    public void AChosenDecor_SurvivesARebuildCausedByResizing()
    {
        var oven = NewOven();
        MaterialManager.ApplyById(oven, DecorId);
        var chosen = PaintOf(oven, Facade);

        oven.DimensionsMM = new Vector3Int(600, 600, 570);

        Assert.AreSame(chosen, PaintOf(oven, Facade),
            "перестройка меша решает про материал заново, и «поставь заводскую дверцу» "
            + "вернуло бы чёрный поверх выбранного декора при первом же изменении размера");
    }
}
