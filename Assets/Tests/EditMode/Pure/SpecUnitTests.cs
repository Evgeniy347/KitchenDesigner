using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>Единица измерения строки спецификации — данные, не подразумеваемая колонка
/// (карта §3.3: «S, м²» в заголовке сегодня подразумевает единицу, она обязана стать данными).
/// Каждая единица обязана дать свою метку, иначе окно и CSV покажут "?" молча.</summary>
public class SpecUnitTests
{
    [Test]
    public void Label_Pieces_IsSht()
    {
        Assert.AreEqual("шт", SpecUnit.Pieces.Label());
    }

    [Test]
    public void Label_LinearMeters_IsM()
    {
        Assert.AreEqual("м", SpecUnit.LinearMeters.Label());
    }

    [Test]
    public void Label_AreaM2_IsM2()
    {
        Assert.AreEqual("м²", SpecUnit.AreaM2.Label());
    }

    [Test]
    public void Label_VolumeM3_IsM3()
    {
        Assert.AreEqual("м³", SpecUnit.VolumeM3.Label());
    }

    [Test]
    public void Label_Kilograms_IsKg()
    {
        Assert.AreEqual("кг", SpecUnit.Kilograms.Label());
    }

    [Test]
    public void Label_AllFiveUnits_AreDistinct()
    {
        var labels = new[]
        {
            SpecUnit.Pieces.Label(), SpecUnit.LinearMeters.Label(), SpecUnit.AreaM2.Label(),
            SpecUnit.VolumeM3.Label(), SpecUnit.Kilograms.Label(),
        };
        Assert.AreEqual(5, new System.Collections.Generic.HashSet<string>(labels).Count,
            "смешать м³ с погонными метрами в отчёте нельзя — метки обязаны отличаться");
    }
}
