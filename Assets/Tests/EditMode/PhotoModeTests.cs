using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class PhotoModeTests
{
    // ── PhotoMode.ResolveTransparent ────────────────────────

    [Test]
    public void ResolveTransparent_Inactive_KeepsFlag()
    {
        Assert.IsTrue(PhotoMode.ResolveTransparent(true, photoActive: false));
        Assert.IsFalse(PhotoMode.ResolveTransparent(false, photoActive: false));
    }

    [Test]
    public void ResolveTransparent_Active_ForcesOpaque()
    {
        Assert.IsFalse(PhotoMode.ResolveTransparent(true, photoActive: true));
        Assert.IsFalse(PhotoMode.ResolveTransparent(false, photoActive: true));
    }

    // ── CeilingGeometry.TryCompute ──────────────────────────

    [Test]
    public void Ceiling_NoWalls_ReturnsFalse()
    {
        Assert.IsFalse(CeilingGeometry.TryCompute(new List<Bounds>(), out _));
        Assert.IsFalse(CeilingGeometry.TryCompute(null!, out _));
    }

    [Test]
    public void Ceiling_DegenerateWalls_ReturnsFalse()
    {
        var walls = new List<Bounds> { new Bounds(Vector3.zero, Vector3.zero) };
        Assert.IsFalse(CeilingGeometry.TryCompute(walls, out _));
    }

    [Test]
    public void Ceiling_SingleWall_SitsOnTopAndCoversFootprint()
    {
        // Стена 2×2.5×0.1, центр (0, 1.25, 0): верх на y=2.5.
        var wall = new Bounds(new Vector3(0f, 1.25f, 0f), new Vector3(2f, 2.5f, 0.1f));
        Assert.IsTrue(CeilingGeometry.TryCompute(new List<Bounds> { wall }, out var ceil));

        // Низ плиты — точно на верхе стены.
        Assert.AreEqual(2.5f, ceil.min.y, 1e-4f, "ceiling bottom must rest on wall top");
        // Толщина плиты.
        Assert.AreEqual(CeilingGeometry.ThicknessUnits, ceil.size.y, 1e-4f);
        // XZ-контур с запасом.
        Assert.AreEqual(2f + CeilingGeometry.MarginUnits * 2f, ceil.size.x, 1e-4f);
        Assert.AreEqual(0.1f + CeilingGeometry.MarginUnits * 2f, ceil.size.z, 1e-4f);
        Assert.AreEqual(0f, ceil.center.x, 1e-4f);
        Assert.AreEqual(0f, ceil.center.z, 1e-4f);
    }

    [Test]
    public void Ceiling_MultipleWalls_EncapsulatesAll()
    {
        // Две противоположные стены комнаты 3×2 (юниты), высота 2.5.
        var w1 = new Bounds(new Vector3(-1.5f, 1.25f, 0f), new Vector3(0.1f, 2.5f, 2f));
        var w2 = new Bounds(new Vector3(1.5f, 1.25f, 0f), new Vector3(0.1f, 2.5f, 2f));
        Assert.IsTrue(CeilingGeometry.TryCompute(new List<Bounds> { w1, w2 }, out var ceil));

        // Контур охватывает обе стены по X (от -1.55 до 1.55) + запас.
        Assert.AreEqual(3.1f + CeilingGeometry.MarginUnits * 2f, ceil.size.x, 1e-4f);
        Assert.AreEqual(2f + CeilingGeometry.MarginUnits * 2f, ceil.size.z, 1e-4f);
        Assert.AreEqual(2.5f, ceil.min.y, 1e-4f);
    }

    // ── PhotoQualityPresetTable (пресет = набор тумблеров) ───

    [Test]
    public void Preset_High_HasSupersamplingAndSoftShadows()
    {
        var p = PhotoQualityPresetTable.Resolve(PhotoQualityPreset.High);
        Assert.IsTrue(p.Supersampling, "High использует супер-сэмплинг");
        Assert.IsTrue(p.SoftShadows);
        Assert.IsTrue(p.AntiAliasing);
    }

    [Test]
    public void Preset_Low_IsLightest()
    {
        var low = PhotoQualityPresetTable.Resolve(PhotoQualityPreset.Low);
        Assert.IsFalse(low.Supersampling);
        Assert.IsFalse(low.SoftShadows);
        Assert.IsFalse(low.Bloom);
        Assert.IsTrue(low.Shadows, "тени есть даже в низком пресете");
    }

    [Test]
    public void Preset_Apply_ThenDetect_Roundtrips()
    {
        var gs = KitchenSettings.Instance;
        var before = gs.ToData();

        foreach (var preset in new[] { PhotoQualityPreset.Low, PhotoQualityPreset.Medium, PhotoQualityPreset.High })
        {
            PhotoQualityPresetTable.Apply(preset, gs);
            Assert.AreEqual(preset, PhotoQualityPresetTable.Detect(gs), $"после Apply({preset}) должен определяться он же");
        }

        gs.ApplyFrom(before);
    }

    [Test]
    public void Preset_ManualChange_BecomesCustom()
    {
        var gs = KitchenSettings.Instance;
        var before = gs.ToData();

        PhotoQualityPresetTable.Apply(PhotoQualityPreset.High, gs);
        gs.PhotoBloom = false; // ручное изменение любого привязанного тумблера
        Assert.AreEqual(PhotoQualityPreset.Custom, PhotoQualityPresetTable.Detect(gs));

        gs.ApplyFrom(before);
    }

    [Test]
    public void Preset_Apply_Custom_IsNoOp()
    {
        var gs = KitchenSettings.Instance;
        var before = gs.ToData();

        PhotoQualityPresetTable.Apply(PhotoQualityPreset.Low, gs);
        PhotoQualityPresetTable.Apply(PhotoQualityPreset.Custom, gs); // не должен ничего менять
        Assert.AreEqual(PhotoQualityPreset.Low, PhotoQualityPresetTable.Detect(gs));

        gs.ApplyFrom(before);
    }

    [Test]
    public void Preset_Next_CyclesNamedPresets()
    {
        Assert.AreEqual(PhotoQualityPreset.Medium, PhotoQualityPresetTable.Next(PhotoQualityPreset.Low));
        Assert.AreEqual(PhotoQualityPreset.High, PhotoQualityPresetTable.Next(PhotoQualityPreset.Medium));
        Assert.AreEqual(PhotoQualityPreset.Low, PhotoQualityPresetTable.Next(PhotoQualityPreset.High));
        Assert.AreEqual(PhotoQualityPreset.Low, PhotoQualityPresetTable.Next(PhotoQualityPreset.Custom));
    }

    // ── KitchenSettings: photo fields persistence ───────────

    [Test]
    public void PhotoSettings_RoundTrip()
    {
        var gs = KitchenSettings.Instance;
        var before = gs.ToData();

        gs.PhotoQuality = PhotoQualityPreset.Custom;
        gs.PhotoShadows = false;
        gs.PhotoSoftShadows = false;
        gs.PhotoAntiAliasing = false;
        gs.PhotoSupersampling = false;
        gs.PhotoAmbientOcclusion = false;
        gs.PhotoBloom = false;
        gs.PhotoVignette = false;
        gs.PhotoCeiling = false;

        var data = gs.ToData();

        gs.PhotoQuality = PhotoQualityPreset.High;
        gs.PhotoShadows = true;
        gs.PhotoSoftShadows = true;
        gs.PhotoAntiAliasing = true;
        gs.PhotoSupersampling = true;
        gs.PhotoAmbientOcclusion = true;
        gs.PhotoBloom = true;
        gs.PhotoVignette = true;
        gs.PhotoCeiling = true;

        gs.ApplyFrom(data);

        Assert.AreEqual(PhotoQualityPreset.Custom, gs.PhotoQuality);
        Assert.IsFalse(gs.PhotoShadows);
        Assert.IsFalse(gs.PhotoSoftShadows);
        Assert.IsFalse(gs.PhotoAntiAliasing);
        Assert.IsFalse(gs.PhotoSupersampling);
        Assert.IsFalse(gs.PhotoAmbientOcclusion);
        Assert.IsFalse(gs.PhotoBloom);
        Assert.IsFalse(gs.PhotoVignette);
        Assert.IsFalse(gs.PhotoCeiling);

        gs.ApplyFrom(before);
    }

    // ── Источник света: температура и мощность ──────────────

    [Test]
    public void LightSource_Temperature_SetsColor_Power_SetsIntensity()
    {
        var go = new GameObject("Light");
        var ls = go.AddComponent<LightSourceElement>();
        ls.EnsureLight();

        ls.TemperatureK = 6500;
        var expected = Mathf.CorrelatedColorTemperatureToRGB(6500);
        Assert.AreEqual(expected.r, ls.PointLight!.color.r, 0.01f);
        Assert.AreEqual(expected.g, ls.PointLight!.color.g, 0.01f);
        Assert.AreEqual(expected.b, ls.PointLight!.color.b, 0.01f);

        float i9 = ls.PointLight!.intensity;
        ls.PowerW = 18;
        Assert.Greater(ls.PointLight!.intensity, i9, "больше ватт → ярче");

        ls.DiffusionPct = 10;
        float rNarrow = ls.PointLight!.range;
        ls.DiffusionPct = 90;
        Assert.Greater(ls.PointLight!.range, rNarrow, "больше рассеивание → шире радиус");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void LightSource_HasEmissivePlafond()
    {
        var go = new GameObject("Light");
        go.AddComponent<MeshFilter>();
        go.AddComponent<MeshRenderer>();
        var ls = go.AddComponent<LightSourceElement>();
        ls.EnsureLight();

        var mr = go.GetComponent<MeshRenderer>();
        Assert.IsTrue(mr.sharedMaterial.IsKeywordEnabled("_EMISSION"), "плафон должен светиться, а не быть чёрным");
        var emission = mr.sharedMaterial.GetColor("_EmissionColor");
        Assert.Greater(emission.maxColorComponent, 0f, "эмиссия не нулевая");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void LightSource_Temperature_Clamped()
    {
        var go = new GameObject("Light");
        var ls = go.AddComponent<LightSourceElement>();
        ls.TemperatureK = 999999;
        Assert.AreEqual(LightSourceElement.MAX_TEMPERATURE_K, ls.TemperatureK);
        ls.TemperatureK = 0;
        Assert.AreEqual(LightSourceElement.MIN_TEMPERATURE_K, ls.TemperatureK);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void ResetToDefaults_SetsHighQualityAndEffectsOn()
    {
        var gs = KitchenSettings.Instance;
        var before = gs.ToData();

        gs.PhotoQuality = PhotoQualityPreset.Low;
        gs.PhotoShadows = false;
        gs.ResetToDefaults();

        Assert.AreEqual(PhotoQualityPreset.High, gs.PhotoQuality);
        Assert.IsTrue(gs.PhotoShadows);
        Assert.IsTrue(gs.PhotoCeiling);

        gs.ApplyFrom(before);
    }

    [Test]
    public void ApplyFrom_LegacyData_KeepsGoodPhotoDefaults()
    {
        // Старый проект без фото-полей: JsonUtility даёт объект с
        // инициализаторами (High/true), а не с нулями.
        var legacy = JsonUtility.FromJson<KitchenSettingsData>(
            "{\"gridStep\":18,\"wallsEnabled\":true}");

        Assert.AreEqual((int)PhotoQualityPreset.High, legacy.photoQuality);
        Assert.IsTrue(legacy.photoShadows);
        Assert.IsTrue(legacy.photoCeiling);
    }
}
