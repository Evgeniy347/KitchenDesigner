using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

/// <summary>
/// Два унитаза как ЖИВЫЕ элементы сцены — то, чего арифметика раскладки
/// (ToiletLayoutTests, WallHungToiletLayoutTests) не видит: имена детей,
/// коллайдер, единичный масштаб корня, порядок в фабрике, дублирование и
/// посадка подвесного на стену.
///
/// Почему это два ОТДЕЛЬНЫХ типа, а не один с переключателем: подвесной
/// крепится к стене (IWallMounted — сам поворачивается и садится на плоскость),
/// напольный просто стоит на полу. Флаг внутри одного класса означал бы, что
/// половину времени элемент реализует интерфейс, которому не отвечает, а каждый
/// реестр проекта ветвится через is XxxElement и не умеет спрашивать «а сейчас
/// ты какой».
///
/// Третье, что здесь удерживается, — ФИЗИЧЕСКОЕ пространство: корневой
/// localScale обязан оставаться единичным, а габарит уезжает в привязку через
/// EffectiveScale. Контур чаши строится из окружностей в миллиметрах, и
/// неравномерное масштабирование корня превратило бы их в эллипсы. Этот проект
/// наступал на такое трижды.
/// </summary>
public class ToiletElementTests
{
    private const float Tol = 1e-4f;

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

    private T Add<T>(GameObject go) where T : KitchenElement
    {
        _spawned.Add(go);
        return go.GetComponent<T>();
    }

    private ToiletElement Toilet(int seatHeightMM = ToiletElement.DefaultSeatHeightMM) =>
        Add<ToiletElement>(ElementFactory.CreateToilet(seatHeightMM, "Унитаз-1", Vector3.zero));

    private WallHungToiletElement WallHung(
        int seatHeightMM = WallHungToiletElement.DefaultSeatHeightMM,
        int plateHeightMM = WallHungToiletElement.DefaultFlushPlateHeightMM) =>
        Add<WallHungToiletElement>(ElementFactory.CreateWallHungToilet(seatHeightMM,
            plateHeightMM, "Инсталляция-1", Vector3.zero));

    private static Transform Child(KitchenElement element, string name)
    {
        var child = element.transform.Find(name);
        Assert.IsNotNull(child, "деталь — ИМЕНОВАННЫЙ ребёнок, и её имя есть часть "
            + "контракта FurniturePartSet: поиск по индексу однажды уже уронил тайлинг "
            + "декора на ножку стола. Не найдено: " + name);
        return child!;
    }

    [Test]
    public void Toilet_HasEveryCeramicPartAndTheButton_AsNamedChildren()
    {
        var toilet = Toilet();

        foreach (var name in new[] { ToiletLayout.PedestalName, ToiletLayout.BowlName,
            ToiletLayout.SeatName, ToiletLayout.LidName, ToiletLayout.CisternName,
            ToiletLayout.ButtonName })
            Assert.IsNotNull(Child(toilet, name).GetComponent<MeshFilter>(),
                "у детали обязан быть меш, иначе она невидима: " + name);
    }

    [Test]
    public void WallHungToilet_HasTheBowlSeatLidPlateAndTwoButtons()
    {
        var toilet = WallHung();

        foreach (var name in new[] { WallHungToiletLayout.BowlName,
            WallHungToiletLayout.SeatName, WallHungToiletLayout.LidName,
            WallHungToiletLayout.PlateName, WallHungToiletLayout.ButtonLargeName,
            WallHungToiletLayout.ButtonSmallName })
            Assert.IsNotNull(Child(toilet, name).GetComponent<MeshFilter>(),
                "у детали обязан быть меш, иначе она невидима: " + name);
    }

    [Test]
    public void BothToilets_KeepTheRootScaleAtOne()
    {
        foreach (var toilet in new KitchenElement[] { Toilet(), WallHung() })
            Assert.AreEqual(Vector3.one, toilet.transform.localScale,
                "составной элемент строится в миллиметрах и НЕ масштабирует корень: "
                + "иначе дети масштабируются дважды, а круглый план чаши становится "
                + "эллипсом — " + toilet.DisplayTypeName);
    }

