using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Пазы как свойство детали: правка набора, сериализация и колонка
/// Grooves в спецификации.</summary>
public class GroovePartTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    private KitchenElement CreatePart(string name, Vector3Int dims)
    {
        var go = new GameObject(name);
        _spawned.Add(go);
        var element = go.AddComponent<KitchenElement>();
        element.PartName = name;
        element.DimensionsMM = dims;
        return element;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
    }

    [Test]
    public void NewPart_HasNoGrooves()
    {
        var part = CreatePart("Board", new Vector3Int(800, 400, 18));
        Assert.AreEqual(0, part.Grooves.Count);
        Assert.IsTrue(part.SupportsGrooves, "базовая деталь поддерживает пазы");
    }

    [Test]
    public void AddGroove_StoresSpec()
    {
        var part = CreatePart("Board", new Vector3Int(800, 400, 18));
        Assert.IsTrue(part.AddGroove(new GrooveSpec(GrooveKind.Through, GrooveSide.Top)));

        Assert.AreEqual(1, part.Grooves.Count);
        Assert.AreEqual(GrooveKind.Through, part.Grooves[0].kind);
        Assert.AreEqual(GrooveSide.Top, part.Grooves[0].side);
    }

    [Test]
    public void AddGroove_DuplicateSideAndKind_IsRejected()
    {
        var part = CreatePart("Board", new Vector3Int(800, 400, 18));
        var spec = new GrooveSpec(GrooveKind.Blind, GrooveSide.Left);

        Assert.IsTrue(part.AddGroove(spec));
        Assert.IsFalse(part.AddGroove(spec), "смещение фиксировано — дубль лёг бы на первый паз");
        Assert.AreEqual(1, part.Grooves.Count);
    }

    [Test]
    public void AddGroove_SameSideDifferentKind_IsAllowed()
    {
        var part = CreatePart("Board", new Vector3Int(800, 400, 18));
        Assert.IsTrue(part.AddGroove(new GrooveSpec(GrooveKind.Through, GrooveSide.Top)));
        Assert.IsTrue(part.AddGroove(new GrooveSpec(GrooveKind.Blind, GrooveSide.Top)));
        Assert.AreEqual(2, part.Grooves.Count);
    }

    [Test]
    public void RemoveGrooveAt_DropsOnlyThatGroove()
    {
        var part = CreatePart("Board", new Vector3Int(800, 400, 18));
        part.AddGroove(new GrooveSpec(GrooveKind.Through, GrooveSide.Top));
        part.AddGroove(new GrooveSpec(GrooveKind.Blind, GrooveSide.Left));

        Assert.IsTrue(part.RemoveGrooveAt(0));
        Assert.AreEqual(1, part.Grooves.Count);
        Assert.AreEqual(GrooveSide.Left, part.Grooves[0].side);

        Assert.IsFalse(part.RemoveGrooveAt(5), "индекс вне списка — не падаем");
    }

    [Test]
    public void Facade_DoesNotSupportGrooves()
    {
        var go = new GameObject("Facade");
        _spawned.Add(go);
        var facade = go.AddComponent<FacadeElement>();
        facade.DimensionsMM = new Vector3Int(600, 700, 18);

        Assert.IsFalse(facade.SupportsGrooves);
        Assert.IsFalse(facade.AddGroove(new GrooveSpec(GrooveKind.Through, GrooveSide.Top)));
        Assert.AreEqual(0, facade.Grooves.Count);
    }

    [Test]
    public void SetGrooves_ReplacesSetAndDropsDuplicates()
    {
        var part = CreatePart("Board", new Vector3Int(800, 400, 18));
        part.AddGroove(new GrooveSpec(GrooveKind.Through, GrooveSide.Bottom));

        part.SetGrooves(new List<GrooveSpec>
        {
            new GrooveSpec(GrooveKind.Blind, GrooveSide.Right),
            new GrooveSpec(GrooveKind.Blind, GrooveSide.Right),
        });

        Assert.AreEqual(1, part.Grooves.Count);
        Assert.AreEqual(GrooveSide.Right, part.Grooves[0].side);
    }

    // ── Сериализация ───────────────────────────────────────────────

    [Test]
    public void ElementData_RoundTripsGrooves()
    {
        var part = CreatePart("Board", new Vector3Int(800, 400, 18));
        part.AddGroove(new GrooveSpec(GrooveKind.Through, GrooveSide.Top));
        part.AddGroove(new GrooveSpec(GrooveKind.Blind, GrooveSide.Left));

        var data = ElementCapture.FromElement(part);
        var json = JsonUtility.ToJson(data);
        var restoredData = JsonUtility.FromJson<ElementData>(json);
        var specs = restoredData.GrooveSpecs();

        Assert.AreEqual(2, specs.Count);
        Assert.AreEqual(new GrooveSpec(GrooveKind.Through, GrooveSide.Top), specs[0]);
        Assert.AreEqual(new GrooveSpec(GrooveKind.Blind, GrooveSide.Left), specs[1]);
    }

    [Test]
    public void ElementData_LegacyFileWithoutGrooves_LoadsEmpty()
    {
        // Файл старого формата: поля grooves нет вовсе.
        var legacy = JsonUtility.FromJson<ElementData>(
            "{\"name\":\"Board\",\"dimensionsMM\":[800,400,18]}");
        Assert.AreEqual(0, legacy.GrooveSpecs().Count);
    }

    [Test]
    public void ElementData_NonPartElement_WritesNoGrooves()
    {
        var go = new GameObject("Facade");
        _spawned.Add(go);
        var facade = go.AddComponent<FacadeElement>();
        facade.DimensionsMM = new Vector3Int(600, 700, 18);

        var data = ElementCapture.FromElement(facade);
        Assert.AreEqual(0, data.grooves.Length);
    }

    // ── Спецификация и CSV ─────────────────────────────────────────

    [Test]
    public void GroovesLabel_ListsKindAndSide()
    {
        var part = CreatePart("Board", new Vector3Int(800, 400, 18));
        part.AddGroove(new GrooveSpec(GrooveKind.Through, GrooveSide.Top));
        part.AddGroove(new GrooveSpec(GrooveKind.Blind, GrooveSide.Left));

        Assert.AreEqual("Сквозной 16*4*7:Верх, Глухой 16*4*7:Лево",
            SpecificationManager.GroovesLabel(part));
    }

    [Test]
    public void GroovesLabel_EmptyWhenNoGrooves()
    {
        var part = CreatePart("Board", new Vector3Int(800, 400, 18));
        Assert.AreEqual("", SpecificationManager.GroovesLabel(part));
    }

    [Test]
    public void Build_PartsWithDifferentGrooves_AreSeparateLines()
    {
        var plain = CreatePart("Board", new Vector3Int(800, 400, 18));
        var grooved = CreatePart("Board", new Vector3Int(800, 400, 18));
        grooved.AddGroove(new GrooveSpec(GrooveKind.Through, GrooveSide.Top));

        var result = SpecificationManager.Build(new List<KitchenElement> { plain, grooved });

        // +1 строка кромки погонными метрами (кромкование по умолчанию включено у обеих
        // досок) — общая на весь Build, не дробится по пазам.
        Assert.AreEqual(3, result.lines.Count,
            "одинаковые щиты с разной врезкой — разные позиции, плюс общая строка кромки");
        Assert.AreEqual(2, result.totalCount);
    }

    [Test]
    public void Build_IdenticalGrooves_GroupTogether()
    {
        var a = CreatePart("Board", new Vector3Int(800, 400, 18));
        var b = CreatePart("Board", new Vector3Int(800, 400, 18));
        a.AddGroove(new GrooveSpec(GrooveKind.Blind, GrooveSide.Bottom));
        b.AddGroove(new GrooveSpec(GrooveKind.Blind, GrooveSide.Bottom));

        var result = SpecificationManager.Build(new List<KitchenElement> { a, b });

        // +1 строка кромки погонными метрами (кромкование по умолчанию включено) — ищем
        // строку доски по unit, а не по индексу [0] (кромка без материала сортируется первой).
        Assert.AreEqual(2, result.lines.Count, "доски слились в одну группу + строка кромки");
        var board = result.lines.Single(l => l.unit == SpecUnit.AreaM2);
        Assert.AreEqual(2, board.count);
        Assert.AreEqual("Глухой 16*4*7:Низ", board.grooves);
    }

    [Test]
    public void ToCsv_HasGroovesColumnWithKindAndSide()
    {
        var part = CreatePart("Board", new Vector3Int(800, 400, 18));
        part.AddGroove(new GrooveSpec(GrooveKind.Through, GrooveSide.Right));

        var result = SpecificationManager.Build(new List<KitchenElement> { part });
        string csv = SpecificationExport.ToCsv(result);

        var rows = csv.Replace("\r\n", "\n").Trim().Split('\n');
        var header = rows[0].Split(';');
        int grooveCol = System.Array.IndexOf(header, "Grooves");
        int nameCol = System.Array.IndexOf(header, "Name");
        Assert.AreNotEqual(-1, grooveCol, "в шапке есть колонка Grooves");

        // Данные — НЕ обязательно строка 1: кромкование по умолчанию включено, торцы этой
        // детали открыты, и Build добавляет ещё строку кромки погонными метрами (без
        // материала), которая сортируется РАНЬШЕ доски (пустой материал раньше любого
        // названия). Строку доски находим по колонке Name, а не по фиксированному индексу —
        // так замер переживёт появление новых строк-спутников до/после доски.
        var dataRow = rows.Skip(1).Select(r => r.Split(';')).Single(cells => cells[nameCol] == "Board");
        Assert.AreEqual("Сквозной 16*4*7:Право", dataRow[grooveCol]);
    }
}
