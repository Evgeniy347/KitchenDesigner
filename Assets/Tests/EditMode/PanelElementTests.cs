using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>ДВП/ХДФ: зазоры входят в ГАБАРИТ (как у фасада), но не в физический
/// меш. В паз заходит номинал, а технологический зазор остаётся внутри детали.</summary>
public class PanelElementTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    private PanelElement MakePanel(Vector3Int dims, int gap = PanelElement.DEFAULT_GAP_MM)
    {
        var go = new GameObject("ДВП");
        _spawned.Add(go);
        var panel = go.AddComponent<PanelElement>();
        panel.PartName = "ДВП";
        panel.DimensionsMM = dims;
        panel.SetUniformGap(gap);
        return panel;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
    }

    [Test]
    public void Panel_DefaultGap_IsOneMillimetre()
    {
        var panel = MakePanel(new Vector3Int(383, 376, 3));
        Assert.AreEqual(1, PanelElement.DEFAULT_GAP_MM);
        Assert.AreEqual(1, panel.GapLeft);
        Assert.AreEqual(6, panel.GapMM, "сумма шести зазоров по 1 мм");
    }

    [Test]
    public void Panel_BoundingBox_IsBiggerThanMeshByGaps()
    {
        var panel = MakePanel(new Vector3Int(383, 376, 3), gap: 1);
        var verts = panel.GetVertices();

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var v in verts)
        {
            minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x);
            minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y);
        }

        // Номинал = физический размер + зазоры с обеих сторон.
        Assert.AreEqual(383 + 2, (maxX - minX) / AppConstants.MM_TO_UNITS, 1e-2f);
        Assert.AreEqual(376 + 2, (maxY - minY) / AppConstants.MM_TO_UNITS, 1e-2f);
    }

    [Test]
    public void Panel_Thickness_GrowsByFrontAndBackGaps()
    {
        var panel = MakePanel(new Vector3Int(383, 376, 3), gap: 5);
        var verts = panel.GetVertices();

        float minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var v in verts)
        {
            minZ = Mathf.Min(minZ, v.z); maxZ = Mathf.Max(maxZ, v.z);
        }
        Assert.AreEqual(3 + 5 + 5, (maxZ - minZ) / AppConstants.MM_TO_UNITS, 1e-2f,
            "зазор спереди и сзади входит в габарит так же, как боковой");
    }

    /// <summary>Только боковые зазоры толщину по-прежнему не трогают: панель
    /// сидит в пазу по пласти, и правка ширины зазора не должна её распирать.</summary>
    [Test]
    public void Panel_SideGaps_LeaveThicknessAlone()
    {
        var panel = MakePanel(new Vector3Int(383, 376, 3), gap: 0);
        panel.GapLeft = 5;
        panel.GapRight = 5;
        panel.GapTop = 5;
        panel.GapBottom = 5;

        float minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var v in panel.GetVertices())
        {
            minZ = Mathf.Min(minZ, v.z); maxZ = Mathf.Max(maxZ, v.z);
        }
        Assert.AreEqual(3, (maxZ - minZ) / AppConstants.MM_TO_UNITS, 1e-2f);
    }

    [Test]
    public void Panel_HasSixFaces_SizedByNominal()
    {
        var panel = MakePanel(new Vector3Int(383, 376, 3), gap: 1);
        var faces = panel.GetFaces();

        Assert.AreEqual(6, faces.Length);
        // Грань, нормальная к Z (пласть), имеет размеры номинала 385×378.
        var front = faces[4];
        Assert.AreEqual(385, front.size.x / AppConstants.MM_TO_UNITS, 1e-2f);
        Assert.AreEqual(378, front.size.y / AppConstants.MM_TO_UNITS, 1e-2f);
    }

    [Test]
    public void Panel_DoesNotSupportGrooves()
    {
        var panel = MakePanel(new Vector3Int(383, 376, 3));
        Assert.IsFalse(panel.SupportsGrooves, "ДВП вставляется В паз, своих пазов не имеет");
        Assert.IsFalse(panel.AddGroove(new GrooveSpec(GrooveKind.Through, GrooveSide.Top)));
    }

    [Test]
    public void Panel_RoundTripsThroughElementData()
    {
        var panel = MakePanel(new Vector3Int(383, 376, 3), gap: 1);

        var data = ElementCapture.FromElement(panel);
        var restored = JsonUtility.FromJson<ElementData>(JsonUtility.ToJson(data));

        Assert.IsTrue(restored.isPanel);
        Assert.IsFalse(restored.isFacade, "ДВП не должна грузиться как фасад");
        Assert.AreEqual(1, restored.gapLeft);
        Assert.AreEqual(1, restored.gapRight);
        Assert.AreEqual(1, restored.gapTop);
        Assert.AreEqual(1, restored.gapBottom);
        Assert.AreEqual(new Vector3Int(383, 376, 3), restored.Dimensions);
    }

    [Test]
    public void Facade_StillUsesSameGapMath_AfterExtraction()
    {
        // GappedBox вынесен из FacadeElement — фасад обязан считать так же.
        var go = new GameObject("Фасад");
        _spawned.Add(go);
        var facade = go.AddComponent<FacadeElement>();
        facade.DimensionsMM = new Vector3Int(600, 400, 18);
        facade.GapLeft = 2; facade.GapRight = 3; facade.GapTop = 4; facade.GapBottom = 5;

        var verts = facade.GetVertices();
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var v in verts)
        {
            minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x);
            minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y);
        }

        Assert.AreEqual(600 + 2 + 3, (maxX - minX) / AppConstants.MM_TO_UNITS, 1e-2f);
        Assert.AreEqual(400 + 4 + 5, (maxY - minY) / AppConstants.MM_TO_UNITS, 1e-2f);
    }
}
