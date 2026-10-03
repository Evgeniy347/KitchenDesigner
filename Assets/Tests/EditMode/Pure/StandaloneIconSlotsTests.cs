using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using KitchenDesigner.Tests.Geometry;
using NUnit.Framework;

// Иконка KitchenDesigner.exe собирается Unity из слотов Standalone в ProjectSettings
// (m_BuildTargetIcons). Пустой слот Unity заполняет уменьшенной копией соседнего — так exe
// и ушёл с размытыми 16/32: заполнен был один слот 256, а нарисованные по пиксельной сетке
// icon-16/32/48.png лежали рядом без дела. Блок m_BuildTargetPlatformIcons для Standalone
// Unity не читает вовсе (GetSupportedIconKinds(Standalone) пуст, проверено 2026-10-03) — именно
// он и создавал видимость, что все размеры назначены. 24 px у Unity в списке нет: этот кадр
// есть только в app.ico установщика.
public class StandaloneIconSlotsTests
{
    // PlayerSettings.GetIconSizesForTargetGroup(BuildTargetGroup.Standalone), Unity 6000.4.3f1.
    private static readonly int[] UnitySlotSizes = { 1024, 512, 256, 128, 64, 48, 32, 16 };

    private static string ShippedDir => RepoPaths.Subdir("Assets", "Art", "Icon", "shipped");

    private static string Settings() =>
        File.ReadAllText(Path.Combine(RepoPaths.Subdir("ProjectSettings"), "ProjectSettings.asset"));

    private static Dictionary<int, string> ShippedRasterGuids() =>
        Directory.GetFiles(ShippedDir, "icon-*.png")
            .ToDictionary(
                png => int.Parse(Regex.Match(Path.GetFileName(png), @"^icon-(\d+)\.png$").Groups[1].Value),
                png => Regex.Match(File.ReadAllText(png + ".meta"), @"^guid:\s*(\w+)", RegexOptions.Multiline).Groups[1].Value);

    private static Dictionary<int, string> StandaloneSlots()
    {
        var block = Regex.Match(Settings(), @"^  m_BuildTargetIcons:(?<b>.*?)^  m_BuildTargetPlatformIcons:",
            RegexOptions.Singleline | RegexOptions.Multiline);
        Assert.IsTrue(block.Success, "в ProjectSettings.asset не найден блок m_BuildTargetIcons");
        var targets = Regex.Matches(block.Groups["b"].Value, @"m_BuildTarget:\s*(\S*)").Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
        Assert.AreEqual(new[] { "Standalone" }, targets, "разбор рассчитан на один набор слотов — Standalone");
        return Regex.Matches(block.Groups["b"].Value,
                @"m_Icon:\s*\{fileID:\s*(?<id>\d+)(?:,\s*guid:\s*(?<g>\w+))?[^}]*\}\s*\n\s*m_Width:\s*(?<w>\d+)")
            .Cast<Match>()
            .ToDictionary(m => int.Parse(m.Groups["w"].Value), m => m.Groups["g"].Success ? m.Groups["g"].Value : "");
    }

    [Test]
    public void EverySizeUnityOffers_HasItsOwnSlot()
    {
        CollectionAssert.AreEquivalent(UnitySlotSizes, StandaloneSlots().Keys,
            "слоты Standalone должны быть все — пропущенный Unity заполняет уменьшенной копией большего");
    }

    [Test]
    public void EachSlot_PointsAtTheShippedRasterOfThatVerySize()
    {
        var rasters = ShippedRasterGuids();
        var wrong = new List<string>();
        foreach (var slot in StandaloneSlots())
        {
            var expected = rasters.TryGetValue(slot.Key, out var guid) ? guid : "";
            if (slot.Value != expected)
                wrong.Add($"{slot.Key}px: {(slot.Value == "" ? "пусто" : slot.Value)}, а нужно {(expected == "" ? "пусто (растра этого размера нет)" : "icon-" + slot.Key + ".png " + expected)}");
        }
        Assert.IsEmpty(wrong, "слот иконки exe не совпадает с растром своего размера из " + ShippedDir
                              + " — Unity отмасштабирует чужой кадр, и мелкие размеры станут размытыми");
    }

    [Test]
    public void TheHandDrawnSmallSizes_ReachTheExe()
    {
        var slots = StandaloneSlots();
        var rasters = ShippedRasterGuids();
        foreach (var size in new[] { 16, 32, 48 })
        {
            Assert.IsTrue(rasters.ContainsKey(size), "нет нарисованного по сетке icon-" + size + ".png");
            Assert.AreEqual(rasters[size], slots[size], size + "px в exe — не нарисованный кадр, а уменьшенный 256");
        }
    }

    [Test]
    public void EveryShippedRaster_IsReallyTheSizeItsNameSays()
    {
        foreach (var png in Directory.GetFiles(ShippedDir, "icon-*.png"))
        {
            var size = int.Parse(Regex.Match(Path.GetFileName(png), @"\d+").Value);
            var header = File.ReadAllBytes(png);
            int width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
            int height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
            Assert.AreEqual((size, size), (width, height), Path.GetFileName(png) + ": слот по имени и пиксели разошлись");
        }
    }

    [Test]
    public void NoStandaloneBlock_InThePlatformIconsUnityIgnores()
    {
        var platformIcons = Regex.Match(Settings(), @"^  m_BuildTargetPlatformIcons:(?<b>.*?)^  m_BuildTargetBatching:",
            RegexOptions.Singleline | RegexOptions.Multiline);
        Assert.IsTrue(platformIcons.Success, "в ProjectSettings.asset нет m_BuildTargetPlatformIcons");
        StringAssert.DoesNotContain("m_BuildTarget: Standalone", platformIcons.Groups["b"].Value,
            "для Standalone Unity этот блок не читает; заполненный, он выглядит как назначенные размеры и прячет пустые слоты");
    }
}
