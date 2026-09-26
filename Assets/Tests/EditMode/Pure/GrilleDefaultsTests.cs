using NUnit.Framework;
using KitchenDesigner.Core.Ventilation;

/// <summary>docs/NORMATIVE-DEFAULTS.md §6 "Размеры решёток": ряд типоразмеров — практика
/// (каталоги производителей), не цитата ГОСТ/ТУ, поэтому без [Category("NormativeUnverified")]
/// - как FenceDefaults.PostStepMm.</summary>
public class GrilleDefaultsTests
{
    [Test]
    public void Sizes_Are_150x150_150x200_180x250_200x200_200x300_250x250_340x340_440x440()
    {
        CollectionAssert.AreEqual(new[]
        {
            (150, 150),
            (150, 200),
            (180, 250),
            (200, 200),
            (200, 300),
            (250, 250),
            (340, 340),
            (440, 440),
        }, GrilleDefaults.Sizes);
    }

    [Test]
    public void DefaultSize_IsOneOfTheListedSizes()
    {
        CollectionAssert.Contains(GrilleDefaults.Sizes,
            (GrilleDefaults.DefaultWidthMm, GrilleDefaults.DefaultHeightMm));
    }

    [Test]
    public void AirflowRange_DefaultLiesInsideMinMax()
    {
        Assert.GreaterOrEqual(GrilleDefaults.DefaultAirflowM3PerHour, GrilleDefaults.MinAirflowM3PerHour);
        Assert.LessOrEqual(GrilleDefaults.DefaultAirflowM3PerHour, GrilleDefaults.MaxAirflowM3PerHour);
    }
}
