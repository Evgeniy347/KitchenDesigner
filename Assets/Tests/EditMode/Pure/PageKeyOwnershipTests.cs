using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>M3: PageUp/PageDown были заявлены двумя владельцами разом —
/// <c>ToolbarUI</c> переключал этаж, а открытая панель «Ошибки» одновременно листала
/// список, раз обе стороны читали клавишу напрямую и ни одна не спрашивала другую.
/// Один хозяин на нажатие, как в <see cref="EscapeOwnership"/>: панель «Ошибки»,
/// когда ей есть что листать, забирает клавишу первой.</summary>
public class PageKeyOwnershipTests
{
    [Test]
    public void NothingClaims_LevelSwitchOwnsTheKey()
    {
        Assert.AreEqual(PageKeyOwner.LevelSwitch, PageKeyOwnership.Resolve(new PageKeyClaims()),
            "без открытой панели «Ошибки» PageUp/PageDown как и раньше листают этажи");
    }

    [Test]
    public void ErrorPanelWithIssues_WinsOverLevelSwitch()
    {
        var claims = new PageKeyClaims { ErrorPanelOpenWithIssues = true };
        Assert.AreEqual(PageKeyOwner.ErrorPanel, PageKeyOwnership.Resolve(claims),
            "панель «Ошибки» открыта и есть что листать — PageDown обязан двигать фокус по " +
            "списку, а не одновременно уводить камеру на другой этаж");
    }
}
