using System;
using NUnit.Framework;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Audio;

/// <summary>
/// Выбранный трек и громкость едут в файле проекта: пользователь открывает свою
/// кухню и слышит ту же музыку, на которой её закрыл. Поля формата проверяются
/// ЧЕРЕЗ ФАЙЛ (снять сцену → JSON → прочитать → восстановить), а не сравнением
/// объекта с самим собой: поле, забытое в SceneCapture или в SceneRestorer,
/// теряется молча — ровно так же, как терялись настройки до появления
/// KitchenSettingsContractTests.
///
/// Дефолт трека и громкости здесь тоже предмет проверки: проект, сохранённый до
/// появления плеера, обязан открываться на первом треке с нормальной
/// громкостью, а не в тишине с нулевым ползунком (JsonUtility приносит
/// отсутствующее число как 0 — защищает только инициализатор поля).
/// </summary>
public class MusicPersistenceTests
{
    private ProjectLoadStateGuard? _globals;
    private int _savedTrack;
    private int _savedVolume;

    [SetUp]
    public void Setup()
    {
        _globals = ProjectLoadStateGuard.Capture();
        _savedTrack = MusicState.Track;
        _savedVolume = MusicState.VolumePct;
    }

    [TearDown]
    public void Teardown()
    {
        MusicState.Track = _savedTrack;
        MusicState.VolumePct = _savedVolume;
        _globals?.Restore();
    }

    private static ProjectData ThroughFile()
    {
        var json = SaveLoadManager.Serialize(
            SaveLoadManager.CaptureScene(Array.Empty<KitchenElement>()));
        var data = SaveLoadManager.Deserialize(json);
        Assert.IsNotNull(data, "проект должен читаться обратно из JSON");
        return data!;
    }

    [Test]
    public void LastTrackAndVolume_SurviveSaveAndLoad()
    {
        MusicState.Track = 3;
        MusicState.VolumePct = 25;

        var data = ThroughFile();
        MusicState.Track = 0;
        MusicState.VolumePct = MusicState.DEFAULT_VOLUME_PCT;
        SaveLoadManager.RestoreScene(data);

        Assert.AreEqual(3, MusicState.Track,
            "последний трек хранится в проекте: без записи в SceneCapture или чтения в "
            + "SceneRestorer он молча вернулся бы к первому");
        Assert.AreEqual(25, MusicState.VolumePct,
            "и громкость тоже — иначе открытый проект каждый раз орал бы на шестидесяти процентах");
    }

    [Test]
    public void ProjectSavedBeforeThePlayerExisted_OpensOnTheFirstTrack_NotInSilence()
    {
        var old = SaveLoadManager.Deserialize("{\"version\":" + AppConstants.SAVE_FORMAT_VERSION + "}");

        Assert.IsNotNull(old, "старый проект обязан читаться");
        Assert.AreEqual(0, old!.musicTrack, "трека в файле нет — берётся первый");
        Assert.AreEqual(MusicState.DEFAULT_VOLUME_PCT, old.musicVolumePct,
            "громкости в файле нет — берётся дефолт плеера, а не ноль от JsonUtility");
    }
}
