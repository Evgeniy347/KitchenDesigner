using NUnit.Framework;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Plumbing;

/// <summary>Арифметика ТРУБЫ как элемента сцены: длина растёт по одной оси,
/// сечение не растёт вовсе — его целиком задаёт условный проход.
///
/// Числа взяты из ГОСТ 3262-75 и повторяют то, что уже проверяет
/// <c>PipeSpecTests</c> для таблицы. Здесь проверяется другое: как таблица
/// превращается в ГАБАРИТ и в СТРОКИ панели. Наружный диаметр 26,8 мм в
/// целочисленный <c>Vector3Int</c> не помещается, и округление обязано быть
/// названо и закреплено: молча «26» вместо «27» — это труба, которая в
/// валидации на полмиллиметра тоньше, чем в жизни.</summary>
public class PipeElementSpecTests
{
    [Test]
    public void DefaultPipe_IsThreeQuarterInch_BecauseThatIsWhatTheTaskNames()
    {
        var size = PipeSpec.Get(PipeSpec.DEFAULT_SIZE);
        Assert.AreEqual("3/4\"", size.Designation, "умолчание ряда — ДУ 20");
        Assert.AreEqual(26.8f, size.OuterDiameterMm, 0.001f);
        Assert.AreEqual(2.8f, size.WallThicknessMm, 0.001f);
        Assert.AreEqual(21.2f, size.InnerDiameterMm, 0.001f,
            "внутренний = наружный минус две стенки; сохраняют его НЕ он, а ДУ");
    }

    [Test]
    public void SectionMM_RoundsAwayFromZero_SoTheBoxIsNeverThinnerThanThePipe()
    {
        Assert.AreEqual(27, PipeElementSpec.SectionMM(PipeSpec.Dn20),
            "26,8 обязано стать 27: коробка валидации тоньше трубы — это деталь, "
            + "которая проходит сквозь трубу и молчит");
        Assert.AreEqual(21, PipeElementSpec.SectionMM(PipeSpec.Dn15));
        Assert.AreEqual(34, PipeElementSpec.SectionMM(PipeSpec.Dn25));
        Assert.AreEqual(42, PipeElementSpec.SectionMM(PipeSpec.Dn32));
        Assert.AreEqual(48, PipeElementSpec.SectionMM(PipeSpec.Dn40));
        Assert.AreEqual(60, PipeElementSpec.SectionMM(PipeSpec.Dn50));
    }

    [Test]
    public void SectionMM_OfAnUnknownSize_FallsBackToTheDefault_NotToZero()
    {
        Assert.AreEqual(PipeElementSpec.SectionMM(PipeSpec.DEFAULT_SIZE),
            PipeElementSpec.SectionMM("dn999"),
            "неизвестный id — это старый файл или опечатка агента; нулевое сечение "
            + "дало бы невидимую трубу вместо заметного отказа");
    }

    [Test]
    public void ClampLength_KeepsThePipeBetweenTenMillimetresAndSixMetres()
    {
        Assert.AreEqual(PipeElementSpec.MIN_LENGTH_MM, PipeElementSpec.ClampLengthMM(0));
        Assert.AreEqual(PipeElementSpec.MIN_LENGTH_MM, PipeElementSpec.ClampLengthMM(-500));
        Assert.AreEqual(PipeElementSpec.MAX_LENGTH_MM, PipeElementSpec.ClampLengthMM(999999));
        Assert.AreEqual(600, PipeElementSpec.ClampLengthMM(600));
    }

    [Test]
    public void ReadOnlyRowTexts_CarryTheTenth_AndDropTheTrailingZero()
    {
        Assert.AreEqual("26.8", PipeElementSpec.OuterDiameterText(PipeSpec.Dn20));
        Assert.AreEqual("21.2", PipeElementSpec.InnerDiameterText(PipeSpec.Dn20));
        Assert.AreEqual("2.8", PipeElementSpec.WallThicknessText(PipeSpec.Dn20));
        Assert.AreEqual("48", PipeElementSpec.OuterDiameterText(PipeSpec.Dn40),
            "48,0 показывают как «48»: лишний ноль читается как точность, которой нет");
    }

    [Test]
    public void Designations_NameBothTheBoreAndTheInch_InTableOrder()
    {
        var labels = PipeElementSpec.Designations();
        Assert.AreEqual(PipeSpec.Table.Length, labels.Length);
        Assert.AreEqual("15 (1/2\")", labels[0]);
        Assert.AreEqual("20 (3/4\")", labels[1]);
        Assert.AreEqual("50 (2\")", labels[labels.Length - 1]);
    }

    [Test]
    public void IndexAndSizeId_AreInverses_SoTheDropdownCannotDriftFromTheTable()
    {
        foreach (var size in PipeSpec.Table)
            Assert.AreEqual(size.Id, PipeElementSpec.SizeIdAt(PipeElementSpec.IndexOf(size.Id)),
                "выпадающий список нумерует ряд ГОСТ по порядку; расхождение здесь молча "
                + "меняет диаметр на соседний");

        Assert.AreEqual(PipeElementSpec.IndexOf(PipeSpec.DEFAULT_SIZE),
            PipeElementSpec.IndexOf("dn999"),
            "неизвестный id показывается умолчанием, а не первой строкой наугад");
        Assert.AreEqual(PipeSpec.DEFAULT_SIZE, PipeElementSpec.SizeIdAt(-1));
        Assert.AreEqual(PipeSpec.DEFAULT_SIZE, PipeElementSpec.SizeIdAt(PipeSpec.Table.Length));
    }
}
