using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using KitchenDesigner.Core.UI;

public class PointerHoverTests
{
    private GameObject? _go;

    [SetUp]
    public void Setup() => _go = new GameObject("HoverTarget");

    [TearDown]
    public void Teardown()
    {
        if (_go != null) Object.DestroyImmediate(_go);
    }

    [Test]
    public void PointerHover_HandlesOnlyEnterAndExit_NeverTheScrollWheel()
    {
        Assert.IsTrue(typeof(IPointerEnterHandler).IsAssignableFrom(typeof(PointerHover)));
        Assert.IsTrue(typeof(IPointerExitHandler).IsAssignableFrom(typeof(PointerHover)));
        Assert.IsFalse(typeof(IScrollHandler).IsAssignableFrom(typeof(PointerHover)),
            "Наведение — отдельный компонент, а НЕ EventTrigger: тот реализует все "
            + "интерфейсы событий сразу, включая IScrollHandler, поэтому колесо над пунктом "
            + "доставалось ему и до ScrollRect списка не доходило — из трёх десятков декоров "
            + "было видно семь, остальные недоступны");
        Assert.IsFalse(typeof(EventTrigger).IsAssignableFrom(typeof(PointerHover)),
            "и подменять его EventTrigger'ом нельзя по той же причине");
    }

    [Test]
    public void PointerHover_Attach_RewiresTheSameComponent_InsteadOfAddingAnother()
    {
        int first = 0, second = 0;

        var a = PointerHover.Attach(_go!, () => first++, null);
        var b = PointerHover.Attach(_go!, () => second++, null);

        Assert.AreSame(a, b, "повторный Attach обязан переписать обработчики, а не плодить "
            + "компоненты: каждый лишний получал бы своё событие входа");
        Assert.AreEqual(1, _go!.GetComponents<PointerHover>().Length);

        a.OnPointerEnter(new PointerEventData(EventSystem.current));
        Assert.AreEqual(0, first, "старый обработчик отвязан");
        Assert.AreEqual(1, second, "работает только последний");
    }
}
