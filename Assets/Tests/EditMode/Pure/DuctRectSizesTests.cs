using NUnit.Framework;
using KitchenDesigner.Core.Ventilation;

/// <summary>Ряд типоразмеров прямоугольного воздуховода ПОДТВЕРЖДЁН пользователем
/// (2026-09-26, часть 3, VENT), поэтому класс и файл больше не носят имя
/// "AwaitingConfirmation" — числа зафиксированы как решение, а не рабочая заготовка.
/// Тест по-прежнему называет их поимённо, чтобы любая правка была видна как красная
/// строка, а не молчаливый дрейф.</summary>
public class DuctRectSizesTests
{
    [Test]
    public void Candidates_Are_100x150_100x200_150x150_150x200_200x200_200x300_300x300_Confirmed()
    {
        CollectionAssert.AreEqual(new[]
        {
            (100, 150),
            (100, 200),
            (150, 150),
            (150, 200),
            (200, 200),
            (200, 300),
            (300, 300),
        }, DuctRectSizes.Candidates);
    }
}
