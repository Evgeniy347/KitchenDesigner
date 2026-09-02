using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

/// <summary>Стул — табуретка со спинкой, но НЕ наследник табуретки. Каждый
/// реестр этого проекта ветвится через <c>is XxxElement</c>, а
/// <c>ElementDuplicators</c> проверяет <c>is StoolElement</c> РАНЬШЕ стула:
/// подкласс молча дублировался бы табуреткой и терял спинку. Отдельный тип —
/// не стилистическое решение, а условие того, что реестры его видят.
///
/// Второе, что здесь удерживается: сиденье строится профильным выдавливанием
/// (как у табуретки), а спинка — ОБЫЧНЫЙ куб-ребёнок. <c>ProfileExtrusionMesh</c>
/// разворачивает UV по следу детали в плоскости XZ; поставить такой меш стоймя
/// значит развернуть декор в осях (ширина, толщина панели) — ровно тот дефект,
/// ради которого существует <c>DecorSurfaceUvTests</c>.</summary>
public class ChairElementTests
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

    private ChairElement Chair(int width, int height, int depth, int cornerRadiusMM,
        int seatHeightMM = AppConstants.CHAIR_SEAT_HEIGHT_DEFAULT, string name = "Стул-1")
    {
        var go = ElementFactory.CreateChair(new Vector3Int(width, height, depth),
            cornerRadiusMM, seatHeightMM, name, Vector3.zero);
        _spawned.Add(go);
        var chair = go.GetComponent<ChairElement>();
        Assert.IsNotNull(chair, "фабрика обязана вернуть ChairElement");
        return chair!;
    }

    private ChairElement DefaultChair() =>
        Chair(ChairElement.DefaultWidthMM, ChairElement.DefaultHeightMM,
            ChairElement.DefaultDepthMM, 0);

    private static Transform Backrest(ChairElement chair)
    {
        var backrest = chair.transform.Find(ChairElement.BackrestChildName);
        Assert.IsNotNull(backrest, "спинка — именованный ребёнок, а не элемент списка детей: "
            + "поиск по индексу однажды уже уронил тайлинг декора на ножку");
        return backrest!;
    }

    [Test]
    public void Chair_Defaults_AreTheAgreedFourHundredByNineHundredByFourHundred()
    {
        var chair = DefaultChair();

        Assert.AreEqual(new Vector3Int(400, 900, 400), chair.DimensionsMM,
            "габариты стула согласованы с пользователем: 400 x 900 x 400 мм");
        Assert.AreEqual(450, chair.SeatHeightMM, "верх сиденья на 450 мм от пола");
        Assert.AreEqual(30, ChairElement.SeatThicknessMM, "сиденье 30 мм");
        Assert.AreEqual(40, ChairElement.LegCrossSectionMM, "ножка 40 x 40 мм");
        Assert.AreEqual(30, ChairElement.LegInsetMM, "отступ ножки от края 30 мм");
        Assert.AreEqual(20, ChairElement.BackrestThicknessMM, "спинка 20 мм");
    }

    [Test]
    public void Chair_IsNotAStool_SoTheRegistriesSeeItAsItsOwnType()
    {
        Assert.IsFalse(typeof(StoolElement).IsAssignableFrom(typeof(ChairElement)),
            "ElementDuplicators спрашивает is StoolElement РАНЬШЕ стула: подкласс "
            + "дублировался бы табуреткой и молча терял спинку");
        Assert.IsTrue(typeof(KitchenElement).IsAssignableFrom(typeof(ChairElement)),
            "и при этом стул обязан оставаться элементом: реестры выводят список типов "
            + "из цепочки наследования до KitchenElement");
    }

    [Test]
    public void CornerRadiusMM_AboveHalfTheSmallerSide_IsClampedToIt()
    {
        var chair = Chair(400, 900, 400, 0);

        chair.CornerRadiusMM = 1000;

        Assert.AreEqual(200, chair.CornerRadiusMM,
            "радиус сиденья ограничен половиной меньшей стороны, как у табуретки: "
            + "400/2 = 200 мм");
    }

    [Test]
    public void CornerRadiusMM_Negative_IsClampedToZero_NotToOne()
    {
        var chair = Chair(400, 900, 400, 0);

        chair.CornerRadiusMM = -50;

        Assert.AreEqual(0, chair.CornerRadiusMM,
            "ноль — законное значение (квадратное сиденье); нижняя граница радиусной "
            + "полки в 1 сюда не переносится");
    }

    [Test]
    public void ApplyDimensions_ShrinkingTheChair_ReClampsARadiusThatWasLegalBefore()
    {
        var chair = Chair(600, 900, 600, 300);
        Assume.That(chair.CornerRadiusMM, Is.EqualTo(300),
            "исходный радиус обязан быть законным, иначе тест ничего не ловит");

        chair.DimensionsMM = new Vector3Int(400, 900, 400);

        Assert.AreEqual(200, chair.CornerRadiusMM,
            "ресайз способен сделать НЕЗАКОННЫМ радиус, который был законным: 300 мм "
            + "законны при следе 600, но при 400 максимум уже 200. Пересжатия в сеттере "
            + "мало — оно обязано быть и в ApplyDimensions");
    }

    [Test]
    public void SeatHeightMM_AboveTheOverallHeight_IsClampedSoTheBackrestKeepsItsRoom()
    {
        var chair = Chair(400, 900, 400, 0);

        chair.SeatHeightMM = 5000;

        Assert.AreEqual(900 - ChairElement.MinBackrestHeightMM, chair.SeatHeightMM,
            "сиденье под самой макушкой — это уже табуретка: спинке оставлено не меньше "
            + "MinBackrestHeightMM");
    }

    [Test]
    public void SeatHeightMM_BelowTheSeatItself_IsClampedSoTheLegsKeepTheirRoom()
    {
        var chair = Chair(400, 900, 400, 0);

        chair.SeatHeightMM = 0;

        Assert.AreEqual(ChairElement.SeatThicknessMM + ChairElement.MinLegHeightMM,
            chair.SeatHeightMM,
            "верх сиденья ниже толщины самого сиденья означал бы ножки отрицательной "
            + "высоты");
    }

    [Test]
    public void ApplyDimensions_LoweringTheChair_ReClampsASeatHeightThatWasLegalBefore()
    {
        var chair = Chair(400, 900, 400, 0, 800);
        Assume.That(chair.SeatHeightMM, Is.EqualTo(800),
            "исходная высота сиденья обязана быть законной, иначе тест ничего не ловит");

        chair.DimensionsMM = new Vector3Int(400, 500, 400);

        Assert.AreEqual(500 - ChairElement.MinBackrestHeightMM, chair.SeatHeightMM,
            "ресайз по высоте способен сделать НЕЗАКОННОЙ высоту сиденья, которая была "
            + "законной: пересжатие обязано быть и в ApplyDimensions, а не только в сеттере");
    }

    [Test]
    public void SeatHeightMM_OnATooShortChair_StaysOrdered_InsteadOfFlipping()
    {
        var chair = Chair(400, 60, 400, 0);

        Assert.LessOrEqual(ChairElement.MinSeatHeightMM(60), ChairElement.MaxSeatHeightMM(60),
            "минимум обязан оставаться не больше максимума даже там, где обе константы "
            + "вместе не помещаются: Mathf.Clamp с min > max возвращает то min, то max в "
            + "зависимости от входа — то есть просто врёт");
        Assert.AreEqual(ChairElement.MaxSeatHeightMM(60), chair.SeatHeightMM,
            "на слишком низком стуле побеждает верхняя граница");
    }

    [Test]
    public void Chair_RootTransform_StaysAtUnitScale()
    {
        var chair = Chair(600, 900, 400, 100);

        Assert.AreEqual(Vector3.one, chair.transform.localScale,
            "сиденье строится в физических единицах, поэтому корень НЕ масштабируется: "
            + "иначе ножки и спинка отмасштабируются дважды");
    }

    [Test]
    public void Chair_BoundingBox_MatchesDimensions_DespiteTheUnitRootScale()
    {
        var chair = Chair(600, 900, 400, 100);

        var v = chair.GetVertices();
        Vector3 min = v[0], max = v[0];
        for (int i = 1; i < v.Length; i++)
        {
            min = Vector3.Min(min, v[i]);
            max = Vector3.Max(max, v[i]);
        }

        Assert.AreEqual(0.6f, max.x - min.x, 1e-4f,
            "габариты для снапа и валидации берутся из EffectiveScale, а не из localScale");
        Assert.AreEqual(0.9f, max.y - min.y, 1e-4f, "то же по высоте");
        Assert.AreEqual(0.4f, max.z - min.z, 1e-4f, "то же по глубине");
    }

    [Test]
    public void Chair_SeatTop_SitsAtTheSeatHeightAboveTheFloor()
    {
        var chair = Chair(400, 900, 400, 0, 500);

        var mesh = chair.GetComponent<MeshFilter>()!.sharedMesh;
        Assert.IsNotNull(mesh, "у стула обязан быть меш сиденья");

        float topY = float.NegativeInfinity;
        foreach (var v in mesh!.vertices) topY = Mathf.Max(topY, v.y);

        float floorY = -900 * 0.5f * AppConstants.MM_TO_UNITS;
        Assert.AreEqual(500 * AppConstants.MM_TO_UNITS, topY - floorY, 1e-4f,
            "SeatHeightMM — это верх сиденья над полом, а не его середина и не низ");
    }

    [Test]
    public void Chair_Legs_StopAtTheSeat_NotAtTheTopOfTheChair()
    {
        var chair = Chair(400, 900, 400, 0, 500);

        var legs = new List<Transform>();
        foreach (Transform child in chair.transform)
            if (child.name.StartsWith("Leg")) legs.Add(child);

        Assert.AreEqual(4, legs.Count, "у стула четыре ножки");

        float expected = (500 - ChairElement.SeatThicknessMM) * AppConstants.MM_TO_UNITS;
        foreach (var leg in legs)
            Assert.AreEqual(expected, leg.localScale.y, 1e-4f,
                "ножка стула доходит до низа СИДЕНЬЯ, а не до общей высоты: взять формулу "
                + "табуретки как есть значит вырастить ножки сквозь спинку");
    }

    [Test]
    public void Chair_Backrest_SpansTheWidthAtTheBack_FromTheSeatTopToTheOverallHeight()
    {
        var chair = Chair(400, 900, 400, 0, 450);
        var backrest = Backrest(chair);

        float toU = AppConstants.MM_TO_UNITS;
        Assert.AreEqual(400 * toU, backrest.localScale.x, 1e-4f, "спинка во всю ширину");
        Assert.AreEqual((900 - 450) * toU, backrest.localScale.y, 1e-4f,
            "спинка от верха сиденья до общей высоты");
        Assert.AreEqual(ChairElement.BackrestThicknessMM * toU, backrest.localScale.z, 1e-4f,
            "спинка толщиной 20 мм");

        float top = backrest.localPosition.y + backrest.localScale.y * 0.5f;
        Assert.AreEqual(900 * 0.5f * toU, top, 1e-4f, "верх спинки — общая высота стула");

        float back = backrest.localPosition.z - backrest.localScale.z * 0.5f;
        Assert.AreEqual(-400 * 0.5f * toU, back, 1e-4f,
            "спинка прижата к ЗАДНЕЙ грани (-Z): ящики в этом проекте выезжают в +Z, "
            + "значит перёд — это +Z");
    }

    [Test]
    public void Chair_Backrest_IsAPlainBox_NotAProfileExtrusion()
    {
        var chair = Chair(400, 900, 400, FurnitureLayout.MaxCornerRadiusMM(
            new Vector3Int(400, 900, 400)));
        var backrest = Backrest(chair);

        var mesh = backrest.GetComponent<MeshFilter>()!.sharedMesh;
        Assert.AreEqual(24, mesh.vertexCount,
            "спинка — примитивный куб (24 вершины). Профильный меш, повёрнутый стоймя, "
            + "получил бы развёртку в осях (ширина, ТОЛЩИНА панели) — не тех, что "
            + "называет DecorSurfaceMM");
    }

    [Test]
    public void Chair_DecorSurface_IsWidthByDepth_InPhysicalMillimetres()
    {
        var chair = Chair(600, 900, 400, 100);

        Assert.AreEqual(new Vector2Int(600, 400), chair.DecorSurfaceMM,
            "ProfileExtrusionMesh разворачивает всё в системе (ширина, ГЛУБИНА), поэтому "
            + "поверхность декора — это (x, z), а не базовые (x, y) стоячей панели");
    }

    [Test]
    public void Chair_DecorRenderer_IsTheSeat_NotWhateverChildComesFirst()
    {
        var chair = Chair(400, 900, 400, 0);

        Assert.AreSame(chair.GetComponent<MeshRenderer>(), chair.DecorRenderer,
            "рендерер декора — сиденье на КОРНЕ, найденное компонентом, а не первый "
            + "попавшийся ребёнок: поиск по индексу однажды посадил тайлинг на ножку");
    }

    [Test]
    public void Chair_SaveLoadRoundTrip_KeepsBothANonDefaultRadiusAndANonDefaultSeatHeight()
    {
        var chair = Chair(600, 900, 400, 137, 512, "Стул-RT");

        var data = ElementCapture.FromElement(chair);
        var restored = JsonUtility.FromJson<ElementData>(JsonUtility.ToJson(data));

        Assert.IsTrue(restored.isChair,
            "без флага типа стул загрузится обычной доской — молчаливая потеря");
        Assert.IsFalse(restored.isStool,
            "стул не табуретка: два флага сразу отдали бы его чужой ветке восстановления");
        Assert.AreEqual(137, restored.cornerRadius,
            "поле cornerRadius в ElementData инициализировано радиусом полки (200): не "
            + "записать его для стула значит загрузить квадратное сиденье скруглённым");
        Assert.AreEqual(512, restored.seatHeightMM, "высота сиденья обязана пережить круг");
        Assert.AreEqual(new[] { 600, 900, 400 }, restored.dimensionsMM,
            "габариты обязаны пережить круг вместе с ними: обе величины зажимаются по ним");
    }

    [Test]
    public void Chair_SaveLoadRoundTrip_KeepsAZeroCornerRadius()
    {
        var chair = Chair(400, 900, 400, 0, 450, "Стул-RT0");

        var data = ElementCapture.FromElement(chair);
        var restored = JsonUtility.FromJson<ElementData>(JsonUtility.ToJson(data));

        Assert.AreEqual(0, restored.cornerRadius,
            "ноль — законное значение, а поле общее с радиусной полкой и стартует с 200");
    }

    [Test]
    public void ElementData_IsChair_IsWrittenEvenWhenFalse()
    {
        var stool = ElementFactory.CreateStool(new Vector3Int(360, 450, 360), 0,
            "Табуретка-НЕ-Стул", Vector3.zero);
        _spawned.Add(stool);

        var data = ElementCapture.FromElement(stool.GetComponent<StoolElement>());

        Assert.IsFalse(data.isChair,
            "флаг обязан выставляться ЯВНО и для не-стула: реестр восстановления читает "
            + "ветки по порядку, и подтёкший true увёл бы табуретку в стулья");
    }

    [Test]
    public void Chair_Duplicate_ComesOutAChair_WithTheSameRadiusAndSeatHeight()
    {
        var chair = Chair(600, 900, 400, 137, 512, "Стул-D");

        var copyGo = ElementFactory.Instance.Duplicate(chair);
        _spawned.Add(copyGo);
        var copy = copyGo.GetComponent<ChairElement>();

        Assert.IsNotNull(copy,
            "пропуск в реестре дублирования молча возвращает обычную доску; а ветка "
            + "табуретки, стоящая раньше, вернула бы табуретку без спинки");
        Assert.AreEqual(137, copy!.CornerRadiusMM, "копия обязана повторить форму оригинала");
        Assert.AreEqual(512, copy.SeatHeightMM, "и высоту сиденья");
        Assert.IsNotNull(copy.transform.Find(ChairElement.BackrestChildName),
            "и спинку");
    }

    [Test]
    public void Chair_TypeForBulkSelector_IsChair_NotStoolAndNotBoard()
    {
        var chair = DefaultChair();

        Assert.AreEqual("chair", KitchenDesigner.Core.Bulk.ElementSelector.TypeOf(chair),
            "забытый тип не отказывает, а притворяется доской и попадает в чужую выборку");
    }

    [Test]
    public void Chair_DisplayTypeName_IsTheChair()
    {
        Assert.AreEqual("Стул", DefaultChair().DisplayTypeName,
            "подпись типа в заголовке окна свойств — данные элемента, а не лестница в UI");
    }
}
