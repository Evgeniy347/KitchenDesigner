using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;
using KitchenDesigner.Core.Plumbing;

/// <summary>РАЗВЕДКА на НАСТОЯЩИХ элементах сцены: тот же вопрос, но по пути пользователя.
///
/// Соседний <c>PipeFittingSnapProbeTests</c> считает быстро и без Unity, собирая
/// коробки руками. Это удобно и это же его слабое место: коробка, собранная в тесте,
/// может разойтись с той, что отдаёт элемент. Поэтому здесь всё то же самое едет
/// через <c>ElementFactory</c>, <c>SnapSystem.TrySnap</c> и <c>ScenePipeSnapshot</c> —
/// ровно те классы, которые работают при перетаскивании мышью. Расхождение между
/// двумя файлами и будет находкой.
///
/// Здесь же — ответ на вопрос «что скажет агенту <c>snap_diagnose</c>». Он скажет
/// «OK — прилипнет», и это правда про коробки и неправда про трассу: у уголка после
/// прилипания порты остаются свободными, и <c>PIP-01</c> продолжает ругаться. Ни один
/// из двух отчётов при этом не врёт — они отвечают на разные вопросы, и сегодня ничто
/// в проекте эти ответы не сводит.
///
/// Имена элементов ЛАТИНСКИЕ: <c>ElementNaming.Rule</c> пропускает в PartName только
/// латиницу, и кириллическое имя доедет до отчёта транслитерированным — сверять с
/// ним значит проверять транслитерацию, а не снэп.</summary>
public class PipeFittingSnapSceneProbeTests : SnapTestBase
{
    private const int PipeLengthMm = 600;

    private const float ApproachGapMm = 10f;

    [TearDown]
    public void ClearRegistry()
    {
        PartRegistry.Clear();
        ElementFactory.ClearPools();
    }

    private static float Units(float mm) => mm * AppConstants.MM_TO_UNITS;

    private PipeElement StandingPipe()
    {
        var go = ElementFactory.CreatePipe(PipeSpec.DEFAULT_SIZE, PipeLengthMm, "Run",
            new Vector3(0f, Units(PipeLengthMm * 0.5f), 0f));
        _spawned.Add(go);
        return go.GetComponent<PipeElement>();
    }

    private PipeFittingElement UnderThatPipe(GameObject go)
    {
        _spawned.Add(go);
        var fitting = go.GetComponent<PipeFittingElement>();
        fitting.transform.position =
            new Vector3(0f, -Units(fitting.DimensionsMM.y * 0.5f + ApproachGapMm), 0f);
        return fitting;
    }

    private PipeFittingElement CouplingUnderThePipe() =>
        UnderThatPipe(ElementFactory.CreatePipeCoupling("Cpl", Vector3.zero));

    private PipeFittingElement ElbowUnderThePipe() =>
        UnderThatPipe(ElementFactory.CreatePipeElbow("Elb", Vector3.zero));

    private static SnapResult SnapOnto(KitchenElement moved, KitchenElement other) =>
        SnapSystem.TrySnap(moved, new List<KitchenElement> { other }, moved.transform.position);

    private static int OpenEnds(params KitchenElement[] scene) =>
        PipeRules.Collect(new ScenePipeSnapshot(scene))
            .Count(f => f.Code == PipeIssueCatalog.CodeOpenEnd);

    private static int JoinedLinks(params KitchenElement[] scene) =>
        PipeNetwork.Build(new ScenePipeSnapshot(scene).Ports()).Links.Count;

    [Test]
    public void PipeCoupling_BroughtUnderAPipeEnd_SnapsThroughSnapSystem_AndTheScenePortsJoin()
    {
        var pipe = StandingPipe();
        var coupling = CouplingUnderThePipe();

        var snap = SnapOnto(coupling, pipe);

        Assert.IsTrue(snap.snapped, "муфта под торцом трубы прилипает через штатный SnapSystem");
        coupling.transform.position = snap.position;

        Assert.AreEqual(1, JoinedLinks(pipe, coupling),
            "и попутно сходятся порты: устье муфты лежит в центре торцевой грани её "
            + "коробки, поэтому выравнивание коробок случайно оказывается и выравниванием "
            + "устьев. Именно поэтому муфта «как-то работает», а уголок нет");
        Assert.AreEqual(2, OpenEnds(pipe, coupling),
            "свободными остаются только верхний конец трубы и нижнее устье муфты");
    }

