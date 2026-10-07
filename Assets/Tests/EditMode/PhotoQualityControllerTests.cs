using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class PhotoQualityControllerTests
{
    private KitchenSettingsData? _before;

    [SetUp]
    public void SetUp() => _before = KitchenSettings.Instance.ToData();

    private readonly List<GameObject> _spawned = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        if (_before != null) KitchenSettings.Instance.ApplyFrom(_before);
    }

    private GroundQuad MakeGround()
    {
        var ground = GroundQuad.Create();
        _spawned.Add(ground.gameObject);
        return ground;
    }

    private FloorElement MakeFloorOfColour(Color colour)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _spawned.Add(go);
        var floor = go.AddComponent<FloorElement>();
        floor.PartName = "Пол";
        floor.DimensionsMM = new Vector3Int(2000, 100, 2000);
        var renderer = go.GetComponent<MeshRenderer>();
        var material = new Material(renderer.sharedMaterial);
        material.color = colour;
        renderer.sharedMaterial = material;
        return floor;
    }

    [Test]
    public void Ambient_ScalesWithTheAmbientPercent_AndZeroMeansAFullyDarkRoom()
    {
        var s = KitchenSettings.Instance;
        s.PhotoFloorBouncePct = 100;

        s.PhotoAmbientPct = 100;
        var full = PhotoQualityController.AmbientFor(s);
        s.PhotoAmbientPct = 200;
        var doubled = PhotoQualityController.AmbientFor(s);
        s.PhotoAmbientPct = 0;
        var dark = PhotoQualityController.AmbientFor(s);

        Assert.AreEqual(full.Sky.r * 2f, doubled.Sky.r, 1e-4f,
            "настройка «Окружающий свет» — множитель: 100 % даёт прежнюю зашитую картинку, 200 % вдвое мягче");
        Assert.AreEqual(0f, dark.Sky.maxColorComponent, 1e-4f,
            "0 % — глухая тень: подсветка обязана уйти в ноль, а не в свой минимум");
        Assert.AreEqual(0f, dark.Ground.maxColorComponent, 1e-4f);
    }

    [Test]
    public void Ambient_GroundIsAWarmFloorBounce_AndSkyIsCold()
    {
        Assert.Greater(PhotoQualityController.NeutralWarmBounce.r,
            PhotoQualityController.NeutralWarmBounce.b,
            "отскок от пола тёплый: грани, смотрящие ВНИЗ, иначе проваливаются в чёрное под полкой");
        Assert.Greater(PhotoQualityController.AmbientSkyBase.b,
            PhotoQualityController.AmbientSkyBase.r,
            "верхние грани берут слабый холодный «sky» — тёплый сверху читается как второе солнце");
        Assert.Less(PhotoQualityController.AmbientSkyBase.maxColorComponent, 0.5f,
            "уровень низкий: «темно значит темно» должно сохраняться, лечится только чёрный провал");
    }

    [Test]
    public void Ambient_FloorBounceZero_LeavesTheGroundBlack()
    {
        var s = KitchenSettings.Instance;
        s.PhotoAmbientPct = 100;
        s.PhotoFloorBouncePct = 0;

        Assert.AreEqual(0f, PhotoQualityController.AmbientFor(s).Ground.maxColorComponent, 1e-4f,
            "отдельная настройка отскока обязана гасить именно нижний цвет, не трогая небо");
        Assert.Greater(PhotoQualityController.AmbientFor(s).Sky.maxColorComponent, 0f);
    }

    [Test]
    public void FloorBounce_OfABrightFloor_IsCappedSoItDoesNotFloodTheScene()
    {
        var bright = PhotoQualityController.DimmedBounceOf(Color.white);

        Assert.AreEqual(PhotoQualityController.MaxBounce.r, bright.r, 1e-4f,
            "белый пол не имеет права залить сцену: отскок — часть альбедо и упирается в потолок");
        Assert.AreEqual(PhotoQualityController.MaxBounce.g, bright.g, 1e-4f);
        Assert.AreEqual(PhotoQualityController.MaxBounce.b, bright.b, 1e-4f);

        var dim = PhotoQualityController.DimmedBounceOf(new Color(0.2f, 0.2f, 0.2f));
        Assert.Less(dim.r, 0.2f, "тёмный пол отскакивает лишь частью своего альбедо");
        Assert.Greater(dim.r, 0f);
    }

    [Test]
    public void SampleFloorBounce_TakesTheColourOfTheUserFloor_NotOfTheGround()
    {
        var ground = MakeGround();
        var floorColour = new Color(0.2f, 0.4f, 0.8f);
        MakeFloorOfColour(floorColour);
        FloorElement.RefreshGroundVisibility();
        var fromTheGround = PhotoQualityController.DimmedBounceOf(ground.GetComponent<MeshRenderer>().sharedMaterial.color, 1f);
        var expected = PhotoQualityController.DimmedBounceOf(floorColour, 1f);
        Assert.Greater(Vector4.Distance(fromTheGround, expected), 0.05f, "посылка: цвет земли и цвет пола различимы");

        var sampled = PhotoQualityController.SampleFloorBounce(null);

        Assert.AreEqual(expected.r, sampled.r, 1e-4f, "отскок света берётся с пола, который виден, а не с серой земли под ним");
        Assert.AreEqual(expected.g, sampled.g, 1e-4f);
        Assert.AreEqual(expected.b, sampled.b, 1e-4f);
    }

    [Test]
    public void SampleFloorBounce_WithoutAUserFloor_IsNeutral_NotTheColourOfTheGround()
    {
        MakeGround();

        var sampled = PhotoQualityController.SampleFloorBounce(null);

        Assert.AreEqual(PhotoQualityController.NeutralWarmBounce.r, sampled.r, 1e-4f,
            "своего пола нет: отскок нейтральный, а земля — картинка, её цвет к освещению отношения не имеет");
        Assert.AreEqual(PhotoQualityController.NeutralWarmBounce.g, sampled.g, 1e-4f);
        Assert.AreEqual(PhotoQualityController.NeutralWarmBounce.b, sampled.b, 1e-4f);
    }

    [Test]
    public void SunShadows_FollowBothToggles()
    {
        var s = KitchenSettings.Instance;

        s.PhotoShadows = false;
        s.PhotoSoftShadows = true;
        Assert.AreEqual(LightShadows.None, PhotoQualityController.SunShadowsFor(s),
            "выключенные тени сильнее тумблера мягкости");

        s.PhotoShadows = true;
        s.PhotoSoftShadows = false;
        Assert.AreEqual(LightShadows.Hard, PhotoQualityController.SunShadowsFor(s));

        s.PhotoSoftShadows = true;
        Assert.AreEqual(LightShadows.Soft, PhotoQualityController.SunShadowsFor(s));
    }

    [Test]
    public void SetRendererFeatureActive_UnknownFeature_IsSilent()
    {
        Assert.DoesNotThrow(() => PhotoQualityController.SetRendererFeatureActive("НетТакойФичи", true),
            "рендерер без такой фичи — переключение неприменимо, а не ошибка (SSGI может быть не установлен)");
        Assert.DoesNotThrow(() => PhotoQualityController.SetRendererFeatureActive("НетТакойФичи", false));
    }
}
