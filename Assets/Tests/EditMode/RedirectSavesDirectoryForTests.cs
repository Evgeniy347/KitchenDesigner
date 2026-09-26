using System;
using System.IO;
using KitchenDesigner.Core;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>Правка на review-perf-tests-tooling.md #1: PlayMode-фикстуры удаляли
/// настоящий persistentDataPath/saves/autosave.json пользователя (общий с установленным
/// приложением), а часть EditMode-тестов писала туда же пробные проекты и zip-бэкапы.
/// Один шов вместо восемнадцати правок по месту: <see cref="SavesDirectoryOverride"/>
/// подменяет <c>ProjectFileStore.SavesDirectory</c> на временный каталог на весь прогон,
/// и ни одному тесту не нужно знать об этом. Проводка — тот же платформенный крючок
/// TestRunnerApi.RegisterCallbacks, что и у SceneLeakGuardWiring (см. его комментарий
/// про то, почему НЕ сборочный [assembly: ITestAction]): он один на прогон, срабатывает
/// и для EditMode, и для PlayMode, и не существует в собранном плеере вовсе — эта правка
/// живёт в Editor-only тестовой сборке и не может тронуть настоящего пользователя.</summary>
[InitializeOnLoad]
internal static class RedirectSavesDirectoryForTestsWiring
{
    static RedirectSavesDirectoryForTestsWiring()
    {
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new Callbacks());
    }

    private sealed class Callbacks : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun)
        {
            var dir = Path.Combine(Path.GetTempPath(), "kd-test-saves-" + Guid.NewGuid().ToString("N"));
            SavesDirectoryOverride.Current = dir;
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            var dir = SavesDirectoryOverride.Current;
            SavesDirectoryOverride.Current = null;
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
            try { Directory.Delete(dir, recursive: true); } catch (Exception) { }
        }

        public void TestStarted(ITestAdaptor test)
        {
        }

        public void TestFinished(ITestResultAdaptor result)
        {
        }
    }
}
