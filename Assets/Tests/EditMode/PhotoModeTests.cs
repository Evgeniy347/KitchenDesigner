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

    // ── PhotoQualityPresetTable.Resolve ─────────────────────

    [Test]
    public void Preset_High_HasSupersamplingAndSoftShadows()
    {
        var p = PhotoQualityPresetTable.Resolve(PhotoQualityPreset.High);
        Assert.Greater(p.RenderScale, 1f, "High использует супер-сэмплинг");
        Assert.IsTrue(p.SoftShadows);
        Assert.GreaterOrEqual(p.MsaaSamples, 4);
    }

    [Test]
    public void Preset_Low_IsLightest()
    {
        var low = PhotoQualityPresetTable.Resolve(PhotoQualityPreset.Low);
        var high = PhotoQualityPresetTable.Resolve(PhotoQualityPreset.High);
        Assert.LessOrEqual(low.RenderScale, high.RenderScale);
        Assert.LessOrEqual(low.ShadowDistance, high.ShadowDistance);
        Assert.IsFalse(low.SoftShadows);
    }

    [Test]
    public void Preset_Monotonic_ShadowDistance()
    {
        var low = PhotoQualityPresetTable.Resolve(PhotoQualityPreset.Low);
        var med = PhotoQualityPresetTable.Resolve(PhotoQualityPreset.Medium);
        var high = PhotoQualityPresetTable.Resolve(PhotoQualityPreset.High);
        Assert.Less(low.ShadowDistance, med.ShadowDistance);
        Assert.Less(med.ShadowDistance, high.ShadowDistance);
    }

    // ── KitchenSettings: photo fields persistence ───────────

    [Test]
    public void PhotoSettings_RoundTrip()
    {
        var gs = KitchenSettings.Instance;
        var before = gs.ToData();

        gs.PhotoQuality = PhotoQualityPreset.Low;
        gs.PhotoShadows = false;
        gs.PhotoAntiAliasing = false;
        gs.PhotoAmbientOcclusion = false;
        gs.PhotoBloom = false;
        gs.PhotoVignette = false;
        gs.PhotoCeiling = false;

        var data = gs.ToData();

        gs.PhotoQuality = PhotoQualityPreset.High;
        gs.PhotoShadows = true;
        gs.PhotoAntiAliasing = true;
        gs.PhotoAmbientOcclusion = true;
        gs.PhotoBloom = true;
        gs.PhotoVignette = true;
        gs.PhotoCeiling = true;

        gs.ApplyFrom(data);

        Assert.AreEqual(PhotoQualityPreset.Low, gs.PhotoQuality);
        Assert.IsFalse(gs.PhotoShadows);
        Assert.IsFalse(gs.PhotoAntiAliasing);
        Assert.IsFalse(gs.PhotoAmbientOcclusion);
        Assert.IsFalse(gs.PhotoBloom);
        Assert.IsFalse(gs.PhotoVignette);
        Assert.IsFalse(gs.PhotoCeiling);

        gs.ApplyFrom(before);
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
