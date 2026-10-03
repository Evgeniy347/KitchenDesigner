using System;
using System.Linq;
using System.Text;
using KitchenDesigner.Core;
using NUnit.Framework;

// Перенос PlayerPrefs DefaultCompany → Evgeniy347 на настоящем реестре, но только под
// HKCU\Software\KitchenDesignerTests\<guid>: настоящие ключи пользователя тест не видит.
// Значения повторяют то, что пишет Unity: DWORD для int, BINARY для строк, имя с хэшем
// «_h<djb2>», и имя из UTF-8 байтов кириллицы, прочитанных как ANSI — его надо перенести
// символ в символ, иначе PlayerPrefs.GetString по этому ключу вернёт значение по умолчанию.
public class RegistryPublisherStoreTests
{
    private const int Binary = 3;
    private const int DWord = 4;
    private const int QWord = 11;
    private const int Sz = 1;
    private const string Product = "KitchenDesigner2";

    private string _software = "";

    private string OldPath => _software + "\\DefaultCompany\\" + Product;
    private string NewPath => _software + "\\Evgeniy347\\" + Product;

    private static readonly RegistryValue[] UserValues =
    {
        new RegistryValue("KitchenFirstRunDone_h3441148672", DWord, BitConverter.GetBytes(1)),
        new RegistryValue("KitchenSettings_h3206251286", Binary, Encoding.UTF8.GetBytes("{\"gridStep\":10}\0")),
        new RegistryValue("KitchenSidebarPreset_Р¤РёС‚РёРЅРі_h2305417706", Binary, Encoding.UTF8.GetBytes("Фитинг\0")),
        new RegistryValue("Language_h3872303031", Binary, Encoding.UTF8.GetBytes("ru\0")),
        new RegistryValue("Screenmanager Resolution Width_h182942802", DWord, BitConverter.GetBytes(1920)),
        new RegistryValue("Wide_h1", QWord, BitConverter.GetBytes(0x0102030405060708L)),
        new RegistryValue("Text_h2", Sz, Encoding.Unicode.GetBytes("строка\0")),
        new RegistryValue("Empty_h3", Binary, new byte[0]),
    };

    [SetUp]
    public void SetUp() => _software = "Software\\KitchenDesignerTests\\publisher-" + Guid.NewGuid().ToString("N");

    [TearDown]
    public void TearDown() => CurrentUserRegistryKey.DeleteTree(_software);

    [OneTimeTearDown]
    public void DropTheTestsRoot() => CurrentUserRegistryKey.DeleteIfEmpty("Software\\KitchenDesignerTests");

    private RegistryPublisherStore Store() =>
        new RegistryPublisherStore(_software, PublisherRename.OldCompany, PublisherRename.NewCompany, Product);

    private static PublisherMigrationLog Quiet() => new PublisherMigrationLog(_ => { }, _ => { }, _ => { });

    private static void Seed(string path, params RegistryValue[] values)
    {
        using var key = CurrentUserRegistryKey.Create(path);
        foreach (var value in values) key.Set(value);
    }

    private static RegistryValue[] Read(string path)
    {
        using var key = CurrentUserRegistryKey.Open(path, writable: false);
        Assert.IsNotNull(key, "нет ключа HKCU\\" + path);
        return key!.Values().OrderBy(v => v.Name, StringComparer.Ordinal).ToArray();
    }

    private static bool Exists(string path)
    {
        using var key = CurrentUserRegistryKey.Open(path, writable: false);
        return key != null;
    }

    private static void AssertSameValues(RegistryValue[] expected, RegistryValue[] actual)
    {
        var want = expected.OrderBy(v => v.Name, StringComparer.Ordinal).ToArray();
        Assert.AreEqual(want.Select(v => v.Name), actual.Select(v => v.Name), "имена значений");
        for (int i = 0; i < want.Length; i++)
            Assert.IsTrue(want[i].SameAs(actual[i]), "значение " + want[i].Name + " перенеслось не байт в байт");
    }

