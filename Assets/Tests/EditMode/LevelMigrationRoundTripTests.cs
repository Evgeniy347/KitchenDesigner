using System.Collections.Generic;
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
/// достаточно испортить ЛЮБУЮ координату при восстановлении, чтобы упасть. Единственное
/// исключение — <see cref="ExemptFromExactCoordinateCheck"/>, деталь с ЗАДОКУМЕНТИРОВАННЫМ
/// и уже проверенным в другом месте сдвигом; список требует причины на каждую запись.
/// Все найденные дрейфы координат агрегируются в одно сообщение, а не только первый —
/// иначе второй и третий дрейф прячутся за первым же упавшим Assert.
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

    /// <summary>Деталь, у которой сдвиг при восстановлении ЗАДОКУМЕНТИРОВАН и ОЖИДАЕМ —
    /// не находка этого стража, а поведение другого, уже принятого. Формат — как
    /// <c>ValidationSnapshotReuseTests.OutOfTheCacheOnPurpose</c>: имя и причина рядом,
    /// так что расширение списка — осознанная правка, а не тихий обход красноты.</summary>
    private static readonly (string name, string why)[] ExemptFromExactCoordinateCheck =
    {
        ("Truba",
            "pipe-gap-scene.save.json заморожен с НАРОЧНО разомкнутым стыком: " +
            "PipeDocking.RepairJoint закрывает его при восстановлении (сдвиг ~0,775 мм, " +
            "в основном по Y) — задокументированное и проверенное поведение, см. " +
            "PipeGapSensorTests (строки 11-27) и ScenePipeJointGridRepairTests"),
    };

    private static string ExemptionReason(string elementName) =>
        ExemptFromExactCoordinateCheck.FirstOrDefault(e => e.name == elementName).why;

    private static bool IsExemptFromExactCoordinateCheck(string elementName) =>
        ExemptionReason(elementName) != null;

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

        var drifts = new List<string>();
        int checkedCount = 0;
        int exemptedCount = 0;
        foreach (var ed in data!.elements)
        {
            if (ed == null || string.IsNullOrEmpty(ed.name)) continue;
            Assert.IsTrue(liveByName.TryGetValue(ed.name, out var live),
                $"{fixtureName}: деталь {ed.name} из файла не нашлась в восстановленной сцене");

            if (IsExemptFromExactCoordinateCheck(ed.name))
            {
                exemptedCount++;
                TestContext.WriteLine(
                    $"{fixtureName}/{ed.name}: пропущена проверка точных координат — {ExemptionReason(ed.name)}");
                continue;
            }

            var wantPos = ed.Position;
            var gotPos = live!.transform.position;
            CompareField(drifts, ed.name, "position.x", wantPos.x, gotPos.x);
            CompareField(drifts, ed.name, "position.y", wantPos.y, gotPos.y);
            CompareField(drifts, ed.name, "position.z", wantPos.z, gotPos.z);

            var wantRot = ed.Rotation;
            var gotRot = live.transform.rotation;
            CompareField(drifts, ed.name, "rotation.x", wantRot.x, gotRot.x);
            CompareField(drifts, ed.name, "rotation.y", wantRot.y, gotRot.y);
            CompareField(drifts, ed.name, "rotation.z", wantRot.z, gotRot.z);
            CompareField(drifts, ed.name, "rotation.w", wantRot.w, gotRot.w);

            if (ed.Dimensions != live.DimensionsMM)
                drifts.Add($"{ed.name}: dimensionsMM {ed.Dimensions} -> {live.DimensionsMM}");

            checkedCount++;
        }
        Assert.Greater(checkedCount + exemptedCount, 0,
            $"{fixtureName}: фикстура не содержит именованных деталей — нечего проверять");

        Assert.IsEmpty(drifts,
            $"{fixtureName}: {drifts.Count} дрейф(а/ов) координат при загрузке (все, не только первый):\n"
            + string.Join("\n", drifts));

        AssertFixtureUntouched(fixtureName, hashBefore);
    }

    private static void CompareField(
        List<string> drifts, string elementName, string field, float want, float got)
    {
        if (want == got) return;
        drifts.Add($"{elementName}.{field}: {want:R} -> {got:R} (Δ={got - want:R})");
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

    /// <summary>
    /// Ни одна из четырёх фикстур не несёт <c>levels</c> — это старые сохранения, снятые
    /// до появления этажей. После восстановления они обязаны читаться как РОВНО один
    /// уровень «1 этаж» на отметке 0, и любая деталь (даже вовсе без <c>levelId</c>)
    /// обязана разрешаться именно в него — что и есть «миграция бесплатна» из
    /// docs/todo_evolution.md §3.4.
    /// </summary>
    [TestCaseSource(nameof(FixtureNames))]
    public void AfterMigration_ExactlyOneLevelExists_AndEveryElementResolvesToIt(string fixtureName)
    {
        var json = ReadFixture(fixtureName, out var hashBefore);
        var data = SaveLoadManager.Deserialize(json);
        Assert.IsNotNull(data, $"{fixtureName}: проект должен читаться из JSON");
        Assert.IsEmpty(data!.levels, $"{fixtureName}: фикстура обязана быть СТАРЫМ файлом без levels");

        SaveLoadManager.RestoreScene(data);

        Assert.AreEqual(1, LevelRegistry.Items.Count,
            $"{fixtureName}: старый файл обязан мигрировать ровно в один уровень");
        var only = LevelRegistry.Items[0];
        Assert.AreEqual(0, only.floorElevationMm, $"{fixtureName}: единственный уровень обязан быть на отметке 0");
        Assert.AreEqual(LevelResolution.DefaultLevelName, only.name);

        var effective = new Level[LevelRegistry.Items.Count];
        for (int i = 0; i < effective.Length; i++) effective[i] = LevelRegistry.Items[i];

        foreach (var ed in data.elements)
        {
            if (ed == null) continue;
            var resolved = LevelResolution.ResolveElementLevel(ed.levelId, effective);
            Assert.AreSame(only, resolved,
                $"{fixtureName}/{ed.name}: деталь обязана разрешаться в единственный мигрированный уровень");
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
        LevelRegistry.Reset();
    }
}
