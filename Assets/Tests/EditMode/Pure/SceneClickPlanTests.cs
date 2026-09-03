using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>Вся лестница решений «что делает клик по сцене», вынутая из
/// SelectionManager.HandleClickOnElement. Раньше она жила внутри MonoBehaviour и
/// проверялась только руками; двойной клик по выключателю добавлен в неё как
/// новая ступень, и порядок ступеней здесь — контракт.
///
/// Порядок: Ctrl сильнее всего, затем группа (двойной клик по сгруппированному
/// элементу по-прежнему открывает модуль, даже если это выключатель), затем
/// переключение выключателя, и только потом обычное выделение. Так добавление
/// новой ступени не отняло ни одного прежнего жеста.</summary>
public class SceneClickPlanTests
{
    private static SceneClickInput OnElement() => new SceneClickInput
    {
        HasElement = true,
        Interactable = true,
    };

    [Test]
    public void SingleClick_OnALightSwitch_SelectsIt()
    {
        var input = OnElement();
        input.Kind = ActivationKind.LightSwitch;

        Assert.AreEqual(SceneClickAction.Select, SceneClickPlan.Decide(input),
            "одиночный клик по выключателю обязан остаться выделением — иначе "
            + "его нельзя будет ни подвинуть, ни открыть в панели свойств");
    }

    [Test]
    public void DoubleClick_OnALightSwitch_TogglesItInsteadOfSelecting()
    {
        var input = OnElement();
        input.Kind = ActivationKind.LightSwitch;
        input.RepeatsElement = true;

        Assert.AreEqual(SceneClickAction.ActivateSwitch, SceneClickPlan.Decide(input),
            "это и есть заказанный жест: двойной клик переключает свет, а не выделяет");
    }

    [Test]
    public void DoubleClick_OnAnOrdinaryBoard_SelectsAsBefore()
    {
        var input = OnElement();
        input.RepeatsElement = true;

        Assert.AreEqual(SceneClickAction.Select, SceneClickPlan.Decide(input),
            "двойной клик по не-выключателю обязан вести себя ровно как раньше");
    }

    [Test]
    public void DoubleClick_OnADoor_SelectsAsBefore()
    {
        var input = OnElement();
        input.Kind = ActivationKind.Openable;
        input.RepeatsElement = true;

        Assert.AreEqual(SceneClickAction.Select, SceneClickPlan.Decide(input),
            "дверь открывается клавишей E, а не двойным кликом: расширять жест на "
            + "IOpenable задача не просила, и молча менять его нельзя");
    }

    [Test]
    public void DoubleClick_OnAGroupedLightSwitch_EntersModuleEdit()
    {
        var input = OnElement();
        input.Kind = ActivationKind.LightSwitch;
        input.InGroup = true;
        input.RepeatsGroup = true;
        input.RepeatsElement = true;

        Assert.AreEqual(SceneClickAction.EnterModuleEdit, SceneClickPlan.Decide(input),
            "у двойного клика по элементу модуля значение уже было — вход в "
            + "редактирование модуля; группа сильнее переключения");
    }

    [Test]
    public void SingleClick_OnAGroupedElement_SelectsTheWholeGroup()
    {
        var input = OnElement();
        input.InGroup = true;

        Assert.AreEqual(SceneClickAction.SelectGroup, SceneClickPlan.Decide(input));
    }

    [Test]
    public void CtrlDoubleClick_OnALightSwitch_OnlyChangesTheSelection()
    {
        var input = OnElement();
        input.Kind = ActivationKind.LightSwitch;
        input.RepeatsElement = true;
        input.CtrlHeld = true;

        Assert.AreEqual(SceneClickAction.ToggleInSelection, SceneClickPlan.Decide(input),
            "Ctrl — это набор мультивыделения; переключать свет во время набора "
            + "пользователь не просил, и случайное срабатывание тут дороже удобства");
    }

    [Test]
    public void Click_OnAnElementInsideAMultiSelection_CollapsesToIt()
    {
        var input = OnElement();
        input.InMultiSelection = true;

        Assert.AreEqual(SceneClickAction.CollapseToClicked, SceneClickPlan.Decide(input),
            "прежнее правило: клик по уже выделенной детали внутри группы выделенных "
            + "схлопывает выбор до неё на отпускании кнопки");
    }

