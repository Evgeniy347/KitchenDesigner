using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using KitchenDesigner.Core;
using KitchenDesigner.Tests.Geometry;

public class ScreenSpaceGIFeatureTests
{
    private static List<string> ShaderPassNames()
    {
        var path = Path.Combine(
            RepoPaths.Subdir("Assets", "Scripts", "Core", "Rendering", "SSGI"),
            "ScreenSpaceGI.shader");
        Assert.IsTrue(File.Exists(path), "шейдер SSGI не найден: тест по несуществующему пути зеленеет впустую");

        var names = new List<string>();
        foreach (Match m in Regex.Matches(File.ReadAllText(path), "Name\\s+\"([^\"]+)\""))
            names.Add(m.Groups[1].Value);
        return names;
    }

    [Test]
    public void PassIndices_PointAtTheGatherAndBlurPasses_InThatOrder()
    {
        var names = ShaderPassNames();

        Assert.AreEqual(2, names.Count, "у шейдера SSGI ровно два прохода: сбор и денойз");
        Assert.AreEqual("ScreenSpaceGI", names[ScreenSpaceGIFeature.GatherPassIndex],
            "проход 0 собирает непрямой свет; перепутанный индекс даёт денойз по неразмытому кадру");
        Assert.AreEqual("ScreenSpaceGIBlur", names[ScreenSpaceGIFeature.DenoisePassIndex],
            "проход 1 — билатеральный денойз; без него результат остаётся шумным");
        Assert.AreNotEqual(ScreenSpaceGIFeature.GatherPassIndex, ScreenSpaceGIFeature.DenoisePassIndex);
    }

    [Test]
    public void Create_InjectsBeforePostProcessing_SoBloomSeesTheIndirectLight()
    {
        var feature = ScriptableObject.CreateInstance<ScreenSpaceGIFeature>();
        feature.Create();

        Assert.IsNotNull(feature.Pass, "Create обязан завести проход");
        Assert.AreEqual(RenderPassEvent.BeforeRenderingPostProcessing, feature.Pass!.renderPassEvent,
            "SSGI считается ДО пост-обработки: иначе bloom и экспозиция работают по кадру без непрямого света");

        Object.DestroyImmediate(feature);
    }
}
