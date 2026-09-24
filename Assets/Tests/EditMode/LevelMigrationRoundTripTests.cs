using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// L0 миграционного страж этажности (docs/todo_evolution.md §3.4, план LEVELS → L0).
///
/// Это ЖЁСТКОЕ ограничение всей кампании: открытие уже существующего сохранения не имеет
/// права сдвинуть НИ ОДНУ координату. Ни position, ни rotation, ни dimensionsMM — ни на
/// миллиметр, потому что «этажность» ложится на код, который каждый пользовательский
/// проект уже проходит на каждом открытии (SceneRestorer.Restore), и любая ошибка в новом
/// коде тут же портит все существующие проекты, а не только новые.
///
/// Фикстуры — реальные пользовательские сцены, замороженные один раз (см.
/// PillarSeatRoundTripTests — тот же приём и та же причина: живой
/// <c>docs/example.save.json</c> переписывается автосохранением десктопа и тестам
/// не принадлежит). <c>levels-baseline.save.json</c> — снимок HEAD:docs/example.save.json,
/// снятый специально для этой кампании (`git show HEAD:docs/example.save.json`), три
/// остальные фикстуры уже существовали и проверяют другие сцены (стены+трубы,
/// опора+цоколь, панель валидации).
///
/// Тест обязан покраснеть, если открытие сдвинет Y хотя бы на 1 мм — сравнение везде
/// идёт БЕЗ допуска (точное равенство float), поэтому доказывать это отдельно не нужно:
/// достаточно испортить ЛЮБУЮ координату при восстановлении, чтобы упасть.
/// </summary>
public class LevelMigrationRoundTripTests
{
    private static readonly string[] FixtureNames =
    {
        "Fixtures/levels-baseline.save.json",
        "Fixtures/validation-scene.save.json",
        "Fixtures/pipe-gap-scene.save.json",
        "Fixtures/pillar-beside-plinth.save.json",
    };

    private ProjectLoadStateGuard? _guard;

    [SetUp]
    public void SetUp()
    {
        _guard = ProjectLoadStateGuard.Capture();
        KitchenSettings.Instance.AutoSave = false;
    }

    [TearDown]
    public void TearDown()
    {
        ClearScene();
        _guard?.Restore();
        _guard = null;
    }

    private static string FullPath(string fixtureName) =>
        Path.Combine(Application.dataPath, "Tests/EditMode", fixtureName);

    private static string Sha256Of(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return System.Convert.ToBase64String(sha.ComputeHash(bytes));
    }

    private static string ReadFixture(string fixtureName, out string hashBefore)
    {
        var fullPath = FullPath(fixtureName);
        Assert.IsTrue(File.Exists(fullPath), $"фикстура не найдена: {fullPath}");
        var rawBytes = File.ReadAllBytes(fullPath);
        hashBefore = Sha256Of(rawBytes);
        return System.Text.Encoding.UTF8.GetString(rawBytes);
    }

    private static void AssertFixtureUntouched(string fixtureName, string hashBefore)
    {
        var hashAfter = Sha256Of(File.ReadAllBytes(FullPath(fixtureName)));
        Assert.AreEqual(hashBefore, hashAfter,
            $"{fixtureName}: фикстура не должна меняться на диске от одного факта прогона теста " +
            "(agents/TESTS.md — фикстуры замораживаются один раз и больше не трогаются)");
    }

