using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Подпись входов накладки: то, по чему `HighlightOverlay` решает, что
/// нарисованное устарело. Прежде он сравнивал зашитую тройку «поза + поворот +
/// `DimensionsMM`» — а красная гильза фитинга считается от `BoreSizeIds`, который
/// меняется отдельно от габаритной коробки. Расширившийся проход оставлял гильзу
/// на старом радиусе, и никакая проверка позы этого увидеть не могла.
///
/// Поэтому подпись теперь даёт ВЫЗЫВАЮЩИЙ — из тех же входов, что читает его
/// замыкание перестроения. От подписи нужно одно: разные входы — разное значение.
/// Сенсор на это обязан проверять именно РАЗЛИЧЕНИЕ (одинаковые входы дают
/// одинаковую подпись — это верно и для функции, всегда возвращающей ноль).</summary>
public class HighlightSignatureTests
{
    private static readonly Quaternion QuarterTurnAroundY =
        new Quaternion(0f, 0.70710678f, 0f, 0.70710678f);

    private static int Of(Vector3 pos, Quaternion rot, float radiusMM, string? bore) =>
        new HighlightSignature().Add(pos).Add(rot).Add(radiusMM).Add(bore).Value;

    [Test]
    public void TheSameInputs_GiveTheSameSignature()
    {
        Assert.AreEqual(
            Of(new Vector3(1f, 2f, 3f), Quaternion.identity, 13.4f, "dn25"),
            Of(new Vector3(1f, 2f, 3f), Quaternion.identity, 13.4f, "dn25"),
            "подпись, дрожащая на неизменных входах, перестраивала бы накладку "
            + "каждый кадр наведения");
    }

    [Test]
    public void ABoreThatWidens_ChangesTheSignature_ThoughThePoseDoesNot()
    {
        Assert.AreNotEqual(
            Of(Vector3.zero, Quaternion.identity, 13.4f, "dn20"),
            Of(Vector3.zero, Quaternion.identity, 16.75f, "dn25"),
            "ровно тот случай, который прежний Sync не видел: поза та же, "
            + "а радиус устья другой");
    }

    [Test]
    public void EachInput_IsHeardSeparately()
    {
        int baseline = Of(Vector3.zero, Quaternion.identity, 10f, "dn20");

        Assert.AreNotEqual(baseline, Of(new Vector3(0f, 0f, 0.001f), Quaternion.identity, 10f,
            "dn20"), "сдвиг хозяина обязан менять подпись");
        Assert.AreNotEqual(baseline, Of(Vector3.zero, QuarterTurnAroundY, 10f, "dn20"),
            "поворот хозяина обязан менять подпись");
        Assert.AreNotEqual(baseline, Of(Vector3.zero, Quaternion.identity, 10.5f, "dn20"),
            "радиус обязан менять подпись");
        Assert.AreNotEqual(baseline, Of(Vector3.zero, Quaternion.identity, 10f, null),
            "пропавший размер прохода — тоже другое состояние, а не то же самое");
    }

    [Test]
    public void OrderOfTheInputs_Matters()
    {
        Assert.AreNotEqual(
            new HighlightSignature().Add(2f).Add(3f).Value,
            new HighlightSignature().Add(3f).Add(2f).Value,
            "подпись, складывающая входы в одну кучу без порядка, склеила бы "
            + "перестановку длины и диаметра в одно значение");
    }

    [Test]
    public void ASleeve_SignsAllThreeOfItsParts()
    {
        var sleeve = new HighlightSleeve(Vector3.zero, new Vector3(0f, 100f, 0f), 13.4f);
        int baseline = new HighlightSignature().Add(sleeve).Value;

        Assert.AreNotEqual(baseline, new HighlightSignature().Add(
            new HighlightSleeve(Vector3.zero, new Vector3(0f, 90f, 0f), 13.4f)).Value,
            "укоротившаяся гильза — другая гильза");
        Assert.AreNotEqual(baseline, new HighlightSignature().Add(
            new HighlightSleeve(Vector3.zero, new Vector3(0f, 100f, 0f), 16.75f)).Value,
            "раздувшаяся гильза — тоже другая");
        Assert.AreNotEqual(baseline, new HighlightSignature().Add(
            new HighlightSleeve(new Vector3(0f, 10f, 0f), new Vector3(0f, 100f, 0f), 13.4f)).Value,
            "и переехавшее начало гильзы обязано быть слышно");
    }
}
