using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Порог перекрытия граней: полоска уже 0,5 мм — не контакт.
///
/// Число не новое и выбрано не «на глаз»: это <c>Tolerance.ContactMm</c>, тот же
/// допуск, которым <c>FaceContactScan</c> меряет зазор ПО НОРМАЛИ. Порог по ширине
/// перекрытия В ПЛОСКОСТИ — это он же, повёрнутый на 90°; два разных числа по
/// нормали и в плоскости означали бы, что «касание» значит разное в зависимости от
/// направления. Заодно 0,5 мм — максимальный сдвиг, который даёт деталям
/// <c>MmGrid</c>, так что перекрытие 0,1–0,5 мм это след округления к сетке, а не
/// чей-то замысел; 1 мм брать нельзя — на целочисленной сетке это наименьшее
/// ПРОЕКТИРУЕМОЕ перекрытие, и звать его шумом было бы неправдой.
///
/// Чего порог НЕ касается: <c>FaceContacts.OverlapAllowingEdgeTouch</c>, где
/// линейное касание — осмысленный ответ (снэп, посадка в паз). Там «ровно ребром»
/// значит «да», здесь — «нет», и это разные вопросы, а не разные ответы на один.</summary>
public class FaceOverlapThresholdTests
{
    private const float MM = 0.001f;

    private const float PlateSide = 100f * MM;

    private const float PlateThickness = 18f * MM;

    /// <summary>Шире порога: 0,6 мм. Взято не 0,5 и не 1,0 намеренно — значение на
    /// самой границе не различает «строго больше» и «не меньше», а 1,0 мм совпало
    /// бы с шагом сетки и прошло бы даже при вдвое большем пороге.</summary>
    private const float WiderThanThreshold = 0.6f * MM;

    /// <summary>Уже порога: 0,4 мм.</summary>
    private const float NarrowerThanThreshold = 0.4f * MM;

    private static ElementGeometry Plate(float xOffset, float zOffset, float yCentre) =>
        ElementGeometry.Box("plate", new Vector3(xOffset, yCentre, zOffset),
            new Vector3(PlateSide, PlateThickness, PlateSide));

    /// <summary>Две плиты, лежащие друг на друге, сдвинутые так, что их общие грани
    /// перекрываются полоской заданной ширины по X и по Z.</summary>
    private static bool FacesTouch(float overlapX, float overlapZ,
        out float area, out float ratio)
    {
        var lower = Plate(0f, 0f, 0f);
        var upper = Plate(PlateSide - overlapX, PlateSide - overlapZ, PlateThickness);
        return FaceContacts.FacesOverlap(lower.Faces[2], upper.Faces[3], out area, out ratio);
    }

    [Test]
    public void FacesOverlap_StripWiderThanContactTolerance_IsAContact()
    {
        Assert.IsTrue(FacesTouch(WiderThanThreshold, PlateSide, out float area, out _),
            "Полоска 0,6 мм шире допуска 0,5 мм — это контакт, а не шум");
        Assert.That(area, Is.GreaterThan(0f),
            "Контакт без площади — признак того, что вернулось не то перекрытие");
    }

    [Test]
    public void FacesOverlap_StripNarrowerThanContactTolerance_IsNotAContact()
    {
        Assert.IsFalse(FacesTouch(NarrowerThanThreshold, PlateSide, out float area, out float ratio),
            "Полоска 0,4 мм уже допуска 0,5 мм — это след округления к мм-сетке, "
            + "а не контакт: у пользователя такая деталь ничего не подпирает");
        Assert.That(area, Is.Zero, "Отказ обязан отдавать нулевую площадь");
        Assert.That(ratio, Is.Zero, "Отказ обязан отдавать нулевую долю");
    }

    [Test]
    public void FacesOverlap_EdgeTouchingExactly_IsNotAContact()
    {
        Assert.IsFalse(FacesTouch(0f, PlateSide, out _, out _),
            "Касание ровно ребром — не контакт для валидации; смысл «линейного "
            + "контакта» живёт в OverlapAllowingEdgeTouch и к этому пути отношения не имеет");
    }

    [Test]
    public void FacesOverlap_CornerTouchingExactly_IsNotAContact()
    {
        Assert.IsFalse(FacesTouch(0f, 0f, out _, out _),
            "Касание ровно углом — не контакт: опереться на точку нельзя");
    }

    [Test]
    public void FacesOverlap_StripNarrowOnOneAxisOnly_IsNotAContact()
    {
        Assert.IsFalse(FacesTouch(NarrowerThanThreshold, PlateSide, out _, out _),
            "Порог применяется к КАЖДОЙ оси отдельно");
        Assert.IsFalse(FacesTouch(PlateSide, NarrowerThanThreshold, out _, out _),
            "Порог применяется к КАЖДОЙ оси отдельно, и вторая ось не исключение — "
            + "иначе широкая площадь прощала бы узкую полоску");
    }

    [Test]
    public void FacesOverlap_ThresholdIsTheSameToleranceAsTheGapAlongTheNormal()
    {
        Assert.That(Tolerance.ContactUnits,
            Is.EqualTo(Tolerance.ContactMm * AppConstants.MM_TO_UNITS),
            "Порог в плоскости обязан быть тем же числом, что и зазор по нормали: "
            + "два разных допуска означали бы два разных смысла слова «касание»");
    }
}