    [Test]
    public void BothToilets_RefuseToBeResized()
    {
        foreach (var toilet in new KitchenElement[] { Toilet(), WallHung() })
        {
            var before = toilet.DimensionsMM;
            toilet.DimensionsMM = new Vector3Int(before.x + 300, before.y + 300, before.z + 300);

            Assert.AreEqual(before, toilet.DimensionsMM,
                "габарит фиксирован как у встраиваемой техники: элемент обязан вернуть "
                + "свой собственный размер, а не принять чужой — " + toilet.DisplayTypeName);
        }
    }

    [Test]
    public void BothToilets_CarryABoxColliderOverTheWholeEnvelope()
    {
        foreach (var toilet in new KitchenElement[] { Toilet(), WallHung() })
        {
            var box = toilet.GetComponent<BoxCollider>();
            Assert.IsNotNull(box, "без коллайдера элемент нельзя ни выделить, ни двигать: "
                + toilet.DisplayTypeName);

            var dims = toilet.DimensionsMM;
            Assert.AreEqual(dims.y * AppConstants.MM_TO_UNITS, box!.size.y, Tol,
                "коллайдер накрывает ВЕСЬ габарит, а не одну деталь: у подвесного это "
                + "в том числе пустота под чашей, которую занимает инсталляция — "
                + toilet.DisplayTypeName);
            Assert.AreEqual(Vector3.zero, box.center,
                "габарит центрирован на элементе — " + toilet.DisplayTypeName);
        }
    }

    /// <summary>Порядок в фабрике. Свойства подрезаются габаритом, и на пустом
    /// компоненте до присвоения размеров подрезка считает от нуля: значение
    /// схлопывается молча, а не отказывает.</summary>
    [Test]
    public void Factory_SetsTheDimensionsBeforeTheProperties()
    {
        var toilet = Toilet(470);
        Assert.AreEqual(470, toilet.SeatHeightMM,
            "заказанная высота чаши обязана уцелеть: если бы габарит выставлялся ПОСЛЕ "
            + "неё, подрезка отработала бы на нулевом размере");
        Assert.AreEqual(ToiletElement.ModelDimensionsMM, toilet.DimensionsMM);

        var hung = WallHung(520, 730);
        Assert.AreEqual(520, hung.SeatHeightMM, "то же у подвесного");
        Assert.AreEqual(730, hung.FlushPlateHeightMM,
            "и высота панели тоже: она подрезается высотой чаши, поэтому чаша обязана "
            + "быть выставлена раньше");
    }

    [Test]
    public void WallHungToilet_RaisingTheBowl_PushesThePlateUp_ButNeverBack()
    {
        var toilet = WallHung();
        int atRest = toilet.FlushPlateHeightMM;

        toilet.SeatHeightMM = WallHungToiletElement.MaxSeatHeightMM;
        int pushed = toilet.FlushPlateHeightMM;
        Assert.Greater(pushed, atRest,
            "панель обязана уехать вверх вместе с чашей, иначе она садится на крышку");

        toilet.SeatHeightMM = WallHungToiletElement.MinSeatHeightMM;
        Assert.AreEqual(pushed, toilet.FlushPlateHeightMM,
            "а обратно панель САМА не опускается: подрезка — не пружина, и опущенная "
            + "чаша не имеет права переставлять то, что человек выставил руками");
    }

    [Test]
    public void Duplicating_ACompactToilet_KeepsItsTypeSeatHeightAndBothDecorSlots()
    {
        var source = Toilet(455);
        source.PrimaryMaterialId = "oak";
        source.SecondaryMaterialId = "concrete";

        var copy = Add<ToiletElement>(ElementDuplicators.Copy(
            ElementFactory.Instance, source, new Vector3(1f, 0.395f, 0f)));

        Assert.IsNotNull(copy, "копия унитаза обязана остаться унитазом, а не выйти доской");
        Assert.AreEqual(455, copy.SeatHeightMM, "высота чаши обязана доехать до копии");
        Assert.AreEqual("oak", copy.PrimaryMaterialId, "керамика обязана доехать");
        Assert.AreEqual("concrete", copy.SecondaryMaterialId,
            "и второй слот тоже: именно его теряла ветка дублирования стола");
    }

    [Test]
    public void Duplicating_AWallHungToilet_KeepsBothOfItsHeights()
    {
        var source = WallHung(505, 720);

        var copy = Add<WallHungToiletElement>(ElementDuplicators.Copy(
            ElementFactory.Instance, source, new Vector3(1f, 0.5f, 0f)));

        Assert.IsNotNull(copy, "копия обязана остаться подвесным унитазом");
        Assert.AreEqual(505, copy.SeatHeightMM, "высота чаши обязана доехать до копии");
        Assert.AreEqual(720, copy.FlushPlateHeightMM,
            "и высота панели: два числа теряются по одному, и потерянное второе "
            + "выглядит как «панель почему-то на месте по умолчанию»");
    }

