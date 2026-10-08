using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using KitchenDesigner.Core.MCP;

/// <summary>Шрифт 5x8 сделан руками, и глазом его не проверишь: один неверный бит превращает «I» в «l».
/// Сторожа держат форму таблицы, различимость похожих знаков и то, что неизвестный знак не роняет рисунок.</summary>
public class PlanFontTests
{
    private const string NameAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_-.!:,()/=+? ";

    [Test]
    public void Has_EveryCharacterOfAnElementName_ReturnsTrue()
    {
        var missing = NameAlphabet.Where(c => !PlanFont.Has(c)).ToList();

        Assert.IsEmpty(missing, "имена деталей — латиница, цифры, _ и -: знак без глифа превратился бы в «?»");
    }

    [Test]
    public void RowsOf_EveryGlyph_IsEightRowsOfFiveColumns()
    {
        foreach (var c in PlanFont.Known)
        {
            var rows = PlanFont.RowsOf(c);
            Assert.AreEqual(PlanFont.Rows, rows.Length, "строк у «" + c + "»");
            Assert.That(rows.All(row => row < 1 << PlanFont.Columns), "бит правее пятой колонки у «" + c + "»");
        }
    }

    [Test]
    public void Glyphs_NoTwoOfThem_AreTheSameBitmap()
    {
        var seen = new Dictionary<string, char>();
        foreach (var c in PlanFont.Known)
        {
            var key = string.Join(",", PlanFont.RowsOf(c));
            Assert.IsFalse(seen.TryGetValue(key, out var twin), "«" + c + "» и «" + twin + "» нарисованы одинаково");
            seen[key] = c;
        }
    }

    [Test]
    public void Glyphs_TheLookAlikes_DifferByAtLeastTwoPixels()
    {
        var pairs = new[] { "O0", "Il", "I1", "l1", "S5", "B8", "Z2", "G6", "gq", "uv", "nh", "rn", "C(", "-_", ":!", "=+" };
        foreach (var pair in pairs)
            Assert.GreaterOrEqual(Distance(pair[0], pair[1]), 2, "«" + pair[0] + "» и «" + pair[1] + "» слишком близки");
    }

    [Test]
    public void RowsOf_AnUnknownCharacter_IsAQuestionMark()
    {
        CollectionAssert.AreEqual(PlanFont.RowsOf('?'), PlanFont.RowsOf('é'));
        CollectionAssert.AreEqual(PlanFont.RowsOf('?'), PlanFont.RowsOf('Ж'));
    }

    [Test]
    public void RowsOf_OnlyTheLettersWithATail_UseTheEighthRow()
    {
        var tailed = NameAlphabet.Where(c => PlanFont.RowsOf(c)[PlanFont.Rows - 1] != 0).ToList();

        CollectionAssert.AreEquivalent("gjpqy_,".ToCharArray(), tailed);
    }

    [Test]
    public void IsSet_ReadsTheBitsLeftToRight()
    {
        Assert.IsTrue(PlanFont.IsSet('L', 0, 0), "левый столбец «L»");
        Assert.IsFalse(PlanFont.IsSet('L', 4, 0), "правый столбец «L» вверху пуст");
        Assert.IsTrue(PlanFont.IsSet('L', 4, 6), "основание «L» идёт до правого края");
    }

    private static int Distance(char a, char b)
    {
        int differing = 0;
        for (int row = 0; row < PlanFont.Rows; row++)
            for (int column = 0; column < PlanFont.Columns; column++)
                if (PlanFont.IsSet(a, column, row) != PlanFont.IsSet(b, column, row)) differing++;
        return differing;
    }
}
