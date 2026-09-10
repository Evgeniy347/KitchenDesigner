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
        Assert.AreEqual(2f + CeilingGeometry.OverhangBeyondWallsUnits * 2f, ceil.size.x, 1e-4f);
        Assert.AreEqual(0.1f + CeilingGeometry.OverhangBeyondWallsUnits * 2f, ceil.size.z, 1e-4f);
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
        Assert.AreEqual(3.1f + CeilingGeometry.OverhangBeyondWallsUnits * 2f, ceil.size.x, 1e-4f);
        Assert.AreEqual(2f + CeilingGeometry.OverhangBeyondWallsUnits * 2f, ceil.size.z, 1e-4f);
        Assert.AreEqual(2.5f, ceil.min.y, 1e-4f);
    }

    [Test]
    public void Ceiling_DegenerateWall_DoesNotStretchTheContour()
    {
        var real = new Bounds(new Vector3(0f, 1.25f, 0f), new Vector3(2f, 2.5f, 0.1f));
        var zeroSized = new Bounds(new Vector3(50f, 0f, 50f), Vector3.zero);

        Assert.IsTrue(CeilingGeometry.TryCompute(new List<Bounds> { real, zeroSized }, out var ceil));
        Assert.AreEqual(2f + CeilingGeometry.OverhangBeyondWallsUnits * 2f, ceil.size.x, 1e-4f,
            "вырожденная (нулевая) стена не задаёт контур: иначе потолок растянулся бы до неё");
        Assert.AreEqual(0f, ceil.center.x, 1e-4f);
    }

    [Test]
    public void Ceiling_OverhangsBothOuterFaces_LeavingNoGapAtTheWallThickness()
    {
        var wall = new Bounds(new Vector3(0f, 1.25f, 0f), new Vector3(2f, 2.5f, 0.1f));
        Assert.IsTrue(CeilingGeometry.TryCompute(new List<Bounds> { wall }, out var ceil));

        Assert.Less(ceil.min.x, wall.min.x,
            "плита обязана выходить за наружную грань стены, иначе на стыке остаётся щель");
        Assert.Greater(ceil.max.x, wall.max.x, "с противоположной стороны — тоже");
        Assert.Less(ceil.min.z, wall.min.z, "по толщине стены запас особенно важен");
        Assert.Greater(ceil.max.z, wall.max.z);
        Assert.Greater(CeilingGeometry.OverhangBeyondWallsUnits, 0f,
            "нулевой запас вернул бы щель по периметру");
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

    [Test]
    public void Preset_Custom_ResolvesToHigh_ButApplyLeavesSettingsAlone()
    {
        var custom = PhotoQualityPresetTable.Resolve(PhotoQualityPreset.Custom);
        var high = PhotoQualityPresetTable.Resolve(PhotoQualityPresetTable.CustomFallsBackTo);
        Assert.AreEqual(high.EnabledCount, custom.EnabledCount,
            "Custom — состояние-метка, а не набор: как база он отдаёт High");
        Assert.AreEqual(PhotoQualityPreset.High, PhotoQualityPresetTable.CustomFallsBackTo);
    }

    [Test]
    public void Presets_GetHeavier_FromLowToHigh()
    {
        int low = PhotoQualityPresetTable.Resolve(PhotoQualityPreset.Low).EnabledCount;
        int medium = PhotoQualityPresetTable.Resolve(PhotoQualityPreset.Medium).EnabledCount;
        int high = PhotoQualityPresetTable.Resolve(PhotoQualityPreset.High).EnabledCount;

        Assert.Less(low, medium, "Low рассчитан на слабое железо: он обязан быть легче Medium");
        Assert.Less(medium, high, "High включает всё, включая супер-сэмплинг");
    }

    [Test]
    public void NamedPresets_AreEveryPresetExceptCustom()
    {
        CollectionAssert.DoesNotContain(PhotoQualityPresetTable.NamedPresets, PhotoQualityPreset.Custom,
            "Custom не именованный пресет: попав в список, Detect всегда возвращал бы его");
        Assert.AreEqual(3, PhotoQualityPresetTable.NamedPresets.Length);
        foreach (var p in PhotoQualityPresetTable.NamedPresets)
            Assert.AreEqual(p, PhotoQualityPresetTable.Next(PhotoQualityPresetTable.Next(
                PhotoQualityPresetTable.Next(p))), "обход по кругу возвращает в ту же точку");
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
        gs.PhotoSSGI = false;
        gs.PhotoAmbientPct = 250;
        gs.PhotoFloorBouncePct = 40;
        gs.PhotoExposurePct = -150;
        gs.PhotoContrastPct = -20;
        gs.PhotoSaturationPct = 30;
        gs.PhotoBloomPct = 80;
        gs.PhotoBloomThresholdPct = 200;
        gs.PhotoVignettePct = 5;
        gs.PhotoSunShadowStrengthPct = 60;
        gs.PhotoShadowDistanceM = 35;
        gs.PhotoLampShadows = false;

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
        Assert.IsFalse(gs.PhotoSSGI);
        Assert.AreEqual(250, gs.PhotoAmbientPct);
        Assert.AreEqual(40, gs.PhotoFloorBouncePct);
        Assert.AreEqual(-150, gs.PhotoExposurePct);
        Assert.AreEqual(-20, gs.PhotoContrastPct);
        Assert.AreEqual(30, gs.PhotoSaturationPct);
        Assert.AreEqual(80, gs.PhotoBloomPct);
        Assert.AreEqual(200, gs.PhotoBloomThresholdPct);
        Assert.AreEqual(5, gs.PhotoVignettePct);
        Assert.AreEqual(60, gs.PhotoSunShadowStrengthPct);
        Assert.AreEqual(35, gs.PhotoShadowDistanceM);
        Assert.IsFalse(gs.PhotoLampShadows);

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
    public void LightSource_MainFlux_PointsDown()
    {
        var go = new GameObject("Light");
        var ls = go.AddComponent<LightSourceElement>();
        ls.EnsureLight();

        Assert.AreEqual(LightType.Spot, ls.PointLight!.type, "главный поток — направленный прожектор");
        Assert.Less(ls.PointLight!.transform.forward.y, -0.9f, "основной свет направлен вниз");
        Assert.Greater(ls.PointLight!.spotAngle, 120f, "широкий конус — вниз и на стены");

        // Вверх — направленный вверх и заметно слабее (глухой купол отражает вниз).
        Assert.Greater(ls.UpLight!.transform.forward.y, 0.9f, "подсветка потолка направлена вверх");
        Assert.Less(ls.UpLight!.intensity, ls.PointLight!.intensity * 0.5f, "вверх совсем немного");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void LightSource_BeamAngle_DrivesBothCones()
    {
        var go = new GameObject("Light");
        var ls = go.AddComponent<LightSourceElement>();
        ls.EnsureLight();

        ls.BeamAngleDeg = 120;
        Assert.AreEqual(120f, ls.PointLight!.spotAngle, 0.5f, "нижний конус = углу пучка");
        Assert.Less(ls.UpLight!.spotAngle, ls.PointLight!.spotAngle, "верхний конус уже нижнего");

        float upNarrow = ls.UpLight!.spotAngle;
        ls.BeamAngleDeg = 160;
        Assert.Greater(ls.PointLight!.spotAngle, 120f, "шире угол → шире нижний конус");
        Assert.Greater(ls.UpLight!.spotAngle, upNarrow, "верхний конус тоже расширился");

        ls.BeamAngleDeg = 9999;
        Assert.AreEqual(LampSpec.MAX_BEAM_DEG, ls.BeamAngleDeg, "верхняя граница");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void LightSource_UpLightPct_ScalesUpFlux()
    {
        var go = new GameObject("Light");
        var ls = go.AddComponent<LightSourceElement>();
        ls.EnsureLight();

        ls.UpLightPct = 0;
        Assert.AreEqual(0f, ls.UpLight!.intensity, 1e-4f, "0% — весь свет вниз");
        ls.UpLightPct = 30;
        Assert.Greater(ls.UpLight!.intensity, 0f, "больше % — заметнее подсветка вверх");
        ls.UpLightPct = 999;
        Assert.AreEqual(LampSpec.MAX_UP_PCT, ls.UpLightPct, "верхняя граница");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void LightSource_Softness_DrivesInnerCone()
    {
        var go = new GameObject("Light");
        var ls = go.AddComponent<LightSourceElement>();
        ls.EnsureLight();

        ls.BeamAngleDeg = 100;
        ls.SoftnessPct = 0;
        Assert.AreEqual(100f, ls.PointLight!.innerSpotAngle, 0.5f, "0 % — резкий край: внутренний конус = внешнему");

        ls.SoftnessPct = 100;
        Assert.AreEqual(0f, ls.PointLight!.innerSpotAngle, 0.5f, "100 % — свет гаснет от самого центра");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void LightSource_Sphere_LightsAllDirections()
    {
        var go = new GameObject("Light");
        var ls = go.AddComponent<LightSourceElement>();
        ls.EnsureLight();

        ls.Shape = LampShape.Sphere;
        Assert.AreEqual(LightType.Point, ls.PointLight!.type, "шар светит во все стороны");
        Assert.AreEqual(0f, ls.UpLight!.intensity, 1e-4f, "отдельная подсветка потолка шару не нужна");
        Assert.IsFalse(ls.UpLight!.enabled);

        ls.Shape = LampShape.Plafond;
        Assert.AreEqual(LightType.Spot, ls.PointLight!.type);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void LightSource_Shadow_ModeAndStrength()
    {
        var gs = KitchenSettings.Instance;
        var before = gs.ToData();
        gs.PhotoLampShadows = true;

        var go = new GameObject("Light");
        var ls = go.AddComponent<LightSourceElement>();
        ls.EnsureLight();

        Assert.AreEqual(LightShadows.None, ls.PointLight!.shadows, "по умолчанию тени от ламп выключены");

        ls.Shadow = LampShadow.Soft;
        ls.ShadowStrengthPct = 40;
        Assert.AreEqual(LightShadows.Soft, ls.PointLight!.shadows);
        Assert.AreEqual(0.4f, ls.PointLight!.shadowStrength, 1e-3f);

        // Глобальный запрет сильнее режима самой лампы.
        gs.PhotoLampShadows = false;
        ls.ApplyLightParams();
        Assert.AreEqual(LightShadows.None, ls.PointLight!.shadows);

        Object.DestroyImmediate(go);
        gs.ApplyFrom(before);
    }

    [Test]
    public void LightSource_RangeBounds_DriveDiffusionScale()
    {
        var go = new GameObject("Light");
        var ls = go.AddComponent<LightSourceElement>();
        ls.EnsureLight();

        ls.RangeMinMM = 1000;
        ls.RangeMaxMM = 5000;
        ls.DiffusionPct = 0;
        Assert.AreEqual(1f, ls.PointLight!.range, 1e-3f, "0 % рассеивания = нижняя граница");
        ls.DiffusionPct = 100;
        Assert.AreEqual(5f, ls.PointLight!.range, 1e-3f, "100 % = верхняя граница");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void LightSource_Efficacy_And_Calibration_ScaleIntensity()
    {
        var go = new GameObject("Light");
        var ls = go.AddComponent<LightSourceElement>();
        ls.EnsureLight();

        ls.PowerW = 9;
        ls.EfficacyLmPerW = 110;
        ls.LumensPerUnit = 700;
        Assert.AreEqual(9f * 110f / 700f, ls.PointLight!.intensity, 1e-3f, "прежняя калибровка = прежняя яркость");

        ls.EfficacyLmPerW = 220;
        Assert.AreEqual(9f * 220f / 700f, ls.PointLight!.intensity, 1e-3f);
        ls.LumensPerUnit = 1400;
        Assert.AreEqual(9f * 220f / 1400f, ls.PointLight!.intensity, 1e-3f, "больше лм на единицу — тусклее");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void LightSource_Drop_LowersSourceUnderPlafond()
    {
        var go = new GameObject("Light");
        var ls = go.AddComponent<LightSourceElement>();
        ls.EnsureLight();

        ls.DropMM = 300;
        Assert.AreEqual(-0.3f, ls.PointLight!.transform.localPosition.y, 1e-3f);
        ls.DropMM = 0;
        Assert.AreEqual(0f, ls.PointLight!.transform.localPosition.y, 1e-3f);

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
        Assert.AreEqual(LampSpec.MAX_TEMPERATURE_K, ls.TemperatureK);
        ls.TemperatureK = 0;
        Assert.AreEqual(LampSpec.MIN_TEMPERATURE_K, ls.TemperatureK);
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
        // Свет: старый проект должен открыться с прежней (зашитой) картинкой.
        Assert.AreEqual(KitchenSettings.PHOTO_AMBIENT_DEFAULT_PCT, legacy.photoAmbientPct);
        Assert.AreEqual(KitchenSettings.PHOTO_BLOOM_DEFAULT_PCT, legacy.photoBloomPct);
        Assert.AreEqual(KitchenSettings.PHOTO_SHADOW_DISTANCE_DEFAULT_M, legacy.photoShadowDistanceM);
        Assert.IsTrue(legacy.photoLampShadows);
    }

    // ── Пресет качества = ещё и цена кадра ───────────────────────────────────

    /// <summary>Сеттер PhotoQuality когда-то только запоминал число, а тумблеры
    /// оставлял как были. Панель звала Apply и потому работала, а set_setting
    /// photo_quality двигал одну подпись — три прогона замера намерили одни и те
    /// же настройки и дали 12,6/11,4/11,5 мс, где низкий вышел дороже высокого.
    /// Ловушку убрал сам сеттер; тест держит её закрытой.</summary>
    [Test]
    public void SettingPhotoQuality_AppliesThePresetAndNotJustTheLabel()
    {
        var gs = KitchenSettings.Instance;
        var before = gs.ToData();
        try
        {
            gs.PhotoQuality = PhotoQualityPreset.High;
            Assume.That(gs.PhotoSupersampling, Is.True, "высокий пресет включает суперсэмплинг");

            gs.PhotoQuality = PhotoQualityPreset.Low;

            Assert.IsFalse(gs.PhotoSupersampling, "низкий пресет обязан выключить суперсэмплинг");
            Assert.IsFalse(gs.PhotoAmbientOcclusion, "низкий пресет обязан выключить AO");
            Assert.IsFalse(gs.PhotoBloom, "низкий пресет обязан выключить свечение");
            Assert.AreEqual(PhotoQualityPresetTable.LOW_SHADOWMAP_PX, gs.PhotoShadowMapPx,
                "низкий пресет обязан уронить карту теней");
        }
        finally
        {
            gs.ApplyFrom(before);
        }
    }

    /// <summary>Пресеты обязаны отличаться не только красотой, но и ценой, иначе
    /// выбирать между ними незачем. Замер на трёх ракурсах дал 5,0 / 6,8 / 13,0 мс
    /// на кадр — разгоняют его ровно эти две числовые оси плюс полноразмерный AO.
    /// Пока лестница строго возрастает, «низкое» не может однажды оказаться
    /// дороже «высокого», как оно уже было.</summary>
    [Test]
    public void Presets_ClimbInCostAndNotOnlyInLooks()
    {
        var low = PhotoQualityPresetTable.Resolve(PhotoQualityPreset.Low);
        var medium = PhotoQualityPresetTable.Resolve(PhotoQualityPreset.Medium);
        var high = PhotoQualityPresetTable.Resolve(PhotoQualityPreset.High);

        Assert.Less(low.ShadowMapPx, medium.ShadowMapPx, "карта теней: низкое < среднее");
        Assert.Less(medium.ShadowMapPx, high.ShadowMapPx, "карта теней: среднее < высокое");
        Assert.LessOrEqual(low.RenderScalePct, medium.RenderScalePct, "масштаб рендера не падает");
        Assert.Less(medium.RenderScalePct, high.RenderScalePct, "суперсэмплинг только у высокого");

        Assert.IsFalse(low.AoFullRes, "у низкого AO вообще выключен");
        Assert.IsFalse(medium.AoFullRes, "среднее держит AO в половинном разрешении");
        Assert.IsTrue(high.AoFullRes, "полноразмерный AO — привилегия высокого");

        Assert.Less(low.EnabledCount, medium.EnabledCount, "включённых эффектов: низкое < среднее");
        Assert.Less(medium.EnabledCount, high.EnabledCount, "включённых эффектов: среднее < высокое");
    }
}

public class PhotoModeLifecycleTests
{
    [SetUp]
    public void SetUp() => EditModeManager.SetMode(EditMode.Normal);

    [TearDown]
    public void TearDown() => EditModeManager.SetMode(EditMode.Normal);

    [Test]
    public void Active_IsDerivedFromTheEditorMode_NotFromASecondFlag()
    {
        PhotoMode.SetActive(true);
        Assert.IsTrue(PhotoMode.Active);
        Assert.AreEqual(EditMode.Photo, EditModeManager.Mode);

        EditModeManager.Reset();

        Assert.IsFalse(PhotoMode.Active,
            "отдельного флага нет намеренно: пока их было два, Reset в обход SetActive оставлял фоторежим включённым при Mode = Normal");
        Assert.AreEqual(EditMode.Normal, EditModeManager.Mode);
    }

    [Test]
    public void Changed_FiresOnEnterAndOnExit_ButNotOnANoOp()
    {
        int fired = 0;
        System.Action handler = () => fired++;
        PhotoMode.Changed += handler;
        try
        {
            PhotoMode.SetActive(true);
            Assert.AreEqual(1, fired, "вход в режим — смена состояния");

            PhotoMode.SetActive(false);
            Assert.AreEqual(2, fired, "выход тоже: иначе подписчики UI остаются в фото-состоянии");

            PhotoMode.SetActive(false);
            Assert.AreEqual(2, fired, "повторный выход ничего не меняет и сигналить не должен");
        }
        finally
        {
            PhotoMode.Changed -= handler;
        }
    }

    [Test]
    public void Exit_RollsBackEveryHeavyEffectItTurnedOn()
    {
        var s = KitchenSettings.Instance;
        var before = s.ToData();
        bool tintBefore = ElementHighlighter.ViolationTintVisible;
        try
        {
            s.PhotoCeiling = true;
            ElementHighlighter.ViolationTintVisible = true;

            PhotoMode.SetActive(true);
            Assert.IsTrue(PhotoQualityController.IsApplied, "вход поднимает качество картинки");
            Assert.IsFalse(ElementHighlighter.ViolationTintVisible, "валидационный тон в кадре не нужен");

            PhotoMode.SetActive(false);

            Assert.IsFalse(PhotoQualityController.IsApplied,
                "рабочий режим лёгкий: всё тяжёлое обязано откатиться полностью, а не остаться висеть");
            Assert.IsFalse(CeilingBuilder.Exists, "временный потолок уходит вместе с режимом");
            Assert.IsTrue(ElementHighlighter.ViolationTintVisible, "выход из фоторежима возвращает тон валидности");
        }
        finally
        {
            EditModeManager.SetMode(EditMode.Normal);
            ElementHighlighter.ViolationTintVisible = tintBefore;
            s.ApplyFrom(before);
        }
    }

    [Test]
    public void RefreshIfActive_IsSilentWhilePhotoModeIsOff()
    {
        Assume.That(PhotoMode.Active, Is.False, "тест начинается в обычном режиме, иначе он ничего не проверяет");

        PhotoMode.RefreshIfActive();

        Assert.IsFalse(PhotoQualityController.IsApplied,
            "смена пресета вне фоторежима не имеет права включить тяжёлые эффекты сама");
        Assert.IsFalse(CeilingBuilder.Exists);
    }


    /// <summary>Инструменты, рисующие свои линии поверх сцены, не знали про
    /// фоторежим и не сбрасывались при входе: рулетка и пипетка продолжали
    /// рисоваться в кадре. Включать их СРАЗУ ОБА нельзя — они взаимно
    /// исключающие, и второй гасит первый: попытка сделать это молча превращала
    /// проверку в Inconclusive. Поэтому каждый инструмент проверяется отдельным
    /// прогоном, и тест краснеет, если Enter перестанет его закрывать.</summary>
    [TestCase("measure")]
    [TestCase("eyedropper")]
    public void EnteringPhotoMode_ClosesTheToolThatDrawsOverlays(string tool)
    {
        var s = KitchenSettings.Instance;
        var before = s.ToData();
        try
        {
            if (tool == "measure") KitchenDesigner.Core.Measure.MeasureMode.SetActive(true);
            else KitchenDesigner.Core.Tools.EyedropperMode.SetActive(true);

            bool on = tool == "measure"
                ? KitchenDesigner.Core.Measure.MeasureMode.Active
                : KitchenDesigner.Core.Tools.EyedropperMode.Active;
            Assume.That(on, Is.True, tool + ": инструмент включён до входа, иначе тест ничего не ловит");

            EditModeManager.SetMode(EditMode.Photo);

            Assert.IsFalse(KitchenDesigner.Core.Measure.MeasureMode.Active,
                "рулетка обязана закрыться при входе в фоторежим");
            Assert.IsFalse(KitchenDesigner.Core.Tools.EyedropperMode.Active,
                "пипетка обязана закрыться при входе в фоторежим");
            Assert.IsFalse(KitchenDesigner.Core.Lighting.LightPickMode.Active,
                "выбор ламп обязан закрыться при входе в фоторежим");
        }
        finally
        {
            EditModeManager.SetMode(EditMode.Normal);
            KitchenDesigner.Core.Measure.MeasureMode.SetActive(false);
            KitchenDesigner.Core.Tools.EyedropperMode.SetActive(false);
            s.ApplyFrom(before);
        }
    }
}
