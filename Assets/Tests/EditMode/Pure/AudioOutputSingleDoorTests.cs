using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using KitchenDesigner.Tests.Geometry;
using NUnit.Framework;

/// <summary>
/// У звука ОДИН выход — `MusicOutput`, и ровно на нём стоит глушитель
/// (`AudioOutputPolicy.Silent`). Пока выход один, тишина в прогоне держится по
/// построению: любой `AudioSource`, созданный мимо него, зазвучит на колонках
/// машины, где тесты идут в холодном batch без единого человека рядом.
///
/// Сторожить список тестов, которые «не забыли выключить музыку», бессмысленно —
/// он протухает на восьмом тесте. Сторожится МЕХАНИЗМ: в продакшен-коде слово
/// AudioSource встречается в одном файле, и каждый метод этого файла, который
/// запускает звук (`Play`, `UnPause`), в том же методе применяет политику. Второй
/// источник звука валит R1, ещё не будучи никем включён; забытая политика в новом
/// методе выхода валит R2.
///
/// Соседний страж, `AudioSilenceGuardTests` (PlayMode), проверяет то же самое по
/// факту: в поднятой сцене не остаётся ни одного незаглушённого источника. Он
/// требует Unity; этот — не требует и идёт в общем dotnet-прогоне за миллисекунды.
///
/// Скан по пути, который перестал резолвиться, зеленеет, ничего не проверив,
/// поэтому у сканера есть собственные тесты: ему подсовывают синтетический
/// исходник со вторым выходом и требуют, чтобы он его НАШЁЛ.
/// </summary>
public class AudioOutputSingleDoorTests
{
    private const string TheDoor = "MusicOutput.cs";

    private static readonly Regex MethodHeader =
        new Regex(@"^\s*(public|internal|protected|private)[^=]*?\b(\w+)\s*\(");

    private static readonly Regex MentionsAudioSource = new Regex(@"\bAudioSource\b");

    private static readonly Regex StartsSound = new Regex(@"\.\s*(Play|UnPause)\s*\(");

    private static readonly Regex AppliesThePolicy =
        new Regex(@"\.\s*mute\s*=\s*AudioOutputPolicy\s*\.\s*Silent");

    internal static Dictionary<string, List<string>> ByMethod(IEnumerable<string> lines)
    {
        var bodies = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        string method = "<вне метода>";

        foreach (var line in lines)
        {
            var header = MethodHeader.Match(line);
            if (header.Success) method = header.Groups[2].Value;
            if (!bodies.TryGetValue(method, out var body)) bodies[method] = body = new List<string>();
            body.Add(line);
        }

        return bodies;
    }

    internal static string[] MethodsThatStartSoundWithoutThePolicy(IEnumerable<string> lines) =>
        ByMethod(lines)
            .Where(m => m.Value.Any(StartsSound.IsMatch))
            .Where(m => !m.Value.Any(AppliesThePolicy.IsMatch))
            .Select(m => m.Key)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

    private static string[] ProductionSources() =>
        Directory.GetFiles(RepoPaths.Subdir("Assets", "Scripts"), "*.cs", SearchOption.AllDirectories);

    private static string[] OutputSource() => File.ReadAllLines(Path.Combine(
        RepoPaths.Subdir("Assets", "Scripts", "Core", "Audio"), "MusicOutput.cs"));

    [Test]
    public void R1_TheWholeAppTouchesAudioSource_InExactlyOneFile()
    {
        var owners = ProductionSources()
            .Where(f => File.ReadLines(f).Any(MentionsAudioSource.IsMatch))
            .Select(Path.GetFileName)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(new[] { TheDoor }, owners,
            "второй файл, заводящий AudioSource, — это второй выход звука, и глушитель на "
            + "нём никто не поставит: он зазвучит вслух в прогоне, где рядом никого нет. "
            + "Найдено: " + string.Join(", ", owners));
    }

    [Test]
    public void R2_EveryMethodThatStartsSound_AppliesTheSilencePolicy()
    {
        var missing = MethodsThatStartSoundWithoutThePolicy(OutputSource());

        CollectionAssert.IsEmpty(missing,
            "метод выхода, который зовёт Play или UnPause, обязан в том же месте применить "
            + "AudioOutputPolicy.Silent. Иначе тишина держится на том, что источник создали "
            + "заглушённым, а любое позднее переключение трека вернёт звук. Без политики: "
            + string.Join(", ", missing));
    }

    [Test]
    public void R3_TheSilencePolicy_IsDecidedInOnePlace()
    {
        var deciders = ProductionSources()
            .Where(f => File.ReadLines(f).Any(l => l.Contains("Application.isBatchMode", StringComparison.Ordinal)))
            .Select(Path.GetFileName)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(new[] { "AudioOutputPolicy.cs" }, deciders,
            "«мы под прогоном» — одно понятие и одно место, где оно решается. Второй такой "
            + "вопрос в другом файле разойдётся с первым молча. Найдено: "
            + string.Join(", ", deciders));
    }

    [Test]
    public void TheScan_FindsASecondOutputThatWasDeliberatelyPlanted()
    {
        var planted = new[]
        {
            "        public void Chime()",
            "        {",
            "            _click.Play();",
            "        }",
        };

        var missing = MethodsThatStartSoundWithoutThePolicy(planted);

        CollectionAssert.AreEqual(new[] { "Chime" }, missing,
            "сканер обязан видеть подсунутый запуск звука без политики — иначе R2 зелен всегда");
    }

    [Test]
    public void TheScan_DoesNotAccuseAMethodThatAppliesThePolicy()
    {
        var legitimate = new[]
        {
            "        public void Start()",
            "        {",
            "            _source.mute = AudioOutputPolicy.Silent;",
            "            if (!_source.isPlaying) _source.Play();",
            "        }",
        };

        CollectionAssert.IsEmpty(MethodsThatStartSoundWithoutThePolicy(legitimate),
            "сканер, обвиняющий и правильный метод, заставил бы отключить R2 при первой правке");
    }

    [Test]
    public void TheScan_ActuallyReadsTheRealFiles()
    {
        Assert.Greater(ProductionSources().Length, 100,
            "исходники прочитаны: скан по несуществующему пути зеленеет, ничего не проверив");

        var output = OutputSource();
        Assert.Greater(output.Length, 20, "и сам файл выхода тоже");
        Assert.IsTrue(output.Any(StartsSound.IsMatch),
            "в MusicOutput СЕГОДНЯ есть запуск звука — пустой список означает сломанный "
            + "сканер, а не безупречный класс");
        Assert.IsTrue(output.Any(AppliesThePolicy.IsMatch),
            "и применение политики тоже есть");
    }
}
