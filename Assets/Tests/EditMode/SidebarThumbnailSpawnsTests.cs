using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>Приёмка вернула ровно этот дефект: <c>SidebarThumbnailSpawns</c> носил
/// СВОЮ таблицу габаритов по типу, отдельную от <see cref="SidebarCatalog"/>, и она
/// уже разошлась с ним (полка 18 против 16, стол 1200×750×700 против
/// 2000×750×1000, ящик тип B/белый/500 против A/антрацит/350, ДВП 600×720×18
/// против 600×400×3, фасад 720 против 716). Картинка показывала не то, что
/// заспавнится.
///
/// Фикс убрал таблицу и повёл миниатюру через ТОТ ЖЕ <see cref="SidebarSpawnRouter"/>,
/// которым пользуется реальный спаун (<c>ElementSpawner</c>), поэтому расхождение
/// теперь структурно невозможно для полей, что попадают в спаун через маршрутизатор
/// (это доказывает <c>SidebarSpawnRouterTests</c>). Остаётся один риск: реализация
/// <c>IElementSpawns</c> внутри самого <c>SidebarThumbnailSpawns</c> может позвать
/// фабрику с перепутанным порядком аргументов или не тем полем — маршрутизатор этого
/// не увидит, он лишь проверяет, ЧТО было передано подставному приёмнику, а не что
/// получившийся элемент содержит.
///
/// Поэтому сенсор здесь — не запись вызова, а СРАВНЕНИЕ РЕЗУЛЬТАТА: элемент,
/// заведённый через <see cref="SidebarThumbnailSpawns"/>, обязан иметь ровно те
/// габариты, что каталог для него объявляет.</summary>
public class SidebarThumbnailSpawnsTests
{
    [TearDown]
    public void TearDown()
    {
        PartRegistry.Clear();
    }

    private static IEnumerable<SidebarCatalog.Item> CatalogItems() =>
        SidebarCatalog.Build().SelectMany(g => g.items);

    /// <summary>Ревертни фикс (верни отдельную таблицу дефолтов в
    /// <c>SidebarThumbnailSpawns</c>, разошедшуюся с каталогом хоть на 1 мм) — этот
    /// тест обязан покраснеть. Он и покраснел на записи «полка 18 вместо 16» до
    /// правки: словарь по <c>SidebarItemKind</c> не знает о выборе КОНКРЕТНОЙ
    /// записи каталога (полка и радиусная полка делят один <c>SidebarItemKind</c> в
    /// старой таблице лишь отчасти — но габарит доски был захардкожен один на всех
    /// «Board»), тогда как каталог для каждой записи свой.</summary>
    [Test]
    public void EveryCatalogItem_SpawnsAnElementWithExactlyTheCatalogsDimensions()
    {
        var mismatched = new List<string>();

        foreach (var item in CatalogItems())
        {
            var spawn = SidebarThumbnailSpawns.For(item);
            Assert.IsNotNull(spawn,
                "у записи каталога «" + item.name + "» (" + item.kind
                + ") нет спауна для миниатюры вовсе");

            GameObject go;
            using (ElementFactorySandbox.Enter())
            {
                go = spawn!();
            }

            try
            {
                var element = go.GetComponent<KitchenElement>();
                Assert.IsNotNull(element,
                    "спаун миниатюры для «" + item.name + "» вернул объект без KitchenElement");

                if (element!.DimensionsMM != item.dims)
                    mismatched.Add(item.name + " (" + item.kind + "): каталог "
                        + item.dims + ", миниатюра " + element.DimensionsMM);
            }
            finally
            {
                DestroyElement(go);
            }
        }

        Assert.IsEmpty(mismatched,
            "миниатюра спавнит элемент С ДРУГИМИ ГАБАРИТАМИ, чем объявляет каталог — "
            + "картинка обманывает пользователя насчёт того, что заспавнится. "
            + "Расхождения: " + string.Join("; ", mismatched));
    }

    private static void DestroyElement(GameObject go)
    {
        var element = go.GetComponent<KitchenElement>();
        if (element != null) element.PrepareForDestruction();
        Object.DestroyImmediate(go);
    }

    /// <summary>Сторож сторожа: если каталог вдруг опустеет или спаун начнёт всегда
    /// возвращать один и тот же тип, сравнение выше молча зазеленеет. Разные виды
    /// каталога обязаны давать разные типы компонентов на корне.</summary>
    [Test]
    public void TheSweep_SeesMultipleDistinctCatalogItems()
    {
        var items = CatalogItems().ToList();
        Assert.GreaterOrEqual(items.Count, 20,
            "в каталоге сайдбара обычно два десятка кнопок; меньше — значит Build() "
            + "вернул не то, и сверка габаритов выше проверяет пустоту");

        var distinctKinds = items.Select(i => i.kind).Distinct().Count();
        Assert.Greater(distinctKinds, 5,
            "в перечне почти нет разнообразия видов — сверка габаритов рискует "
            + "сравнивать один и тот же тип сам с собой");
    }

    /// <summary>Регрессия на «мимо SidebarSpawnRouter»: миниатюра фасада обязана
    /// уважать <c>facadeAssembled</c> так же, как настоящий спаун — иначе сборный
    /// фасад в каталоге показывает картинку щитового.</summary>
    [Test]
    public void AssembledFacadePreset_SpawnsAnAssembledFacade_NotAPlainOne()
    {
        var assembled = CatalogItems()
            .FirstOrDefault(i => i.kind == SidebarItemKind.Facade && i.facadeAssembled);
        Assert.IsTrue(assembled.kind == SidebarItemKind.Facade,
            "в каталоге нет сборного фасада — проверка сторожила бы пустоту");

        var plain = CatalogItems()
            .First(i => i.kind == SidebarItemKind.Facade && !i.facadeAssembled);

        GameObject assembledGo, plainGo;
        using (ElementFactorySandbox.Enter())
        {
            assembledGo = SidebarThumbnailSpawns.For(assembled)!();
            plainGo = SidebarThumbnailSpawns.For(plain)!();
        }

        try
        {
            Assert.AreNotEqual(assembledGo.GetComponent<KitchenElement>().GetType(),
                plainGo.GetComponent<KitchenElement>().GetType(),
                "миниатюра сборного фасада завела тот же тип элемента, что и щитовой — "
                + "признак facadeAssembled потерялся между каталогом и спауном миниатюры");
        }
        finally
        {
            DestroyElement(assembledGo);
            DestroyElement(plainGo);
        }
    }
}
