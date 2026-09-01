using System;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Measure;
using KitchenDesigner.Core.Tools;

public class ToolExecutionOrderTests
{
    private static int OrderOf(Type behaviour)
    {
        var attribute = (DefaultExecutionOrder?)Attribute.GetCustomAttribute(
            behaviour, typeof(DefaultExecutionOrder));
        return attribute == null ? 0 : attribute.order;
    }

    [Test]
    public void SelectionAndMove_StayAtTheDefaultOrder()
    {
        Assert.AreEqual(0, OrderOf(typeof(SelectionManager)),
            "остальные порядки заданы ОТНОСИТЕЛЬНО этого нуля; сдвинешь его — "
            + "и «раньше»/«позже» ниже станут ложью");
        Assert.AreEqual(0, OrderOf(typeof(ElementMover)));
    }

    [Test]
    public void ModesThatCaptureTheMouse_RunBeforeSelectionAndMove()
    {
        Assert.Less(OrderOf(typeof(EyedropperController)), OrderOf(typeof(SelectionManager)),
            "пипетка обязана выставить состояние кадра ДО того, как выделение и "
            + "перенос спросят ToolMode.MouseCaptured — иначе клик проскочит в выделение");
        Assert.Less(OrderOf(typeof(MeasureController)), OrderOf(typeof(SelectionManager)),
            "рулетка захватывает мышь тем же способом и обязана идти так же рано");
    }

    [Test]
    public void HandlesThatEatClicks_RunAfterSelectionAndMove()
    {
        Assert.Greater(OrderOf(typeof(ResizeHandleManager)), OrderOf(typeof(ElementMover)),
            "при недетерминированном порядке клик по новому объекту попадал в ещё не "
            + "убранную ручку прежнего выделения и давал ресайз вместо move");
        Assert.Greater(OrderOf(typeof(TextureOverlayHandles)), OrderOf(typeof(ElementMover)),
            "ручки накладок ловят клики по той же причине и в том же порядке");
    }
}