    [Test]
    public void DoubleClick_OnALightSwitchInsideAMultiSelection_TogglesIt()
    {
        var input = OnElement();
        input.Kind = ActivationKind.LightSwitch;
        input.RepeatsElement = true;
        input.InMultiSelection = true;

        Assert.AreEqual(SceneClickAction.ActivateSwitch, SceneClickPlan.Decide(input),
            "переключение стоит в лестнице ВЫШЕ схлопывания: иначе второй клик по "
            + "выключателю из мультивыделения просто схлопнул бы выбор и ничего не сделал");
    }

    [Test]
    public void ActivateSwitch_KeepsTheSelectionSnapshot()
    {
        Assert.IsTrue(SceneClickPlan.KeepsSelectionSnapshot(SceneClickAction.ActivateSwitch),
            "снимок выделения берётся ДО первого клика жеста; второй клик обязан его "
            + "сохранить, иначе третий быстрый клик вернул бы уже испорченный выбор");
        Assert.IsFalse(SceneClickPlan.KeepsSelectionSnapshot(SceneClickAction.Select),
            "обычный клик, наоборот, обновляет снимок — он и есть начало нового жеста");
    }

    [Test]
    public void ActivateSwitch_IsRemembered_SoAThirdClickTogglesAgain()
    {
        Assert.IsTrue(SceneClickPlan.RecordsClick(SceneClickAction.ActivateSwitch),
            "щёлкать выключателем подряд — нормально; каждый следующий быстрый клик "
            + "переключает снова, и выделение каждый раз возвращается к исходному");
    }

    [Test]
    public void ModuleEditActions_AreNotRemembered()
    {
        Assert.IsFalse(SceneClickPlan.RecordsClick(SceneClickAction.ModuleSelect),
            "внутри редактирования модуля прежний код память о клике не обновлял; "
            + "сохраняем это, чтобы выход из модуля не считал жест продолжающимся");
        Assert.IsFalse(SceneClickPlan.RecordsClick(SceneClickAction.ModuleToggleInSelection));
        Assert.IsFalse(SceneClickPlan.RecordsClick(SceneClickAction.DeselectAll));
        Assert.IsFalse(SceneClickPlan.RecordsClick(SceneClickAction.Ignore));
    }

    [Test]
    public void ClickInsideModuleEdit_OnAnEditableElement_JustSelects()
    {
        var input = OnElement();
        input.Kind = ActivationKind.LightSwitch;
        input.ModuleEditActive = true;
        input.ModuleEditable = true;
        input.RepeatsElement = true;

        Assert.AreEqual(SceneClickAction.ModuleSelect, SceneClickPlan.Decide(input),
            "в режиме редактирования модуля клик выделяет деталь модуля и ничего "
            + "больше — переключение туда не лезет");
    }

    [Test]
    public void ClickInsideModuleEdit_OnAForeignElement_IsIgnored()
    {
        var input = OnElement();
        input.ModuleEditActive = true;
        input.ModuleEditable = false;

        Assert.AreEqual(SceneClickAction.Ignore, SceneClickPlan.Decide(input),
            "чужая деталь в режиме модуля не выделяется и выделения не сбрасывает");
    }

    [Test]
    public void ClickOnEmptySpace_DeselectsAll()
    {
        var input = new SceneClickInput { HasElement = false, Interactable = false };

        Assert.AreEqual(SceneClickAction.DeselectAll, SceneClickPlan.Decide(input));
    }

    [Test]
    public void CtrlClickOnEmptySpace_KeepsTheSelection()
    {
        var input = new SceneClickInput { HasElement = false, CtrlHeld = true };

        Assert.AreEqual(SceneClickAction.Ignore, SceneClickPlan.Decide(input),
            "Ctrl набирает выделение — промах мимо детали не имеет права его стереть");
    }

    [Test]
    public void ClickOnANonInteractableElement_DeselectsAll()
    {
        var input = OnElement();
        input.Interactable = false;
        input.Kind = ActivationKind.LightSwitch;
        input.RepeatsElement = true;

        Assert.AreEqual(SceneClickAction.DeselectAll, SceneClickPlan.Decide(input),
            "выключатель в выключенной категории редактирования не переключается "
            + "даже двойным кликом: он для пользователя сейчас не существует");
    }
}
