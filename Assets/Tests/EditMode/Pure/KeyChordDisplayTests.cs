using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.Keybinding;
using KitchenDesigner.Core.UI;
using KitchenDesigner.Tests.Geometry;

/// <summary>
/// У аккорда ДВА представления, и их нельзя путать: `KeyChord.Format` — это формат
/// ХРАНЕНИЯ (он же уходит в json, и `KeyChord.TryParse` разбирает его обратно), а
/// `KeyChordDisplay` — только подпись на кнопке. Поводом была строка «KeypadPlus»
/// среди русских подписей; соблазн — поправить саму таблицу `KeyChord`, и это молча
/// переписало бы формат сохранённых проектов.
///
/// Поэтому проверок три вида: хранение не поменялось (round-trip и английское имя в
/// строке), отображение читается по-русски, и — сенсор — эти две строки для нумпада
/// РАЗЛИЧАЮТСЯ. Без сенсора тест про хранение зеленел бы и в том случае, если бы
/// отображение случайно стало храниться.
/// </summary>
public class KeyChordDisplayTests
{
    private static readonly KeyCode[] NumpadKeys =
    {
        KeyCode.Keypad0, KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3, KeyCode.Keypad4,
        KeyCode.Keypad5, KeyCode.Keypad6, KeyCode.Keypad7, KeyCode.Keypad8, KeyCode.Keypad9,
        KeyCode.KeypadPeriod, KeyCode.KeypadDivide, KeyCode.KeypadMultiply, KeyCode.KeypadMinus,
        KeyCode.KeypadPlus, KeyCode.KeypadEnter, KeyCode.KeypadEquals,
    };

    [Test]
    public void EveryNumpadKey_HasAReading()
    {
        var missing = NumpadKeys
            .Where(k => !KeyChordDisplay.TryReadingOf(k, out _))
            .ToList();

        CollectionAssert.IsEmpty(missing,
            "нумпад читается целиком, а не только Plus и Minus — иначе английское имя "
            + "клавиши всплывёт на первой же непокрытой: " + string.Join(", ", missing));
    }

    [Test]
    public void NumpadKeys_ReadAsNumPrefixedKeycaps()
    {
        Assert.AreEqual("Num +", KeyChordDisplay.Of(new KeyChord(KeyCode.KeypadPlus)));
        Assert.AreEqual("Num -", KeyChordDisplay.Of(new KeyChord(KeyCode.KeypadMinus)));
        Assert.AreEqual("Num Enter", KeyChordDisplay.Of(new KeyChord(KeyCode.KeypadEnter)));
        Assert.AreEqual("Num 7", KeyChordDisplay.Of(new KeyChord(KeyCode.Keypad7)));
        Assert.AreEqual("Num .", KeyChordDisplay.Of(new KeyChord(KeyCode.KeypadPeriod)));
    }

    [Test]
    public void Display_AndStorage_DifferForTheNumpad()
    {
        var sameBoth = NumpadKeys
            .Where(k => KeyChordDisplay.Of(new KeyChord(k)) == KeyChord.Format(new KeyChord(k)))
            .ToList();

        CollectionAssert.IsEmpty(sameBoth,
            "сенсор: для нумпада подпись и хранимая строка обязаны быть РАЗНЫМИ. Совпали — "
            + "значит либо отображение не подменяется (подпись осталась английской), либо "
            + "подменили сам формат хранения: " + string.Join(", ", sameBoth));
    }

    [Test]
    public void Storage_OfTheNumpad_StillRoundTripsThroughTheEnglishName()
    {
        foreach (var key in NumpadKeys)
        {
            var chord = new KeyChord(key, ctrl: true, shift: true);
            string stored = KeyChord.Format(chord);

            StringAssert.Contains(key.ToString(), stored,
                "в json по-прежнему уходит английское имя клавиши: " + stored);
            Assert.IsTrue(KeyChord.TryParse(stored, out var parsed), "не разобралось: " + stored);
            Assert.AreEqual(chord, parsed, "round-trip хранения поехал на " + key);
        }
    }

    [Test]
    public void Display_KeepsTheModifierPrefix_OwnedByTheStorageFormatter()
    {
        var chord = new KeyChord(KeyCode.KeypadPlus, ctrl: true, alt: true, shift: true);
        string stored = KeyChord.Format(chord);
        string shown = KeyChordDisplay.Of(chord);

        string prefix = stored.Substring(0, stored.Length - KeyCode.KeypadPlus.ToString().Length);
        Assert.AreEqual(prefix + "Num +", shown,
            "приставка модификаторов у подписи ровно та же, что у хранения: второй список "
            + "«Ctrl+/Alt+/Shift+» разъехался бы с первым");
    }

    [Test]
    public void EveryOtherKey_IsShownExactlyAsItIsStored()
    {
        var chords = new[]
        {
            new KeyChord(KeyCode.W),
            new KeyChord(KeyCode.F9, shift: true),
            new KeyChord(KeyCode.Z, ctrl: true),
            new KeyChord(KeyCode.LeftArrow),
            new KeyChord(KeyCode.Minus),
            new KeyChord(KeyCode.Alpha1),
        };

        foreach (var chord in chords)
            Assert.AreEqual(KeyChord.Format(chord), KeyChordDisplay.Of(chord),
                "всё, что не нумпад, подпись отдаёт делегированием — без второй таблицы клавиш");
    }

    [Test]
    public void EmptyChord_ShowsNothing()
    {
        Assert.AreEqual(string.Empty, KeyChordDisplay.Of(KeyChord.Empty));
    }

    /// <summary>Главный запрет этого файла, и проверить его можно только сканом: подпись
    /// не имеет права попасть в сохранение. Если `KeyChordDisplay` однажды позовут из
    /// сериализации настроек, проект начнёт сохраняться строкой «Num +», которую
    /// `KeyChord.TryParse` не разберёт, и привязка молча вернётся к дефолтной.</summary>
    [Test]
    public void TheDisplayFormatter_IsUsedByTheUiOnly_NeverBySaveOrLoad()
    {
        var core = RepoPaths.Subdir("Assets", "Scripts", "Core");
        var callers = new List<string>();

        foreach (var file in SourceCorpus.Files(core))
        {
            string name = Path.GetFileName(file);
            if (name == "KeyChordDisplay.cs") continue;
            if (SourceCorpus.Text(file).Contains("KeyChordDisplay")) callers.Add(name);
        }

        Assert.That(callers.Count, Is.GreaterThan(0),
            "скан не нашёл НИ ОДНОГО потребителя подписи — значит он смотрит не туда и "
            + "запрет ниже зеленеет вхолостую");
        CollectionAssert.DoesNotContain(callers, "KitchenSettings.Input.cs",
            "сохранение привязок обязано звать KeyChord.Format, а не подпись для кнопки");

        var offenders = callers.Where(IsASaveOrLoadFile).ToList();
        CollectionAssert.IsEmpty(offenders,
            "подпись для кнопки попала в путь сохранения/загрузки: " + string.Join(", ", offenders));
    }

    private static bool IsASaveOrLoadFile(string fileName) =>
        fileName.Contains("Save") || fileName.Contains("Load") || fileName.Contains("Persist")
        || fileName.Contains("Capture") && fileName.Contains("Element")
        || fileName.Contains("Json") || fileName.Contains("Data") || fileName.Contains("Restorer");
}
