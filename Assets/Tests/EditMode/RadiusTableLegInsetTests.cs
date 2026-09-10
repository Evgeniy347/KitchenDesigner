using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

/// <summary>Крышка радиусного стола — «стадион»: две полуокружности радиусом
/// min(Ш, Г)/2 и прямая вставка между ними. Она строится в ФИЗИЧЕСКИХ
/// миллиметрах, потому что круглый контур, построенный в единичном пространстве
/// и растянутый корнем неравномерно, превращается в эллипс — стол был эллипсом
/// на любом размере. Отсюда же и посадка ножек, и раскладка UV.</summary>
public class RadiusTableLegInsetTests
{
    private const float ToleranceMM = 0.1f;

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

    private static RadiusTableElement Table(Vector3Int dimsMM, int legInsetMM)
    {
        var go = ElementFactory.CreateRadiusTable(dimsMM, "RT", Vector3.zero);
        var table = go.GetComponent<RadiusTableElement>()!;
        table.LegInsetMM = legInsetMM;
        return table;
    }

    private static float TabletopRadiusMM(Vector3Int dims)
        => Mathf.Min(dims.x, dims.z) * 0.5f;

    private static Vector2 FootprintMM(RadiusTableElement table, Vector3 worldPoint)
    {
        var offset = worldPoint - table.transform.position;
        return new Vector2(offset.x / AppConstants.MM_TO_UNITS,
            offset.z / AppConstants.MM_TO_UNITS);
    }

    private static float WorstLegClearanceMM(RadiusTableElement table)
    {
        var dims = table.DimensionsMM;
        float half = RadiusTableElement.LegCrossSectionMM * 0.5f;
        float worst = float.MaxValue;

        for (int i = 1; i <= 4; i++)
        {
            var leg = table.transform.Find("Leg" + i);
            Assert.IsNotNull(leg, "ножка Leg" + i + " должна существовать");
            var centre = FootprintMM(table, leg!.position);

            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    var corner = new Vector2(centre.x + sx * half, centre.y + sz * half);
                    float distance = RoundedRectProfile.SignedDistance(
                        corner, dims.x, dims.z, TabletopRadiusMM(dims));
                    worst = Mathf.Min(worst, -distance);
                }
        }