    [TestCaseSource(nameof(FixtureNames))]
    public void RestoringAnExistingSave_MovesNoElementCoordinate(string fixtureName)
    {
        var json = ReadFixture(fixtureName, out var hashBefore);

        var data = SaveLoadManager.Deserialize(json);
        Assert.IsNotNull(data, $"{fixtureName}: проект должен читаться из JSON");

        var created = SaveLoadManager.RestoreScene(data!);
        Assert.IsNotEmpty(created, $"{fixtureName}: должен восстановиться хотя бы один элемент");

        var liveByName = PartRegistry.GetAll()
            .Where(e => e != null && !string.IsNullOrEmpty(e.PartName))
            .GroupBy(e => e.PartName)
            .ToDictionary(g => g.Key, g => g.First());

        int checkedCount = 0;
        foreach (var ed in data!.elements)
        {
            if (ed == null || string.IsNullOrEmpty(ed.name)) continue;
            Assert.IsTrue(liveByName.TryGetValue(ed.name, out var live),
                $"{fixtureName}: деталь {ed.name} из файла не нашлась в восстановленной сцене");

            var wantPos = ed.Position;
            var gotPos = live!.transform.position;
            Assert.AreEqual(wantPos.x, gotPos.x, 0f, $"{fixtureName}/{ed.name}: X сдвинулся при загрузке");
            Assert.AreEqual(wantPos.y, gotPos.y, 0f, $"{fixtureName}/{ed.name}: Y сдвинулся при загрузке");
            Assert.AreEqual(wantPos.z, gotPos.z, 0f, $"{fixtureName}/{ed.name}: Z сдвинулся при загрузке");

            var wantRot = ed.Rotation;
            var gotRot = live.transform.rotation;
            Assert.AreEqual(wantRot.x, gotRot.x, 0f, $"{fixtureName}/{ed.name}: rotation.x изменился");
            Assert.AreEqual(wantRot.y, gotRot.y, 0f, $"{fixtureName}/{ed.name}: rotation.y изменился");
            Assert.AreEqual(wantRot.z, gotRot.z, 0f, $"{fixtureName}/{ed.name}: rotation.z изменился");
            Assert.AreEqual(wantRot.w, gotRot.w, 0f, $"{fixtureName}/{ed.name}: rotation.w изменился");

            Assert.AreEqual(ed.Dimensions, live.DimensionsMM,
                $"{fixtureName}/{ed.name}: dimensionsMM изменились при загрузке");
            checkedCount++;
        }
        Assert.Greater(checkedCount, 0, $"{fixtureName}: фикстура не содержит именованных деталей — нечего проверять");

        AssertFixtureUntouched(fixtureName, hashBefore);
    }

    [TestCaseSource(nameof(FixtureNames))]
    public void SavingAndReloading_PreservesElementFieldsAndCamera_Exactly(string fixtureName)
    {
        var json = ReadFixture(fixtureName, out var hashBefore);

        var data = SaveLoadManager.Deserialize(json);
        Assert.IsNotNull(data, $"{fixtureName}: проект должен читаться из JSON");

        var reparsed = SaveLoadManager.Deserialize(SaveLoadManager.Serialize(data!));
        Assert.IsNotNull(reparsed, $"{fixtureName}: сериализованный проект должен читаться обратно");

        Assert.AreEqual(data!.camera.valid, reparsed!.camera.valid, $"{fixtureName}: camera.valid");
        Assert.AreEqual(data.camera.targetX, reparsed.camera.targetX, 0f, $"{fixtureName}: camera.targetX");
        Assert.AreEqual(data.camera.targetY, reparsed.camera.targetY, 0f, $"{fixtureName}: camera.targetY");
        Assert.AreEqual(data.camera.targetZ, reparsed.camera.targetZ, 0f, $"{fixtureName}: camera.targetZ");
        Assert.AreEqual(data.camera.angleX, reparsed.camera.angleX, 0f, $"{fixtureName}: camera.angleX");
        Assert.AreEqual(data.camera.angleY, reparsed.camera.angleY, 0f, $"{fixtureName}: camera.angleY");
        Assert.AreEqual(data.camera.distance, reparsed.camera.distance, 0f, $"{fixtureName}: camera.distance");

        Assert.AreEqual(data.elements.Length, reparsed.elements.Length,
            $"{fixtureName}: число деталей изменилось после круга сериализации");

        for (int i = 0; i < data.elements.Length; i++)
        {
            var before = data.elements[i];
            var after = reparsed.elements[i];
            if (before == null) { Assert.IsNull(after); continue; }
            Assert.IsNotNull(after, $"{fixtureName}: деталь #{i} ({before.name}) потерялась при круге");

            CollectionAssert.AreEqual(before.position, after!.position,
                $"{fixtureName}/{before.name}: position изменился при круге сериализации");
            CollectionAssert.AreEqual(before.rotation, after.rotation,
                $"{fixtureName}/{before.name}: rotation изменился при круге сериализации");
            CollectionAssert.AreEqual(before.dimensionsMM, after.dimensionsMM,
                $"{fixtureName}/{before.name}: dimensionsMM изменились при круге сериализации");
        }

        AssertFixtureUntouched(fixtureName, hashBefore);
    }

    private static void ClearScene()
    {
        foreach (var e in Object.FindObjectsByType<KitchenElement>(FindObjectsSortMode.None))
            if (e != null) Object.DestroyImmediate(e.gameObject);

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
