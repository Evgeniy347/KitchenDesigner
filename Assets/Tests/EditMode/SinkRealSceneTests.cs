using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Мойка на НАСТОЯЩЕЙ сцене: docs/example.save.json целиком, со всеми
/// модулями, боковинами и ящиками. Синтетическая пара «столешница + мойка» такие
/// поломки не ловит — здесь мойку окружает больше сотни деталей.</summary>
public class SinkRealSceneTests
{
    private const string SaveFileName = "example.save.json";
    private string _json = "";
    private ProjectLoadStateGuard? _guard;

    [SetUp]
    public void SetUp() => _guard = ProjectLoadStateGuard.Capture();

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var fullPath = Path.Combine(Application.dataPath, "../docs", SaveFileName);
        Assert.IsTrue(File.Exists(fullPath), $"Save file not found: {fullPath}");
        _json = File.ReadAllText(fullPath);
    }

    [TearDown]
    public void TearDown()
    {
        _guard?.Restore();
        foreach (var e in Object.FindObjectsByType<KitchenElement>(FindObjectsSortMode.None))
            if (e != null) Object.DestroyImmediate(e.gameObject);
        PartRegistry.Clear();
        GroupManager.Clear();
        CommandStack.Clear();
    }

    private List<KitchenElement> RestoreScene()
    {
        var data = SaveLoadManager.Deserialize(_json);
        Assert.IsNotNull(data);
        var objs = SaveLoadManager.RestoreScene(data!);
        Assert.IsNotEmpty(objs);
        return objs.Select(g => g.GetComponent<KitchenElement>()).Where(e => e != null).ToList()!;
    }

    [Test]
    public void Moyka_CutsTheCountertopItStandsOn()
    {
        var elements = RestoreScene();
        var sink = elements.OfType<SinkElement>().FirstOrDefault();
        Assert.IsNotNull(sink, "в сцене есть мойка Moyka");

        // Сцена восстановлена — в игре это делает Start(); в EditMode зовём руками.
        sink!.SnapToPart();

        var host = elements.FirstOrDefault(e => e != null && e.HasCutout(sink));
        Assert.IsNotNull(host, Diagnose(elements, sink));
        Assert.AreEqual("Countertop_B", host!.PartName);

        var mesh = host.GetComponent<MeshFilter>().sharedMesh;
        // Коробка без выреза — ровно 24 вершины (6 граней × 4).
        Assert.Greater(mesh.vertexCount, 24, "столешница получила меш с вырезом");

        var rect = sink.CutoutRectIn(host);
        int upAxis = SinkElement.HoleAxisFor(host);
        int a = upAxis == 2 ? 0 : upAxis == 1 ? 0 : 2;
        int b = upAxis == 2 ? 1 : upAxis == 1 ? 2 : 1;
        foreach (var v in mesh.vertices)
        {
            bool onFace = Mathf.Abs(Mathf.Abs(v[upAxis]) - 0.5f) < 1e-4f;
            if (!onFace) continue;
            bool inside = v[a] > rect.xMin + 1e-4f && v[a] < rect.xMax - 1e-4f &&
                          v[b] > rect.yMin + 1e-4f && v[b] < rect.yMax - 1e-4f;
            Assert.IsFalse(inside, $"вершина {v} внутри проёма — вырез не сквозной");
        }
    }

    /// <summary>Разбор «почему не прилипла» — идёт в текст падения, чтобы не
    /// гадать по зелёному/красному.</summary>
    private static string Diagnose(List<KitchenElement> elements, SinkElement sink)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"мойка не врезалась ни в одну деталь; поза {sink.transform.position}");
        foreach (var el in elements)
        {
            if (el == null || el == sink) continue;
            if (!SinkElement.IsSuitableHost(el)) continue;
            var d = el.DimensionsMM;
            sb.AppendLine($"  кандидат {el.PartName} {d.x}x{d.y}x{d.z} @ {el.transform.position} " +
                          $"upAxis={SinkElement.HoleAxisFor(el)} {sink.DescribeCatch(el)}");
        }
        return sb.ToString();
    }
}
