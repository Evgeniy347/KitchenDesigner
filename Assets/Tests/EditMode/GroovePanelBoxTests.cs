using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Задняя стенка ДВП, вставляемая в короб из четырёх деталей — каждая
/// со своим пазом. Реальная сцена: два паза сквозных (верх/низ), два глухих
/// (перёд/зад), детали повёрнуты по-разному. Проверяем, что ДВП ловит ВСЕ
/// четыре паза — и перемещением, и растягиванием.
///
/// Координаты сняты из приложения как есть.</summary>
public class GroovePanelBoxTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    // ДВП: панель YZ, толщина 3 мм по X, номинальный зазор 1 мм.
    private static readonly Vector3Int PanelDims = new Vector3Int(1369, 861, 3);
    private static readonly Vector3 PanelPos = new Vector3(1.5665f, 1.81f, -2.566f);
    private static readonly Quaternion PanelRot = Quaternion.Euler(0f, 90f, 0f);

    private KitchenElement Make(string name, Vector3Int dims, Vector3 pos, Quaternion rot,
        GrooveSpec? groove)
    {
        var go = new GameObject(name);
        _spawned.Add(go);
        var el = go.AddComponent<KitchenElement>();
        el.PartName = name;
        el.DimensionsMM = dims;
        el.transform.position = pos;
        el.transform.rotation = rot;
        if (groove.HasValue) el.AddGroove(groove.Value);
        return el;
    }

    private KitchenElement Top() => Make("top", new Vector3Int(332, 1372, 18),
        new Vector3(1.419f, 2.251f, -2.566f), Quaternion.Euler(90f, 0f, 0f),
        new GrooveSpec(GrooveKind.Through, GrooveSide.Right));

    private KitchenElement Bottom() => Make("bottom", new Vector3Int(332, 1372, 18),
        new Vector3(1.419f, 1.369f, -2.566f), Quaternion.Euler(270f, 0f, 0f),
        new GrooveSpec(GrooveKind.Through, GrooveSide.Right));

    private KitchenElement FrontSide() => Make("front", new Vector3Int(332, 900, 18),
        new Vector3(1.419f, 1.81f, -1.871f), Quaternion.Euler(0f, 180f, 0f),
        new GrooveSpec(GrooveKind.Blind, GrooveSide.Left));

    private KitchenElement BackSide() => Make("back", new Vector3Int(332, 900, 18),
        new Vector3(1.419f, 1.81f, -3.261f), Quaternion.identity,
        new GrooveSpec(GrooveKind.Blind, GrooveSide.Right));

    private PanelElement Panel()
    {
        var go = new GameObject("DVP_HDF");
        _spawned.Add(go);
        var p = go.AddComponent<PanelElement>();
        p.PartName = "DVP_HDF";
        p.DimensionsMM = PanelDims;
        p.transform.position = PanelPos;
        p.transform.rotation = PanelRot;
        p.SetUniformGap(PanelElement.DEFAULT_GAP_MM);
        return p;
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

    /// <summary>Дно паза детали, обращённое к ДВП: ищем seat-грань, чья нормаль
    /// направлена к центру панели.</summary>
    private static Face SeatToward(KitchenElement board, Vector3 panelCenter)
    {
        var seats = board.GetGrooveSeatFaces();
        Assert.Greater(seats.Length, 0, $"{board.PartName}: нет посадочной грани паза");
        Face best = seats[0];
        float bestDot = float.MinValue;
        foreach (var s in seats)
        {
            float d = Vector3.Dot((panelCenter - s.center).normalized, s.normal);
            if (d > bestDot) { bestDot = d; best = s; }
        }
        return best;
    }

    // ── Диагностика: где сейчас стоят пазы относительно ДВП ──────────

    [Test]
    public void Diagnostic_AllFourSeatsFaceThePanel()
    {
        var panel = Panel();
        // Ожидаемая нормаль дна = направление ОТ паза к центру ДВП: дно смотрит
        // навстречу входящей кромке панели.
        var boards = new (string, KitchenElement, Vector3)[]
        {
            ("top",   Top(),       Vector3.down),
            ("bottom",Bottom(),    Vector3.up),
            ("front", FrontSide(), Vector3.back),
            ("back",  BackSide(),  Vector3.forward),
        };

        var lines = new List<string>();
        foreach (var (label, board, expectedNormal) in boards)
        {
            var seat = SeatToward(board, panel.transform.position);
            lines.Add($"{label}: дно паза нормаль {seat.normal} центр {seat.center}");
            Assert.Greater(Vector3.Dot(seat.normal, expectedNormal), 0.999f,
                $"{label}: дно паза должно смотреть навстречу ДВП, а смотрит {seat.normal}");
        }
        // Печать для наглядности; сам факт разных нормалей уже проверен выше.
        Assert.Pass(string.Join("\n", lines));
    }

    // ── Перемещение: ДВП должна ловить каждый паз ────────────────────

    private void AssertMoveSnapsToSeat(string label, KitchenElement board, Vector3 axis)
    {
        var panel = Panel();
        var others = new List<KitchenElement> { board };
        var seat = SeatToward(board, panel.transform.position);

        // Отводим ДВП на 4 мм ОТ паза вдоль оси, затем просим прилипнуть.
        var start = PanelPos + axis * 0.004f;
        var result = SnapSystem.TrySnap(panel, others, start);

        Assert.IsTrue(result.snapped, $"{label}: перемещение не поймало паз");

        // Номинальная грань ДВП, смотрящая к дну, должна встать на дно паза.
        float halfNominal = HalfNominalAlong(panel, axis);
        float seatCoord = Vector3.Dot(seat.center, axis);
        float panelEdge = Vector3.Dot(result.position, axis) + halfNominal * Mathf.Sign(Vector3.Dot(-seat.normal, axis));
        Assert.AreEqual(seatCoord, panelEdge, 1.5e-3f,
            $"{label}: номинал ДВП не сел на дно паза");
    }

    private static float HalfNominalAlong(PanelElement panel, Vector3 axis)
    {
        // Габаритная (номинальная) полутолщина панели вдоль мировой оси axis.
        var verts = panel.GetVertices();
        float min = float.MaxValue, max = float.MinValue;
        foreach (var v in verts)
        {
            float p = Vector3.Dot(v, axis);
            min = Mathf.Min(min, p); max = Mathf.Max(max, p);
        }
        return (max - min) * 0.5f;
    }

    [Test] public void Move_CatchesTopGroove()    => AssertMoveSnapsToSeat("top",   Top(),       Vector3.up);
    [Test] public void Move_CatchesBottomGroove() => AssertMoveSnapsToSeat("bottom",Bottom(),    Vector3.down);
    [Test] public void Move_CatchesFrontGroove()  => AssertMoveSnapsToSeat("front", FrontSide(), Vector3.forward);
    [Test] public void Move_CatchesBackGroove()   => AssertMoveSnapsToSeat("back",  BackSide(),  Vector3.back);

    [Test]
    public void Move_FullBox_SnapsIntoNearestGroove()
    {
        var panel = Panel();
        var others = new List<KitchenElement> { Top(), Bottom(), FrontSide(), BackSide() };

        // Слегка отводим по Z к переднему пазу и проверяем, что ловит.
        var seat = SeatToward(others[2], panel.transform.position); // front
        var result = SnapSystem.TrySnap(panel, others, PanelPos + Vector3.forward * 0.003f);

        Assert.IsTrue(result.snapped, "в полном коробе ДВП должна прилипнуть");
    }

    // ── Растягивание: ДВП грань к дну паза ───────────────────────────
    // ДВП растягивают за кромку к пазу: передняя/задняя грань (±Z) — к глухим
    // пазам, верхняя/нижняя (±Y) — к сквозным.

    /// <summary>Тянем грань ДВП с нормалью faceNormal на подведённой позиции и
    /// возвращаем мировую координату этой грани после снэпа вдоль оси.</summary>
    private float ResizeFaceToSeat(PanelElement panel, KitchenElement board,
        Vector3 faceNormal, int axisIndex, List<KitchenElement> others)
    {
        // Оси в плоскости грани — любые два мировых орта, ⊥ нормали.
        Vector3 uAxis = Mathf.Abs(faceNormal.y) > 0.5f ? Vector3.right : Vector3.up;
        Vector3 vAxis = Vector3.Cross(faceNormal, uAxis).normalized;

        // Текущая грань ДВП с этой нормалью.
        Face face = default;
        foreach (var f in panel.GetFaces())
            if (Vector3.Dot(f.normal, faceNormal) > 0.999f) { face = f; break; }

        var dims = panel.DimensionsMM;
        float sizeStart = (axisIndex == 0 ? dims.x : axisIndex == 1 ? dims.y : dims.z)
            * AppConstants.MM_TO_UNITS;

        // Дно паза и текущая грань — считаем «сырую» дельту так, чтобы грань
        // не дотянулась 3 мм до дна (в пределах порога снэпа).
        var seat = SeatToward(board, panel.transform.position);
        float seatCoord = Vector3.Dot(seat.center, faceNormal);
        float faceCoord = Vector3.Dot(face.center, faceNormal);
        float rawDelta = (seatCoord - faceCoord) - 0.003f;

        ResizeMath.Compute(dims, axisIndex, faceNormal, face.center, uAxis, vAxis, face.size,
            panel.transform.position, sizeStart, rawDelta, others, panel,
            snapEnabled: true, threshold: 0.05f,
            out Vector3Int newDims, out Vector3 newCenter, out _);

        // Новая координата тянутой грани.
        int newSize = axisIndex == 0 ? newDims.x : axisIndex == 1 ? newDims.y : newDims.z;
        return Vector3.Dot(newCenter, faceNormal)
            + newSize * AppConstants.MM_TO_UNITS * 0.5f;
    }

    private void AssertResizeSeatsIntoGroove(string label, KitchenElement board,
        Vector3 faceNormal, int axisIndex)
    {
        var panel = Panel();
        var others = new List<KitchenElement> { board };
        var seat = SeatToward(board, panel.transform.position);
        float seatCoord = Vector3.Dot(seat.center, faceNormal);

        float faceAfter = ResizeFaceToSeat(panel, board, faceNormal, axisIndex, others);

        // Номинальная грань ДВП садится на дно паза. Размер детали целочисленный в
        // мм, поэтому допуск = округление (±0.5 мм) плюс зазор — грань попадает В
        // ПАЗ около дна, а не остаётся снаружи на пласти (та лежала бы на ~8.5 мм
        // мельче: глубина паза 7 мм + зазор 1 мм).
        Assert.AreEqual(seatCoord, faceAfter, 2.0e-3f,
            $"{label}: растянутая грань ДВП должна сесть на дно паза (номиналом), " +
            $"а встала на {faceAfter:F4} вместо {seatCoord:F4}");
    }

    [Test] public void Resize_FrontFace_SeatsIntoBlindGroove()
        => AssertResizeSeatsIntoGroove("front", FrontSide(), Vector3.forward, 0);
    [Test] public void Resize_BackFace_SeatsIntoBlindGroove()
        => AssertResizeSeatsIntoGroove("back", BackSide(), Vector3.back, 0);
    [Test] public void Resize_TopFace_SeatsIntoThroughGroove()
        => AssertResizeSeatsIntoGroove("top", Top(), Vector3.up, 1);
    [Test] public void Resize_BottomFace_SeatsIntoThroughGroove()
        => AssertResizeSeatsIntoGroove("bottom", Bottom(), Vector3.down, 1);
}
