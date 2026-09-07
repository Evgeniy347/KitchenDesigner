using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Связь опоры с хозяином выведена из геометрии, а не выбрана человеком, —
/// значит она обязана пересчитываться КАЖДЫЙ раз, когда геометрия сдвинулась, а не
/// только на отпускании мыши (<c>ScrewLegAutoFit.Seat</c>), при загрузке проекта и
/// при дублировании.
///
/// Жалоба пользователя дословно: «поле "заход в корпус" не обновляется при
/// перемещении опоры от одной детали к другой». Панель читает число каждый кадр
/// (<c>ContextMenuUI.RefreshTransformFields</c> → <c>ScrewLegFieldsEditor.Refresh</c>),
/// поэтому виновата не панель, а устаревшее поле <c>AttachedToName</c>.
///
/// Тесты ходят той же дорогой, что и приложение: двигают трансформ и дают
/// отработать кадровому опросу <c>SceneChangeTracker.Poll</c> — той самой функции,
/// в которую упирается и мышиный драг, и мутация через MCP
/// (см. CONVENTIONS.md → «Not only the guard — every READER of a state must ask
/// through one function»).
///
/// Опрос ходит по <c>PartRegistry</c>, а не по списку, собранному тестом, поэтому
/// деталь, не попавшая в реестр, для него не существует. В EditMode Unity НЕ зовёт
/// <c>Awake</c> у добавленного компонента, и саморегистрация
/// <c>KitchenElement.Awake</c> не срабатывает — стенд обязан звать
/// <c>PartRegistry.Register</c> сам. Пока он этого не делал, весь класс уходил в
/// Inconclusive на <c>Assume</c> и не проверял НИЧЕГО: ни на сломанном коде, ни на
/// починенном. Отсюда два правила ниже — реестр проверяется явно
/// (<see cref="AssertPolled"/>), а предусловия стоят на <c>Assert</c>, а не на
/// <c>Assume</c>: неверная исходная посадка — это часть проверяемого поведения,
/// и она обязана краснеть (CONVENTIONS.md → «Prove the harness before you trust
/// what it measures», «If a test would not go red, DELETE it»).</summary>
public class ScrewLegHostLinkReproTests
{
    private const float U = AppConstants.MM_TO_UNITS;

    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void SetUp() => PartRegistry.Clear();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
    }

    private KitchenElement Board(string name, Vector3 pos, Vector3Int dims)
    {
        var go = new GameObject(name);
        _spawned.Add(go);
        go.transform.position = pos;
        var e = go.AddComponent<KitchenElement>();
        e.PartName = name;
        e.DimensionsMM = dims;
        e.ApplyDimensions();
        PartRegistry.Register(e);
        return e;
    }

    /// <summary>Первое утверждение стенда — про сам стенд: то, что мы двигаем,
    /// действительно лежит в реестре, по которому ходит кадровый опрос.</summary>
    private static void AssertPolled(params KitchenElement[] scene)
    {
        var registered = PartRegistry.GetAll();
        foreach (var e in scene)
            Assert.That(registered, Has.Member(e),
                $"«{e.PartName}» нет в PartRegistry — кадровый опрос её не увидит, "
                + "и всё, что тест измерит дальше, будет измерением пустой сцены");
    }

    /// <summary>Дно корпуса: панель 600×18×500, нижняя грань на заданной высоте.</summary>
    private KitchenElement BottomPanel(string name, float x, float bottomMM) =>
        Board(name, new Vector3(x, (bottomMM + 9f) * U, 0f), new Vector3Int(600, 18, 500));

    private ScrewLegElement Leg(Vector3 pos)
    {
        var go = ElementFactory.CreateScrewLeg("Опора1", pos);
        _spawned.Add(go);
        return go.GetComponent<ScrewLegElement>();
    }

    private static void Frame() => SceneChangeTracker.Poll();

    /// <summary>Левый корпус на 150 мм, правый на 160 мм, опора вкручена в левый.
    /// Разная высота дна выбрана нарочно: одинаковая скрыла бы половину дефекта —
    /// заход совпал бы по числу и тест остался бы зелёным против кода, который
    /// хозяина не переключает.</summary>
    private (KitchenElement left, KitchenElement right, ScrewLegElement leg) TwoBoardsAndASeatedLeg()
    {
        var left = BottomPanel("ДноЛевое", 0f, 150f);
        var right = BottomPanel("ДноПравое", 1f, 160f);
        var leg = Leg(new Vector3(0f, 0.100f, 0f));
        AssertPolled(left, right, leg);

        leg.SeatAfterMove(PartRegistry.GetAll());
        Frame();

        Assert.AreEqual("ДноЛевое", leg.HostPartName,
            "исходная посадка: неверный хозяин ДО перемещения обесценивает всё, "
            + "что тест измерит после, — это часть предмета теста, а не внешняя помеха");
        Assert.AreEqual(25, leg.InsertionIntoHostMM, "исходный заход — 25 мм");
        return (left, right, leg);
    }

    private static void DragTo(KitchenElement e, Vector3 position)
    {
        e.transform.position = position;
        Frame();
    }

    [Test]
    public void LegDraggedFromOneBoardToAnother_TakesTheNewBoardAsItsHost()
    {
        var (_, right, leg) = TwoBoardsAndASeatedLeg();

        DragTo(leg, new Vector3(1f, leg.transform.position.y, 0f));

        Assert.AreEqual(right.PartName, leg.HostPartName,
            "опора уехала под другую деталь — хозяином обязана стать она, "
            + "иначе прощение пересечения и заход в корпус остаются у бывшего хозяина");
    }

    [Test]
    public void LegDraggedFromOneBoardToAnother_ShowsTheInsertionIntoTheNewBoard()
    {
        var (_, _, leg) = TwoBoardsAndASeatedLeg();

        DragTo(leg, new Vector3(1f, leg.transform.position.y, 0f));

        Assert.AreEqual(15, leg.InsertionIntoHostMM,
            "резьба кончается на 175 мм, дно правого корпуса на 160 мм — заход 15 мм; "
            + "25 мм здесь означало бы, что панель показывает мерку по прежнему хозяину");
    }

    [Test]
    public void LegDraggedOutFromUnderEverything_ShowsADashInsteadOfTheLastLiveInsertion()
    {
        var (_, _, leg) = TwoBoardsAndASeatedLeg();

        DragTo(leg, new Vector3(3f, leg.transform.position.y, 0f));

        Assert.IsNull(leg.HostPartName, "над опорой не осталось ни одной детали");
        Assert.IsNull(leg.InsertionIntoHostMM,
            "без хозяина панель обязана показать прочерк: тихое последнее живое число "
            + "неотличимо от настоящего захода");
    }

    [Test]
    public void BoardMovedOverAHostlessLeg_BecomesItsHost()
    {
        var leg = Leg(new Vector3(0f, 0.029f, 0f));
        var board = BottomPanel("Дно", 2f, 50f);
        AssertPolled(leg, board);

        Frame();
        Assert.IsNull(leg.HostPartName, "пока деталь в стороне, хозяина нет");

        DragTo(board, new Vector3(0f, board.transform.position.y, 0f));

        Assert.AreEqual("Дно", leg.HostPartName,
            "двигают не опору, а деталь над ней — связь обязана появиться так же, "
            + "как если бы двигали опору");
        Assert.AreEqual(8, leg.InsertionIntoHostMM,
            "резьба заводской опоры кончается на 58 мм, дно детали на 50 мм");
    }

    [Test]
    public void HostShrunkAwayFromTheLeg_ClearsTheLink()
    {
        var (left, _, leg) = TwoBoardsAndASeatedLeg();
        DragTo(leg, new Vector3(0.200f, leg.transform.position.y, 0f));
        Assert.AreEqual("ДноЛевое", leg.HostPartName, "200 мм ещё под деталью 600 мм");

        var pos = left.transform.position;
        var rot = left.transform.rotation;
        CommandStack.Execute(new ResizeCommand(left, left.DimensionsMM,
            new Vector3Int(100, 18, 500), pos, pos, rot, rot));
        Frame();

        Assert.IsNull(leg.HostPartName,
            "хозяин сузился до 100 мм и ушёл из-под опоры — связь обязана очиститься, "
            + "а не пережить деталь, в которую резьба больше не входит");
        Assert.IsNull(leg.InsertionIntoHostMM);
    }

    private static bool OverlapReported(ValidationResult result, KitchenElement a, KitchenElement b)
    {
        if (result.diagnostics == null) return false;
        foreach (var d in result.diagnostics)
        {
            if (d.kind != ViolationKind.Overlap) continue;
            if ((d.element == a && d.other == b) || (d.element == b && d.other == a)) return true;
        }
        return false;
    }

    [Test]
    public void LegMovedToAnotherBoard_LeavesNoCollisionOnTheBoardItIsScrewedInto()
    {
        var (_, right, leg) = TwoBoardsAndASeatedLeg();

        DragTo(leg, new Vector3(1f, leg.transform.position.y, 0f));
        var result = ConstraintValidator.Validate(PartRegistry.GetAll());

        Assert.IsFalse(OverlapReported(result, leg, right),
            "прощение объёма выдаётся ТОЛЬКО спаренному хозяину; с устаревшей связью "
            + "оно уходит бывшему, и COL-01 загорается на детали, в которую опора "
            + "вкручена правильно");
    }

    /// <summary>Положительный контроль к предыдущему на ТОЙ ЖЕ сцене: сенсор
    /// <see cref="OverlapReported"/> умеет сказать «да». Деталь входит ПОЗЖЕ хозяина
    /// (дно на 170 мм против 150 мм), поэтому хозяином не становится, а резьба,
    /// кончающаяся на 175 мм, в неё попадает.</summary>
    [Test]
    public void ThreadReachingABoardThatIsNotItsHost_IsReportedAsAnOverlap()
    {
        var (_, _, leg) = TwoBoardsAndASeatedLeg();
        var stranger = BottomPanel("Чужая", 0f, 170f);
        AssertPolled(stranger);

        Frame();
        Assert.AreEqual("ДноЛевое", leg.HostPartName, "хозяином остаётся тот, кто ниже");

        var result = ConstraintValidator.Validate(PartRegistry.GetAll());

        Assert.IsTrue(OverlapReported(result, leg, stranger),
            "чужой детали объём не отдан: резьба в ней — это COL-01");
    }

    /// <summary>Парная к предыдущим: пересчёт на каждом кадре не имеет права ГАСИТЬ
    /// живую связь. Без неё «чинить» баг можно было бы, просто очищая хозяина.</summary>
    [Test]
    public void LegLeftWhereItIs_KeepsItsHostAcrossFrames()
    {
        var (left, _, leg) = TwoBoardsAndASeatedLeg();

        Frame();
        Frame();

        Assert.AreEqual(left.PartName, leg.HostPartName,
            "никто ничего не двигал — связь обязана остаться прежней");
        Assert.AreEqual(25, leg.InsertionIntoHostMM);
    }

    /// <summary>Удаление детали — такой же повод пересчитать выведенное поле, как и её
    /// перемещение, но поз при нём никто не трогает, и покадровый опрос по
    /// <c>transform.hasChanged</c> его не видит: удалённой детали в реестре уже нет,
    /// а у оставшихся ничего не сдвинулось. Опора остаётся с именем детали, которой в
    /// сцене больше нет.
    ///
    /// Прочерк в панели этого не выдаёт: <c>AttachLinks.Parent</c> не находит
    /// исчезнувшую деталь, и <c>InsertionIntoHostMM</c> возвращает null на устаревшем
    /// имени тоже. Поэтому проверять надо ИМЯ (<c>HostPartName</c>), а не мерку —
    /// на мерке тест был бы зелёным против сломанного кода. Цена устаревшего имени
    /// платится позже: первая же деталь с тем же именем получит и прощение объёма,
    /// и заход, которых не заслужила, а сохранение проекта унесёт битую связь на диск
    /// (CONVENTIONS.md → «A derived field needs ONE writer and one occasion to call
    /// it»).
    ///
    /// Удаление идёт через <c>DeleteCommand</c>, а не через <c>DestroyImmediate</c>:
    /// это дорога пользователя, и только она проходит через реестр гарантированно —
    /// в EditMode Unity не зовёт у добавленного компонента ни <c>Awake</c>, ни
    /// <c>OnDestroy</c>, так что саморегистрация и саморазрегистрация тут не
    /// работают.</summary>
    [Test]
    public void HostDeleted_ClearsTheLegsLink()
    {
        var (left, _, leg) = TwoBoardsAndASeatedLeg();

        CommandStack.Execute(new DeleteCommand(left.gameObject));
        Frame();

        Assert.IsNull(leg.HostPartName,
            "деталь удалили, а опора всё ещё числит её хозяином: имя пережило саму "
            + "деталь и ждёт тёзку, чтобы отдать ей чужую связь");
    }

    /// <summary>Парная к предыдущей: повод «элемент вышел из реестра» обязан
    /// ПЕРЕСЧИТЫВАТЬ связь, а не гасить её. Без этой проверки дыру выше можно было бы
    /// «закрыть», очищая хозяина на любом удалении, и она осталась бы зелёной.</summary>
    [Test]
    public void ABystanderDeleted_LeavesTheLegOnItsHost()
    {
        var (left, right, leg) = TwoBoardsAndASeatedLeg();

        CommandStack.Execute(new DeleteCommand(right.gameObject));
        Frame();

        Assert.AreEqual(left.PartName, leg.HostPartName,
            "удалили деталь, под которой опора не стояла, — связь трогать не за что");
        Assert.AreEqual(25, leg.InsertionIntoHostMM,
            "и заход у прежнего хозяина остался прежним");
    }

    /// <summary>Обратный повод той же дыры: деталь ВОШЛА в реестр, и опять без единого
    /// сдвига трансформа — отмена удаления возвращает деталь ровно туда, где она
    /// стояла. Сценарий выбран так, чтобы связь была обязана ПОЯВИТЬСЯ, а не
    /// сохраниться: опору сначала увозят из-под всех, деталь удаляют, опору возвращают
    /// на пустое место (хозяина нет и быть не может), и только потом отменяют
    /// удаление. Последний кадр не двигает ничего — весь повод в том, что состав сцены
    /// изменился.</summary>
    [Test]
    public void HostBroughtBackByUndo_TakesTheLegAgain()
    {
        var (left, _, leg) = TwoBoardsAndASeatedLeg();

        DragTo(leg, new Vector3(3f, leg.transform.position.y, 0f));
        Assert.IsNull(leg.HostPartName, "опора отъехала от всех деталей");

        CommandStack.Execute(new DeleteCommand(left.gameObject));
        Frame();

        DragTo(leg, new Vector3(0f, leg.transform.position.y, 0f));
        Assert.IsNull(leg.HostPartName,
            "опора вернулась на прежнее место, но детали на нём больше нет");

        CommandStack.Undo();
        Frame();

        Assert.AreEqual("ДноЛевое", leg.HostPartName,
            "отмена вернула деталь в реестр прямо над опорой — связь обязана появиться "
            + "так же, как если бы деталь пододвинули");
        Assert.AreEqual(25, leg.InsertionIntoHostMM,
            "и заход считается по вернувшейся детали, а не остаётся прочерком");
    }
}
