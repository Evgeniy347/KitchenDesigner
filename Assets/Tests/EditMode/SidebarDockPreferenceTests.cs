using KitchenDesigner.Core.UI;
using NUnit.Framework;
using UnityEngine;

/// <summary>Выбор режима каталога (раскрытый док / рейка иконок) — настройка
/// пользователя, а не проекта: она живёт в PlayerPrefs и обязана пережить
/// перезапуск программы. Прежнее значение ключа сохраняется и восстанавливается,
/// как и для остальных PlayerPrefs-настроек в этом наборе (см. DemoModeSaveTests) —
/// иначе прогон тестов молча стирает реальный выбор разработчика на этой машине.</summary>
public class SidebarDockPreferenceTests
{
    private const string Key = "KitchenSidebarDockChoice";
    private bool _hadPrevValue;
    private int _prevValue;

    [SetUp]
    public void SetUp()
    {
        _hadPrevValue = PlayerPrefs.HasKey(Key);
        _prevValue = PlayerPrefs.GetInt(Key, 0);
    }

    [TearDown]
    public void TearDown()
    {
        if (_hadPrevValue) PlayerPrefs.SetInt(Key, _prevValue);
        else PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
    }

    [Test]
    public void Load_WithNoStoredValue_IsUnset()
    {
        PlayerPrefs.DeleteKey(Key);

        Assert.AreEqual(SidebarDockChoice.Unset, SidebarDockPreference.Load(),
            "программа, которую ещё никогда не запускали, не должна вести себя так, будто "
            + "пользователь уже что-то выбрал");
    }

    [Test]
    public void Save_Docked_ThenLoad_ReturnsDocked()
    {
        SidebarDockPreference.Save(SidebarDockChoice.Docked);

        Assert.AreEqual(SidebarDockChoice.Docked, SidebarDockPreference.Load(),
            "выбор «раскрытый док» обязан пережить обращение к PlayerPrefs, как переживает "
            + "перезапуск программы");
    }

    [Test]
    public void Save_Rail_ThenLoad_ReturnsRail()
    {
        SidebarDockPreference.Save(SidebarDockChoice.Rail);

        Assert.AreEqual(SidebarDockChoice.Rail, SidebarDockPreference.Load(),
            "выбор «рейка иконок» обязан пережить обращение к PlayerPrefs так же честно, "
            + "как и противоположный выбор — оба входа проверены, не только один");
    }
}
