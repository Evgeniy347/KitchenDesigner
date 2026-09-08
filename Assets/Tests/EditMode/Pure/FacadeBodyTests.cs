using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Фасад: зазор входит в ГАБАРИТ по X и Y (замочная щель вокруг дверцы),
/// но не по Z — толщина коробки не растёт от зазора, в отличие от ХДФ-панели.
///
/// Здесь фасад — обычный C#-объект: ни GameObject, ни компонента. Сценовая
/// половина того же контракта живёт в FacadeElementTests.Facade_Mirrors*,
/// и она обязана давать те же числа — иначе оболочка разошлась с моделью.</summary>
public class FacadeBodyTests
{
    private static FacadeBody Facade(Vector3Int dims, int gapL, int gapR, int gapT, int gapB) =>
        FacadeBody.OfSize(dims).WithGaps(new BoxGaps(gapL, gapR, gapT, gapB));

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
    public void Facade_DefaultGap_IsTwoMillimetres()
    {
        var f = FacadeBody.OfSize(new Vector3Int(400, 300, 18));
        Assert.AreEqual(2, FacadeBody.DEFAULT_GAP_MM);
        Assert.AreEqual(2, f.Gaps.Left);
        Assert.AreEqual(0, f.Gaps.Front, "зазор не растягивает толщину, в отличие от ХДФ");
        Assert.AreEqual(8, f.GapMM, "сумма четырёх зазоров по 2 мм, перед/зад — 0");
    }

    [Test]
    public void Facade_NegativeGap_IsClampedToZero()
    {
        var gaps = FacadeBody.DefaultGaps(-5);
        Assert.AreEqual(0, gaps.Left);
        Assert.AreEqual(0, gaps.Bottom,
            "отрицательный зазор ужал бы габарит внутрь дверцы — такого зазора не бывает");
    }

    [Test]
    public void Facade_EffectiveScale_AddsGapOnXAndY_NotZ()
    {
        var f = Facade(new Vector3Int(400, 300, 18), 2, 2, 2, 2);
        var faces = f.Faces();

        Assert.AreEqual(0.404f, faces[4].size.x, 1e-5f);
        Assert.AreEqual(0.304f, faces[4].size.y, 1e-5f);

        Assert.AreEqual(0.304f, faces[0].size.x, 1e-5f);
        Assert.AreEqual(0.018f, faces[0].size.y, 1e-5f, "по Z зазор не входит в габарит");
    }

    [Test]
    public void Facade_Vertices_ExtendedOnXAndY_NotZ()
    {
        var f = Facade(new Vector3Int(400, 300, 18), 2, 2, 2, 2);
        var verts = f.Vertices();

        foreach (var v in verts)
        {
            Assert.IsTrue(Mathf.Abs(v.x) <= 0.202f + 1e-5f, $"v.x={v.x} exceeds 0.202");
            Assert.IsTrue(Mathf.Abs(v.y) <= 0.152f + 1e-5f, $"v.y={v.y} exceeds 0.152");
            Assert.IsTrue(Mathf.Abs(v.z) <= 0.009f + 1e-5f, $"v.z={v.z} exceeds 0.009");
        }
    }

