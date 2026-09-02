using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Ванна красит себя САМА, и это не украшение, а необходимость.
///
/// Общий путь <c>MaterialManager.Apply</c> для элемента без своей ветки пишет
/// материал ПРЯМО В РЕНДЕРЕР, мимо элемента. По умолчанию это серый ЛДСП —
/// декор мебели. Ванна акриловая, её умолчание белый акрил, и оно живёт в
/// самом элементе; поэтому любой вызов Apply с умолчательным id закрашивал
/// свежую ванну серым. Молча: ни один прогон от этого не краснел, потому что
/// материал не участвует ни в одном снимке размеров.
///
/// Попасть туда легче всего не руками пользователя, а двумя штатными путями:
/// дублирование зовёт <c>CopyMaterial</c> → <c>ApplyById(el, source.MaterialId)</c>,
/// а загрузка проекта — <c>SceneRestorer</c>, который применяет materialId
/// каждому элементу подряд. То есть серой ванна выходила у КОПИИ и после
/// открытия файла, а не при создании — самый неудобный вид дефекта.
///
/// Лечится не веткой по типу в MaterialManager (лестница типов там уже есть, и
/// растить её запрещено — CONVENTIONS.md → «Element type checks live in ONE
/// place per layer»), а способностью: <c>IPaintsItself</c>. Элемент, умеющий
/// решать про свой материал сам, получает материал на вход и возвращает
/// решение. Унитазы делают ровно это же через ITabletop, и оба варианта сходятся
/// в одном помощнике SanitaryDecor — «заводской вид или выбранный декор»
/// спрашивается в сантехнике одним способом.
///
/// Ключевое свойство здесь ДВУСТОРОННЕЕ, и односторонний тест был бы хуже, чем
/// никакого: заводское умолчание обязано пережить Apply, а СОЗНАТЕЛЬНО
/// выбранный декор обязан Apply пережить тоже. Заглушив первое возвратом
/// акрила всегда, получили бы ванну, которую нельзя перекрасить.</summary>
public class BathtubDecorTests
{
    private const string DecorId = "test_bathtub_decor";

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

    private static BathtubElement NewTub()
    {
        var go = ElementFactory.CreateBathtub(BathtubLayout.DefaultDimensionsMM,
            BathtubLayout.DefaultRimWidthMM, BathtubLayout.DefaultBowlDepthMM,
            BathtubLayout.DefaultBowlRadiusMM, BathtubLayout.DefaultBowlFilletMM,
            "Ванна", Vector3.zero);
        var tub = go.GetComponent<BathtubElement>();
        Assert.IsNotNull(tub, "фабрика обязана вернуть объект с BathtubElement");
        return tub!;
    }

    private static Material? PaintOf(BathtubElement tub)
    {
        var renderer = tub.GetComponent<MeshRenderer>();
        Assert.IsNotNull(renderer, "ванна строит свой меш на самом объекте — рендерер "
            + "обязан лежать там же, иначе DecorRenderer указывает в пустоту");
        return renderer!.sharedMaterial;
    }

    [Test]
    public void ANewBathtub_IsWhiteAcrylic_NotTheFurnitureDefault()
    {
        Assert.AreSame(SanitaryMaterials.WhiteAcrylic, PaintOf(NewTub()),
            "ванна рождается акриловой: умолчание каталога — серый ЛДСП, и это декор "
            + "мебели, а не сантехники");
    }

    [Test]
    public void ApplyingTheCatalogDefault_LeavesTheAcrylicAlone()
    {
        var tub = NewTub();

        MaterialManager.ApplyById(tub, MaterialCatalog.DefaultId);

        Assert.AreSame(SanitaryMaterials.WhiteAcrylic, PaintOf(tub),
            "умолчательный id значит «пользователь ничего не выбирал», а не «покрась "
            + "серым». Через этот вызов ходят дублирование (CopyMaterial) и загрузка "
            + "проекта (SceneRestorer), поэтому серой ванна выходила у КОПИИ и после "
            + "открытия файла");
    }

    [Test]
    public void AChosenDecor_SurvivesAndReplacesTheAcrylic()
    {
        var tub = NewTub();

        MaterialManager.ApplyById(tub, DecorId);

        Assert.AreNotSame(SanitaryMaterials.WhiteAcrylic, PaintOf(tub),
            "выбранный декор обязан лечь на ванну: вето на умолчание не должно "
            + "превратиться в ванну, которую нельзя перекрасить");
        Assert.AreEqual(DecorId, tub.MaterialId, "выбор обязан ещё и сохраниться");
    }

    [Test]
    public void AChosenDecor_SurvivesARebuildCausedByResizing()
    {
        var tub = NewTub();
        MaterialManager.ApplyById(tub, DecorId);
        var chosen = PaintOf(tub);

        tub.DimensionsMM = new Vector3Int(1500, 580, 680);

        Assert.AreSame(chosen, PaintOf(tub),
            "перестройка меша решает про материал заново, и кэш «что я ставил в прошлый "
            + "раз» вернул бы сюда акрил поверх выбранного декора при первом же "
            + "изменении размера");
    }
}
