using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>
/// Единый порядок владения Escape в каталоге: ровно один хозяин на нажатие,
/// от самого частного к самому общему —
/// перетаскивание → пикер света → измерение → пипетка → облачко подсказки →
/// подтверждение удаления → контекстное меню → меню группы →
/// выделение плитки каталога → схлопывание каталога →
/// панель дня/ночи → панель музыки. Нижний по списку не спрашивается, если верхний
/// уже забрал нажатие — каждый тест ниже это доказывает встречным входом: включает
/// хозяина ВЫШЕ и проверяет, что владение не спускается ниже.
/// </summary>
public class EscapeOwnershipTests
{
    [Test]
    public void NothingClaims_ResolvesToNone()
    {
        Assert.AreEqual(EscapeOwner.None, EscapeOwnership.Resolve(new EscapeClaims()),
            "если ни один потребитель не активен, Escape не должен доставаться никому");
    }

    [Test]
    public void Dragging_WinsOverEveryOtherClaim()
    {
        var claims = new EscapeClaims
        {
            Dragging = true,
            LightPicking = true,
            Measuring = true,
            Eyedropping = true,
            ConfirmArmed = true,
            ContextMenuOpen = true,
            GroupMenuOpen = true,
            CatalogTileSelected = true,
            CatalogCollapsible = true,
            DayNightOpen = true,
            MusicOpen = true,
        };
        Assert.AreEqual(EscapeOwner.ElementDrag, EscapeOwnership.Resolve(claims),
            "активное перетаскивание — самое частное действие, отменяется первым независимо " +
            "от того, что ещё открыто");
    }

    [Test]
    public void ModalDialog_WinsOverEveryToolAndPanel_ButNotOverDragging()
    {
        var claims = new EscapeClaims
        {
            ModalOpen = true,
            LightPicking = true,
            Measuring = true,
            Eyedropping = true,
            HintOpen = true,
            ConfirmArmed = true,
            ContextMenuOpen = true,
            GroupMenuOpen = true,
            CatalogTileSelected = true,
            CatalogCollapsible = true,
            DayNightOpen = true,
            MusicOpen = true,
            LevelsWindowOpen = true,
        };
        Assert.AreEqual(EscapeOwner.ModalDialog, EscapeOwnership.Resolve(claims),
            "модальный диалог перекрывает всё под подложкой: Escape закрывает его («Отмена»), а не "
            + "панель или инструмент, которых пользователь сейчас не видит (dialogs.md, D5)");
        claims.Dragging = true;
        Assert.AreEqual(EscapeOwner.ElementDrag, EscapeOwnership.Resolve(claims),
            "перетаскивание — единственное, что старше диалога: диалог может всплыть, пока деталь в руке");
    }

    [Test]
    public void LightPick_WinsOverEverythingBelowIt_ButNotOverDragging()
    {
        var claims = new EscapeClaims
        {
            LightPicking = true,
            Measuring = true,
            Eyedropping = true,
            ConfirmArmed = true,
            ContextMenuOpen = true,
            GroupMenuOpen = true,
            CatalogTileSelected = true,
            CatalogCollapsible = true,
            DayNightOpen = true,
            MusicOpen = true,
        };
        Assert.AreEqual(EscapeOwner.LightPick, EscapeOwnership.Resolve(claims),
            "пикер связи со светом заведён из открытого контекстного меню — его собственная " +
            "отмена обязана забрать Escape раньше, чем закрытие меню");

        claims.Dragging = true;
        Assert.AreEqual(EscapeOwner.ElementDrag, EscapeOwnership.Resolve(claims),
            "но перетаскивание всё равно старше пикера света");
    }

