using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>ДВП/ХДФ: зазоры входят в ГАБАРИТ (как у фасада), но не в физический
/// меш. В паз заходит номинал, а технологический зазор остаётся внутри детали.
///
/// Здесь панель — обычный C#-объект: ни GameObject, ни компонента. Сценовая
/// половина того же контракта живёт в PanelElementTests.Panel_MirrorsItsBody_*,
/// и она обязана давать те же числа — иначе оболочка разошлась с моделью.</summary>
public class PanelBodyTests
{
    private static PanelBody Panel(Vector3Int dims, int gap = PanelBody.DEFAULT_GAP_MM) =>
        PanelBody.OfSize(dims).WithGaps(PanelBody.UniformGaps(gap));

    private static void Extent(Vector3[] verts, out Vector3 min, out Vector3 max)
    {
        min = verts[0];
        max = verts[0];
        foreach (var v in verts)
        {
            min = Vector3.Min(min, v);
            max = Vector3.Max(max, v);
        }
    }

    [Test]
    public void Panel_DefaultGap_IsOneMillimetre()
    {
        var panel = Panel(new Vector3Int(383, 376, 3));
        Assert.AreEqual(1, PanelBody.DEFAULT_GAP_MM);
        Assert.AreEqual(1, panel.Gaps.Left);
        Assert.AreEqual(6, panel.GapMM, "сумма шести зазоров по 1 мм");
    }

    [Test]
    public void Panel_NegativeGap_IsClampedToZero()
    {
        var gaps = PanelBody.UniformGaps(-5);
        Assert.AreEqual(0, gaps.Left);
        Assert.AreEqual(0, gaps.Back,
            "отрицательный зазор ужал бы габарит внутрь детали — такого зазора не бывает");
    }

    [Test]
    public void Panel_BoundingBox_IsBiggerThanMeshByGaps()
    {
        var panel = Panel(new Vector3Int(383, 376, 3), gap: 1);
        Extent(panel.Vertices(), out var min, out var max);

        // Номинал = физический размер + зазоры с обеих сторон.
        Assert.AreEqual(383 + 2, (max.x - min.x) / AppConstants.MM_TO_UNITS, 1e-2f);
        Assert.AreEqual(376 + 2, (max.y - min.y) / AppConstants.MM_TO_UNITS, 1e-2f);
    }

    [Test]
    public void Panel_Thickness_GrowsByFrontAndBackGaps()
    {
        var panel = Panel(new Vector3Int(383, 376, 3), gap: 5);
        Extent(panel.Vertices(), out var min, out var max);

        Assert.AreEqual(3 + 5 + 5, (max.z - min.z) / AppConstants.MM_TO_UNITS, 1e-2f,
            "зазор спереди и сзади входит в габарит так же, как боковой");
    }

    /// <summary>Только боковые зазоры толщину по-прежнему не трогают: панель
    /// сидит в пазу по пласти, и правка ширины зазора не должна её распирать.</summary>
    [Test]
    public void Panel_SideGaps_LeaveThicknessAlone()
    {
        var panel = Panel(new Vector3Int(383, 376, 3), gap: 0)
            .WithGaps(new BoxGaps(5, 5, 5, 5, 0, 0));
        Extent(panel.Vertices(), out var min, out var max);

        Assert.AreEqual(3, (max.z - min.z) / AppConstants.MM_TO_UNITS, 1e-2f);
    }

    [Test]
    public void Panel_HasSixFaces_SizedByNominal()
    {
        var panel = Panel(new Vector3Int(383, 376, 3), gap: 1);
        var faces = panel.Faces();

        Assert.AreEqual(6, faces.Length);
        // Грань, нормальная к Z (пласть), имеет размеры номинала 385×378.
        var front = faces[4];
        Assert.AreEqual(385, front.size.x / AppConstants.MM_TO_UNITS, 1e-2f);
        Assert.AreEqual(378, front.size.y / AppConstants.MM_TO_UNITS, 1e-2f);
    }

    /// <summary>Поза — часть модели, а не сцены: сдвинутая и повёрнутая панель
    /// считается тем же кодом, без transform.</summary>
    [Test]
    public void Panel_MovedAndTurned_KeepsItsNominalExtent()
    {
        var at = PanelBody.OfSize(new Vector3Int(383, 376, 3))
            .At(new Vector3(1.5665f, 1.81f, -2.566f), ManagedRotation.Euler(0f, 90f, 0f));
        Extent(at.Vertices(), out var min, out var max);

        Assert.AreEqual(385, (max.z - min.z) / AppConstants.MM_TO_UNITS, 1e-2f,
            "поворот на 90° по Y уводит ширину номинала на ось Z");
        Assert.AreEqual(378, (max.y - min.y) / AppConstants.MM_TO_UNITS, 1e-2f);
        Assert.AreEqual(1.81f, (min.y + max.y) * 0.5f, 1e-4f, "центр остался там, куда поставили");
    }
}
