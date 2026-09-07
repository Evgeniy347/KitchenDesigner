using KitchenDesigner.Core.Audio;
using NUnit.Framework.Interfaces;
using UnityEngine.TestRunner;

[assembly: TestRunCallback(typeof(SilenceAudioWhenTheRunStarts))]

/// <summary>
/// Дверь «мы под прогоном» открывается ЗДЕСЬ, один раз на весь прогон, и ни один
/// тест про неё не знает: <c>ITestRunCallback.RunStarted</c> — собственный хук
/// каркаса, он срабатывает до первого теста и в EditMode, и в PlayMode, и в
/// плеере. Список «тестов, которые не забыли выключить музыку» протух бы на
/// восьмом тесте; хук нельзя забыть, потому что его никто не зовёт руками.
///
/// Второй информант той же двери — <c>Application.isBatchMode</c> внутри
/// <see cref="AudioOutputPolicy"/>: шлюз поднимает Unity холодным batch, где
/// человека рядом нет по определению. Он держит тишину даже если каркас однажды
/// перестанет звать этот хук, и не трогает ни собранное приложение, ни Play в
/// редакторе — там batch не бывает.
/// </summary>
public class SilenceAudioWhenTheRunStarts : ITestRunCallback
{
    public void RunStarted(ITest testsToRun) => AudioOutputPolicy.SilenceForTestRun();

    public void RunFinished(ITestResult testResults) { }

    public void TestStarted(ITest test) { }

    public void TestFinished(ITestResult result) { }
}
