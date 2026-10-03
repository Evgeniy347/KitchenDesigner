using System.Linq;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using NUnit.Framework;

/// <summary>
/// Вкладка «О программе» показывает сведения о сборке сразу строками, без кнопки
/// «скопировать»: человек, пишущий о проблеме, читает их глазами. Набор строк и их порядок
/// держит этот класс — он на быстром пути, потому что строки собираются из чистых данных.
/// </summary>
public class AboutLinesTests
{
    private const string Version = "0.1234";
    private const string BuildDate = "2026-10-03 09:00";
    private const string Platform = "WindowsPlayer";
    private const string UnityVersion = "6000.4.3f1";
    private const string Api = "Direct3D11";
    private const string Gpu = "NVIDIA GeForce RTX 3060";

    private static AboutEnvironment Environment(string gpu = Gpu) =>
        new AboutEnvironment(Version, BuildDate, Platform, UnityVersion, Api, gpu);

    [OneTimeSetUp]
    public void SourceLanguage() => Loc.SetLanguage("ru");

    [Test]
    public void Lines_AreProductBuildPlatformUnityApiGpuCopyright_InThatOrder()
    {
        var lines = AboutLines.For(Environment());

        Assert.AreEqual(7, lines.Count,
            "семь строк: продукт с версией, сборка, платформа, Unity, графический API, видеокарта, "
            + "копирайт. Лишняя строка или потерянная видна здесь, а не на глаз в окне");
        Assert.AreEqual($"Kitchen Designer {Version}", lines[0],
            "первая строка — название и версия одной строкой, без подписи «Версия:»");
        StringAssert.StartsWith("Сборка:", lines[1]);
        StringAssert.Contains(BuildDate, lines[1]);
        StringAssert.StartsWith("Платформа:", lines[2]);
        StringAssert.Contains(Platform, lines[2]);
        StringAssert.StartsWith("Unity:", lines[3]);
        StringAssert.Contains(UnityVersion, lines[3]);
        StringAssert.StartsWith("Графика (API):", lines[4]);
        StringAssert.Contains(Api, lines[4]);
        StringAssert.StartsWith("Видеокарта:", lines[5]);
        StringAssert.Contains(Gpu, lines[5]);
        Assert.AreEqual(AboutLines.Copyright, lines[6], "копирайт — последняя строка, дословно");
    }

    [Test]
    public void GpuLine_FollowsTheGpuName_AndNotTheGraphicsApi()
    {
        var other = AboutLines.For(Environment("AMD Radeon RX 6600"));
        var first = AboutLines.For(Environment());

        Assert.AreNotEqual(first[5], other[5],
            "строка видеокарты обязана зависеть от имени видеокарты: иначе она показывает не то "
            + "железо, на котором программа реально рисует");
        CollectionAssert.AreEqual(first.Where((_, i) => i != 5).ToList(),
            other.Where((_, i) => i != 5).ToList(),
            "смена видеокарты не трогает остальные строки");
    }

    [Test]
    public void EveryLine_IsNonEmpty_AndHasNoUnresolvedPlaceholder()
    {
        foreach (var line in AboutLines.For(Environment()))
        {
            Assert.IsNotEmpty(line);
            StringAssert.DoesNotContain("{0}", line,
                "подпись без подставленного значения означает потерянный аргумент Loc.F");
        }
    }
}
