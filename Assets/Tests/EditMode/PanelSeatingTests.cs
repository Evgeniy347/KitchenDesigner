using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;

/// <summary>
/// ДВП должна входить в пазы корпуса до дна. Если панель «приклеилась снаружи
/// паза» (у устья), пользователь щель не видит, а держится плохо — предупреждаем.
///
/// Геометрия короба и ДВП снята с реального проекта (docs/example.save.json):
/// A12_upper_A_top/bottom, A34K1_upper_side_L, A12_upper_inner_side + DVP_HDF.
/// </summary>
public class PanelSeatingTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    private ProjectLoadStateGuard? _guard;

    [SetUp]
    public void SetUp()
    {
        _guard = ProjectLoadStateGuard.Capture();
        PartRegistry.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        _guard?.Restore();
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
    }

    private KitchenElement MakeGroovedBoard(string name, Vector3Int dims, Vector3 pos, Quaternion rot,
        GrooveKind kind, GrooveSide side)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        var el = go.AddComponent<KitchenElement>();
        el.PartName = name;
        el.DimensionsMM = dims;
        el.transform.SetPositionAndRotation(pos, rot);
        Assert.IsTrue(el.AddGroove(new GrooveSpec(kind, side)), $"{name}: паз не добавился");
        PartRegistry.Register(el);
        _spawned.Add(go);
        return el;
    }

    private PanelElement MakePanel(string name, Vector3Int dims, Vector3 pos, Quaternion rot)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        var p = go.AddComponent<PanelElement>();
        p.PartName = name;
        p.DimensionsMM = dims;
        p.SetUniformGap(1);
        p.transform.SetPositionAndRotation(pos, rot);
        PartRegistry.Register(p);
        _spawned.Add(go);
        return p;
    }

    private static readonly Quaternion RotTop = new Quaternion(0.70710683f, 0f, 0f, 0.70710683f);
    private static readonly Quaternion RotBottom = new Quaternion(0.70710683f, 0f, 0f, -0.70710683f);
    private static readonly Quaternion RotSideL = new Quaternion(0f, 1f, 0f, 0f);
    private static readonly Quaternion RotDvp = new Quaternion(0f, 0.70710683f, 0f, 0.70710683f);

    private (PanelElement dvp, KitchenElement top) BuildFrameWithDvp()
    {
        var top = MakeGroovedBoard("A12_upper_A_top", new Vector3Int(332, 1372, 18),
            new Vector3(1.419f, 2.251f, -2.566f), RotTop, GrooveKind.Through, GrooveSide.Right);
        MakeGroovedBoard("A12_upper_A_bottom", new Vector3Int(332, 1372, 18),
            new Vector3(1.419f, 1.369f, -2.566f), RotBottom, GrooveKind.Through, GrooveSide.Right);
        MakeGroovedBoard("A34K1_upper_side_L", new Vector3Int(332, 900, 18),
            new Vector3(1.419f, 1.81f, -1.871f), RotSideL, GrooveKind.Blind, GrooveSide.Left);
        MakeGroovedBoard("A12_upper_inner_side", new Vector3Int(332, 900, 18),
            new Vector3(1.419f, 1.81f, -3.261f), Quaternion.identity, GrooveKind.Blind, GrooveSide.Right);

        var dvp = MakePanel("DVP_HDF", new Vector3Int(1383, 875, 3),
            new Vector3(1.5665f, 1.81f, -2.566f), RotDvp);
        return (dvp, top);
    }

    [Test]
    public void SeatedDvp_NoWarning()
    {
        BuildFrameWithDvp();
        var unseated = ConstraintValidator.FindUnseatedPanels(PartRegistry.GetAll());
        Assert.IsEmpty(unseated, "Корректно вставленная ДВП не должна давать предупреждений");
    }

    [Test]
    public void DvpPulledToGrooveMouth_Warns()
    {
        var (dvp, top) = BuildFrameWithDvp();

        // Сдвигаем ДВП к устью паза вдоль нормали посадочной грани (эмуляция
        // «приклеилось снаружи»). Используем реальную нормаль/глубину паза.
        var seat = top.GetGrooveSeatFaces()[0];
        float depth = GrooveMesh.DepthFraction(top.DimensionsMM) * top.transform.localScale.z;
        dvp.transform.position += seat.normal * depth;

        var unseated = ConstraintValidator.FindUnseatedPanels(PartRegistry.GetAll());
        Assert.IsNotEmpty(unseated, "Непосаженная ДВП должна дать предупреждение");
    }

    // Пара «ДВП ↔ доска с пазом», в которую панель зашла: между их ГАБАРИТАМИ зазор
    // равен глубине захода в паз. Это обслуживает логика посадки (SEAT-01), поэтому
    // near-contact (GAP-01/02) для таких пар выдаваться НЕ должен.
    private static bool DvpInAnyNearContact(PanelElement dvp)
    {
        foreach (var nc in ConstraintValidator.FindNearContacts(PartRegistry.GetAll(),
            SceneAnalyzer.NearContactMinGapMm, SceneAnalyzer.NearContactMaxGapMm))
            if (nc.a == dvp || nc.b == dvp) return true;
        return false;
    }

    [Test]
    public void SeatedDvp_NoNearContactWarning()
    {
        var (dvp, _) = BuildFrameWithDvp();
        // Полностью посаженная ДВП: box-зазор до боковин/дна корпуса = глубине паза,
        // но это конструкция, а не «почти касание». Ни одна из четырёх пар
        // «ДВП ↔ доска с пазом» не должна давать ложный GAP-01.
        Assert.IsFalse(DvpInAnyNearContact(dvp),
            "Посаженная в паз ДВП не должна давать ложный GAP-01");
    }
}
