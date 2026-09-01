using NUnit.Framework;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>Категория пункта каталога для режима редактора. Здесь только чистая
/// классификация: всё, что строит саму панель, живёт в PlayMode-наборе
/// SidebarPanelTests — сайдбар подписывается на EditModeManager.Changed, а вне
/// Play mode Unity не зовёт OnDestroy и подписка переживает свои кнопки.</summary>
public class SidebarUITests
{
    [Test]
    public void ItemCategory_MatchesTheCategoryTheSpawnedElementWillGet()
    {
        var groups = SidebarCatalog.Build();
        var room = groups[5].items;

        Assert.AreEqual(EditModeManager.Category.Always,
            SidebarUI.ItemCategory(room.Find(i => i.name == EditModeManager.KorobName)),
            "короб доступен в любом режиме — так же его классифицирует "
            + "EditModeManager.Categorize уже в сцене");
        Assert.AreEqual(EditModeManager.Category.Always,
            SidebarUI.ItemCategory(room.Find(i => i.isWindow)));
        Assert.AreEqual(EditModeManager.Category.Always,
            SidebarUI.ItemCategory(room.Find(i => i.isDoor)));
        Assert.AreEqual(EditModeManager.Category.Room,
            SidebarUI.ItemCategory(room.Find(i => i.isWall)));
        Assert.AreEqual(EditModeManager.Category.Room,
            SidebarUI.ItemCategory(room.Find(i => i.isFloor)));
        Assert.AreEqual(EditModeManager.Category.Regular,
            SidebarUI.ItemCategory(groups[0].items[0]));
    }
}