        return worst;
    }

    [Test]
    public void RadiusTable_Tabletop_EndCapsAreCircularArcs_OnANonSquareTable()
    {
        var dims = new Vector3Int(2000, 750, 1000);
        var table = Table(dims, 100);
        var mesh = table.GetComponent<MeshFilter>().sharedMesh;

        float radius = TabletopRadiusMM(dims);
        float arcCentreX = dims.x * 0.5f - radius;
        int onArc = 0;

        foreach (var vertex in mesh.vertices)
        {
            var p = FootprintMM(table, table.transform.TransformPoint(vertex));
            if (p.x < arcCentreX + ToleranceMM) continue;
            onArc++;
            Assert.AreEqual(radius, (p - new Vector2(arcCentreX, 0f)).magnitude, ToleranceMM,
                "торец крышки обязан быть ДУГОЙ ОКРУЖНОСТИ радиуса min(Ш, Г)/2. Круг, "
                + "построенный в единичном пространстве и растянутый корнем в 2:1, даёт "
                + "эллипс — на 2000×1000 точка под 60° уезжает на 433 мм вместо 500");
        }

        Assert.Greater(onArc, 4, "вершины торцевой дуги не найдены — сторож ослеп");
    }

    [Test]
    public void RadiusTable_Tabletop_ArcsMeetTheStraightRun_OnANonSquareTable()
    {
        var dims = new Vector3Int(2000, 750, 1000);
        var table = Table(dims, 100);
        var mesh = table.GetComponent<MeshFilter>().sharedMesh;

        float halfDepth = dims.z * 0.5f;
        float arcCentreX = dims.x * 0.5f - TabletopRadiusMM(dims);
        var tangent = new Vector2(arcCentreX, halfDepth);
        float best = float.MaxValue;

        foreach (var vertex in mesh.vertices)
        {
            var p = FootprintMM(table, table.transform.TransformPoint(vertex));
            best = Mathf.Min(best, (p - tangent).magnitude);
        }

        Assert.AreEqual(0f, best, ToleranceMM,
            "у стадиона дуга переходит в ПРЯМУЮ вставку в точке касания (500, 500) — "
            + "стол 2000×1000 обязан иметь там вершину. У эллипса такой точки нет: "
            + "глубины 500 он достигает только в x = 0");
    }

    [Test]
    public void RadiusTable_CapUv_SpansTheWholeFootprint_OnANonSquareTable()
    {
        var table = Table(new Vector3Int(2000, 750, 1000), 100);
        var mesh = table.GetComponent<MeshFilter>().sharedMesh;
        var uv = mesh.uv;
        var normals = mesh.normals;
        float minU = float.MaxValue, maxU = float.MinValue;
        float minV = float.MaxValue, maxV = float.MinValue;
        int caps = 0;

        for (int i = 0; i < uv.Length; i++)
        {
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

    [Test]
    public void RadiusTable_Root_KeepsUnitScale_SoChildrenAreNotScaledTwice()
    {
        var table = Table(new Vector3Int(2000, 750, 1000), 100);

        Assert.AreEqual(Vector3.one, table.transform.localScale,
            "крышка и ножки строятся в физических миллиметрах, поэтому корень обязан "
            + "остаться единичным: масштаб на корне растянул бы круглый торец в эллипс, "
            + "а детей — вдвое");

        var leg = table.transform.Find("Leg1")!;
        Assert.AreEqual(RadiusTableElement.LegCrossSectionMM * AppConstants.MM_TO_UNITS,
            leg.lossyScale.x, 1e-5f,
            "сечение ножки в мире равно её физическому сечению, а не произведению "
            + "масштабов корня и ребёнка");
    }

    [Test]
    public void RadiusTable_Legs_SitExactlyAtTheInsetFromTheTabletopEdge_OnAnElongatedTable()
    {
        var table = Table(new Vector3Int(2000, 750, 1000), 100);

        Assert.AreEqual(100f, WorstLegClearanceMM(table), ToleranceMM,
            "отступ ножки считается ОТ КРАЯ СТОЛЕШНИЦЫ: дальний угол ножки отстоит от "
            + "контура ровно на LegInsetMM. Посадку считали параметрически по эллипсу, "
            + "и на 2000×1000 углы ножек оказывались СНАРУЖИ настоящего контура");
    }

    [Test]
    public void RadiusTable_Legs_StayInside_WhenTheUserAsksForZeroInset()
    {
        var table = Table(new Vector3Int(2000, 750, 1000), 0);

        Assert.AreEqual((float)RadiusTableElement.MinLegInsetFromContourMM,
            WorstLegClearanceMM(table), ToleranceMM,
            "нулевая утопленность у радиусного стола запрещена: минимум держится "
            + "независимо от поля");
    }

    [Test]
    public void RadiusTable_Legs_StayInside_OnASquareTable()
    {
        var table = Table(new Vector3Int(900, 750, 900), 100);

        Assert.AreEqual(100f, WorstLegClearanceMM(table), ToleranceMM,
            "круглая крышка — тот же стадион при Ш == Г, вырождаться он не имеет права");
    }

    [Test]
    public void RadiusTable_Legs_GoDeeper_WhenTheUserAsksForALargerInset()
    {
        var table = Table(new Vector3Int(2000, 750, 1000),
            RadiusTableElement.MinLegInsetFromContourMM);
        float atMinimum = WorstLegClearanceMM(table);

        table.LegInsetMM = RadiusTableElement.MinLegInsetFromContourMM + 200;
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

    // ── Дефект: столешница радиусного стола молча выпадала из ведомости раскроя ──

    /// <summary>Столешница — прямоугольная заготовка ЛДСП толщиной
    /// TabletopThicknessMM, скругление появляется только при раскрое, как и у
    /// радиусной полки. Стол — композит (крышка + ножки), поэтому объявляет себя не
    /// через IsFlatBoardElement (это была бы вся коробка с ножками), а через
    /// ISpecificationParts — как AssembledFacadeElement.</summary>
    [Test]
    public void GetSpecParts_ReturnsOneTabletopPart_SheetSizedWithoutLegs()
    {
        var dims = new Vector3Int(2000, 750, 1000);
        var table = Table(dims, 100);

        var parts = new System.Collections.Generic.List<AssembledFacadeMesh.Part>(
            ((ISpecificationParts)table).GetSpecParts());

        Assert.AreEqual(1, parts.Count, "у столешницы одна деталь — сама крышка");
        Assert.AreEqual(new Vector3Int(dims.x, RadiusTableElement.TabletopThicknessMM, dims.z),
            parts[0].dimsMM,
            "заготовка крышки — это ширина/глубина стола и толщина плиты, а не высота стола с ногами");
    }

    /// <summary>Противоположный вход: другой габарит стола обязан дать другую
    /// заготовку — формула не возвращает одну и ту же деталь для любого стола.</summary>
    [Test]
    public void GetSpecParts_DifferentTableSize_DifferentTabletopBlank()
    {
        var small = ((ISpecificationParts)Table(new Vector3Int(900, 750, 900), 100))
            .GetSpecParts().GetEnumerator();
        var large = ((ISpecificationParts)Table(new Vector3Int(2000, 750, 1000), 100))
            .GetSpecParts().GetEnumerator();
        small.MoveNext();
        large.MoveNext();

        Assert.AreNotEqual(small.Current.dimsMM, large.Current.dimsMM);
    }

    [Test]
    public void Build_RadiusTable_IsIncludedInTheSpecificationAsOneAreaLine()
    {
        var table = Table(new Vector3Int(2000, 750, 1000), 100);

        var result = SpecificationManager.Build(new KitchenElement[] { table });

        Assert.AreEqual(1, result.lines.Count,
            "радиусный стол обязан дать ровно одну строку — заготовку крышки");
        Assert.AreEqual(SpecUnit.AreaM2, result.lines[0].unit);
    }
}
