using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Цена из дампа пользователя от 13.09: <c>ScrewLegHostLink.ApplyAll</c> —
/// <b>2,4 мс в КАЖДОМ кадре</b>, и покоя, и жеста, внутри
/// <c>SceneChangeTracker.SettleDerivedLinks</c>.
///
/// Предсказание, с которым я сюда шёл, оказалось НЕВЕРНЫМ, и это записано здесь
/// нарочно. Я ждал шестой раз форму «за деталь считаем по всей сцене» — 24 опоры,
/// каждая ищет хозяина перебором. Форма другая: <c>ApplyAll</c> собирает список
/// хозяев ОДИН раз и разрешает против него всех опор сразу, то есть по деталям он
/// линеен и всегда был. <c>AttachLinks.Parent</c> из него не зовётся вовсе.
///
/// Дорога другая часть: <c>CollectHosts</c> строил <c>ElementGeometry</c> на
/// КАЖДОГО кандидата в хозяева — а хозяином может быть почти любая деталь, то есть
/// ~400 сборок геометрии каждый кадр ради 24 опор. Это та же работа, что уже дважды
/// переехала на снимок прошлого прохода (<c>SnapSceneGeometry</c>,
/// <c>SceneFaces</c>), и лечится она так же: геометрия берётся из
/// <c>ElementSnapshotReuse</c> по штампу значений.
///
/// Калитку «вход не менялся» я СОЗНАТЕЛЬНО не ставлю, и причина конкретная.
/// Ключом была бы поза хозяина, но ответ зависит не только от позы: кандидат
/// проходит через <c>CanHost</c> → <c>AttachLinks.CanBeParent</c> →
/// <c>CanCarryAttachedParts</c>, а это ВИРТУАЛЬНОЕ свойство, которого в штампе нет.
/// Деталь может перестать быть годной в хозяева, не сдвинувшись ни на миллиметр, —
/// и калитка с таким ключом заморозила бы связь молча. Признак «не менялось» бывает
/// тихим; здесь он тихий.
///
/// Связь опоры с хозяином уезжает в сохранённый проект — через неё тянется длина
/// резьбы до пола. Поэтому счётчиков тут мало: у каждой опоры обязан остаться ТОТ ЖЕ
/// хозяин и ТОТ ЖЕ заход, а на перепривязке — пересчитаться.</summary>
public class ScrewLegHostGeometryReuseTests
{
    private const float U = AppConstants.MM_TO_UNITS;

    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void SetUp()
    {
        PartRegistry.Clear();
        FaceCache.Clear();
        ElementSnapshotReuse.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
        ElementSnapshotReuse.Clear();
    }

    private KitchenElement BottomPanel(string name, float x, float bottomMM)
    {
        var go = new GameObject(name);
        _spawned.Add(go);
        go.transform.position = new Vector3(x, (bottomMM + 9f) * U, 0f);
        var e = go.AddComponent<KitchenElement>();
        e.PartName = name;
        e.DimensionsMM = new Vector3Int(600, 18, 500);
        e.ApplyDimensions();
        PartRegistry.Register(e);
        return e;
    }

    private ScrewLegElement Leg(Vector3 pos)
    {
        var go = ElementFactory.CreateScrewLeg("Опора1", pos);
        _spawned.Add(go);
        return go.GetComponent<ScrewLegElement>();
    }

    private (KitchenElement left, KitchenElement right, ScrewLegElement leg) AScrewedLeg()
    {
        var left = BottomPanel("ДноЛевое", 0f, 150f);
        var right = BottomPanel("ДноПравое", 1f, 160f);
        var leg = Leg(new Vector3(0f, 0.100f, 0f));
        leg.SeatAfterMove(PartRegistry.GetAll());
        SceneChangeTracker.Poll();

        Assert.AreEqual("ДноЛевое", leg.HostPartName,
            "исходная посадка неверна — всё, что тест измерит дальше, ничего не значит");
        return (left, right, leg);
    }

    private static void WarmTheValidationPass()
        => ValidationSnapshot.Build(PartRegistry.GetAll(), new List<ValidationElement>());

    private static int RebuiltByOneApplyAll()
    {
        ScrewLegHostLink.ApplyAll(PartRegistry.GetAll());
        return ScrewLegHostLink.HostGeometriesRebuiltByLastApplyAll;
    }

