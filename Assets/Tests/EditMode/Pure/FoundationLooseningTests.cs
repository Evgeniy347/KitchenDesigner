using System;
using System.Linq;
using NUnit.Framework;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Construction;

/// <summary>Коэффициент первоначального разрыхления (Kp) переводит объём траншеи «в плотном
/// теле» в объём вынутого грунта, который увозят машинами, — он больше 1, потому что рыхлый
/// грунт занимает больше места, чем тот же грунт в массиве. Числа ниже — из ЕНиР (сборник Е2),
/// а не из СП 22.13330: у ленты нет своего норматива на разрыхление, это раздел земляных работ.
/// Исполнитель НЕ смог сверить точные величины таблицы с текстом сборника — весь класс помечен
/// [Category("NormativeUnverified")] по правилу docs/todo_evolution.md §3.6.</summary>
[Category("NormativeUnverified")]
public class FoundationLooseningTests
{
    [Test]
    public void FoundationLoosening_Enir_SborникE2_CoefficientsGrowWithWorseSoil()
    {
        Assert.IsTrue(FoundationLoosening.TryCoefficient(SoilKind.Sand, out float sand));
        Assert.IsTrue(FoundationLoosening.TryCoefficient(SoilKind.SandyLoam, out float sandyLoam));
        Assert.IsTrue(FoundationLoosening.TryCoefficient(SoilKind.Loam, out float loam));
        Assert.IsTrue(FoundationLoosening.TryCoefficient(SoilKind.Clay, out float clay));
        Assert.IsTrue(FoundationLoosening.TryCoefficient(SoilKind.Peat, out float peat));

        Assert.AreEqual(1.10f, sand, 1e-3f, "песок — наименьшее разрыхление из пяти; число не "
            + "цитата пункта, см. FoundationLoosening.Source");
        Assert.AreEqual(1.14f, sandyLoam, 1e-3f);
        Assert.AreEqual(1.20f, loam, 1e-3f);
        Assert.AreEqual(1.28f, clay, 1e-3f);
        Assert.AreEqual(1.30f, peat, 1e-3f, "торф — самое большое разрыхление: органика держит "
            + "меньше исходного объёма в куске и сильнее вспухает при выемке");

        Assert.LessOrEqual(sand, sandyLoam);
        Assert.LessOrEqual(sandyLoam, loam);
        Assert.LessOrEqual(loam, clay);
        Assert.LessOrEqual(clay, peat);
    }

    [Test]
    public void FoundationLoosening_UnknownSoil_TakesTheLargestCoefficientOfTheOnesOffered()
    {
        Assert.IsTrue(FoundationLoosening.TryCoefficient(SoilKind.Unknown, out float unknown));

        float largest = Enum.GetValues(typeof(SoilKind)).Cast<SoilKind>()
            .Where(s => s != SoilKind.Unknown && FoundationLoosening.TryCoefficient(s, out _))
            .Max(s => { FoundationLoosening.TryCoefficient(s, out float kp); return kp; });

        Assert.AreEqual(largest, unknown, 1e-3f,
            "«неизвестно» — худший случай, тем же порядком, что и у FrostDepth и "
            + "FoundationSoleWidth: берётся наибольший коэффициент из предложенных, иначе "
            + "заказ вывоза грунта выйдет заниженным там, где грунт не определяли");
    }

    [Test]
    public void FoundationLoosening_AnUnknownEnumValue_IsRefused_RatherThanSilentlyTakingOne()
    {
        Assert.IsFalse(FoundationLoosening.TryCoefficient((SoilKind)99, out float kp),
            "значение вне перечисления приходит из чужой версии сохранения — молчаливый откат "
            + "к любой из строк выдал бы коэффициент, который к этому грунту отношения не имеет");
        Assert.AreEqual(1f, kp, 1e-4f, "отказ оставляет в out нейтральное значение (без "
            + "разрыхления), а не мусор");
    }
}
