using System.IO;
using KitchenDesigner.Core;
using KitchenDesigner.Tests;
using NUnit.Framework;
using UnityEngine;

/// <summary>Байты, которые записыватель снапшотов кладёт на диск.
///
/// Приёмка эталона — это момент, когда человек глазами отличает ожидаемый
/// сдвиг от регрессии, и шум в дифф стоит здесь дороже всего. Записыватель
/// звал <c>File.WriteAllText(..., Encoding.UTF8)</c>, а у этой кодировки есть
/// преамбула: каждый принятый кандидат приносил BOM в первую строку. Вторую
/// строку добавлял <c>TrimEnd</c> в нормализации — концевой перевод строки
/// пропадал. Итог одной приёмки: 27 файлов, 131 вставка при 52 «удалениях»,
/// из которых по делу были только новые ключи, и дважды пришлось руками
/// перепроверять, чисто ли изменение.
///
/// Маятника здесь нет: и <c>Snapshot.Match</c>, и <c>UiSnapshotEngine</c>
/// сравнивают НОРМАЛИЗОВАННЫЙ текст, а <c>File.ReadAllText</c> съедает BOM
/// сам. Последний тест закрепляет именно это — принятый в новом виде эталон
/// совпадает на следующем же прогоне.</summary>
public class SnapshotFileFormatTests
{
    private const string ProbeName = "snapshot_file_format_probe";
    private const string ProbeJson = "{\n  \"probe\": 1\n}";

    private static string CandidatePath =>
        Path.Combine(Snapshot.SnapshotDir, ProbeName + ".candidate.json");

    private static string VerifiedPath =>
        Path.Combine(Snapshot.SnapshotDir, ProbeName + ".verified.json");

    private static string CapturePath =>
        Path.Combine(Application.temporaryCachePath, ProbeName + ".json");

    private GameObject _root = null!;

    [SetUp]
    public void SetUp()
    {
        RemoveProbeFiles();
        _root = new GameObject("SnapshotFileFormatRoot");
    }

    [TearDown]
    public void TearDown()
    {
        if (_root != null) Object.DestroyImmediate(_root);
        RemoveProbeFiles();
    }

    private static void RemoveProbeFiles()
    {
        foreach (var path in new[] { CandidatePath, VerifiedPath, CapturePath })
            if (File.Exists(path)) File.Delete(path);
    }

    private static void AssertBaselineShape(string path, string what)
    {
        var bytes = File.ReadAllBytes(path);

        Assert.Greater(bytes.Length, 1,
            what + ": файла нет или он пуст, значит проверки ниже зелены ни на "
            + "чём — " + path);

        bool bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        Assert.IsFalse(bom,
            what + ": в начале файла BOM. Эталоны лежат в репозитории без него, "
            + "поэтому каждая приёмка добавляла бы лишнюю изменённую строку ровно "
            + "там, где человек ищет настоящую регрессию. Писать через "
            + "SnapshotFile.Write (UTF8Encoding(false)), а не Encoding.UTF8 — "
            + path);

        Assert.AreEqual((byte)'\n', bytes[bytes.Length - 1],
            what + ": файл не кончается переводом строки — git показывает "
            + "последнюю строку изменённой при каждой приёмке. " + path);

        Assert.AreNotEqual((byte)'\n', bytes[bytes.Length - 2],
            what + ": в конце больше одного перевода строки — пустая строка "
            + "в хвосте так же шумит в диффе, как её отсутствие. " + path);
    }

    [Test]
    public void SnapshotMatch_WithNoBaseline_WritesTheCandidateWithoutBomAndWithATrailingNewline()
    {
        Assert.IsFalse(File.Exists(VerifiedPath),
            "проба обязана стартовать БЕЗ эталона — иначе Match пойдёт по ветке "
            + "сравнения и кандидата не напишет: " + VerifiedPath);

        Assert.Throws<AssertionException>(() => Snapshot.Match(ProbeJson, ProbeName),
            "снапшот без эталона обязан ронять тест — иначе кандидата не будет");

        AssertBaselineShape(CandidatePath, "кандидат Snapshot.Match");
    }

    [Test]
    public void UiSnapshotEngineCapture_WritesTheFileWithoutBomAndWithATrailingNewline()
    {
        UiSnapshotEngine.Capture(_root, CapturePath);

        AssertBaselineShape(CapturePath, "снимок UiSnapshotEngine.Capture");
    }

    [Test]
    public void SnapshotMatch_AgainstACandidateAcceptedAsIs_PassesSoTheEncodingChangeIsNotAPendulum()
    {
        Assert.Throws<AssertionException>(() => Snapshot.Match(ProbeJson, ProbeName));
        File.Move(CandidatePath, VerifiedPath);

        Assert.DoesNotThrow(() => Snapshot.Match(ProbeJson, ProbeName),
            "эталон, принятый переименованием кандидата, обязан совпасть на "
            + "следующем прогоне. Иначе смена кодировки даёт вечный маятник: "
            + "каждый прогон краснеет и предлагает принять то же самое");

        Assert.IsFalse(File.Exists(CandidatePath),
            "совпавший снимок обязан подчистить кандидата");
    }
}
