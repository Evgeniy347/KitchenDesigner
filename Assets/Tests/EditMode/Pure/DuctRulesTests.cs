using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using KitchenDesigner.Core.Construction;
using KitchenDesigner.Core.Ports;
using KitchenDesigner.Core.Ventilation;

/// <summary>V5b — сеть, скорость и воздухообмен собраны в одном Collect(), как PipeRules для
/// сантехники. Каждый тест бьёт по ОДНОЙ ветке правила: несовпадение сечений в стыке (VNT-02),
/// выбор порога скорости по связности узла (VNT-01 — магистраль/ответвление/перед решёткой) и
/// норма воздухообмена по контуру пола (VNT-03).</summary>
public class DuctRulesTests
{
    private static Port RoundEndA(string id, float xMm) =>
        new Port(id, 0, new PointMm(xMm, 0f, 0f), PipeAxis.Left, DuctProfile.Round(200).ProfileId);

    [Test]
    public void Collect_MismatchedProfilesAtAJoint_ReportsVnt02_NamingBothElements()
    {
        var ports = new[]
        {
            new Port("d1", 0, new PointMm(0f, 0f, 0f), PipeAxis.Left,
                DuctProfile.Round(200).ProfileId),
            new Port("d1", 1, new PointMm(1000f, 0f, 0f), PipeAxis.Right,
                DuctProfile.Round(200).ProfileId),
            new Port("d2", 0, new PointMm(1000f, 0f, 0f), PipeAxis.Left,
                DuctProfile.Rect(200, 150).ProfileId),
            new Port("d2", 1, new PointMm(2000f, 0f, 0f), PipeAxis.Right,
                DuctProfile.Rect(200, 150).ProfileId),
        };
        var ducts = new[]
        {
            new DuctRun("d1", DuctProfile.Round(200), 10f, 0, 1),
            new DuctRun("d2", DuctProfile.Rect(200, 150), 10f, 2, 3),
        };
        var survey = DuctSurvey.Of(ports, ducts, System.Array.Empty<GrilleRun>(),
            System.Array.Empty<RoomFootprint>());

        var findings = DuctRules.Collect(survey);
        var mismatch = findings.Single(f => f.Code == DuctIssueCatalog.CodeProfileMismatch);

        Assert.AreEqual("d1", mismatch.ElementId);
        Assert.AreEqual("d2", mismatch.OtherElementId,
            "стык двух воздуховодов — находка обязана называть ОБЕ стороны, не только первую");
    }

    [Test]
    public void Collect_SameProfileAtAJoint_ReportsNoVnt02()
    {
        var ports = new[]
        {
            new Port("d1", 0, new PointMm(0f, 0f, 0f), PipeAxis.Left,
                DuctProfile.Round(200).ProfileId),
            new Port("d1", 1, new PointMm(1000f, 0f, 0f), PipeAxis.Right,
                DuctProfile.Round(200).ProfileId),
            new Port("d2", 0, new PointMm(1000f, 0f, 0f), PipeAxis.Left,
                DuctProfile.Round(200).ProfileId),
            new Port("d2", 1, new PointMm(2000f, 0f, 0f), PipeAxis.Right,
                DuctProfile.Round(200).ProfileId),
        };
        var ducts = new[]
        {
            new DuctRun("d1", DuctProfile.Round(200), 10f, 0, 1),
            new DuctRun("d2", DuctProfile.Round(200), 10f, 2, 3),
        };
        var survey = DuctSurvey.Of(ports, ducts, System.Array.Empty<GrilleRun>(),
            System.Array.Empty<RoomFootprint>());

        var findings = DuctRules.Collect(survey);
        Assert.IsFalse(findings.Any(f => f.Code == DuctIssueCatalog.CodeProfileMismatch),
            "одинаковое круглое сечение по обе стороны стыка — сходится, находки быть не должно");
    }

