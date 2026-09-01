using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class SpatialGridRendererTests
{
    [Test]
    public void IsMajorLine_EveryWholeMetre_AndNothingBetween()
    {
        Assert.AreEqual(1000f, SpatialGridRenderer.MajorStepMM,
            "крупная линия — раз в метр; изменение шага меняет читаемость сетки");

        for (int metre = -3; metre <= 3; metre++)
            Assert.IsTrue(SpatialGridRenderer.IsMajorLine(metre),
                "линия на целом метре обязана быть крупной: " + metre);

        foreach (float between in new[] { 0.1f, 0.5f, 0.9f, 1.1f, -2.4f })
            Assert.IsFalse(SpatialGridRenderer.IsMajorLine(between),
                "промежуточная линия должна остаться мелкой: " + between);
    }

    [Test]
    public void MinorStep_DividesTheMajorStep_SoEveryMetreLandsOnALine()
    {
        float linesPerMajor = SpatialGridRenderer.MajorStepMM / SpatialGridRenderer.StepMM;

        Assert.AreEqual(Mathf.Round(linesPerMajor), linesPerMajor, 1e-4f,
            "шаг метра обязан делиться на шаг сетки нацело, иначе крупные линии не попадут на мелкие");
        Assert.AreEqual(100f, SpatialGridRenderer.StepMM, "шаг сетки — 100 мм");
    }

    [Test]
    public void Extent_ReachesTheBoundaryExactly_OnAWholeNumberOfSteps()
    {
        float stepsToEdge = SpatialGridRenderer.HalfExtentUnits
                            / (SpatialGridRenderer.StepMM * AppConstants.MM_TO_UNITS);

        Assert.AreEqual(Mathf.Round(stepsToEdge), stepsToEdge, 1e-3f,
            "полуразмер сетки обязан быть целым числом шагов: иначе крайняя линия обрывается не на краю");
        Assert.IsTrue(SpatialGridRenderer.IsMajorLine(SpatialGridRenderer.HalfExtentUnits),
            "край сетки приходится на крупную линию");
    }
}