    [Test]
    public void WithoutASnapshot_EveryCandidateHostIsBuiltAnew_ThatIsThePriceOfTheFrame()
    {
        AScrewedLeg();
        ElementSnapshotReuse.Clear();

        Assert.AreEqual(2, RebuiltByOneApplyAll(),
            "на холодном снимке геометрия хозяев собирается заново — это та самая "
            + "работа, которой на сцене пользователя приходилось ~400 сборок в кадр");
    }

    [Test]
    public void AfterAValidationPass_NoHostGeometryIsBuiltAgain()
    {
        AScrewedLeg();
        WarmTheValidationPass();

        Assert.AreEqual(0, RebuiltByOneApplyAll(),
            "снимок прошлого прохода лежит рядом и годен — пересобирать нечего");
    }

    [Test]
    public void TheCandidatesAreStillAllConsidered_EvenWhenNothingIsBuilt()
    {
        AScrewedLeg();
        WarmTheValidationPass();

        ScrewLegHostLink.ApplyAll(PartRegistry.GetAll());

        Assert.AreEqual(2, ScrewLegHostLink.HostGeometriesBuiltByLastApplyAll,
            "переиспользование не имеет права СОКРАЩАТЬ список кандидатов: "
            + "ноль здесь означал бы опору, которой не из кого выбирать");
    }

    [Test]
    public void AMovedHost_IsTheOnlyOneBuiltAgain()
    {
        var (left, _, _) = AScrewedLeg();
        WarmTheValidationPass();
        left.transform.position += new Vector3(0f, 0.001f, 0f);

        Assert.AreEqual(1, RebuiltByOneApplyAll(),
            "сдвинутый хозяин обязан прийти пересобранным, второй — из снимка");
    }

    [Test]
    public void TheHostAndTheInsertion_AreTheSame_WhetherTheGeometryWasReusedOrBuilt()
    {
        var (_, _, leg) = AScrewedLeg();

        ElementSnapshotReuse.Clear();
        ScrewLegHostLink.ApplyAll(PartRegistry.GetAll());
        string hostWhenBuilt = leg.HostPartName ?? "";
        int? insertionWhenBuilt = leg.InsertionIntoHostMM;

        WarmTheValidationPass();
        ScrewLegHostLink.ApplyAll(PartRegistry.GetAll());

        Assert.AreEqual(0, ScrewLegHostLink.HostGeometriesRebuiltByLastApplyAll,
            "фикстура собрана зря: второй проход обязан был идти по снимку");
        Assert.AreEqual(hostWhenBuilt, leg.HostPartName,
            "хозяин поменялся от того, ОТКУДА взялась геометрия: эта связь уезжает "
            + "в сохранённый проект, и менять её молча нельзя");
        Assert.AreEqual(insertionWhenBuilt, leg.InsertionIntoHostMM,
            "заход в корпус поменялся вместе с источником геометрии — через него "
            + "тянется длина резьбы до пола");
    }

    [Test]
    public void MovingTheLegToTheOtherBoard_RebindsIt_EvenWithAWarmSnapshot()
    {
        var (_, _, leg) = AScrewedLeg();
        WarmTheValidationPass();

        leg.transform.position = new Vector3(1f, 0.110f, 0f);
        SceneChangeTracker.Poll();

        Assert.AreEqual("ДноПравое", leg.HostPartName,
            "опору перенесли к другому корпусу, а связь осталась старой: снимок "
            + "заморозил хозяина — это и есть та тихая ложь, ради которой калитки здесь нет");
        Assert.AreEqual(25, leg.InsertionIntoHostMM,
            "и заход обязан пересчитаться по НОВОМУ хозяину, у которого дно на 160 мм");
    }

    [Test]
    public void ALegOverNoBoardAtAll_LosesItsHost()
    {
        var (_, _, leg) = AScrewedLeg();
        WarmTheValidationPass();

        leg.transform.position = new Vector3(5f, 0.100f, 0f);
        SceneChangeTracker.Poll();

        Assert.IsNull(leg.HostPartName,
            "опора уехала от всех корпусов — хозяина у неё быть не может");
    }
}
