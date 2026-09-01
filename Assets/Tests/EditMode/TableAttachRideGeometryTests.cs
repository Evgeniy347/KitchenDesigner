using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Геометрия столов, пока стол ЕДЕТ за открывающимся фасадом.
///
/// Здесь живёт причина, по которой у TableElement и RadiusTableElement БОЛЬШЕ НЕТ
/// собственных GetFacesAt/GetVerticesAt. Они были, и они брали позу напрямую из
/// аргумента и из transform.rotation, тогда как база (KitchenElement) берёт её из
/// ValidationPositionAt/ValidationRotation — то есть из позы ПОКОЯ, пока элемент
/// прикреплён к смещённому родителю. Коробка при этом считалась одинаково:
/// FurnitureLayout.PhysicalScale(DimensionsMM) — это ровно тот localScale, который
/// ставит base.ApplyDimensions, а зазоры у мебели всегда нулевые (SupportsGaps =>
/// SupportsGrooves => GetType() == typeof(KitchenElement)). Расходилась ТОЛЬКО поза,
/// и расходилась дважды: грани брали поворот покоя при живой позиции, вершины —
/// живые и поворот, и позицию. Один и тот же стол описывался двумя по-разному
/// повёрнутыми коробками, и обе уезжали вместе с дверцей.
///
/// Достижимо это было так: «Прикреплён к» в контекстном меню открыт для всего, что
/// проходит AttachLinks.CanBeChild, столы проходят; _attachRidden ставит только
/// AttachRider.Drive и только когда родитель СМЕЩЁН, а смещаться умеет один лишь
/// открытый FacadeElement (у ящика CanCarryAttachedParts == false). Дальше живая
/// поза уходила в ValidationSnapshot.Build → e.GetVertices() — единственную дверь в
/// ядро валидации — и в ToGeometry() соседей при перетаскивании.
///
/// PhysicalScale переехал в EffectiveScale: удаление переопределений вернуло столы
/// на базовую формулу, а она берёт размер именно оттуда.
///
/// Табурет в первом тесте — контрольный образец: он всегда жил на базовой
/// реализации, и теперь стол обязан вести себя так же.</summary>
public class TableAttachRideGeometryTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void SetUp() => PartRegistry.Clear();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var el in PartRegistry.GetAll())
            if (el != null) Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
        CommandStack.Clear();
    }

    private FacadeElement MakeFacade(string name, Vector3 pos)
    {
        var go = ElementFactory.CreateFacade(new Vector3Int(600, 716, 18), name, pos, 2, 2, 2, 2);
        _spawned.Add(go);
        return go.GetComponent<FacadeElement>();
    }

    private TableElement MakeTable(string name, Vector3 pos)
    {
        var go = ElementFactory.CreateTable(new Vector3Int(1600, 750, 700), name, pos);
        _spawned.Add(go);
        return go.GetComponent<TableElement>();
    }

    private RadiusTableElement MakeRadiusTable(string name, Vector3 pos)
    {
        var go = ElementFactory.CreateRadiusTable(new Vector3Int(1600, 750, 700), name, pos);
        _spawned.Add(go);
        return go.GetComponent<RadiusTableElement>();
    }

    private StoolElement MakeStool(string name, Vector3 pos)
    {
        var go = ElementFactory.CreateStool(new Vector3Int(360, 450, 360), 0, name, pos);
        _spawned.Add(go);
        return go.GetComponent<StoolElement>();
    }

    private static Vector3 Centre(Vector3[] verts)
    {
        var min = verts[0];
        var max = verts[0];
        foreach (var v in verts) { min = Vector3.Min(min, v); max = Vector3.Max(max, v); }
        return (min + max) * 0.5f;
    }

    private static float HalfExtentAlong(Vector3[] verts, Vector3 centre, Vector3 axis)
    {
        float best = 0f;
        foreach (var v in verts) best = Mathf.Max(best, Vector3.Dot(v - centre, axis));
        return best;
    }

    private static void OpenAndRide(FacadeElement facade)
    {
        facade.SetOpen(true);
        facade.StepDoor(1f);
        AttachRider.Step();
    }

    [Test]
    public void Table_GetVertices_WhileRidingAnOpenFacade_StaysAtTheRestingPose()
    {
        var facade = MakeFacade("Фасад", Vector3.zero);
        var table = MakeTable("Стол", new Vector3(1.2f, 0.375f, 0f));
        var stool = MakeStool("Табурет", new Vector3(2.4f, 0.225f, 0f));
        table.AttachedToName = facade.PartName;
        stool.AttachedToName = facade.PartName;
        var tableRest = table.transform.position;
        var stoolRest = stool.transform.position;

        OpenAndRide(facade);

        Assert.IsTrue(table.IsAttachRidden, "предусловие: стол сейчас едет за фасадом");
        Assert.IsTrue(stool.IsAttachRidden, "предусловие: и табурет тоже");
        Assert.AreNotEqual(tableRest, table.transform.position,
            "предусловие: трансформ стола реально уехал, иначе живую позу от позы покоя "
            + "не отличить и тест ничего не проверяет");

        Assert.AreEqual(stoolRest.x, Centre(stool.GetVertices()).x, 1e-4f,
            "табурет — контрольный образец на базовой реализации: пока деталь едет за "
            + "анимацией, её геометрия для валидации и привязки остаётся в позе ПОКОЯ");
        Assert.AreEqual(stoolRest.z, Centre(stool.GetVertices()).z, 1e-4f);

        Assert.AreEqual(tableRest.x, Centre(table.GetVertices()).x, 1e-4f,
            "и стол теперь ведёт себя ровно так же: пока у него были свои GetVerticesAt, "
            + "он игнорировал ValidationPositionAt и уезжал в проверки вместе с дверцей — "
            + "открытая дверца могла зажечь мнимое пересечение и утащить за собой цель "
            + "примагничивания для соседа");
        Assert.AreEqual(tableRest.z, Centre(table.GetVertices()).z, 1e-4f);
        Assert.AreNotEqual(table.transform.position.x, Centre(table.GetVertices()).x,
            "именно позы покоя, а не живой: расхождение существовало и было измеримо");
    }

    [Test]
    public void Table_FacesAndVertices_WhileRiding_AgreeOnTheRestingRotation()
    {
        var facade = MakeFacade("Фасад", Vector3.zero);
        var table = MakeTable("Стол", new Vector3(1.2f, 0.375f, 0f));
        table.AttachedToName = facade.PartName;

        OpenAndRide(facade);

        var restRot = table.AttachRestRotation;
        Assert.Greater(Quaternion.Angle(table.transform.rotation, restRot), 1f,
            "предусловие: распашной фасад поворачивает пассажира, иначе оба поворота "
            + "совпали бы и согласованность была бы неразличима");

        var faces = table.GetFaces();
        Assert.AreEqual(1f, Vector3.Dot(faces[0].normal, restRot * Vector3.right), 1e-3f,
            "грани берут ValidationRotation, то есть поворот ПОКОЯ");

        var verts = table.GetVertices();
        var centre = Centre(verts);
        float halfWidth = table.DimensionsMM.x * 0.5f * AppConstants.MM_TO_UNITS;
        Assert.AreEqual(halfWidth,
            HalfExtentAlong(verts, centre, restRot * Vector3.right), 1e-3f,
            "и вершины — тоже: коробка выступает на полную полуширину вдоль оси ПОКОЯ. "
            + "Пока у стола были свои GetVerticesAt, они брали transform.rotation, и грани "
            + "с вершинами описывали две по-разному повёрнутые коробки одного элемента");
        Assert.Less(HalfExtentAlong(verts, centre, table.transform.rotation * Vector3.right),
            halfWidth - 1e-3f,
            "вдоль ЖИВОЙ оси она короче — вершины за поворотом дверцы больше не следуют");
    }

    [Test]
    public void RadiusTable_ValidationSnapshot_WhileRiding_TakesTheRestingPose()
    {
        var facade = MakeFacade("Фасад", Vector3.zero);
        var table = MakeRadiusTable("Радиусный стол", new Vector3(1.2f, 0.375f, 0f));
        table.AttachedToName = facade.PartName;
        var rest = table.transform.position;

        OpenAndRide(facade);
        Assert.IsTrue(table.IsAttachRidden, "предусловие: стол едет");

        var snapshot = new List<ValidationElement>();
        ValidationSnapshot.Build(new List<KitchenElement> { table }, snapshot);

        Assert.AreEqual(rest.x, Centre(snapshot[0].Vertices).x, 1e-4f,
            "ValidationSnapshot — единственная дверь в ядро валидации, и она берёт вершины "
            + "через e.GetVertices(): проверка идёт по настоящему пути в ядро, а не по "
            + "удобному для теста");
        Assert.AreNotEqual(table.transform.position.x, Centre(snapshot[0].Vertices).x,
            "ядро видит радиусный стол на его месте, а не в распахнутом положении дверцы — "
            + "как и любую другую едущую деталь");
    }
}
