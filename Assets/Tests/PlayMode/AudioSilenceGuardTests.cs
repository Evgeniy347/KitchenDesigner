using System.Collections;
using System.Linq;
using KitchenDesigner.Core.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Страж тишины: в прогоне не остаётся НИ ОДНОГО незаглушённого источника звука.
/// Повод — прогон тестов играл музыку вслух на машине человека: плеер стартует
/// сам (<c>MusicPlayer.Start</c>), и любая сцена, поднятая тестом, заводила его
/// вместе с остальным приложением.
///
/// Судится именно ВЫВОД, а не поведение. Логика плеера обязана остаться прежней,
/// поэтому каждый тест здесь сначала требует, чтобы плеер ИГРАЛ (трек загружен,
/// круг крутится, громкость приезжает из проекта), и только потом — чтобы это
/// нигде не было слышно. Тишина, добытая выключением плеера, валит первую
/// половину: <c>MusicPlaylistTests</c> и <c>MusicPersistenceTests</c> проверяют то
/// же самое, что и раньше.
///
/// Сенсор, который никого не видит, зелен всегда, поэтому у стража есть парный
/// тест: ему подсаживают заведомо незаглушённый источник и требуют, чтобы он его
/// НАЗВАЛ. Эти два теста держат друг друга — вместе они не могут быть зелёными по
/// недоразумению.
///
/// Механизм тишины стоит в `Core/Audio/MusicOutput`, а «мы под прогоном» решает
/// `AudioOutputPolicy`; что выход ровно один, сторожит `AudioOutputSingleDoorTests`
/// в быстром dotnet-прогоне — этот файл проверяет тот же контур по факту, в живой
/// сцене.
/// </summary>
public class AudioSilenceGuardTests
{
    private GameObject? _host;
    private int _savedTrack;
    private int _savedVolume;

    [SetUp]
    public void SetUp()
    {
        _savedTrack = MusicState.Track;
        _savedVolume = MusicState.VolumePct;
        _host = new GameObject("MusicPlayerHost");
        _host.AddComponent<MusicPlayer>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_host != null) Object.DestroyImmediate(_host);
        MusicState.Track = _savedTrack;
        MusicState.VolumePct = _savedVolume;
    }

    private static AudioSource[] AllSources() =>
        Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);

    private static string Unmuted() => string.Join(", ",
        AllSources().Where(s => !s.mute).Select(s => s.gameObject.name));

    private static void AssertSilent(string what)
    {
        CollectionAssert.IsNotEmpty(AllSources(),
            "сенсор обязан видеть хоть один источник звука: пустая сцена делает стража "
            + "зелёным, ничего не проверив");
        Assert.AreEqual("", Unmuted(), what);
    }

    [UnityTest]
    public IEnumerator TheAutoStart_LoadsTheTrack_AndStaysInaudible()
    {
        yield return null;

        var player = MusicPlayer.Instance!;
        Assert.IsTrue(player.IsPlaying,
            "автостарт не отменён: тесты глушат ВЫВОД, а не поведение — иначе "
            + "«музыка играет с первого кадра» перестало бы проверяться вовсе");
        Assert.IsNotNull(_host!.GetComponent<AudioSource>().clip, "и трек по-прежнему загружен");

        AssertSilent("прогон поднимает сцену без человека рядом: звук обязан молчать. "
            + "Незаглушено: " + Unmuted());
    }

    [UnityTest]
    public IEnumerator RestoringAProject_BringsBackTrackAndVolume_WithoutASound()
    {
        yield return null;

        MusicState.VolumePct = 90;
        MusicState.Track = MusicPlaylist.TRACK_COUNT - 1;
        yield return null;

        Assert.AreEqual(MusicPlaylist.TRACK_COUNT - 1, MusicState.Track,
            "трек из проекта доехал: SceneRestorer кладёт его в MusicState, и плеер "
            + "переключается сам");
        Assert.IsTrue(MusicPlayer.Instance!.IsPlaying, "и продолжает играть");

        AssertSilent("открытие проекта в тесте не имеет права зазвучать — ни первым треком, "
            + "ни тем, что записан в файле. Незаглушено: " + Unmuted());
    }

    [UnityTest]
    public IEnumerator SkippingToTheNextTrack_KeepsTheCircleTurning_AndTheRoomQuiet()
    {
        yield return null;

        MusicPlayer.Instance!.Skip(1);
        yield return null;

        Assert.AreEqual(MusicPlaylist.Wrap(_savedTrack + 1), MusicState.Track,
            "перемотка работает как прежде");

        AssertSilent("перемотка и автопереход к следующему треку заводят звук заново — "
            + "глушитель обязан пережить и это. Незаглушено: " + Unmuted());
    }

    [Test]
    public void TheGuard_NamesAnUnmutedSource_SoItCanActuallyGoRed()
    {
        var planted = new GameObject("PlantedSpeaker");
        planted.AddComponent<AudioSource>();

        try
        {
            StringAssert.Contains("PlantedSpeaker", Unmuted(),
                "сенсор обязан находить незаглушённый источник: страж, который этого не "
                + "умеет, зелен при любом состоянии сцены");
        }
        finally
        {
            Object.DestroyImmediate(planted);
        }

        Assert.AreEqual("", Unmuted(),
            "а без подсадки — снова тишина, иначе первый тест ловил бы чужой мусор");
    }
}
