using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>Подменю «Корпус» в окне свойств опоры: заход, «слева / справа» и
/// «сверху / снизу».
///
/// Числа ВЫВЕДЕНЫ из геометрии, писателя у них нет — панель спрашивает их у той
/// же функции, что считает заход (<c>ScrewLegSeat</c> → <c>ScrewLegHosting</c>),
/// и обновляется тем же поводом (<c>SceneChangeTracker</c>). Поэтому стенд
/// обязан ходить дорогой приложения: двигать трансформ и звать
/// <c>SceneChangeTracker.Poll</c>, а детали — регистрировать в
/// <c>PartRegistry</c> руками. В EditMode Unity не зовёт <c>Awake</c>, и без
/// регистрации опрос меряет пустую сцену; это уже стоило одного круга
/// (см. ScrewLegHostLinkReproTests).
///
/// Пара осей берётся у грани хозяина, в которую входит резьба: «слева/справа» —
/// вдоль её правой оси, «сверху/снизу» — вдоль верхней. У неповёрнутого дна
/// 600×18×500 это 600 мм и 500 мм соответственно.</summary>
public class ScrewLegHostSectionTests
{
    private const float U = AppConstants.MM_TO_UNITS;

    private readonly List<GameObject> _spawned = new List<GameObject>();

    private Canvas? _canvas;
    private ContextMenuUI? _menu;

    [SetUp]
    public void SetUp()
    {
        PartRegistry.Clear();
        UIFactory.EnsureEventSystem();
        _canvas = UIFactory.CreateCanvas("ScrewLegSectionCanvas");
        var go = new GameObject("CtxMenu");
        _menu = go.AddComponent<ContextMenuUI>();
        _menu.Build(_canvas.transform);
    }

    [TearDown]
    public void TearDown()
    {
        if (_menu != null) Object.DestroyImmediate(_menu.gameObject);
        if (_canvas != null) Object.DestroyImmediate(_canvas.gameObject);
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
        CommandStack.Clear();
    }

    private KitchenElement Bottom()
    {
        var go = new GameObject("Дно");
        _spawned.Add(go);
        go.transform.position = new Vector3(0f, (150f + 9f) * U, 0f);
        var e = go.AddComponent<KitchenElement>();
        e.PartName = "Дно";
        e.DimensionsMM = new Vector3Int(600, 18, 500);
        e.ApplyDimensions();
        PartRegistry.Register(e);
        return e;
    }

    private ScrewLegElement Leg(Vector3 position)
    {
        var go = ElementFactory.CreateScrewLeg("Опора1", position);
        _spawned.Add(go);
        var leg = go.GetComponent<ScrewLegElement>();
        if (!PartRegistry.GetAll().Contains(leg)) PartRegistry.Register(leg);
        return leg;
    }

    private ScrewLegElement SeatedLeg()
    {
        var bottom = Bottom();
        var leg = Leg(new Vector3(0f, 0.100f, 0f));
        Assert.That(PartRegistry.GetAll(), Has.Member(bottom).And.Member(leg),
            "обе детали обязаны быть в реестре: кадровый опрос ходит по нему, "
            + "и без регистрации тест померил бы пустую сцену");

        leg.SeatAfterMove(PartRegistry.GetAll());
        SceneChangeTracker.Poll();

        Assert.AreEqual("Дно", leg.HostPartName,
            "исходная посадка: без хозяина мерить нечего, и всё дальнейшее было бы прочерками");
        return leg;
    }

    private Transform Panel() => _canvas!.transform.Find("ContextMenu")!;

    private TMP_InputField Field(string node)
    {
        foreach (var t in Panel().GetComponentsInChildren<Transform>(true))
            if (t.name == node)
            {
                var field = t.GetComponent<TMP_InputField>();
                Assert.IsNotNull(field, "узел «" + node + "» есть, но это не поле ввода");
                return field!;
            }
        Assert.Fail("в панели нет узла «" + node + "» — секция не построилась");
        return null!;
    }

    private void Type(TMP_InputField field, string text)
    {
        ((IContextMenuHost)_menu!).Fields.ForgetLastApplyFrame();
        field.text = text;
        field.onEndEdit.Invoke(text);
    }

    [Test]
    public void UnderTheCentreOfTheBottom_TheFourNumbersAreItsHalves()
    {
        var leg = SeatedLeg();
        _menu!.Open(leg);

        Assert.AreEqual("300", Field("F_screwLeft").text, "половина 600 мм влево");
        Assert.AreEqual("300", Field("F_screwRight").text, "и столько же вправо");
        Assert.AreEqual("250", Field("F_screwTop").text,
            "вторая ось нижней грани — 500 мм глубины, половина её");
        Assert.AreEqual("250", Field("F_screwBottom").text, "и вторая половина");
    }

