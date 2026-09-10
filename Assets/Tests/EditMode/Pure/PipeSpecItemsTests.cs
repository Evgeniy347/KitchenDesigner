using NUnit.Framework;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Plumbing;

/// <summary>Труба и фитинги объявляют свои строки спецификации сами (карта, «скелет спецификации
/// ни разу не нёс живой груз»). Формулы здесь чисто арифметические — без сцены — поэтому живут
/// в Pure и гоняются вторым, быстрым, прогоном (`geometry/pure-tests`).</summary>
public class PipeSpecItemsTests
{
    [Test]
    public void PipeLine_600mm_Is0_6Meters()
    {
        var item = PipeSpecItems.PipeLine(PipeSpec.Get(PipeSpec.Dn20), 600);

        Assert.AreEqual(SpecUnit.LinearMeters, item.unit);
        Assert.AreEqual(0.6f, item.qty, 0.0001f);
        Assert.AreEqual(SpecSections.Plumbing, item.section);
        Assert.IsFalse(item.hasDims, "у трубы нет короба WxHxD — только ДН и длина");
    }

    /// <summary>Противоположный вход: другая длина даёт другое число метров — формула не
    /// возвращает константу.</summary>
    [Test]
    public void PipeLine_DifferentLength_DifferentMeters()
    {
        var short_ = PipeSpecItems.PipeLine(PipeSpec.Get(PipeSpec.Dn20), 300);
        var long_ = PipeSpecItems.PipeLine(PipeSpec.Get(PipeSpec.Dn20), 900);

        Assert.AreEqual(0.3f, short_.qty, 0.0001f);
        Assert.AreEqual(0.9f, long_.qty, 0.0001f);
        Assert.AreNotEqual(short_.qty, long_.qty);
    }

    /// <summary>Разбивка по условному проходу: дн20 и дн32 обязаны дать РАЗНЫЕ имена строк, иначе
    /// они слились бы в одну при группировке по `GroupKey` в `SpecificationManager`.</summary>
    [Test]
    public void PipeLine_DifferentBore_DifferentGroupKey()
    {
        var dn20 = PipeSpecItems.PipeLine(PipeSpec.Get(PipeSpec.Dn20), 600);
        var dn32 = PipeSpecItems.PipeLine(PipeSpec.Get(PipeSpec.Dn32), 600);

        Assert.AreNotEqual(dn20.GroupKey(), dn32.GroupKey());
    }

    [Test]
    public void FittingLine_IsOnePieceRegardlessOfBore()
    {
        var item = PipeSpecItems.FittingLine("Отвод", PipeSpec.Get(PipeSpec.Dn25));

        Assert.AreEqual(SpecUnit.Pieces, item.unit);
        Assert.AreEqual(1f, item.qty, 0.0001f);
        Assert.IsFalse(item.hasDims);
    }

    /// <summary>Противоположный вход: тот же вид фитинга, другой диаметр — разные строки
    /// («по виду фитинга и диаметру», не только по виду).</summary>
    [Test]
    public void FittingLine_SameKindDifferentBore_DifferentGroupKey()
    {
        var dn20 = PipeSpecItems.FittingLine("Отвод", PipeSpec.Get(PipeSpec.Dn20));
        var dn32 = PipeSpecItems.FittingLine("Отвод", PipeSpec.Get(PipeSpec.Dn32));

        Assert.AreNotEqual(dn20.GroupKey(), dn32.GroupKey());
    }

    [Test]
    public void FittingLine_DifferentKindSameBore_DifferentGroupKey()
    {
        var elbow = PipeSpecItems.FittingLine("Отвод", PipeSpec.Get(PipeSpec.Dn20));
        var tee = PipeSpecItems.FittingLine("Тройник", PipeSpec.Get(PipeSpec.Dn20));

        Assert.AreNotEqual(elbow.GroupKey(), tee.GroupKey());
    }
}
