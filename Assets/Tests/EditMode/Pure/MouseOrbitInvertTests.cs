using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class MouseOrbitInvertTests
{
    [Test]
    public void Apply_NeitherFlagSet_ReturnsTheDeltaUnchanged()
    {
        var delta = new Vector2(3f, -4f);

        var result = MouseOrbitInvert.Apply(delta, invertX: false, invertY: false);

        Assert.AreEqual(delta, result, "сегодняшнее поведение (обе инверсии выключены) не должно менять дельту");
    }

    [Test]
    public void Apply_InvertXOnly_FlipsX_AndLeavesYUntouched()
    {
        var delta = new Vector2(3f, -4f);

        var result = MouseOrbitInvert.Apply(delta, invertX: true, invertY: false);

        Assert.AreEqual(-3f, result.x, "горизонтальная инверсия обязана перевернуть только X");
        Assert.AreEqual(-4f, result.y, "и не тронуть Y");
    }

    [Test]
    public void Apply_InvertYOnly_FlipsY_AndLeavesXUntouched()
    {
        var delta = new Vector2(3f, -4f);

        var result = MouseOrbitInvert.Apply(delta, invertX: false, invertY: true);

        Assert.AreEqual(3f, result.x, "вертикальная инверсия обязана не трогать X");
        Assert.AreEqual(4f, result.y, "и перевернуть только Y");
    }

    [Test]
    public void Apply_BothFlagsSet_FlipsBothAxesIndependently()
    {
        var delta = new Vector2(3f, -4f);

        var result = MouseOrbitInvert.Apply(delta, invertX: true, invertY: true);

        Assert.AreEqual(new Vector2(-3f, 4f), result,
            "оба флага действуют по своей оси независимо друг от друга, а не как один общий переключатель");
    }
}
