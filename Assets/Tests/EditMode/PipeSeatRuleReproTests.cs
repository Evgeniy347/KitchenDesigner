using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;
using KitchenDesigner.Core.Plumbing;
using KitchenDesigner.Tests.Geometry;

/// <summary>Правило связи спрашивали только те, кто ДОКЛАДЫВАЕТ о стыке
/// (<c>PipeJoint.Connects</c>, <c>PipeRunFit.ForRun</c>), и не спрашивал никто, кто
/// САЖАЕТ: ни <c>SnapPortDock</c>, ни <c>SnapPortSeat</c>, ни
/// <c>PipeDocking.Seat</c>, ни <c>PipeDocking.RepairAfterGridSnap</c> — они чисто
/// геометрические.
///
/// Отсюда дефект, который видит пользователь. Он тянет конец трубы А на конец
/// трубы Б, магнит радостно сажает устье в устье — а <c>PipeConnectionRule</c>
/// такую пару стыком не считает. Итог: ДВА <c>PIP-01</c> «открытый конец» на
/// стыке, который пользователь только что сделал руками и не может убрать
/// перетаскиванием; оба порта числятся свободными, поэтому ремонт по сетке
/// продолжает предлагать их себе и пересаживает трубы друг на друга на каждом
/// проходе; и панель портов показывает «пусто» там, где на сцене контакт.
///
/// Правило теперь спрашивают ТАМ, ГДЕ ВЫБИРАЕТСЯ ПОСАДКА: список кандидатов
/// строится через <c>PipeDocking.MaySeatOn</c>, поэтому ему подчиняются все пути
/// сразу — покадровый магнит (<c>SnapSystem.TrySnap</c> →
/// <c>ElementGeometryExtensions.ToGeometryFor</c>), посадка при отпускании кнопки
/// (<c>PipeDocking.Seat</c> → <c>ToPortedParts</c>), ремонт по сетке и снэп при
/// изменении размера.
///
/// Вторая половина того же куста — ЗАНЯТОЕ устье: подгонка пролёта спрашивала
/// правило связи, но не спрашивала сеть, и список ей подавали из всех устьев сцены
/// подряд. Теперь список строится из сети (<c>PipeDocking.PortsExcept</c>): устье
/// предлагается, если оно свободно ИЛИ занято самой этой трубой — второе
/// обязательно, иначе подгонка перестала бы работать ровно там, где нужна.</summary>
public class PipeSeatRuleReproTests : SnapTestBase
{
    private const int PipeLengthMm = 118;
    private const float SpanMm = 118.6f;

    [TearDown]
    public void ClearRegistry()
    {
        PartRegistry.Clear();
        CommandStack.Clear();
        ElementFactory.ClearPools();
    }

    private static float Units(float mm) => mm * AppConstants.MM_TO_UNITS;

    private PipeElement Pipe(string name, int lengthMm, Vector3 centre)
    {
        var go = ElementFactory.CreatePipe(PipeSpec.DEFAULT_SIZE, lengthMm, name, centre);
        _spawned.Add(go);
        return go.GetComponent<PipeElement>();
    }

    private PipeFittingElement Coupling(string name)
    {
        var go = ElementFactory.CreatePipeCoupling(name, Vector3.zero);
        _spawned.Add(go);
        return go.GetComponent<PipeFittingElement>();
    }

    private PipeFittingElement Tee(string name)
    {
        var go = ElementFactory.CreatePipeTee(name, Vector3.zero);
        _spawned.Add(go);
        return go.GetComponent<PipeFittingElement>();
    }

    private static void PutPortAt(PipeFittingElement fitting, int portIndex, Vector3 world) =>
        fitting.transform.position += world - fitting.PortPositionUnits(portIndex);

    private static void PutEndAt(PipeElement pipe, int end, Vector3 world) =>
        pipe.transform.position += world - pipe.SnapPortAt(end, pipe.transform.position).Position;

    private static int JoinedLinks(params KitchenElement[] scene) =>
        PipeNetwork.Build(new ScenePipeSnapshot(scene).Ports()).Links.Count;

    private PipeElement APipeBroughtUpToTheEndOf(PipeElement target)
    {
        var moved = Pipe("Ehavshaya", PipeLengthMm, Vector3.zero);
        PutEndAt(moved, 0, target.SnapPortAt(1, target.transform.position).Position
                           + new Vector3(0f, Units(1f), 0f));
        return moved;
    }

    /// <summary>Посадка при отпускании кнопки НЕ должна сводить устья двух труб,
    /// потому что стыком такая пара всё равно не станет.</summary>
    [Test]
    public void SeatingAPipeOnAnotherPipe_LeavesItWhereItIs_BecauseTwoPipesCannotJoin()
    {
        var target = Pipe("Cel", PipeLengthMm, Vector3.zero);
        var moved = APipeBroughtUpToTheEndOf(target);

        var before = moved.transform.position;
        ((IAutoSeated)moved).SeatAfterMove(new List<KitchenElement> { target, moved });

        Assert.AreEqual(before, moved.transform.position,
            "устье в устье — но труба к трубе не стык (PipeConnectionRule.CanConnect), и "
            + "посадка, севшая тут, оставила бы пользователю два PIP-01 «открытый конец» на "
            + "стыке, который он только что сделал руками, и убрать их перетаскиванием было "
            + "бы нельзя");
        Assert.AreEqual(0, JoinedLinks(target, moved),
            "контрольная половина: связи и правда нет — значит проверка выше говорит о "
            + "настоящем правиле, а не о том, что посадке просто нечего было делать");
    }

