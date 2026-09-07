using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Диагностика обязана судить по тем же правилам, по каким идёт отбор.
///
/// Это не внутренняя кухня: `SnapNeighbourFacts` — то, что пользователь получает
/// через MCP-инструмент `snap_diagnose`. Пока правила отбора жили только в
/// `SnapCandidateCollector`, диагностика меряла всем подряд обычный зазор по
/// плоскости и объявляла соперниками пары, которых выбор не рассматривает:
/// свип `SnapMutationTests` 2026-09-08 дал на этом 78 ложных MOVE-COMPETITION,
/// а агент, отлаживающий сцену, получал уверенный неверный ответ.
///
/// Расхождений было два, и оба про винтовую опору (`CentresOnTarget`):
/// 1) отбор берёт ТОЛЬКО грани оси крепления, диагностика брала все встречные;
/// 2) у грани крепления сдвиг вдоль нормали нулевой по построению, её
///    «расстояние» — это детент ПОПЕРЁК оси, а диагностика меряла зазор по
///    плоскости и сравнивала его с чужими зазорами как величину той же оси.
///
/// Теперь обе стороны зовут `SnapPairOffer.For`, и правило описано один раз.
/// Третьему месту не дают появиться `SnapMountRuleSingleSourceTests`.</summary>
public class SnapDiagnosisRulesTests : SnapCoreTestBase
{
    private const float MaxDist = Threshold + Tolerance.SnapEpsilon;

    private const float LegCentreY = 29f * MM;
    private const float PlinthBottomY = 20f * MM;

    private static Box FloorLeg() =>
        new Box("Leg", new Vector3Int(25, 58, 25), null, false, Vector3.up);

    private static Box PlainLeg() =>
        new Box("Leg", new Vector3Int(25, 58, 25), null, false, Vector3.zero);

    private static ElementGeometry Plinth() =>
        At(Make("Plinth", new Vector3Int(482, 80, 16)),
            new Vector3(0f, PlinthBottomY + 40f * MM, 0f));

    private static ElementGeometry BoardBesideTheLeg() =>
        At(Make("Board", new Vector3Int(482, 80, 16)), new Vector3(0f, 60f * MM, 0f));

    private static SnapNeighbourFacts Facts(Box moved, ElementGeometry other, Vector3 pos)
        => SnapNeighbourFacts.Of(moved.At(pos), pos, other, MaxDist);

    [Test]
    public void ASideFaceOfTheLeg_IsNotDiagnosedAsSnapping()
    {
        var pos = new Vector3(0f, LegCentreY, -52.5f * MM);
        var leg = FloorLeg().At(pos);
        var board = BoardBesideTheLeg();
        var facts = SnapNeighbourFacts.Of(leg, pos, board, MaxDist);

        Assert.IsTrue(facts.hasFacingFaces,
            "встречные параллельные грани здесь ЕСТЬ — бок пятки против пласти доски. "
            + "Если этой строки не станет, тест перестанет доказывать, что дело в правиле "
            + "отбора, а не в геометрии");

        var side = SnapFacePairRules.RoleOf(leg, leg.Faces[4], board.Faces[5], false,
            System.Array.Empty<Face>(), out SnapPairRejection why);
        Assert.AreEqual(SnapPairRole.NotACandidate, side,
            "именно эта пара — +Z пятки против −Z доски — и есть та боковая, что "
            + "выигрывала по сдвигу у посадки и уносила опору с середины царги на пласть");
        Assert.AreEqual(SnapPairRejection.OffTheMountAxis, why,
            "и причина названа та самая, по которой отбор её выбрасывает");

        Assert.AreEqual(1, facts.movedFaceIndex / 2,
            "диагноз идёт по оси крепления (индекс/2 = 1, ось Y), а не по боковой паре Z, "
            + "которая ближе (32 мм против 38) и перекрывается на 66%: раньше побеждала "
            + "именно она");
        Assert.IsFalse(facts.wouldSnap,
            "диагностика говорила «прилипнет» о паре, до которой снэп никогда не доходит: "
            + "зазор 32 мм при пороге 50, перекрытие 66% — по старому правилу «встречные "
            + "грани в пределах порога» это был уверенный неверный ответ");

        Assert.IsFalse(Snap(FloorLeg(), BoardBesideTheLeg(), pos).snapped,
            "а TrySnap здесь и не прилипает — именно с ним диагноз обязан совпадать");
    }

    [Test]
    public void AnOrdinaryPartInTheSamePlace_IsStillDiagnosedAsSnapping()
    {
        var pos = new Vector3(0f, LegCentreY, -52.5f * MM);
        var facts = Facts(PlainLeg(), BoardBesideTheLeg(), pos);

        Assert.AreEqual(2, facts.movedFaceIndex / 2,
            "та же геометрия без пометки о центровке — и выбрана боковая пара по Z");
        Assert.IsTrue(facts.wouldSnap,
            "и прилипание по ней есть. Это противоположный вход к соседнему тесту: сними "
            + "с правила условие CentresOnTarget — и отбор начнёт брать только грани оси "
            + "крепления У ВСЕХ, соседний тест останется зелёным, а красным станет этот");
        Assert.IsTrue(Snap(PlainLeg(), BoardBesideTheLeg(), pos).snapped,
            "TrySnap подтверждает");
    }

