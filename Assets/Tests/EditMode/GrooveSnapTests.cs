using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Прилипание вкладной панели в паз. Дно паза отдаётся снапу как
/// обычная грань, поэтому панель ловится тем же попарным сопоставлением, что и
/// всё остальное, и садится НОМИНАЛОМ на дно (зазор остаётся внутри детали).</summary>
public class GrooveSnapTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    // Деталь 600×400×18, пласть смотрит в +Z, паз вдоль правой кромки.
    private static readonly Vector3Int BoardDims = new Vector3Int(600, 400, 18);

    private KitchenElement MakeBoard(string name)
    {
        var go = new GameObject(name);
        _spawned.Add(go);
        var el = go.AddComponent<KitchenElement>();
        el.PartName = name;
        el.DimensionsMM = BoardDims;
        return el;
    }

    private PanelElement MakePanel(string name, Vector3Int dims, int gap = PanelElement.DEFAULT_GAP_MM)
    {
        var go = new GameObject(name);
        _spawned.Add(go);
        var panel = go.AddComponent<PanelElement>();
        panel.PartName = name;
        panel.DimensionsMM = dims;
        panel.SetUniformGap(gap);
        return panel;
    }

    [SetUp]
    public void SetUp() => KitchenSettings.Instance.SnapEnabled = true;

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
    }

    [Test]
    public void BoardWithoutGrooves_HasNoSeatFaces()
    {
        var board = MakeBoard("box");
        Assert.AreEqual(0, board.GetGrooveSeatFaces().Length);
    }

    [Test]
    public void SeatFace_SitsAtGrooveDepthBelowFrontFace()
    {
        var board = MakeBoard("box");
        board.transform.position = Vector3.zero;
        board.AddGroove(new GrooveSpec(GrooveKind.Through, GrooveSide.Right));

        var seats = board.GetGrooveSeatFaces();
        Assert.AreEqual(1, seats.Length);

        // Пласть на +Z: половина толщины 18 мм = 9 мм. Дно паза на 7 мм глубже.
        float frontZ = 0.5f * BoardDims.z * AppConstants.MM_TO_UNITS;
        float expected = frontZ - AppConstants.GROOVE_DEPTH_MM * AppConstants.MM_TO_UNITS;
        Assert.AreEqual(expected, seats[0].center.z, 1e-5f, "дно паза на 7 мм ниже пласти");
        Assert.AreEqual(Vector3.forward, seats[0].normal, "нормаль дна смотрит наружу паза");
    }

    [Test]
    public void SeatFace_WidthIsGrooveWidth_AndSitsAtOffsetFromEdge()
    {
        var board = MakeBoard("box");
        board.transform.position = Vector3.zero;
        board.AddGroove(new GrooveSpec(GrooveKind.Through, GrooveSide.Right));

        var seat = board.GetGrooveSeatFaces()[0];
        Assert.AreEqual(AppConstants.GROOVE_WIDTH_MM, seat.size.x / AppConstants.MM_TO_UNITS, 1e-2f);

        // Центр паза: 16 + 4/2 = 18 мм от правой кромки.
        float rightEdge = 0.5f * BoardDims.x * AppConstants.MM_TO_UNITS;
        float expectedX = rightEdge - (AppConstants.GROOVE_OFFSET_MM + AppConstants.GROOVE_WIDTH_MM * 0.5f)
            * AppConstants.MM_TO_UNITS;
        Assert.AreEqual(expectedX, seat.center.x, 1e-5f);
    }

    // Панель в пазу лежит ПЛОСКОСТЬЮ: её толщина занимает ширину паза, а внутрь
    // детали заходит кромка. Поэтому она развёрнута на 90° вокруг Y — как ДВП
    // в реальном коробе. Зазор при этом работает по направлению посадки.
    private const int PanelDepthMM = 40;   // насколько панель уходит вглубь/наружу
    private const int PanelWidthMM = 200;

    private PanelElement MakeSeatedPanel(Face seat, float shortfallM, int gap)
    {
        var panel = MakePanel("двп", new Vector3Int(PanelDepthMM, PanelWidthMM, 3), gap);
        panel.transform.rotation = ManagedRotation.Euler(0f, 90f, 0f);

        // Номинал по направлению посадки = глубина + два зазора.
        float halfNominal = 0.5f * (PanelDepthMM + 2 * gap) * AppConstants.MM_TO_UNITS;
        panel.transform.position = seat.center + seat.normal * (halfNominal + shortfallM);
        return panel;
    }

    [Test]
    public void Panel_NearGroove_SnapsNominalOntoGrooveFloor()
    {
        var board = MakeBoard("box");
        board.transform.position = Vector3.zero;
        board.AddGroove(new GrooveSpec(GrooveKind.Through, GrooveSide.Right));
        var seat = board.GetGrooveSeatFaces()[0];

        // Ставим панель, не доведя 4 мм до дна паза.
        var panel = MakeSeatedPanel(seat, shortfallM: 0.004f, gap: PanelElement.DEFAULT_GAP_MM);
        var testPos = panel.transform.position;

        var result = SnapSystem.TrySnap(panel, new List<KitchenElement> { board }, testPos);

        Assert.IsTrue(result.snapped, "панель должна поймать дно паза");

        float halfNominal = 0.5f * (PanelDepthMM + 2 * PanelElement.DEFAULT_GAP_MM)
            * AppConstants.MM_TO_UNITS;
        Assert.AreEqual(seat.center.z, result.position.z - halfNominal, 1e-4f,
            "номинал панели встаёт ровно на дно паза");

        // А сама деталь не достаёт до дна ровно на свой зазор.
        float physHalf = 0.5f * PanelDepthMM * AppConstants.MM_TO_UNITS;
        float clearance = (result.position.z - physHalf) - seat.center.z;
        Assert.AreEqual(PanelElement.DEFAULT_GAP_MM * AppConstants.MM_TO_UNITS, clearance, 1e-4f,
            "технологический зазор до дна = зазор детали (1 мм)");
    }

    [Test]
    public void Diagnose_PanelNearGroove_ReportsTheSeatFace_NotTheOuterFace()
    {
        var board = MakeBoard("box");
        board.transform.position = Vector3.zero;
        board.AddGroove(new GrooveSpec(GrooveKind.Through, GrooveSide.Right));
        var seat = board.GetGrooveSeatFaces()[0];
        var panel = MakeSeatedPanel(seat, shortfallM: 0.004f, gap: PanelElement.DEFAULT_GAP_MM);
        var testPos = panel.transform.position;
        var others = new List<KitchenElement> { board };

        var real = SnapSystem.TrySnap(panel, others, testPos);
        Assume.That(real.snapped, Is.True, "проба должна прилипать, иначе тест пуст");

        var n = SnapSystem.Diagnose(panel, others, testPos).neighbors[0];

        Assert.IsTrue(n.wouldSnap,
            "диагностика видит те же грани, что и TrySnap: шесть габаритных плюс дно "
            + "каждого паза (только для вкладной панели). Без дна паза она сообщала "
            + "«не прилипнет» там, где TrySnap сажает панель в паз: " + n.verdict);
        Assert.GreaterOrEqual(n.otherFaceIndex, 6,
            "выбрано ДНО ПАЗА, а не пласть детали: над пазом материала нет, и пласть "
            + "вытесняется дном — как и в Collect. Иначе ближайшей оказалась бы пласть, "
            + "и вердикт описывал бы грань, к которой панель не прилипает");
    }

    [Test]
    public void PlainBoard_IsNotOfferedGrooveSeats()
    {
        // Толстая деталь в паз не садится — посадочные грани ей не предлагаются.
        var board = MakeBoard("box");
        board.transform.position = Vector3.zero;
        board.AddGroove(new GrooveSpec(GrooveKind.Through, GrooveSide.Right));
        var seat = board.GetGrooveSeatFaces()[0];

        var other = MakeBoard("другая");
        float halfH = 0.5f * BoardDims.z * AppConstants.MM_TO_UNITS;
        var testPos = new Vector3(seat.center.x, seat.center.y, seat.center.z + halfH + 0.004f);

        var result = SnapSystem.TrySnap(other, new List<KitchenElement> { board }, testPos);

        // Прилипание к габаритным граням возможно, но НЕ на дно паза.
        if (result.snapped)
            Assert.Greater(Mathf.Abs(seat.center.z - (result.position.z - halfH)), 1e-4f,
                "обычная деталь не должна садиться на дно паза");
    }

    // ── Валидация ──────────────────────────────────────────────────

    [Test]
    public void SeatedPanel_IsNotReportedAsOverlap()
    {
        var board = MakeBoard("box");
        board.transform.position = Vector3.zero;
        board.AddGroove(new GrooveSpec(GrooveKind.Through, GrooveSide.Right));
        var seat = board.GetGrooveSeatFaces()[0];

        // Посажена ровно: номинал упирается в дно паза.
        var panel = MakeSeatedPanel(seat, shortfallM: 0f, gap: PanelElement.DEFAULT_GAP_MM);

        var result = ConstraintValidator.Validate(new List<KitchenElement> { board, panel });

        Assert.IsFalse(result.violations.Contains(panel), "панель в пазу — конструкция, не ошибка");
        Assert.IsFalse(result.violations.Contains(board));
    }

    [Test]
    public void PanelDrivenPastGrooveFloor_IsStillAnOverlap()
    {
        var board = MakeBoard("box");
        board.transform.position = Vector3.zero;
        board.AddGroove(new GrooveSpec(GrooveKind.Through, GrooveSide.Right));
        var seat = board.GetGrooveSeatFaces()[0];

        // Загнали на 3 мм ГЛУБЖЕ дна паза — это уже настоящее пересечение.
        var panel = MakeSeatedPanel(seat, shortfallM: -0.003f, gap: PanelElement.DEFAULT_GAP_MM);

        var result = ConstraintValidator.Validate(new List<KitchenElement> { board, panel });

        Assert.IsTrue(result.violations.Contains(panel) || result.violations.Contains(board),
            "пробитое дно паза должно оставаться нарушением");
    }

    [Test]
    public void PanelNearBoardWithoutGrooves_IsStillAnOverlap()
    {
        // Без пазов освобождения нет: панель, воткнутая в глухую деталь, — ошибка.
        var board = MakeBoard("box");
        board.transform.position = Vector3.zero;

        var panel = MakePanel("двп", new Vector3Int(PanelDepthMM, PanelWidthMM, 3));
        panel.transform.rotation = ManagedRotation.Euler(0f, 90f, 0f);
        panel.transform.position = Vector3.zero; // воткнута в глухую деталь

        var result = ConstraintValidator.Validate(new List<KitchenElement> { board, panel });

        Assert.IsTrue(result.violations.Contains(panel) || result.violations.Contains(board));
    }
}
