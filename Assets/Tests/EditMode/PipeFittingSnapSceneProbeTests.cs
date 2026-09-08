using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;
using KitchenDesigner.Core.Plumbing;

/// <summary>То же самое на НАСТОЯЩИХ элементах сцены — по пути пользователя.
///
/// Соседний <c>PipeFittingSnapProbeTests</c> считает быстро и без Unity, собирая
/// коробки и устья руками. Это удобно и это же его слабое место: стенд может разойтись
/// с тем, что отдаёт элемент. Поэтому здесь всё то же самое едет через
/// <c>ElementFactory</c>, <c>SnapSystem.TrySnap</c> и <c>ScenePipeSnapshot</c> — ровно
/// те классы, которые работают при перетаскивании мышью. Расхождение между двумя
/// файлами и будет находкой.
///
/// Здесь же — ответ на вопрос «что скажет агенту <c>snap_diagnose</c>». Раньше он
/// говорил «OK — прилипнет», и это была правда про коробки и неправда про трассу: у
/// уголка после прилипания порты оставались свободными, а полей про трассу в
/// <c>SnapDiagnosis</c> не было ни одного. Теперь оракул зовёт ту же функцию, что и
/// отбор (<c>SnapPortSeat</c>), и отчёт несёт посадку по устьям: кто цель, какие устья,
/// на сколько сдвинет.
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

    private static float Mm(float units) => units / AppConstants.MM_TO_UNITS;

    private PipeElement StandingPipe()
    {
        var go = ElementFactory.CreatePipe(PipeSpec.DEFAULT_SIZE, PipeLengthMm, "Run",
            new Vector3(0f, Units(PipeLengthMm * 0.5f), 0f));
        _spawned.Add(go);
        return go.GetComponent<PipeElement>();
    }

    private PipeFittingElement UnderThatPipe(GameObject go, float rotationZ = 0f)
    {
        _spawned.Add(go);
        var fitting = go.GetComponent<PipeFittingElement>();
        fitting.transform.rotation = Quaternion.Euler(0f, 0f, rotationZ);
        fitting.transform.position =
            new Vector3(0f, -Units(fitting.DimensionsMM.y * 0.5f + ApproachGapMm), 0f);
        return fitting;
    }

    private PipeFittingElement CouplingUnderThePipe() =>
        UnderThatPipe(ElementFactory.CreatePipeCoupling("Cpl", Vector3.zero));

    private PipeFittingElement ElbowUnderThePipe(float rotationZ = 0f) =>
        UnderThatPipe(ElementFactory.CreatePipeElbow("Elb", Vector3.zero), rotationZ);

    private static SnapResult SnapOnto(KitchenElement moved, KitchenElement other) =>
        SnapSystem.TrySnap(moved, new List<KitchenElement> { other }, moved.transform.position);

    private static float NearestMouthGapMm(PipeFittingElement fitting, Vector3 target)
    {
        float best = float.MaxValue;
        for (int i = 0; i < fitting.PortCount; i++)
        {
            float d = Mm(Vector3.Distance(fitting.PortPositionUnits(i), target));
            if (d < best) best = d;
        }
        return best;
    }

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
            "муфта стыковалась и раньше — но ПОПУТНО: её устье лежит в центре торцевой "
            + "грани коробки, и выравнивание коробок случайно оказывалось выравниванием "
            + "устьев. Теперь она стыкуется по правилу, и тест остаётся отрицательным "
            + "контролем к соседнему про уголок");
        Assert.AreEqual(2, OpenEnds(pipe, coupling),
            "свободными остаются только верхний конец трубы и нижнее устье муфты");
    }

    [Test]
    public void PipeElbow_BroughtUnderAPipeEnd_PutsOneOfItsMouthsOnThatEnd()
    {
        var pipe = StandingPipe();
        var elbow = ElbowUnderThePipe();

        var snap = SnapOnto(elbow, pipe);

        Assert.IsTrue(snap.snapped, "уголок прилипает — как и раньше");
        elbow.transform.position = snap.position;

        Assert.LessOrEqual(NearestMouthGapMm(elbow, pipe.EndAUnits), Tolerance.ContactMm,
            "но теперь он прилипает УСТЬЕМ НА ТОРЕЦ, а не коробкой к коробке: раньше после "
            + "посадки ближайшее устье оказывалось в 11,7 мм при допуске 0,5 мм, и никакой "
            + "поворот этого не исправлял. Сцена обязана давать тот же ответ, что и "
            + "быстрый стенд: если числа разойдутся — расходятся элемент и стенд");
        Assert.AreEqual(0, JoinedLinks(pipe, elbow),
            "а СТЫКА при этом нет, и это правильно: оба плеча уголка без поворота смотрят "
            + "вниз и вбок, навстречу торцу трубы не смотрит ни одно. Посадка держит устье "
            + "на месте, стык закрывает поворот — см. соседний тест");
    }

    [Test]
    public void PipeElbow_TurnedSoAMouthFacesThePipe_ClosesTheJointOnThatEnd()
    {
        var pipe = StandingPipe();
        var elbow = ElbowUnderThePipe(180f);

        var snap = SnapOnto(elbow, pipe);

        Assert.IsTrue(snap.snapped);
        elbow.transform.position = snap.position;

        Assert.AreEqual(1, JoinedLinks(pipe, elbow),
            "вот то, чего не было НИКОГДА: развёрнутый устьем вверх уголок соединяется с "
            + "торцом трубы. Раньше при любом из четырёх осевых поворотов ближайшее устье "
            + "оставалось в 11,7 мм, потому что снэп ровнял коробки, а коробка уголка "
            + "несимметрична относительно оси плеча");
        Assert.AreEqual(2, OpenEnds(pipe, elbow),
            "и трасса собирается: свободны только верхний конец трубы и второе плечо "
            + "уголка. Было четыре — оба конца трубы и оба плеча");
    }

    [Test]
    public void SnapDiagnose_ForAnElbowAtAPipeEnd_ReportsThePortSeatItWillActuallyTake()
    {
        var pipe = StandingPipe();
        var elbow = ElbowUnderThePipe(180f);

        var diagnosis = SnapSystem.Diagnose(elbow, new List<KitchenElement> { pipe },
            elbow.transform.position);

        Assert.IsTrue(diagnosis.wouldSnap, "диагноз согласен с TrySnap");
        Assert.AreEqual("Run", diagnosis.snapTarget);
        Assert.AreEqual(1, diagnosis.neighbors.Count);

        Assert.IsTrue(diagnosis.portSeatWins,
            "именно это получит агент из MCP-инструмента snap_diagnose. Раньше отчёт "
            + "говорил «OK — прилипнет» и молчал про 11,7 мм расхождения устьев: полей про "
            + "трассу в SnapDiagnosis не было ни одного, и оракул уверенно отвечал не на "
            + "тот вопрос. AGENTS.md → «A rule added to candidate SELECTION must reach "
            + "Diagnose in the same commit»");
        Assert.AreEqual("Run", diagnosis.portSeatTarget);
        Assert.AreEqual(0, diagnosis.portSeatMovedPortIndex,
            "и называет КОНКРЕТНЫЕ устья, а не «прилипнет»: повёрнутый уголок садится "
            + "нулевым плечом");
        Assert.AreEqual(0, diagnosis.portSeatOtherPortIndex, "на нижний торец трубы");
        Assert.Greater(diagnosis.portSeatShiftMM, Tolerance.ContactMm,
            "сдвиг ненулевой — деталь ещё не на месте");
        Assert.Less(diagnosis.portSeatShiftMM, diagnosis.thresholdMM,
            "и он в пределах порога, иначе посадки бы не было");

        var neighbour = diagnosis.neighbors[0];
        Assert.IsTrue(neighbour.bothCarryPorts,
            "сосед тоже несёт устья — без этого правило вообще не включается");
        Assert.IsTrue(neighbour.portsFaceEachOther,
            "и оси устьев встречные: только такая пара даёт настоящий стык");
        Assert.AreEqual(diagnosis.portSeatShiftMM, neighbour.portSeatShiftMM, 0.01f,
            "число в строке соседа и число в шапке — из ОДНОЙ функции SnapPortSeat, а не "
            + "из двух похожих");
        StringAssert.Contains("устье", neighbour.verdict,
            "и вердикт говорит про устья, а не про грани: агент, читающий его, должен "
            + "понимать, ПОЧЕМУ деталь встанет именно сюда");
        Assert.AreEqual(ApproachGapMm, neighbour.gapMM, 0.5f,
            "габаритный зазор при этом никуда не делся и по-прежнему меряется — просто он "
            + "больше не единственное, что видит отчёт");
    }

    [Test]
    public void PipeAndFitting_AreNotCentringPartsLikeAScrewLeg_ButTheyDoCarryMouths()
    {
        var pipe = StandingPipe();
        var elbow = ElbowUnderThePipe();
        var legGo = ElementFactory.CreateScrewLeg("Leg", new Vector3(0f, -Units(400f), 0f));
        _spawned.Add(legGo);

        Assert.IsFalse(pipe.ToGeometry().CentresOnTarget,
            "труба не центруется на цели: ось крепления объявляет сам элемент через "
            + "IMountsOnTarget, и объявляет её только винтовая опора — лестницы типов в "
            + "ElementGeometryExtensions больше нет");
        Assert.IsFalse(elbow.ToGeometry().CentresOnTarget,
            "и фитинг не центруется. Механизм SnapMountSeat несёт ОДНУ ось на деталь и "
            + "центрует прямоугольник на прямоугольнике — для тройника с тремя устьями он "
            + "не годится, поэтому у труб своя геометрия посадки");
        Assert.IsTrue(legGo.GetComponent<ScrewLegElement>().ToGeometry().CentresOnTarget,
            "положительный контроль: механизм центрующейся детали в проекте есть и "
            + "работает — просто трубы в него не включены");

        Assert.IsTrue(pipe.ToGeometry().HasPorts,
            "положительный контроль к двум отрицаниям выше: устья доезжают до снимка "
            + "геометрии. Без него оба Is.False остались бы зелёными на полностью "
            + "нерабочей посадке");
        Assert.AreEqual(elbow.PortCount, elbow.ToGeometry().Ports.Length,
            "и доезжают ВСЕ: у тройника их три, и потерять одно по дороге — значит "
            + "потерять один способ соединения молча");
        Assert.IsFalse(legGo.GetComponent<ScrewLegElement>().ToGeometry().HasPorts,
            "а у обычной детали устьев нет, и правило посадки по ним её не касается");
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
