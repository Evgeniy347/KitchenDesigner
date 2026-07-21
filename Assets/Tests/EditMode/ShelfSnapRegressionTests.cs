using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Прицельный тест бага A12_upper_shelf_2_1_2: проверяет, что снап срабатывает
/// по всем осям с ожидаемыми соседями.
/// </summary>
public class ShelfSnapRegressionTests
{
    private readonly List<GameObject> _spawned = new();

    [SetUp]
    public void SetUp()
    {
        KitchenSettings.Instance.SnapEnabled = true;
        KitchenSettings.Instance.SnapThreshold = 50f;
        KitchenSettings.Instance.BlockOnViolation = false;
        SnapSystem.VerboseLog = false;

        var fullPath = Path.Combine(Application.dataPath, "../docs/example.save.json");
        var json = File.ReadAllText(fullPath);
        var data = SaveLoadManager.Deserialize(json);
        _spawned.AddRange(SaveLoadManager.RestoreScene(data!));
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.DestroyImmediate(e.gameObject);
        PartRegistry.Clear();
        GroupManager.Clear();
        CommandStack.Clear();
    }

    [Test]
    public void Shelf_ShouldSnapTo_Top_Bottom_And_Side()
    {
        KitchenElement? shelf = null, top = null, bottom = null, side = null;
        foreach (var go in _spawned)
        {
            var e = go.GetComponent<KitchenElement>();
            if (e == null) continue;
            switch (e.PartName)
            {
                case "A12_upper_shelf_2_1_2": shelf = e; break;
                case "B12_upper_top": top = e; break;
                case "B12_upper_bottom": bottom = e; break;
                case "A12_upper_A_side": side = e; break;
            }
        }

        Assert.IsNotNull(shelf, "Shelf not found");
        Assert.IsNotNull(top, "Top not found");
        Assert.IsNotNull(bottom, "Bottom not found");
        Assert.IsNotNull(side, "Side not found");

        var savedPos = shelf!.transform.position;

        // ── Test 1: Shelf top face (Y+) → top bottom face (Y-) ───
        {
            Vector3 testPos = savedPos - new Vector3(0f, 0.01f, 0f);
            var r = SnapSystem.TrySnap(shelf, new List<KitchenElement> { top! }, testPos);
            Assert.IsTrue(r.snapped,
                $"Shelf→Top: should snap when moved DOWN from {savedPos.y:F3} to {testPos.y:F3}.");
            shelf.transform.position = savedPos;
        }

        // ── Test 2: Shelf right face (X+) → top right face (X+) — edge align ───
        {
            Vector3 testPos = savedPos - new Vector3(0.015f, 0f, 0f);
            var r = SnapSystem.TrySnap(shelf, new List<KitchenElement> { top! }, testPos);
            Assert.IsTrue(r.snapped,
                $"Shelf→Top X-edge: should snap when moved LEFT.");
            shelf.transform.position = savedPos;
        }

        // ── Test 3: Shelf→A12_upper_A_side (Z-axis snap) ───
        {
            Vector3 testPos = savedPos - new Vector3(0f, 0f, 0.01f);
            var r = SnapSystem.TrySnap(shelf, new List<KitchenElement> { side! }, testPos);
            Assert.IsTrue(r.snapped,
                $"Shelf→Side Z: should snap along Z axis.");
            shelf.transform.position = savedPos;
        }

        // ── Test 4: Shelf→B12_upper_bottom (Y-axis) ───
        {
            Vector3 testPos = savedPos + new Vector3(0f, 0.01f, 0f);
            var r = SnapSystem.TrySnap(shelf, new List<KitchenElement> { bottom! }, testPos);
            Assert.IsTrue(r.snapped,
                $"Shelf→Bottom: should snap when moved UP.");
            shelf.transform.position = savedPos;
        }

        // ── Test 5: Shelf bottom face (Y-) → side top face (Y+) ───
        {
            Vector3 testPos = savedPos + new Vector3(0f, 0.015f, 0f);
            var r = SnapSystem.TrySnap(shelf, new List<KitchenElement> { side! }, testPos);
            Assert.IsTrue(r.snapped,
                $"Shelf→Side Y: should snap along Y axis.");
            shelf.transform.position = savedPos;
        }

        // ── Test 6: Resize snap — reduce shelf width, should snap to top ───
        {
            var savedDims = shelf.DimensionsMM;
            int newWidth = savedDims.x - 100;
            Assert.Greater(newWidth, 0);
            shelf.DimensionsMM = new Vector3Int(newWidth, savedDims.y, savedDims.z);

            // Check if resize snap would trigger — verify via SnapDelta for any face
            var others = new List<KitchenElement>();
            foreach (var e in Object.FindObjectsByType<KitchenElement>())
                if (e != null && e != shelf && e.gameObject.activeInHierarchy)
                    others.Add(e);

            float negDelta = (newWidth - savedDims.x) * 0.5f * 0.001f;
            var allFaces = shelf.GetFaces();
            bool anySnap = false;
            for (int f = 0; f < allFaces.Length; f++)
            {
                var face = allFaces[f];
                Vector3 cand = face.center + face.normal * negDelta;
                if (ResizeSnap.SnapDelta(cand, face.normal, face.rightAxis, face.upAxis,
                    new Vector2(face.size.x, face.size.y), others, shelf, 0.05f, out float g))
                {
                    anySnap = true;
                    TestContext.Progress.WriteLine($"  ResizeSnap OK: f{f} gap={g*1000f:F1}mm");
                }
            }
            Assert.IsTrue(anySnap,
                $"ResizeSnap: reducing width {savedDims.x}→{newWidth} should snap to neighbor.");

            shelf.DimensionsMM = savedDims;
            shelf.transform.position = savedPos;
        }
    }
}
