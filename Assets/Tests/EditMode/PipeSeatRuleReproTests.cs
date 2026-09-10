using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;
using KitchenDesigner.Core.Plumbing;

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
/// изменении размера.</summary>
public class PipeSeatRuleReproTests : SnapTestBase
{
    private const int PipeLengthMm = 118;

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
}