    /// <summary>Противоположный вход к тому же правилу (agents/TEST-DESIGN.md → «Two
    /// questions need OPPOSITE inputs»): труба к ФИТИНГУ садиться обязана, иначе
    /// «посадка ничего не двигает» было бы неотличимо от «посадка сломана
    /// вообще».</summary>
    [Test]
    public void SeatingAPipeOnACoupling_StillClosesTheJoint()
    {
        var coupling = Coupling("Mufta");
        var moved = Pipe("Truba", PipeLengthMm, Vector3.zero);
        PutEndAt(moved, 0, coupling.PortPositionUnits(1) + new Vector3(0f, Units(1f), 0f));

        ((IAutoSeated)moved).SeatAfterMove(new List<KitchenElement> { coupling, moved });

        Assert.AreEqual(1, JoinedLinks(coupling, moved),
            "труба и муфта — законная пара, и посадка обязана свести устья вплотную");
    }

    /// <summary>Тот же запрет на пути ремонта по сетке: он ходит по СВОБОДНЫМ портам,
    /// а у пары «труба — труба» свободны оба порта всегда, поэтому без правила связи
    /// ремонт пересаживал такие трубы друг на друга на каждом проходе, и выйти из
    /// этого аттрактора пользователю было нечем.</summary>
    [Test]
    public void GridRepair_DoesNotDragOnePipeOntoAnother()
    {
        var target = Pipe("Cel", PipeLengthMm, Vector3.zero);
        var moved = APipeBroughtUpToTheEndOf(target);

        var before = moved.transform.position;
        moved.RepairJointAfterGridSnap(new List<KitchenElement> { target, moved });

        Assert.AreEqual(before, moved.transform.position,
            "ремонт по сетке чинит СТЫК; там, где стыка быть не может, чинить нечего");
    }

    /// <summary>Покадровый магнит — тот же путь и то же правило. Оракул снэпа
    /// (<c>SnapSystem.Diagnose</c>) обязан отвечать то же, что и код: иначе
    /// snap_diagnose начнёт врать раньше, чем сломается посадка.</summary>
    [Test]
    public void TheDragMagnet_OffersNoPortSeat_BetweenTwoPipes()
    {
        var target = Pipe("Cel", PipeLengthMm, Vector3.zero);
        var moved = APipeBroughtUpToTheEndOf(target);

        var report = SnapSystem.Diagnose(moved, new List<KitchenElement> { target },
            moved.transform.position);

        Assert.IsFalse(report.portSeatWins,
            "магнит предлагал посадку устье в устье двум трубам — именно с этого "
            + "предложения начинался стык, которого PipeJoint не видит");
    }

    /// <summary>Противоположный вход к магниту: труба к муфте посадку получать
    /// обязана.</summary>
    [Test]
    public void TheDragMagnet_StillOffersAPortSeat_BetweenAPipeAndACoupling()
    {
        var coupling = Coupling("Mufta");
        var moved = Pipe("Truba", PipeLengthMm, Vector3.zero);
        PutEndAt(moved, 0, coupling.PortPositionUnits(1) + new Vector3(0f, Units(1f), 0f));

        var report = SnapSystem.Diagnose(moved, new List<KitchenElement> { coupling },
            moved.transform.position);

        Assert.IsTrue(report.portSeatWins,
            "иначе «магнит ничего не предлагает» было бы неотличимо от «магнит выключен»");
    }

    /// <summary>Занятое устье. <c>PipeRunFit.ForRun</c> отбирал устье по id элемента,
    /// правилу связи, встречной оси и зазору — но не спрашивал, не занято ли оно, а
    /// <c>PipeDocking</c> подавал ему ВСЕ устья сцены, занятые в том числе. Труба А
    /// садилась на устье тройника, уже занятое трубой Б: два претендента на одно
    /// устье, сеть выбирала одного, второй молча становился открытым концом —
    /// <c>PIP-01</c>, причину которого пользователю не видно.</summary>
    [Test]
    public void RefittingARun_IgnoresAMouthAlreadyTakenByAnotherPipe()
    {
        var scene = SceneWithATakenTeeMouth(out PipeElement claimant, out PipeElement sitting);

        int lengthBefore = claimant.LengthMM;
        bool refitted = PipeDocking.RefitRunAfterResize(claimant, scene);

        Assert.IsFalse(refitted,
            "устье тройника уже занято трубой " + sitting.PartName + ", и предлагать его "
            + "второй трубе нельзя: сеть выберет одну из двух, а вторая станет открытым "
            + "концом без видимой причины");
        Assert.AreEqual(lengthBefore, claimant.LengthMM, "длину при этом никто не менял");
    }

