using System.Collections.Generic;
using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>WallMeshBuilder.CollapseNearDuplicates — слияние почти совпадающих
/// линий реза.
///
/// Здесь живёт причина, которая раньше была комментарием в WallMeshBuilder.cs:
/// пара окон «на одной высоте» (Y отличается на доли мм) давала ячейку тоньше
/// порога, а её отбрасывали ЦЕЛИКОМ — вместе с гранями и откосом. На экране это
/// была сквозная полоса света у верха и низа проёма. Сам эффект на стене держит
/// WallCutoutTests; здесь — свойства самой функции.</summary>
public class WallSplitCollapseTests
{
    private const float MinGap = 0.01f;

    private static List<float> Collapse(params float[] splits)
    {
        var list = new List<float>(splits);
        WallMeshBuilder.CollapseNearDuplicates(list, MinGap);
        return list;
    }

    [Test]
    public void SplitsFartherApartThanTheGap_AreAllKept()
    {
        CollectionAssert.AreEqual(new[] { 0f, 0.3f, 0.7f, 1f }, Collapse(0f, 0.3f, 0.7f, 1f),
            "положительный контроль: нормальные линии реза функция не трогает");
    }

    [Test]
    public void TwoSplitsCloserThanTheGap_BecomeOne()
    {
        var result = Collapse(0f, 0.5f, 0.5001f, 1f);

        CollectionAssert.AreEqual(new[] { 0f, 0.5f, 1f }, result,
            "две границы в доле миллиметра друг от друга дали бы ячейку-волосок, "
            + "а её отбрасывают вместе с откосом — и в стене остаётся щель со светом");
    }

    [Test]
    public void EdgesOfTheFace_SurviveEvenWhenSomethingCrowdsThem()
    {
        var result = Collapse(0f, 0.000001f, 0.999999f, 1f);

        Assert.AreEqual(0f, result[0], 1e-6f, "ближний край грани обязан уцелеть");
        Assert.AreEqual(1f, result[result.Count - 1], 1e-6f, "и дальний тоже");
        Assert.GreaterOrEqual(result.Count, 2, "иначе резать было бы нечего");
    }

    [Test]
    public void NoGapNarrowerThanTheThreshold_SurvivesTheCollapse()
    {
        var result = Collapse(0f, 0.002f, 0.004f, 0.006f, 0.5f, 0.9999f, 1f);

        for (int i = 1; i < result.Count; i++)
            Assert.GreaterOrEqual(result[i] - result[i - 1], MinGap - 1e-6f,
                $"промежуток {result[i - 1]:F5}..{result[i]:F5} тоньше порога — "
                + "ровно та ячейка, которую потом выбросят вместе с гранью");
    }
}
