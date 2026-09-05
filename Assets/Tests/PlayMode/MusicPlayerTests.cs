using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core.Audio;

/// <summary>Плеер поднимается в PlayMode, потому что проверяется ровно то, чего
/// EditMode не видит: что файл трека НАЙДЁН по пути плейлиста и принят движком
/// как AudioClip. Опечатка в имени папки или в номере («track-1» вместо
/// «track-01») ничего не ломает громко — Resources.Load просто вернёт null, и
/// кнопка «пуск» станет молчаливой пустышкой.
///
/// Второй предмет проверки — слушатель. AudioSource без AudioListener в сцене
/// играет «в никуда»: isPlaying истинно, а звука нет. Сцена проекта своего
/// слушателя не содержит, поэтому его заводит сам плеер.</summary>
public class MusicPlayerTests
{
    private GameObject? _host;
    private int _savedTrack;

    [SetUp]
    public void SetUp()
    {
        _savedTrack = MusicState.Track;
        _host = new GameObject("MusicPlayerHost");
        _host.AddComponent<MusicPlayer>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_host != null) Object.DestroyImmediate(_host);
        MusicState.Track = _savedTrack;
    }

    [UnityTest]
    public IEnumerator Playback_StartsByItself_WhenTheAppLaunches()
    {
        yield return null;

        Assert.IsTrue(MusicPlayer.Instance!.IsPlaying,
            "музыка играет с первого кадра, без нажатия «пуск»: плеер поднимается в Bootstrap "
            + "ДО открытия проекта, и старт живёт в Start, а не в Awake — иначе он взял бы "
            + "первый трек, а не тот, что записан в открываемом проекте");
        Assert.IsNotNull(_host!.GetComponent<AudioSource>().clip, "и трек для него загружен");
    }

    [UnityTest]
    public IEnumerator Play_LoadsTheTrackFromResources_AndStartsIt()
    {
        var player = MusicPlayer.Instance!;
        player.Play();
        yield return null;

        var source = _host!.GetComponent<AudioSource>();
        Assert.IsNotNull(source.clip,
            "трек не загрузился по пути " + MusicPlaylist.ResourcePath(MusicState.Track)
            + ": файлы лежат в Assets/Resources/Music и едут в сборку сами");
        Assert.IsTrue(player.IsPlaying, "после «пуск» плеер играет");
        Assert.IsNotNull(Object.FindAnyObjectByType<AudioListener>(),
            "без слушателя в сцене AudioSource играет беззвучно");
    }

    [UnityTest]
    public IEnumerator Next_FromTheLastTrack_ComesBackToTheFirst_AndKeepsPlaying()
    {
        var player = MusicPlayer.Instance!;
        MusicState.Track = MusicPlaylist.TRACK_COUNT - 1;
        player.Play();
        yield return null;

        player.Skip(1);
        yield return null;

        Assert.AreEqual(0, MusicState.Track, "плейлист крутится по кругу");
        Assert.IsTrue(player.IsPlaying, "и после перехода музыка не замолкает");
        Assert.IsNotNull(_host!.GetComponent<AudioSource>().clip, "первый трек тоже нашёлся");
    }

    [UnityTest]
    public IEnumerator Pause_StopsThePlayback_AndTheNextPressResumesIt()
    {
        var player = MusicPlayer.Instance!;
        player.Play();
        yield return null;

        player.TogglePlay();
        yield return null;
        Assert.IsFalse(player.IsPlaying, "пауза останавливает воспроизведение");

        player.TogglePlay();
        yield return null;
        Assert.IsTrue(player.IsPlaying,
            "а второе нажатие продолжает: Update не считает паузу концом трека и не "
            + "перематывает плейлист вперёд сам");
        Assert.AreEqual(_savedTrack, MusicState.Track, "и остаётся на том же треке");
    }
}
