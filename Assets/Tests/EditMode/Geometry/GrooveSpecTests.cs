using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>Паз задаётся парой «вид + сторона», и равенство по ЭТОЙ паре —
/// не формальность: по нему сравниваются наборы пазов при сохранении, undo
/// и пересборке меша. Ошибка в равенстве не падает, а тихо теряет паз.
///
/// Подписи для UI (SideLabel/KindLabel) намеренно не проверяются: это текст
/// интерфейса, его утверждение в тестах ломается при любой правке формулировки
/// и ничего не защищает.</summary>
public class GrooveSpecTests
{
    [Test]
    public void Equals_RequiresBothKindAndSide()
    {
        var blindTop = new GrooveSpec(GrooveKind.Blind, GrooveSide.Top);

        Assert.IsTrue(blindTop.Equals(new GrooveSpec(GrooveKind.Blind, GrooveSide.Top)));
        Assert.IsFalse(blindTop.Equals(new GrooveSpec(GrooveKind.Blind, GrooveSide.Bottom)),
            "та же сторона, другой вид — уже другой паз");
        Assert.IsFalse(blindTop.Equals(new GrooveSpec(GrooveKind.Through, GrooveSide.Top)),
            "тот же вид, другая сторона — тоже другой паз");
        Assert.IsFalse(blindTop.Equals(new GrooveSpec(GrooveKind.Through, GrooveSide.Bottom)));
    }

    [Test]
    public void EqualSpecs_ShareHashCode()
    {
        var a = new GrooveSpec(GrooveKind.Through, GrooveSide.Left);
        var b = new GrooveSpec(GrooveKind.Through, GrooveSide.Left);

        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }

    /// <summary>Все восемь сочетаний «вид × сторона» обязаны давать РАЗНЫЕ хеши.
    /// Множитель в хеше подобран так, чтобы вид не накладывался на сторону;
    /// стоит его уменьшить — и пазы начнут схлопываться в наборах.</summary>
    [Test]
    public void EveryKindSideCombination_HasItsOwnHashCode()
    {
        var hashes = new System.Collections.Generic.List<int>();
        foreach (GrooveKind kind in System.Enum.GetValues(typeof(GrooveKind)))
            foreach (GrooveSide side in System.Enum.GetValues(typeof(GrooveSide)))
                hashes.Add(new GrooveSpec(kind, side).GetHashCode());

        CollectionAssert.AllItemsAreUnique(hashes);
    }

    /// <summary>Обозначение попадает в спецификацию, поэтому вид паза в нём
    /// обязан различаться — иначе глухой и сквозной сольются в одну строку.</summary>
    [Test]
    public void Designation_DistinguishesKinds()
    {
        Assert.AreNotEqual(
            GrooveSpec.Designation(GrooveKind.Blind),
            GrooveSpec.Designation(GrooveKind.Through));
    }

    [Test]
    public void ToString_DistinguishesSides()
    {
        var top = new GrooveSpec(GrooveKind.Blind, GrooveSide.Top).ToString();
        var left = new GrooveSpec(GrooveKind.Blind, GrooveSide.Left).ToString();

        Assert.AreNotEqual(top, left);
        Assert.IsNotEmpty(top);
    }

    [Test]
    public void SideLabel_IsDistinctForEverySide()
    {
        var labels = new[]
        {
            GrooveSpec.SideLabel(GrooveSide.Top),
            GrooveSpec.SideLabel(GrooveSide.Bottom),
            GrooveSpec.SideLabel(GrooveSide.Left),
            GrooveSpec.SideLabel(GrooveSide.Right),
        };

        CollectionAssert.AllItemsAreUnique(labels);
        foreach (var label in labels)
            Assert.IsNotEmpty(label, "пустая подпись стороны не отличима от отсутствующей");
    }
}
