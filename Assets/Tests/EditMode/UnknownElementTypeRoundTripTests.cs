using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;

/// <summary>Круг «сохранено НОВОЙ версией → открыто СТАРОЙ → сохранено старой →
/// открыто новой» на замороженных данных.
///
/// Дефект, ради которого набор написан: пользователь завёл объект нового типа,
/// сохранил проект, откатил программу на прежнюю версию — и объект молча стал
/// обычной деталью. Не «не открылся», а ПОДМЕНИЛСЯ: следующее сохранение уносило
/// подмену в файл насовсем, и лечилось это только удалением объекта и созданием
/// заново уже в новой версии.
///
/// Ловушка здесь в сериализаторе Unity: при обычном разборе он молча выбрасывает
/// поле, которого нет в типе. Поэтому запись элемента доживает до записи файла
/// СЫРЫМ текстом (<see cref="RawElementRecords"/>), а известные поля берутся из
/// свежего снимка — иначе передвинутый объект уезжал бы в файл на старом месте.
///
/// Фикстура <c>Fixtures/newer-version-unknown-type.save.json</c> изображает файл,
/// сделанный версией 99.0: обычная деталь и объект типа <c>star_projector</c> с
/// тремя полями и вложенным объектом, которых эта версия не знает. Живой
/// <c>docs/example.save.json</c> тесты не читают (agents/TESTS.md).
///
/// Проверка идёт ПОЛЕ В ПОЛЕ, а не «не упало»: неизвестные ключи обязаны уехать
/// в файл слово в слово, вместе со вложенным объектом.</summary>
public class UnknownElementTypeRoundTripTests
{
    private const string NewerFixture = "Fixtures/newer-version-unknown-type.save.json";
    private const string OldFixture = "Fixtures/pillar-beside-plinth.save.json";

    private const string UnknownName = "Zvezdnyy_proektor_1";
    private const string UnknownTypeId = "star_projector";
    private const string KnownName = "Detal_1";

    private static readonly string[] FieldsThisVersionDoesNotKnow =
    {
        "isStarProjector", "starBeamCount", "starTintId", "starNested",
    };

    private string _newerJson = "";
    private string _oldJson = "";
    private ProjectLoadStateGuard? _guard;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _newerJson = ReadFixture(NewerFixture);
        _oldJson = ReadFixture(OldFixture);

