using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Границы механики накладок текстуры: сколько их влезает на элемент и
/// как точка физического луча превращается в миллиметры грани.
///
/// Здесь живут причины, которые раньше были комментариями в TextureOverlay.cs и
/// TextureOverlayPicker.cs. Всё остальное про накладки держит TextureOverlayTests.</summary>
public class TextureOverlayLimitsTests
{
    private static readonly Vector3Int WallDims = new Vector3Int(3000, 1800, 100);
    private const int FaceZPlus = 4;

    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void SetUp() => PartRegistry.Clear();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var el in PartRegistry.GetAll())
            if (el != null) Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
    }

    private KitchenElement CreateWall()
    {
        var go = new GameObject("Стена");
        _spawned.Add(go);
        go.AddComponent<MeshFilter>();
        go.AddComponent<MeshRenderer>();
        go.AddComponent<Wall>();
        var element = go.AddComponent<KitchenElement>();
        element.PartName = "Стена";
        element.DimensionsMM = WallDims;
        element.ApplyDimensions();
        return element;
    }

    private static Vector3 PointOnWall(float uMM, float vMM) => new Vector3(
        (uMM - WallDims.x * 0.5f) * AppConstants.MM_TO_UNITS,
        (vMM - WallDims.y * 0.5f) * AppConstants.MM_TO_UNITS,
        WallDims.z * 0.5f * AppConstants.MM_TO_UNITS);

    [Test]
    public void FacePointMM_WithFloatNoiseFromTheRay_RoundsToTheNearestMillimetre()
    {
        var wall = CreateWall();

        Assert.IsTrue(TextureOverlayPicker.TryFacePointMM(
            wall, FaceZPlus, PointOnWall(1199.9999f, 899.9999f), out var uv));

        Assert.AreEqual(new Vector2Int(1200, 900), uv,
            "точка приходит из физического луча в метрах: на 1200 мм ошибка float легко "
            + "даёт 1199,9999, и отбрасывание дробной части промахнулось бы на миллиметр");
    }

    [Test]
    public void SetTextureOverlays_StopsAtTheMaximumPerElement()
    {
        var wall = CreateWall();
        int max = TextureOverlayGeometry.MAX_PER_ELEMENT;

        var tooMany = new List<TextureOverlaySpec>();
        for (int i = 0; i < max + 5; i++)
            tooMany.Add(TextureOverlaySpec.FullFace(OverlaySide.A, $"decor{i}"));

        wall.SetTextureOverlays(tooMany);

        Assert.AreEqual(max, wall.TextureOverlays.Count,
            "ограничение прикладное, а не техническое: по строке на накладку — и длинный "
            + "список в окне свойств всё равно нечитаем");
    }

    [Test]
    public void SetTextureOverlays_BelowTheLimit_KeepsThemAll()
    {
        var wall = CreateWall();
        int fits = TextureOverlayGeometry.MAX_PER_ELEMENT - 1;

        var some = new List<TextureOverlaySpec>();
        for (int i = 0; i < fits; i++)
            some.Add(TextureOverlaySpec.FullFace(OverlaySide.A, $"decor{i}"));

        wall.SetTextureOverlays(some);

        Assert.AreEqual(fits, wall.TextureOverlays.Count,
            "положительный контроль: до предела ничего не теряется");
    }
}
