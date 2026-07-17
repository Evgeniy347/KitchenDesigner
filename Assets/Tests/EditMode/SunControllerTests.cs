using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class SunControllerTests
{
    [SetUp]
    public void SetUp() => SunController.Reset();

    [TearDown]
    public void TearDown() => SunController.Reset();

    [Test]
    public void Noon_ElevationIsMax()
    {
        SunController.SetTimeOfDay(12f);
        Assert.AreEqual(SunController.MAX_ELEVATION_DEG, SunController.ElevationDeg, 0.01f);
        Assert.IsFalse(SunController.IsNight);
    }

    [Test]
    public void SunriseAndSunset_ElevationIsZero()
    {
        SunController.SetTimeOfDay(6f);
        Assert.AreEqual(0f, SunController.ElevationDeg, 0.01f);

        SunController.SetTimeOfDay(18f);
        Assert.AreEqual(0f, SunController.ElevationDeg, 0.01f);
    }

    [Test]
    public void Midnight_IsNight()
    {
        SunController.SetTimeOfDay(0f);
        Assert.IsTrue(SunController.IsNight);
        Assert.Less(SunController.ElevationDeg, 0f);
    }

    [Test]
    public void SetTimeOfDay_ClampsToDayRange()
    {
        SunController.SetTimeOfDay(-5f);
        Assert.AreEqual(0f, SunController.TimeOfDay, 0.001f);

        SunController.SetTimeOfDay(30f);
        Assert.AreEqual(24f, SunController.TimeOfDay, 0.001f);
    }

    [Test]
    public void SetAzimuth_WrapsAround360()
    {
        SunController.SetAzimuth(370f);
        Assert.AreEqual(10f, SunController.Azimuth, 0.001f);

        SunController.SetAzimuth(-30f);
        Assert.AreEqual(330f, SunController.Azimuth, 0.001f);
    }

    [Test]
    public void SetIntensity_Clamped()
    {
        SunController.SetIntensity(-1f);
        Assert.AreEqual(0f, SunController.Intensity, 0.001f);

        SunController.SetIntensity(100f);
        Assert.AreEqual(3f, SunController.Intensity, 0.001f);
    }

    [Test]
    public void Reset_RestoresDefaults()
    {
        SunController.SetTimeOfDay(3f);
        SunController.SetAzimuth(200f);
        SunController.SetIntensity(2f);

        SunController.Reset();

        Assert.AreEqual(SunController.DEFAULT_TIME, SunController.TimeOfDay, 0.001f);
        Assert.AreEqual(SunController.DEFAULT_AZIMUTH, SunController.Azimuth, 0.001f);
        Assert.AreEqual(SunController.DEFAULT_INTENSITY, SunController.Intensity, 0.001f);
    }

    [Test]
    public void Apply_MovesSceneSun()
    {
        var go = new GameObject("TestSun");
        var light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        RenderSettings.sun = light;
        try
        {
            SunController.SetTimeOfDay(12f);
            float noonX = light.transform.rotation.eulerAngles.x;
            Assert.AreEqual(SunController.MAX_ELEVATION_DEG, noonX, 0.1f);

            SunController.SetTimeOfDay(0f);
            Assert.Less(light.intensity, 0.2f, "ночью солнце почти погашено");
        }
        finally
        {
            RenderSettings.sun = null;
            Object.DestroyImmediate(go);
            SunController.Reset();
        }
    }
}