    [Test]
    public void Collect_FreeStandingDuct_OverBranchLimit_UsesBranchTier()
    {
        // Оба конца свободны -> "ответвление" (6 м/с), не "магистраль" (8 м/с) и не "перед
        // решёткой" (2,5 м/с) - решёток в сцене нет вовсе.
        var profile = DuctProfile.Round(100);
        float area = DuctVelocity.CrossSectionAreaM2(profile);
        float overBranch = DuctVelocity.BranchMaxMs * 1.2f;
        float airflow = overBranch * area * 3600f;

        var ports = new[]
        {
            new Port("d1", 0, new PointMm(0f, 0f, 0f), PipeAxis.Left, profile.ProfileId),
            new Port("d1", 1, new PointMm(1000f, 0f, 0f), PipeAxis.Right, profile.ProfileId),
        };
        var ducts = new[] { new DuctRun("d1", profile, airflow, 0, 1) };
        var survey = DuctSurvey.Of(ports, ducts, System.Array.Empty<GrilleRun>(),
            System.Array.Empty<RoomFootprint>());

        var finding = DuctRules.Collect(survey).Single(f => f.Code == DuctIssueCatalog.CodeVelocity);
        StringAssert.Contains("ответвление", finding.Message);
    }

    [Test]
    public void Collect_DuctConnectedOnBothEnds_UsesMainTier_SoTheSameSpeedDoesNotFire()
    {
        // Средний сегмент связан с обеих сторон -> "магистраль" (8 м/с): скорость выше
        // порога ответвления (6), но ниже порога магистрали, поэтому находки по СРЕДНЕМУ
        // сегменту быть не должно, а по свободным крайним - должна.
        var profile = DuctProfile.Round(100);
        float area = DuctVelocity.CrossSectionAreaM2(profile);
        float betweenBranchAndMain = (DuctVelocity.BranchMaxMs + DuctVelocity.MainMaxMs) * 0.5f;
        float airflow = betweenBranchAndMain * area * 3600f;

        var ports = new[]
        {
            new Port("d1", 0, new PointMm(0f, 0f, 0f), PipeAxis.Left, profile.ProfileId),
            new Port("d1", 1, new PointMm(1000f, 0f, 0f), PipeAxis.Right, profile.ProfileId),
            new Port("d2", 0, new PointMm(1000f, 0f, 0f), PipeAxis.Left, profile.ProfileId),
            new Port("d2", 1, new PointMm(2000f, 0f, 0f), PipeAxis.Right, profile.ProfileId),
            new Port("d3", 0, new PointMm(2000f, 0f, 0f), PipeAxis.Left, profile.ProfileId),
            new Port("d3", 1, new PointMm(3000f, 0f, 0f), PipeAxis.Right, profile.ProfileId),
        };
        var ducts = new[]
        {
            new DuctRun("d1", profile, airflow, 0, 1),
            new DuctRun("d2", profile, airflow, 2, 3),
            new DuctRun("d3", profile, airflow, 4, 5),
        };
        var survey = DuctSurvey.Of(ports, ducts, System.Array.Empty<GrilleRun>(),
            System.Array.Empty<RoomFootprint>());

        var findings = DuctRules.Collect(survey).Where(f => f.Code == DuctIssueCatalog.CodeVelocity)
            .ToList();

        Assert.IsFalse(findings.Any(f => f.ElementId == "d2"),
            "d2 связан с обеих сторон - магистральный порог 8 м/с, эта скорость его не превышает");
        Assert.IsTrue(findings.Any(f => f.ElementId == "d1"));
        Assert.IsTrue(findings.Any(f => f.ElementId == "d3"));
    }

