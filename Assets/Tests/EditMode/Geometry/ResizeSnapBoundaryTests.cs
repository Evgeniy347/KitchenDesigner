using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class ResizeSnapBoundaryTests
{
    private const float MM = 0.001f;
    private const float Threshold = 50f * MM;

    private static ElementGeometry Box(string name, Vector3 pos, Vector3Int dims)
        => ElementGeometry.Box(name, pos, new Vector3(dims.x, dims.y, dims.z) * MM);

    private sealed class PosedBox : IPosedGeometry
    {
        private readonly Vector3Int _dims;
        public PosedBox(Vector3Int dims) => _dims = dims;
        public ElementGeometry At(Vector3 position)
            => Box("post", position, _dims);
    }

    private static readonly Vector3Int PanelDims = new Vector3Int(1022, 18, 332);
    private static readonly Vector3Int PostDims = new Vector3Int(18, 883, 331);
    private static readonly Vector3 PanelCentre = new Vector3(1.056f, 2.251f, -3.454f);
    private static readonly Vector3 PostCentre = new Vector3(1.576f, 1.818f, -3.454f);

    private static bool SnapsDelta(ElementGeometry neighbour, out float gap)
    {
        var a = Box("A", new Vector3(0f, 0.2f, 0f), new Vector3Int(800, 400, 18));
        var fa = a.Faces[0];
        return ResizeSnap.SnapDelta(fa.center, fa.normal, fa.rightAxis, fa.upAxis, fa.size,
            new List<ElementGeometry> { neighbour }, a, Threshold, out gap);
    }

    [Test]
    public void SnapDelta_NeighbourJustBeyondTheThreshold_DoesNotSnap()
    {
        var justInside = Box("in", new Vector3(0.4f + Threshold + 0.4f, 0.2f, 0f),
            new Vector3Int(800, 400, 18));
        Assert.IsTrue(SnapsDelta(justInside, out float gapInside),
            "контроль: грань РОВНО на пороге прилипает — порог инклюзивный");
        Assert.AreEqual(Threshold, gapInside, 0.2f * MM,
            "контроль: зазор равен порогу");

        var justOutside = Box("out", new Vector3(0.4f + Threshold + 0.4f + 0.2f * MM, 0.2f, 0f),
            new Vector3Int(800, 400, 18));
        Assert.IsFalse(SnapsDelta(justOutside, out _),
            "0,2 мм за порогом — уже не прилипает. Прежний отрицательный случай стоял "
            + "в 700 мм от порога и не отличал инклюзивное сравнение от любого другого");
    }

    [Test]
    public void MoveAndResize_BothTakeAnEdgeOnlyFootprintTouch_AsContact()
    {
        var panel = Box("top", PanelCentre, PanelDims);
        var post = Box("post", PostCentre, PostDims);
        var topFace = post.Faces[2];

        bool resizeSnaps = ResizeSnap.SnapDelta(topFace.center, topFace.normal,
            topFace.rightAxis, topFace.upAxis, topFace.size,
            new List<ElementGeometry> { panel }, post, Threshold, out _);

        var moved = SnapCore.TrySnap(new PosedBox(PostDims),
            new List<ElementGeometry> { panel },
            new Vector3(PostCentre.x, PostCentre.y - 5f * MM, PostCentre.z), Threshold);

        Assert.IsTrue(resizeSnaps,
            "ресайз принимает касание РОВНО ПО РЕБРУ (площадь пересечения footprint'ов "
            + "нулевая) — стойка торцом у кромки панели");
        Assert.IsTrue(moved.snapped,
            "перемещение обязано видеть тот же контакт: порог и знак сравнения у "
            + "ресайза и у перемещения одни. Разойдясь, они дают «растягивается, но "
            + "не перетаскивается»");
        Assert.AreEqual(PostCentre.y + 0.5f * MM, moved.position.y, 0.2f * MM,
            "и приводят к одному и тому же положению: верх стойки заподлицо с верхом панели");
    }

    [Test]
    public void MoveAndResize_BothRefuseAFootprintWithARealGap()
    {
        var panel = Box("top", PanelCentre, PanelDims);
        var apart = new Vector3(PostCentre.x + 20f * MM, PostCentre.y, PostCentre.z);
        var post = Box("post", apart, PostDims);
        var topFace = post.Faces[2];

        bool resizeSnaps = ResizeSnap.SnapDelta(topFace.center, topFace.normal,
            topFace.rightAxis, topFace.upAxis, topFace.size,
            new List<ElementGeometry> { panel }, post, Threshold, out _);

        var moved = SnapCore.TrySnap(new PosedBox(PostDims),
            new List<ElementGeometry> { panel },
            new Vector3(apart.x, apart.y - 5f * MM, apart.z), Threshold);

        Assert.IsFalse(resizeSnaps,
            "контроль: между footprint'ами 20 мм настоящего зазора — послабление на "
            + "касание не превращается в «липнет ко всему»");
        Assert.AreNotEqual(PostCentre.y + 0.5f * MM, moved.position.y,
            "и перемещение не сажает стойку заподлицо с верхом панели через воздух");
    }
}
