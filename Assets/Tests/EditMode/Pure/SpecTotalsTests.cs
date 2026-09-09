using System.Collections.Generic;
using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>Итог по спецификации складывается ПО ЕДИНИЦЕ отдельно — м³ не складывается
/// с погонными метрами (карта §3.6). Проверяем это на смешанном входе, а не только на
/// однородном, иначе тест не может упасть на реальной ошибке сложения "всё в одну кучу".</summary>
public class SpecTotalsTests
{
    [Test]
    public void ByUnit_MixedUnits_KeepsSeparateBuckets()
    {
        var rows = new (SpecUnit, float)[]
        {
            (SpecUnit.AreaM2, 0.32f),
            (SpecUnit.AreaM2, 0.32f),
            (SpecUnit.VolumeM3, 1.2f),
            (SpecUnit.LinearMeters, 3f),
        };

        var totals = SpecTotals.ByUnit(rows);

        Assert.AreEqual(0.64f, totals[SpecUnit.AreaM2], 0.0001f);
        Assert.AreEqual(1.2f, totals[SpecUnit.VolumeM3], 0.0001f);
        Assert.AreEqual(3f, totals[SpecUnit.LinearMeters], 0.0001f);
        Assert.IsFalse(totals.ContainsKey(SpecUnit.Kilograms), "единица без строк не появляется в итогах");
    }

    [Test]
    public void ByUnit_VolumeNeverLeaksIntoArea()
    {
        var rows = new (SpecUnit, float)[]
        {
            (SpecUnit.AreaM2, 10f),
            (SpecUnit.VolumeM3, 999f),
        };

        var totals = SpecTotals.ByUnit(rows);

        Assert.AreEqual(10f, totals[SpecUnit.AreaM2], 0.0001f,
            "объём в кубах не должен попасть в сумму площади только потому, что он большой");
    }

    [Test]
    public void ByUnit_EmptyInput_EmptyResult()
    {
        var totals = SpecTotals.ByUnit(new List<(SpecUnit, float)>());
        Assert.AreEqual(0, totals.Count);
    }
}
