using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Регрессия: выдвижение ящика НЕ должно порождать ложные пересечения объёма.
/// Раньше DrawerElement валидировался по текущему (выдвинутому) трансформу, а его
/// фасад — по закрытой позе, из-за чего открытый ящик «пересекал» собственный
/// фасад/корпус. Теперь ящик, как и фасад, проверяется в ЗАКРЫТОЙ позе.
///
/// Геометрия модуля A3 снята с реального проекта (docs/example.save.json).
/// </summary>
public class DrawerOpenValidationTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void SetUp() => PartRegistry.Clear();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
    }

    private KitchenElement MakeBoard(string name, Vector3Int dims, Vector3 pos, Quaternion rot)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        var el = go.AddComponent<KitchenElement>();
        el.PartName = name;
        el.DimensionsMM = dims;
        el.transform.SetPositionAndRotation(pos, rot);
        PartRegistry.Register(el);
        _spawned.Add(go);
        return el;
    }

    private DrawerElement MakeDrawer(string name, DrawerType type, int nominal, int width,
        Vector3 pos, Quaternion rot, string facade, bool isDouble = false, bool isUpper = false,
        string paired = "")
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        var d = go.AddComponent<DrawerElement>();
        d.PartName = name;
        d.Type = type;
        d.NominalLength = nominal;
        d.InternalWidth = width;
        d.IsDouble = isDouble;
        d.IsUpperDrawer = isUpper;
        d.PairedDrawerName = paired;
        d.AttachedFacadeName = facade;
        d.transform.SetPositionAndRotation(pos, rot);
        PartRegistry.Register(d);
        _spawned.Add(go);
        return d;
    }

    private FacadeElement MakeFacade(string name, Vector3Int dims, Vector3 pos, Quaternion rot, int gap = 2)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        var f = go.AddComponent<FacadeElement>();
        f.PartName = name;
        f.DimensionsMM = dims;
        f.GapLeft = gap; f.GapRight = gap; f.GapTop = gap; f.GapBottom = gap;
        f.transform.SetPositionAndRotation(pos, rot);
        PartRegistry.Register(f);
        _spawned.Add(go);
        return f;
    }

    // Выдвинуть ящик и сдвинуть трансформ в открытую позу (эмуляция завершённой
    // анимации): закрытая поза уже захвачена SetOpen/ApplyDoubleState.
    private static void SlideOpen(DrawerElement d)
    {
        var slid = d.ClosedPosition + d.ClosedRotation * Vector3.forward * DrawerConstants.DRAWER_SLIDE_METERS;
        d.transform.position = slid;
    }

    private static readonly Quaternion RotY = new Quaternion(0f, 0.707106828689575f, 0f, -0.707106828689575f);
    private static readonly Quaternion RotX = new Quaternion(0.707106828689575f, 0f, 0f, -0.707106828689575f);

    private (DrawerElement lower, DrawerElement upper, DrawerElement single) BuildA3Module()
    {
        MakeBoard("A3_side_L", new Vector3Int(540, 720, 18),
            new Vector3(1.31500006f, 0.46000001f, -2.45300007f), Quaternion.identity);
        MakeBoard("A3_side_R", new Vector3Int(540, 720, 18),
            new Vector3(1.31500006f, 0.46000001f, -1.87100005f), Quaternion.identity);
        MakeBoard("A3_bottom", new Vector3Int(540, 564, 16),
            new Vector3(1.31500006f, 0.10799999f, -2.16206288f), RotX);

        var single = MakeDrawer("A3_gtv_single_C", DrawerType.C, 500, 564,
            new Vector3(1.31500006f, 0.21500000f, -2.16209984f), RotY, "A3_drawer_lower");
        var lower = MakeDrawer("A3_gtv_double_lower_A", DrawerType.A, 500, 563,
            new Vector3(1.29500020f, 0.52268642f, -2.16249990f), RotY, "A3_drawer_upper",
            isDouble: true, isUpper: false, paired: "A3_gtv_double_upper_A");
        var upper = MakeDrawer("A3_gtv_double_upper_A", DrawerType.A, 500, 563,
            new Vector3(1.29500020f, 0.63768643f, -2.16249990f), RotY, "",
            isDouble: true, isUpper: true, paired: "A3_gtv_double_lower_A");

        MakeFacade("A3_drawer_lower", new Vector3Int(596, 356, 18),
            new Vector3(1.03600001f, 0.28000000f, -2.16199994f), RotY);
        MakeFacade("A3_drawer_upper", new Vector3Int(596, 356, 18),
            new Vector3(1.03600013f, 0.64000005f, -2.16200018f), RotY);

        return (lower, upper, single);
    }

    [Test]
    public void OpeningDoubleDrawer_AddsNoViolations()
    {
        var (lower, upper, _) = BuildA3Module();
        var all = PartRegistry.GetAll();

        int closed = ConstraintValidator.Validate(all).violations.Count;

        // Открыть двойной ящик и сдвинуть трансформы обеих половин.
        lower.DoubleState = DoubleDrawerState.BothOpen;
        SlideOpen(lower);
        SlideOpen(upper);

        int open = ConstraintValidator.Validate(all).violations.Count;

        Assert.LessOrEqual(open, closed,
            $"Открытие ящика добавило нарушений: было {closed}, стало {open}");
    }

    [Test]
    public void OpenDrawer_DoesNotOverlapOwnFacade()
    {
        // Ящик у начала координат, фасад — прямо перед ним (закрыто: контакт).
        var drawer = MakeDrawer("drawer", DrawerType.C, 500, 400,
            Vector3.zero, Quaternion.identity, "facade");
        // Фасад по фронту ящика (локальный +Z ~ глубина/2).
        float front = drawer.DimensionsMM.z * 0.5f * AppConstants.MM_TO_UNITS;
        MakeFacade("facade", new Vector3Int(420, 150, 18),
            new Vector3(0f, 0f, front + 0.010f), Quaternion.identity);

        var all = PartRegistry.GetAll();
        int closed = ConstraintValidator.Validate(all).violations.Count;

        drawer.SetOpen(true);
        SlideOpen(drawer);

        var result = ConstraintValidator.Validate(all);
        Assert.LessOrEqual(result.violations.Count, closed,
            "Выдвинутый ящик даёт ложное пересечение со своим фасадом");
        Assert.IsFalse(result.violations.Contains(drawer),
            "Ящик помечен нарушением после выдвижения");
    }
}
