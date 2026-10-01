using KitchenDesigner.Core;
using KitchenDesigner.Core.Audio;
using NUnit.Framework.Interfaces;
using UnityEngine.TestRunner;

[assembly: TestRunCallback(typeof(TestRunStartAnnouncements))]

/// <summary>
/// Дверь «мы под прогоном» открывается ЗДЕСЬ, один раз на весь прогон, и ни один
/// тест про неё не знает: <c>ITestRunCallback.RunStarted</c> — собственный хук
/// каркаса, он срабатывает до первого теста и в EditMode, и в PlayMode, и в
/// плеере. Список «тестов, которые не забыли выключить музыку» протух бы на
/// восьмом тесте; хук нельзя забыть, потому что его никто не зовёт руками.
/// Атрибут <c>TestRunCallback</c> на сборку ставится ОДИН раз, поэтому все,
/// кому нужно знать о прогоне, слушают этот же класс.
///
/// Сейчас их двое. Звук молчит (<see cref="AudioOutputPolicy"/>). Язык закреплён
/// за русским исходником (<see cref="LanguageStartup"/>): эталоны UI сняты
/// по-русски, и прогон не должен перекрашиваться ни от выбора English в
/// настройках редактора, ни от английской Windows.
///
/// Второй информант той же двери — <c>Application.isBatchMode</c> внутри обоих
/// адаптеров: шлюз поднимает Unity холодным batch, где человека рядом нет по
/// определению. Он держит обе настройки даже если каркас однажды перестанет звать
/// этот хук, и не трогает ни собранное приложение, ни Play в редакторе — там batch
/// не бывает.
/// </summary>
public class TestRunStartAnnouncements : ITestRunCallback
{
    public void RunStarted(ITest testsToRun)
    {
        AudioOutputPolicy.SilenceForTestRun();
        LanguageStartup.PinSourceLanguageForTestRun();
    }

    public void RunFinished(ITestResult testResults) { }

    public void TestStarted(ITest test) { }

    public void TestFinished(ITestResult result) { }
}
