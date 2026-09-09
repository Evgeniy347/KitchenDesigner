using KitchenDesigner.Core.UI;
using NUnit.Framework;
using UnityEngine;

public class SidebarPresetPreferenceTests
{
    private const string Key = "KitchenSidebarPreset_Ящик";
    private bool _hadPrevValue;
    private string _prevValue = "";

    [SetUp]
    public void SetUp()
    {
        _hadPrevValue = PlayerPrefs.HasKey(Key);
        _prevValue = PlayerPrefs.GetString(Key, "");
    }

    [TearDown]
    public void TearDown()
    {
        if (_hadPrevValue) PlayerPrefs.SetString(Key, _prevValue);
        else PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
    }

    [Test]
    public void Load_WithNoStoredValue_IsNull()
    {
        PlayerPrefs.DeleteKey(Key);

        Assert.IsNull(SidebarPresetPreference.Load("Ящик"),
            "плитку, которую ещё никогда не открывали, не выдают за что-то уже выбранное");
    }

    [Test]
    public void Save_ThenLoad_ReturnsTheSamePresetName()
    {
        SidebarPresetPreference.Save("Ящик", "Ящик Movento");

        Assert.AreEqual("Ящик Movento", SidebarPresetPreference.Load("Ящик"),
            "последний выбранный пресет плитки обязан пережить обращение к PlayerPrefs, как "
            + "переживает перезапуск программы");
    }

    [Test]
    public void DifferentTiles_KeepIndependentLastUsedPresets()
    {
        SidebarPresetPreference.Save("Ящик", "Ящик Movento");
        SidebarPresetPreference.Save("Фитинг", "Тройник");

        Assert.AreEqual("Ящик Movento", SidebarPresetPreference.Load("Ящик"));
        Assert.AreEqual("Тройник", SidebarPresetPreference.Load("Фитинг"),
            "у каждой плитки — свой ключ: выбор пресета одной не должен перезаписывать выбор другой");

        PlayerPrefs.DeleteKey("KitchenSidebarPreset_Фитинг");
    }
}
