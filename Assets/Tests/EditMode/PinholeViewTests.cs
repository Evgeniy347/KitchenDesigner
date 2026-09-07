using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.Handles;
using KitchenDesigner.Core.Measure;

/// <summary>Стенд, на котором меряют, должен сначала доказать, что меряет ТО САМОЕ
/// (CONVENTIONS.md → «Prove the harness before you trust what it measures»).
/// PinholeView — чистая копия проекции камеры, живущая в ядре: без неё свип
/// HandleScreenExtentTests нельзя гонять под dotnet, а с ней он бесполезен, пока
/// не показано, что её пиксели — это пиксели Camera.WorldToScreenPoint.</summary>
public class PinholeViewTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
    }

    private Camera MakeCamera(Vector3 at, Quaternion rotation, float fov = 60f)
    {
        var go = new GameObject("Камера");
        _spawned.Add(go);
        go.transform.SetPositionAndRotation(at, rotation);
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = fov;
        return cam;
    }

    private static readonly Vector3[] Probes =
    {
        new Vector3(0f, 0f, 3f),
        new Vector3(0.4f, 0.25f, 2f),
        new Vector3(-1.2f, 0.8f, 6f),
        new Vector3(0.1f, -0.6f, 12f),
    };

    [Test]
    public void WorldToScreen_MatchesTheCameraItWasBuiltFrom()
    {
        var cam = MakeCamera(new Vector3(0.3f, 1.6f, -2f),
            ManagedRotation.Euler(12f, 8f, 0f));
        var view = HandleView.Of(cam);

        foreach (var probe in Probes)
        {
            Vector3 unity = cam.WorldToScreenPoint(probe);
            Vector3 pure = view.WorldToScreen(probe);

            Assert.AreEqual(unity.x, pure.x, 0.5f, $"x у {probe}");
            Assert.AreEqual(unity.y, pure.y, 0.5f, $"y у {probe}");
            Assert.AreEqual(unity.z, pure.z, 1e-3f,
                $"z — глубина вдоль взгляда, по ней отсеиваются точки за спиной ({probe})");
        }
    }

    [Test]
    public void WorldSizeForPixels_MatchesTheMeasureToolsOwnFormula()
    {
        var cam = MakeCamera(new Vector3(0f, 1f, -3f), Quaternion.identity, 55f);
        var view = HandleView.Of(cam);

        foreach (var probe in Probes)
            Assert.AreEqual(MeasureGeometry.WorldSizeForPixels(cam, probe, 26f),
                view.WorldSizeForPixels(probe, 26f), 1e-5f,
                $"две записи одного контура обязаны совпасть в точке {probe}");
    }

    [Test]
    public void PixelsForWorldSize_IsTheInverseOfWorldSizeForPixels()
    {
        var cam = MakeCamera(new Vector3(0f, 0f, -4f), Quaternion.identity);
        var view = HandleView.Of(cam);
        var probe = new Vector3(0f, 0f, 1f);

        float world = view.WorldSizeForPixels(probe, 42f);
        Assert.AreEqual(42f, view.PixelsForWorldSize(probe, world), 1e-2f,
            "датчик переводит в обе стороны: длина стрелки в пикселях считается "
            + "именно этой обратной функцией");
    }

    [Test]
    public void Orthographic_ScalesByOrthographicSize_NotByDistance()
    {
        var cam = MakeCamera(new Vector3(0f, 0f, -5f), Quaternion.identity);
        cam.orthographic = true;
        cam.orthographicSize = 2f;
        var view = HandleView.Of(cam);

        Assert.AreEqual(view.WorldSizeForPixels(new Vector3(0f, 0f, 0f), 26f),
            view.WorldSizeForPixels(new Vector3(0f, 0f, 40f), 26f), 1e-6f,
            "в ортографии удаление не меняет масштаб — иначе ручки «худели» бы "
            + "там, где картинка этого не делает");
    }
}
