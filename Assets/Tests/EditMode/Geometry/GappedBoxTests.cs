using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Проёмный бокс: габарит С УЧЁТОМ зазоров, по которому работают
/// прилипание, валидация и ручки. Физический меш меньше на зазоры — прилипает
/// НОМИНАЛ, а зазор остаётся внутри детали. Так устроены фасад (зазор от проёма)
/// и вкладная панель (технологический зазор в пазу).
///
/// Зазоры АСИММЕТРИЧНЫ, поэтому бокс может быть не центрирован вокруг
/// трансформа — это и есть главный источник ошибок здесь.</summary>
public class GappedBoxTests
{
    private const float U = AppConstants.MM_TO_UNITS;

    /// <summary>Деталь 600×400×18 мм.</summary>
    private static Vector3 Physical => new Vector3(600f * U, 400f * U, 18f * U);

    [Test]
    public void NoGaps_BoxEqualsPhysicalSize()
    {
        var scale = GappedBox.EffectiveScale(Physical, BoxGaps.None);

        Assert.AreEqual(Physical.x, scale.x, 1e-6f);
        Assert.AreEqual(Physical.y, scale.y, 1e-6f);
        Assert.AreEqual(Physical.z, scale.z, 1e-6f);
    }

    [Test]
    public void EffectiveScale_GrowsByTheSumOfOppositeGaps()
    {
        var gaps = new BoxGaps(left: 2, right: 3, top: 4, bottom: 5);

        var scale = GappedBox.EffectiveScale(Physical, gaps);

        Assert.AreEqual(Physical.x + 5f * U, scale.x, 1e-6f, "ширина растёт на left+right");
        Assert.AreEqual(Physical.y + 9f * U, scale.y, 1e-6f, "высота растёт на top+bottom");
    }

    /// <summary>Толщина зазорами не меняется: деталь вкладывается в паз по
    /// пласти, а не по торцу.</summary>
    [Test]
    public void EffectiveScale_LeavesThicknessAlone()
    {
        var gaps = new BoxGaps(9, 9, 9, 9);

        Assert.AreEqual(Physical.z, GappedBox.EffectiveScale(Physical, gaps).z, 1e-6f);
    }

    /// <summary>Каждый зазор двигает СВОЮ границу и только её: перепутанные
    /// местами left/right или top/bottom сдвинут фасад в проёме.</summary>
    [Test]
    public void CornerUnits_EachGapMovesItsOwnEdge()
    {
        var gaps = new BoxGaps(left: 2, right: 3, top: 4, bottom: 5);

        GappedBox.CornerUnits(Physical, gaps, out var minX, out var maxX,
            out var minY, out var maxY, out var minZ, out var maxZ);

        Assert.AreEqual(-Physical.x * 0.5f - 2f * U, minX, 1e-6f, "левая граница ушла на left");
        Assert.AreEqual(Physical.x * 0.5f + 3f * U, maxX, 1e-6f, "правая граница ушла на right");
        Assert.AreEqual(-Physical.y * 0.5f - 5f * U, minY, 1e-6f, "нижняя граница ушла на bottom");
        Assert.AreEqual(Physical.y * 0.5f + 4f * U, maxY, 1e-6f, "верхняя граница ушла на top");
        Assert.AreEqual(-Physical.z * 0.5f, minZ, 1e-6f, "толщину зазоры не трогают");
        Assert.AreEqual(Physical.z * 0.5f, maxZ, 1e-6f);
    }

    [Test]
    public void Vertices_CoverEveryCornerOfTheBox()
    {
        var gaps = new BoxGaps(left: 2, right: 3, top: 4, bottom: 5);

        var verts = GappedBox.Vertices(Physical, gaps, Vector3.zero, Quaternion.identity);
        ElementGeometry.BoundsOf(verts, out var min, out var max);

        Assert.AreEqual(8, verts.Length);
        GappedBox.CornerUnits(Physical, gaps, out var minX, out var maxX,
            out var minY, out var maxY, out var minZ, out var maxZ);
        Assert.AreEqual(minX, min.x, 1e-6f);
        Assert.AreEqual(maxX, max.x, 1e-6f);
        Assert.AreEqual(minY, min.y, 1e-6f);
        Assert.AreEqual(maxY, max.y, 1e-6f);
        Assert.AreEqual(minZ, min.z, 1e-6f);
        Assert.AreEqual(maxZ, max.z, 1e-6f);
    }