    [Test]
    public void TheMountFace_IsMeasuredByItsSeatDetent_NotByThePlaneGap()
    {
        var pos = new Vector3(0f, LegCentreY, -12f * MM);
        var facts = Facts(FloorLeg(), Plinth(), pos);

        Assert.AreEqual(SnapPairRole.Centring, facts.role,
            "выбрана грань крепления — верх опоры против дна цоколя");
        Assert.IsTrue(facts.wouldSnap, "посадка под цоколь предлагается");
        Assert.AreEqual(12f, facts.distanceUnits / MM, 0.5f,
            "расстояние по этой паре — детент ПОПЕРЁК оси крепления: опора смещена с "
            + "середины шестнадцатимиллиметровой толщины цоколя на 12 мм. Вдоль нормали "
            + "посадка не двигает деталь вовсе (AddCentringContact не трогает planeShift), "
            + "и зазор по плоскости здесь равен 38 мм — именно его диагностика и "
            + "показывала, после чего свип сравнивал его с чужими зазорами как величину "
            + "той же оси");
    }

    [Test]
    public void AFaceWithoutOverlap_NeverOutranksAFacingPairThatIsMerelyTooFar()
    {
        // Две обычные детали 800×400×18 в 100 мм друг от друга по Z при пороге 50.
        var target = Std("A", new Vector3(0f, 0.2f, 0f));
        var moved = MakeStd("B");
        var pos = new Vector3(0f, 0.2f, 118f * MM);
        var facts = Facts(moved, target, pos);

        Assert.IsFalse(Snap(moved, target, pos).snapped, "прилипания здесь нет");
        Assert.IsFalse(facts.wouldSnap, "и диагноз это подтверждает");

        Assert.AreEqual(2, facts.movedFaceIndex / 2,
            "докладывать надо про пару по Z — единственную, чьи грани смотрят друг "
            + "на друга. Пары ±X и ±Y у этих деталей ЛЕЖАТ В ОДНОЙ ПЛОСКОСТИ "
            + "(сдвиг по нормали ровно 0) и не перекрываются вовсе: их «зазор» — "
            + "не зазор, а совпадение плоскостей. Отбор до них не доходит, но в "
            + "отчёте они выигрывали по расстоянию 0 мм");
        Assert.IsFalse(facts.withinThreshold,
            "зазор больше порога — это и есть причина отказа");
        Assert.AreEqual(100f, facts.distanceUnits / MM, 0.5f,
            "и зазор назван настоящий: 118 мм между центрами минус 18 мм толщины");
        Assert.AreEqual(1f, facts.overlapRatio, 1e-3f,
            "грани по Z совпадают полностью — именно перекрытие и отличает их от "
            + "соплоскостных боковых, а раньше у отвергнутой по порогу пары оно "
            + "не считалось вовсе и она падала в тот же ранг, что и они");
    }

    [Test]
    public void APairWithinTheThreshold_StillOutranksTheFarOne_EvenWithTinyOverlap()
    {
        // Противоположный вход: если предпочесть «дальнюю, но перекрытую» пару
        // безусловно, красным станет этот тест, а соседний останется зелёным.
        var target = Std("A", new Vector3(0f, 0.2f, 0f));
        var moved = MakeStd("B");
        var pos = new Vector3(760f * MM, 0.2f, 28f * MM);
        var facts = Facts(moved, target, pos);

        Assert.IsFalse(facts.wouldSnap, "перекрытие мало — прилипания нет");
        Assert.IsTrue(facts.withinThreshold,
            "но причина не в пороге: до встречной грани 10 мм из 50");
        Assert.AreEqual(SnapPairRejection.OverlapTooSmall, facts.rejection,
            "докладывать надо про перекрытие");
        Assert.AreEqual(10f, facts.distanceUnits / MM, 0.5f, "зазор именно у этой пары");
    }

    [Test]
    public void TheDiagnosis_AgreesWithTrySnap_AsTheLegPassesThePlinth()
    {
        var plinth = Plinth();
        var mismatches = new List<string>();

        for (int mm = -60; mm <= 60; mm += 3)
        {
            var pos = new Vector3(0f, LegCentreY, mm * MM);
            bool real = Snap(FloorLeg(), plinth, pos).snapped;
            bool told = Facts(FloorLeg(), plinth, pos).wouldSnap;
            if (real != told) mismatches.Add($"z={mm}мм: TrySnap={real}, диагноз={told}");
        }

        Assert.IsEmpty(mismatches,
            "у одного соседа «эта пара прилипнет» и «прилипание есть» — одно и то же "
            + "утверждение: " + string.Join("; ", mismatches));
    }

    [Test]
    public void TheDiagnosis_AgreesWithTrySnap_AsAnOrdinaryBoardPassesAnother()
    {
        var target = Std("A", new Vector3(0f, 0.2f, 0f));
        var moved = MakeStd("B");
        var mismatches = new List<string>();

        for (int mm = -120; mm <= 120; mm += 3)
        {
            var pos = new Vector3(0f, 0.2f, mm * MM);
            bool real = Snap(moved, target, pos).snapped;
            bool told = SnapNeighbourFacts.Of(moved.At(pos), pos, target, MaxDist).wouldSnap;
            if (real != told) mismatches.Add($"z={mm}мм: TrySnap={real}, диагноз={told}");
        }

        Assert.IsEmpty(mismatches,
            "обычная деталь проходит мимо обычной: тот же инвариант на входе, где ни "
            + "одно правило опоры не действует. " + string.Join("; ", mismatches));
    }
}
