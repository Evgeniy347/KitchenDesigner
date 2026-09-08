using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;
using KitchenDesigner.Core.Plumbing;

/// <summary>Задача C: выбор детали на конце трубы обязан не только замкнуть
/// обязательный стык с трубой, но и, если рядом стоит свободный порт другого узла
/// (например, «Подача»), довернуть новую деталь так, чтобы закрыть и его —
/// правило «максимум связей» из <see cref="PipeFittingSeatChoice"/>.
///
/// Геометрия здесь — не догадка, а число, которое уже проверено отдельно
/// (<c>PipeFittingSeatChoiceTests</c>, дотнет-набор): у отвода нулевой порт садится
/// на трубу, а первый — второе плечо — после доворота на 90° оказывается ровно в
/// точке <c>(0, −нога, −нога)</c> от устья трубы, глядя «назад». Оба теста ниже
/// ставят соседа В ЭТУ ЖЕ точку и различаются только осью его свободного порта —
/// это и есть пара «может дотянуться» / «не может», а не два независимых сценария.</summary>
public class PipeEndFittingsMaximizeLinksTests : SnapTestBase
{
    private const int PipeLengthMm = 600;
    private const int LowerEnd = 0;

    [SetUp]
    public void ClearFaceCacheBeforeTest() => FaceCache.Clear();

    [TearDown]
    public void ClearScene()
    {
        CommandStack.Clear();
        PartRegistry.Clear();
        ElementFactory.ClearPools();
        FaceCache.Clear();
    }

    private static float Units(float mm) => mm * AppConstants.MM_TO_UNITS;

    private PipeElement PipeWithItsLowerEndAt(Vector3 end, string name)
    {
        var go = ElementFactory.CreatePipe(PipeSpec.DEFAULT_SIZE, PipeLengthMm, name,
            end + new Vector3(0f, Units(PipeLengthMm * 0.5f), 0f));
        _spawned.Add(go);
        return go.GetComponent<PipeElement>();
    }

    private PipeFittingElement PodachaFacing(Vector3 portPosition, Vector3 portOutward)
    {
        var go = ElementFactory.CreatePipeSupply("Podacha", portPosition);
        _spawned.Add(go);
        var podacha = go.GetComponent<PipeFittingElement>();
        PipeDocking.SeatPort(podacha, 0, podacha.transform.position, podacha.transform.rotation,
            new SnapPort(portPosition, -portOutward));
        return podacha;
    }

    private static int JoinedLinks() =>
        PipeNetwork.Build(new ScenePipeSnapshot(PartRegistry.GetAll()).Ports()).Links.Count;

    private static int OpenEnds() =>
        PipeRules.Collect(new ScenePipeSnapshot(PartRegistry.GetAll()))
            .Count(f => f.Code == PipeIssueCatalog.CodeOpenEnd);

    private static PipeFittingElement? FittingOn(PipeElement pipe, int end) =>
        PipeEndFittings.NeighbourAt(pipe, end, PartRegistry.GetAll()) as PipeFittingElement;

    [Test]
    public void ChoosingAnElbow_AlsoClosesABonusLink_WhenItsSecondArmCanReachTheNeighbour()
    {
        var pipe = PipeWithItsLowerEndAt(Vector3.zero, "Run");
        float leg = PipeFittingSpec.LegLengthMm(PipeSpec.DEFAULT_SIZE);
        Vector3 target = pipe.EndAUnits + new Vector3(0f, -leg, -leg) * AppConstants.MM_TO_UNITS;
        PodachaFacing(target, Vector3.forward);

        var outcome = PipeEndFittings.Set(pipe, LowerEnd, PipeNodeKind.Elbow, PartRegistry.GetAll());
        foreach (var e in PartRegistry.GetAll())
            if (!_spawned.Contains(e.gameObject)) _spawned.Add(e.gameObject);

        Assert.AreEqual(PipeEndEdit.Changed, outcome);
        var elbow = FittingOn(pipe, LowerEnd);
        Assert.IsNotNull(elbow, "отвод обязан появиться на конце трубы");
        Assert.AreEqual(2, JoinedLinks(),
            "выбор из списка закрыл и обязательный стык с трубой, и бонусный — со "
            + "свободным портом Podacha, который стоит там, куда доворот на 90° кладёт "
            + "второе плечо отвода");
        Assert.AreEqual(1, OpenEnds(), "открыт остался только верхний конец трубы");
    }

    [Test]
    public void ChoosingAnElbow_DoesNotCountABonusLink_WhenTheNeighbourPortFacesTheSameWay()
    {
        var pipe = PipeWithItsLowerEndAt(Vector3.zero, "Run");
        float leg = PipeFittingSpec.LegLengthMm(PipeSpec.DEFAULT_SIZE);
        Vector3 target = pipe.EndAUnits + new Vector3(0f, -leg, -leg) * AppConstants.MM_TO_UNITS;
        PodachaFacing(target, Vector3.back);

        var outcome = PipeEndFittings.Set(pipe, LowerEnd, PipeNodeKind.Elbow, PartRegistry.GetAll());
        foreach (var e in PartRegistry.GetAll())
            if (!_spawned.Contains(e.gameObject)) _spawned.Add(e.gameObject);

        Assert.AreEqual(PipeEndEdit.Changed, outcome);
        Assert.AreEqual(1, JoinedLinks(),
            "порт соседа стоит в ТОЙ ЖЕ точке, что и в предыдущем тесте, но смотрит В ТУ ЖЕ "
            + "сторону, что и плечо отвода — это наложение, а не стык, и бонус не засчитывается");
        Assert.AreEqual(3, OpenEnds(),
            "открыты верхний конец трубы, второе плечо отвода — И порт Podacha: без "
            + "бонусного стыка её единственный порт тоже никуда не подключён");
    }
}
