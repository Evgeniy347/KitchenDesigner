using NUnit.Framework;
using KitchenDesigner.Core.MCP;

/// <summary>Сокращение имени под размер детали. Картинка — единственное место, где модель видит имя,
/// и ровно там оно обязано оставаться однозначным: два шкафа с одинаковым «Ca..01» — это путаница вместо подсказки.</summary>
public class PlanLabelNamesTests
{
    [Test]
    public void Unique_TheSecondNameWithTheSameCut_GetsANumberedForm()
    {
        var names = new PlanLabelNames();
        var first = names.Unique("Cabinet_Left_Tall", 9)!;
        names.Commit(first, "Cabinet_Left_Tall");

        var second = names.Unique("Cabinet_Right_Tall", 9);

        Assert.AreEqual("Cabin..ll", first);
        Assert.IsNotNull(second);
        Assert.AreNotEqual(first, second);
        Assert.LessOrEqual(second!.Length, 9);
    }

    [Test]
    public void Unique_TheSameNameAsksTwice_GetsTheSameLabel()
    {
        var names = new PlanLabelNames();
        var first = names.Unique("Cabinet_Left_A1", 8)!;
        names.Commit(first, "Cabinet_Left_A1");

        Assert.AreEqual(first, names.Unique("Cabinet_Left_A1", 8));
    }

    [Test]
    public void Unique_ANameThatFits_IsReturnedAsItIs()
    {
        Assert.AreEqual("Cab01", new PlanLabelNames().Unique("Cab01", 8));
    }

    [Test]
    public void Unique_NumberedSeries_KeepEachItsOwnNumber_WithoutAnyVariant()
    {
        var names = new PlanLabelNames();
        var shown = new System.Collections.Generic.List<string>();
        for (int i = 1; i <= 12; i++)
        {
            var full = "Cabinet_Left_Tall_" + i.ToString("00");
            var label = names.Unique(full, 10)!;
            names.Commit(label, full);
            shown.Add(label);
            StringAssert.EndsWith(i.ToString("00"), label, "номер — то, по чему деталь узнают");
        }
        CollectionAssert.AllItemsAreUnique(shown);
    }

    [Test]
    public void Unique_WhenEveryVariantIsTaken_SaysSoInsteadOfLying()
    {
        var names = new PlanLabelNames();
        names.Commit(McpNameShortening.Fit("Cabinet_Left_A1", 8), "Other");
        for (int n = 2; n <= PlanLabelNames.MaxVariants; n++) names.Commit("Cabin.." + n, "Other" + n);

        Assert.IsNull(names.Unique("Cabinet_Left_A1", 8));
    }
}
