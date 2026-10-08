using NUnit.Framework;
using KitchenDesigner.Core.MCP;

/// <summary>Сокращение имени под размер детали. Картинка — единственное место, где модель видит имя,
/// и ровно там оно обязано оставаться однозначным: два шкафа с одинаковым «Ca..01» — это путаница вместо подсказки.</summary>
public class PlanLabelNamesTests
{
    [Test]
    public void Cut_KeepsTheHeadAndTheLastTwoCharacters()
    {
        Assert.AreEqual("Cabin..A1", PlanLabelNames.Cut("Cabinet_Left_A1", 9));
        Assert.AreEqual("Ca..A1", PlanLabelNames.Cut("Cabinet_Left_A1", 6));
    }

    [Test]
    public void Cut_ANameThatFitsIsLeftAlone()
    {
        Assert.AreEqual("Cab01", PlanLabelNames.Cut("Cab01", 5));
        Assert.AreEqual("Cab01", PlanLabelNames.Cut("Cab01", 12));
    }

    [Test]
    public void Cut_NeverExceedsTheRoom()
    {
        for (int room = PlanLabelNames.MinShortChars; room < 20; room++)
            Assert.LessOrEqual(PlanLabelNames.Cut("Cabinet_Left_Tall_Section_01", room).Length, room, "room " + room);
    }

    [Test]
    public void Unique_TheSecondNameWithTheSameCut_GetsANumberedForm()
    {
        var names = new PlanLabelNames();
        var first = names.Unique("Cabinet_Left_A1", 8)!;
        names.Commit(first, "Cabinet_Left_A1");

        var second = names.Unique("Cabinet_Right_A1", 8);

        Assert.AreEqual("Cabi..A1", first);
        Assert.IsNotNull(second);
        Assert.AreNotEqual(first, second);
        Assert.LessOrEqual(second!.Length, 8);
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
    public void Unique_WhenEveryVariantIsTaken_SaysSoInsteadOfLying()
    {
        var names = new PlanLabelNames();
        names.Commit("Cabi..A1", "Other");
        for (int n = 2; n <= PlanLabelNames.MaxVariants; n++) names.Commit("Cabin.." + n, "Other" + n);

        Assert.IsNull(names.Unique("Cabinet_Left_A1", 8));
    }
}