    [Test]
    public void Measure_WinsOverEverythingBelowIt_ButNotOverDragOrLightPick()
    {
        var claims = new EscapeClaims
        {
            Measuring = true,
            Eyedropping = true,
            ConfirmArmed = true,
            ContextMenuOpen = true,
            GroupMenuOpen = true,
            CatalogTileSelected = true,
            CatalogCollapsible = true,
            DayNightOpen = true,
            MusicOpen = true,
        };
        Assert.AreEqual(EscapeOwner.Measure, EscapeOwnership.Resolve(claims),
            "один шаг измерения отменяется раньше любой панели");

        claims.LightPicking = true;
        Assert.AreEqual(EscapeOwner.LightPick, EscapeOwnership.Resolve(claims));

        claims.LightPicking = false;
        claims.Dragging = true;
        Assert.AreEqual(EscapeOwner.ElementDrag, EscapeOwnership.Resolve(claims));
    }

    [Test]
    public void Eyedropper_WinsOverEverythingBelowIt_ButNotOverDragLightPickOrMeasure()
    {
        var claims = new EscapeClaims
        {
            Eyedropping = true,
            ConfirmArmed = true,
            ContextMenuOpen = true,
            GroupMenuOpen = true,
            CatalogTileSelected = true,
            CatalogCollapsible = true,
            DayNightOpen = true,
            MusicOpen = true,
        };
        claims.HintOpen = true;
        Assert.AreEqual(EscapeOwner.Eyedropper, EscapeOwnership.Resolve(claims));

        claims.Measuring = true;
        Assert.AreEqual(EscapeOwner.Measure, EscapeOwnership.Resolve(claims));
    }

    [Test]
    public void HintBubble_WinsOverConfirmDeleteAndPanels_ButNotOverAnyToolMode()
    {
        var claims = new EscapeClaims
        {
            HintOpen = true,
            ConfirmArmed = true,
            ContextMenuOpen = true,
            GroupMenuOpen = true,
            CatalogTileSelected = true,
            CatalogCollapsible = true,
            DayNightOpen = true,
            MusicOpen = true,
        };
        Assert.AreEqual(EscapeOwner.HintBubble, EscapeOwnership.Resolve(claims),
            "приколотое кликом облачко подсказки нарисовано ПОВЕРХ панели и её кнопок — " +
            "Escape целится в верхнее, а не в то, что под ним");

        claims.Eyedropping = true;
        Assert.AreEqual(EscapeOwner.Eyedropper, EscapeOwnership.Resolve(claims),
            "но режим инструмента — состояние всей сцены, он старше любого облачка");

        claims.Eyedropping = false;
        claims.Dragging = true;
        Assert.AreEqual(EscapeOwner.ElementDrag, EscapeOwnership.Resolve(claims));
    }

    [Test]
    public void ConfirmDelete_WinsOverBothMenusAndCatalogAndPanels_ButNotOverToolModesOrDrag()
    {
        var claims = new EscapeClaims
        {
            ConfirmArmed = true,
            ContextMenuOpen = true,
            GroupMenuOpen = true,
            CatalogTileSelected = true,
            CatalogCollapsible = true,
            DayNightOpen = true,
            MusicOpen = true,
        };
        Assert.AreEqual(EscapeOwner.ConfirmDelete, EscapeOwnership.Resolve(claims),
            "взведённая вторым кликом кнопка удаления — самая частная деталь открытой панели, " +
            "отменяется раньше закрытия самой панели");

        claims.HintOpen = true;
        Assert.AreEqual(EscapeOwner.HintBubble, EscapeOwnership.Resolve(claims),
            "но открытое поверх кнопки облачко подсказки Escape закроет первым — " +
            "иначе один Escape снял бы взвод, а облачко осталось бы висеть");

        claims.HintOpen = false;
        claims.Eyedropping = true;
        Assert.AreEqual(EscapeOwner.Eyedropper, EscapeOwnership.Resolve(claims));
    }

