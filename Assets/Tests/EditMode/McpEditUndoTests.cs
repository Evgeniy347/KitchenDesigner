using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Правка по проводу откатывается так же, как правка из панели. Панель снимает слепок
/// [Undoable]-свойств до и после и кладёт разницу одной командой; edit_elements когда-то
/// клал в отмену только положение и габарит, а типовые поля (радиусы, высота сиденья,
/// флаги кровати) писал голыми сеттерами — Ctrl+Z молча пропускал их, и агентская
/// правка оставалась «навсегда».
///
/// Сторож обобщён по строкам таблицы, а не по именам свойств: каждая строка — тип и ОДНО
/// проволочное поле; проверяется не «это свойство отменяется», а «после правки в стеке один
/// шаг, а отмена возвращает слепок всех [Undoable]-свойств целиком». Новое поле любого
/// типа проверяется добавлением строки.</summary>
public class McpEditUndoTests : McpTestFixture
{
    private static readonly (Type type, string wireField, object op)[] Rows =
    {
        (typeof(SofaElement), "edge_radius", new { edge_radius = 25 }),
        (typeof(SofaElement), "seat_height", new { seat_height = 400 }),
        (typeof(SofaElement), "corner_radius", new { corner_radius = 60 }),
        (typeof(ChairElement), "seat_height", new { seat_height = 500 }),
        (typeof(ChairElement), "corner_radius", new { corner_radius = 30 }),
        (typeof(StoolElement), "corner_radius", new { corner_radius = 31 }),
        (typeof(PouffeElement), "corner_radius", new { corner_radius = 41 }),
        (typeof(ToiletElement), "seat_height", new { seat_height = 440 }),
        (typeof(RadialShelfElement), "corner_radius", new { corner_radius = 50 }),
        (typeof(BedElement), "bed_double", new { bed_double = true }),
        (typeof(BedElement), "bed_headboard", new { bed_headboard = true }),
    };

    private static IEnumerable<TestCaseData> RowCases()
    {
        foreach (var row in Rows)
            yield return new TestCaseData(row.type, row.op).SetName(
                $"EditElements_{row.type.Name}_{row.wireField}_IsOneUndoStep_AndUndoRestoresTheWholeSnapshot");
    }

    [SetUp]
    public void ClearUndoHistory() => CommandStack.Clear();

    [TearDown]
    public void ClearUndoHistoryAfter() => CommandStack.Clear();

    [TestCaseSource(nameof(RowCases))]
    public void EditElements_TypeSpecificField_IsOneUndoStep_AndUndoRestoresTheWholeSnapshot(
        Type type, object fields)
    {
        var element = EveryElementType.Spawn(type, "UndoProbe");
        _spawned.Add(element.gameObject);
        if (!PartRegistry.GetAll().Contains(element)) PartRegistry.Register(element);
        CommandStack.Clear();
        var before = UndoableProperties.Capture(element);

        var op = Newtonsoft.Json.Linq.JObject.FromObject(fields);
        op["name"] = element.PartName;
        var resp = _handler!.Handle(MakeReq("edit_elements", new { ops = new object[] { op } }));
        Assert.AreEqual("result", resp.type, "правка принята");

        var after = UndoableProperties.Capture(element);
        var changed = UndoableProperties.Changed(before, after);
        if (changed.Count == 0)
            Assert.Inconclusive("значение строки совпало с тем, что уже стояло, — подберите другое");
        Assert.AreEqual(1, CommandStack.UndoCount,
            "правка по проводу — ровно один шаг отмены");

        CommandStack.Undo();

        var restored = UndoableProperties.Capture(element);
        Assert.IsEmpty(UndoableProperties.Changed(before, restored),
            "отмена обязана вернуть слепок целиком, как отмена из панели");
    }
}
