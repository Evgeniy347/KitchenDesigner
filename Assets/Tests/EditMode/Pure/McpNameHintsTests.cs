using System.Linq;
using NUnit.Framework;
using KitchenDesigner.Core.MCP;

public class McpNameHintsTests
{
    private static readonly string[] Names = { "Wall", "Wall_North", "Win1", "Cab1", "Cab2", "Floor" };

    [Test]
    public void Closest_ATypoFindsTheRealName_AndAnUnrelatedWordFindsNothing()
    {
        CollectionAssert.Contains(McpNameHints.Closest("Wal", Names), "Wall");
        CollectionAssert.Contains(McpNameHints.Closest("cab_1", Names), "Cab1", "регистр и подчёркивание не мешают");
        CollectionAssert.IsEmpty(McpNameHints.Closest("Refrigerator", Names), "ничего похожего - молчим, а не гадаем");
    }

    [Test]
    public void Closest_IsCappedAndDeterministic()
    {
        var many = Enumerable.Range(0, 20).Select(i => "Shelf" + i).ToArray();

        var close = McpNameHints.Closest("Shelf", many, 3);

        Assert.AreEqual(3, close.Count);
        CollectionAssert.AreEqual(close, McpNameHints.Closest("Shelf", many.Reverse(), 3), "порядок входа не влияет");
    }

    [Test]
    public void NotFound_KeepsTheOldPrefix_AndAddsTheCandidates()
    {
        var message = McpNameHints.NotFound("Wal", Names);

        StringAssert.StartsWith("Element not found: Wal", message);
        StringAssert.Contains("closest names: Wall", message);
        StringAssert.StartsWith("Element not found: Zzz", McpNameHints.NotFound("Zzz", Names));
        StringAssert.Contains("get_scene_tree", McpNameHints.NotFound("Zzz", Names), "и без кандидатов сказано, где взять имена");
    }

    [Test]
    public void AlreadyExists_OffersAFreeNameAndTheRightTools()
    {
        var message = McpNameHints.AlreadyExists("Cab1", new[] { "Cab1", "Cab1_2" });

        StringAssert.Contains("'Cab1' already exists", message);
        StringAssert.Contains("'Cab1_3'", message, "предложенное имя действительно свободно");
        StringAssert.Contains("edit_elements", message);
        StringAssert.Contains("place", message);
    }

    [Test]
    public void Locked_NamesTheRealTool_NotTheOneThatNeverExisted()
    {
        var message = McpNameHints.Locked("Cab1");

        StringAssert.Contains("LOCKED", message);
        StringAssert.Contains("edit_elements", message);
        StringAssert.DoesNotContain("set_element_lock", message, "прежнее сообщение отсылало к несуществующему инструменту");
    }

    [Test]
    public void FaceAxisMismatch_SaysWhichTargetFaceFitsTheMovingFace()
    {
        var message = McpNameHints.FaceAxisMismatch("left", "top");

        StringAssert.Contains("target_face 'right'", message);
    }

    [TestCase("left", 0, false)]
    [TestCase("RIGHT", 0, true)]
    [TestCase("bottom", 1, false)]
    [TestCase("top", 1, true)]
    [TestCase("back", 2, false)]
    [TestCase("front", 2, true)]
    public void McpFace_ParsesEverySixFaces_ByAxisAndSide(string word, int axis, bool maxSide)
    {
        Assert.IsTrue(McpFace.TryParse(word, out int a, out bool m));
        Assert.AreEqual((axis, maxSide), (a, m));
        Assert.AreEqual(word.ToLowerInvariant(), McpFace.NameOf(a, m), "имя грани обратимо");
    }

    [Test]
    public void McpFace_RefusesAnythingElse()
    {
        Assert.IsFalse(McpFace.TryParse("inside", out _, out _));
        Assert.IsFalse(McpFace.TryParse(null, out _, out _));
        Assert.IsFalse(McpFace.TryParseAxis("w", out _));
        Assert.IsTrue(McpFace.TryParseAxis("Z", out int axis) && axis == 2);
    }
}
