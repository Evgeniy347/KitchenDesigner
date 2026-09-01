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

    [Test]
    public void TheSource_StillRefusesToSetTheResolutionUnderWebGl()
    {
        var path = System.IO.Path.Combine(
            KitchenDesigner.Tests.Geometry.RepoPaths.Subdir("Assets", "Scripts", "Core", "Validation"),
            "DisplaySettings.cs");
        var source = System.IO.File.ReadAllText(path);

        StringAssert.Contains("#if UNITY_WEBGL", source,
            "проверка живёт на исходнике: под WebGL этот код скомпилирован не будет, "
            + "и никакой тест в редакторе его не выполнит");
        int guard = source.IndexOf("#if UNITY_WEBGL", System.StringComparison.Ordinal);
        int elseAt = source.IndexOf("#else", guard, System.StringComparison.Ordinal);
        var webglBranch = source.Substring(guard, elseAt - guard);

        StringAssert.DoesNotContain("Screen.SetResolution", webglBranch,
            "в WebGL размером canvas управляет браузер через CSS (ширина 100%). "
            + "Screen.SetResolution фиксирует canvas в DOM-пикселях и оставляет вокруг "
            + "Unity пустые поля, пока пользователь не нажмёт F11, — поэтому под WebGL "
            + "режим окна не применяется вовсе");
        StringAssert.Contains("return false", webglBranch,
            "ветка WebGL обязана отвечать «окном распоряжается не приложение»");
    }
}
