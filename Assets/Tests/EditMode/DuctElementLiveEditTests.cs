using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Ventilation;

/// <summary>Тот же риск, что FenceElementLiveEditTests уже проверяет для FenceElement:
/// [Undoable]-сеттер, который меняет геометрию, обязан реально перестроить меш через
/// ApplyDimensions(), а не только обновить поле, которое ApplyDimensions потом прочитает
/// сам по себе при следующей случайной перестройке. DuctElement строит меш сам
/// (AdoptOwnedMesh получает НОВЫЙ Mesh при каждом Rebuild), поэтому смену меша видно по
/// смене ссылки sharedMesh, а не только по числам.</summary>
public class DuctElementLiveEditTests
{
    private readonly List<GameObject> _spawned = new();

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
    }

    private DuctElement Spawn(int lengthMm)
    {
        var go = ElementFactory.CreateDuct(lengthMm, "D", Vector3.zero);
        _spawned.Add(go);
        return go.GetComponent<DuctElement>();
    }

    private static Mesh Mesh(DuctElement duct) => duct.GetComponent<MeshFilter>().sharedMesh;

    [Test]
    public void ProfileKind_ChangedToRect_RebuildsTheMesh_WithTheRectFaceCount()
    {
        var duct = Spawn(1250);
        var meshBefore = Mesh(duct);
        Assert.AreEqual(DuctProfileKind.Round, duct.ProfileKind, "по умолчанию профиль круглый");

        duct.ProfileKind = DuctProfileKind.Rect;

        var meshAfter = Mesh(duct);
        Assert.AreNotSame(meshBefore, meshAfter,
            "смена ProfileKind обязана перестроить меш через ApplyDimensions()");
        Assert.AreEqual(RectTransitionMesh.FaceCount * RectTransitionMesh.VerticesPerFace,
            meshAfter.vertexCount,
            "прямоугольный профиль строится BoxRunMesh/RectTransitionMesh — 6 граней по 4 вершины");
    }

    [Test]
    public void DiameterMm_Changed_RebuildsTheMesh_AndWidensItsBounds()
    {
        var duct = Spawn(1250);
        var meshBefore = Mesh(duct);
        float radiusBeforeU = meshBefore.bounds.extents.x;

        duct.DiameterMm = 300;

        var meshAfter = Mesh(duct);
        Assert.AreNotSame(meshBefore, meshAfter,
            "смена DiameterMm обязана перестроить меш через ApplyDimensions()");
        Assert.Greater(meshAfter.bounds.extents.x, radiusBeforeU,
            "300 мм в диаметре обязаны дать более широкий меш, чем дефолтный 125 мм");
    }

    [Test]
    public void RectWidthMm_Changed_RebuildsTheMesh_WhenProfileIsRect()
    {
        var duct = Spawn(1250);
        duct.ProfileKind = DuctProfileKind.Rect;
        var meshBefore = Mesh(duct);
        // AdoptOwnedMesh destroys the OLD mesh once a new one is adopted, so the width
        // extent has to be read out BEFORE the next rebuild, not from meshBefore afterwards.
        float widthExtentBeforeU = meshBefore.bounds.extents.x;

        duct.RectWidthMm = 400;

        var meshAfter = Mesh(duct);
        Assert.AreNotSame(meshBefore, meshAfter,
            "смена RectWidthMm обязана перестроить меш через ApplyDimensions(), пока профиль "
            + "прямоугольный");
        Assert.Greater(meshAfter.bounds.extents.x, widthExtentBeforeU);
    }

    [Test]
    public void AirflowM3PerHour_Changed_DoesNotRebuildTheMesh()
    {
        var duct = Spawn(1250);
        var meshBefore = Mesh(duct);

        duct.AirflowM3PerHour = 500;

        Assert.AreSame(meshBefore, Mesh(duct),
            "расход воздуха не влияет на геометрию — участвует только в VNT-01/ведомости, "
            + "перестройка меша тут была бы лишней работой на каждый ввод числа");
    }
}
