using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.Construction;

/// <summary>RoofFrameGeometry turns the flat RoofFrame (RunMm/EaveEdgeMm/RidgeEdgeMm per
/// plane) into actual tilted 3D boundaries that RoofPlaneMesh can extrude. The cross-check
/// that catches a wrong corner: the boundary's SHADOW (its XZ projection, i.e. the plan
/// footprint before the pitch lifts it) must have exactly the area RoofQuantities.PlanAreaM2
/// already computes from the same RoofFrame by a completely different route (Run x average
/// edge). Two independent derivations of the same footprint agreeing is a much stronger
/// proof than either one asserting a hand-picked number.</summary>
public class RoofFrameGeometryTests
{
    private static readonly RoofFootprint WideInX = new RoofFootprint(-4f, 4f, -1.5f, 1.5f);
    private static readonly RoofFootprint WideInZ = new RoofFootprint(-1.5f, 1.5f, -4f, 4f);

    private const float PitchDeg = 30f;
    private const float OverhangMm = 500f;

    private static double PlanAreaM2(Vector3[] boundary)
    {
        double sum = 0d;
        for (int i = 0; i < boundary.Length; i++)
        {
            var a = boundary[i];
            var b = boundary[(i + 1) % boundary.Length];
            sum += a.x * b.z - b.x * a.z;
        }
        return System.Math.Abs(sum) * 0.5d;
    }

    private static double TotalPlanAreaM2(List<Vector3[]> boundaries)
    {
        double total = 0d;
        foreach (var b in boundaries) total += PlanAreaM2(b);
        return total;
    }

    [TestCase(RoofType.Single, 1)]
    [TestCase(RoofType.Gable, 2)]
    [TestCase(RoofType.Hip, 4)]
    public void PlaneBoundariesUnits_ReturnsOneBoundaryPerPlane(RoofType type, int expectedCount)
    {
        var boundaries = RoofFrameGeometry.PlaneBoundariesUnits(WideInX, type,
            RoofRidgeAxis.Auto, OverhangMm, PitchDeg);

        Assert.AreEqual(expectedCount, boundaries.Count);
    }

    [TestCase(RoofType.Single)]
    [TestCase(RoofType.Gable)]
    [TestCase(RoofType.Hip)]
    public void PlaneBoundariesUnits_ShadowArea_MatchesRoofQuantities_PlanAreaM2(RoofType type)
    {
        var boundaries = RoofFrameGeometry.PlaneBoundariesUnits(WideInX, type,
            RoofRidgeAxis.Auto, OverhangMm, PitchDeg);
        var frame = RoofPitchPlanes.Build(WideInX, type, RoofRidgeAxis.Auto, OverhangMm);

        Assert.AreEqual(RoofQuantities.PlanAreaM2(frame), TotalPlanAreaM2(boundaries), 1e-6,
            "тень контура (проекция на XZ, до подъёма по уклону) обязана давать ту же "
            + "площадь, что и RoofQuantities.PlanAreaM2 из ТОГО ЖЕ RoofFrame - иначе одна "
            + "из двух реализаций считает не тот контур");
    }

    [Test]
    public void PlaneBoundariesUnits_Hip_TrapezoidAndTriangle_ShareTheExactRidgeCorner()
    {
        var boundaries = RoofFrameGeometry.PlaneBoundariesUnits(WideInX, RoofType.Hip,
            RoofRidgeAxis.Auto, OverhangMm, PitchDeg);

        var trapezoidRidgeCorners = new HashSet<Vector3>();
        foreach (var corner in boundaries[0]) trapezoidRidgeCorners.Add(Round(corner));
        foreach (var corner in boundaries[1]) trapezoidRidgeCorners.Add(Round(corner));

        var triangleApexA = Round(FindApex(boundaries[2]));
        var triangleApexB = Round(FindApex(boundaries[3]));

        Assert.Contains(triangleApexA, new List<Vector3>(trapezoidRidgeCorners),
            "вершина одного вальмового треугольника обязана лежать РОВНО на конце "
            + "конькового ребра трапеций - иначе в этом угле щель");
        Assert.Contains(triangleApexB, new List<Vector3>(trapezoidRidgeCorners),
            "то же самое для второго вальмового треугольника, другой конец конька");
    }

    private static Vector3 FindApex(Vector3[] triangle)
    {
        var apex = triangle[0];
        for (int i = 1; i < triangle.Length; i++)
            if (triangle[i].y > apex.y) apex = triangle[i];
        return apex;
    }

    private static Vector3 Round(Vector3 v) =>
        new Vector3(Mathf.Round(v.x * 1000f) / 1000f, Mathf.Round(v.y * 1000f) / 1000f,
            Mathf.Round(v.z * 1000f) / 1000f);

    [TestCase(RoofType.Single)]
    [TestCase(RoofType.Gable)]
    [TestCase(RoofType.Hip)]
    public void PlaneBoundariesUnits_EveryPlane_WindsWithAnUpwardNormal_BothAxisOrientations(
        RoofType type)
    {
        AssertAllUpward(RoofFrameGeometry.PlaneBoundariesUnits(WideInX, type,
            RoofRidgeAxis.Auto, OverhangMm, PitchDeg));
        AssertAllUpward(RoofFrameGeometry.PlaneBoundariesUnits(WideInZ, type,
            RoofRidgeAxis.Auto, OverhangMm, PitchDeg));
    }

    private static void AssertAllUpward(List<Vector3[]> boundaries)
    {
        foreach (var boundary in boundaries)
        {
            var cross = Vector3.Cross(boundary[1] - boundary[0], boundary[2] - boundary[0]);
            Assert.GreaterOrEqual(cross.y, 0f,
                "нормаль ската обязана смотреть вверх/наружу — иначе RoofPlaneMesh "
                + "построит грань, невидимую снаружи здания");
        }
    }

    [Test]
    public void PlaneBoundariesUnits_Hip_RidgeAxisForcedToTheShortSide_ShadowAreaMatchesRoofQuantities()
    {
        var footprint = new RoofFootprint(0f, 6000f, 0f, 10000f);

        var boundaries = RoofFrameGeometry.PlaneBoundariesUnits(footprint, RoofType.Hip,
            RoofRidgeAxis.X, 0f, PitchDeg);
        var frame = RoofPitchPlanes.Build(footprint, RoofType.Hip, RoofRidgeAxis.X, 0f);

        Assert.AreEqual(RoofQuantities.PlanAreaM2(frame), TotalPlanAreaM2(boundaries), 1e-4,
            "конёк, принудительно поставленный на короткую сторону (X 6000 короче Z 10000), "
            + "обязан давать ту же площадь в плане, что и RoofPitchPlanes.Build - у RoofPitchPlanes "
            + "конёк уже клампится к нулю (RoofPitchPlanesTests), а тут ridgeHalf уходит в минус и "
            + "трапеция ската превращается в самопересекающийся бант (review-construction.md #9)");
    }

    [Test]
    public void PlaneBoundariesUnits_ZeroPitch_CollapsesEveryPointToTheEaveHeight()
    {
        var boundaries = RoofFrameGeometry.PlaneBoundariesUnits(WideInX, RoofType.Gable,
            RoofRidgeAxis.Auto, OverhangMm, 0f);

        foreach (var boundary in boundaries)
            foreach (var corner in boundary)
                Assert.AreEqual(0f, corner.y, 1e-6f,
                    "нулевой уклон - конёк на той же высоте, что и карниз, плоскость "
                    + "вырождается в горизонтальную, а не куда-то улетает");
    }
}
