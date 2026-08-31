using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Светотехника лампы: как параметры «мощность / температура / рассеивание /
/// форма» превращаются в два дочерних источника. Раньше эти рассуждения стояли
/// комментариями над константами LightSourceElement.
/// </summary>
public class LightSourceLampTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        LightSourceElement.SetGlobalOn(true);
        foreach (var go in _spawned)
        {
            if (go == null) continue;
            var el = go.GetComponent<KitchenElement>();
            if (el != null) PartRegistry.Unregister(el);
            Object.DestroyImmediate(go);
        }
        _spawned.Clear();
    }

    private LightSourceElement Lamp()
    {
        var go = new GameObject("Lamp");
        _spawned.Add(go);
        var lamp = go.AddComponent<LightSourceElement>();
        lamp.PartName = "Lamp";
        lamp.DimensionsMM = new Vector3Int(
            LampSpec.DEFAULT_SIZE_MM,
            LampSpec.DEFAULT_SIZE_MM,
            LampSpec.DEFAULT_SIZE_MM);
        PartRegistry.Register(lamp);
        lamp.EnsureLight();
        return lamp;
    }

    private static Light Down(LightSourceElement lamp) => Find(lamp, "SpotDown");
    private static Light Up(LightSourceElement lamp) => Find(lamp, "SpotUp");

    private static Light Find(LightSourceElement lamp, string name)
    {
        var child = lamp.transform.Find(name);
        Assert.IsNotNull(child, "у лампы должен быть дочерний источник " + name);
        var light = child!.GetComponent<Light>();
        Assert.IsNotNull(light);
        return light!;
    }

    /// <summary>Калибровка «люмены → интенсивность Unity»: делитель подобран так,
    /// чтобы паспортные 9 Вт при 110 лм/Вт давали ровно прежнюю 1.4.</summary>
    [Test]
    public void DefaultLamp_LandsOnTheHistoricalIntensityOfOnePointFour()
    {
        var lamp = Lamp();
        Assert.AreEqual(1.4f, Down(lamp).intensity, 0.02f,
            "9 Вт × 110 лм/Вт ÷ 700 = 1.414: сдвинешь любой из трёх — вся сцена поменяет яркость");
    }

    [Test]
    public void Power_DrivesTheIntensityLinearly()
    {
        var lamp = Lamp();
        float single = Down(lamp).intensity;
        lamp.PowerW = LampSpec.DEFAULT_POWER_W * 2;
        Assert.AreEqual(single * 2f, Down(lamp).intensity, 0.02f,
            "яркость — это мощность × светоотдача, а не отдельная ручка");
    }

    /// <summary>Вплотную к потолку свет по 1/r² даёт пересвет: источник опускают
    /// от центра плафона, иначе на потолке остаётся «выжженное» пятно.</summary>
    [Test]
    public void Emitter_HangsBelowThePlafondCentre_ByTheDropDistance()
    {
        var lamp = Lamp();
        float expected = -LampSpec.DEFAULT_DROP_MM * AppConstants.MM_TO_UNITS;
        float scaleY = lamp.transform.lossyScale.y;

        Assert.AreEqual(expected, Down(lamp).transform.localPosition.y * scaleY, 1e-4f,
            "источник опущен от центра плафона — вплотную к потолку 1/r² выжигает пятно");
        Assert.AreEqual(expected, Up(lamp).transform.localPosition.y * scaleY, 1e-4f,
            "верхняя подсветка висит там же");
    }

    /// <summary>Отступ задаётся в МИРЕ: масштаб корня его не растягивает.</summary>
    [Test]
    public void EmitterDrop_IsCompensatedForTheRootScale()
    {
        var lamp = Lamp();
        lamp.transform.localScale = new Vector3(1f, 4f, 1f);
        lamp.ApplyLightParams();

        float worldDrop = Down(lamp).transform.localPosition.y * lamp.transform.lossyScale.y;
        Assert.AreEqual(-LampSpec.DEFAULT_DROP_MM * AppConstants.MM_TO_UNITS, worldDrop, 1e-4f,
            "растянули плафон — отступ источника в мире обязан остаться прежним");
    }

    /// <summary>Глухой купол свет не теряет, а отражает вниз: весь поток идёт в
    /// нижний прожектор, вверх уходит лишь утечка в UpLightPercent.</summary>
    [Test]
    public void UpLight_IsOnlyALeakFraction_OfTheDownwardFlux()
    {
        var lamp = Lamp();
        float expected = Down(lamp).intensity * (LampSpec.DEFAULT_UP_PCT / 100f);
        Assert.AreEqual(expected, Up(lamp).intensity, 1e-3f,
            "вверх уходит лишь утечка сквозь купол — основной поток отражается вниз");

        lamp.UpLightPct = 0;
        Assert.AreEqual(0f, Up(lamp).intensity, 1e-4f,
            "0 % — полностью отражающий купол, весь свет вниз");
    }

    [Test]
    public void UpperCone_IsNarrowerThanTheLowerOne()
    {
        var lamp = Lamp();
        Assert.Less(Up(lamp).spotAngle, Down(lamp).spotAngle,
            "верхняя подсветка берёт пропорциональную, более узкую долю угла пучка");
    }

    /// <summary>Мягкость края — это отношение внутреннего конуса к внешнему:
    /// 0 % даёт резкую границу пятна, 100 % — плавный градиент.</summary>
    [Test]
    public void EdgeSoftness_ShrinksTheInnerCone_TowardsZero()
    {
        var lamp = Lamp();

        lamp.SoftnessPct = 0;
        Assert.AreEqual(Down(lamp).spotAngle, Down(lamp).innerSpotAngle, 0.01f,
            "0 % — внутренний конус равен внешнему, граница пятна резкая");

        lamp.SoftnessPct = 100;
        Assert.AreEqual(0f, Down(lamp).innerSpotAngle, 0.01f,
            "100 % — свет плавно гаснет от центра к краю");
    }

    /// <summary>«Шар» светит во все стороны сам — отдельная подсветка потолка ему
    /// не нужна и должна быть погашена.</summary>
    [Test]
    public void Sphere_BecomesAPointLight_AndDropsTheCeilingFill()
    {
        var lamp = Lamp();
        Assert.AreEqual(LightType.Spot, Down(lamp).type, "предусловие: плафон — прожектор");

        lamp.Shape = LampShape.Sphere;

        Assert.AreEqual(LightType.Point, Down(lamp).type, "шар — точечный источник");
        Assert.AreEqual(0f, Up(lamp).intensity, 1e-4f);
        Assert.IsFalse(Up(lamp).enabled, "второй источник шару не нужен вовсе");
    }

    /// <summary>Тени точечных источников дороги — по умолчанию их нет.</summary>
    [Test]
    public void Shadows_AreOffByDefault_BecauseTheyAreExpensive()
    {
        var lamp = Lamp();
        Assert.AreEqual(LampShadow.None, lamp.Shadow);
        Assert.AreEqual(LightShadows.None, Down(lamp).shadows,
            "тени точечных источников дороги: свет заворачивается через ambient и SSGI");
    }

    /// <summary>Глобальный выключатель ищет лампы ПО СЦЕНЕ, а не по своему
    /// списку: EditMode не гоняет OnEnable/OnDisable, и на списке этот тест
    /// не прошёл бы.</summary>
    [Test]
    public void GlobalSwitch_ReachesALampThatNeverGotOnEnable()
    {
        var lamp = Lamp();
        Assert.IsTrue(Down(lamp).enabled, "предусловие: свет включён");

        LightSourceElement.SetGlobalOn(false);
        Assert.IsFalse(Down(lamp).enabled);
        Assert.IsFalse(Up(lamp).enabled);

        LightSourceElement.SetGlobalOn(true);
        Assert.IsTrue(Down(lamp).enabled);
    }

    /// <summary>Плафон светится собственным материалом лампы — он обязан пережить
    /// любую пересборку параметров, иначе после загрузки проекта лампа гаснет
    /// визуально.</summary>
    [Test]
    public void Plafond_KeepsItsOwnEmissiveMaterial_AcrossParameterChanges()
    {
        var lamp = Lamp();
        var go = lamp.gameObject;
        go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        lamp.ApplyLightParams();

        var first = mr.sharedMaterial;
        Assert.IsNotNull(first, "плафон получает собственный материал");

        lamp.PowerW = 24;
        Assert.AreSame(first, mr.sharedMaterial, "материал переиспользуется, а не создаётся заново");
        Assert.Greater(mr.sharedMaterial.GetColor("_EmissionColor").maxColorComponent,
            0f, "свечение растёт с мощностью");
    }
}
