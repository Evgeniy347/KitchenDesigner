using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Сцена-зависимая половина «включения» элемента жестом. Чистая
/// половина — какой жест что делает — живёт в SceneClickPlanTests и
/// ActivationRulesTests и гоняется под dotnet; здесь проверяется то, что без
/// сцены не проверить: что переключение выключателя идёт через SwitchPower и
/// потому ОТМЕНЯЕТСЯ, и что клавиша E разбирает выделение по видам.
///
/// Отменяемость тут не украшение, а причина, по которой ElementActivator не
/// пишет <c>source.IsOn = !source.IsOn</c> напрямую: голое присваивание не
/// оставляет шага в CommandStack, и свет, погашенный клавишей, нельзя было бы
/// вернуть Ctrl+Z. Ровно один шаг — тоже требование: пользователь нажал одну
/// клавишу, и одного Ctrl+Z обязано хватить.</summary>
public class ElementActivatorTests
{
    [SetUp]
    public void SetUp()
    {
        PartRegistry.Clear();
        CommandStack.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        CommandStack.Clear();
        PartRegistry.Clear();
    }

    private static ILightSwitch MakeSwitch(bool on) =>
        ElementFactory.CreateLightSwitch(WallDeviceSpec.Default, on, null,
            "Выключатель", Vector3.zero).GetComponent<ILightSwitch>();

    [Test]
    public void KindOf_TellsASwitchFromADoorAndFromAPlainBoard()
    {
        var source = MakeSwitch(true);
        var board = ElementFactory.CreatePart(new Vector3Int(600, 18, 300),
            "Полка", Vector3.zero).GetComponent<KitchenElement>();

        Assert.AreEqual(ActivationKind.LightSwitch, ElementActivator.KindOf(source));
        Assert.AreEqual(ActivationKind.None, ElementActivator.KindOf(board),
            "у обычной детали нечего включать, и жест обязан пройти мимо неё");
    }

    [Test]
    public void Activate_OnALightSwitch_FlipsIt()
    {
        var source = MakeSwitch(true);

        Assert.IsTrue(ElementActivator.Activate(source), "выключатель обязан отозваться");
        Assert.IsFalse(source.IsOn, "включённый выключатель после жеста гаснет");
    }

    [Test]
    public void Activate_OnALightSwitch_IsUndoneByASingleStep()
    {
        var source = MakeSwitch(true);

        ElementActivator.Activate(source);
        Assert.IsFalse(source.IsOn, "жест обязан сработать до отмены, иначе тест проверяет пустоту");

        CommandStack.Undo();

        Assert.IsTrue(source.IsOn,
            "переключение обязано идти через SwitchPower и ложиться в CommandStack: "
            + "голое присваивание IsOn не отменяется, и погашенный клавишей свет было "
            + "бы нечем вернуть");
    }

    [Test]
    public void Activate_OnAPlainBoard_DoesNothingAndLeavesNoUndoStep()
    {
        var board = ElementFactory.CreatePart(new Vector3Int(600, 18, 300),
            "Полка", Vector3.zero).GetComponent<KitchenElement>();

        Assert.IsFalse(ElementActivator.Activate(board));
        Assert.IsFalse(CommandStack.CanUndo,
            "пустой жест не имеет права засорять историю: иначе Ctrl+Z после нажатия E "
            + "по обычной детали откатывал бы чужую правку");
    }

    [Test]
    public void ActivateAll_OnAMixedSelection_TouchesOnlyWhatRespondsToTheHotkey()
    {
        var first = MakeSwitch(true);
        var second = MakeSwitch(false);
        var board = ElementFactory.CreatePart(new Vector3Int(600, 18, 300),
            "Полка", Vector3.zero).GetComponent<KitchenElement>();

        int touched = ElementActivator.ActivateAll(new[]
        {
            (KitchenElement)first, (KitchenElement)second, board,
        });

        Assert.AreEqual(2, touched, "деталь без состояния в счёт не идёт");
        Assert.IsFalse(first.IsOn, "клавиша E переключает КАЖДЫЙ выделенный выключатель");
        Assert.IsTrue(second.IsOn,
            "каждый переключается от СВОЕГО состояния, а не приводится к общему: "
            + "иначе выделив два разных выключателя, пользователь получил бы их "
            + "одинаковыми, чего он не просил");
    }
}