    [Test]
    public void NewKeyMissing_EveryValueArrivesByteForByte_AndTheOldKeyIsGone()
    {
        Seed(OldPath, UserValues);
        Seed(OldPath + "\\Nested", new RegistryValue("Inner", DWord, BitConverter.GetBytes(7)));

        Assert.AreEqual(PublisherMoveOutcome.Moved, PublisherMigration.Run(Store(), Quiet()));

        AssertSameValues(UserValues, Read(NewPath));
        AssertSameValues(new[] { new RegistryValue("Inner", DWord, BitConverter.GetBytes(7)) }, Read(NewPath + "\\Nested"));
        Assert.IsFalse(Exists(OldPath), "старый ключ остался — при следующем запуске оба места были бы с данными");
        Assert.IsFalse(Exists(_software + "\\DefaultCompany"), "пустой родитель DefaultCompany должен уйти вместе с продуктом");
    }

    [Test]
    public void NewKeyHoldsOnlyWhatTheEngineWroteAtStartup_UserValuesStillMove()
    {
        Seed(OldPath, UserValues);
        Seed(NewPath,
            new RegistryValue("Screenmanager Resolution Width_h182942802", DWord, BitConverter.GetBytes(1024)),
            new RegistryValue("unity.player_session_count_h922449978", Binary, Encoding.UTF8.GetBytes("1\0")));

        Assert.AreEqual(PublisherMoveOutcome.Moved, PublisherMigration.Run(Store(), Quiet()),
            "движок создаёт новый ключ и пишет в него размер окна ДО первого скрипта — это не данные пользователя");

        var moved = Read(NewPath);
        AssertSameValues(UserValues, moved.Where(v => UserValues.Any(u => u.Name == v.Name)).ToArray());
        Assert.AreEqual(1920, BitConverter.ToInt32(moved.Single(v => v.Name.StartsWith("Screenmanager Resolution Width")).Data, 0),
            "размер окна пользователя важнее значения по умолчанию, которое движок записал минуту назад");
        Assert.IsFalse(Exists(OldPath));
    }

    [Test]
    public void SiblingProductUnderDefaultCompany_KeepsTheParent()
    {
        Seed(OldPath, UserValues);
        Seed(_software + "\\DefaultCompany\\OtherGame", new RegistryValue("x", DWord, BitConverter.GetBytes(1)));

        PublisherMigration.Run(Store(), Quiet());

        Assert.IsFalse(Exists(OldPath));
        Assert.IsTrue(Exists(_software + "\\DefaultCompany\\OtherGame"), "чужой проект под DefaultCompany трогать нельзя");
    }

    [Test]
    public void BothHoldData_NothingChanges()
    {
        var newer = new RegistryValue("KitchenSettings_h3206251286", Binary, Encoding.UTF8.GetBytes("{\"new\":1}\0"));
        Seed(OldPath, UserValues);
        Seed(NewPath, newer);

        Assert.AreEqual(PublisherMoveOutcome.LeftBothAlone, PublisherMigration.Run(Store(), Quiet()));

        AssertSameValues(UserValues, Read(OldPath));
        AssertSameValues(new[] { newer }, Read(NewPath));
    }

    [Test]
    public void SecondRun_FindsNothingToMove_AndChangesNothing()
    {
        Seed(OldPath, UserValues);
        PublisherMigration.Run(Store(), Quiet());

        Assert.AreEqual(PublisherMoveOutcome.NothingToMove, PublisherMigration.Run(Store(), Quiet()));
        AssertSameValues(UserValues, Read(NewPath));
    }

    [Test]
    public void NoOldKey_DoesNotCreateTheNewOne()
    {
        Assert.AreEqual(PublisherMoveOutcome.NothingToMove, PublisherMigration.Run(Store(), Quiet()));
        Assert.IsFalse(Exists(NewPath), "чистая установка: переносить нечего, и ключ создавать незачем");
    }
}
