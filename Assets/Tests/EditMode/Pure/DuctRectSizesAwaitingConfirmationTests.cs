using NUnit.Framework;
using KitchenDesigner.Core.Ventilation;

/// <summary>Список типоразмеров прямоугольного воздуховода НЕ подтверждён пользователем
/// (3.5, part3-plan.md: «Rect sizes list… don't invent it silently»). Числа ниже — рабочая
/// заготовка для будущего справочника (UI, ведомость), а не решение; тест называет их
/// поимённо, чтобы любая правка была видна как красная строка, а не молчаливый дрейф.</summary>
public class DuctRectSizesAwaitingConfirmationTests
{
    [Test]
    public void Candidates_Are_100x150_100x200_150x150_150x200_200x200_200x300_300x300_PendingUserConfirmation()
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
        }, DuctRectSizesAwaitingConfirmation.Candidates);
    }
}
