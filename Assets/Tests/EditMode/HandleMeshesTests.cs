using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.Handles;

/// <summary>Меши ручек единичные и вдоль +Z: размер задаёт localScale ручки,
/// а не сам меш, — поэтому одна геометрия обслуживает и ручки объекта, и ручки
/// накладки с их разными метриками.</summary>
public class HandleMeshesTests
{
    [Test]
    public void Cone_IsAUnitConeAlongPlusZ()
    {
        var cone = HandleMeshes.Cone();

        Assert.AreEqual(1f, cone.bounds.size.z, 1e-4f,
            "конус единичного масштаба: длину наконечника задаёт localScale ручки");
        Assert.AreEqual(0.5f, cone.bounds.max.z, 1e-4f, "вершина при z = +0.5");
        Assert.AreEqual(-0.5f, cone.bounds.min.z, 1e-4f, "основание при z = −0.5");
        Assert.AreEqual(0.5f, cone.bounds.max.x, 1e-4f, "радиус основания 0.5");
        Assert.AreEqual(1f, cone.bounds.size.y, 1e-4f);
    }

    [Test]
    public void Cube_IsAUnitCubeBuiltByHand()
    {
        var cube = HandleMeshes.Cube();

        Assert.AreEqual(1f, cube.bounds.size.x, 1e-4f, "единичный куб");
        Assert.AreEqual(1f, cube.bounds.size.y, 1e-4f);
        Assert.AreEqual(1f, cube.bounds.size.z, 1e-4f);
        Assert.AreEqual(24, cube.vertexCount,
            "куб собран вручную по четыре вершины на грань: ручки — голые меши "
            + "без коллайдера, поэтому линкеру нечего у них вырезать");
    }

    [Test]
    public void Cylinder_IsAUnitCylinderAlongPlusZ()
    {
        var cylinder = HandleMeshes.Cylinder();

        Assert.AreEqual(1f, cylinder.bounds.size.z, 1e-4f,
            "стержень тоже вдоль +Z и единичный: раньше это был примитив Unity "
            + "высотой 2 по Y, который приходилось доворачивать на 90° и делить "
            + "масштаб пополам");
        Assert.AreEqual(1f, cylinder.bounds.size.x, 1e-4f, "диаметр 1");
        Assert.AreEqual(1f, cylinder.bounds.size.y, 1e-4f);
    }

    [Test]
    public void Meshes_AreBuiltOnce()
    {
        Assert.AreSame(HandleMeshes.Cone(), HandleMeshes.Cone());
        Assert.AreSame(HandleMeshes.Cube(), HandleMeshes.Cube());
        Assert.AreSame(HandleMeshes.Cylinder(), HandleMeshes.Cylinder(),
            "меш строится один раз: PositionHandles зовётся каждый кадр");
    }

    [Test]
    public void Metrics_ArrowPartsAddUpToTheArrowLength()
    {
        var m = HandleMetrics.Resize;

        Assert.AreEqual(m.Gap + m.ShaftLen + m.TipLen, m.ArrowLen, 1e-6f);
        Assert.AreEqual(m.Gap + m.ShaftLen + m.TipLen * 0.5f, m.TipCenterZ, 1e-6f,
            "точка захвата ручки — центр наконечника, и она же центр меша наконечника");
        Assert.AreEqual(0f, HandleMetrics.Overlay.Gap,
            "у ручек накладки стрелка начинается прямо на поверхности, без зазора: "
            + "пресеты намеренно разные, общий здесь код, а не числа");
    }
}
