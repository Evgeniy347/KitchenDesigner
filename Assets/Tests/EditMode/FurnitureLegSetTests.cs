using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

/// <summary>Табуретка, стол и радиусный стол делят один <c>LegSet</c> — общий
/// жизненный цикл четырёх ножек. Общий класс, расставляющий детали по массиву,
/// может незаметно ПЕРЕСТАВИТЬ их: все четыре ножки одинакового размера, все в
/// одном материале, и ни один снимок, ни один габаритный тест такого не увидит.
/// Поэтому порядок здесь закреплён поимённо.
///
/// Порядки РАЗНЫЕ и обязаны такими остаться:
/// <c>RoundedRectSeating.LegCentres</c> (табуретка и радиусный стол) обходит след
/// по кругу — задняя пара идёт (+X,+Z), затем (−X,+Z); прямоугольный стол считает
/// свои позиции сам и обходит их «змейкой» — (−X,+Z), затем (+X,+Z). Унификация
/// порядка «по очевидному шаблону» переименует Leg3 и Leg4 местами, и материал,
/// назначенный на конкретную ножку, уедет на другую.
///
/// Второй сторож — сечение ножки. Стол держит детей в НОРМАЛИЗОВАННОМ
/// пространстве (localScale корня = физический габарит), табуретка и радиусный
/// стол — в физическом (localScale = 1). Перепутать систему координат при
/// переезде на общий LegSet — значит получить ножку, растянутую вместе с
/// корпусом: на неквадратном следе квадрат в сечении превращается в прямоугольник.</summary>
public class FurnitureLegSetTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    private const float ToleranceU = 1e-4f;

    [SetUp]
    public void SetUp() => LogAssert.ignoreFailingMessages = true;

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
        LogAssert.ignoreFailingMessages = false;
    }

    private GameObject Spawn(GameObject go)
    {
        _spawned.Add(go);
        return go;
    }

    private static Transform Leg(KitchenElement element, int oneBasedIndex)
    {
        var leg = element.transform.Find("Leg" + oneBasedIndex);
        Assert.IsNotNull(leg, "ножка Leg" + oneBasedIndex + " должна существовать");
        return leg!;
    }

    private static void AssertQuadrant(KitchenElement element, int oneBasedIndex,
        float signX, float signZ, string why)
    {
        var p = Leg(element, oneBasedIndex).position;
        Assert.AreEqual(signX, Mathf.Sign(p.x), $"Leg{oneBasedIndex}: знак X. {why}");
        Assert.AreEqual(signZ, Mathf.Sign(p.z), $"Leg{oneBasedIndex}: знак Z. {why}");
    }

    private static void AssertLegsFormOneSymmetricRectangle(KitchenElement element)
    {
        float x = Mathf.Abs(Leg(element, 1).position.x);
        float z = Mathf.Abs(Leg(element, 1).position.z);
        for (int i = 2; i <= 4; i++)
        {
            var p = Leg(element, i).position;
            Assert.AreEqual(x, Mathf.Abs(p.x), ToleranceU,
                $"Leg{i} стоит на другом расстоянии от оси, чем Leg1 — след уже не прямоугольник");
            Assert.AreEqual(z, Mathf.Abs(p.z), ToleranceU, $"Leg{i}: то же по Z");
            Assert.AreEqual(Leg(element, 1).position.y, p.y, ToleranceU,
                $"Leg{i} висит на другой высоте, чем Leg1");
        }
        Assert.Greater(x, 0f, "ножки не могут стоять на оси симметрии");
        Assert.Greater(z, 0f, "ножки не могут стоять на оси симметрии");
    }

    private static void AssertLegCrossSectionIsSquare(KitchenElement element, int crossSectionMM)
    {
        float expected = crossSectionMM * AppConstants.MM_TO_UNITS;
        for (int i = 1; i <= 4; i++)
        {
            var s = Leg(element, i).lossyScale;
            Assert.AreEqual(expected, s.x, ToleranceU,
                $"Leg{i}: сечение по X. Ножка живёт в системе координат корня — "
                + "перепутать нормализованные координаты с физическими значит растянуть её "
                + "вместе с корпусом");
            Assert.AreEqual(expected, s.z, ToleranceU, $"Leg{i}: сечение по Z");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  Порядок ножек: своим у каждого типа, и он поимённый
    // ═══════════════════════════════════════════════════════════════

    [Test]
    public void Stool_Legs_GoRoundTheFootprint_StartingAtTheNearLeftCorner()
    {
        var go = Spawn(ElementFactory.CreateStool(new Vector3Int(360, 450, 360), 0,
            "Табуретка-1", Vector3.zero));
        var stool = go.GetComponent<StoolElement>()!;

        const string why = "порядок RoundedRectSeating.LegCentres: обход по кругу";
        AssertQuadrant(stool, 1, -1f, -1f, why);
        AssertQuadrant(stool, 2, +1f, -1f, why);
        AssertQuadrant(stool, 3, +1f, +1f, why);
        AssertQuadrant(stool, 4, -1f, +1f, why);
        AssertLegsFormOneSymmetricRectangle(stool);
    }

    [Test]
    public void RadiusTable_Legs_GoRoundTheFootprint_StartingAtTheNearLeftCorner()
    {
        var go = Spawn(ElementFactory.CreateRadiusTable(new Vector3Int(1600, 750, 900),
            "РадСтол-1", Vector3.zero));
        var table = go.GetComponent<RadiusTableElement>()!;

        const string why = "порядок RoundedRectSeating.LegCentres: обход по кругу";
        AssertQuadrant(table, 1, -1f, -1f, why);
        AssertQuadrant(table, 2, +1f, -1f, why);
        AssertQuadrant(table, 3, +1f, +1f, why);
        AssertQuadrant(table, 4, -1f, +1f, why);
        AssertLegsFormOneSymmetricRectangle(table);
    }

    [Test]
    public void Table_Legs_GoRowByRow_NotRoundTheFootprint()
    {
        var go = Spawn(ElementFactory.CreateTable(new Vector3Int(1600, 750, 900),
            "Стол-1", Vector3.zero));
        var table = go.GetComponent<TableElement>()!;

        const string why = "у прямоугольного стола СВОЙ порядок — «змейкой», "
            + "передняя пара, затем задняя; Leg3 у него слева, а не справа";
        AssertQuadrant(table, 1, -1f, -1f, why);
        AssertQuadrant(table, 2, +1f, -1f, why);
        AssertQuadrant(table, 3, -1f, +1f, why);
        AssertQuadrant(table, 4, +1f, +1f, why);
        AssertLegsFormOneSymmetricRectangle(table);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Сечение ножки квадратно и на неквадратном следе
    // ═══════════════════════════════════════════════════════════════

    [Test]
    public void Table_LegCrossSection_StaysSquare_OnAnElongatedFootprint()
    {
        var go = Spawn(ElementFactory.CreateTable(new Vector3Int(2400, 750, 800),
            "Стол-2", Vector3.zero));
        AssertLegCrossSectionIsSquare(go.GetComponent<TableElement>()!,
            TableElement.LegCrossSectionMM);
    }

    [Test]
    public void RadiusTable_LegCrossSection_StaysSquare_OnAnElongatedFootprint()
    {
        var go = Spawn(ElementFactory.CreateRadiusTable(new Vector3Int(2400, 750, 800),
            "РадСтол-2", Vector3.zero));
        AssertLegCrossSectionIsSquare(go.GetComponent<RadiusTableElement>()!,
            RadiusTableElement.LegCrossSectionMM);
    }

    [Test]
    public void Stool_LegCrossSection_StaysSquare_OnAnElongatedFootprint()
    {
        var go = Spawn(ElementFactory.CreateStool(new Vector3Int(520, 450, 320), 0,
            "Табуретка-2", Vector3.zero));
        AssertLegCrossSectionIsSquare(go.GetComponent<StoolElement>()!,
            StoolElement.LegCrossSectionMM);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Крышку стола держит ССЫЛКА, а не индекс в списке детей
    // ═══════════════════════════════════════════════════════════════

    [Test]
    public void Table_DecorRenderer_SurvivesARebuild_AndIsNeverALeg()
    {
        var go = Spawn(ElementFactory.CreateTable(new Vector3Int(1600, 750, 900),
            "Стол-3", Vector3.zero));
        var table = go.GetComponent<TableElement>()!;

        table.LegInsetMM = 150;
        table.ApplyDimensions();

        var decor = ((KitchenElement)table).DecorRenderer;
        Assert.IsNotNull(decor, "крышка обязана оставаться поверхностью под декор");
        Assert.AreEqual("Tabletop", decor!.gameObject.name,
            "крышку нельзя искать индексом в списке детей: ошибись на единицу — и "
            + "текстура уедет на ножку, молча и без исключения");
        Assert.AreEqual(4, CountLegs(table), "ножек по-прежнему четыре, а не пять");
    }

    private static int CountLegs(KitchenElement element)
    {
        int n = 0;
        foreach (Transform child in element.transform)
            if (child.name.StartsWith("Leg")) n++;
        return n;
    }
}
