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

    /// <summary>dn20-труба несёт сечение 27 мм, чья половина (13,5 мм) всегда дробная —
    /// округление вершины меша при восстановлении почти для любой позиции сдвигает
    /// трубу/фитинг до ~1 мм на стыке (ScenePipeJointGridRepairTests, строки 19-22).
    /// <c>PipeDocking.RepairJoint</c> (зовётся из <c>SceneRestorer.RepairAutoSeatedJoints</c>
    /// для КАЖДОГО <c>IAutoSeated</c>, не только для одной детали) закрывает это на
    /// загрузке — задокументированное и проверенное поведение, не находка этого стража.
    /// Узел из пяти связанных деталей назван в <c>PipeGapSensorTests.AllFiveNames</c>
    /// (Truba, Otvod_92, Otvod_91, Podacha, Obratka); в исключение попадают только те,
    /// что реально дрейфуют в этой фикстуре — не весь узел заранее.</summary>
    private const string DnGridRoundingRepairReason =
        "pipe-gap-scene.save.json заморожен с НАРОЧНО разомкнутыми стыками dn20-трубопровода: " +
        "PipeDocking.RepairJoint закрывает их на загрузке (округление вершины меша у дробной " +
        "половины сечения, ~0,1-1 мм на деталь) — задокументированное и проверенное поведение, " +
        "см. ScenePipeJointGridRepairTests и сеть узла PipeGapSensorTests.AllFiveNames";

    /// <summary>Деталь, у которой сдвиг при восстановлении ЗАДОКУМЕНТИРОВАН и ОЖИДАЕМ —
    /// не находка этого стража, а поведение другого, уже принятого. Формат — как
    /// <c>ValidationSnapshotReuseTests.OutOfTheCacheOnPurpose</c>: имя и причина рядом,
    /// так что расширение списка — осознанная правка, а не тихий обход красноты.</summary>
    private static readonly (string name, string why)[] ExemptFromExactCoordinateCheck =
    {
        ("Truba", DnGridRoundingRepairReason),
        ("Otvod_91", DnGridRoundingRepairReason),
        ("Obratka", DnGridRoundingRepairReason),
    };

    private static string ExemptionReason(string elementName) =>
        ExemptFromExactCoordinateCheck.FirstOrDefault(e => e.name == elementName).why;

    private static bool IsExemptFromExactCoordinateCheck(string elementName) =>
        ExemptionReason(elementName) != null;

    /// <summary>review-perf-tests-tooling.md #5: исключение раньше пропускало position,
    /// rotation И dimensionsMM разом — регресс, который повернёт Truba на 90° или сдвинет её
    /// на полметра, прошёл бы тем же путём. Допуск — ТОЛЬКО на позицию и только для трёх
    /// перечисленных имён. 1,1 мм — с запасом над РЕАЛЬНО измеренным на pipe-gap-scene.save.json
    /// максимумом (Obratka 1,018 мм, Otvod_91 1,001 мм — округление вершины меша у дробной
    /// половины сечения dn20-стыка, PipeDocking.RepairJoint, см. ScenePipeJointGridRepairTests):
    /// заявленные ранее «~1 мм» (комментарий выше) и «~0,775 мм» (черновая оценка ревью) оба
    /// оказались НИЖЕ фактического дрейфа — измеренное число всегда важнее оценки на бумаге.
    /// Rotation и dimensionsMM для этих же имён по-прежнему сравниваются точно.</summary>
    private const float MaxExemptPositionDriftMm = 1.1f;

    private static void ComparePositionWithinTolerance(
        List<string> drifts, string elementName, Vector3 want, Vector3 got, float toleranceUnits)
    {
        float driftUnits = (got - want).magnitude;
        if (driftUnits <= toleranceUnits) return;
        drifts.Add($"{elementName}.position: {want} -> {got} (|Δ|={driftUnits / AppConstants.MM_TO_UNITS:R} мм " +
            $"> {toleranceUnits / AppConstants.MM_TO_UNITS:R} мм допуска — {ExemptionReason(elementName)})");
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

        var drifts = new List<string>();
        int checkedCount = 0;
        int exemptedCount = 0;
        foreach (var ed in data!.elements)
        {
            if (ed == null || string.IsNullOrEmpty(ed.name)) continue;
            Assert.IsTrue(liveByName.TryGetValue(ed.name, out var live),
                $"{fixtureName}: деталь {ed.name} из файла не нашлась в восстановленной сцене");

            var wantRot = ed.Rotation;
            var gotRot = live!.transform.rotation;
            var wantPos = ed.Position;
            var gotPos = live.transform.position;

            if (IsExemptFromExactCoordinateCheck(ed.name))
            {
                exemptedCount++;
                TestContext.WriteLine(
                    $"{fixtureName}/{ed.name}: допуск {MaxExemptPositionDriftMm} мм только на позицию — {ExemptionReason(ed.name)}");
                ComparePositionWithinTolerance(drifts, ed.name, wantPos, gotPos,
                    MaxExemptPositionDriftMm * AppConstants.MM_TO_UNITS);
                CompareField(drifts, ed.name, "rotation.x", wantRot.x, gotRot.x);
                CompareField(drifts, ed.name, "rotation.y", wantRot.y, gotRot.y);
                CompareField(drifts, ed.name, "rotation.z", wantRot.z, gotRot.z);
                CompareField(drifts, ed.name, "rotation.w", wantRot.w, gotRot.w);
                if (ed.Dimensions != live.DimensionsMM)
                    drifts.Add($"{ed.name}: dimensionsMM {ed.Dimensions} -> {live.DimensionsMM}");
                continue;
            }

            CompareField(drifts, ed.name, "position.x", wantPos.x, gotPos.x);
            CompareField(drifts, ed.name, "position.y", wantPos.y, gotPos.y);
            CompareField(drifts, ed.name, "position.z", wantPos.z, gotPos.z);

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

    [Test]
    public void ComparePositionWithinTolerance_WithinBudget_RecordsNoDrift()
    {
        var drifts = new List<string>();
        ComparePositionWithinTolerance(drifts, "Truba", Vector3.zero,
            new Vector3(0.0007f, 0f, 0f), MaxExemptPositionDriftMm * AppConstants.MM_TO_UNITS);
        Assert.IsEmpty(drifts, "0,7 мм — внутри допуска 0,8 мм, дрейф не должен попасть в отчёт");
    }

    /// <summary>review-perf-tests-tooling.md #5: до правки исключение пропускало ЛЮБОЙ
    /// сдвиг позиции у Truba/Otvod_91/Obratka без предела — регресс, двигающий деталь на
    /// полметра, прошёл бы тем же путём, что честный дрейф стыка в доли миллиметра.</summary>
    [Test]
    public void ComparePositionWithinTolerance_BeyondBudget_RecordsADrift()
    {
        var drifts = new List<string>();
        ComparePositionWithinTolerance(drifts, "Truba", Vector3.zero,
            new Vector3(0.5f, 0f, 0f), MaxExemptPositionDriftMm * AppConstants.MM_TO_UNITS);
        Assert.IsNotEmpty(drifts, "500 мм — далеко за допуском 0,8 мм, регресс обязан попасть в отчёт");
    }

    /// <summary>review-perf-tests-tooling.md #5: rotation у исключённых имён по-прежнему
    /// сравнивается ТОЧНО — допуск даётся только позиции. До правки exempt-ветка делала
    /// continue сразу и не доходила ни до одной из четырёх компонент кватерниона.</summary>
    [Test]
    public void ExemptedName_StillRejectsARotationDrift()
    {
        var drifts = new List<string>();
        var want = Quaternion.identity;
        var got = Quaternion.Euler(0f, 90f, 0f);
        CompareField(drifts, "Truba", "rotation.x", want.x, got.x);
        CompareField(drifts, "Truba", "rotation.y", want.y, got.y);
        CompareField(drifts, "Truba", "rotation.z", want.z, got.z);
        CompareField(drifts, "Truba", "rotation.w", want.w, got.w);
        Assert.IsNotEmpty(drifts, "поворот на 90° у исключённого имени обязан остаться дрейфом");
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
