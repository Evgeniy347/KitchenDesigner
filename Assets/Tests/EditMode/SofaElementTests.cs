using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Bulk;

/// <summary>Диван собран по фотографии пользователя: у него НЕТ подлокотников,
/// вместо них подушки, и подушек ровно четыре — две стоят на спинке, две лежат
/// по бокам сиденья.
///
/// Тип самостоятельный, а не наследник табуретки или стула: каждый реестр
/// проекта ветвится через <c>is XxxElement</c>, и подкласс молча проваливается
/// сквозь них все — ElementDuplicators выдал бы копию дивана стулом и потерял
/// бы подушки.
///
/// Как и вся эта семья мебели, диван строится в ФИЗИЧЕСКОМ пространстве:
/// <c>localScale</c> корня единичный, габариты отдаёт <c>EffectiveScale</c>,
/// дети расставлены в мировых единицах. Скругление задано в миллиметрах, и
/// растяжение корнем превратило бы окружности в эллипсы — на следе 2000x900 это
/// видно сразу.</summary>
public class SofaElementTests
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

    private SofaElement Sofa(int width, int height, int depth, int cornerRadiusMM,
        int seatHeightMM, string name = "Диван-1")
    {
        var go = ElementFactory.CreateSofa(new Vector3Int(width, height, depth),
            cornerRadiusMM, seatHeightMM, name, Vector3.zero);
        _spawned.Add(go);
        var sofa = go.GetComponent<SofaElement>();
        Assert.IsNotNull(sofa, "фабрика обязана вернуть SofaElement");
        return sofa!;
    }

    private SofaElement DefaultSofa() => Sofa(SofaElement.DefaultWidthMM,
        SofaElement.DefaultHeightMM, SofaElement.DefaultDepthMM,
        SofaElement.DefaultCornerRadiusMM, SofaElement.DefaultSeatHeightMM);

    private static Transform Child(SofaElement sofa, string name)
    {
        var child = sofa.transform.Find(name);
        Assert.IsNotNull(child, "у дивана обязан быть ребёнок " + name);
        return child!;
    }

    [Test]
    public void Sofa_Defaults_AreTheAgreedTwoThousandByEightHundredByNineHundred()
    {
        var sofa = DefaultSofa();

        Assert.AreEqual(new Vector3Int(2000, 800, 900), sofa.DimensionsMM,
            "ширина 2000 и глубина 900 заданы пользователем и не обсуждаются; высота 800 "
            + "снята с фотографии: 360 мм глухого основания плюс 440 мм спинки");
        Assert.AreEqual(360, SofaElement.DefaultSeatHeightMM,
            "верх основания на 360 мм: промер правого края фотографии (он почти в профиль, "
            + "и перспектива там врёт меньше всего) дал отношение цоколя к боковой подушке "
            + "1,6 к 1 — на 400 подушка выходила вдвое тоньше цоколя и читалась бугорком");
    }

    [Test]
    public void Sofa_ElementConstants_AreTheOnesTheLayoutUses_NotACopy()
    {
        Assert.AreEqual(SofaLayout.DefaultWidthMM, SofaElement.DefaultWidthMM,
            "умолчания живут в SofaLayout, потому что быстрый путь (dotnet) не видит "
            + "Core/Elements; элемент обязан их ПЕРЕИЗЛУЧАТЬ, а не заводить свою копию — "
            + "разошедшись, сайдбар и MCP заведут разные диваны");
        Assert.AreEqual(SofaLayout.DefaultHeightMM, SofaElement.DefaultHeightMM,
            "то же по высоте");
        Assert.AreEqual(SofaLayout.DefaultDepthMM, SofaElement.DefaultDepthMM,
            "то же по глубине");
        Assert.AreEqual(SofaLayout.DefaultSeatHeightMM, SofaElement.DefaultSeatHeightMM,
            "то же по высоте основания");
        Assert.AreEqual(SofaLayout.DefaultCornerRadiusMM, SofaElement.DefaultCornerRadiusMM,
            "то же по скруглению");
    }

    [Test]
    public void Sofa_HasFourCushions_AndNoArmrests()
    {
        var sofa = DefaultSofa();

        Child(sofa, SofaLayout.ArmCushionLeftName);
        Child(sofa, SofaLayout.ArmCushionRightName);
        Child(sofa, SofaLayout.BackCushionLeftName);
        Child(sofa, SofaLayout.BackCushionRightName);
        Child(sofa, SofaLayout.BackRailName);

        Assert.AreEqual(SofaLayout.CushionCount + 1, sofa.transform.childCount,
            "четыре подушки и одна спинка-полка — и больше ничего: подлокотников у этого "
            + "дивана нет, их роль играют боковые подушки");
    }

    [Test]
    public void CornerRadiusMM_AboveHalfTheSmallerSide_IsClampedToIt()
    {
        var sofa = DefaultSofa();

        sofa.CornerRadiusMM = 5000;

        Assert.AreEqual(450, sofa.CornerRadiusMM,
            "радиус основания ограничен половиной меньшей стороны: 900/2 = 450 мм. Больше "
            + "половины — это уже не скругление, а вылет контура наружу");
    }

    [Test]
    public void SeatHeightMM_AboveTheHeightLeftForTheBack_IsClampedToIt()
    {
        var sofa = DefaultSofa();

        sofa.SeatHeightMM = 5000;

        Assert.AreEqual(SofaElement.DefaultHeightMM - SofaElement.MinBackrestHeightMM,
            sofa.SeatHeightMM,
            "спинке всегда остаётся не меньше 200 мм: основание во всю высоту — это уже "
            + "не диван, а тумба");
    }

    [Test]
    public void SeatHeightMM_BelowTheMinimumBase_IsClampedToIt()
    {
        var sofa = DefaultSofa();

        sofa.SeatHeightMM = 10;

        Assert.AreEqual(SofaElement.MinBaseHeightMM, sofa.SeatHeightMM,
            "основание тоньше 150 мм перестаёт читаться цоколем, а боковые подушки "
            + "оказываются на полу");
    }

    [Test]
    public void Sofa_ResizedToANonSquareFootprint_KeepsTheCornerArcCircular_NotElliptical()
    {
        var sofa = Sofa(1200, 800, 1200, 200, 400);
        sofa.DimensionsMM = new Vector3Int(2000, 800, 900);

        var mesh = sofa.GetComponent<MeshFilter>()!.sharedMesh;
        Assert.IsNotNull(mesh, "у дивана обязан быть меш основания");

        float radius = 200 * AppConstants.MM_TO_UNITS;
        var centre = new Vector2(-2.0f * 0.5f + radius, -0.9f * 0.5f + radius);
        var scale = sofa.transform.lossyScale;

        int checkedPoints = 0;
        foreach (var local in mesh!.vertices)
        {
            var p = new Vector2(local.x * scale.x, local.z * scale.z);
            if (p.x > centre.x + 1e-4f || p.y > centre.y + 1e-4f) continue;
            checkedPoints++;
            Assert.AreEqual(radius, (p - centre).magnitude, 1e-4f,
                "угол основания обязан остаться ДУГОЙ ОКРУЖНОСТИ физического радиуса 200 мм. "
                + "Если строить контур в единичном пространстве и растягивать корнем — как "
                + "делает радиусный стол, — на следе 2000x900 угол станет эллиптическим");
        }

        Assert.GreaterOrEqual(checkedPoints, RoundedRectProfile.DefaultSegments,
            "вершин углового скругления не нашлось — тест позеленел бы, ничего не проверив");
    }

    [Test]
    public void SofaCushion_OnANonSquareSofa_KeepsItsCornerFilletAtItsPhysicalRadius()
    {
        var sofa = Sofa(2000, 800, 900, 120, 400);

        var cushion = Child(sofa, SofaLayout.BackCushionRightName);
        var mesh = cushion.GetComponent<MeshFilter>()!.sharedMesh;
        Assert.IsNotNull(mesh, "у спинной подушки обязан быть свой меш");

        var box = CushionBox(SofaLayout.BackCushionRightName);
        var surface = new CushionSurface(box.LocalSizeMM * AppConstants.MM_TO_UNITS,
            box.RadiusMM * AppConstants.MM_TO_UNITS);

        int checkedPoints = 0;
        foreach (var local in mesh!.vertices)
        {
            var innerLocal = surface.ClampToInnerBox(local);
            if (Mathf.Abs((local - innerLocal).magnitude - surface.Radius) > 1e-5f) continue;

            checkedPoints++;
            var world = cushion.TransformPoint(local);
            var innerWorld = cushion.TransformPoint(innerLocal);
            Assert.AreEqual(surface.Radius, (world - innerWorld).magnitude, 1e-4f,
                "подушка теперь скруглена по ВСЕМ трём осям, и её скругление задано в "
                + "физических миллиметрах: на шве каждая точка стоит ровно на радиусе от "
                + "внутренней коробки. Мерим в МИРОВЫХ координатах, а не в локальных "
                + "вершинах меша: локально расстояние останется прежним и тогда, когда "
                + "корень растянет всю мебель, и тест был бы слеп ровно к тому дефекту, "
                + "ради которого написан");
        }

        Assert.GreaterOrEqual(checkedPoints, CushionSurface.DefaultArcSegments * 4,
            "вершин на шве скругления не нашлось — тест позеленел бы, ничего не проверив");
    }

    private static FurniturePartBox CushionBox(string name)
    {
        foreach (var box in SofaLayout.Cushions(new Vector3Int(2000, 800, 900), 400))
            if (box.Name == name) return box;
        Assert.Fail("в раскладке нет подушки " + name);
        return default;
    }

    [Test]
    public void Sofa_RootStaysUnitScaled_WhileTheVerticesSpanThePhysicalSize()
    {
        var sofa = DefaultSofa();

        Assert.AreEqual(Vector3.one, sofa.transform.localScale,
            "корень остаётся единичным: иначе дети масштабировались бы дважды");

        var v = sofa.GetVertices();
        Vector3 min = v[0], max = v[0];
        for (int i = 1; i < v.Length; i++)
        {
            min = Vector3.Min(min, v[i]);
            max = Vector3.Max(max, v[i]);
        }

        Assert.AreEqual(2.0f, max.x - min.x, 1e-4f,
            "габариты для снапа и валидации берутся из EffectiveScale, а не из localScale");
        Assert.AreEqual(0.8f, max.y - min.y, 1e-4f, "то же по высоте");
        Assert.AreEqual(0.9f, max.z - min.z, 1e-4f, "то же по глубине");
    }

    [Test]
    public void Sofa_Cushions_AreBuiltInWorldMillimetres_NotScaledByTheRoot()
    {
        var sofa = Sofa(2000, 800, 900, 120, 400);

        var arm = Child(sofa, SofaLayout.ArmCushionRightName);
        Assert.AreEqual(Vector3.one, arm.localScale,
            "подушка не масштабируется: её размер уже заложен в собственный меш, и "
            + "масштаб поверх него — это второе умножение");

        var bounds = arm.GetComponent<MeshFilter>()!.sharedMesh.bounds;
        Assert.AreEqual(SofaLayout.ArmCushionWidthFor(2000) * AppConstants.MM_TO_UNITS,
            bounds.size.y, 1e-4f,
            "боковой валик выдавлен вдоль своей толщины (280 мм), и в СОБСТВЕННЫХ осях "
            + "меша это ось Y: поворот -90 по X и 90 по Y укладывает её вдоль X дивана");
    }

    [Test]
    public void Sofa_DecorSurface_IsWidthByDepth_InPhysicalMillimetres()
    {
        var sofa = Sofa(2000, 800, 900, 120, 400);

        Assert.AreEqual(new Vector2Int(2000, 900), sofa.DecorSurfaceMM,
            "декор носит основание, а его меш строит ProfileExtrusionMesh в системе "
            + "(ширина, ГЛУБИНА) — значит поверхность декора это (x, z), а не базовые "
            + "(x, y) стоячей панели, иначе декор растянется");
        Assert.AreSame(sofa.GetComponent<MeshRenderer>(), sofa.DecorRenderer,
            "рендерер декора назван явно, а не выбран из списка детей по индексу: "
            + "GetComponentInChildren попал бы в подушку, и мощение уехало бы на неё");
    }

    [Test]
    public void Sofa_SaveLoadRoundTrip_KeepsANonDefaultRadiusAndSeatHeight()
    {
        var sofa = Sofa(1800, 820, 950, 143, 371, "Диван-RT");

        var data = ElementCapture.FromElement(sofa);
        var restored = JsonUtility.FromJson<ElementData>(JsonUtility.ToJson(data));

        Assert.IsTrue(restored.isSofa,
            "без флага типа диван загрузится обычной доской — молчаливая потеря");
        Assert.AreEqual(143, restored.cornerRadius,
            "поле cornerRadius общее с радиусной полкой и стартует с 200: не записать его "
            + "значит загрузить диван с чужим скруглением");
        Assert.AreEqual(371, restored.seatHeightMM,
            "поле seatHeightMM общее со стулом и стартует с 450: не записать его значит "
            + "поднять основание дивана на чужое значение");
        Assert.AreEqual(new[] { 1800, 820, 950 }, restored.dimensionsMM,
            "габариты обязаны пережить круг вместе с формой: по ним зажимаются оба "
            + "параметра");
    }

    [Test]
    public void Sofa_SaveLoadRoundTrip_KeepsAZeroCornerRadius()
    {
        var sofa = Sofa(2000, 800, 900, 0, 400, "Диван-RT0");

        var data = ElementCapture.FromElement(sofa);
        var restored = JsonUtility.FromJson<ElementData>(JsonUtility.ToJson(data));

        Assert.AreEqual(0, restored.cornerRadius,
            "ноль — законное значение (прямоугольное основание), а поле инициализировано "
            + "радиусом полки (200): забыть записать его значит скруглить основание при "
            + "загрузке");
    }

    [Test]
    public void ANonSofa_LeavesIsSofaFalse_SoTheFlagCannotLeakOntoOtherTypes()
    {
        var go = ElementFactory.CreateStool(new Vector3Int(360, 450, 360), 0, "Табуретка-NS",
            Vector3.zero);
        _spawned.Add(go);

        var data = ElementCapture.FromElement(go.GetComponent<KitchenElement>());
        var restored = JsonUtility.FromJson<ElementData>(JsonUtility.ToJson(data));

        Assert.IsFalse(restored.isSofa,
            "флаг обязан писаться явно и для ЛОЖНОГО значения: поле с ненулевым умолчанием "
            + "уже однажды загрузило квадратную табуретку скруглённой");
    }

    [Test]
    public void Sofa_Duplicate_ComesOutASofa_WithTheSameShapeAndItsCushions()
    {
        var sofa = Sofa(1800, 820, 950, 143, 371, "Диван-D");

        var copyGo = ElementFactory.Instance.Duplicate(sofa);
        _spawned.Add(copyGo);
        var copy = copyGo.GetComponent<SofaElement>();

        Assert.IsNotNull(copy,
            "каждый реестр ветвится через is XxxElement, и пропуск в ElementDuplicators не "
            + "падает, а молча выдаёт обычную доску");
        Assert.AreEqual(143, copy!.CornerRadiusMM, "скругление обязано пережить копирование");
        Assert.AreEqual(371, copy.SeatHeightMM, "и высота основания тоже");
        Assert.AreEqual(SofaLayout.CushionCount + 1, copy.transform.childCount,
            "и подушки: копия без них была бы другим предметом");
    }

    [Test]
    public void Sofa_TypeOf_IsSofa_NotBoard()
    {
        var sofa = DefaultSofa();

        Assert.AreEqual("sofa", ElementSelector.TypeOf(sofa),
            "лестница ElementSelector.TypeOf замыкается на «board»: забытый тип не "
            + "отказывает, а притворяется доской и попадает в чужую массовую выборку");
    }
}