        _guard = ProjectLoadStateGuard.Capture();
        var settings = KitchenSettings.Instance;
        Assert.IsNotNull(settings);
        settings!.AutoSave = false;
    }

    [TearDown]
    public void TearDown() => ClearScene();

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        ClearScene();
        _guard?.Restore();
        _guard = null;
    }

    [Test]
    public void TheFixture_CarriesFieldsThisVersionHasNoSlotFor_OrTheSetChecksNothing()
    {
        var record = RecordOf(_newerJson, UnknownName);
        var slots = typeof(ElementData).GetFields().Select(f => f.Name).ToArray();

        foreach (var key in FieldsThisVersionDoesNotKnow)
        {
            Assert.IsTrue(KeysOf(record).Contains(key), $"фикстура потеряла поле {key}");
            CollectionAssert.DoesNotContain(slots, key,
                $"поле {key} появилось в ElementData — фикстура перестала изображать "
                + "объект БУДУЩЕГО типа, и весь набор зазеленел бы вхолостую");
        }
    }

    [Test]
    public void AnUnknownType_OpensAsAPlainBoard_SoTheProjectStaysWorkable()
    {
        RestoreNewerFixture();

        var element = ByName(UnknownName);
        Assert.AreEqual(new Vector3Int(300, 200, 300), element.DimensionsMM,
            "объект обязан открыться со своими размерами, а не исчезнуть");
        Assert.AreEqual(UnknownTypeId, ElementTypeId.Of(element),
            "и помнить, чем он был в файле");
    }

    [Test]
    public void AnUnknownType_IsFlaggedWithTyp01_NotPassedOffAsABoard()
    {
        RestoreNewerFixture();

        var issue = SceneAnalyzer.Analyze()
            .FirstOrDefault(i => i.Code == IssueCatalog.CodeUnknownElementType);

        Assert.AreEqual(IssueCatalog.CodeUnknownElementType, issue.Code,
            "объект неизвестного типа обязан быть помечен нарушением");
        Assert.AreSame(ByName(UnknownName), issue.Target);
        StringAssert.Contains(UnknownTypeId, issue.Message,
            "и нарушение обязано назвать тип, которого эта версия не знает");
    }

    [Test]
    public void AKnownTypeInTheSameFile_IsNotFlagged()
    {
        RestoreNewerFixture();

        var flagged = SceneAnalyzer.Analyze()
            .Where(i => i.Code == IssueCatalog.CodeUnknownElementType)
            .Select(i => i.Target != null ? i.Target!.PartName : "")
            .ToArray();

        CollectionAssert.DoesNotContain(flagged, KnownName,
            "обычная деталь из того же файла нарушением не помечается");
        Assert.AreEqual(1, flagged.Length, "и нарушение ровно одно, а не на каждой детали");
    }

    [Test]
    public void TheCircle_ReturnsEveryUnknownFieldWordForWord()
    {
        RestoreNewerFixture();
        string saved = SaveScene();

        var before = MembersOf(RecordOf(_newerJson, UnknownName));
        var after = MembersOf(RecordOf(saved, UnknownName));

        foreach (var key in before.Keys)
            Assert.IsTrue(after.ContainsKey(key),
                $"круг потерял поле {key}: ровно так объект и превращался в деталь");

        foreach (var key in FieldsThisVersionDoesNotKnow)
            Assert.AreEqual(Compact(before[key]), Compact(after[key]),
                $"поле {key} обязано уехать в файл ровно таким, каким пришло");

        Assert.AreEqual("\"" + UnknownTypeId + "\"", after["elementType"],
            "тип обязан остаться прежним, иначе следующая версия его не узнает");
    }

    [Test]
    public void TheCircle_KeepsTheKnownFieldsTheUserCanSee()
    {
        RestoreNewerFixture();
        var reopened = Deserialize(SaveScene());
        var data = ElementDataOf(reopened, UnknownName);

        Assert.AreEqual(new Vector3Int(300, 200, 300), data.Dimensions);
        Assert.AreEqual(1.5f, data.Position.x, 1e-4f);
        Assert.AreEqual(0.5f, data.Position.y, 1e-4f);
        Assert.AreEqual(-2.0f, data.Position.z, 1e-4f);
    }

    [Test]
    public void TheCircle_CarriesTheNewPosition_WhenTheUserMovedTheUnknownObject()
    {
        RestoreNewerFixture();
        ByName(UnknownName).transform.position = new Vector3(2.5f, 0.5f, -2.0f);

        string saved = SaveScene();
        var after = MembersOf(RecordOf(saved, UnknownName));

        Assert.AreEqual(2.5f, ElementDataOf(Deserialize(saved), UnknownName).Position.x, 1e-4f,
            "передвинутый объект обязан уехать на НОВОМ месте: сырая запись не имеет "
            + "права заморозить то, что пользователь только что подвинул");
        Assert.AreEqual("7", Compact(after["starBeamCount"]),
            "и при этом не растерять поля, которых эта версия не знает");
    }

    [Test]
    public void AFileWithoutUnknownTypes_LosesNothingAndIsFlaggedNowhere()
    {
        var objects = SaveLoadManager.RestoreScene(Deserialize(_oldJson));
        Assert.IsNotEmpty(objects);

        var issues = SceneAnalyzer.Analyze()
            .Where(i => i.Code == IssueCatalog.CodeUnknownElementType)
            .ToArray();
        CollectionAssert.IsEmpty(issues, "в этом файле незнакомых типов нет");

        var before = Deserialize(_oldJson);
        var after = Deserialize(SaveScene());
        CollectionAssert.AreEquivalent(
            before.elements.Select(e => e.name).ToArray(),
            after.elements.Select(e => e.name).ToArray(),
            "круг не имеет права ни потерять деталь, ни выдумать новую");
    }

    [Test]
    public void AFileWithoutTheVersionField_CarriesNoVersion_AndRaisesNoWarning()
    {
        Assert.AreEqual("", ProjectFileVersion.In(_oldJson),
            "весь сегодняшний парк сохранений такой — поля версии в них нет");
        Assert.IsFalse(ProjectVersionNotice.FileIsNewerThanApp(
            ProjectFileVersion.In(_oldJson), BuildInfo.Version));
    }

    [Test]
    public void AFileFromAMuchNewerBuild_IsRecognisedAsNewer()
    {
        Assert.AreEqual("99.0", ProjectFileVersion.In(_newerJson));
        Assert.IsTrue(ProjectVersionNotice.FileIsNewerThanApp(
            ProjectFileVersion.In(_newerJson), BuildInfo.Version));
    }

    [Test]
    public void ASavedProject_CarriesTheVersionItWasMadeBy()
    {
        RestoreNewerFixture();

        Assert.AreEqual(BuildInfo.Version, ProjectFileVersion.In(SaveScene()),
            "сохранение обязано называть свою версию — иначе следующая старая версия "
            + "снова промолчит");
    }

    private void RestoreNewerFixture()
    {
        var objects = SaveLoadManager.RestoreScene(Deserialize(_newerJson));
        Assert.IsNotEmpty(objects);
    }

    private static string SaveScene() =>
        SaveLoadManager.Serialize(SaveLoadManager.CaptureScene(PartRegistry.GetAll()));

    private static ProjectData Deserialize(string json)
    {
        var data = SaveLoadManager.Deserialize(json);
        Assert.IsNotNull(data);
        return data!;
    }

    private static ElementData ElementDataOf(ProjectData data, string name)
    {
        var found = data.elements.FirstOrDefault(e => e != null && e.name == name);
        Assert.IsNotNull(found, $"в данных проекта нет элемента {name}");
        return found!;
    }

    private static string RecordOf(string projectJson, string name)
    {
        foreach (var record in RawElementRecords.Extract(projectJson))
            if (MembersOf(record).TryGetValue("name", out var value)
                && value == "\"" + name + "\"")
                return record;
        Assert.Fail($"в тексте проекта нет записи элемента {name}");
        return "";
    }

    private static Dictionary<string, string> MembersOf(string record)
    {
        var members = new Dictionary<string, string>();
        foreach (var member in JsonText.Members(record, JsonText.RootObject(record)))
            members[member.Key] = member.Value.Text(record);
        return members;
    }

    private static IEnumerable<string> KeysOf(string record) => MembersOf(record).Keys;

    private static string Compact(string value) =>
        new string(value.Where(c => !char.IsWhiteSpace(c)).ToArray());

    private static string ReadFixture(string relative)
    {
        var fullPath = Path.Combine(Application.dataPath, "Tests/EditMode", relative);
        Assert.IsTrue(File.Exists(fullPath), $"фикстура не найдена: {fullPath}");
        var text = File.ReadAllText(fullPath);
        Assert.IsNotEmpty(text);
        return text;
    }

    private static KitchenElement ByName(string partName)
    {
        var found = PartRegistry.GetAll().FirstOrDefault(e => e != null && e.PartName == partName);
        Assert.IsNotNull(found, $"в восстановленной сцене нет детали {partName}");
        return found!;
    }

    private static void ClearScene()
    {
        foreach (var e in UnityEngine.Object.FindObjectsByType<KitchenElement>(FindObjectsSortMode.None))
            if (e != null) UnityEngine.Object.DestroyImmediate(e.gameObject);

        PartRegistry.Clear();
        GroupManager.Clear();
        CommandStack.Clear();
        ElementFactory.ClearPools();
        MaterialManager.ClearCache();

        ProjectInstructions.Reset();
        ProjectRooms.Reset();
        ProjectFloorplans.Reset();
    }
}
