using System;
using System.Linq;
using KitchenDesigner.Core;
using NUnit.Framework;

/// <summary>Подписи ящика переехали из ядра геометрии в переводимый слой: ядро собирается
/// отдельной сборкой и до таблиц перевода не дотягивается. Проверки остались те же —
/// различимость и непустота, — плюс русский исходник для тех строк, которые человек сверяет
/// глазами в спецификации.</summary>
public class DrawerTextsTests
{
    [Test]
    public void TypeLabel_IsDistinctAndNonEmpty_ForEveryType()
    {
        var labels = DrawerConstants.Types.Select(DrawerTexts.TypeLabel).ToList();
        CollectionAssert.AllItemsAreUnique(labels);
        Assert.IsTrue(labels.All(l => l.Length > 0));
    }

    [Test]
    public void ColorName_Russian()
    {
        Assert.AreEqual("Антрацит", DrawerTexts.ColorName(DrawerColor.Anthracite));
        Assert.AreEqual("Белый", DrawerTexts.ColorName(DrawerColor.White));
        Assert.AreEqual("Чёрный", DrawerTexts.ColorName(DrawerColor.Black));
    }

    [Test]
    public void DefaultName_IsDistinctPerSystem()
    {
        var names = Enum.GetValues(typeof(DrawerSystem)).Cast<DrawerSystem>().Select(DrawerTexts.DefaultName).ToList();
        CollectionAssert.AllItemsAreUnique(names, "имена систем уходят в спецификацию, где их читает человек на производстве");
        Assert.IsTrue(names.All(n => n.Length > 0));
    }

    [Test]
    public void CycleButtonLabel_SaysWhatTheNextClickDoes()
    {
        StringAssert.Contains("Открыть", DrawerTexts.CycleButtonLabel(DoubleDrawerState.Closed));
        StringAssert.Contains("верхний", DrawerTexts.CycleButtonLabel(DoubleDrawerState.BothOpen));
        StringAssert.Contains("всё", DrawerTexts.CycleButtonLabel(DoubleDrawerState.LowerOnly));
    }
}
