using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using NUnit.Framework;
using UnityEngine;

/// <summary>Второй заход того же дефекта, что и в SmokeRunKeepsUserPrefsReproTests:
/// дымовой прогон на машине пользователя перетирал НАСТРОЙКИ САЙДБАРА, и пользователь
/// получал переехавшую панель — раскрытый док вместо рейки, чужую открытую группу,
/// чужой пресет плитки. Лечится тем же способом: под -ephemeralSession запись не
/// делается вовсе, а не подчищается после (прогон может упасть посередине).</summary>
public class SmokeRunKeepsSidebarPrefsTests
{
    private static readonly string[] SmokeRunArgs =
    {
        "KitchenDesigner.exe", "-mcpPort", "19881",
        MuteAudioArgument.Name, HideWindowArgument.Name, EphemeralSessionArgument.Name
    };

    private static readonly string[] OrdinaryRunArgs = { "KitchenDesigner.exe" };

    private const string PresetKey = SidebarPresetPreference.KeyPrefix + "Ящик";

    private static readonly string[] TextKeys =
        { SidebarLastGroupPreference.Key, PresetKey };

    private static readonly string[] AllKeys =
        { SidebarDockPreference.Key, SidebarLastGroupPreference.Key, PresetKey };

    private readonly string[] _previousText = new string[TextKeys.Length];
    private readonly bool[] _hadPreviousText = new bool[TextKeys.Length];
    private bool _hadPreviousDock;
    private int _previousDock;

    [SetUp]
    public void SaveUserPrefs()
    {
        _hadPreviousDock = PlayerPrefs.HasKey(SidebarDockPreference.Key);
        _previousDock = PlayerPrefs.GetInt(SidebarDockPreference.Key, 0);

        for (int i = 0; i < TextKeys.Length; i++)
        {
            _hadPreviousText[i] = PlayerPrefs.HasKey(TextKeys[i]);
            _previousText[i] = PlayerPrefs.GetString(TextKeys[i], "");
        }

        foreach (var key in AllKeys) PreferenceStore.For(SmokeRunArgs).Delete(key);
    }

    [TearDown]
    public void RestoreUserPrefs()
    {
        if (_hadPreviousDock) PlayerPrefs.SetInt(SidebarDockPreference.Key, _previousDock);
        else PlayerPrefs.DeleteKey(SidebarDockPreference.Key);

        for (int i = 0; i < TextKeys.Length; i++)
        {
            if (_hadPreviousText[i]) PlayerPrefs.SetString(TextKeys[i], _previousText[i]);
            else PlayerPrefs.DeleteKey(TextKeys[i]);
        }
        PlayerPrefs.Save();
    }

    [Test]
    public void SmokeRun_MovesTheSidebarToTheRail_UsersDockChoiceStaysInPlayerPrefs()
    {
        PlayerPrefs.SetInt(SidebarDockPreference.Key, (int)SidebarDockChoice.Docked);
        PlayerPrefs.Save();

        var smokeRun = PreferenceStore.For(SmokeRunArgs);
        smokeRun.SetInt(SidebarDockPreference.Key, (int)SidebarDockChoice.Rail);

        Assert.AreEqual((int)SidebarDockChoice.Rail,
            smokeRun.GetInt(SidebarDockPreference.Key, (int)SidebarDockChoice.Unset),
            "внутри самого прогона выбор всё-таки действует — иначе панель перестроится посреди проверки");
        Assert.AreEqual((int)SidebarDockChoice.Docked,
            PlayerPrefs.GetInt(SidebarDockPreference.Key, (int)SidebarDockChoice.Unset),
            "а у пользователя каталог остаётся там, где он его оставил");
    }

    [Test]
    public void OrdinaryRun_MovesTheSidebarToTheRail_ChoiceIsRemembered()
    {
        PlayerPrefs.DeleteKey(SidebarDockPreference.Key);
        PlayerPrefs.Save();

        SidebarDockPreference.Save(SidebarDockChoice.Rail);

        Assert.AreEqual((int)SidebarDockChoice.Rail,
            PlayerPrefs.GetInt(SidebarDockPreference.Key, (int)SidebarDockChoice.Unset),
            "противоположный вход: обычный запуск обязан по-прежнему запоминать выбор — "
            + "ради этого ключ и существует");
    }

    [Test]
    public void SmokeRun_OpensItsOwnGroupAndPreset_UsersChoicesStayInPlayerPrefs()
    {
        PlayerPrefs.SetString(SidebarLastGroupPreference.Key, "Мойки");
        PlayerPrefs.SetString(PresetKey, "Ящик GTV");
        PlayerPrefs.Save();

        var smokeRun = PreferenceStore.For(SmokeRunArgs);
        smokeRun.SetString(SidebarLastGroupPreference.Key, "Трубы");
        smokeRun.SetString(PresetKey, "Ящик Movento");

        Assert.AreEqual("Трубы", smokeRun.GetString(SidebarLastGroupPreference.Key, ""),
            "внутри прогона открытая группа помнится — иначе каталог схлопывается сам по себе");
        Assert.AreEqual("Мойки", PlayerPrefs.GetString(SidebarLastGroupPreference.Key, ""),
            "а у пользователя открытой остаётся его группа");
        Assert.AreEqual("Ящик GTV", PlayerPrefs.GetString(PresetKey, ""),
            "и его пресет плитки: прогон ставит свой только на время прогона");
    }

    [Test]
    public void OrdinaryRun_PicksAGroupAndAPreset_BothAreRemembered()
    {
        PlayerPrefs.DeleteKey(SidebarLastGroupPreference.Key);
        PlayerPrefs.DeleteKey(PresetKey);
        PlayerPrefs.Save();

        SidebarLastGroupPreference.Save("Трубы");
        SidebarPresetPreference.Save("Ящик", "Ящик Movento");

        Assert.AreEqual("Трубы", PlayerPrefs.GetString(SidebarLastGroupPreference.Key, ""),
            "противоположный вход: без аргумента прогона открытая группа обязана писаться");
        Assert.AreEqual("Ящик Movento", PlayerPrefs.GetString(PresetKey, ""),
            "и выбранный пресет плитки тоже");
    }

    [Test]
    public void ThePreferenceStore_TellsTheTwoRunsApart()
    {
        Assert.AreNotSame(PreferenceStore.For(SmokeRunArgs), PreferenceStore.For(OrdinaryRunArgs),
            "если бы решение не зависело от аргументов, все проверки выше были бы зелены "
            + "на одном и том же хранилище и не проверяли бы ничего");
    }
}
