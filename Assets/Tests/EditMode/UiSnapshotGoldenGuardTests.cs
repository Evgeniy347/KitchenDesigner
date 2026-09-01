using System.IO;
using System.Text.RegularExpressions;
using KitchenDesigner.Tests;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>Сторож на самого сторожа: UI-снапшот без эталона обязан ронять
/// тест, а не шептать.
///
/// UiSnapshotEngine.MatchGolden при отсутствии verified-файла писал
/// Debug.LogWarning и выходил — тест оставался ЗЕЛЁНЫМ. Новый снапшот
/// не был закреплён ничем и молчал об этом: ui_iso_sofa_* и
/// ui_contextmenu_sofa прошли первый же прогон без единого эталона, а
/// семь кандидатов висели непринятыми, и никто не жаловался. Соседняя
/// ветка того же метода (эталон есть, содержимое разошлось) с самого
/// начала звала Debug.LogError, и именно на этом держится её падение:
/// незапрошенный LogType.Error роняет тест силами Unity Test Framework —
/// поэтому лечение сводится к тому, чтобы обе ветки были одинаково
/// громкими, без затаскивания NUnit в Assets/Scripts.
///
/// Тест не может протухнуть: он не трогает НИ ОДИН настоящий эталон.
/// Имя пробы своё, файлов с ним в репозитории нет, а всё, что проба
/// создаёт, удаляется и в [SetUp], и в [TearDown] — то есть на любом
/// пути, включая падение с исключением. Если кто-нибудь всё же заведёт
/// эталон с таким именем, первый тест не позеленеет молча: ожидаемая
/// ошибка не придёт, и LogAssert уронит его сам.</summary>
public class UiSnapshotGoldenGuardTests
{
    private const string ProbeName = "uisnapshot_golden_guard_probe";

    private static string GoldenDir =>
        Path.Combine(Application.dataPath, "Tests", "EditMode", "Snapshots");

    private static string VerifiedPath =>
        Path.Combine(GoldenDir, "ui_" + ProbeName + ".verified.json");

    private static string CandidatePath =>
        Path.Combine(GoldenDir, "ui_" + ProbeName + ".candidate.json");

    private static string OutputPath =>
        Path.Combine(Application.temporaryCachePath, ProbeName + ".json");

    private static readonly Regex NoGolden = new Regex("No verified golden");

    private GameObject _root = null!;

    [SetUp]
    public void SetUp()
    {
        RemoveProbeFiles();
        _root = new GameObject("UiSnapshotGoldenGuardRoot");
    }

    [TearDown]
    public void TearDown()
    {
        if (_root != null) Object.DestroyImmediate(_root);
        RemoveProbeFiles();
    }

    private static void RemoveProbeFiles()
    {
        foreach (var path in new[] { VerifiedPath, CandidatePath, OutputPath })
            if (File.Exists(path)) File.Delete(path);
    }

    [Test]
    public void CaptureVerified_WithNoGoldenFile_LogsAnErrorSoTheTestCannotPassUnpinned()
    {
        Assert.IsFalse(File.Exists(VerifiedPath),
            "проба обязана стартовать БЕЗ эталона — иначе проверяется не та ветка: "
            + VerifiedPath);

        LogAssert.Expect(LogType.Error, NoGolden);
        UiSnapshotEngine.CaptureVerified(_root, OutputPath);

        Assert.IsTrue(File.Exists(CandidatePath),
            "кандидат обязан лечь рядом: без него принимать нечего, а сообщение "
            + "об ошибке предлагает переименовать несуществующий файл");
    }

    [Test]
    public void CaptureVerified_AfterTheCandidateIsAccepted_StaysSilent()
    {
        LogAssert.Expect(LogType.Error, NoGolden);
        UiSnapshotEngine.CaptureVerified(_root, OutputPath);

        File.Move(CandidatePath, VerifiedPath);

        UiSnapshotEngine.CaptureVerified(_root, OutputPath);

        Assert.IsFalse(File.Exists(CandidatePath),
            "совпавший снимок обязан подчистить кандидата — иначе принятый эталон "
            + "вечно тянет за собой файл, который выглядит непринятым");
    }
}
