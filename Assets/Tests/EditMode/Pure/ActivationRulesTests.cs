using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>Что именно «включается» жестом. Две поверхности спрашивают одно и то
/// же правило: клавиша E (CameraController.ActivateSelected) и двойной клик по
/// сцене (SceneClickPlan). Раньше E знала только про IOpenable, а выключатель
/// переключался единственным способом — галочкой в панели свойств.
///
/// Клавиша шире жеста мышью намеренно: дверь уже открывается по E, а двойной
/// клик по двери — это не «открыть», там его значение занято другими правилами
/// (вход в модуль у сгруппированного элемента), и менять его никто не просил.</summary>
public class ActivationRulesTests
{
    [Test]
    public void Hotkey_OnALightSwitch_Activates()
    {
        Assert.IsTrue(ActivationRules.RespondsToHotkey(ActivationKind.LightSwitch),
            "клавиша E на выделенном выключателе обязана его переключать — ради этого "
            + "правила задача и заводилась");
    }

    [Test]
    public void Hotkey_OnAnOpenable_StillActivates()
    {
        Assert.IsTrue(ActivationRules.RespondsToHotkey(ActivationKind.Openable),
            "E давно открывает дверь/окно/фасад/ящик; добавление выключателя не имеет "
            + "права отнять у клавиши её прежнее значение");
    }

    [Test]
    public void Hotkey_OnAnOrdinaryBoard_DoesNothing()
    {
        Assert.IsFalse(ActivationRules.RespondsToHotkey(ActivationKind.None),
            "у обычной детали нет состояния «включено/открыто», и E не должна "
            + "порождать пустой шаг отмены");
    }

    [Test]
    public void DoubleClick_OnALightSwitch_Activates()
    {
        Assert.IsTrue(ActivationRules.RespondsToDoubleClick(ActivationKind.LightSwitch),
            "двойной клик по выключателю — второй способ его переключить, помимо E");
    }

    [Test]
    public void DoubleClick_OnAnOpenable_DoesNothing()
    {
        Assert.IsFalse(ActivationRules.RespondsToDoubleClick(ActivationKind.Openable),
            "двойным кликом двери не открывают: этот жест в проекте уже занят входом "
            + "в редактирование модуля, и расширять его на IOpenable никто не просил");
    }

    [Test]
    public void DoubleClick_OnAnOrdinaryBoard_DoesNothing()
    {
        Assert.IsFalse(ActivationRules.RespondsToDoubleClick(ActivationKind.None),
            "по обычной детали двойной клик обязан остаться обычным выделением");
    }
}