    [Test]
    public void Facade_AsymmetricGap_EffectiveBounds()
    {
        var f = Facade(new Vector3Int(400, 300, 18), 1, 3, 0, 5);
        var verts = f.Vertices();

        float minX = -0.201f, maxX = 0.203f;
        float minY = -0.155f, maxY = 0.150f;
        float minZ = -0.009f, maxZ = 0.009f;
        foreach (var v in verts)
        {
            Assert.IsTrue(v.x >= minX - 1e-4f && v.x <= maxX + 1e-4f, $"v.x={v.x} out of [{minX},{maxX}]");
            Assert.IsTrue(v.y >= minY - 1e-4f && v.y <= maxY + 1e-4f, $"v.y={v.y} out of [{minY},{maxY}]");
            Assert.IsTrue(v.z >= minZ - 1e-4f && v.z <= maxZ + 1e-4f, $"v.z={v.z} out of [{minZ},{maxZ}]");
        }

        bool hasMinX = false, hasMaxX = false, hasMinY = false, hasMaxY = false;
        foreach (var v in verts)
        {
            if (Mathf.Abs(v.x - minX) < 1e-5f) hasMinX = true;
            if (Mathf.Abs(v.x - maxX) < 1e-5f) hasMaxX = true;
            if (Mathf.Abs(v.y - minY) < 1e-5f) hasMinY = true;
            if (Mathf.Abs(v.y - maxY) < 1e-5f) hasMaxY = true;
        }
        Assert.IsTrue(hasMinX, "Left edge at -0.201 not found");
        Assert.IsTrue(hasMaxX, "Right edge at 0.203 not found");
        Assert.IsTrue(hasMinY, "Bottom edge at -0.155 not found");
        Assert.IsTrue(hasMaxY, "Top edge at 0.150 not found");
    }

    [Test]
    public void Facade_AsymmetricGap_Faces_LocalBounds()
    {
        var f = Facade(new Vector3Int(400, 300, 18), 1, 3, 0, 5);
        var faces = f.Faces();

        float rightCenter = 0.203f;
        float leftCenter = -0.201f;
        Assert.AreEqual(rightCenter, faces[0].center.x, 2e-5f);
        Assert.AreEqual(leftCenter, faces[1].center.x, 2e-5f);

        float topCenter = 0.150f;
        float bottomCenter = -0.155f;
        Assert.AreEqual(topCenter, faces[2].center.y, 2e-5f);
        Assert.AreEqual(bottomCenter, faces[3].center.y, 2e-5f);

        Assert.AreEqual(0.305f, faces[0].size.x, 2e-5f);
        Assert.AreEqual(0.018f, faces[0].size.y, 2e-5f);
    }

    [Test]
    public void Facade_StillUsesSameGapMath_AsPanel()
    {
        var f = Facade(new Vector3Int(600, 400, 18), 2, 3, 4, 5);
        Extent(f.Vertices(), out var min, out var max);

        Assert.AreEqual(600 + 2 + 3, (max.x - min.x) / AppConstants.MM_TO_UNITS, 1e-2f);
        Assert.AreEqual(400 + 4 + 5, (max.y - min.y) / AppConstants.MM_TO_UNITS, 1e-2f);
    }

    /// <summary>Поза — часть модели, а не сцены: сдвинутая и повёрнутая дверца
    /// считается тем же кодом, без transform. Локальный X (400 + 2 + 2 зазора)
    /// после поворота на 90° по Y уходит на мировую Z; локальный Y (300 + 2 + 2)
    /// поворот не трогает; локальный Z (18, без зазора) уходит на мировую X.</summary>
    [Test]
    public void Facade_MovedAndTurned_KeepsItsNominalExtent()
    {
        var at = FacadeBody.OfSize(new Vector3Int(400, 300, 18))
            .At(new Vector3(1.5665f, 1.81f, -2.566f), ManagedRotation.Euler(0f, 90f, 0f));
        Extent(at.Vertices(), out var min, out var max);

        Assert.AreEqual(404, (max.z - min.z) / AppConstants.MM_TO_UNITS, 1e-2f,
            "поворот на 90° по Y уводит X (номинал + боковые зазоры) на ось Z");
        Assert.AreEqual(304, (max.y - min.y) / AppConstants.MM_TO_UNITS, 1e-2f);
        Assert.AreEqual(18, (max.x - min.x) / AppConstants.MM_TO_UNITS, 1e-2f,
            "по Z зазора нет, поворот отправляет чистый номинал 18 на ось X");
        Assert.AreEqual(1.81f, (min.y + max.y) * 0.5f, 1e-4f, "центр остался там, куда поставили");
    }
}
