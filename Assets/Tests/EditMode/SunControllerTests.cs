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
    [Test]
    public void HorizonToZenith01_RisesFromZeroAtSunriseToOneAtNoon_AndIsZeroAtNight()
    {
        SunController.SetTimeOfDay(SunController.SUNRISE_HOUR);
        Assert.AreEqual(0f, SunController.HorizonToZenith01, 0.001f, "на восходе солнце у горизонта");

        SunController.SetTimeOfDay(12f);
        Assert.AreEqual(1f, SunController.HorizonToZenith01, 0.001f, "в полдень — зенит");

        SunController.SetTimeOfDay(SunController.SUNSET_HOUR);
        Assert.AreEqual(0f, SunController.HorizonToZenith01, 0.001f, "на закате снова у горизонта");

        SunController.SetTimeOfDay(0f);
        Assert.AreEqual(0f, SunController.HorizonToZenith01, 0.001f,
            "ночью доля дня зажата в ноль: отрицательная высота не должна утекать в цвет и в ambient");
    }

    [Test]
    public void Night_LightsTheSceneWithColdMoonFromTheOppositeSideOfTheSky()
    {
        var go = new GameObject("TestSun");
        var light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        RenderSettings.sun = light;
        try
        {
            SunController.SetAzimuth(90f);
            SunController.SetTimeOfDay(12f);
            float dayAzimuth = light.transform.rotation.eulerAngles.y;

            SunController.SetTimeOfDay(0f);

            Assert.Greater(light.color.b, light.color.r,
                "ночная подсветка холодная: тёплый цвет ночью читается как закат, а не как луна");
            Assert.AreEqual(
                Mathf.Repeat(dayAzimuth + SunController.MOON_AZIMUTH_OFFSET_DEG, 360f),
                Mathf.Repeat(light.transform.rotation.eulerAngles.y, 360f), 0.5f,
                "луна светит с противоположной стороны неба");
            Assert.Greater(light.transform.rotation.eulerAngles.x, 0f,
                "источник ночью всё равно над горизонтом, иначе сцена чернеет");
        }
        finally
        {
            RenderSettings.sun = null;
            Object.DestroyImmediate(go);
            SunController.Reset();
        }
    }

    [Test]
    public void Sun_FollowsAReassignedSceneSun_WithoutCachingTheOldOne()
    {
        var first = new GameObject("Sun1");
        first.AddComponent<Light>().type = LightType.Directional;
        var second = new GameObject("Sun2");
        second.AddComponent<Light>().type = LightType.Directional;
        try
        {
            RenderSettings.sun = first.GetComponent<Light>();
            Assert.AreSame(first.GetComponent<Light>(), SunController.Sun);

            RenderSettings.sun = second.GetComponent<Light>();
            Assert.AreSame(second.GetComponent<Light>(), SunController.Sun,
                "RenderSettings.sun не кэшируется: сцена и тесты подменяют солнце в любой момент");
        }
        finally
        {
            RenderSettings.sun = null;
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
            SunController.Reset();
        }
    }
}
