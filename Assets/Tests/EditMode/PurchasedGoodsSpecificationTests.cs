using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Дефект: ни одной строки в ведомости не было у мойки, варочной, духовки,
/// посудомойки, винтовой опоры, розетки, выключателя, светильника, смесителя и
/// ванны — покупные изделия не умели считать себя (ни IQuantifies, ни
/// ISpecificationParts, ни IsFlatBoardElement), и SpecificationManager проходил
/// мимо них без единого предупреждения. Каждый тест здесь — прямая проверка того,
/// что конкретное изделие теперь даёт РОВНО ОДНУ строку в штуках.</summary>
public class PurchasedGoodsSpecificationTests
{
    [TearDown]
    public void TearDown()
    {
        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.DestroyImmediate(e.gameObject);
        PartRegistry.Clear();
    }

    private static void AssertSinglePurchasedPiece(KitchenElement element, string expectedName)
    {
        var result = SpecificationManager.Build(new[] { element });

        Assert.AreEqual(1, result.lines.Count,
            $"{element.GetType().Name} обязан дать ровно одну строку в ведомости");
        var line = result.lines[0];
        Assert.AreEqual(SpecUnit.Pieces, line.unit, "покупное изделие считается штуками");
        Assert.AreEqual(PurchasedGoodsSpecItems.Section, line.section);
        Assert.AreEqual(expectedName, line.name);
        Assert.AreEqual(1, line.count);
    }

    [Test]
    public void Sink_IsCountedAsOnePurchasedPiece()
    {
        var go = ElementFactory.CreateSink("Sink", Vector3.zero);
        AssertSinglePurchasedPiece(go.GetComponent<SinkElement>(), "Мойка");
    }

    [Test]
    public void Cooktop_IsCountedAsOnePurchasedPiece()
    {
        var go = ElementFactory.CreateCooktop("Cooktop", Vector3.zero);
        AssertSinglePurchasedPiece(go.GetComponent<CooktopElement>(), "Варочная");
    }

    [Test]
    public void Oven_IsCountedAsOnePurchasedPiece()
    {
        var go = ElementFactory.CreateOven("Oven", Vector3.zero);
        AssertSinglePurchasedPiece(go.GetComponent<OvenElement>(), OvenElement.MODEL);
    }

    [Test]
    public void Dishwasher_IsCountedAsOnePurchasedPiece()
    {
        var go = ElementFactory.CreateDishwasher("Dishwasher", Vector3.zero);
        AssertSinglePurchasedPiece(go.GetComponent<DishwasherElement>(), DishwasherElement.MODEL);
    }

    [Test]
    public void ScrewLeg_IsCountedAsOnePurchasedPiece()
    {
        var go = ElementFactory.CreateScrewLeg("Leg", Vector3.zero);
        AssertSinglePurchasedPiece(go.GetComponent<ScrewLegElement>(), "Винтовая опора");
    }

    [Test]
    public void Socket_IsCountedAsOnePurchasedPiece()
    {
        var go = ElementFactory.CreateSocket(WallDeviceSpec.Clamped(90, 90, 15, 1), "Socket", Vector3.zero);
        AssertSinglePurchasedPiece(go.GetComponent<SocketElement>(), "Розетка");
    }

    [Test]
    public void LightSwitch_IsCountedAsOnePurchasedPiece()
    {
        var go = ElementFactory.CreateLightSwitch(WallDeviceSpec.Clamped(90, 90, 15, 1), false, null,
            "Switch", Vector3.zero);
        AssertSinglePurchasedPiece(go.GetComponent<LightSwitchElement>(), "Выключатель");
    }

    [Test]
    public void LightSource_IsCountedAsOnePurchasedPiece()
    {
        var go = ElementFactory.CreateLightSource("Lamp", Vector3.zero);
        AssertSinglePurchasedPiece(go.GetComponent<LightSourceElement>(), "Источник света");
    }

    [Test]
    public void BathMixer_IsCountedAsOnePurchasedPiece()
    {
        var go = ElementFactory.CreateBathMixer(
            BathMixerSpec.Clamped(150, 250, 40, 30, 120, 15), "Mixer", Vector3.zero);
        AssertSinglePurchasedPiece(go.GetComponent<BathMixerElement>(), "Смеситель для ванны");
    }

    [Test]
    public void Bathtub_IsCountedAsOnePurchasedPiece()
    {
        var go = ElementFactory.CreateBathtub(new Vector3Int(1700, 600, 700), 40, 450, 120, 70,
            "Tub", Vector3.zero);
        AssertSinglePurchasedPiece(go.GetComponent<BathtubElement>(), "Ванна");
    }

    /// <summary>Девять светильников одной кухни — девять отдельных счётов, суммирующихся
    /// в totalsByUnit по штукам, а не одна строка "х9" молчаливо потерявшая тираж.</summary>
    [Test]
    public void NineLightSources_SumToNinePiecesInTotalsByUnit()
    {
        var lamps = Enumerable.Range(0, 9)
            .Select(i => ElementFactory.CreateLightSource("Lamp" + i, Vector3.zero)
                .GetComponent<LightSourceElement>())
            .Cast<KitchenElement>()
            .ToArray();

        var result = SpecificationManager.Build(lamps);

        Assert.AreEqual(1, result.lines.Count, "все девять светильников — один тип, одна строка");
        Assert.AreEqual(9, result.lines[0].count);
        Assert.AreEqual(9f, result.totalsByUnit[SpecUnit.Pieces], 0.0001f);
    }

    /// <summary>Противоположный вход: разные покупные изделия не сворачиваются в одну
    /// строку только потому, что обе считаются штуками.</summary>
    [Test]
    public void DifferentPurchasedGoods_DoNotMergeIntoOneLine()
    {
        var sink = ElementFactory.CreateSink("Sink", Vector3.zero).GetComponent<SinkElement>();
        var oven = ElementFactory.CreateOven("Oven", Vector3.zero).GetComponent<OvenElement>();

        var result = SpecificationManager.Build(new KitchenElement[] { sink, oven });

        Assert.AreEqual(2, result.lines.Count,
            "мойка и духовка — разные изделия, обязаны остаться разными строками");
    }
}
