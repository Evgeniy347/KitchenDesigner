using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

/// <summary>Табуретка — ОДИН тип на обе формы: квадрат и круг различает
/// параметр <c>CornerRadiusMM</c>, а не отдельный класс (в отличие от пары
/// TableElement / RadiusTableElement).
///
/// Главное, что здесь удерживается, — решение строить сиденье в ФИЗИЧЕСКОМ
/// пространстве: <c>localScale</c> корня остаётся единичным, физические
/// габариты отдаёт <c>EffectiveScale</c>, а дети расставлены в мировых
/// единицах. Радиусный стол сделан наоборот, и ему это можно: растянутая
/// капсула — всё ещё капсула. Табуретке нельзя — её радиус задан в
/// миллиметрах, и растяжение корнем превратило бы окружности в эллипсы, а
/// детей — в дважды отмасштабированные.</summary>
public class StoolElementTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

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

    private StoolElement Stool(int width, int height, int depth, int cornerRadiusMM,
        string name = "Табуретка-1")
    {
        var go = ElementFactory.CreateStool(new Vector3Int(width, height, depth),
            cornerRadiusMM, name, Vector3.zero);
        _spawned.Add(go);
        var stool = go.GetComponent<StoolElement>();
        Assert.IsNotNull(stool, "фабрика обязана вернуть StoolElement");
        return stool!;
    }

    private static float SignedDistanceToSeat(Vector2 point, float width, float depth, float radius)
    {
        float qx = Mathf.Abs(point.x) - (width * 0.5f - radius);
        float qy = Mathf.Abs(point.y) - (depth * 0.5f - radius);
        var q = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f));
        return q.magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
    }

    private static StoolElement DefaultStool(StoolElementTests owner) =>
        owner.Stool(StoolElement.DefaultWidthMM, StoolElement.DefaultHeightMM,
            StoolElement.DefaultDepthMM, 0);

    [Test]
    public void Stool_Defaults_AreTheAgreedThreeHundredSixtyByFourFiftyByThreeHundredSixty()
    {
        var stool = DefaultStool(this);

        Assert.AreEqual(new Vector3Int(360, 450, 360), stool.DimensionsMM,
            "габариты табуретки согласованы с пользователем: 360 x 450 x 360 мм");
        Assert.AreEqual(30, StoolElement.SeatThicknessMM, "сиденье 30 мм");
        Assert.AreEqual(40, StoolElement.LegCrossSectionMM, "ножка 40 x 40 мм");
        Assert.AreEqual(30, StoolElement.LegInsetMM, "отступ ножки от края 30 мм");
    }

    [Test]
    public void CornerRadiusMM_AboveHalfTheSmallerSide_IsClampedToIt()
    {
        var stool = Stool(360, 450, 360, 0);

        stool.CornerRadiusMM = 1000;

        Assert.AreEqual(180, stool.CornerRadiusMM,
            "радиус ограничен половиной меньшей стороны: больше половины — это уже не "
            + "скругление, а вылет контура наружу. 360/2 = 180 мм");
    }

    [Test]
    public void CornerRadiusMM_Negative_IsClampedToZero_NotToOne()
    {
        var stool = Stool(360, 450, 360, 0);

        stool.CornerRadiusMM = -50;

        Assert.AreEqual(0, stool.CornerRadiusMM,
            "у табуретки НОЛЬ — законное значение (квадратная табуретка). "
            + "RadialShelfElement.ClampCornerRadius зажимает по минимуму в 1 — копировать "
            + "это сюда нельзя");
    }

    [Test]
    public void CornerRadiusMM_MaximumOnASquareFootprint_MakesTheStoolFullyRound()
    {
        var stool = Stool(360, 450, 360, 180);

        Assert.AreEqual(StoolElement.ShapeRound, stool.ShapeName,
            "радиус = min(Ш, Г)/2 на квадратном следе — полностью круглая табуретка");
    }

    [Test]
    public void CornerRadiusMM_Zero_IsASquareStool()
    {
        Assert.AreEqual(StoolElement.ShapeSquare, Stool(360, 450, 360, 0).ShapeName,
            "ноль — квадратная табуретка с острыми углами");
    }

    [Test]
    public void CornerRadiusMM_BetweenTheBounds_IsARoundedStool()
    {
        Assert.AreEqual(StoolElement.ShapeRounded, Stool(360, 450, 360, 60).ShapeName,
            "промежуточный радиус — скруглённые углы, но не круг");
    }

    [Test]
    public void ApplyDimensions_ShrinkingTheStool_ReClampsARadiusThatWasLegalBefore()
    {
        var stool = Stool(600, 450, 600, 300);
        Assume.That(stool.CornerRadiusMM, Is.EqualTo(300),
            "исходный радиус обязан быть законным, иначе тест ничего не ловит");

        stool.DimensionsMM = new Vector3Int(360, 450, 360);

        Assert.AreEqual(180, stool.CornerRadiusMM,
            "ресайз способен сделать НЕЗАКОННЫМ радиус, который был законным: 300 мм "
            + "законны при следе 600, но при 360 максимум уже 180. Пересжатия в сеттере "
            + "мало — оно обязано быть и в ApplyDimensions");
    }

    [Test]
    public void Stool_RootTransform_StaysAtUnitScale()
    {
        var stool = Stool(600, 450, 360, 100);

        Assert.AreEqual(Vector3.one, stool.transform.localScale,
            "сиденье строится в физических единицах, поэтому корень НЕ масштабируется. "
            + "Стоит корню получить габариты — и дети (ножки, уже расставленные в мировых "
            + "единицах) отмасштабируются дважды");
    }

    [Test]
    public void Stool_BoundingBox_MatchesDimensions_DespiteTheUnitRootScale()
    {
        var stool = Stool(600, 450, 360, 100);

        var v = stool.GetVertices();
        Vector3 min = v[0], max = v[0];
        for (int i = 1; i < v.Length; i++)
        {
            min = Vector3.Min(min, v[i]);
            max = Vector3.Max(max, v[i]);
        }

        Assert.AreEqual(0.6f, max.x - min.x, 1e-4f,
            "габариты для снапа и валидации берутся из EffectiveScale, а не из localScale");
        Assert.AreEqual(0.45f, max.y - min.y, 1e-4f, "то же по высоте");
        Assert.AreEqual(0.36f, max.z - min.z, 1e-4f, "то же по глубине");
    }

    [Test]
    public void Stool_ResizedToANonSquareFootprint_KeepsTheCornerArcCircular_NotElliptical()
    {
        var stool = Stool(360, 450, 360, 120);
        stool.DimensionsMM = new Vector3Int(600, 450, 360);

        var mesh = stool.GetComponent<MeshFilter>()!.sharedMesh;
        Assert.IsNotNull(mesh, "у табуретки обязан быть меш сиденья");

        float radius = 120 * AppConstants.MM_TO_UNITS;
        var centre = new Vector2(-0.6f * 0.5f + radius, -0.36f * 0.5f + radius);
        var scale = stool.transform.lossyScale;

        int checkedPoints = 0;
        foreach (var local in mesh!.vertices)
        {
            var p = new Vector2(local.x * scale.x, local.z * scale.z);
            if (p.x > centre.x + 1e-4f || p.y > centre.y + 1e-4f) continue;
            checkedPoints++;
            Assert.AreEqual(radius, (p - centre).magnitude, 1e-4f,
                "угол обязан остаться ДУГОЙ ОКРУЖНОСТИ физического радиуса 120 мм. Если "
                + "строить сиденье в единичном пространстве и растягивать корнем — как "
                + "делает радиусный стол, — на следе 600x360 угол станет эллиптическим");
        }

        Assert.GreaterOrEqual(checkedPoints, RoundedRectProfile.DefaultSegments,
            "вершин углового скругления не нашлось — тест позеленел бы, ничего не проверив");
    }

    [Test]
    public void Stool_Legs_AreFourAndStandUnderTheSeat_InWorldUnits()
    {
        var stool = Stool(600, 450, 360, 180);

        var legs = new List<Transform>();
        foreach (Transform child in stool.transform)
            if (child.name.StartsWith("Leg")) legs.Add(child);

        Assert.AreEqual(4, legs.Count, "у табуретки четыре ножки");

        float legHalf = StoolElement.LegCrossSectionMM * 0.5f * AppConstants.MM_TO_UNITS;
        foreach (var leg in legs)
        {
            Assert.AreEqual(StoolElement.LegCrossSectionMM * AppConstants.MM_TO_UNITS,
                leg.lossyScale.x, 1e-4f,
                "ножка 40 x 40 мм в МИРОВЫХ единицах: при единичном корне localScale и есть "
                + "мировой размер, а двойное масштабирование выдало бы 40 x 40 от 600 мм");

            var p = leg.localPosition;
            var farCorner = new Vector2(Mathf.Abs(p.x) + legHalf, Mathf.Abs(p.z) + legHalf);
            Assert.LessOrEqual(SignedDistanceToSeat(farCorner, 0.6f, 0.36f, 0.18f), 1e-4f,
                "дальний угол ножки обязан остаться под сиденьем: у капсулы углы следа "
                + "срезаны, и прямоугольная расстановка вынесла бы ножку наружу");
        }
    }

    [Test]
    public void Stool_DecorSurface_IsWidthByDepth_InPhysicalMillimetres()
    {
        var stool = Stool(600, 450, 360, 100);

        Assert.AreEqual(new Vector2Int(600, 360), stool.DecorSurfaceMM,
            "ProfileExtrusionMesh разворачивает ВСЁ в системе (ширина, ГЛУБИНА), поэтому "
            + "поверхность декора — это (x, z), а не базовые (x, y) стоячей панели: иначе "
            + "декор на сиденье растянется, как когда-то на столешнице. "
            + "DecorSurfaceUvTests проверяет лишь ФАКТ объявления, но не его правильность");
    }

    [Test]
    public void Stool_SaveLoadRoundTrip_KeepsANonDefaultCornerRadius()
    {
        var stool = Stool(600, 450, 360, 137, "Табуретка-RT");

        var data = ElementCapture.FromElement(stool);
        var restored = JsonUtility.FromJson<ElementData>(JsonUtility.ToJson(data));

        Assert.IsTrue(restored.isStool,
            "без флага типа табуретка загрузится обычной доской — молчаливая потеря");
        Assert.AreEqual(137, restored.cornerRadius,
            "радиус — единственный параметр формы табуретки; потерять его значит "
            + "загрузить круглую табуретку квадратной");
        Assert.AreEqual(new[] { 600, 450, 360 }, restored.dimensionsMM,
            "габариты обязаны пережить круг вместе с радиусом: радиус зажимается по ним");
    }

    [Test]
    public void Stool_SaveLoadRoundTrip_KeepsAZeroCornerRadius()
    {
        var stool = Stool(360, 450, 360, 0, "Табуретка-RT0");

        var data = ElementCapture.FromElement(stool);
        var restored = JsonUtility.FromJson<ElementData>(JsonUtility.ToJson(data));

        Assert.AreEqual(0, restored.cornerRadius,
            "ноль — законное значение, а поле cornerRadius в ElementData инициализировано "
            + "радиусом полки (200): забыть записать его для табуретки значит превратить "
            + "квадратную табуретку в скруглённую при загрузке");
    }

    [Test]
    public void Stool_Duplicate_ComesOutAStool_WithTheSameRadius()
    {
        var stool = Stool(600, 450, 360, 137, "Табуретка-D");

        var copyGo = ElementFactory.Instance.Duplicate(stool);
        _spawned.Add(copyGo);
        var copy = copyGo.GetComponent<StoolElement>();

        Assert.IsNotNull(copy, "пропуск в реестре дублирования молча возвращает обычную доску");
        Assert.AreEqual(137, copy!.CornerRadiusMM, "копия обязана повторить форму оригинала");
    }

    [Test]
    public void Stool_TypeForBulkSelector_IsStool_NotBoard()
    {
        var stool = DefaultStool(this);

        Assert.AreEqual("stool", KitchenDesigner.Core.Bulk.ElementSelector.TypeOf(stool),
            "забытый тип не отказывает, а притворяется доской и попадает в чужую выборку");
    }

    [Test]
    public void Stool_DisplayTypeName_IsTheStool()
    {
        Assert.AreEqual("Табуретка", DefaultStool(this).DisplayTypeName,
            "подпись типа в заголовке окна свойств — данные элемента, а не лестница в UI");
    }
}