    [Test]
    public void WallHungToilet_TurnsItsBackToTheNearestWall_AndStandsOffByHalfItsDepth()
    {
        var wallGo = ElementFactory.CreateWall(new Vector3Int(3000, 2500, 100), "Стена",
            new Vector3(0f, 1.25f, 2f));
        _spawned.Add(wallGo);

        var toilet = WallHung();
        toilet.transform.position = new Vector3(0.4f, 0.5f, 1.2f);
        toilet.SnapToWall();

        float toU = AppConstants.MM_TO_UNITS;
        float expectedZ = 2f - (100 * 0.5f + WallHungToiletLayout.DepthMM * 0.5f) * toU;

        Assert.AreEqual(expectedZ, toilet.transform.position.z, 1e-3f,
            "задняя грань элемента обязана лечь на плоскость стены: отступ — полтолщины "
            + "стены плюс полглубины унитаза, и ни то ни другое не отгадать по месту");
        Assert.AreEqual(0.4f, toilet.transform.position.x, 1e-3f,
            "вдоль стены унитаз ездит свободно — привязка не имеет права его центрировать");
        Assert.AreEqual(0.5f, toilet.transform.position.y, 1e-3f,
            "высоту решает габарит от пола, а не стена");

        Assert.AreEqual(180f, Mathf.Abs(Mathf.DeltaAngle(
            toilet.transform.rotation.eulerAngles.y, 0f)), 0.1f,
            "унитаз стоит перед стеной со стороны -Z... то есть смотрит от неё: его "
            + "перёд обязан быть развёрнут прочь от стены, а не в неё");
    }

    [Test]
    public void WallHungToilet_WithNoWallInTheScene_StaysWhereItWasPut()
    {
        var toilet = WallHung();
        var placed = new Vector3(1.3f, 0.5f, -0.7f);
        toilet.transform.position = placed;

        toilet.SnapToWall();

        Assert.AreEqual(placed, toilet.transform.position,
            "без стены привязке не к чему цепляться, и молча утащить элемент в ноль "
            + "она не вправе");
    }

    [Test]
    public void BothToilets_AnswerTheirOwnSelectorName_NotTheBoardFallback()
    {
        Assert.AreEqual("toilet",
            KitchenDesigner.Core.Bulk.ElementSelector.TypeOf(Toilet()),
            "напольный унитаз обязан называться собой: провалившись в «board», он попадёт "
            + "в чужую массовую выборку all_boards");
        Assert.AreEqual("wall_hung_toilet",
            KitchenDesigner.Core.Bulk.ElementSelector.TypeOf(WallHung()),
            "и подвесной отдельно от него: одно имя на два типа сделало бы селектор "
            + "type: неспособным их различить");
    }

    [Test]
    public void BothToilets_NameTheirOwnDecorSurface_InsteadOfTheFirstRendererFound()
    {
        var toilet = Toilet();
        Assert.AreSame(Child(toilet, ToiletLayout.BowlName).GetComponent<MeshRenderer>(),
            toilet.DecorRenderer,
            "поверхность декора обязана быть НАЗВАНА элементом: "
            + "GetComponentInChildren означает «первый попавшийся», и на составном "
            + "элементе это решает порядок детей, а не смысл");

        var hung = WallHung();
        Assert.AreSame(Child(hung, WallHungToiletLayout.BowlName).GetComponent<MeshRenderer>(),
            hung.DecorRenderer, "то же у подвесного");
    }

    [Test]
    public void BothToilets_DestroyTheirChildren_WhenTheElementGoes()
    {
        var toilet = Toilet();
        var children = Enumerable.Range(0, toilet.transform.childCount)
            .Select(i => toilet.transform.GetChild(i).gameObject).ToList();
        Assert.IsNotEmpty(children, "предусловие: детали вообще были построены");

        toilet.PrepareForDestruction();
        Object.DestroyImmediate(toilet.gameObject);

        foreach (var child in children)
            Assert.IsTrue(child == null,
                "деталь пережила свой элемент: осиротевшие меши остаются в сцене и "
                + "текут между тестами");
    }
}