    [Test]
    public void WithNoHost_TheSectionShowsDashes_NotZeroes()
    {
        var leg = Leg(new Vector3(0f, 2f, 0f));
        SceneChangeTracker.Poll();
        Assert.IsNull(leg.HostPartName, "опора в воздухе: хозяина нет");

        _menu!.Open(leg);

        foreach (var node in new[] { "F_screwLeft", "F_screwRight", "F_screwTop", "F_screwBottom" })
        {
            var field = Field(node);
            Assert.AreEqual(ScrewLegHostSection.NoHostText, field.text,
                node + ": ноль читался бы как «стоит ровно на кромке», а опора висит в воздухе");
            Assert.IsFalse(field.interactable,
                node + ": править нечего, пока не от чего мерить");
        }
        Assert.AreEqual(ScrewLegHostSection.NoHostText, Field("F_Заход в корпус").text,
            "заход показывает прочерк по той же причине — правило одно на всю секцию");
    }

    [Test]
    public void TypingIntoLeft_MovesTheLeg_AndRightBecomesTheRestOfTheSpan()
    {
        var leg = SeatedLeg();
        _menu!.Open(leg);

        Type(Field("F_screwLeft"), "100");
        SceneChangeTracker.Poll();

        Assert.AreEqual(-200f * U, leg.transform.position.x, 0.0005f,
            "«слева» = 100 при ширине дна 600 ставит центр опоры на 100 мм от левой кромки, "
            + "то есть на −200 мм от середины: правка числа ДВИГАЕТ опору");
        Assert.AreEqual(500, leg.RightInHostMM,
            "противоположное число пересчиталось само: сумма пары — габарит хозяина");
        Assert.AreEqual("500", Field("F_screwRight").text,
            "и панель показала пересчитанное, а не то, что в ней стояло до правки");
    }

    /// <summary>Правка «справа» обязана двигать опору В ДРУГУЮ сторону. Без этого
    /// теста годился бы код, который обе половины пары трактует одинаково.</summary>
    [Test]
    public void TypingIntoRight_MovesTheLegTheOtherWay()
    {
        var leg = SeatedLeg();
        _menu!.Open(leg);

        Type(Field("F_screwRight"), "100");
        SceneChangeTracker.Poll();

        Assert.AreEqual(200f * U, leg.transform.position.x, 0.0005f,
            "«справа» = 100 ставит центр опоры на 100 мм от ПРАВОЙ кромки");
        Assert.AreEqual(500, leg.LeftInHostMM, "и «слева» стало остатком");
    }

    /// <summary>Ввод того же числа не двигает опору ни на микрон. Числа целые, а
    /// геометрия дробная: если бы сдвиг считался от ПОКАЗАННОГО (округлённого)
    /// значения, опора уползала бы на пол-миллиметра за каждое подтверждение.</summary>
    [Test]
    public void RetypingTheSameNumber_LeavesTheLegExactlyWhereItWas()
    {
        var leg = SeatedLeg();
        leg.transform.position += new Vector3(0.3f * U, 0f, 0f);
        SceneChangeTracker.Poll();
        _menu!.Open(leg);

        float before = leg.transform.position.x;
        string shown = Field("F_screwLeft").text;
        for (int i = 0; i < 3; i++)
        {
            Type(Field("F_screwLeft"), shown);
            SceneChangeTracker.Poll();
        }

        Assert.AreEqual(before, leg.transform.position.x, 1e-6f,
            "трижды подтверждён тот же округлённый «слева» — опора не сдвинулась. "
            + "Панель зовёт сеттер, только когда ЧИСЛО изменилось; сеттер, вызванный "
            + "с показанным округлением, подтянул бы опору на 0,3 мм при каждом Enter");
    }

    /// <summary>Число 60 мм из <see cref="ScrewLegSpec.MOUNT_DETENT_FROM_EDGE_MM"/>
    /// доезжает до ядра прилипания не ссылкой, а данными: ядро (Core/Geometry) не
    /// видит Pure, поэтому опора кладёт константу в свой снимок сама. Здесь
    /// сходятся два конца, которые иначе разъехались бы молча — свип
    /// ScrewLegMountDetentSweepTests проверяет ядро против собственного литерала.</summary>
    [Test]
    public void TheLegCarriesTheSixtyMillimetreDetent_IntoItsGeometrySnapshot()
    {
        var leg = Leg(new Vector3(0f, 0.100f, 0f));

        Assert.AreEqual(ScrewLegSpec.MOUNT_DETENT_FROM_EDGE_MM * U,
            leg.ToGeometry().MountEdgeDetentUnits, 1e-6f,
            "снимок опоры несёт 60 мм от кромки — то самое число, по которому ядро "
            + "строит посадки. Ноль здесь означал бы, что опора снова знает одну середину");
    }
}
