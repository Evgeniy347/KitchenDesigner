using KitchenDesigner.Core;

/// <summary>Любой вызов SaveToPath/LoadFromPath/CreateEmptyProjectAt теперь проводит путь
/// через SaveLoadManagerInstance.AdoptCurrentPath, который пишет его в СПИСОК НЕДАВНИХ
/// ПРОЕКТОВ — тот же PlayerPrefs, что видит реальный пользователь в окне «Загрузить». Тест,
/// вызывающий любую из этих функций с временным путём, обязан снять и вернуть список, как уже
/// делает с SaveLoadManager.LastPath — иначе временный файл теста навсегда оседает в реальном
/// списке недавних проектов.</summary>
internal static class RecentProjectsTestBackup
{
    internal static string[] Capture() =>
        RecentProjectsMemory.For(System.Environment.GetCommandLineArgs()).Values;

    internal static void Restore(string[] backup) =>
        RecentProjectsMemory.For(System.Environment.GetCommandLineArgs()).Values = backup;
}
