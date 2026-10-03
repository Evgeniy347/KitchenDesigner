using System;
using System.Linq;
using System.Text;
using KitchenDesigner.Core;
using NUnit.Framework;
using UnityEngine;

// Перенос DefaultCompany → Evgeniy347 копирует значения в ключ, который движок УЖЕ открыл и
// прочёл при старте (размер окна). Пользователь видит свои настройки в первом же сеансе только
// потому, что PlayerPrefs на Windows не кэширует реестр: каждое Get идёт в ключ, каждое Set
// пишется сразу, без Save. Замер 2026-10-03 (Unity 6000.4.3f1): значение, записанное мимо
// PlayerPrefs, видно сразу — и перезаписанное, и новое, и строка в UTF-8. Если Unity начнёт
// кэшировать, этот тест покраснеет, и перенос придётся делать до запуска движка (в установщике).
// Пишет только свои пробные имена в ключ PlayerPrefs РЕДАКТОРА и убирает их за собой.
public class PlayerPrefsLiveRegistryTests
{
    private string _name = "";

    private static string EditorPrefsPath =>
        "Software\\Unity\\UnityEditor\\" + Application.companyName + "\\" + Application.productName;

    private static string Hashed(string key)
    {
        uint hash = 5381;
        foreach (var b in Encoding.UTF8.GetBytes(key)) hash = (hash * 33) ^ b;
        return key + "_h" + hash;
    }

    [SetUp]
    public void SetUp() => _name = "KdLiveRegistryProbe" + Guid.NewGuid().ToString("N").Substring(0, 8);

    [TearDown]
    public void TearDown()
    {
        PlayerPrefs.DeleteKey(_name);
        PlayerPrefs.DeleteKey(_name + "Text");
        PlayerPrefs.Save();
    }

    [Test]
    public void ValueWrittenStraightIntoTheRegistry_IsVisibleToPlayerPrefsAtOnce()
    {
        PlayerPrefs.SetInt(_name, 1);
        using var key = CurrentUserRegistryKey.Open(EditorPrefsPath, writable: true);
        Assert.IsNotNull(key, "PlayerPrefs.SetInt не создал ключ HKCU\\" + EditorPrefsPath);
        Assert.IsTrue(key!.Values().Any(v => v.Name == Hashed(_name)),
            "имя значения в реестре — «ключ_h<djb2>»; без этого тест не знает, куда писать");

        key.Set(new RegistryValue(Hashed(_name), 4, BitConverter.GetBytes(2)));
        key.Set(new RegistryValue(Hashed(_name + "Text"), 3, Encoding.UTF8.GetBytes("Фитинг\0")));

        Assert.AreEqual(2, PlayerPrefs.GetInt(_name, -1), "перезаписанное значение: PlayerPrefs отдал кэш");
        Assert.AreEqual("Фитинг", PlayerPrefs.GetString(_name + "Text", "нет"),
            "новое значение, которого не было при старте, PlayerPrefs не увидел — перенос стал бы виден только со второго запуска");
    }
}