    /// <summary>Противоположный вход: то же самое устье, но СВОБОДНОЕ — подгонка
    /// обязана сработать, иначе тест выше проходил бы и на коде, который не подгоняет
    /// пролёт никогда.</summary>
    [Test]
    public void RefittingARun_TakesTheSameMouth_OnceItIsFree()
    {
        var scene = SceneWithATakenTeeMouth(out PipeElement claimant, out PipeElement sitting);
        scene.Remove(sitting);
        PartRegistry.Unregister(sitting);
        Object.DestroyImmediate(sitting.gameObject);

        bool refitted = PipeDocking.RefitRunAfterResize(claimant, scene);

        Assert.IsTrue(refitted, "устье освободилось — пролёт подгоняется");
        Assert.AreEqual(Mathf.RoundToInt(SpanMm), claimant.LengthMM,
            "и длина берётся из ДРОБНОГО пролёта между устьями, округлённого до целых мм");
    }

    /// <summary>Труба, которая УЖЕ сидит на устье, обязана видеть его своим: иначе
    /// подгонка пролёта перестала бы работать ровно в том случае, ради которого
    /// написана — один конец сидит, второй не достаёт (см.
    /// <c>PipeRunFitTests</c>).</summary>
    [Test]
    public void RefittingARun_StillSeesTheMouthItIsSittingOnItself()
    {
        var low = Coupling("Nizhnyaya");
        Vector3 lowMouth = low.PortPositionUnits(1);
        var high = Coupling("Verhnyaya");
        PutPortAt(high, 0, lowMouth + new Vector3(0f, Units(SpanMm), 0f));

        var pipe = Pipe("Truba", PipeLengthMm, Vector3.zero);
        PutEndAt(pipe, 0, lowMouth);

        var scene = new List<KitchenElement> { low, high, pipe };
        Assert.AreEqual(1, JoinedLinks(low, high, pipe),
            "стенд обязан начинаться с ОДНОГО закрытого стыка — нижнего");

        Assert.IsTrue(PipeDocking.RefitRunAfterResize(pipe, scene),
            "нижнее устье занято САМОЙ этой трубой, и не предложить его ей — значит "
            + "отказаться подгонять пролёт именно там, где он и нужен");
        Assert.AreEqual(Mathf.RoundToInt(SpanMm), pipe.LengthMM, "пролёт закрыт целой длиной");
    }

    private List<KitchenElement> SceneWithATakenTeeMouth(out PipeElement claimant,
        out PipeElement sitting)
    {
        var tee = Tee("Troinik");
        Vector3 upperMouth = tee.PortPositionUnits(1);

        sitting = Pipe("Sidit", PipeLengthMm, Vector3.zero);
        PutEndAt(sitting, 0, upperMouth);

        var coupling = Coupling("Mufta");
        PutPortAt(coupling, 0, upperMouth + new Vector3(0f, Units(SpanMm), 0f));

        claimant = Pipe("Pretendent", PipeLengthMm, Vector3.zero);
        PutEndAt(claimant, 0, upperMouth + new Vector3(0f, Units(1f), 0f));

        var scene = new List<KitchenElement> { tee, sitting, coupling, claimant };

        Assert.AreEqual(1, JoinedLinks(tee, sitting, coupling, claimant),
            "стенд обязан начинаться с ОДНОЙ связи — трубы " + sitting.PartName
            + " на верхнем устье тройника; без неё «устье занято» не воспроизведено");
        return scene;
    }

    /// <summary>Допуск ремонта по сетке — самостоятельный выбор, а не эхо
    /// <c>PipeRunFit.ReachMm</c>. Числом они совпали (2 мм), и константа была
    /// написана как <c>= PipeRunFit.ReachMm</c>: правка допуска подгонки пролёта
    /// молча перенастраивала бы восстановление целостности данных при ЗАГРУЗКЕ
    /// проекта, которое к пользовательскому жесту отношения не имеет (обоснование —
    /// в шапке <c>ScenePipeJointGridRepairTests</c>).</summary>
    [Test]
    public void TheGridRepairTolerance_IsItsOwnConstant_NotAnEchoOfTheRunFitReach()
    {
        Assert.AreEqual(2f, PipeDocking.GridRepairMaxDistMm, 1e-4f,
            "2 мм — осознанный выбор ремонта по сетке, задокументированный в "
            + "ScenePipeJointGridRepairTests");

        string source = File.ReadAllText(Path.Combine(
            RepoPaths.Subdir("Assets", "Scripts", "Core", "Elements"), "PipeDocking.cs"));
        int declaration = source.IndexOf("GridRepairMaxDistMm =", System.StringComparison.Ordinal);

        Assert.Greater(declaration, -1,
            "константа исчезла — сторож остался бы зелёным ни о чём");
        Assert.IsFalse(
            source.Substring(declaration, source.IndexOf(';', declaration) - declaration)
                .Contains("PipeRunFit"),
            "склеивать два независимых допуска в один нельзя: следующая правка ReachMm "
            + "перенастроит ремонт по сетке, и ни один тест этого не заметит");
    }
}
