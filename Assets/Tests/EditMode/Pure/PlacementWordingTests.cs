using System.Collections.Generic;
using NUnit.Framework;
using KitchenDesigner.Core.MCP;

public class PlacementWordingTests
{
    [TestCase(2f, "2")]
    [TestCase(12.3f, "12.3")]
    [TestCase(12.04f, "12")]
    [TestCase(-0.04f, "0")]
    [TestCase(-3.5f, "-3.5")]
    [TestCase(1200f, "1200")]
    public void Mm_RoundsToATenthAndDropsTheTrailingZero(float value, string expected)
    {
        Assert.AreEqual(expected, PlacementWording.Mm(value));
    }

    [Test]
    public void Contact_PutsTheFaceOfThePartBeforeTheNeighbour()
    {
        Assert.AreEqual("left→B3", PlacementWording.Contact(new PlacementContact { n = "B3", face = "left" }));
    }

    [Test]
    public void Gap_AddsTheDistanceInMillimetres()
    {
        Assert.AreEqual("right→B5 gap2", PlacementWording.Gap(new PlacementGap { n = "B5", face = "right", gapMm = 2f }));
    }

    [Test]
    public void Relations_ContactsFirstThenGaps_WithoutTheSupportContact()
    {
        var placement = new PlacementInfo
        {
            on = "Floor",
            touches = new List<PlacementContact>
            {
                new PlacementContact { n = "Floor", face = "bottom" },
                new PlacementContact { n = "Wall", face = "back" },
            },
            gaps = new List<PlacementGap> { new PlacementGap { n = "B2", face = "right", gapMm = 12f } },
        };

        CollectionAssert.AreEqual(new[] { "back→Wall", "right→B2 gap12" }, PlacementWording.Relations(placement));
    }

    [Test]
    public void Relations_AFloorContactThatIsNotTheNamedSupport_StaysAsARelation()
    {
        var placement = new PlacementInfo
        {
            on = null,
            touches = new List<PlacementContact> { new PlacementContact { n = "Floor", face = "bottom" } },
        };

        CollectionAssert.AreEqual(new[] { "bottom→Floor" }, PlacementWording.Relations(placement));
    }

    [Test]
    public void Relations_NothingAround_IsEmpty()
    {
        CollectionAssert.IsEmpty(PlacementWording.Relations(new PlacementInfo()));
    }
}
