using NUnit.Framework;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>Категория пункта каталога для режима редактора. Здесь только чистая
/// классификация: всё, что строит саму панель, живёт в PlayMode-наборе
/// SidebarPanelTests — сайдбар подписывается на EditModeManager.Changed, а вне
/// Play mode Unity не зовёт OnDestroy и подписка переживает свои кнопки.</summary>
public class SidebarUITests
{
    private const string RoomGroupTitle = "Помещение";

    [Test]
    public void ItemCategory_MatchesTheCategoryTheSpawnedElementWillGet()
    {
        var groups = SidebarCatalog.Build();
        var roomGroup = groups.Find(g => g.title == RoomGroupTitle);
        Assert.IsNotNull(roomGroup.items,
            "группа «" + RoomGroupTitle + "» не найдена: ищем её ПО ИМЕНИ, а не по номеру, "
            + "потому что номер сдвигает каждая новая группа перед ней — «Сантехника» "
            + "сдвинула, и Find по пустому списку тихо вернул пункт по умолчанию, "
            + "который классифицируется как Regular");
        var room = roomGroup.items;

        Assert.AreEqual(EditModeManager.Category.Always,
            SidebarUI.ItemCategory(room.Find(i => i.name == EditModeManager.KorobName)),
            "короб доступен в любом режиме — так же его классифицирует "
            + "EditModeManager.Categorize уже в сцене");
        Assert.AreEqual(EditModeManager.Category.Always,
            SidebarUI.ItemCategory(room.Find(i => i.kind == SidebarItemKind.Window)));
        Assert.AreEqual(EditModeManager.Category.Always,
            SidebarUI.ItemCategory(room.Find(i => i.kind == SidebarItemKind.Door)));
        Assert.AreEqual(EditModeManager.Category.Room,
            SidebarUI.ItemCategory(room.Find(i => i.kind == SidebarItemKind.Wall)));
        Assert.AreEqual(EditModeManager.Category.Room,
            SidebarUI.ItemCategory(room.Find(i => i.kind == SidebarItemKind.Floor)));
        Assert.AreEqual(EditModeManager.Category.Regular,
            SidebarUI.ItemCategory(groups[0].items[0]));
    }

    [Test]
    public void ItemTooltipText_ExplainsRoomOnlyItems_ButNotRegularOnes()
    {
        var groups = SidebarCatalog.Build();
        var wall = groups.Find(g => g.title == RoomGroupTitle).items.Find(
            i => i.kind == SidebarItemKind.Wall);
        var shelf = groups[0].items[0];

        string wallTooltip = SidebarUI.ItemTooltipText(wall, SidebarUI.ItemCategory(wall));
        string shelfTooltip = SidebarUI.ItemTooltipText(shelf, SidebarUI.ItemCategory(shelf));

        StringAssert.Contains("Помещение", wallTooltip,
            "«Стена» доступна только в режиме «Помещение», и это обязано быть сказано "
            + "прямо, а не угадываться по серому цвету кнопки (D3)");
        StringAssert.Contains(SidebarUI.FormatDims(wall.dims), wallTooltip,
            "габариты (D8) обязаны быть в tooltip даже у комнатных пунктов, не только "
            + "у обычных");
        Assert.AreEqual($"{shelf.name}\n{SidebarUI.FormatDims(shelf.dims)}", shelfTooltip,
            "обычный пункт доступен в любом режиме — tooltip не добавляет пояснений про режим, "
            + "но обязан нести габариты (D8): в плитке 96×96 подписи «600 × 400 × 16» уже "
            + "негде поместиться, и tooltip — единственное место, откуда их теперь узнать");
    }

    [Test]
    public void ItemTooltipText_CarriesTheFullNameWithModel_EvenThoughTheButtonDoesNot()
    {
        var modelCooktop = SidebarCatalog.Build()[4].items[1];

        string tooltip = SidebarUI.ItemTooltipText(modelCooktop, SidebarUI.ItemCategory(modelCooktop));

        StringAssert.Contains(CooktopElement.MODEL_BOSCH_PUE611BB5E, tooltip,
            "модель прибора убрана из подписи кнопки (D7), но обязана остаться доступной "
            + "через tooltip — иначе узнать её в списке стало неоткуда");
    }

    [Test]
    public void GroupTitles_AllStartWithAnUppercaseLetter()
    {
        foreach (var g in SidebarCatalog.Build())
            Assert.IsTrue(char.IsUpper(g.title[0]),
                $"заголовок группы «{g.title}» начинается со строчной буквы — "
                + "docs/UI-GUIDELINES.md §5 требует прописную (D6)");
    }
}
