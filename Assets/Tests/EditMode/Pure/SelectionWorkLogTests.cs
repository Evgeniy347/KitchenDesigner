using System;
using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>Счётчик РАБОТЫ выделения, а не секундомер: тесты по времени в batch флаки и
/// зеленеют на выкинутом раннем выходе, а число созданных красок, перекрашенных рендереров,
/// заказанных проверок сцены и разосланных оповещений краснеет сразу и в обе стороны.
///
/// Каждая единица здесь куплена дампом: одна «проверка сцены» — это
/// <c>PartRegistry.GetAll</c> плюс полная <c>ConstraintValidator.Validate</c>, 17,3 мс на
/// сцене в 411 деталей; одно «оповещение» — это весь хвост подписчиков выделения, ради
/// цены которого заведён <see cref="ListenerCostLog"/>.</summary>
public class SelectionWorkLogTests
{
    [SetUp]
    public void SetUp() => SelectionWorkLog.Forget();

    [TearDown]
    public void TearDown() => SelectionWorkLog.Forget();

    [Test]
    public void EveryKindOfWork_HasACaption()
    {
        Assert.AreEqual(Enum.GetValues(typeof(SelectionWork)).Length, SelectionWorkLog.KindsCounted,
            "новый вид работы без подписи роняет счётчик на индексе — а до первого падения "
            + "он же и молчит про эту работу");

        foreach (SelectionWork what in Enum.GetValues(typeof(SelectionWork)))
            Assert.IsNotEmpty(SelectionWorkLog.CaptionOf(what), what.ToString());
    }

    [Test]
    public void AnEmptyFrame_SaysNothing()
    {
        Assert.AreEqual(string.Empty, SelectionWorkLog.Take(),
            "кадр без выделения не имеет права печатать строку о выделении");
    }

    [Test]
    public void NotedWork_IsCounted_PerKind()
    {
        SelectionWorkLog.Note(SelectionWork.TintCreated);
        SelectionWorkLog.Note(SelectionWork.TintCreated);
        SelectionWorkLog.Note(SelectionWork.ListenersNotified);

        Assert.AreEqual(2, SelectionWorkLog.Count(SelectionWork.TintCreated));
        Assert.AreEqual(1, SelectionWorkLog.Count(SelectionWork.ListenersNotified));
        Assert.AreEqual(0, SelectionWorkLog.Count(SelectionWork.SceneValidationAsked),
            "нетронутый вид работы обязан остаться нулём, иначе счётчик сливает виды");
    }

    [Test]
    public void Take_PrintsOnlyTheWorkThatHappened_AndForgetsIt()
    {
        SelectionWorkLog.Note(SelectionWork.SceneValidationAsked);
        SelectionWorkLog.Note(SelectionWork.ListenersNotified);

        var line = SelectionWorkLog.Take();

        StringAssert.Contains("проверок сцены 1", line);
        StringAssert.Contains("оповещений 1", line);
        StringAssert.DoesNotContain("красок", line,
            "ноль, напечатанный как работа, читается как «здесь что-то делали»");
        Assert.AreEqual(string.Empty, SelectionWorkLog.Take(),
            "следующий кадр не имеет права унаследовать эту работу");
    }
}
