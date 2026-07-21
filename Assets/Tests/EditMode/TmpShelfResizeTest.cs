using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Временный: точечная проверка ресайз-снапа полки.</summary>
public class TmpShelfResizeTest
{
    private readonly List<GameObject> _spawned = new();

    [SetUp]
    public void SetUp()
    {
        KitchenSettings.Instance.SnapEnabled = true;
        KitchenSettings.Instance.SnapThreshold = 50f;
        KitchenSettings.Instance.BlockOnViolation = false;
        SnapSystem.VerboseLog = false;

        var path = Path.Combine(Application.dataPath, "../docs/example.save.json");
        var json = File.ReadAllText(path);
        var data = SaveLoadManager.Deserialize(json);
        _spawned.AddRange(SaveLoadManager.RestoreScene(data!));
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var e in Object.FindObjectsByType<KitchenElement>()) if (e != null) Object.DestroyImmediate(e.gameObject);
        PartRegistry.Clear(); GroupManager.Clear(); CommandStack.Clear();
    }

    [Test]
    public void Shelf_Growing_ShouldSnapToTop()
    {
        KitchenElement? shelf = null, top = null;
        foreach (var go in _spawned)
        {
            var e = go.GetComponent<KitchenElement>();
            if (e == null) continue;
            if (e.PartName == "A12_upper_shelf_2_1_2") shelf = e;
            if (e.PartName == "B12_upper_top") top = e;
        }
        Assert.IsNotNull(shelf, "shelf");
        Assert.IsNotNull(top, "top");

        // Устанавливаем размер и позицию как в текущей сцене
        shelf!.DimensionsMM = new Vector3Int(802, 331, 18);
        shelf.transform.position = new Vector3(1.576f, 1.831f, -3.454f);

        var savedPos = shelf.transform.position;
        var others = _spawned.Select(g => g.GetComponent<KitchenElement>())
            .Where(e => e != null && e != shelf).ToList()!;

        var errors = new List<string>();

        // Растём от 802 до 1050 с шагом 10 мм
        for (int w = 812; w <= 1050; w += 10)
        {
            shelf.DimensionsMM = new Vector3Int(w, 331, 18);
            shelf.transform.position = savedPos;

            var faces = shelf.GetFaces();
            bool foundAny = false;

            for (int f = 0; f < faces.Length; f++)
            {
                var mf = faces[f];
                bool rs = ResizeSnap.SnapDelta(mf.center, mf.normal, mf.rightAxis, mf.upAxis,
                    new Vector2(mf.size.x, mf.size.y), others, shelf, 0.05f, out float g);
                if (rs)
                {
                    TestContext.Progress.WriteLine($"  SNAP w={w} f{f} gap={g*1000f:F1}mm normal={mf.normal}");
                    foundAny = true;
                }
            }

            if (!foundAny)
            {
                // Проверяем Diagnose — может ли TrySnap найти то, что ResizeSnap пропустил
                var diag = SnapSystem.Diagnose(shelf, others, savedPos, maxNeighbors: 50);
                bool diagFound = diag.neighbors.Any(r => r.wouldSnap && r.withinThreshold);
                if (diagFound)
                {
                    var sn = diag.neighbors.First(r => r.wouldSnap);
                    errors.Add($"w={w}: Diagnose=wouldSnap({sn.name} gap={sn.gapMM:F1}mm ovl={sn.overlapRatio:P0}) but ResizeSnap=NONE");
                }
            }
        }

        // Восстанавливаем
        shelf.DimensionsMM = new Vector3Int(802, 331, 18);
        shelf.transform.position = savedPos;

        if (errors.Count > 0)
            Assert.Fail(string.Join("\n", errors));
        else
            Assert.Pass("All snap checks passed");
    }
}
