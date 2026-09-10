using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;

/// <summary>save_project/load_project — единственный внешний способ снаружи MCP
/// сохранить и заново открыть проект: без них агент мог создавать и удалять
/// элементы, но не мог унести результат и не мог открыть существующий файл.
/// Оба теста ниже стерегут ПРОТИВОПОЛОЖНЫЕ входы: сохранение/загрузка дают ту же
/// сцену, а отсутствующий/битый файл дают внятный отказ и не трогают сцену -
/// не тихий пустой успех.</summary>
public class McpProjectSaveLoadTests : McpTestFixture
{
    private string? _tempFile;

    [SetUp]
    public void ConfigureSaveDirectory()
    {
        McpSaveDirectoryStatus.TestDirectory = Application.temporaryCachePath;
    }

    [TearDown]
    public void CleanupTempFile()
    {
        if (!string.IsNullOrEmpty(_tempFile) && File.Exists(_tempFile)) File.Delete(_tempFile);
        DemoMode.ResetCurrent();
        McpSaveDirectoryStatus.ResetForTests();
    }

    private string TempPath()
    {
        _tempFile = Path.Combine(Application.temporaryCachePath, $"mcp_save_{System.Guid.NewGuid():N}.json");
        return _tempFile;
    }

    private static string ErrorMessage(McpResponse resp) =>
        JObject.FromObject(resp.data!)["message"]!.Value<string>()!;

    [Test]
    public void SaveProject_ThenLoadProject_RestoresDimensionsAndPosition()
    {
        var original = MakeElement("Board1", new Vector3Int(600, 400, 18), new Vector3(1.5f, 0f, 2.25f));
        string path = TempPath();

        var saveResp = _handler!.Handle(MakeReq("save_project", new { path }));
        Assert.AreEqual("result", saveResp.type, saveResp.type == "error" ? ErrorMessage(saveResp) : "");
        Assert.IsTrue(File.Exists(path), "save_project отчитался ok, но файла на диске нет");

        Object.DestroyImmediate(original.gameObject);
        PartRegistry.Clear();

        var loadResp = _handler.Handle(MakeReq("load_project", new { path }));
        Assert.AreEqual("result", loadResp.type, loadResp.type == "error" ? ErrorMessage(loadResp) : "");

        var restored = PartRegistry.GetAll().Find(e => e != null && e.PartName == "Board1");
        Assert.IsNotNull(restored, "сохранённая деталь обязана вернуться под тем же именем после load_project");
        Assert.AreEqual(new Vector3Int(600, 400, 18), restored!.DimensionsMM,
            "габариты должны пережить полный цикл сохранение -> загрузка");
        Assert.AreEqual(1.5f, restored.transform.position.x, 1e-3f, "позиция X должна пережить цикл");
        Assert.AreEqual(2.25f, restored.transform.position.z, 1e-3f, "позиция Z должна пережить цикл");
    }

    [Test]
    public void LoadProject_MissingFile_IsRejected_SceneUntouched()
    {
        MakeElement("Board1", new Vector3Int(600, 400, 18));
        string path = Path.Combine(Application.temporaryCachePath, $"mcp_missing_{System.Guid.NewGuid():N}.json");

        LogAssert.Expect(LogType.Error, $"[SaveLoad] File not found: {path}");
        var resp = _handler!.Handle(MakeReq("load_project", new { path }));

        Assert.AreEqual("error", resp.type,
            "загрузка отсутствующего файла обязана быть внятным отказом, а не тихим пустым успехом");
        StringAssert.Contains(path, ErrorMessage(resp), "причина отказа обязана называть путь");
        Assert.AreEqual(1, PartRegistry.GetAll().Count, "неудачная загрузка не должна трогать сцену");
    }

    [Test]
    public void LoadProject_CorruptFile_IsRejected_SceneUntouched()
    {
        MakeElement("Board1", new Vector3Int(600, 400, 18));
        string path = TempPath();
        File.WriteAllText(path, "{ this is not valid json ][");

        LogAssert.Expect(LogType.Error, new Regex(@"^\[SaveLoad\] Load failed: .*Missing a name for object member.*"));
        var resp = _handler!.Handle(MakeReq("load_project", new { path }));

        Assert.AreEqual("error", resp.type,
            "битый файл проекта обязан быть отказом, а не тихой пустой сценой");
        Assert.AreEqual(1, PartRegistry.GetAll().Count, "неудачная загрузка не должна трогать сцену");
    }

    [Test]
    public void SaveProject_MissingPath_IsRejected()
    {
        var resp = _handler!.Handle(MakeReq("save_project", new { }));
        Assert.AreEqual("error", resp.type,
            "path обязателен - молчаливый no-op выглядел бы как успешное сохранение");
    }

    [Test]
    public void SaveProject_WithoutConfiguredSaveDirectory_IsRefused()
    {
        McpSaveDirectoryStatus.TestDirectory = null;
        MakeElement("Board1", new Vector3Int(600, 400, 18));
        string path = TempPath();

        var resp = _handler!.Handle(MakeReq("save_project", new { path }));

        Assert.AreEqual("error", resp.type,
            "без -mcpSaveDir save_project обязан отказывать любому пути, а не тихо писать на диск");
        StringAssert.Contains(McpSaveDirectoryStatus.DirectoryArgument, ErrorMessage(resp),
            "отказ обязан называть параметр запуска, который включает сохранение");
        Assert.IsFalse(File.Exists(path));
    }

    [Test]
    public void SaveProject_OutsideTheConfiguredDirectory_IsRefused()
    {
        var outsideDir = Path.Combine(Application.temporaryCachePath, $"mcp_outside_{System.Guid.NewGuid():N}");
        Directory.CreateDirectory(outsideDir);
        McpSaveDirectoryStatus.TestDirectory = outsideDir;
        MakeElement("Board1", new Vector3Int(600, 400, 18));
        string path = TempPath(); // under Application.temporaryCachePath itself, NOT under outsideDir

        var resp = _handler!.Handle(MakeReq("save_project", new { path }));

        Assert.AreEqual("error", resp.type,
            "путь за пределами -mcpSaveDir обязан быть отказом, даже если каталог настроен");
        StringAssert.Contains(path, ErrorMessage(resp));
        Assert.IsFalse(File.Exists(path));

        Directory.Delete(outsideDir, recursive: true);
    }

    [Test]
    public void SaveProject_OntoTheDemoFile_IsRejectedByName_NotSilently()
    {
        string demoPath = TempPath();
        DemoMode.Current.Enter(demoPath);
        MakeElement("Board1", new Vector3Int(600, 400, 18));

        var resp = _handler!.Handle(MakeReq("save_project", new { path = demoPath }));

        Assert.AreEqual("error", resp.type,
            "перезапись демо-файла через MCP обязана быть отказом, как и из интерфейса "
            + "(SaveLoadManagerInstance.SaveToPath), а не тихим no-op");
        StringAssert.Contains(demoPath, ErrorMessage(resp));
    }
}
