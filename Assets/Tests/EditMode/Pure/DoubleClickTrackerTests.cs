using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>Память о предыдущем клике, вынутая из SelectionManager. Раньше она
/// помнила ТОЛЬКО номер группы, поэтому «второй клик по тому же элементу» вне
/// группы отличить было нечем: у всех негруппированных элементов ключ был 0.
///
/// Два вопроса намеренно разные. Повтор по ГРУППЕ засчитывается и когда второй
/// клик пришёлся на другую деталь той же группы — так вход в редактирование
/// модуля работал и работает. Повтор по ЭЛЕМЕНТУ требует и того же элемента, и
/// того, что прошлый клик был вне группы, иначе id группы и id объекта могли бы
/// совпасть числом и жест сработал бы не там.</summary>
public class DoubleClickTrackerTests
{
    private const int Switch = 4242;
    private const int OtherElement = 777;
    private const int Group = 3;

    [Test]
    public void FirstClickEver_IsNotARepeat()
    {
        var tracker = new DoubleClickTracker();

        Assert.IsFalse(tracker.RepeatsElement(Switch, 0f),
            "нулевое время в EditMode не должно выглядеть как «уже кликали»: "
            + "иначе первый же клик по выключателю переключал бы его");
        Assert.IsFalse(tracker.RepeatsGroup(Group, 0f),
            "то же самое для группы: первый клик обязан только выделить модуль");
    }

    [Test]
    public void SecondClickOnTheSameElement_WithinTheWindow_IsARepeat()
    {
        var tracker = new DoubleClickTracker();
        tracker.Remember(0, Switch, 10f);

        Assert.IsTrue(tracker.RepeatsElement(Switch, 10f + DoubleClickTracker.WindowSeconds * 0.5f),
            "клик внутри окна 0,35 с — это двойной клик; окно взято тем же, каким оно "
            + "было у входа в редактирование модуля, чтобы два жеста не расходились");
    }

    [Test]
    public void SecondClickOnTheSameElement_AfterTheWindow_IsNotARepeat()
    {
        var tracker = new DoubleClickTracker();
        tracker.Remember(0, Switch, 10f);

        Assert.IsFalse(tracker.RepeatsElement(Switch, 10f + DoubleClickTracker.WindowSeconds + 0.01f),
            "два раздельных клика по выключателю — это два выделения, а не переключение");
    }

    [Test]
    public void SecondClickOnAnotherElement_IsNotARepeat()
    {
        var tracker = new DoubleClickTracker();
        tracker.Remember(0, Switch, 10f);

        Assert.IsFalse(tracker.RepeatsElement(OtherElement, 10.1f),
            "быстрый клик по соседней детали не имеет права переключить выключатель");
    }

    [Test]
    public void ElementRepeat_AfterAGroupClick_IsNotCounted()
    {
        var tracker = new DoubleClickTracker();
        tracker.Remember(Group, Switch, 10f);

        Assert.IsFalse(tracker.RepeatsElement(Switch, 10.1f),
            "id группы и id объекта — разные пространства чисел; повтор по элементу "
            + "засчитывается только когда прошлый клик был вне группы");
    }

    [Test]
    public void GroupRepeat_OnAnotherMemberOfTheSameGroup_IsCounted()
    {
        var tracker = new DoubleClickTracker();
        tracker.Remember(Group, Switch, 10f);

        Assert.IsTrue(tracker.RepeatsGroup(Group, 10.1f),
            "вход в редактирование модуля срабатывал и когда второй клик пришёлся "
            + "на другую деталь того же модуля — это поведение не меняется");
    }

    [Test]
    public void GroupRepeat_WithoutAGroup_IsNeverCounted()
    {
        var tracker = new DoubleClickTracker();
        tracker.Remember(0, Switch, 10f);

        Assert.IsFalse(tracker.RepeatsGroup(0, 10.1f),
            "нулевой номер группы означает «группы нет», а не «та же самая группа»");
    }

    [Test]
    public void Forget_DropsTheHistory()
    {
        var tracker = new DoubleClickTracker();
        tracker.Remember(0, Switch, 10f);
        tracker.Forget();

        Assert.IsFalse(tracker.RepeatsElement(Switch, 10.1f),
            "после сброса следующий клик обязан считаться первым");
    }
}
