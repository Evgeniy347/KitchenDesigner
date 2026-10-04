using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Bulk;

/// <summary>Диван-книжка собран по фотографиям пользователя. Три части: СИДЕНЬЕ
/// спереди, СПИНКА сзади (в сложенном виде идёт вниз до пола минус зазор) и
/// внутренний КОРОБ, который виден только когда сиденье выдвинуто. Подлокотников
/// нет: вместо них четыре подушки — две стоят на сиденье у спинки, две лежат
/// по бокам.
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
/// видно сразу.
///
/// Раскладывание (этапы, анимация, сохранение) — в <c>SofaUnfoldElementTests</c>.</summary>
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

    private static Transform Part(SofaElement sofa, string name)
    {
        foreach (var group in new[] { SofaLayout.FrontGroupName, SofaLayout.HingeGroupName })
        {
            var part = sofa.transform.Find(group + "/" + name);
            if (part != null) return part;
        }

        Assert.Fail("у дивана нет детали " + name + " ни в передней группе, ни в группе спинки");
        return sofa.transform;
    }

    [Test]
    public void Sofa_Defaults_AreTheAgreedTwoThousandByEightHundredByNineHundred()
    {
        var sofa = DefaultSofa();

        Assert.AreEqual(new Vector3Int(2000, 800, 900), sofa.DimensionsMM,
            "ширина 2000 и глубина 900 (180 спинка + 720 сиденье) заданы пользователем и не "
            + "обсуждаются; высота 800 — это низ спинки 100 плюс сама спинка 700");
        Assert.AreEqual(360, SofaElement.DefaultSeatHeightMM,
            "верх сиденья на 360 мм: промер правого края фотографии (он почти в профиль, "
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
            "то же по высоте сиденья");
        Assert.AreEqual(SofaLayout.DefaultCornerRadiusMM, SofaElement.DefaultCornerRadiusMM,
            "то же по скруглению");
        Assert.AreEqual(SofaLayout.MinSeatHeightMM, SofaElement.MinSeatHeightMM,
            "и нижняя граница высоты сиденья");
        Assert.AreEqual(SofaLayout.MaxSeatHeightMM, SofaElement.MaxSeatHeightMM,
            "и верхняя");
    }

    [Test]
    public void Sofa_IsASeatABackrestAndABox_WithFourCushionsRidingOnTheSeat()
    {
        var sofa = DefaultSofa();

        Part(sofa, SofaLayout.SeatName);
        Part(sofa, SofaLayout.BackrestName);
        Part(sofa, SofaLayout.ArmCushionLeftName);
        Part(sofa, SofaLayout.ArmCushionRightName);
        Part(sofa, SofaLayout.BackCushionLeftName);
        Part(sofa, SofaLayout.BackCushionRightName);

        Assert.AreEqual(2 + SofaBoxLayout.CompartmentCount, sofa.transform.childCount,
            "у корня передняя группа (сиденье и подушки), петля спинки и три фасада ящиков; "
            + "короб — это сам меш корня, он и фасады никуда не едут");
        Assert.AreEqual(1 + SofaLayout.CushionCount,
            sofa.transform.Find(SofaLayout.FrontGroupName)!.childCount,
            "в передней группе сиденье и четыре подушки, и больше ничего: подлокотников у "
            + "этого дивана нет, их роль играют боковые подушки");
        Assert.AreEqual(1, sofa.transform.Find(SofaLayout.HingeGroupName)!.childCount,
            "на петле одна спинка");
    }

    [Test]
    public void Sofa_BoxHasThreeDrawerFronts_AlwaysShown_InTheirOwnMaterial()
    {
        var sofa = DefaultSofa();

        MeshRenderer? first = null;
        for (int i = 0; i < SofaBoxLayout.CompartmentCount; i++)
        {
            var front = sofa.transform.Find(SofaBoxLayout.DrawerFrontName(i));
            Assert.IsNotNull(front, "у короба фасад ящика №" + i + ": «ящики» на фото 43");
            var renderer = front!.GetComponent<MeshRenderer>()!;
            Assert.IsTrue(renderer.enabled, "фасады не прячутся при раскладывании");
            first ??= renderer;
            Assert.AreSame(first.sharedMaterial, renderer.sharedMaterial,
                "у всех трёх фасадов один материал");
        }

        Assert.IsNotNull(first!.sharedMaterial, "у фасадов есть материал");
        Assert.AreNotSame(first.sharedMaterial, sofa.GetComponent<MeshRenderer>()!.sharedMaterial,
            "и он отличается от материала корпуса короба: иначе щели между фасадами не "
            + "читаются, и короб снова выглядит сплошным ящиком без ящиков");
        Assert.AreNotSame(first.sharedMaterial, sofa.DecorRenderer!.sharedMaterial,
            "и от обивки: фасады ящиков не носят декор дивана и не должны меняться с ним");
    }

    [Test]
    public void Sofa_RootCarriesTheBox_SoItIsTheOnlyPartThatNeverMoves()
    {
        var sofa = DefaultSofa();

        var mesh = sofa.GetComponent<MeshFilter>()!.sharedMesh;
        Assert.IsNotNull(mesh, "у корня обязан быть меш короба");
        Assert.Greater(mesh!.vertexCount, 0, "и он не пустой");
        Assert.IsNotNull(sofa.GetComponent<MeshRenderer>(), "короб рисуется");

        float seatTop = (-SofaElement.DefaultHeightMM * 0.5f + SofaElement.DefaultSeatHeightMM)
            * AppConstants.MM_TO_UNITS;
        Assert.Less(mesh.bounds.max.y, seatTop,
            "короб целиком ниже верха сиденья: пока сиденье на месте, он скрыт, и сложенный "
            + "диван выглядит сплошным");
    }

    [Test]
    public void Sofa_Backrest_GoesDownToTheFloorMinusTheClearance_WhenFolded()
    {
        var sofa = DefaultSofa();

        var bounds = Part(sofa, SofaLayout.BackrestName).GetComponent<MeshRenderer>()!.bounds;

        Assert.AreEqual((-SofaElement.DefaultHeightMM * 0.5f + SofaLayout.BackrestBottomMM)
            * AppConstants.MM_TO_UNITS, bounds.min.y, 1e-4f,
            "сложенная спинка идёт вниз до пола минус зазор в 100 мм, а не обрывается у сиденья");
        Assert.AreEqual(SofaElement.DefaultHeightMM * 0.5f * AppConstants.MM_TO_UNITS,
            bounds.max.y, 1e-4f, "а верх спинки — общая высота дивана");
        Assert.AreEqual(SofaLayout.BackrestThicknessMM * AppConstants.MM_TO_UNITS,
            bounds.size.z, 1e-4f, "и она толщиной 180 мм");
    }

    [Test]
    public void CornerRadiusMM_AboveHalfTheSeatDepth_IsClampedToIt()
    {
        var sofa = DefaultSofa();

        sofa.CornerRadiusMM = 5000;

        Assert.AreEqual(360, sofa.CornerRadiusMM,
            "радиус основания ограничен половиной меньшей стороны СИДЕНЬЯ: 720/2 = 360 мм. "
            + "Больше половины — это уже не скругление, а вылет контура наружу");
    }

    [Test]
    public void SeatHeightMM_AboveTheMaximum_IsClampedToIt()
    {
        var sofa = DefaultSofa();

        sofa.SeatHeightMM = 5000;

        Assert.AreEqual(SofaElement.MaxSeatHeightMM, sofa.SeatHeightMM,
            "выше предела спинка при складывании ушла бы за заднюю стенку");
    }

    [Test]
    public void SeatHeightMM_BelowTheMinimum_IsClampedToIt()
    {
        var sofa = DefaultSofa();

        sofa.SeatHeightMM = 10;

        Assert.AreEqual(SofaElement.MinSeatHeightMM, sofa.SeatHeightMM,
            "ниже минимума под сиденьем не помещается короб с бельём");
    }

    [Test]
    public void DimensionsMM_WithAnyHeight_IsPulledBackToTheFixedHeight()
    {
        var sofa = DefaultSofa();

        sofa.DimensionsMM = new Vector3Int(1800, 1500, 1000);

        Assert.AreEqual(new Vector3Int(1800, SofaLayout.OverallHeightMM, 1000), sofa.DimensionsMM,
            "высота дивана не растягивается: заказанные 1500 сводятся к 800, длина и "
            + "глубина остаются");
    }

    [Test]
    public void DimensionsMM_WithADeeperSofa_StretchesOnlyTheSeat_NotTheBackrest()
    {
        var sofa = DefaultSofa();

        sofa.DimensionsMM = new Vector3Int(2000, 800, 1200);

        var seat = Part(sofa, SofaLayout.SeatName).GetComponent<MeshRenderer>()!.bounds;
        var backrest = Part(sofa, SofaLayout.BackrestName).GetComponent<MeshRenderer>()!.bounds;
        Assert.AreEqual(1020f * AppConstants.MM_TO_UNITS, seat.size.z, 1e-4f,
            "вся добавленная глубина досталась сиденью: 1200 − 180");
        Assert.AreEqual(180f * AppConstants.MM_TO_UNITS, backrest.size.z, 1e-4f,
            "а спинка осталась 180 мм");
    }

    [Test]
    public void Sofa_ResizedToANonSquareFootprint_KeepsTheFrontCornerArcCircular_NotElliptical()
    {
        var sofa = Sofa(1200, 800, 1200, 200, 400);
        sofa.DimensionsMM = new Vector3Int(2000, 800, 900);

        var mesh = Part(sofa, SofaLayout.SeatName).GetComponent<MeshFilter>()!.sharedMesh;
        Assert.IsNotNull(mesh, "у сиденья обязан быть меш");

        float radius = 200 * AppConstants.MM_TO_UNITS;
        float seatDepth = 720 * AppConstants.MM_TO_UNITS;
        var centre = new Vector2(2.0f * 0.5f - radius, seatDepth * 0.5f - radius);

        int checkedPoints = 0;
        foreach (var local in mesh!.vertices)
        {
            if (local.x < centre.x - 1e-4f || local.z < centre.y - 1e-4f) continue;
            checkedPoints++;
            Assert.AreEqual(radius, (new Vector2(local.x, local.z) - centre).magnitude, 1e-4f,
                "передний угол сиденья обязан остаться ДУГОЙ ОКРУЖНОСТИ физического радиуса "
                + "200 мм. Если строить контур в единичном пространстве и растягивать корнем — "
                + "как делает радиусный стол, — на следе 2000x720 угол станет эллиптическим");
        }

        Assert.GreaterOrEqual(checkedPoints, RoundedRectProfile.DefaultSegments,
            "вершин углового скругления не нашлось — тест позеленел бы, ничего не проверив");
    }

    [Test]
    public void Seat_RearCorners_AreSquare_SoItMeetsTheBackrestWithoutAGap()
    {
        var sofa = DefaultSofa();

        var bounds = Part(sofa, SofaLayout.SeatName).GetComponent<MeshRenderer>()!.bounds;
        var mesh = Part(sofa, SofaLayout.SeatName).GetComponent<MeshFilter>()!.sharedMesh;

        bool hasRearCorner = false;
        foreach (var local in mesh.vertices)
            if (Mathf.Abs(local.x - mesh.bounds.min.x) < 1e-4f
                && Mathf.Abs(local.z - mesh.bounds.min.z) < 1e-4f)
                hasRearCorner = true;

        Assert.IsTrue(hasRearCorner,
            "в заднем углу сиденья есть вершина ровно в углу габарита — угол прямой: "
            + "скруглённый оставил бы щель у боковой кромки спинки");
        Assert.AreEqual(2000f * AppConstants.MM_TO_UNITS, bounds.size.x, 1e-4f,
            "и сиденье на всю ширину");
    }

    [Test]
    public void SofaCushion_OnANonSquareSofa_KeepsItsCornerFilletAtItsPhysicalRadius()
    {
        var sofa = Sofa(2000, 800, 900, 120, 400);

        var cushion = Part(sofa, SofaLayout.BackCushionRightName);
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
                "подушка скруглена по ВСЕМ трём осям, и её скругление задано в физических "
                + "миллиметрах: на шве каждая точка стоит ровно на радиусе от внутренней "
                + "коробки. Мерим в МИРОВЫХ координатах, а не в локальных вершинах меша: "
                + "локально расстояние останется прежним и тогда, когда корень растянет всю "
                + "мебель, и тест был бы слеп ровно к тому дефекту, ради которого написан");
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
    public void Sofa_ArmCushions_AreBuiltInWorldMillimetres_NotScaledByTheRoot()
    {
        var sofa = Sofa(2000, 800, 900, 120, 400);

        var arm = Part(sofa, SofaLayout.ArmCushionRightName);
        Assert.AreEqual(Vector3.one, arm.localScale,
            "подушка не масштабируется: её размер уже заложен в собственный меш, и "
            + "масштаб поверх него — это второе умножение");

        var size = arm.GetComponent<MeshFilter>()!.sharedMesh.bounds.size;
        Assert.AreEqual(SofaLayout.ArmCushionWidthFor(2000) * AppConstants.MM_TO_UNITS,
            size.x, 1e-4f, "боковой валик по ширине — 320 мм");
        Assert.AreEqual(SofaLayout.ArmCushionHeightMM * AppConstants.MM_TO_UNITS,
            size.y, 1e-4f, "по высоте — 240 мм: теперь это плоский валик, лежащий плашмя");
        Assert.AreEqual(SofaLayout.ArmCushionLengthFor(900) * AppConstants.MM_TO_UNITS,
            size.z, 1e-4f, "и по длине — от спинки до переднего края сиденья");
    }

    [Test]
    public void Sofa_DecorSurface_IsTheSeatTop_InPhysicalMillimetres()
    {
        var sofa = Sofa(2000, 800, 900, 120, 400);

        Assert.AreEqual(new Vector2Int(2000, 720), sofa.DecorSurfaceMM,
            "декор носит сиденье, а его меш строит ProfileExtrusionMesh в системе (ширина, "
            + "ГЛУБИНА) — значит поверхность декора это (x, глубина сиденья), а не весь "
            + "габарит и не базовые (x, y) стоячей панели, иначе декор растянется");
        Assert.AreSame(Part(sofa, SofaLayout.SeatName).GetComponent<MeshRenderer>(),
            sofa.DecorRenderer,
            "рендерер декора назван явно: корень теперь рисует короб, а GetComponentInChildren "
            + "попал бы в подушку, и мощение уехало бы на неё");
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
        Assert.AreEqual(new[] { 1800, 800, 950 }, restored.dimensionsMM,
            "габариты обязаны пережить круг вместе с формой: по ним зажимаются оба "
            + "параметра; заказанные 820 по высоте сведены к фиксированным 800");
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
        Part(copy, SofaLayout.BackCushionLeftName);
        Part(copy, SofaLayout.ArmCushionRightName);
        Part(copy, SofaLayout.BackrestName);
        Assert.AreEqual(2 + SofaBoxLayout.CompartmentCount, copy.transform.childCount,
            "и обе группы с фасадами ящиков: копия без подушек или спинки была бы другим предметом");
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