    [Test]
    public void PipeElbow_BroughtUnderAPipeEnd_SnapsVisually_ButEveryPortStaysOpen()
    {
        var pipe = StandingPipe();
        var elbow = ElbowUnderThePipe();

        var snap = SnapOnto(elbow, pipe);

        Assert.IsTrue(snap.snapped, "уголок прилипает ТОЧНО ТАК ЖЕ, как муфта — снэп не "
            + "различает типы фитингов, он ровняет коробки");
        elbow.transform.position = snap.position;

        Assert.AreEqual(0, JoinedLinks(pipe, elbow),
            "но ни одно устье уголка не попало на торец трубы: у уголка устье смещено "
            + "вдоль своей грани, и после посадки коробок оно оказывается в 11,7 мм при "
            + "допуске 0,5 мм");
        Assert.AreEqual(4, OpenEnds(pipe, elbow),
            "и трасса остаётся не собранной: PIP-01 на оба конца трубы и на оба порта "
            + "уголка. Вот тот самый разрыв между «прижалось» и «соединилось»");
    }

    [Test]
    public void SnapDiagnose_ForAnElbowAtAPipeEnd_SaysItWillStick_AndKnowsNothingAboutPorts()
    {
        var pipe = StandingPipe();
        var elbow = ElbowUnderThePipe();

        var diagnosis = SnapSystem.Diagnose(elbow, new List<KitchenElement> { pipe },
            elbow.transform.position);

        Assert.IsTrue(diagnosis.wouldSnap, "диагноз согласен с TrySnap");
        Assert.AreEqual("Run", diagnosis.snapTarget);
        Assert.AreEqual(1, diagnosis.neighbors.Count);
        StringAssert.Contains("OK", diagnosis.neighbors[0].verdict,
            "именно это получит агент из MCP-инструмента snap_diagnose — и это правдивый "
            + "ответ про КОРОБКИ. Про то, соединятся ли порты, вопроса ему никто не задавал "
            + "и ответить он не может: в SnapDiagnosis нет ни одного поля про трассу. "
            + "Оракул не врёт — он просто не про то, и следующий агент должен знать об этом "
            + "до того, как поверит зелёному «OK»");
        Assert.AreEqual(ApproachGapMm, diagnosis.neighbors[0].gapMM, 0.5f,
            "и меряет он зазор между ГАБАРИТАМИ, а не между устьями: поднос на 10 мм видно, "
            + "а 11,7 мм расхождения портов — нет");
    }

    [Test]
    public void PipeAndFitting_AreOrdinaryParts_NotCentringOnesLikeAScrewLeg()
    {
        var pipe = StandingPipe();
        var elbow = ElbowUnderThePipe();
        var legGo = ElementFactory.CreateScrewLeg("Leg", new Vector3(0f, -Units(400f), 0f));
        _spawned.Add(legGo);

        Assert.IsFalse(pipe.ToGeometry().CentresOnTarget,
            "труба — обычная деталь: MountNormalOf в ElementGeometryExtensions выдаёт "
            + "ненулевую нормаль только для ScrewLegElement");
        Assert.IsFalse(elbow.ToGeometry().CentresOnTarget,
            "и фитинг тоже обычный. Это ответ на вопрос «а не центруется ли он на цели, "
            + "как винтовая опора»: нет, и потому в отборе участвуют ВСЕ шесть граней его "
            + "коробки, а не грани оси крепления");
        Assert.IsTrue(legGo.GetComponent<ScrewLegElement>().ToGeometry().CentresOnTarget,
            "положительный контроль: механизм центрующейся детали в проекте есть и "
            + "работает — просто трубы в него не включены");
    }

    [Test]
    public void PipeAndFitting_MayBeAttachedToEachOther_ButSnappingLinksNothing()
    {
        var pipe = StandingPipe();
        var coupling = CouplingUnderThePipe();

        Assert.IsTrue(AttachLinks.CanAttach(coupling, pipe),
            "механизм «прикреплённая деталь едет за хозяином» для пары «фитинг на трубе» "
            + "годится как есть: ни PipeElement, ни PipeFittingElement не запрещают себе "
            + "ни быть ребёнком, ни быть родителем");
        Assert.IsTrue(AttachLinks.CanChooseParent(coupling),
            "и связь у фитинга НЕ производная — в отличие от винтовой опоры, у которой "
            + "хозяин вычисляется сам. Значит, автопривязку при посадке придётся заводить");

        coupling.transform.position = SnapOnto(coupling, pipe).position;

        Assert.IsTrue(string.IsNullOrEmpty(coupling.AttachedToName),
            "прилипание само по себе связи НЕ создаёт: AttachedToName остаётся пустым, и "
            + "передвинутая труба уедет из-под муфты, оставив её висеть");
    }
}
