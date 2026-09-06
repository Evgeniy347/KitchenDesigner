using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Handles;
using KitchenDesigner.Core.UI;

/// <summary>Состояние наведения у гизмо — требование UI-GUIDELINES §12. Без него
/// промах по ручке неотличим от «инструмент не работает»: пользователь видит
/// стрелку, жмёт по ней, выделяется деталь за ней, и ничто на экране не сказало,
/// что курсор ручку не держал.</summary>
public class ResizeHandleHoverTests
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
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var material in _materials) if (material != null) Object.DestroyImmediate(material);
        _materials.Clear();
    }

    private ResizeHandleManager MakeManager()
    {
        var go = new GameObject("Ручки");
        _spawned.Add(go);
        return go.AddComponent<ResizeHandleManager>();
    }

    private ResizeHandle MakeArrowHandle(int faceIndex)
    {
        var go = new GameObject($"ResizeHandle_{faceIndex}");
        _spawned.Add(go);
        var handle = go.AddComponent<ResizeHandle>();
        handle.faceIndex = faceIndex;
        HandleVisual.BuildArrow(go.transform, HandleMaterials.For(
                HandleMaterials.ForAxis(faceIndex / 2)),
            HandleMetrics.Resize, HandleShaft.Cylinder, HandleTip.Box);
        return handle;
    }

    private static IEnumerable<Color> ColorsOf(ResizeHandle handle)
    {
        foreach (var renderer in handle.GetComponentsInChildren<MeshRenderer>())
            yield return renderer.sharedMaterial.color;
    }

    [Test]
    public void Hover_RepaintsEveryPartOfTheArrow_AndReleasesItBack()
    {
        var manager = MakeManager();
        var handle = MakeArrowHandle(0);

        manager.SetHover(handle);
        foreach (var color in ColorsOf(handle))
            Assert.AreEqual(UIStyle.MeasureHover, color,
                "подсвечивается ВСЯ стрелка: шток отдельным объектом от наконечника, "
                + "и перекрашенный наполовину гизмо читается как артефакт");

        manager.SetHover(null);
        foreach (var color in ColorsOf(handle))
            Assert.AreEqual(HandleMaterials.AxisX, color,
                "уход курсора возвращает цвет ОСИ, а не первый попавшийся: цвет — "
                + "единственное, чем ручка X отличается от Y и Z");
    }

    [Test]
    public void Hover_MovingToAnotherHandle_LeavesOnlyOneLit()
    {
        var manager = MakeManager();
        var x = MakeArrowHandle(0);
        var y = MakeArrowHandle(2);

        manager.SetHover(x);
        manager.SetHover(y);

        foreach (var color in ColorsOf(x))
            Assert.AreEqual(HandleMaterials.AxisX, color,
                "прежняя ручка гасится: две подсвеченные стрелки означали бы "
                + "две активные оси");
        foreach (var color in ColorsOf(y))
            Assert.AreEqual(UIStyle.MeasureHover, color);
        Assert.AreSame(y, manager.Hovered);
    }

    [Test]
    public void Hover_OverNothing_LeavesTheHandlesAlone()
    {
        var manager = MakeManager();
        var handle = MakeArrowHandle(4);

        manager.SetHover(null);

        foreach (var color in ColorsOf(handle))
            Assert.AreEqual(HandleMaterials.AxisZ, color,
                "положительный контроль: без него тест был бы зелёным и на коде, "
                + "который красит ручку всегда");
        Assert.IsNull(manager.Hovered);
    }
}
