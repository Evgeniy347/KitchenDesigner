using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Bulk;

/// <summary>Кровать: ножки 100 мм, царга, матрас, подушки и необязательная
/// спинка у изголовья.
///
/// Тип самостоятельный, а не наследник дивана или стула: каждый реестр проекта
/// ветвится через <c>is XxxElement</c>, и подкласс молча проваливается сквозь
/// них все — ElementDuplicators выдал бы копию кровати диваном и потерял бы
/// матрас с подушками.
///
/// Два переключателя, и оба СБРАСЫВАЮТ размер — это решение пользователя, а не
/// упрощение: «односпальная и двуспальная отличаются размерами по умолчанию, и
/// при переключении габарит становится дефолтным для нового типа, независимо от
/// того, растягивал ли пользователь её руками». Отсюда следует, что туда и
/// обратно — это ДВА сброса, а не возврат к прежнему размеру; это пришпилено
/// отдельным тестом, чтобы следующий не принял поведение за баг.
///
/// Как и вся эта семья мебели, кровать строится в ФИЗИЧЕСКОМ пространстве:
/// <c>localScale</c> корня единичный, габариты отдаёт <c>EffectiveScale</c>.
/// Растяжение корнем превратило бы скругления царги и подушек в эллипсы — на
/// следе 1800x2000 это видно сразу.</summary>
public class BedElementTests
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
        CommandStack.Clear();
        PartRegistry.Clear();
        LogAssert.ignoreFailingMessages = false;
    }

    private BedElement Bed(Vector3Int dims, bool isDouble, bool hasHeadboard,
        string name = "Кровать-1")
    {
        var go = ElementFactory.CreateBed(dims, isDouble, hasHeadboard, name, Vector3.zero);
        _spawned.Add(go);
        var bed = go.GetComponent<BedElement>();
        Assert.IsNotNull(bed, "фабрика обязана вернуть BedElement");
        return bed!;
    }

    private BedElement DefaultBed() =>
        Bed(BedLayout.DefaultDimensions(true, true), true, true);

    private static Transform? Child(BedElement bed, string name) => bed.transform.Find(name);

    private static Transform Required(BedElement bed, string name)
    {
        var child = Child(bed, name);
        Assert.IsNotNull(child, "у кровати обязан быть ребёнок " + name);
        return child!;
    }

    [Test]
    public void Bed_Defaults_AreTheDoubleTwoThousandByEighteenHundred()
    {
        var bed = DefaultBed();

        Assert.AreEqual(new Vector3Int(1800, 900, 2000), bed.DimensionsMM,
            "2000x1800 задано пользователем: 2000 — длина (Z), 1800 — ширина (X)");
        Assert.IsTrue(bed.IsDouble, "кровать по умолчанию двуспальная — так заказано");
        Assert.IsTrue(bed.HasHeadboard, "и со спинкой: без неё это просто матрас на коробе");
    }

    [Test]
    public void Bed_Legs_AreExactlyOneHundredMillimetresTall()
    {
        var bed = DefaultBed();
        var leg = Required(bed, BedLayout.LegNamePrefix + "1");

        Assert.AreEqual(BedLayout.LegHeightMM * AppConstants.MM_TO_UNITS,
            leg.localScale.y, 1e-4f,
            "ножки 100 мм — прямое требование пользователя, а не подобранная пропорция");
    }

    [Test]
    public void ADoubleBed_Has_SixLegs_AndASingleOne_Four()
    {
        Assert.AreEqual(6, CountLegs(DefaultBed()),
            "у двуспальной пролёт царги 2000 мм: середина длинной стороны провисает без "
            + "пары средних ножек");
        Assert.AreEqual(4, CountLegs(Bed(BedLayout.DefaultDimensions(false, true), false, true)),
            "односпальная 900 мм несёт вдвое меньше — четырёх углов хватает");
    }

    private static int CountLegs(BedElement bed)
    {
        int count = 0;
        foreach (Transform child in bed.transform)
            if (child.name.StartsWith(BedLayout.LegNamePrefix)) count++;
        return count;
    }

    [Test]
    public void ADoubleBed_Carries_TwoPillows_AndASingleOne_One()
    {
        var doubleBed = DefaultBed();
        Assert.IsNotNull(Child(doubleBed, BedLayout.PillowName(0)),
            "первая подушка есть у любой кровати");
        Assert.IsNotNull(Child(doubleBed, BedLayout.PillowName(1)),
            "вторая — только у двуспальной; число подушек это следствие типа");

        var single = Bed(BedLayout.DefaultDimensions(false, true), false, true);
        Assert.IsNotNull(Child(single, BedLayout.PillowName(0)),
            "у односпальной одна подушка");
        Assert.IsNull(Child(single, BedLayout.PillowName(1)),
            "и ровно одна: вторая обязана исчезнуть");
    }

    [Test]
    public void SwitchingToSingle_RemovesTheSecondPillow_AndTwoLegs()
    {
        var bed = DefaultBed();

        bed.IsDouble = false;

        Assert.IsNull(Child(bed, BedLayout.PillowName(1)),
            "вторая подушка обязана быть УНИЧТОЖЕНА, а не оставлена от прошлой сборки: "
            + "иначе односпальная кровать носит подушку, свисающую с матраса");
        Assert.AreEqual(4, CountLegs(bed),
            "и лишняя пара ножек тоже: LegSet переиспользует объекты, а не пересоздаёт их");
    }

    [Test]
    public void SwitchingToDouble_BringsTheSecondPillowBack()
    {
        var bed = Bed(BedLayout.DefaultDimensions(false, true), false, true);

        bed.IsDouble = true;

        Assert.IsNotNull(Child(bed, BedLayout.PillowName(1)),
            "подушка, уничтоженная при переходе в односпальную, обязана вернуться — иначе "
            + "переключатель работает только в одну сторону");
        Assert.AreEqual(6, CountLegs(bed), "и шестая ножка тоже");
    }

    [Test]
    public void SwitchingTheType_ResetsTheSize_EvenAfterAManualResize()
    {
        var bed = DefaultBed();
        bed.DimensionsMM = new Vector3Int(1600, 950, 2100);

        bed.IsDouble = false;

        Assert.AreEqual(BedLayout.DefaultDimensions(false, true), bed.DimensionsMM,
            "смена типа СБРАСЫВАЕТ габарит на дефолтный для нового типа — прямое указание "
            + "пользователя. Ручной размер при этом теряется, и это не дефект");
    }

    [Test]
    public void SwitchingTheTypeThereAndBack_IsTwoResets_NotAnUndo()
    {
        var bed = DefaultBed();
        var byHand = new Vector3Int(1600, 950, 2100);
        bed.DimensionsMM = byHand;

        bed.IsDouble = false;
        bed.IsDouble = true;

        Assert.AreEqual(BedLayout.DefaultDimensions(true, true), bed.DimensionsMM,
            "переключение туда и обратно — это ДВА сброса, а не возврат к прежнему "
            + "размеру. Так решил пользователь; не «чините» это на запоминание ручного "
            + "габарита. Отмена ручного размера — работа CommandStack, а не переключателя");
        Assert.AreNotEqual(byHand, bed.DimensionsMM,
            "положительный контроль к предыдущей строке: если бы размер запоминался, тест "
            + "выше был бы зелёным и ничего не проверял");
    }

    [Test]
    public void ManualResize_StillWorks_AfterTheTypeHasBeenSwitched()
    {
        var bed = DefaultBed();
        bed.IsDouble = false;

        bed.DimensionsMM = new Vector3Int(1000, 900, 1900);

        Assert.AreEqual(new Vector3Int(1000, 900, 1900), bed.DimensionsMM,
            "тип задаёт СТАРТОВЫЙ размер, а не запирает его: после переключения кровать "
            + "обязана тянуться руками как раньше");
    }

    [Test]
    public void TheHeadboardToggle_AddsAndRemovesThePanel()
    {
        var bed = DefaultBed();
        Assert.IsNotNull(Child(bed, BedLayout.HeadboardName), "со спинкой щит есть");

        bed.HasHeadboard = false;
        Assert.IsNull(Child(bed, BedLayout.HeadboardName),
            "без спинки щит обязан быть уничтожен: спрятанный щит остался бы в габаритах "
            + "рендера и в снимке");

        bed.HasHeadboard = true;
        Assert.IsNotNull(Child(bed, BedLayout.HeadboardName),
            "и вернуться: переключатель обязан работать в обе стороны");
    }

    [Test]
    public void TheHeadboardToggle_ResetsTheHeight_AndLeavesWidthAndLengthAlone()
    {
        var bed = DefaultBed();
        bed.DimensionsMM = new Vector3Int(1600, 1100, 2100);

        bed.HasHeadboard = false;

        Assert.AreEqual(BedLayout.HeightFor(false), bed.DimensionsMM.y,
            "спинка живёт по высоте, поэтому её переключатель сбрасывает высоту — "
            + "иначе кровать без спинки унесла бы 500 мм пустого воздуха в габаритной "
            + "коробке");
        Assert.AreEqual(1600, bed.DimensionsMM.x,
            "но ширину не трогает: её задаёт тип кровати, а не изголовье");
        Assert.AreEqual(2100, bed.DimensionsMM.z, "и длину тоже не трогает");
    }

    [Test]
    public void ABedWithoutAHeadboard_CannotBeSqueezedBelowTheTopOfItsPillows()
    {
        var bed = Bed(BedLayout.DefaultDimensions(true, false), true, false);

        bed.DimensionsMM = new Vector3Int(1800, 300, 2000);

        Assert.AreEqual(BedLayout.MinHeightMM(false), bed.DimensionsMM.y,
            "ножки, царга, матрас и подушка занимают 600 мм; ниже кровать не сжимается, "
            + "иначе габаритная коробка начнёт врать про силуэт");
    }

    [Test]
    public void ABedWithAHeadboard_KeepsRoomForIt_WhenSqueezed()
    {
        var bed = DefaultBed();

        bed.DimensionsMM = new Vector3Int(1800, 300, 2000);

        Assert.AreEqual(BedLayout.MinHeightMM(true), bed.DimensionsMM.y,
            "со спинкой минимум выше на её минимальный подъём: спинка вровень с подушками "
            + "— это уже кровать без спинки, просто с другим флагом");
    }

    [Test]
    public void Bed_RootStaysUnitScaled_WhileTheVerticesSpanThePhysicalSize()
    {
        var bed = Bed(new Vector3Int(1800, 900, 2000), true, true);

        Assert.AreEqual(Vector3.one, bed.transform.localScale,
            "корень единичный: масштабирование корнем превратило бы скругления царги и "
            + "подушек в эллипсы — на следе 1800x2000 это видно сразу");

        var vertices = bed.GetVertices();
        var min = vertices[0];
        var max = vertices[0];
        foreach (var v in vertices)
        {
            min = Vector3.Min(min, v);
            max = Vector3.Max(max, v);
        }

        Assert.AreEqual(1.8f, max.x - min.x, 1e-3f,
            "габариты для снапа и валидации берутся из EffectiveScale, а не из localScale");
        Assert.AreEqual(0.9f, max.y - min.y, 1e-3f, "то же по высоте");
        Assert.AreEqual(2.0f, max.z - min.z, 1e-3f, "и по длине");
    }

    [Test]
    public void BedChildren_AreUnitScaled_SoTheirRoundingsStayCircular()
    {
        var bed = Bed(new Vector3Int(1800, 900, 2000), true, true);

        foreach (var name in new[]
                 {
                     BedLayout.MattressName, BedLayout.HeadboardName,
                     BedLayout.PillowName(0), BedLayout.PillowName(1),
                 })
            Assert.AreEqual(Vector3.one, Required(bed, name).localScale,
                "деталь «" + name + "» построена в физических миллиметрах и не имеет права "
                + "растягиваться трансформом: скругление задано в мм и превратилось бы "
                + "в эллипс");
    }

    [Test]
    public void Bed_DecorSurface_IsWidthByLength_InPhysicalMillimetres()
    {
        var bed = Bed(new Vector3Int(1800, 900, 2000), true, true);

        Assert.AreEqual(new Vector2Int(1800, 2000), bed.DecorSurfaceMM,
            "царга лежит горизонтально: её развёртка идёт по (ширина, ГЛУБИНА), а не по "
            + "(ширина, высота) стоячей панели — иначе декор делится на высоту кровати и "
            + "растягивается");
    }

    [Test]
    public void Bed_DecorRenderer_IsTheFrameOnTheRoot_NotWhicheverChildComesFirst()
    {
        var bed = DefaultBed();

        Assert.AreSame(bed.GetComponent<MeshRenderer>(), bed.DecorRenderer,
            "GetComponentInChildren вернул бы «первого попавшегося» ребёнка, и мощение "
            + "легло бы на ножку — этот дефект в проекте уже случался");
    }

    [Test]
    public void Bed_TypeOf_IsBed_NotBoard()
    {
        Assert.AreEqual("bed", ElementSelector.TypeOf(DefaultBed()),
            "забытый тип не отказывает, а притворяется доской и попадает в чужую выборку "
            + "массовых операций");
    }

    [Test]
    public void Bed_SurvivesASaveLoadRoundTrip_WithNonDefaultValues()
    {
        var bed = Bed(new Vector3Int(940, 640, 1930), false, false, "Кровать-круг");

        var json = JsonUtility.ToJson(ElementCapture.FromElement(bed));
        var restored = JsonUtility.FromJson<ElementData>(json);

        Assert.IsTrue(restored.isBed, "иначе кровать загрузится обычной доской");
        Assert.IsFalse(restored.bedDouble,
            "оба флага стартуют с true, поэтому забытая запись поля неотличима от "
            + "записанной; круг делается на НЕумолчальных значениях");
        Assert.IsFalse(restored.bedHeadboard, "то же для изголовья");
        Assert.AreEqual(new[] { 940, 640, 1930 }, restored.dimensionsMM,
            "ручной размер обязан пережить сохранение: сбрасывает его только переключатель "
            + "типа, а не загрузка");
    }

    [Test]
    public void ADuplicatedBed_ComesBackABed_WithTheSameTypeAndHeadboard()
    {
        var bed = Bed(new Vector3Int(940, 640, 1930), false, false, "Кровать-исходная");

        var copyGo = ElementDuplicators.Copy(ElementFactory.Instance, bed,
            bed.transform.position + Vector3.right);
        Assert.IsNotNull(copyGo, "дублирование обязано что-то вернуть");
        _spawned.Add(copyGo);

        var copy = copyGo.GetComponent<BedElement>();
        Assert.IsNotNull(copy,
            "пропуск в реестре дублирования не падает, а молча возвращает обычную доску");
        Assert.IsFalse(copy!.IsDouble, "копия обязана быть той же односпальной");
        Assert.IsFalse(copy.HasHeadboard, "и без спинки, как оригинал");
        Assert.AreEqual(bed.DimensionsMM, copy.DimensionsMM,
            "и того же размера: переключатели в фабрике обязаны отработать ДО габарита");
    }

    [Test]
    public void Bed_SizeName_FollowsTheType()
    {
        var bed = DefaultBed();
        Assert.AreEqual(BedElement.SizeDouble, bed.SizeName,
            "имя типа едет в MCP: агент читает его вместо того, чтобы гадать по ширине");

        bed.IsDouble = false;
        Assert.AreEqual(BedElement.SizeSingle, bed.SizeName, "и следует за переключателем");
    }
}
