using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class DemoProjectLoaderCommandLineTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private string? _prevLastPath;
    private string _path = "";

    [SetUp]
    public void Setup() => _prevLastPath = SaveLoadManager.LastPath;

    [TearDown]
    public void TearDown()
    {
        SaveLoadManager.LastPath = _prevLastPath!;
        foreach (var e in Object.FindObjectsByType<KitchenElement>(FindObjectsSortMode.None))
            if (e != null) Object.DestroyImmediate(e.gameObject);
        _spawned.Clear();
        if (!string.IsNullOrEmpty(_path) && File.Exists(_path)) File.Delete(_path);
    }

    private KitchenElement Make(string name, Vector3Int dims, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var e = go.AddComponent<KitchenElement>();
        e.PartName = name;
        e.DimensionsMM = dims;
        PartRegistry.Register(e);
        _spawned.Add(go);
        return e;
    }

    [Test]
    public void TryOpenFromCommandLine_WithAKdprojArgument_LoadsThatFile()
    {
        Make("CmdLineBoard", new Vector3Int(800, 400, 18), Vector3.zero);
        _path = Path.Combine(Application.temporaryCachePath, "cmdline_open.kdproj");
        Assert.IsTrue(SaveLoadManager.SaveToPath(_path));

        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();

        bool opened = DemoProjectLoader.TryOpenFromCommandLine(SaveLoadManager.Instance, new[] { _path });

        Assert.IsTrue(opened);
        var restored = Object.FindObjectsByType<KitchenElement>(FindObjectsSortMode.None);
        Assert.AreEqual(1, restored.Length);
        Assert.AreEqual("CmdLineBoard", restored[0].PartName);
    }

    [Test]
    public void TryOpenFromCommandLine_WithNoRecognizedArgument_ReturnsFalse()
    {
        bool opened = DemoProjectLoader.TryOpenFromCommandLine(
            SaveLoadManager.Instance, new[] { "-mcpPort", "9337" });
        Assert.IsFalse(opened);
    }

    [Test]
    public void TryOpenFromCommandLine_WithAPathThatDoesNotExist_ReturnsFalseWithoutThrowing()
    {
        bool opened = DemoProjectLoader.TryOpenFromCommandLine(
            SaveLoadManager.Instance,
            new[] { Path.Combine(Application.temporaryCachePath, "nope.kdproj") });
        Assert.IsFalse(opened);
    }
}
