using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Handles;
using KitchenDesigner.Core.UI;

/// <summary>Состояние наведения у ручек области накладки — то же требование
/// UI-GUIDELINES §12, что и у ручек трансформации, и по той же причине: без
/// подсветки промах по ручке неотличим от «правка области не работает».
/// Ручек тут две формы, и красить надо обе — кубик в режиме растяжения и
/// стрелку в режиме переноса.</summary>
public class TextureOverlayHandleHoverTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private readonly List<Material> _materials = new List<Material>();
    private System.Func<Color, Material?>? _factoryBefore;

    [SetUp]
    public void Setup()
    {
        var shader = HandleMaterials.FindShader();
        Assert.IsNotNull(shader, HandleMaterials.ShaderMissingMessage);

        _factoryBefore = HandleMaterials.MaterialFactory;
        HandleMaterials.MaterialFactory = color =>
        {
            var material = new Material(shader) { color = color };
            _materials.Add(material);
            return material;
        };
    }

    [TearDown]
    public void Teardown()
    {
        HandleMaterials.MaterialFactory = _factoryBefore;
        TextureOverlayHandles.End();
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var material in _materials) if (material != null) Object.DestroyImmediate(material);
        _materials.Clear();
    }

    private TextureOverlayHandles MakeManager()
    {
        var go = new GameObject("Ручки области");
        _spawned.Add(go);
        return go.AddComponent<TextureOverlayHandles>();
    }

    private TextureOverlayHandle MakeHandle(int edge, bool arrow)
    {
        var go = new GameObject($"TextureOverlayHandle_{edge}");
        _spawned.Add(go);
        var handle = go.AddComponent<TextureOverlayHandle>();
        handle.edge = edge;
        var material = HandleMaterials.For(UIStyle.HighlightChanged);
        if (arrow)
            HandleVisual.BuildArrow(go.transform, material, HandleMetrics.Overlay,
                HandleShaft.Box, HandleTip.Cone);
        else
            HandleVisual.BuildCube(go.transform, material, OverlayHandleScale.CubeEdgeUnits);
        return handle;
    }

    private static IEnumerable<Color> ColorsOf(TextureOverlayHandle handle)
    {
        foreach (var renderer in handle.GetComponentsInChildren<MeshRenderer>())
            yield return renderer.sharedMaterial.color;
    }

    [Test]
    public void Hover_RepaintsEveryPartOfTheArrow_AndReleasesItBack()
    {
        var manager = MakeManager();
        var handle = MakeHandle(TextureOverlayHandle.EdgeMinU, arrow: true);

        manager.SetHover(handle);
        foreach (var color in ColorsOf(handle))
            Assert.AreEqual(UIStyle.MeasureHover, color,
                "подсвечивается ВСЯ стрелка: шток отдельным объектом от наконечника, "
                + "и перекрашенный наполовину гизмо читается как артефакт");

        manager.SetHover(null);
        foreach (var color in ColorsOf(handle))
            Assert.AreEqual(UIStyle.HighlightChanged, color,
                "уход курсора возвращает базовый цвет ручек накладки, а не цвет оси: "
                + "у области рёбра не различаются цветом, все четыре одинаковы");
    }

    [Test]
    public void Hover_RepaintsTheCubeToo_NotOnlyTheArrow()
    {
        var manager = MakeManager();
        var handle = MakeHandle(TextureOverlayHandle.EdgeMaxV, arrow: false);

        manager.SetHover(handle);

        foreach (var color in ColorsOf(handle))
            Assert.AreEqual(UIStyle.MeasureHover, color,
                "в режиме «растяжение» ручка — кубик, а не стрелка; подсветка, "
                + "написанная под стрелку, оставила бы половину режимов без наведения");
    }

    [Test]
    public void Hover_MovingToAnotherHandle_LeavesOnlyOneLit()
    {
        var manager = MakeManager();
        var left = MakeHandle(TextureOverlayHandle.EdgeMinU, arrow: true);
        var right = MakeHandle(TextureOverlayHandle.EdgeMaxU, arrow: true);

        manager.SetHover(left);
        manager.SetHover(right);

        foreach (var color in ColorsOf(left))
            Assert.AreEqual(UIStyle.HighlightChanged, color,
                "прежняя ручка гасится: две подсвеченные границы означали бы, что "
                + "потянутся обе");
        foreach (var color in ColorsOf(right))
            Assert.AreEqual(UIStyle.MeasureHover, color,
                "новая под курсором — подсвечена");
        Assert.AreSame(right, manager.Hovered,
            "менеджер помнит именно её: иначе следующий уход курсора погасит не ту");
    }

    [Test]
    public void Hover_OverNothing_LeavesTheHandlesAlone()
    {
        var manager = MakeManager();
        var handle = MakeHandle(TextureOverlayHandle.EdgeMinV, arrow: true);

        manager.SetHover(null);

        foreach (var color in ColorsOf(handle))
            Assert.AreEqual(UIStyle.HighlightChanged, color,
                "положительный контроль: без него тест был бы зелёным и на коде, "
                + "который красит ручку всегда");
        Assert.IsNull(manager.Hovered,
            "и наведения ни на что нет — иначе гашение прежней ручки некому вызвать");
    }
}