    [Test]
    public void ContextMenu_WinsOverGroupMenuAndCatalogAndPanels_ButNotOverConfirmDelete()
    {
        var claims = new EscapeClaims
        {
            ContextMenuOpen = true,
            GroupMenuOpen = true,
            CatalogTileSelected = true,
            CatalogCollapsible = true,
            DayNightOpen = true,
            MusicOpen = true,
        };
        Assert.AreEqual(EscapeOwner.ContextMenu, EscapeOwnership.Resolve(claims));

        claims.ConfirmArmed = true;
        Assert.AreEqual(EscapeOwner.ConfirmDelete, EscapeOwnership.Resolve(claims),
            "пока кнопка удаления внутри контекстного меню взведена, Escape обязан её " +
            "разоружить и оставить меню открытым");
    }

    [Test]
    public void GroupMenu_WinsOverCatalogAndPanels_ButNotOverContextMenu()
    {
        var claims = new EscapeClaims
        {
            GroupMenuOpen = true,
            CatalogTileSelected = true,
            CatalogCollapsible = true,
            DayNightOpen = true,
            MusicOpen = true,
        };
        Assert.AreEqual(EscapeOwner.GroupMenu, EscapeOwnership.Resolve(claims));

        claims.ContextMenuOpen = true;
        Assert.AreEqual(EscapeOwner.ContextMenu, EscapeOwnership.Resolve(claims));
    }

    [Test]
    public void CatalogTileSelection_WinsOverCollapseAndPanels_ButNotOverGroupMenu()
    {
        var claims = new EscapeClaims
        {
            CatalogTileSelected = true,
            CatalogCollapsible = true,
            DayNightOpen = true,
            MusicOpen = true,
        };
        Assert.AreEqual(EscapeOwner.CatalogTileSelection, EscapeOwnership.Resolve(claims),
            "выделенная плитка снимается раньше, чем каталог схлопывается или закрывается " +
            "фоновая панель");

        claims.GroupMenuOpen = true;
        Assert.AreEqual(EscapeOwner.GroupMenu, EscapeOwnership.Resolve(claims));
    }

    [Test]
    public void CatalogCollapse_WinsOverPanels_ButNotOverTileSelection()
    {
        var claims = new EscapeClaims
        {
            CatalogCollapsible = true,
            DayNightOpen = true,
            MusicOpen = true,
        };
        Assert.AreEqual(EscapeOwner.CatalogCollapse, EscapeOwnership.Resolve(claims));

        claims.CatalogTileSelected = true;
        Assert.AreEqual(EscapeOwner.CatalogTileSelection, EscapeOwnership.Resolve(claims));
    }

    [Test]
    public void DayNightPanel_WinsOverMusicPanel_ButNotOverCatalogCollapse()
    {
        var claims = new EscapeClaims { DayNightOpen = true, MusicOpen = true };
        Assert.AreEqual(EscapeOwner.DayNightPanel, EscapeOwnership.Resolve(claims));

        claims.CatalogCollapsible = true;
        Assert.AreEqual(EscapeOwner.CatalogCollapse, EscapeOwnership.Resolve(claims));
    }

    [Test]
    public void MusicPanel_WinsOverLevelsWindow_ButNotOverDayNightPanel()
    {
        var claims = new EscapeClaims { MusicOpen = true, LevelsWindowOpen = true };
        Assert.AreEqual(EscapeOwner.MusicPanel, EscapeOwnership.Resolve(claims));

        claims.DayNightOpen = true;
        Assert.AreEqual(EscapeOwner.DayNightPanel, EscapeOwnership.Resolve(claims),
            "музыкальная панель уступает более специфичному хозяину");
    }

    [Test]
    public void LevelsWindow_IsTheLastResort()
    {
        Assert.AreEqual(EscapeOwner.LevelsWindow,
            EscapeOwnership.Resolve(new EscapeClaims { LevelsWindowOpen = true }));

        var claims = new EscapeClaims { LevelsWindowOpen = true, MusicOpen = true };
        Assert.AreEqual(EscapeOwner.MusicPanel, EscapeOwnership.Resolve(claims),
            "окно «Этажи» — самый общий, последний хозяин в списке");
    }
}
