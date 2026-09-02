using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class DisplaySettingsTests
{
    [Test]
    public void TheAppOwnsItsWindowSize_IsFalseInTheEditor()
    {
        Assert.IsFalse(DisplaySettings.TheAppOwnsItsWindowSize(),
            "настройка режима окна влияет только на СОБРАННЫЙ плеер: в редакторе "
            + "Screen.SetResolution меняет размер игрового окна разработчика");
    }

    [Test]
    public void ApplyWindowMode_InTheEditor_LeavesTheScreenAlone()
    {
        int width = Screen.width;
        int height = Screen.height;

        DisplaySettings.ApplyWindowMode();

        Assert.AreEqual(width, Screen.width,
            "в редакторе ApplyWindowMode обязан быть пустой операцией");
        Assert.AreEqual(height, Screen.height,
            "то же по высоте: иначе прогон тестов переставлял бы окно редактора");
    }
}