    [Test]
    public void Collect_FreeEndNearAGrille_UsesNearGrilleTier_StricterThanBranch()
    {
        // Скорость выбрана МЕЖДУ порогом "перед решёткой" (2,5) и порогом "ответвление" (6):
        // без решётки рядом находки бы не было, у решётки - обязана появиться.
        var profile = DuctProfile.Round(150);
        float area = DuctVelocity.CrossSectionAreaM2(profile);
        float betweenNearGrilleAndBranch = (DuctVelocity.NearGrilleMaxMs + DuctVelocity.BranchMaxMs) * 0.5f;
        float airflow = betweenNearGrilleAndBranch * area * 3600f;

        var freeEnd = new PointMm(1000f, 0f, 0f);
        var ports = new[]
        {
            new Port("d1", 0, new PointMm(0f, 0f, 0f), PipeAxis.Left, profile.ProfileId),
            new Port("d1", 1, freeEnd, PipeAxis.Right, profile.ProfileId),
        };
        var ducts = new[] { new DuctRun("d1", profile, airflow, 0, 1) };
        var grilles = new[] { new GrilleRun("g1", freeEnd, 60f) };
        var survey = DuctSurvey.Of(ports, ducts, grilles, System.Array.Empty<RoomFootprint>());

        var finding = DuctRules.Collect(survey).Single(f => f.Code == DuctIssueCatalog.CodeVelocity);
        StringAssert.Contains("решёткой", finding.Message);
    }

    [Test]
    public void Collect_RoomBelowOneAirChangePerHour_ReportsVnt03()
    {
        // Комната 2х2х2,5 м = 10 м³, требуется >= 10 м³/ч; решётка внутри контура даёт 5.
        var room = new RoomFootprint("Пол-1", 0f, 2000f, 0f, 2000f, 2500f);
        var grille = new GrilleRun("g1", new PointMm(1000f, 0f, 1000f), 5f);

        var survey = DuctSurvey.Of(System.Array.Empty<Port>(), System.Array.Empty<DuctRun>(),
            new[] { grille }, new[] { room });

        var finding = DuctRules.Collect(survey).Single(f => f.Code == DuctIssueCatalog.CodeAirExchange);
        Assert.AreEqual("Пол-1", finding.ElementId);
    }

    [Test]
    public void Collect_RoomAtOrAboveOneAirChangePerHour_ReportsNoVnt03()
    {
        var room = new RoomFootprint("Пол-1", 0f, 2000f, 0f, 2000f, 2500f);
        var grille = new GrilleRun("g1", new PointMm(1000f, 0f, 1000f), 10f);

        var survey = DuctSurvey.Of(System.Array.Empty<Port>(), System.Array.Empty<DuctRun>(),
            new[] { grille }, new[] { room });

        Assert.IsFalse(DuctRules.Collect(survey).Any(f => f.Code == DuctIssueCatalog.CodeAirExchange));
    }

    [Test]
    public void Collect_GrilleOutsideTheRoomFootprint_DoesNotCountTowardsItsAirExchange()
    {
        var room = new RoomFootprint("Пол-1", 0f, 2000f, 0f, 2000f, 2500f);
        var farGrille = new GrilleRun("g1", new PointMm(5000f, 0f, 5000f), 1000f);

        var survey = DuctSurvey.Of(System.Array.Empty<Port>(), System.Array.Empty<DuctRun>(),
            new[] { farGrille }, new[] { room });

        Assert.IsTrue(DuctRules.Collect(survey).Any(f => f.Code == DuctIssueCatalog.CodeAirExchange),
            "решётка вне контура пола не должна засчитываться в приток этой комнаты");
    }

    [Test]
    public void Collect_NoDuctsOrGrillesAnywhereInTheScene_ReportsNoVnt03_EvenThoughSuppliedIsZero()
    {
        // Тот же порядок ошибки, что когда-то FND-05: правило по вентиляции не имеет права
        // звучать в проекте, где вентиляции ещё нет вовсе (0 воздуховодов, 0 решёток) — это
        // не "нарушение нормы", а "раздел ещё не начат". Обнаружено на закреплённой сцене
        // pipe-gap-scene.save.json: комната там есть, вентиляции нет, воздухообмен раньше
        // всё равно звучал.
        var room = new RoomFootprint("Пол-1", 0f, 2000f, 0f, 2000f, 2500f);

        var survey = DuctSurvey.Of(System.Array.Empty<Port>(), System.Array.Empty<DuctRun>(),
            System.Array.Empty<GrilleRun>(), new[] { room });

        Assert.IsFalse(DuctRules.Collect(survey).Any(f => f.Code == DuctIssueCatalog.CodeAirExchange),
            "0 воздуховодов и 0 решёток во всей сцене — раздел вентиляции не начат, а не "
            + "провален");
    }
}
