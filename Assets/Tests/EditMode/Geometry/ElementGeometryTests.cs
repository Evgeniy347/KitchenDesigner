using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Снимок геометрии — фундамент ядра: на нём стоят прилипание, ресайз
/// и все тесты, строящие сцену без Unity. Ошибка здесь тихо испортит всё
/// остальное, поэтому проверяются и порядок граней, и габарит.</summary>
public class ElementGeometryTests
{
    private const float U = AppConstants.MM_TO_UNITS;

    private static Vector3 Size(int x, int y, int z)
        => new Vector3(x * U, y * U, z * U);

    [Test]
    public void Box_HasSixFacesAndKeepsTheAxisOrderContract()
    {
        var box = ElementGeometry.Box("b", Vector3.zero, Size(600, 400, 18));

        Assert.AreEqual(6, box.Faces.Length);
        var axes = new[] { Vector3.right, Vector3.up, Vector3.forward };
        for (int i = 0; i < 6; i++)
        {
            var expected = (i % 2 == 0) ? axes[i / 2] : -axes[i / 2];
            Assert.AreEqual(1f, Vector3.Dot(expected, box.Faces[i].normal), 1e-4f,
                $"грань {i}: index/2 = ось, чётный индекс = положительное направление");
        }
    }

    [Test]
    public void Box_PlacesFacesAtHalfExtentFromTheCentre()
    {
        var centre = new Vector3(1f, 2f, 3f);
        var size = Size(600, 400, 18);

        var box = ElementGeometry.Box("b", centre, size);

        Assert.AreEqual(centre.x + size.x * 0.5f, box.Faces[0].center.x, 1e-6f);
        Assert.AreEqual(centre.x - size.x * 0.5f, box.Faces[1].center.x, 1e-6f);
        Assert.AreEqual(centre.y + size.y * 0.5f, box.Faces[2].center.y, 1e-6f);
        Assert.AreEqual(centre.z + size.z * 0.5f, box.Faces[4].center.z, 1e-6f);
    }

    [Test]
    public void Box_FaceSizeIsTheOtherTwoDimensions()
    {
        var size = Size(600, 400, 18);

        var box = ElementGeometry.Box("b", Vector3.zero, size);

        Assert.AreEqual(size.y, box.Faces[0].size.x, 1e-6f, "грань X: ширина = Y");
        Assert.AreEqual(size.z, box.Faces[0].size.y, 1e-6f, "грань X: высота = Z");
        Assert.AreEqual(size.x, box.Faces[2].size.x, 1e-6f, "грань Y: ширина = X");
        Assert.AreEqual(size.x, box.Faces[4].size.x, 1e-6f, "грань Z: ширина = X");
        Assert.AreEqual(size.y, box.Faces[4].size.y, 1e-6f, "грань Z: высота = Y");
    }

    [Test]
    public void Box_BoundsWrapTheBodyExactly()
    {
        var centre = new Vector3(1f, 2f, 3f);
        var size = Size(600, 400, 18);

        var box = ElementGeometry.Box("b", centre, size);

        Assert.AreEqual(centre.x - size.x * 0.5f, box.Min.x, 1e-6f);
        Assert.AreEqual(centre.y - size.y * 0.5f, box.Min.y, 1e-6f);
        Assert.AreEqual(centre.z - size.z * 0.5f, box.Min.z, 1e-6f);
        Assert.AreEqual(centre.x + size.x * 0.5f, box.Max.x, 1e-6f);
        Assert.AreEqual(centre.y + size.y * 0.5f, box.Max.y, 1e-6f);
        Assert.AreEqual(centre.z + size.z * 0.5f, box.Max.z, 1e-6f);
    }

    [Test]
    public void Box_CarriesNameAndPanelFlag()
    {
        var plain = ElementGeometry.Box("plain", Vector3.zero, Size(600, 400, 18));
        var panel = ElementGeometry.Box("panel", Vector3.zero, Size(600, 400, 4), isPanel: true);

        Assert.AreEqual("plain", plain.Name);
        Assert.IsFalse(plain.IsPanel);
        Assert.IsTrue(panel.IsPanel, "только вкладной панели предлагается дно паза");
    }

    [Test]
    public void Box_HasNoGroovesByDefault()
    {
        var box = ElementGeometry.Box("b", Vector3.zero, Size(600, 400, 18));

        Assert.IsEmpty(box.GrooveSeatFaces);
        Assert.IsEmpty(box.GrooveWallFaces);
    }

    [Test]
    public void DefaultSnapshot_IsEmpty()
    {
        Assert.IsTrue(default(ElementGeometry).IsEmpty);
        Assert.IsFalse(ElementGeometry.Box("b", Vector3.zero, Size(600, 400, 18)).IsEmpty);
    }

    [Test]
    public void BoundsOf_TakesTheExtremeOfEveryAxis()
    {
        var verts = new[]
        {
            new Vector3(-1f, 5f, 0f),
            new Vector3(4f, -2f, 7f),
            new Vector3(0f, 0f, -3f),
        };

        ElementGeometry.BoundsOf(verts, out var min, out var max);

        Assert.AreEqual(new Vector3(-1f, -2f, -3f), min);
        Assert.AreEqual(new Vector3(4f, 5f, 7f), max);
    }

    [Test]
    public void BoundsOf_SinglePointCollapsesToIt()
    {
        var p = new Vector3(1f, 2f, 3f);

        ElementGeometry.BoundsOf(new[] { p }, out var min, out var max);

        Assert.AreEqual(p, min);
        Assert.AreEqual(p, max);
    }
}
