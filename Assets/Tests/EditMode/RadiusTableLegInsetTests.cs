using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

/// <summary>Крышка овального стола — эллипс, а не «стадион» из габаритной рамки.
/// Отсюда и посадка ножек, и раскладка UV на ней.</summary>
public class RadiusTableLegInsetTests
{
    [SetUp]
    public void SetUp() => LogAssert.ignoreFailingMessages = true;

    [TearDown]
    public void TearDown()
    {
        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.DestroyImmediate(e.gameObject);
        PartRegistry.Clear();
        LogAssert.ignoreFailingMessages = false;
    }

    [Test]
    public void CapsuleTableMesh_CapUv_SpansTheWholeFootprint()
    {
        var mesh = CapsuleTableMesh.Build(1f, 0.03f, 1f);
        var uv = mesh.uv;
        var normals = mesh.normals;
        float minU = float.MaxValue, maxU = float.MinValue;
        float minV = float.MaxValue, maxV = float.MinValue;
        int caps = 0;
        for (int i = 0; i < uv.Length; i++)
        {
            // Только крышки: у боковин своя развёртка — u идёт по ДЛИНЕ контура
            // (периметр / ширина, то есть до π у круглой крышки), v — по толщине.
            // Смешивать их с крышкой в одном min/max нельзя.
            if (Mathf.Abs(normals[i].y) < 0.5f) continue;
            caps++;
            minU = Mathf.Min(minU, uv[i].x); maxU = Mathf.Max(maxU, uv[i].x);
            minV = Mathf.Min(minV, uv[i].y); maxV = Mathf.Max(maxV, uv[i].y);
        }

        Assert.Greater(caps, 0, "вершины крышек в меше не найдены");

        Assert.AreEqual(0f, minU, 1e-3f,
            "UV крышки занимали 0,25..0,75 — половину диапазона, и «вырез» по физическому "
            + "размеру плитки давал вдвое растянутый рисунок");
        Assert.AreEqual(1f, maxU, 1e-3f, "развёртка крышки занимает весь диапазон по u");
        Assert.AreEqual(0f, minV, 1e-3f, "и начинается с нуля по v");
        Assert.AreEqual(1f, maxV, 1e-3f, "и занимает весь диапазон по v");
    }

    private static float MillimetresInsideTheEllipse(Vector2 pointMM, float aMM, float bMM)
    {
        const int samples = 4096;
        float best = float.MaxValue;
        for (int i = 0; i < samples; i++)
        {
            float t = 2f * Mathf.PI * i / samples;
            var onContour = new Vector2(aMM * Mathf.Cos(t), bMM * Mathf.Sin(t));
            best = Mathf.Min(best, (onContour - pointMM).magnitude);
        }
        float nx = pointMM.x / aMM, ny = pointMM.y / bMM;
        return nx * nx + ny * ny <= 1f ? best : -best;
    }

    private static float WorstLegClearanceMM(RadiusTableElement table)
    {
        var dims = table.DimensionsMM;
        float a = dims.x * 0.5f, b = dims.z * 0.5f;
        float half = RadiusTableElement.LegCrossSectionMM * 0.5f;
        float worst = float.MaxValue;

        for (int i = 1; i <= 4; i++)
        {
            var leg = table.transform.Find("Leg" + i);
            Assert.IsNotNull(leg, "ножка Leg" + i + " должна существовать");
            var w = leg!.position - table.transform.position;
            float cx = w.x / AppConstants.MM_TO_UNITS;
            float cz = w.z / AppConstants.MM_TO_UNITS;

            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    worst = Mathf.Min(worst, MillimetresInsideTheEllipse(
                        new Vector2(cx + sx * half, cz + sz * half), a, b));
        }
        return worst;
    }

    [Test]
    public void RadiusTable_Legs_StayAtLeastMinimumInsetInside_OnAnElongatedTable()
    {
        var go = ElementFactory.CreateRadiusTable(new Vector3Int(2000, 750, 1000), "RT", Vector3.zero);
        var table = go.GetComponent<RadiusTableElement>()!;
        table.LegInsetMM = 100;

        Assert.GreaterOrEqual(WorstLegClearanceMM(table),
            RadiusTableLegs.MinLegInsetFromContourMM - 1f,
            "посадку ножек считали по «стадиону», построенному из габаритной рамки, а крышка "
            + "у овала — эллипс: на 2000×1000 углы ножек оказывались СНАРУЖИ столешницы");
    }

    [Test]
    public void RadiusTable_Legs_StayInside_WhenTheUserAsksForZeroInset()
    {
        var go = ElementFactory.CreateRadiusTable(new Vector3Int(2000, 750, 1000), "RT", Vector3.zero);
        var table = go.GetComponent<RadiusTableElement>()!;
        table.LegInsetMM = 0;

        Assert.GreaterOrEqual(WorstLegClearanceMM(table),
            RadiusTableLegs.MinLegInsetFromContourMM - 1f,
            "нулевая утопленность у овала запрещена: минимум держится независимо от поля");
    }

    [Test]
    public void RadiusTable_Legs_StayInside_OnASquareTable()
    {
        var go = ElementFactory.CreateRadiusTable(new Vector3Int(900, 750, 900), "RT", Vector3.zero);
        var table = go.GetComponent<RadiusTableElement>()!;
        table.LegInsetMM = 100;

        Assert.GreaterOrEqual(WorstLegClearanceMM(table),
            RadiusTableLegs.MinLegInsetFromContourMM - 1f,
            "круглая крышка — та же формула при a == b, вырождаться она не имеет права");
    }

    [Test]
    public void RadiusTable_Legs_GoDeeper_WhenTheUserAsksForALargerInset()
    {
        var go = ElementFactory.CreateRadiusTable(new Vector3Int(2000, 750, 1000), "RT", Vector3.zero);
        var table = go.GetComponent<RadiusTableElement>()!;

        table.LegInsetMM = RadiusTableLegs.MinLegInsetFromContourMM;
        float atMinimum = WorstLegClearanceMM(table);
        table.LegInsetMM = RadiusTableLegs.MinLegInsetFromContourMM + 200;
        float deeper = WorstLegClearanceMM(table);

        Assert.Greater(deeper, atMinimum + 100f,
            "порог — это ПОЛ, а не фиксированная посадка: большее значение поля обязано "
            + "утопить ножки глубже");
    }

    [Test]
    public void Table_Legs_NeverLeaveTheRectangularTabletop_AtZeroInset()
    {
        var dims = new Vector3Int(2000, 750, 1000);
        var go = ElementFactory.CreateTable(dims, "T", Vector3.zero);
        var table = go.GetComponent<TableElement>()!;
        table.LegInsetMM = 0;

        float half = TableElement.LegCrossSectionMM * 0.5f;
        for (int i = 1; i <= 4; i++)
        {
            var leg = table.transform.Find("Leg" + i)!;
            var w = leg.position - table.transform.position;
            float cx = w.x / AppConstants.MM_TO_UNITS;
            float cz = w.z / AppConstants.MM_TO_UNITS;
            Assert.LessOrEqual(Mathf.Abs(cx) + half, dims.x * 0.5f + 1f,
                "у прямоугольного стола контур И ЕСТЬ габаритная рамка: ножка при нулевой "
                + "утопленности встаёт заподлицо и наружу не выходит — правило про 50 мм "
                + "тут не нужно");
            Assert.LessOrEqual(Mathf.Abs(cz) + half, dims.z * 0.5f + 1f);
        }
    }
}
