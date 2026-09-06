using NUnit.Framework;
using KitchenDesigner.Core.Audio;

/// <summary>
/// Плейлист крутится ПО КРУГУ: «далее» с последнего трека обязано приводить на
/// первый, «назад» с первого — на последний. Наивное <c>index + 1</c> здесь даёт
/// молчаливый дефект — не исключение, а тишину: клип с несуществующим номером
/// не грузится, кнопка «далее» перестаёт работать, и выглядит это как «плеер
/// завис», а не как выход за границу списка.
///
/// Остаток от отрицательного числа в C# отрицателен (-1 % 5 == -1), поэтому
/// «назад» проверяется отдельно от «далее»: обёртка, написанная одним `%`,
/// проходит вперёд и ломается назад.
/// </summary>
public class MusicPlaylistTests
{
    [Test]
    public void Next_FromTheLastTrack_WrapsToTheFirst()
    {
        Assert.AreEqual(0, MusicPlaylist.Wrap(MusicPlaylist.TRACK_COUNT),
            "за последним треком идёт первый, а не пустота");
    }

    [Test]
    public void Previous_FromTheFirstTrack_WrapsToTheLast()
    {
        Assert.AreEqual(MusicPlaylist.TRACK_COUNT - 1, MusicPlaylist.Wrap(-1),
            "«назад» с первого трека ведёт на последний: остаток отрицательного числа в C# "
            + "отрицателен, и обёртка одним % дала бы несуществующий номер");
        Assert.AreEqual(MusicPlaylist.TRACK_COUNT - 2, MusicPlaylist.Wrap(-2),
            "и на два шага назад тоже");
    }

    [Test]
    public void ResourcePath_MatchesTheFilesShippedInResources()
    {
        Assert.AreEqual("Music/track-01", MusicPlaylist.ResourcePath(0),
            "номер в имени файла двузначный: Resources.Load ищет по точному пути, и track-1 "
            + "не нашёлся бы");
        Assert.AreEqual("Music/track-05", MusicPlaylist.ResourcePath(4));
        Assert.AreEqual("Music/track-01", MusicPlaylist.ResourcePath(MusicPlaylist.TRACK_COUNT),
            "путь считается от обёрнутого номера, иначе круг ломался бы на загрузке");
    }

    [Test]
    public void DisplayName_CountsTracksFromOne_NotFromZero()
    {
        Assert.AreEqual("Трек 1", MusicPlaylist.DisplayName(0),
            "человеку показывается номер с единицы, индекс с нуля — внутреннее дело плейлиста");
        Assert.AreEqual("Трек 5", MusicPlaylist.DisplayName(4));
    }

    [Test]
    public void TrackCount_MatchesTheFilesLyingInResources()
    {
        var dir = KitchenDesigner.Tests.Geometry.RepoPaths.Subdir("Assets", "Resources", "Music");
        var files = System.IO.Directory.GetFiles(dir, "*.mp3");

        Assert.AreEqual(MusicPlaylist.TRACK_COUNT, files.Length,
            "TRACK_COUNT и папка с треками — две записи об одном и том же, и расходятся они "
            + "молча: лишний номер в константе делает «далее» тишиной (Resources.Load вернёт "
            + "null), лишний файл в папке просто никогда не зазвучит. Файлов в " + dir + ": "
            + files.Length);

        for (int i = 0; i < MusicPlaylist.TRACK_COUNT; i++)
        {
            var path = System.IO.Path.Combine(dir, "track-" + (i + 1).ToString("00") + ".mp3");
            Assert.IsTrue(System.IO.File.Exists(path),
                "нумерация треков сплошная: " + MusicPlaylist.ResourcePath(i) + " обязан лежать "
                + "на диске, иначе дыра в середине круга останется незамеченной до нажатия");
        }
    }

    [Test]
    public void Volume_IsClampedToThePercentScale()
    {
        int saved = MusicState.VolumePct;
        try
        {
            MusicState.VolumePct = 140;
            Assert.AreEqual(100, MusicState.VolumePct, "громче ста процентов ползунок не уезжает");
            MusicState.VolumePct = -20;
            Assert.AreEqual(0, MusicState.VolumePct, "и тише нуля тоже");
            Assert.AreEqual(0f, MusicState.Volume, 0.001f,
                "AudioSource.volume — доля единицы, а не проценты");
        }
        finally { MusicState.VolumePct = saved; }
    }

    [Test]
    public void Track_KeepsTurningInACircle_WhenSetPastTheEnd()
    {
        int saved = MusicState.Track;
        try
        {
            MusicState.Track = MusicPlaylist.TRACK_COUNT - 1;
            MusicState.Track += 1;
            Assert.AreEqual(0, MusicState.Track, "«далее» с последнего трека возвращает на первый");
            MusicState.Track -= 1;
            Assert.AreEqual(MusicPlaylist.TRACK_COUNT - 1, MusicState.Track,
                "«назад» с первого — на последний");
        }
        finally { MusicState.Track = saved; }
    }
}
