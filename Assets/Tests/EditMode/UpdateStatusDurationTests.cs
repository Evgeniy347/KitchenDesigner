using NUnit.Framework;
using KitchenDesigner.Core.UI;
using KitchenDesigner.Core.Update;

public class UpdateStatusDurationTests
{
    [Test]
    public void TransientSeconds_IsNotShorterThanTheStatusBarWillEverShowAMessage()
    {
        Assert.GreaterOrEqual(UpdateStrings.TransientSeconds, StatusBarUI.MinSeconds,
            "StatusBarUI дополнительно клампит длительность к своему MinSeconds, "
            + "поэтому число меньше него — не «покажем короче», а молча тот же MinSeconds: "
            + "настройка, которая ничего не настраивает");
    }
}