    [Test]
    public void Vertices_FollowPositionAndRotation()
    {
        var pos = new Vector3(1f, 2f, 3f);

        var verts = GappedBox.Vertices(Physical, BoxGaps.None, pos, Quaternion.identity);
        ElementGeometry.BoundsOf(verts, out var min, out var max);

        Assert.AreEqual(pos.x, (min.x + max.x) * 0.5f, 1e-6f, "бокс центрирован на позиции");
        Assert.AreEqual(pos.y, (min.y + max.y) * 0.5f, 1e-6f);
        Assert.AreEqual(pos.z, (min.z + max.z) * 0.5f, 1e-6f);
    }

    /// <summary>Из-за асимметричных зазоров центр бокса СМЕЩЁН относительно
    /// трансформа, и грани обязаны считаться от смещённого центра. Иначе фасад
    /// прилипает на половину разницы зазоров мимо проёма.</summary>
    [Test]
    public void Faces_AreBuiltAroundTheShiftedCentre()
    {
        var gaps = new BoxGaps(left: 0, right: 10, top: 0, bottom: 0);

        var faces = GappedBox.Faces(Physical, gaps, Vector3.zero, Quaternion.identity);

        float expectedShift = 10f * U * 0.5f;
        float centreX = (faces[0].center.x + faces[1].center.x) * 0.5f;
        Assert.AreEqual(expectedShift, centreX, 1e-6f,
            "центр обязан уехать на половину одностороннего зазора");
    }

    [Test]
    public void Faces_KeepTheAxisOrderContract()
    {
        var faces = GappedBox.Faces(Physical, BoxGaps.None, Vector3.zero, Quaternion.identity);

        Assert.AreEqual(6, faces.Length);
        var axes = new[] { Vector3.right, Vector3.up, Vector3.forward };
        for (int i = 0; i < 6; i++)
        {
            var expected = (i % 2 == 0) ? axes[i / 2] : -axes[i / 2];
            Assert.AreEqual(1f, Vector3.Dot(expected, faces[i].normal), 1e-4f,
                $"грань {i}: index/2 = ось, чётный = плюс");
        }
    }

    [Test]
    public void Faces_SizesGrowWithGaps()
    {
        var gaps = new BoxGaps(left: 2, right: 3, top: 4, bottom: 5);

        var faces = GappedBox.Faces(Physical, gaps, Vector3.zero, Quaternion.identity);

        // Грань по X: её размеры — высота и толщина проёмного бокса.
        Assert.AreEqual(Physical.y + 9f * U, faces[0].size.x, 1e-6f);
        Assert.AreEqual(Physical.z, faces[0].size.y, 1e-6f);
        // Грань по Z: ширина и высота проёмного бокса.
        Assert.AreEqual(Physical.x + 5f * U, faces[4].size.x, 1e-6f);
        Assert.AreEqual(Physical.y + 9f * U, faces[4].size.y, 1e-6f);
    }

    /// <summary>Поворот детали уносит грани вместе с ней, а размеры оставляет.
    ///
    /// Кватернион задан ЛИТЕРАЛОМ, а не через Quaternion.Euler: тест исполняется
    /// и под dotnet, где конструирование поворотов — вызов в нативный движок и
    /// падает с SecurityException. Здесь это поворот на 90° вокруг Y.</summary>
    [Test]
    public void Faces_FollowRotation()
    {
        const float s = 0.70710678f; // sin(45°) = cos(45°)
        var rot = new Quaternion(0f, s, 0f, s);

        var faces = GappedBox.Faces(Physical, BoxGaps.None, Vector3.zero, rot);

        Assert.AreEqual(1f, Vector3.Dot(rot * Vector3.right, faces[0].normal), 1e-3f);
        Assert.AreEqual(Physical.y, faces[0].size.x, 1e-6f, "поворот не меняет размер грани");
    }
}
