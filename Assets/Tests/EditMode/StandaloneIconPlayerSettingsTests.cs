using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;

// Тот же сторож, что StandaloneIconSlotsTests, но спрашивает саму Unity, а не YAML: текстовый
// разбор ProjectSettings проверяет то, что записано, а этот — то, что сборка плеера прочтёт.
// Расхождение между ними уже было: блок m_BuildTargetPlatformIcons выглядел назначенным, а Unity
// для Standalone его не читала.
public class StandaloneIconPlayerSettingsTests
{
    private const string ShippedDir = "Assets/Art/Icon/shipped/";

    [Test]
    public void EverySlotUnityReads_HoldsTheShippedRasterOfThatSize_OrNothingWhenNoneWasDrawn()
    {
        var sizes = PlayerSettings.GetIconSizes(NamedBuildTarget.Standalone, IconKind.Any);
        var icons = PlayerSettings.GetIcons(NamedBuildTarget.Standalone, IconKind.Any);
        Assert.AreEqual(sizes.Length, icons.Length);

        var wrong = new List<string>();
        for (int i = 0; i < sizes.Length; i++)
        {
            var raster = ShippedDir + "icon-" + sizes[i] + ".png";
            var expected = File.Exists(raster) ? raster : "";
            var actual = icons[i] == null ? "" : AssetDatabase.GetAssetPath(icons[i]);
            if (actual != expected) wrong.Add($"{sizes[i]}px: «{actual}», нужно «{expected}»");
        }
        Assert.IsEmpty(wrong, "слот иконки exe держит растр чужого размера или пуст при готовом растре — Unity его отмасштабирует");
    }
}
